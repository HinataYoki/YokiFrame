namespace YokiFrame.Tooling.Application.Models.Capabilities;

/// <summary>
/// 描述由 Project Model、harness 回退、宿主注册表和可选实时命令目录共同投影的能力目录。
/// </summary>
public sealed class CapabilityCatalog
{
    /// <summary>
    /// 创建不可变能力目录。
    /// </summary>
    public CapabilityCatalog(
        int schemaVersion,
        string generatedAtUtc,
        string state,
        CapabilityCatalogProject project,
        IReadOnlyList<CapabilityCatalogEngine> engines,
        IReadOnlyList<CapabilityCatalogKit> kits,
        IReadOnlyList<CapabilityCatalogIssue> issues,
        IReadOnlyList<CapabilityCatalogSource> sources)
    {
        SchemaVersion = schemaVersion;
        GeneratedAtUtc = generatedAtUtc;
        State = state;
        Project = project;
        Engines = engines.ToArray();
        Kits = kits.ToArray();
        Issues = issues.ToArray();
        Sources = sources.ToArray();
    }

    /// <summary>获取能力目录 schema 版本。</summary>
    public int SchemaVersion { get; }

    /// <summary>获取本次聚合生成时间。</summary>
    public string GeneratedAtUtc { get; }

    /// <summary>获取 Ready、Partial、Drifted 或 Blocked 状态。</summary>
    public string State { get; }

    /// <summary>获取静态项目与包声明摘要。</summary>
    public CapabilityCatalogProject Project { get; }

    /// <summary>获取当前发现的宿主能力。</summary>
    public IReadOnlyList<CapabilityCatalogEngine> Engines { get; }

    /// <summary>获取按 Kit 聚合且保留来源差异的能力。</summary>
    public IReadOnlyList<CapabilityCatalogKit> Kits { get; }

    /// <summary>获取缺失、过期或漂移问题。</summary>
    public IReadOnlyList<CapabilityCatalogIssue> Issues { get; }

    /// <summary>获取构成本目录的证据来源。</summary>
    public IReadOnlyList<CapabilityCatalogSource> Sources { get; }
}

/// <summary>
/// 描述正式 Project Model 身份，以及 harness 提供的协议回退摘要。
/// </summary>
public sealed class CapabilityCatalogProject
{
    /// <summary>创建项目声明摘要。</summary>
    public CapabilityCatalogProject(
        string modelState,
        string projectRoot,
        string packageName,
        string packageVersion,
        string packageRoot,
        int fileBridgeVersion,
        int telemetryVersion,
        int fastChannelVersion,
        IReadOnlyList<string> declaredEngineKinds,
        IReadOnlyList<string> declaredKitIds,
        string modelId = "",
        string modelGeneration = "",
        string modelPath = ".yokiframe/project/project-model.json",
        string inputHash = "")
    {
        ModelState = modelState;
        ModelId = modelId;
        ModelGeneration = modelGeneration;
        ModelPath = modelPath;
        InputHash = inputHash;
        ProjectRoot = projectRoot;
        PackageName = packageName;
        PackageVersion = packageVersion;
        PackageRoot = packageRoot;
        FileBridgeVersion = fileBridgeVersion;
        TelemetryVersion = telemetryVersion;
        FastChannelVersion = fastChannelVersion;
        DeclaredEngineKinds = declaredEngineKinds.ToArray();
        DeclaredKitIds = declaredKitIds.ToArray();
    }

    /// <summary>获取 Project Model 的 Ready、Missing、Stale、Partial 或 Blocked 状态。</summary>
    public string ModelState { get; }

    /// <summary>获取已校验 Project Model 的稳定标识。</summary>
    public string ModelId { get; }

    /// <summary>获取已校验 Project Model 的 generation。</summary>
    public string ModelGeneration { get; }

    /// <summary>获取 Project Model manifest 的项目相对路径。</summary>
    public string ModelPath { get; }

    /// <summary>获取生成 Project Model 时计算的权威输入哈希。</summary>
    public string InputHash { get; }

