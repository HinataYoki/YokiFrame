namespace YokiFrame.RoslynKit.Tests;

/// <summary>测试用的引擎侧 Provider：声明身份、宿主目标与操作集合。</summary>
internal sealed class StubEngineOperationProvider : IRoslynOperationProvider, IRoslynScriptEvalProvider
{
    private readonly IRoslynOperation[] _operations;

    /// <summary>固定引擎种类、宿主目标和操作列表。调度器与脚本服务留空。</summary>
    /// <param name="engineKind">引擎种类。</param>
    /// <param name="hostTargets">宿主可承载的目标。</param>
    /// <param name="operations">声明的操作，可为空。</param>
    internal StubEngineOperationProvider(
        string engineKind,
        RoslynExecutionTarget hostTargets,
        params IRoslynOperation[] operations)
    {
        EngineKind = engineKind;
        HostTargets = hostTargets;
        _operations = operations;
    }

    public string EngineKind { get; }

    public string EngineVersion => "1.2.3-test";

    public RoslynExecutionTarget HostTargets { get; }

    public IReadOnlyList<IRoslynOperation> Operations => _operations;

    /// <summary>获取或设置运行调度器；未接线时为空。</summary>
    internal IRoslynRunScheduler? Runs { get; set; }

    IRoslynRunScheduler IRoslynOperationProvider.Runs => Runs;

    /// <summary>获取或设置脚本 eval 服务；未接线时为空。</summary>
    internal IRoslynScriptEvalService? ScriptEvalService { get; set; }

    IRoslynScriptEvalService IRoslynScriptEvalProvider.ScriptEval => ScriptEvalService!;

    internal RoslynDomainState State { get; set; } = new RoslynDomainState
    {
        EngineKind = "Unity",
        EngineVersion = "1.2.3-test",
        Mode = "EditMode",
        ActiveTarget = RoslynExecutionTargets.EDITOR,
        IsPlaying = false,
        IsCompiling = false,
        IsBusy = false,
        SessionId = string.Empty,
        Generation = 0L,
        SessionIdentityAvailable = false
    };

    /// <summary>返回当前领域状态对象本身。不复制，不触发初始化。</summary>
    /// <returns>测试预设的领域状态。</returns>
    public RoslynDomainState ReadDomainState()
    {
        return State;
    }
}
