using YokiFrame;

namespace YokiFrame.RoslynKit.Tests;

/// <summary>提供可计数的 Engine 开关读取端口，用于验证 Gate 是否真的被调用。</summary>
internal sealed class StubEngineSettingsSource : IRoslynSettingsSource
{
    internal StubEngineSettingsSource(RoslynSettingsSnapshot snapshot)
    {
        Snapshot = snapshot;
    }

    /// <summary>获取或设置当前开关快照；测试可据此模拟运行中开关变化。</summary>
    internal RoslynSettingsSnapshot Snapshot { get; set; }

    /// <summary>获取开关读取次数；Policy 先行拒绝时必须为 0。</summary>
    internal int ReadCount { get; private set; }

    public RoslynSettingsSnapshot Read()
    {
        ReadCount++;
        return Snapshot;
    }
}

/// <summary>记录执行次数的假操作，用于断言 Gate 拒绝时操作调用次数为零。</summary>
internal sealed class RecordingEngineOperation : IRoslynOperation
{
    private const string ResultJson = "{\"handled\":true}";

    internal RecordingEngineOperation(
        string action,
        YokiFrameCommandKind kind,
        bool isDiagnostic = false,
        bool isCancellation = false,
        RoslynExecutionTarget targets = RoslynExecutionTarget.Editor,
        bool isTargetAgnostic = false)
    {
        Descriptor = new RoslynOperationDescriptor(action, kind, isDiagnostic, isCancellation, targets, isTargetAgnostic);
    }

    internal int InvocationCount { get; private set; }

    public RoslynOperationDescriptor Descriptor { get; }

    public YokiFrameCommandResult Execute(YokiFrameCommandRequest request)
    {
        InvocationCount++;
        return YokiFrameCommandResult.Success(ResultJson);
    }
}

/// <summary>装配真实 Dispatcher → Policy → Gate 的最小 RoslynKit 管线。</summary>
internal static class EnginePipeline
{
    internal const string CliSource = "cli";
    internal const string WorkbenchSource = "workbench";
    internal const string CodexSource = "codex";
    internal const string ExternalAutomationSource = "external-automation";

    internal static YokiFrameCommandDispatcher CreateDispatcher(
        RoslynSettingsSnapshot settings,
        out StubEngineSettingsSource settingsSource,
        params RecordingEngineOperation[] operations)
    {
        settingsSource = new StubEngineSettingsSource(settings);
        var gate = RoslynGate.CreateDefault(settingsSource);
        var handler = new RoslynCommandHandler(gate, operations);
        var policy = YokiFrameCommandPolicy.CreateWithDefaultSources(handler.Descriptors.ToArray());
        return new YokiFrameCommandDispatcher(policy, new IYokiFrameCommandHandler[] { handler });
    }

    internal static YokiFrameCommandDispatcher CreateDispatcherWithHostTargets(
        RoslynSettingsSnapshot settings,
        RoslynExecutionTarget hostTargets,
        out StubEngineSettingsSource settingsSource,
        params RecordingEngineOperation[] operations)
    {
        settingsSource = new StubEngineSettingsSource(settings);
        var gate = RoslynGate.CreateDefault(settingsSource);
        var handler = new RoslynCommandHandler(gate, operations, hostTargets);
        var policy = YokiFrameCommandPolicy.CreateWithDefaultSources(handler.Descriptors.ToArray());
        return new YokiFrameCommandDispatcher(policy, new IYokiFrameCommandHandler[] { handler });
    }

    internal static YokiFrameCommandRequest CreateRequest(
        string source,
        string action,
        string payloadJson = "{}",
        string kit = RoslynCommandHandler.KIT_NAME)
    {
        return new YokiFrameCommandRequest(
            source,
            kit,
            action,
            payloadJson,
            5000,
            0L,
            "req-" + Guid.NewGuid().ToString("N"),
            DateTimeOffset.UtcNow);
    }
}
