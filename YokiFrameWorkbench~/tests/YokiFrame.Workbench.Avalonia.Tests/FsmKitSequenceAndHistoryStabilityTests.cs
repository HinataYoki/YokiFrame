using System.Reflection;
using YokiFrame.Tooling.Application.Models.FsmKit;
using YokiFrame.Workbench.Avalonia.ViewModels;

namespace YokiFrame.Workbench.Avalonia.Tests;

/// <summary>覆盖 Telemetry 顺序权威和转换历史增量集合的稳定性。</summary>
public sealed class FsmKitSequenceAndHistoryStabilityTests
{
    /// <summary>验证 Telemetry 不受墙钟回拨或旧详情未来时间影响，并报告精确提交成功。</summary>
    [Fact]
    public void TelemetryDetailsIgnoreWallClockRollback()
    {
        FsmKitPageViewModel viewModel = new();
        var futureCommand = CreateState(
            "command", "future-command", DateTimeOffset.Parse("2026-07-15T12:00:00Z"));
        var olderTelemetry = CreateState(
            "telemetry", "live-sequence", DateTimeOffset.Parse("2026-07-15T11:00:00Z"));

        viewModel.ApplyPeriodicState(futureCommand);
        var accepted = viewModel.TryApplySequencedTelemetryState(olderTelemetry);

        Assert.True(accepted);
        Assert.Equal("telemetry", viewModel.Source);
        Assert.Equal("{\"source\":\"live-sequence\"}", viewModel.RawPayload);
        Assert.Equal("live-sequence", Assert.Single(viewModel.Transitions).To);
    }

    /// <summary>验证同实例旧 overview 即使也标记为 telemetry，仍不能回退命名详情。</summary>
    [Fact]
    public void OlderOverviewDoesNotRollbackSequencedTelemetry()
    {
        FsmKitPageViewModel viewModel = new();
        var namedDetails = CreateState(
            "telemetry", "named-live", DateTimeOffset.Parse("2026-07-15T12:00:00Z"));
        var olderOverview = CreateState(
            "telemetry", "overview-old", DateTimeOffset.Parse("2026-07-15T11:00:00Z"));

        Assert.True(viewModel.TryApplySequencedTelemetryState(namedDetails));
        var history = viewModel.Transitions;
        viewModel.ApplyPeriodicState(olderOverview);

        Assert.Equal("{\"source\":\"named-live\"}", viewModel.RawPayload);
        Assert.Same(history, viewModel.Transitions);
        Assert.Equal("named-live", Assert.Single(viewModel.Transitions).To);
    }

    /// <summary>验证旧 sequence overview 即使因墙钟回拨带有更晚时间，也只能合并摘要。</summary>
    [Fact]
    public void FutureWallClockOverviewDoesNotRollbackSequencedTelemetry()
    {
        FsmKitPageViewModel viewModel = new();
        var namedDetails = CreateState(
            "telemetry", "named-sequence", DateTimeOffset.Parse("2026-07-15T12:00:00Z"));
        var delayedOldSequenceOverview = CreateState(
            "telemetry", "overview-old-sequence", DateTimeOffset.Parse("2026-07-15T13:00:00Z"));

        Assert.True(viewModel.TryApplySequencedTelemetryState(namedDetails));
        var history = viewModel.Transitions;
        viewModel.ApplyPeriodicState(delayedOldSequenceOverview);

        Assert.Equal("{\"source\":\"named-sequence\"}", viewModel.RawPayload);
        Assert.Same(history, viewModel.Transitions);
        Assert.Equal("named-sequence", Assert.Single(viewModel.Transitions).To);
    }

