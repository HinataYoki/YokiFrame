using YokiFrame;

namespace YokiFrame.EngineKit.Tests;

public sealed partial class LiveCodeTests
{
    [Fact]
    public void ExportMany_shares_source_but_keeps_per_target_fields_without_loading_or_importing()
    {
        using var bed = new LiveBed(exports: true);
        bed.Run(async () =>
        {
            await bed.Manager.Attach("First", new object(), "Shared", "public int Value = 7;", bed.Guard, default);
            await bed.Manager.Attach("Second", new object(), "Shared", "public int Value = 7;", bed.Guard, default);
            bed.Manager.SetField("Second", "Value", 23, bed.Guard);
            int loaded = bed.Budget.LoadedCount;
            var batch = await bed.Manager.ExportMany(new[]
            {
                new YokiFrameLiveExportRequest("First", "Assets/Shared.cs"),
                new YokiFrameLiveExportRequest("Second", "Assets/Shared.cs")
            }, null!, bed.Guard, default);
            Assert.Equal("Prepared", batch.State);
            Assert.Equal(loaded, bed.Budget.LoadedCount);
            var store = new YokiFrameLiveExportStore(bed.Root);
            var version = store.ReadVersion(Assert.Single(batch.ExportIds));
            Assert.Equal(2, version.Targets.Count);
            Assert.Contains("7", version.Targets[0].Fields);
            Assert.Contains("23", version.Targets[1].Fields);
            Assert.False(Directory.Exists(Path.Combine(bed.Root, "Assets")));
        });
    }

    [Fact]
    public void Reexport_works_in_editor_with_no_live_handle_and_does_not_load()
    {
        using var bed = new LiveBed(exports: true);
        bed.Run(async () =>
        {
            await bed.Attach("public int Value = 7;");
            string first = await bed.Manager.Export("Probe", "Assets/Probe.cs", bed.Guard, default);
            ((ExportFakeHost)bed.Host).CompleteCommit!();
            bed.Manager.Clear(_ => { });
            bed.State.ActiveTarget = "editor";
            var store = new YokiFrameLiveExportStore(bed.Root);
            var previous = store.ReadVersion(first);
            File.WriteAllText(store.ScriptPath(previous) + ".meta", "guid: stable");
            int loaded = bed.Budget.LoadedCount;
            var batch = await bed.Manager.Reexport(first, "public int Value = 9;", null!, bed.Guard, default);
            Assert.Equal("Prepared", batch.State);
            Assert.Equal(loaded, bed.Budget.LoadedCount);
            Assert.Empty(bed.Manager.List());
            Assert.Equal(first, store.ReadVersion(batch.ExportIds[0]).PreviousExportId);
        });
    }

    [Fact]
    public void Export_conflicting_shared_sources_do_not_stage_any_files()
    {
        using var bed = new LiveBed(exports: true);
        bed.Run(async () =>
        {
            await bed.Manager.Attach("First", new object(), "Shared", "public int Value = 1;", bed.Guard, default);
            await bed.Manager.Attach("Second", new object(), "Shared", "public int Value = 2;", bed.Guard, default);
            await Assert.ThrowsAsync<ArgumentException>(() => bed.Manager.ExportMany(new[]
            {
                new YokiFrameLiveExportRequest("First", "Assets/Shared.cs"),
                new YokiFrameLiveExportRequest("Second", "Assets/Shared.cs")
            }, null!, bed.Guard, default));
            Assert.False(Directory.Exists(Path.Combine(bed.Root, ".yokiframe/engine/live-exports")));
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Export_cancel_or_session_change_during_compile_does_not_stage(bool session)
    {
        using var bed = new LiveBed(exports: true);
        bed.Run(async () =>
        {
            await bed.Attach("public int Value = 7;");
            using var token = new CancellationTokenSource();
            var pending = bed.Manager.ExportMany(new[] { new YokiFrameLiveExportRequest("Probe", "Assets/Probe.cs") },
                null!, bed.Guard, token.Token);
            if (session) bed.State.SessionId = "changed";
            else token.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
            Assert.False(Directory.Exists(Path.Combine(bed.Root, ".yokiframe/engine/live-exports")));
        });
    }

    [Fact]
    public void Commit_outlives_submitter_but_rechecks_session_before_writing()
    {
        using var bed = new LiveBed(exports: true);
        bed.Run(async () =>
        {
            await bed.Attach("public int Value = 7;");
            var batch = await bed.Manager.ExportMany(new[] { new YokiFrameLiveExportRequest("Probe", "Assets/Probe.cs") },
                null!, bed.Guard, default);
            bool scriptAlive = true;
            bed.Manager.CommitExport(batch.BatchId, () => Assert.True(scriptAlive));
            scriptAlive = false;
            var host = (ExportFakeHost)bed.Host;
            bed.State.SessionId = "changed";
            Assert.Throws<OperationCanceledException>(() => host.CompleteCommit!());
            Assert.False(Directory.Exists(Path.Combine(bed.Root, "Assets")));
            bed.State.SessionId = "session-1";
            host.CompleteCommit!();
            Assert.True(File.Exists(Path.Combine(bed.Root, "Assets/Probe.cs")));
        });
    }

    private sealed class ExportFakeHost : FakeHost, IYokiFrameLiveExportHost
    {
        private readonly YokiFrameLiveExportStore mStore;
        public Action? CompleteCommit { get; private set; }
        public ExportFakeHost(string root) { mStore = new(root); }
        public YokiFrameLiveExportTarget CaptureExportTarget(IDisposable attachment) => new()
        {
            TargetId = Guid.NewGuid().ToString("N"), ScenePath = "scene",
            Fields = "{\"Value\":" + ReadField(attachment, "Value") + "}"
        };
        public string VersionExportSource(string source, string exportId) => source;
        public YokiFrameLiveExportVersion ReadExport(string id) => mStore.ReadVersion(id);
        public YokiFrameLiveExportVersion CreateReexport(string id, string members, string batchId)
        {
            var old = mStore.ReadVersion(id);
            string source = WrapBehaviour(old.ClassName, members, true);
            return new YokiFrameLiveExportVersion
            {
                ExportId = Guid.NewGuid().ToString("N"), BatchId = batchId, PreviousExportId = id,
                ClassName = old.ClassName, SourcePath = old.SourcePath, Source = source,
                SourceHash = YokiFrameLiveCodeManager.Hash(source), PreviousMetaHash = mStore.MetaHash(old)
            };
        }
        public YokiFrameLiveExportBatch StageExports(string id, IReadOnlyList<YokiFrameLiveExportVersion> versions) => mStore.Stage(id, versions);
        public void QueueExportCommit(string id, Action guard)
        {
            guard();
            mStore.MarkQueued(id);
            CompleteCommit = () => { guard(); mStore.Commit(id); };
        }
    }
}
