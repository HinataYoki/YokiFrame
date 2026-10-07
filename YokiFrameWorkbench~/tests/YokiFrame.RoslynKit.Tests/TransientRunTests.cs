using YokiFrame;

namespace YokiFrame.RoslynKit.Tests;

public sealed class TransientRunTests
{
    /// <summary>提交和读取不执行工作，也不需要入口命令。Tick 之后才启动，完成后状态为 Passed。</summary>
    [Fact]
    public void Submission_and_reads_do_not_execute_and_do_not_need_an_entry()
    {
        using var bed = RunTestBed.Create();
        var work = new Work();
        var record = Submit(bed, work);
        Assert.Equal("script", record.Kind);
        Assert.Empty(record.LegacyEntry);
        Assert.True(bed.Scheduler.TryLookupReadOnly("script-req", out _));
        Assert.True(bed.Scheduler.TryReadRun(record.RunId, out _));
        Assert.Equal(0, work.StartCount);
        bed.Scheduler.Tick();
        Assert.Equal(1, work.StartCount);
        work.Completion.SetResult(new RunResult { Status = RunStatus.Passed });
        bed.RunUntilTerminal(record.RunId);
        Assert.Equal(RunStatus.Passed, Read(bed, record).State);
    }

    /// <summary>相同请求不替换已提交工作；哈希冲突抛出 InvalidOperationException。只启动第一次工作。</summary>
    [Fact]
    public void Duplicate_submission_does_not_replace_work_and_conflicting_hash_is_rejected()
    {
        using var bed = RunTestBed.Create();
        var first = new Work();
        var second = new Work();
        var record = Submit(bed, first);
        Assert.Equal(record.RunId, Submit(bed, second).RunId);
        Assert.Throws<InvalidOperationException>(() => Submit(bed, second, "different"));
        bed.Scheduler.Tick();
        Assert.Equal(1, first.StartCount);
        Assert.Equal(0, second.StartCount);
    }

    /// <summary>排队中取消不会调用工厂。Tick 后状态为 Cancelled，启动次数为零。</summary>
    [Fact]
    public void Cancelling_queued_work_never_calls_factory()
    {
        using var bed = RunTestBed.Create();
        var work = new Work();
        var record = Submit(bed, work);
        Assert.True(bed.Scheduler.TryCancel(record.RunId, out _, out _));
        bed.Scheduler.Tick();
        Assert.Equal(0, work.StartCount);
        Assert.Equal(RunStatus.Cancelled, Read(bed, record).State);
    }

    /// <summary>目标从 editor 改到 play 后不能认领。工作不启动，状态保持 Unknown。</summary>
    [Fact]
    public void Target_change_prevents_claim()
    {
        using var bed = RunTestBed.Create();
        var work = new Work();
        var record = Submit(bed, work);
        bed.Host.Target = "play";
        bed.Scheduler.Tick();
        Assert.Equal(0, work.StartCount);
        Assert.Equal(RunStatus.Unknown, Read(bed, record).State);
    }

    /// <summary>编译中的失败是终态 CompileFailed，不会被记成通过。中间状态仍是 Compiling。</summary>
    [Fact]
    public void Compilation_failure_is_terminal_and_is_not_a_pass()
    {
        using var bed = RunTestBed.Create();
        var work = new Work { IsCompiling = true };
        var record = Submit(bed, work);
        bed.Scheduler.Tick();
        Assert.Equal(RunStatus.Compiling, Read(bed, record).State);
        work.Completion.SetResult(new RunResult { Status = RunStatus.CompileFailed });
        bed.RunUntilTerminal(record.RunId);
        Assert.Equal(RunStatus.CompileFailed, Read(bed, record).State);
    }

    [Theory]
    [InlineData(RunStatus.Queued)]
    [InlineData(RunStatus.Compiling)]
    [InlineData(RunStatus.Running)]
    /// <summary>重新加载不重放临时源码。协调后状态变为 Unknown，不恢复传入的非终态。</summary>
    /// <param name="state">写入存储的非终态。</param>
    public void Reload_does_not_replay_transient_source(RunStatus state)
    {
        using var bed = RunTestBed.Create();
        var record = Submit(bed, new Work());
        record.State = state;
        bed.Store.Save(record);
        var nextHost = new TestRunHost { GenerationValue = 8 };
        var reloaded = new RoslynRunScheduler(bed.Store, nextHost);
        Assert.Equal(1, reloaded.Reconcile(TimeSpan.FromMinutes(1)));
        reloaded.Tick();
        Assert.Equal(RunStatus.Unknown, Read(bed, record).State);
    }