    /// <summary>验证切换 instanceId 后清除旧权威，新实例可重新采用 dashboard 精确详情。</summary>
    [Fact]
    public void InstanceChangeResetsSequencedTelemetryAuthority()
    {
        FsmKitPageViewModel viewModel = new();
        Assert.True(viewModel.TryApplySequencedTelemetryState(CreateState(
            "telemetry", "chosen-live", DateTimeOffset.Parse("2026-07-15T12:00:00Z"))));
        viewModel.SelectedMachine = Assert.Single(
            viewModel.Machines,
            static machine => machine.InstanceId == "default-instance");
        var defaultOverview = FsmKitContractTestData.CreateState(
            "default-instance", "telemetry", "{\"source\":\"default-overview\"}",
            "F:/Project/default-overview.json", "default-overview",
            updatedAtUtc: DateTimeOffset.Parse("2026-07-15T13:00:00Z"));

        viewModel.ApplyPeriodicState(defaultOverview);

        Assert.Equal("default-instance", viewModel.SelectedInstanceId);
        Assert.Equal("{\"source\":\"default-overview\"}", viewModel.RawPayload);
    }

    /// <summary>验证 session/generation 变化会清除旧权威，使新宿主详情可以建立页面基线。</summary>
    [Fact]
    public void HostChangeResetsSequencedTelemetryAuthority()
    {
        FsmKitPageViewModel viewModel = new();
        Assert.True(viewModel.TryApplySequencedTelemetryState(CreateState(
            "telemetry", "old-host-live", DateTimeOffset.Parse("2026-07-15T12:00:00Z"))));
        var newHostOverview = FsmKitContractTestData.CreateState(
            "chosen-instance", "telemetry", "{\"source\":\"new-host-overview\"}",
            "F:/Project/new-host-overview.json", "new-host-overview",
            sessionId: "session-8", generation: 8L,
            updatedAtUtc: DateTimeOffset.Parse("2026-07-15T13:00:00Z"));

        viewModel.ApplyPeriodicState(newHostOverview);

        Assert.Equal("session-8", viewModel.SessionId);
        Assert.Equal(8L, viewModel.Generation);
        Assert.Equal("{\"source\":\"new-host-overview\"}", viewModel.RawPayload);
    }

    /// <summary>验证历史追加只新增尾项，集合和已存在转换对象均保持稳定。</summary>
    [Fact]
    public void AppendedHistoryKeepsCollectionAndExistingItems()
    {
        FsmKitPageViewModel viewModel = new();
        viewModel.ApplyPeriodicState(CreateState(
            "telemetry", "initial", DateTimeOffset.UtcNow,
            new[] { ("Start", "Ready"), ("Ready", "Attack") }));
        var collection = viewModel.Transitions;
        var first = collection[0];
        var second = collection[1];

        viewModel.ApplyPeriodicState(CreateState(
            "telemetry", "append", DateTimeOffset.UtcNow,
            new[] { ("Start", "Ready"), ("Ready", "Attack"), ("Attack", "Idle") }));

        Assert.Same(collection, viewModel.Transitions);
        Assert.Same(first, viewModel.Transitions[0]);
        Assert.Same(second, viewModel.Transitions[1]);
        Assert.Equal("Idle", viewModel.Transitions[2].To);
    }

    /// <summary>验证有界窗口滚动只移除最旧项，并复用重叠后缀的转换对象。</summary>
    [Fact]
    public void RollingHistoryReusesOverlappingWindow()
    {
        FsmKitPageViewModel viewModel = new();
        viewModel.ApplyPeriodicState(CreateStateWithTimedHistory(
            "initial",
            new[] { ("A", "B", "10:00:00"), ("B", "C", "10:00:01"), ("C", "D", "10:00:02") }));
        var retainedFirst = viewModel.Transitions[1];
        var retainedSecond = viewModel.Transitions[2];

        viewModel.ApplyPeriodicState(CreateStateWithTimedHistory(
            "rolling",
            new[] { ("B", "C", "10:00:01"), ("C", "D", "10:00:02"), ("D", "E", "10:00:03") }));

        Assert.Same(retainedFirst, viewModel.Transitions[0]);
        Assert.Same(retainedSecond, viewModel.Transitions[1]);
        Assert.Equal("E", viewModel.Transitions[2].To);
    }

