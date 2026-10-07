using System.Text.Json;
using YokiFrame;

namespace YokiFrame.RoslynKit.Tests;

public sealed class LiveExportStoreTests : IDisposable
{
    private readonly string mRoot = Path.Combine(Path.GetTempPath(), "yokiframe-export-tests", Guid.NewGuid().ToString("N"));
    private LiveExportStore Store => new(mRoot);

    [Fact]
    public void Stage_writes_no_assets_and_query_does_not_advance_state()
    {
        var versions = new[] { Version("One"), Version("Two") };
        versions[1].BatchId = versions[0].BatchId;
        var batch = Store.Stage(versions[0].BatchId, versions);
        Assert.Equal("Prepared", batch.State);
        Assert.False(Directory.Exists(Path.Combine(mRoot, "Assets")));
        byte[] before = File.ReadAllBytes(Store.BatchPath(batch.BatchId));
        for (int index = 0; index < 4; index++) Assert.Equal("Prepared", Store.ReadBatch(batch.BatchId).State);
        Assert.Equal(before, File.ReadAllBytes(Store.BatchPath(batch.BatchId)));
        Assert.Throws<InvalidOperationException>(() => Store.Commit(batch.BatchId));
        Assert.True(Store.MarkQueued(batch.BatchId));
        Assert.Equal("Committed", Store.Commit(batch.BatchId).State);
        Assert.False(Store.MarkQueued(batch.BatchId));
        foreach (var item in versions) Assert.Equal(item.Source, File.ReadAllText(Store.ScriptPath(item)));
    }

    [Fact]
    public void Reexport_preserves_metadata_and_archives_both_sources()
    {
        var original = Version("One");
        Commit(original);
        File.WriteAllText(Store.ScriptPath(original) + ".meta", "guid: preserved");
        var next = Revision(original, "public int Value = 9;");
        Store.Stage(next.BatchId, new[] { next });
        Store.MarkQueued(next.BatchId);
        Assert.Equal("Committed", Store.Commit(next.BatchId).State);
        Assert.Equal("guid: preserved", File.ReadAllText(Store.ScriptPath(original) + ".meta"));
        Assert.Equal(original.Source, Store.ReadVersion(original.ExportId).Source);
        Assert.Equal(next.Source, Store.ReadVersion(next.ExportId).Source);
        var stale = Revision(original, "stale");
        Assert.Throws<IOException>(() => Store.Stage(stale.BatchId, new[] { stale }));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Concurrent_source_or_metadata_edit_blocks_whole_batch(bool metadata)
    {
        var original = Version("One");
        Commit(original);
        File.WriteAllText(Store.ScriptPath(original) + ".meta", "guid: original");
        var next = Revision(original, "new source");
        var extra = Version("Two");
        extra.BatchId = next.BatchId;
        Store.Stage(next.BatchId, new[] { next, extra });
        string changed = Store.ScriptPath(original) + (metadata ? ".meta" : "");
        File.WriteAllText(changed, "user edit");
        Store.MarkQueued(next.BatchId);
        Assert.Equal("Failed", Store.Commit(next.BatchId).State);
        Assert.Equal("user edit", File.ReadAllText(changed));
        Assert.False(File.Exists(Store.ScriptPath(extra)));
        Assert.Throws<InvalidOperationException>(() => Store.MarkQueued(next.BatchId));
    }

    [Fact]
    public void Mid_commit_failure_rolls_back_only_written_files()
    {
        var one = Version("One");
        var two = Version("Two");
        two.BatchId = one.BatchId;
        Store.Stage(one.BatchId, new[] { one, two });
        Directory.CreateDirectory(Store.ScriptPath(two));
        Store.MarkQueued(one.BatchId);
        Assert.Equal("Failed", Store.Commit(one.BatchId).State);
        Assert.False(File.Exists(Store.ScriptPath(one)));
        Assert.True(Directory.Exists(Store.ScriptPath(two)));
    }

    [Fact]
    public void Shared_source_retains_distinct_target_field_snapshots()
    {
        var version = Version("Shared");
        version.Targets.Add(new() { TargetId = "A", ScenePath = "scene", Fields = "{\"Value\":10}" });
        version.Targets.Add(new() { TargetId = "B", ScenePath = "scene", Fields = "{\"Value\":20}" });
        Store.Stage(version.BatchId, new[] { version });
        var saved = Store.ReadVersion(version.ExportId);
        Assert.Equal(2, saved.Targets.Count);
        Assert.NotEqual(saved.Targets[0].Fields, saved.Targets[1].Fields);
    }

    [Fact]
    public void Duplicated_classes_or_targets_are_rejected_before_staging()
    {
        var one = Version("One");
        var two = Version("One");
        two.BatchId = one.BatchId;
        two.SourcePath = "Assets/Other/One.cs";
        Assert.Throws<ArgumentException>(() => Store.Stage(one.BatchId, new[] { one, two }));
        Assert.False(Directory.Exists(Path.Combine(mRoot, "Assets")));
        one.Targets.Add(new() { TargetId = "A", ScenePath = "scene", Fields = "{}" });
        one.Targets.Add(new() { TargetId = "A", ScenePath = "scene", Fields = "{}" });
        Assert.Throws<ArgumentException>(() => Store.Stage(one.BatchId, new[] { one }));
    }

    [Fact]
    public void Legacy_record_is_unchanged_and_its_source_is_archived_before_update()
    {
        var original = Version("Legacy");
        Directory.CreateDirectory(Path.GetDirectoryName(Store.ScriptPath(original))!);
        Directory.CreateDirectory(Path.GetDirectoryName(Store.RecordPath(original.ExportId))!);
        File.WriteAllText(Store.ScriptPath(original), original.Source);
        File.WriteAllText(Store.ScriptPath(original) + ".meta", "guid: legacy");
        string legacy = JsonSerializer.Serialize(new
        {
            schema = 1, exportId = original.ExportId, className = original.ClassName, sourcePath = original.SourcePath,
            sourceHash = original.SourceHash, targetId = "legacy-target", scenePath = "scene", fields = new { values = Array.Empty<object>() }
        });
        File.WriteAllText(Store.RecordPath(original.ExportId), legacy);
        var next = Revision(original, "changed");
        Commit(next);
        Assert.Equal(legacy, File.ReadAllText(Store.RecordPath(original.ExportId)));
        Assert.Equal(original.Source, Store.ReadVersion(original.ExportId).Source);
    }

    [Fact]
    public void Duplicate_export_ids_are_rejected_before_writing_any_records()
    {
        var one = Version("One");
        var two = Version("Two");
        two.BatchId = one.BatchId;
        two.ExportId = one.ExportId;
        Assert.Throws<ArgumentException>(() => Store.Stage(one.BatchId, new[] { one, two }));
        Assert.False(File.Exists(Store.RecordPath(one.ExportId)));
    }

    [Theory]
    [InlineData("batchId", "../outside")]
    [InlineData("previousExportId", "../outside")]
    [InlineData("sourcePath", "Packages/One.cs")]
    public void Version_reads_revalidate_identity_and_path_boundaries(string key, string value)
    {
        var version = Version("One");
        Store.Stage(version.BatchId, new[] { version });
        string path = Store.RecordPath(version.ExportId);
        var record = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(path))!;
        record[key] = value;
        File.WriteAllText(path, record.ToJsonString());
        Assert.ThrowsAny<Exception>(() => Store.ReadVersion(version.ExportId));
    }