    /// <summary>获取当前项目根路径。</summary>
    public string ProjectRoot { get; }

    /// <summary>获取包名。</summary>
    public string PackageName { get; }

    /// <summary>获取包版本。</summary>
    public string PackageVersion { get; }

    /// <summary>获取包相对根路径。</summary>
    public string PackageRoot { get; }

    /// <summary>获取 FileBridge 协议版本。</summary>
    public int FileBridgeVersion { get; }

    /// <summary>获取 Shared Memory Telemetry 协议版本。</summary>
    public int TelemetryVersion { get; }

    /// <summary>获取 FastChannel 协议版本。</summary>
    public int FastChannelVersion { get; }

    /// <summary>获取静态声明的引擎类型。</summary>
    public IReadOnlyList<string> DeclaredEngineKinds { get; }

    /// <summary>获取静态声明的 Kit 标识。</summary>
    public IReadOnlyList<string> DeclaredKitIds { get; }
}

/// <summary>
/// 描述单个引擎宿主的身份、在线状态和实时命令目录。
/// </summary>
public sealed class CapabilityCatalogEngine
{
    /// <summary>创建宿主能力摘要。</summary>
    public CapabilityCatalogEngine(
        string engineId,
        string engine,
        string version,
        string adapterVersion,
        string mode,
        string sessionId,
        long generation,
        bool online,
        string identityState,
        IReadOnlyList<string> declaredCapabilities,
        CapabilityCatalogCommandSet commandCatalog)
    {
        EngineId = engineId;
        Engine = engine;
        Version = version;
        AdapterVersion = adapterVersion;
        Mode = mode;
        SessionId = sessionId;
        Generation = generation;
        Online = online;
        IdentityState = identityState;
        DeclaredCapabilities = declaredCapabilities.ToArray();
        CommandCatalog = commandCatalog;
    }

    /// <summary>获取 engine 标识。</summary>
    public string EngineId { get; }

    /// <summary>获取 engine 类型。</summary>
    public string Engine { get; }

    /// <summary>获取 engine 版本。</summary>
    public string Version { get; }

    /// <summary>获取 Adapter 版本。</summary>
    public string AdapterVersion { get; }

    /// <summary>获取宿主模式。</summary>
    public string Mode { get; }

    /// <summary>获取会话标识。</summary>
    public string SessionId { get; }

    /// <summary>获取宿主 generation。</summary>
    public long Generation { get; }

    /// <summary>获取 heartbeat 是否证明宿主在线。</summary>
    public bool Online { get; }

    /// <summary>获取 registry 与 heartbeat 身份状态。</summary>
    public string IdentityState { get; }

    /// <summary>获取 registry 静态声明的传输能力。</summary>
    public IReadOnlyList<string> DeclaredCapabilities { get; }

    /// <summary>获取可选实时命令目录。</summary>
    public CapabilityCatalogCommandSet CommandCatalog { get; }
}

/// <summary>
/// 描述一次 System/list_commands 观测结果。
/// </summary>
public sealed class CapabilityCatalogCommandSet
{
    /// <summary>创建命令目录观测结果。</summary>
    public CapabilityCatalogCommandSet(
        string state,
        long sequence,
        string transport,
        IReadOnlyList<CapabilityCatalogCommand> commands,
        IReadOnlyList<string> evidencePaths)
    {
        State = state;
        Sequence = sequence;
        Transport = transport;
        Commands = commands.ToArray();
        EvidencePaths = evidencePaths.ToArray();
    }

    /// <summary>获取 NotRequested、Observed、Stale 或 Failed 状态。</summary>
    public string State { get; }

    /// <summary>获取宿主返回的命令目录序号。</summary>
    public long Sequence { get; }

    /// <summary>获取实际命令传输。</summary>
    public string Transport { get; }

    /// <summary>获取已观测命令。</summary>
    public IReadOnlyList<CapabilityCatalogCommand> Commands { get; }

    /// <summary>获取 FileBridge 证据路径。</summary>
    public IReadOnlyList<string> EvidencePaths { get; }
}

