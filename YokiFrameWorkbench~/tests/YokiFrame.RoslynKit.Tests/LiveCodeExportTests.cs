using YokiFrame;

namespace YokiFrame.RoslynKit.Tests;

public sealed partial class LiveCodeTests
{
    [Fact]
    /// <summary>批量导出共享源码但保留各目标字段。不增加加载计数，也不创建 Assets 目录。</summary>
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
                new LiveExportRequest("First", "Assets/Shared.cs"),
                new LiveExportRequest("Second", "Assets/Shared.cs")
            }, null!, bed.Guard, default);
            Assert.Equal("Prepared", batch.State);
            Assert.Equal(loaded, bed.Budget.LoadedCount);
            var store = new LiveExportStore(bed.Root);
            var version = store.ReadVersion(Assert.Single(batch.ExportIds));
            Assert.Equal(2, version.Targets.Count);
            Assert.Contains("7", version.Targets[0].Fields);
            Assert.Contains("23", version.Targets[1].Fields);
            Assert.False(Directory.Exists(Path.Combine(bed.Root, "Assets")));
        });
    }

    [Fact]
    /// <summary>编辑器里没有活动句柄时仍可再导出。不增加加载，列表保持空，并记住上一导出号。</summary>
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
            var store = new LiveExportStore(bed.Root);
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
    /// <summary>共享路径的源码冲突时抛出 ArgumentException，并且不暂存任何导出文件。</summary>
    public void Export_conflicting_shared_sources_do_not_stage_any_files()
    {
        using var bed = new LiveBed(exports: true);
        bed.Run(async () =>
        {
            await bed.Manager.Attach("First", new object(), "Shared", "public int Value = 1;", bed.Guard, default);
            await bed.Manager.Attach("Second", new object(), "Shared", "public int Value = 2;", bed.Guard, default);
            await Assert.ThrowsAsync<ArgumentException>(() => bed.Manager.ExportMany(new[]
            {
                new LiveExportRequest("First", "Assets/Shared.cs"),
                new LiveExportRequest("Second", "Assets/Shared.cs")
            }, null!, bed.Guard, default));
            Assert.False(Directory.Exists(Path.Combine(bed.Root, ".yokiframe/engine/live-exports")));
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    /// <summary>编译期间取消或会话变化都会抛出取消异常，并且不暂存导出。</summary>
    /// <param name="session">为 true 时改会话，否则取消令牌。</param>
    public void Export_cancel_or_session_change_during_compile_does_not_stage(bool session)
    {
        using var bed = new LiveBed(exports: true);
        bed.Run(async () =>
        {
            await bed.Attach("public int Value = 7;");
            using var token = new CancellationTokenSource();
            var pending = bed.Manager.ExportMany(new[] { new LiveExportRequest("Probe", "Assets/Probe.cs") },
                null!, bed.Guard, token.Token);
            if (session) bed.State.SessionId = "changed";
            else token.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
            Assert.False(Directory.Exists(Path.Combine(bed.Root, ".yokiframe/engine/live-exports")));
        });
    }

    [Fact]
    /// <summary>提交方结束后提交仍会在写盘前复查会话。会话变化时取消且不写文件，恢复后才落盘。</summary>
    public void Commit_outlives_submitter_but_rechecks_session_before_writing()
    {
        using var bed = new LiveBed(exports: true);
        bed.Run(async () =>
        {
            await bed.Attach("public int Value = 7;");
            var batch = await bed.Manager.ExportMany(new[] { new LiveExportRequest("Probe", "Assets/Probe.cs") },
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

    private sealed class ExportFakeHost : FakeHost, ILiveExportHost
    {
        private readonly LiveExportStore mStore;
        public Action? CompleteCommit { get; private set; }
        /// <summary>绑定项目根上的导出存储。不创建提交回调。</summary>
        /// <param name="root">临时项目根。</param>
        public ExportFakeHost(string root) { mStore = new(root); }
        /// <summary>捕获当前字段值作为导出目标。不写文件。</summary>
        /// <param name="attachment">活动附件。</param>
        /// <returns>带新目标号和 Value 字段的目标。</returns>
        public LiveExportTarget CaptureExportTarget(IDisposable attachment) => new()
        {
            TargetId = Guid.NewGuid().ToString("N"), ScenePath = "scene",
            Fields = "{\"Value\":" + ReadField(attachment, "Value") + "}"
        };
        /// <summary>版本化时原样返回源码。不插入导出号。</summary>
        /// <param name="source">原始源码。</param>
        /// <param name="exportId">未使用的导出号。</param>
        /// <returns>传入的源码。</returns>
        public string VersionExportSource(string source, string exportId) => source;
        /// <summary>按导出号读取已存储版本。不修改存储。</summary>
        /// <param name="id">导出号。</param>
        /// <returns>存储中的版本。</returns>
        public LiveExportVersion ReadExport(string id) => mStore.ReadVersion(id);
        /// <summary>基于旧版本生成再导出记录。不写盘，只计算新源码和哈希。</summary>
        /// <param name="id">上一导出号。</param>
        /// <param name="members">新的成员源码。</param>
        /// <param name="batchId">所属批次。</param>
        /// <returns>指向旧导出的新版本。</returns>
        public LiveExportVersion CreateReexport(string id, string members, string batchId)
        {
            var old = mStore.ReadVersion(id);
            string source = WrapBehaviour(old.ClassName, members, true);
            return new LiveExportVersion
            {
                ExportId = Guid.NewGuid().ToString("N"), BatchId = batchId, PreviousExportId = id,
                ClassName = old.ClassName, SourcePath = old.SourcePath, Source = source,
                SourceHash = LiveCodeManager.Hash(source), PreviousMetaHash = mStore.MetaHash(old)
            };
        }
        /// <summary>把版本暂存到导出存储。不提交到 Assets。</summary>
        /// <param name="id">批次号。</param>
        /// <param name="versions">待暂存版本。</param>
        /// <returns>暂存后的批次。</returns>
        public LiveExportBatch StageExports(string id, IReadOnlyList<LiveExportVersion> versions) => mStore.Stage(id, versions);
        /// <summary>先执行守卫并标记排队，再把真正提交留到 CompleteCommit。不立即写 Assets。</summary>
        /// <param name="id">批次号。</param>
        /// <param name="guard">提交前后都要调用的守卫。</param>
        public void QueueExportCommit(string id, Action guard)
        {
            guard();
            mStore.MarkQueued(id);
            CompleteCommit = () => { guard(); mStore.Commit(id); };
        }
    }
}