    /// <summary>取消编译会使工作失效。之后即使报 Passed，终态仍保持 Cancelled。</summary>
    [Fact]
    public void Cancelling_compilation_invalidates_work_and_preserves_cancelled_result()
    {
        using var bed = RunTestBed.Create();
        var work = new Work { IsCompiling = true };
        var record = Submit(bed, work);
        bed.Scheduler.Tick();
        Assert.True(bed.Scheduler.TryCancel(record.RunId, out _, out _));
        Assert.True(work.Invalidated);
        work.Completion.SetResult(new RunResult { Status = RunStatus.Passed });
        bed.RunUntilTerminal(record.RunId);
        Assert.Equal(RunStatus.Cancelled, Read(bed, record).State);
    }

    /// <summary>其他宿主不能协调或取消这条脚本。原记录保持 Queued。</summary>
    [Fact]
    public void Other_host_cannot_reconcile_or_cancel_a_script()
    {
        using var bed = RunTestBed.Create();
        var record = Submit(bed, new Work());
        var otherHost = new RoslynRunScheduler(bed.Store,
            new TestRunHost { SessionIdValue = "other-session" }, ownerHostId: "godot-editor");
        Assert.Equal(0, otherHost.Reconcile(TimeSpan.Zero));
        Assert.False(otherHost.TryCancel(record.RunId, out _, out _));
        Assert.Equal(RunStatus.Queued, Read(bed, record).State);
    }

    /// <summary>没有索引时只读查找不修复索引、也不执行。找到的记录保持 Queued。</summary>
    [Fact]
    public void Lookup_without_an_index_does_not_repair_or_execute()
    {
        using var bed = RunTestBed.Create();
        var record = new RoslynRunRecord
        {
            RunId = "read-only-run", RequestId = "read-only-request", Kind = "script"
        };
        bed.Store.Save(record);
        Assert.True(bed.Scheduler.TryLookupReadOnly(record.RequestId, out var found));
        Assert.Equal(record.RunId, found.RunId);
        Assert.False(bed.Store.TryReadRequestIndex(record.RequestId, out _));
        Assert.Equal(RunStatus.Queued, found.State);
    }

    /// <summary>向 editor 提交一段临时工作。不 Tick，因此不会立刻执行。</summary>
    /// <param name="bed">测试床。</param>
    /// <param name="work">工厂返回的工作。</param>
    /// <param name="hash">请求哈希，冲突用例可改成不同值。</param>
    /// <returns>已持久化的排队记录。</returns>
    private static RoslynRunRecord Submit(RunTestBed bed, Work work, string hash = "hash")
        => bed.Scheduler.SubmitWork("editor", "script-req", "cli", hash, 30000, () => work);

    /// <summary>按运行号读回存储记录。读不到时断言失败。</summary>
    /// <param name="bed">测试床。</param>
    /// <param name="record">提供运行号的记录。</param>
    /// <returns>存储中的当前记录。</returns>
    private static RoslynRunRecord Read(RunTestBed bed, RoslynRunRecord record)
    {
        Assert.True(bed.Store.TryReadRun(record.RunId, out var stored));
        return stored;
    }

    private sealed class Work : IRoslynRunWork
    {
        public TaskCompletionSource<RunResult> Completion { get; } = new();
        public int StartCount { get; private set; }
        public bool Invalidated { get; private set; }
        public bool IsCompiling { get; set; }
        public int Frames { get; private set; }
        public int StaleContextCalls => 0;
        /// <summary>启动次数加一，并返回未完成的任务。不改变帧计数。</summary>
        /// <returns>测试自行完成的结果任务。</returns>
        public Task<RunResult> Start() { StartCount++; return Completion.Task; }

        /// <summary>帧计数加一。不改变完成状态。</summary>
        public void AdvanceFrame() { Frames++; }

        /// <summary>把工作标成已失效。不取消完成源。</summary>
        public void Invalidate() { Invalidated = true; }
    }
}
