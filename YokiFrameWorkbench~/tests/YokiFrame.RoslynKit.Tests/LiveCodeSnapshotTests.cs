using System.Text.Json.Nodes;
using YokiFrame;

namespace YokiFrame.RoslynKit.Tests;

public sealed partial class LiveCodeTests
{
    private const string SnapshotSource = "public int Value = 7;";

    [Fact]
    public void Snapshot_command_persists_after_budget_exhaustion_without_loading_or_invoking()
    {
        using var bed = new LiveBed();
        bed.Run(async () =>
        {
            await bed.Attach(SnapshotSource);
            while (bed.Budget.LoadedCount < RoslynLoadBudget.MaxAssemblies) bed.Budget.Reserve(0);
            bed.Host.Events.Clear();
            var operation = new LiveCodeOperation(bed.Manager, "live_snapshot");
            Assert.Equal(YokiFrameCommandKind.UserAction, operation.Descriptor.Kind);
            Assert.False(operation.Descriptor.ExemptFromExecutionSwitch);
            var response = operation.Execute(new YokiFrameCommandRequest("cli", "RoslynKit", "live_snapshot",
                "{\"target\":\"play\"}", 30000, 0));
            Assert.True(response.IsSuccess);
            var payload = JsonNode.Parse(response.ResultJson)!;
            var snapshot = bed.Manager.ReadSnapshot(payload["snapshotId"]!.GetValue<string>(), bed.Guard);
            Assert.DoesNotContain("fields", response.ResultJson);
            Assert.True(snapshot.Complete);
            Assert.Empty(bed.Host.Events);
            Assert.Equal(RoslynLoadBudget.MaxAssemblies, bed.Budget.LoadedCount);
            var reloaded = new LiveSnapshotStore(bed.Root).Read(snapshot.SnapshotId);
            Assert.Equal("7", Assert.Single(reloaded.Entries).State.Fields);
            Assert.Equal(snapshot.SessionId, reloaded.SessionId);
        });
    }

    [Fact]
    public void Persisted_snapshot_restores_in_a_new_manager_with_a_new_session()
    {
        using var bed = new LiveBed();
        bed.Run(async () =>
        {
            await bed.Attach(SnapshotSource);
            bed.Manager.SetField("Probe", "Value", 42, bed.Guard);
            var snapshot = bed.Manager.Snapshot(bed.Guard);
            bed.Manager.Clear(_ => Assert.Fail());
            var budget = new RoslynLoadBudget();
            var state = new RoslynDomainState { ActiveTarget = "play", SessionIdentityAvailable = true,
                SessionId = "new-domain", Generation = 2 };
            var manager = new LiveCodeManager(new RoslynCompilerLoader(bed.Root), budget,
                null!, bed.Host, () => state, () => true, bed.Root);
            try
            {
                bed.Host.Events.Clear();
                var result = await manager.Restore(snapshot.SnapshotId, item =>
                {
                    Assert.Equal("Probe", item.Id);
                    Assert.Equal(LiveCodeManager.Hash(SnapshotSource), item.SourceHash);
                    return SnapshotSource;
                }, _ => new object(), bed.Guard, default);
                Assert.True(result.Success, result.Error);
                Assert.Equal(42, manager.ReadField("Probe", "Value", bed.Guard));
                Assert.Equal("new-domain", Assert.Single(manager.List()).SessionId);
                Assert.Equal(new[] { "resolve", "prepare", "fields", "activate" }, bed.Host.Events);
                Assert.Equal(1, budget.LoadedCount);
                Assert.True(result.RequiredBytes > 0);
            }
            finally { manager.Clear(_ => Assert.Fail()); }
        });
    }

    [Fact]
    public void Whole_batch_fields_are_restored_and_published_before_first_activation()
    {
        using var bed = new LiveBed();
        bed.Run(async () =>
        {
            await bed.Attach(SnapshotSource);
            await bed.Manager.Attach("Second", new object(), "Second", SnapshotSource, bed.Guard, default);
            bed.Manager.SetField("Second", "Value", 99, bed.Guard);
            var snapshot = bed.Manager.Snapshot(bed.Guard);
            bed.Manager.Clear(_ => Assert.Fail());
            bed.Host.Events.Clear();
            bed.Host.OnActivate = () =>
            {
                Assert.Equal(2, bed.Manager.List().Count);
                Assert.Equal(99, bed.Manager.ReadField("Second", "Value", bed.Guard));
            };
            var result = await bed.Manager.Restore(snapshot.SnapshotId, _ => SnapshotSource, _ => new object(), bed.Guard, default);
            Assert.True(result.Success, result.Error);
            Assert.Equal(new[] { "resolve", "resolve", "prepare", "prepare", "fields", "fields", "activate", "activate" }, bed.Host.Events);
        });
    }

