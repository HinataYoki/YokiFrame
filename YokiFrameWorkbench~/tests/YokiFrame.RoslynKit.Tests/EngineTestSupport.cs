using YokiFrame;

namespace YokiFrame.RoslynKit.Tests;

/// <summary>提供可计数的 Engine 开关读取端口，用于验证 Gate 是否真的被调用。</summary>
internal sealed class StubEngineSettingsSource : IRoslynSettingsSource
{
    /// <summary>固定初始快照。不读取磁盘，读取次数从零开始。</summary>
    /// <param name="snapshot">后续 <see cref="Read"/> 返回的快照。</param>
    internal StubEngineSettingsSource(RoslynSettingsSnapshot snapshot)
    {
        Snapshot = snapshot;
    }

    /// <summary>获取或设置当前开关快照；测试可据此模拟运行中开关变化。</summary>
    internal RoslynSettingsSnapshot Snapshot { get; set; }

    /// <summary>获取开关读取次数；Policy 先行拒绝时必须为 0。</summary>
    internal int ReadCount { get; private set; }

    /// <summary>返回当前快照并把读取次数加一。不复制快照。</summary>
    /// <returns>最近一次设置的快照。</returns>
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

    /// <summary>记录描述符，调用次数从零开始。不注册到调度器。</summary>
    /// <param name="action">操作名。</param>
    /// <param name="kind">命令种类。</param>
    /// <param name="isDiagnostic">是否为诊断豁免。</param>
    /// <param name="isCancellation">是否为取消豁免。</param>
    /// <param name="targets">操作支持的目标。</param>
    /// <param name="isTargetAgnostic">是否忽略宿主目标绑定。</param>
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

    /// <summary>把调用次数加一并返回固定成功结果。不读取请求内容。</summary>
    /// <param name="request">被忽略的请求。</param>
    /// <returns>固定的成功 JSON。</returns>
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

    /// <summary>装配真实调度管线，并交出可计数的设置源。</summary>
    /// <param name="settings">初始开关快照。</param>
    /// <param name="settingsSource">接收本次创建的设置源。</param>
    /// <param name="operations">注册到同一个处理器的操作。</param>
    /// <returns>可直接 Dispatch 的调度器。</returns>
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

    /// <summary>装配带宿主目标限制的调度管线。设置源同样可计数。</summary>
    /// <param name="settings">初始开关快照。</param>
    /// <param name="hostTargets">宿主实际承载的目标。</param>
    /// <param name="settingsSource">接收本次创建的设置源。</param>
    /// <param name="operations">注册到同一个处理器的操作。</param>
    /// <returns>可直接 Dispatch 的调度器。</returns>
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

    /// <summary>构造一条带新请求号的命令。不发送、不写文件。</summary>
    /// <param name="source">命令来源。</param>
    /// <param name="action">操作名。</param>
    /// <param name="payloadJson">请求 JSON，默认空对象。</param>
    /// <param name="kit">目标 Kit 名。</param>
    /// <returns>尚未调度的请求。</returns>
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
