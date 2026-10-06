namespace YokiFrame.EngineKit.Tests;

/// <summary>测试用的引擎侧 Provider：声明身份、宿主目标与操作集合。</summary>
internal sealed class StubEngineOperationProvider : IYokiFrameEngineOperationProvider, IYokiFrameEngineScriptEvalProvider
{
    private readonly IYokiFrameEngineOperation[] _operations;

    internal StubEngineOperationProvider(
        string engineKind,
        YokiFrameEngineExecutionTarget hostTargets,
        params IYokiFrameEngineOperation[] operations)
    {
        EngineKind = engineKind;
        HostTargets = hostTargets;
        _operations = operations;
    }

    public string EngineKind { get; }

    public string EngineVersion => "1.2.3-test";

    public YokiFrameEngineExecutionTarget HostTargets { get; }

    public IReadOnlyList<IYokiFrameEngineOperation> Operations => _operations;

    /// <summary>获取或设置运行调度器；未接线时为空。</summary>
    internal IYokiFrameEngineRunScheduler? Runs { get; set; }

    IYokiFrameEngineRunScheduler IYokiFrameEngineOperationProvider.Runs => Runs;

    /// <summary>获取或设置脚本 eval 服务；未接线时为空。</summary>
    internal IYokiFrameEngineScriptEvalService? ScriptEvalService { get; set; }

    IYokiFrameEngineScriptEvalService IYokiFrameEngineScriptEvalProvider.ScriptEval => ScriptEvalService!;

    internal YokiFrameEngineDomainState State { get; set; } = new YokiFrameEngineDomainState
    {
        EngineKind = "Unity",
        EngineVersion = "1.2.3-test",
        Mode = "EditMode",
        ActiveTarget = YokiFrameEngineExecutionTargets.EDITOR,
        IsPlaying = false,
        IsCompiling = false,
        IsBusy = false,
        SessionId = string.Empty,
        Generation = 0L,
        SessionIdentityAvailable = false
    };

    public YokiFrameEngineDomainState ReadDomainState()
    {
        return State;
    }
}