    [Theory]
    [InlineData("hash")]
    [InlineData("count")]
    [InlineData("bytes")]
    [InlineData("target")]
    [InlineData("session")]
    [InlineData("cancel")]
    [InlineData("permission")]
    [InlineData("compile")]
    public void Restore_rejects_preflight_failure_before_loading_or_resolving(string failure)
    {
        using var bed = new LiveBed();
        bed.Run(async () =>
        {
            await bed.Attach(SnapshotSource);
            var snapshot = bed.Manager.Snapshot(bed.Guard);
            bed.Manager.Clear(_ => Assert.Fail());
            if (failure == "count")
                while (bed.Budget.LoadedCount < RoslynLoadBudget.MaxAssemblies) bed.Budget.Reserve(0);
            if (failure == "bytes") bed.Budget.Reserve(RoslynLoadBudget.MaxLoadedBytes - bed.Budget.LoadedBytes - 1);
            if (failure == "target") bed.State.ActiveTarget = "editor";
            if (failure == "compile") RewriteSnapshot(bed, snapshot.SnapshotId, root =>
                root["entries"]![0]!["sourceHash"] = LiveCodeManager.Hash("invalid source"));
            int count = bed.Budget.LoadedCount;
            bed.Host.Events.Clear();
            using var cancellation = new CancellationTokenSource();
            if (failure == "cancel") cancellation.Cancel();
            var result = await bed.Manager.Restore(snapshot.SnapshotId, _ =>
            {
                if (failure == "session") bed.State.SessionId = "changed";
                if (failure == "permission") bed.Permitted = false;
                return failure == "hash" ? "changed" : failure == "compile" ? "invalid source" : SnapshotSource;
            }, _ => throw new Exception("must not resolve"), bed.Guard, cancellation.Token);
            Assert.False(result.Success);
            Assert.NotEmpty(result.Error);
            Assert.Equal(count, bed.Budget.LoadedCount);
            Assert.Empty(bed.Host.Events);
            Assert.Empty(bed.Manager.List());
        });
    }

    [Theory]
    [InlineData("resolve")]
    [InlineData("fields")]
    [InlineData("activate")]
    public void Restore_failure_reports_stage_and_cleans_up_new_attachments(string stage)
    {
        using var bed = new LiveBed();
        bed.Run(async () =>
        {
            await bed.Attach(SnapshotSource);
            var snapshot = bed.Manager.Snapshot(bed.Guard);
            bed.Manager.Clear(_ => Assert.Fail());
            bed.Host.FailResolve = stage == "resolve";
            bed.Host.FailFields = stage == "fields";
            bed.Host.FailActivation = stage == "activate";
            bed.Host.Events.Clear();
            int loaded = bed.Budget.LoadedCount;
            var result = await bed.Manager.Restore(snapshot.SnapshotId, _ => SnapshotSource, _ => new object(), bed.Guard, default);
            Assert.False(result.Success);
            Assert.Equal(stage, result.Stage);
            Assert.NotEmpty(result.Error);
            Assert.Empty(bed.Manager.List());
            if (stage == "resolve") Assert.Equal(loaded, bed.Budget.LoadedCount);
            else
            {
                Assert.Contains("dispose", bed.Host.Events);
                Assert.Equal("rolledBack", Assert.Single(result.Items).Status);
            }
        });
    }

    [Fact]
    public void Faulted_and_oversized_fields_are_explicitly_incomplete_and_cannot_restore()
    {
        using var bed = new LiveBed();
        bed.Run(async () =>
        {
            await bed.Attach(SnapshotSource);
            bed.Host.SnapshotFields = new string('x', LiveSnapshotStore.MaxFieldBytes + 1);
            var large = bed.Manager.Snapshot(bed.Guard);
            Assert.False(large.Complete);
            Assert.NotEmpty(Assert.Single(large.Entries).Error);
            bed.Host.Latest!.Alive = false;
            var snapshot = bed.Manager.Snapshot(bed.Guard);
            Assert.False(snapshot.Complete);
            Assert.Contains("faulted", Assert.Single(snapshot.Entries).Error);
            bed.Manager.Clear(_ => Assert.Fail());
            var result = await bed.Manager.Restore(snapshot.SnapshotId, _ => throw new Exception("not called"),
                _ => new object(), bed.Guard, default);
            Assert.False(result.Success);
            Assert.Contains("incomplete", result.Error);
        });
    }

    [Fact]
    public void Existing_ids_and_concurrent_changes_are_not_overwritten()
    {
        using var bed = new LiveBed();
        bed.Run(async () =>
        {
            await bed.Attach(SnapshotSource);
            var snapshot = bed.Manager.Snapshot(bed.Guard);
            var conflict = await bed.Manager.Restore(snapshot.SnapshotId, _ => SnapshotSource, _ => new object(), bed.Guard, default);
            Assert.False(conflict.Success);
            Assert.Equal(7, bed.Manager.ReadField("Probe", "Value", bed.Guard));
            bed.Manager.Clear(_ => Assert.Fail());
            var pending = bed.Manager.Restore(snapshot.SnapshotId, _ => SnapshotSource, _ => new object(), bed.Guard, default);
            Assert.Throws<InvalidOperationException>(() => bed.Manager.Snapshot(bed.Guard));
            await Assert.ThrowsAsync<InvalidOperationException>(() => bed.Attach(SnapshotSource));
            bed.Manager.Clear(_ => Assert.Fail());
            var stale = await pending;
            Assert.False(stale.Success);
            Assert.Empty(bed.Manager.List());
        });
    }