    [Fact]
    public void Version_reads_revalidate_duplicate_targets_and_field_size()
    {
        var version = Version("One");
        version.Targets.Add(new() { TargetId = "A", ScenePath = "scene", Fields = "{}" });
        Store.Stage(version.BatchId, new[] { version });
        string path = Store.RecordPath(version.ExportId);
        var record = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(path))!;
        var targets = record["targets"]!.AsArray();
        targets.Add(targets[0]!.DeepClone());
        File.WriteAllText(path, record.ToJsonString());
        Assert.Throws<ArgumentException>(() => Store.ReadVersion(version.ExportId));
        targets.RemoveAt(1);
        targets[0]!["fields"] = new string('x', 64 * 1024 + 1);
        File.WriteAllText(path, record.ToJsonString());
        Assert.Throws<InvalidDataException>(() => Store.ReadVersion(version.ExportId));
    }

    [Fact]
    public void Incomplete_commit_is_not_replayed_and_cross_project_records_are_rejected()
    {
        var version = Version("One");
        Store.Stage(version.BatchId, new[] { version });
        string path = Store.BatchPath(version.BatchId);
        File.WriteAllText(path, File.ReadAllText(path).Replace("\"Prepared\"", "\"Committing\""));
        Assert.Equal("Committing", Store.ReadBatch(version.BatchId).State);
        Assert.Throws<InvalidOperationException>(() => Store.MarkQueued(version.BatchId));
        string otherRoot = Path.Combine(mRoot, "other");
        var other = new LiveExportStore(otherRoot);
        Directory.CreateDirectory(Path.GetDirectoryName(other.RecordPath(version.ExportId))!);
        File.Copy(Store.RecordPath(version.ExportId), other.RecordPath(version.ExportId));
        Assert.Throws<InvalidDataException>(() => other.ReadVersion(version.ExportId));
    }

    [Theory]
    [InlineData("Assets/../One.cs")]
    [InlineData("Assets/Wrong.cs")]
    [InlineData("Packages/One.cs")]
    public void Paths_remain_bounded(string path)
    {
        var version = Version("One");
        version.SourcePath = path;
        Assert.ThrowsAny<Exception>(() => Store.Stage(version.BatchId, new[] { version }));
    }

    private void Commit(LiveExportVersion version)
    {
        Store.Stage(version.BatchId, new[] { version });
        Store.MarkQueued(version.BatchId);
        Assert.Equal("Committed", Store.Commit(version.BatchId).State);
    }

    private LiveExportVersion Revision(LiveExportVersion previous, string source)
    {
        var next = Version(previous.ClassName);
        next.PreviousExportId = previous.ExportId;
        next.Source = source;
        next.SourceHash = LiveCodeManager.Hash(source);
        next.PreviousMetaHash = Store.MetaHash(previous);
        return next;
    }

    private static LiveExportVersion Version(string className)
    {
        string source = "public sealed class " + className + " { public int Value = 1; }";
        return new LiveExportVersion
        {
            ExportId = Guid.NewGuid().ToString("N"), BatchId = Guid.NewGuid().ToString("N"),
            ClassName = className, SourcePath = "Assets/" + className + ".cs",
            Source = source, SourceHash = LiveCodeManager.Hash(source)
        };
    }

    public void Dispose() { if (Directory.Exists(mRoot)) Directory.Delete(mRoot, true); }
}