/// <summary>
/// 描述一个带宿主来源的 Kit/action 能力。
/// </summary>
public sealed class CapabilityCatalogCommand
{
    /// <summary>创建命令能力。</summary>
    public CapabilityCatalogCommand(
        string engineId,
        string kit,
        string action,
        string kind)
    {
        EngineId = engineId;
        Kit = kit;
        Action = action;
        Kind = kind;
    }

    /// <summary>获取能力所属 engine。</summary>
    public string EngineId { get; }

    /// <summary>获取 Kit 标识。</summary>
    public string Kit { get; }

    /// <summary>获取 action 标识。</summary>
    public string Action { get; }

    /// <summary>获取 ReadOnly、Maintenance、UserAction 或 Dangerous 风险类型。</summary>
    public string Kind { get; }

}

/// <summary>
/// 描述一个 Kit 的静态声明与实时观测关系。
/// </summary>
public sealed class CapabilityCatalogKit
{
    /// <summary>创建 Kit 能力摘要。</summary>
    public CapabilityCatalogKit(
        string kit,
        string availability,
        bool declaredSnapshot,
        bool declaredCommand,
        IReadOnlyList<CapabilityCatalogCommand> observedCommands,
        IReadOnlyList<string> sources)
    {
        Kit = kit;
        Availability = availability;
        DeclaredSnapshot = declaredSnapshot;
        DeclaredCommand = declaredCommand;
        ObservedCommands = observedCommands.ToArray();
        Sources = sources.ToArray();
    }

    /// <summary>获取 Kit 标识。</summary>
    public string Kit { get; }

    /// <summary>获取 Declared、Available、Drifted 或 Unavailable 状态。</summary>
    public string Availability { get; }

    /// <summary>获取静态 harness 是否声明 snapshot。</summary>
    public bool DeclaredSnapshot { get; }

    /// <summary>获取静态 harness 是否声明 command。</summary>
    public bool DeclaredCommand { get; }

    /// <summary>获取实时观测到的命令。</summary>
    public IReadOnlyList<CapabilityCatalogCommand> ObservedCommands { get; }

    /// <summary>获取能力来源标签。</summary>
    public IReadOnlyList<string> Sources { get; }
}

/// <summary>
/// 描述能力目录中的缺失、过期、冲突或漂移问题。
/// </summary>
public sealed class CapabilityCatalogIssue
{
    /// <summary>创建能力目录问题。</summary>
    public CapabilityCatalogIssue(
        string code,
        string severity,
        string scope,
        string message,
        string suggestion,
        IReadOnlyList<string> evidencePaths)
    {
        Code = code;
        Severity = severity;
        Scope = scope;
        Message = message;
        Suggestion = suggestion;
        EvidencePaths = evidencePaths.ToArray();
    }

    /// <summary>获取稳定问题码。</summary>
    public string Code { get; }

    /// <summary>获取 Warning 或 Error 严重度。</summary>
    public string Severity { get; }

    /// <summary>获取问题所属项目、engine 或 Kit 范围。</summary>
    public string Scope { get; }

    /// <summary>获取问题说明。</summary>
    public string Message { get; }

    /// <summary>获取恢复建议。</summary>
    public string Suggestion { get; }

    /// <summary>获取问题证据路径。</summary>
    public IReadOnlyList<string> EvidencePaths { get; }
}

/// <summary>
/// 描述能力目录使用的单个事实来源。
/// </summary>
public sealed class CapabilityCatalogSource
{
    /// <summary>创建事实来源。</summary>
    public CapabilityCatalogSource(string kind, string path, string status, string engineId = "")
    {
        Kind = kind;
        Path = path;
        Status = status;
        EngineId = engineId;
    }

    /// <summary>获取来源类型。</summary>
    public string Kind { get; }

    /// <summary>获取来源路径或逻辑命令名。</summary>
    public string Path { get; }

    /// <summary>获取来源读取状态。</summary>
    public string Status { get; }

    /// <summary>获取可选 engine 标识。</summary>
    public string EngineId { get; }
}
