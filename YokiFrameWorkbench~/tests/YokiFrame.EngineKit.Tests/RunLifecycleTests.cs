using System.Text.Json;
using YokiFrame;

namespace YokiFrame.EngineKit.Tests;

public sealed class RunLifecycleTests
{
    [Fact]
    public void Submission_persists_before_execution_and_is_idempotent()
    {
        using var bed = RunTestBed.Create();
        var first = bed.Submit("req");
        Assert.Equal(first.RunId, bed.Submit("req").RunId);
        Assert.True(bed.Store.TryReadRequestIndex("req", out string id));
        Assert.Equal(first.RunId, id);
        Assert.Equal(YokiFrameRunStatus.Queued, first.State);
        bed.RunUntilTerminal(id);
        Assert.Equal(YokiFrameRunStatus.Passed, bed.Read(id).State);
        Assert.Equal(1, bed.Read(id).Attempt);
    }

    [Theory]
    [InlineData("editor|play")]
    [InlineData("invalid")]
    public void Combined_or_unknown_targets_are_rejected(string target)
    {
        using var bed = RunTestBed.Create();
        Assert.Throws<ArgumentException>(() => bed.Scheduler.SubmitWork(target, "req", "cli", "hash", 1000, () => new TestRunWork()));
    }

    [Fact]
    public void Execution_switch_blocks_claims_until_enabled()
    {
        using var bed = RunTestBed.Create();
        bed.Blocked = true;
        var record = bed.Submit("blocked");
        bed.Scheduler.Tick();
        Assert.Equal(YokiFrameRunStatus.Queued, bed.Read(record.RunId).State);
        bed.Blocked = false;
        bed.RunUntilTerminal(record.RunId);
        Assert.Equal(YokiFrameRunStatus.Passed, bed.Read(record.RunId).State);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Cooperative_cancellation_and_timeout_reach_correct_terminal_state(bool timeout)
    {
        using var bed = RunTestBed.Create();
        var work = new TestRunWork { AutoComplete = false, Cooperative = true };
        var run = bed.Scheduler.SubmitWork("editor", "req", "cli", "hash", timeout ? 1 : 30000, () => work);
        bed.Scheduler.Tick();
        if (timeout) { Thread.Sleep(10); bed.Scheduler.Tick(); }
        else Assert.True(bed.Scheduler.TryCancel(run.RunId, out _, out _));
        bed.RunUntilTerminal(run.RunId);
        Assert.True(work.Invalidated);
        Assert.Equal(timeout ? YokiFrameRunStatus.Timeout : YokiFrameRunStatus.Cancelled, bed.Read(run.RunId).State);
    }

    [Fact]
    public void Uncooperative_work_detaches_and_late_result_does_not_overwrite_terminal_state()
    {
        using var bed = RunTestBed.Create();
        var work = new TestRunWork { AutoComplete = false };
        var run = bed.Scheduler.SubmitWork("editor", "req", "cli", "hash", 30000, () => work);
        run.GraceMs = 1;
        bed.Store.Save(run);
        bed.Scheduler.Tick();
        Assert.True(bed.Scheduler.TryCancel(run.RunId, out _, out _));
        Thread.Sleep(10);
        bed.Scheduler.Tick();
        Assert.Equal(YokiFrameRunStatus.Detached, bed.Read(run.RunId).State);
        work.Completion.SetResult(new YokiFrameRunResult { Status = YokiFrameRunStatus.Passed });
        bed.Scheduler.Tick();
        var late = bed.Read(run.RunId);
        Assert.Equal(YokiFrameRunStatus.Detached, late.State);
        Assert.NotNull(late.LateCompletionAtUtc);
        Assert.NotEmpty(late.LateResultPath);
    }

    [Fact]
    public void Faults_record_exception_details()
    {
        using var bed = RunTestBed.Create();
        var work = new TestRunWork { AutoComplete = false };
        var run = bed.Scheduler.SubmitWork("editor", "req", "cli", "hash", 30000, () => work);
        bed.Scheduler.Tick();
        work.Completion.SetException(new InvalidOperationException("boom"));
        bed.RunUntilTerminal(run.RunId);
        var finished = bed.Read(run.RunId);
        Assert.Equal(YokiFrameRunStatus.Errored, finished.State);
        using var result = bed.ReadResult(finished);
        Assert.Equal("boom", result.RootElement.GetProperty("exceptionMessage").GetString());
    }

    [Theory]
    [InlineData(YokiFrameRunStatus.Queued)]
    [InlineData(YokiFrameRunStatus.Running)]
    [InlineData(YokiFrameRunStatus.Passed)]
    public void Legacy_history_is_read_only_and_never_replayed(YokiFrameRunStatus state)
    {
        using var bed = RunTestBed.Create();
        var old = new YokiFrameEngineRunRecord
        {
            RunId = "legacy", RequestId = "old-request", LegacyEntry = "old.entry", State = state,
            SubmittedAtUtc = DateTime.UtcNow.AddDays(-1), UpdatedAtUtc = DateTime.UtcNow.AddDays(-1)
        };
        bed.Store.Save(old);
        string path = Path.Combine(bed.Store.RunsRoot, "legacy.json");
        string original = File.ReadAllText(path);
        Assert.Equal(0, bed.Scheduler.Reconcile(TimeSpan.Zero));
        bed.Scheduler.Tick();
        Assert.True(bed.Scheduler.TryLookupReadOnly(old.RequestId, out _));
        var operation = new YokiFrameEngineRunOperation(bed.Scheduler, "run_result");
        var result = operation.Execute(EnginePipeline.CreateRequest("cli", "run_result", """{"runId":"legacy"}"""));
        Assert.True(result.IsSuccess);
        Assert.Contains("old.entry", result.ResultJson);
        Assert.False(bed.Scheduler.TryCancel(old.RunId, out _, out _));
        Assert.Equal(original, File.ReadAllText(path));
        Assert.False(bed.Store.TryReadRequestIndex(old.RequestId, out _));
    }

    [Fact]
    public void Removed_commands_and_types_cannot_be_discovered()
    {
        using var bed = RunTestBed.Create();
        var settings = new StubEngineSettingsSource(YokiFrameEngineSettingsSnapshot.Enabled());
        var provider = new YokiFrameEngineKitProvider(YokiFrameEngineGate.CreateDefault(settings),
            new StubEngineOperationProvider("Unity", YokiFrameEngineExecutionTarget.Editor) { Runs = bed.Scheduler }, settings);
        Assert.DoesNotContain(provider.Commands, c => c.Action.StartsWith("entry_", StringComparison.Ordinal) || c.Action.StartsWith("eval", StringComparison.Ordinal));
        Assert.Null(typeof(YokiFrameRunResult).Assembly.GetType("YokiFrame.YokiFrameEntryAttribute"));
        Assert.Null(typeof(YokiFrameEngineKitProvider).Assembly.GetType("YokiFrame.YokiFrameEngineEntryRegistry"));
        Assert.Null(typeof(YokiFrameEngineKitProvider).Assembly.GetType("YokiFrame.YokiFrameEngineEvalSourceGenerator"));
    }
}

internal sealed class TestRunHost : IYokiFrameEngineRunHost
{
    internal string SessionIdValue { get; set; } = "session-1";
    internal long GenerationValue { get; set; } = 7;
    internal string Target { get; set; } = "editor";
    public string SessionId => SessionIdValue;
    public long Generation => GenerationValue;
    public string HostTarget => Target;
}

internal sealed class TestRunWork : IYokiFrameEngineRunWork
{
    public bool AutoComplete { get; set; } = true;
    public bool Cooperative { get; set; }
    public bool Invalidated { get; private set; }
    public TaskCompletionSource<YokiFrameRunResult> Completion { get; } = new();
    public bool IsCompiling => false;
    public int Frames { get; private set; }
    public int StaleContextCalls => 0;
    public Task<YokiFrameRunResult> Start() => Completion.Task;
    public void AdvanceFrame()
    {
        Frames++;
        if (AutoComplete && Frames >= 2) Completion.TrySetResult(new YokiFrameRunResult { Status = YokiFrameRunStatus.Passed });
    }
    public void Invalidate()
    {
        Invalidated = true;
        if (Cooperative) Completion.TrySetCanceled();
    }
}

internal sealed class RunTestBed : IDisposable
{
    private RunTestBed()
    {
        Root = Path.Combine(Path.GetTempPath(), "yokiframe-run-tests", Guid.NewGuid().ToString("N"));
        Store = new YokiFrameEngineRunStore(Root);
        Scheduler = new YokiFrameEngineRunScheduler(Store, Host, () => Blocked);
    }
    public string Root { get; }
    public YokiFrameEngineRunStore Store { get; }
    public TestRunHost Host { get; } = new();
    public YokiFrameEngineRunScheduler Scheduler { get; }
    public bool Blocked { get; set; }
    public static RunTestBed Create() => new();
    public YokiFrameEngineRunRecord Submit(string requestId) =>
        Scheduler.SubmitWork("editor", requestId, "cli", "hash", 30000, () => new TestRunWork());
    public YokiFrameEngineRunRecord Read(string id)
    {
        Assert.True(Store.TryReadRun(id, out var record));
        return record;
    }
    public void RunUntilTerminal(string id, int attempts = 200)
    {
        for (int i = 0; i < attempts; i++)
        {
            Scheduler.Tick();
            if (Read(id).IsTerminal) return;
            Thread.Sleep(5);
        }
        Assert.Fail("Run did not reach a terminal state: " + id);
    }
    public JsonDocument ReadResult(YokiFrameEngineRunRecord record)
    {
        Assert.True(Store.TryReadResult(record.ResultPath, out string json));
        return JsonDocument.Parse(json);
    }
    public void Dispose() { if (Directory.Exists(Root)) Directory.Delete(Root, true); }
}