    /// <summary>验证重复转换跨滚动边界时仍选择最大后缀重叠，并复用正确的重复项实例。</summary>
    [Fact]
    public void DuplicateHistoryAcrossRollingBoundaryReusesCorrectSuffix()
    {
        FsmKitPageViewModel viewModel = new();
        viewModel.ApplyPeriodicState(CreateStateWithTimedHistory(
            "duplicates-initial",
            new[] { ("A", "B", "10:00:00"), ("A", "B", "10:00:00"), ("B", "C", "10:00:01") }));
        var retainedDuplicate = viewModel.Transitions[1];
        var retainedTail = viewModel.Transitions[2];

        viewModel.ApplyPeriodicState(CreateStateWithTimedHistory(
            "duplicates-rolling",
            new[] { ("A", "B", "10:00:00"), ("B", "C", "10:00:01"), ("C", "D", "10:00:02") }));

        Assert.Same(retainedDuplicate, viewModel.Transitions[0]);
        Assert.Same(retainedTail, viewModel.Transitions[1]);
        Assert.Equal("D", viewModel.Transitions[2].To);
    }

    /// <summary>验证空窗口和同长度替换不会更换集合，也不会留下越界或旧记录。</summary>
    [Fact]
    public void EmptyAndSameLengthReplacementKeepCollectionValid()
    {
        FsmKitPageViewModel viewModel = new();
        viewModel.ApplyPeriodicState(CreateState(
            "telemetry", "initial", DateTimeOffset.UtcNow,
            new[] { ("A", "B"), ("B", "C") }));
        var collection = viewModel.Transitions;

        viewModel.ApplyPeriodicState(CreateState(
            "telemetry", "empty", DateTimeOffset.UtcNow,
            Array.Empty<(string From, string To)>()));
        Assert.Same(collection, viewModel.Transitions);
        Assert.Empty(viewModel.Transitions);

        viewModel.ApplyPeriodicState(CreateState(
            "telemetry", "replace", DateTimeOffset.UtcNow,
            new[] { ("X", "Y"), ("Y", "Z") }));
        Assert.Same(collection, viewModel.Transitions);
        Assert.Equal(new[] { "Y", "Z" }, viewModel.Transitions.Select(static item => item.To));
    }

    /// <summary>创建当前 chosen 实例的强类型详情，并允许测试控制时间和历史窗口。</summary>
    private static WorkbenchFsmKitState CreateState(
        string source,
        string marker,
        DateTimeOffset updatedAtUtc,
        IReadOnlyList<(string From, string To)>? transitions = null)
    {
        return FsmKitContractTestData.CreateState(
            "chosen-instance",
            source,
            "{\"source\":\"" + marker + "\"}",
            "F:/Project/" + marker + ".json",
            marker,
            transitions: transitions,
            updatedAtUtc: updatedAtUtc);
    }

    /// <summary>创建保留真实时间文本的有界历史窗口，避免测试工厂按数组索引重写时间。</summary>
    private static WorkbenchFsmKitState CreateStateWithTimedHistory(
        string marker,
        IReadOnlyList<(string From, string To, string Time)> transitions)
    {
        var baseline = CreateState("telemetry", marker, DateTimeOffset.UnixEpoch);
        var history = new WorkbenchFsmTransition[transitions.Count];
        for (var index = 0; index < transitions.Count; index++)
        {
            var transition = transitions[index];
            history[index] = CreateInternal<WorkbenchFsmTransition>(
                transition.From, transition.To, transition.Time);
        }

        return CreateInternal<WorkbenchFsmKitState>(
            baseline.DataSource,
            baseline.FsmName,
            baseline.InstanceId,
            baseline.DeclaredCount,
            baseline.Machines,
            baseline.Selected!,
            history,
            history.Length,
            baseline.StateEvents,
            baseline.StateEventDeclaredCount);
    }

    /// <summary>调用 Application 模型的内部构造方法，生成具有精确历史时间的测试对象。</summary>
    private static T CreateInternal<T>(params object[] arguments)
    {
        var instance = Activator.CreateInstance(
            typeof(T),
            BindingFlags.Instance | BindingFlags.NonPublic,
            null,
            arguments,
            null);
        return Assert.IsType<T>(instance);
    }
}
