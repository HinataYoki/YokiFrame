using YokiFrame;

namespace YokiFrame.EngineKit.Tests;

public sealed class TransientRunTests
{
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
        work.Completion.SetResult(new YokiFrameRunResult { Status = YokiFrameRunStatus.Passed });
        bed.RunUntilTerminal(record.RunId);
        Assert.Equal(YokiFrameRunStatus.Passed, Read(bed, record).State);
    }

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

    [Fact]
    public void Cancelling_queued_work_never_calls_factory()
    {
        using var bed = RunTestBed.Create();
        var work = new Work();
        var record = Submit(bed, work);
        Assert.True(bed.Scheduler.TryCancel(record.RunId, out _, out _));
        bed.Scheduler.Tick();
        Assert.Equal(0, work.StartCount);
        Assert.Equal(YokiFrameRunStatus.Cancelled, Read(bed, record).State);
    }

    [Fact]
    public void Target_change_prevents_claim()
    {
        using var bed = RunTestBed.Create();
        var work = new Work();
        var record = Submit(bed, work);
        bed.Host.Target = "play";
        bed.Scheduler.Tick();
        Assert.Equal(0, work.StartCount);
        Assert.Equal(YokiFrameRunStatus.Unknown, Read(bed, record).State);
    }

    [Fact]
    public void Compilation_failure_is_terminal_and_is_not_a_pass()
    {
        using var bed = RunTestBed.Create();
        var work = new Work { IsCompiling = true };
        var record = Submit(bed, work);
        bed.Scheduler.Tick();
        Assert.Equal(YokiFrameRunStatus.Compiling, Read(bed, record).State);
        work.Completion.SetResult(new YokiFrameRunResult { Status = YokiFrameRunStatus.CompileFailed });
        bed.RunUntilTerminal(record.RunId);
        Assert.Equal(YokiFrameRunStatus.CompileFailed, Read(bed, record).State);
    }

    [Theory]
    [InlineData(YokiFrameRunStatus.Queued)]
    [InlineData(YokiFrameRunStatus.Compiling)]
    [InlineData(YokiFrameRunStatus.Running)]
    public void Reload_does_not_replay_transient_source(YokiFrameRunStatus state)
    {
        using var bed = RunTestBed.Create();
        var record = Submit(bed, new Work());
        record.State = state;
        bed.Store.Save(record);
        var nextHost = new TestRunHost { GenerationValue = 8 };
        var reloaded = new YokiFrameEngineRunScheduler(bed.Store, nextHost);
        Assert.Equal(1, reloaded.Reconcile(TimeSpan.FromMinutes(1)));
        reloaded.Tick();
        Assert.Equal(YokiFrameRunStatus.Unknown, Read(bed, record).State);
    }

    [Fact]
    public void Cancelling_compilation_invalidates_work_and_preserves_cancelled_result()
    {
        using var bed = RunTestBed.Create();
        var work = new Work { IsCompiling = true };
        var record = Submit(bed, work);
        bed.Scheduler.Tick();
        Assert.True(bed.Scheduler.TryCancel(record.RunId, out _, out _));
        Assert.True(work.Invalidated);
        work.Completion.SetResult(new YokiFrameRunResult { Status = YokiFrameRunStatus.Passed });
        bed.RunUntilTerminal(record.RunId);
        Assert.Equal(YokiFrameRunStatus.Cancelled, Read(bed, record).State);
    }

    [Fact]
    public void Other_host_cannot_reconcile_or_cancel_a_script()
    {
        using var bed = RunTestBed.Create();
        var record = Submit(bed, new Work());
        var otherHost = new YokiFrameEngineRunScheduler(bed.Store,
            new TestRunHost { SessionIdValue = "other-session" }, ownerHostId: "godot-editor");
        Assert.Equal(0, otherHost.Reconcile(TimeSpan.Zero));
        Assert.False(otherHost.TryCancel(record.RunId, out _, out _));
        Assert.Equal(YokiFrameRunStatus.Queued, Read(bed, record).State);
    }

    [Fact]
    public void Lookup_without_an_index_does_not_repair_or_execute()
    {
        using var bed = RunTestBed.Create();
        var record = new YokiFrameEngineRunRecord
        {
            RunId = "read-only-run", RequestId = "read-only-request", Kind = "script"
        };
        bed.Store.Save(record);
        Assert.True(bed.Scheduler.TryLookupReadOnly(record.RequestId, out var found));
        Assert.Equal(record.RunId, found.RunId);
        Assert.False(bed.Store.TryReadRequestIndex(record.RequestId, out _));
        Assert.Equal(YokiFrameRunStatus.Queued, found.State);
    }

    private static YokiFrameEngineRunRecord Submit(RunTestBed bed, Work work, string hash = "hash")
        => bed.Scheduler.SubmitWork("editor", "script-req", "cli", hash, 30000, () => work);

    private static YokiFrameEngineRunRecord Read(RunTestBed bed, YokiFrameEngineRunRecord record)
    {
        Assert.True(bed.Store.TryReadRun(record.RunId, out var stored));
        return stored;
    }

    private sealed class Work : IYokiFrameEngineRunWork
    {
        public TaskCompletionSource<YokiFrameRunResult> Completion { get; } = new();
        public int StartCount { get; private set; }
        public bool Invalidated { get; private set; }
        public bool IsCompiling { get; set; }
        public int Frames { get; private set; }
        public int StaleContextCalls => 0;
        public Task<YokiFrameRunResult> Start() { StartCount++; return Completion.Task; }
        public void AdvanceFrame() { Frames++; }
        public void Invalidate() { Invalidated = true; }
    }
}