    [Fact]
    public void Shared_reference_keys_resolve_once_after_all_targets_and_before_fields()
    {
        using var bed = new LiveBed();
        bed.Run(async () =>
        {
            await bed.Attach(SnapshotSource);
            await bed.Manager.Attach("Second", new object(), "Second", SnapshotSource, bed.Guard, default);
            var snapshot = bed.Manager.Snapshot(bed.Guard);
            RewriteSnapshot(bed, snapshot.SnapshotId, root =>
            {
                foreach (var entry in root["entries"]!.AsArray())
                {
                    entry!["references"]!.AsArray().Add(new JsonObject
                    {
                        ["field"] = "Peer", ["object"] = root["entries"]![1]!["object"]!.DeepClone()
                    });
                    var extra = root["entries"]![1]!["object"]!.DeepClone();
                    extra["key"] = "ExtraReference";
                    entry["references"]!.AsArray().Add(new JsonObject { ["field"] = "Extra", ["object"] = extra });
                }
            });
            bed.Manager.Clear(_ => Assert.Fail());
            var keys = new List<string>();
            var peer = new object();
            var extraObject = new object();
            bed.Host.OnRestoreFields = references =>
            {
                Assert.Same(peer, references["Peer"]);
                Assert.Same(extraObject, references["Extra"]);
            };
            var result = await bed.Manager.Restore(snapshot.SnapshotId, _ => SnapshotSource, identity =>
            {
                keys.Add(identity.Key);
                return identity.Key == "Second:target" ? peer : identity.Key == "ExtraReference" ? extraObject : new object();
            }, bed.Guard, default);
            Assert.True(result.Success, result.Error);
            Assert.Equal(new[] { "Probe:target", "Second:target", "ExtraReference" }, keys);
        });
    }

    [Fact]
    public void Missing_field_reference_fails_before_loading_even_when_target_resolves()
    {
        using var bed = new LiveBed();
        bed.Run(async () =>
        {
            await bed.Attach(SnapshotSource);
            var snapshot = bed.Manager.Snapshot(bed.Guard);
            RewriteSnapshot(bed, snapshot.SnapshotId, root =>
            {
                var identity = root["entries"]![0]!["object"]!.DeepClone();
                identity["key"] = "MissingPeer";
                root["entries"]![0]!["references"]!.AsArray().Add(new JsonObject { ["field"] = "Peer", ["object"] = identity });
            });
            bed.Manager.Clear(_ => Assert.Fail());
            int count = bed.Budget.LoadedCount;
            bed.Host.Events.Clear();
            var result = await bed.Manager.Restore(snapshot.SnapshotId, _ => SnapshotSource,
                identity => identity.Key == "MissingPeer" ? null! : new object(), bed.Guard, default);
            Assert.False(result.Success);
            Assert.Equal("resolve", result.Stage);
            Assert.Equal(count, bed.Budget.LoadedCount);
            Assert.DoesNotContain("prepare", bed.Host.Events);
        });
    }

    [Theory]
    [InlineData("schema")]
    [InlineData("project")]
    [InlineData("id")]
    [InlineData("duplicate")]
    [InlineData("size")]
    [InlineData("complete")]
    public void Invalid_persistence_is_rejected_before_source_callbacks(string failure)
    {
        using var bed = new LiveBed();
        bed.Run(async () =>
        {
            await bed.Attach(SnapshotSource);
            var snapshot = bed.Manager.Snapshot(bed.Guard);
            RewriteSnapshot(bed, snapshot.SnapshotId, root =>
            {
                if (failure == "schema") root["schema"] = 99;
                if (failure == "project") root["project"] = "other";
                if (failure == "id") root["snapshotId"] = Guid.NewGuid().ToString("N");
                if (failure == "duplicate") root["entries"]!.AsArray().Add(root["entries"]![0]!.DeepClone());
                if (failure == "size") root["extra"] = new string('x', LiveSnapshotStore.MaxBytes);
                if (failure == "complete") root["complete"] = false;
            });
            await Assert.ThrowsAnyAsync<Exception>(() => bed.Manager.Restore(snapshot.SnapshotId,
                _ => throw new Exception("must not be called"), _ => new object(), bed.Guard, default));
        });
    }

    [Fact]
    public void Snapshot_path_is_fixed_and_rejects_traversal()
    {
        using var bed = new LiveBed();
        var store = new LiveSnapshotStore(bed.Root);
        Assert.Throws<ArgumentException>(() => store.GetPath("../escape"));
        Assert.Throws<ArgumentException>(() => store.Read("C:\\outside.json"));
        Assert.EndsWith(Path.Combine(".yokiframe", "engine", "live-snapshots", new string('a', 32) + ".json"),
            store.GetPath(new string('a', 32)));
    }

    private static void RewriteSnapshot(LiveBed bed, string id, Action<JsonObject> change)
    {
        string path = new LiveSnapshotStore(bed.Root).GetPath(id);
        var root = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        change(root);
        File.WriteAllText(path, root.ToJsonString());
    }
}
