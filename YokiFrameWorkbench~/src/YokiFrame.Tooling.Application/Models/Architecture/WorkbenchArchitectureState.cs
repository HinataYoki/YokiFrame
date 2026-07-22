namespace YokiFrame.Tooling.Application.Models.Architecture;

/// <summary>
/// 提供 Workbench 可直接绑定的 Architecture 强类型状态。
/// </summary>
public sealed class WorkbenchArchitectureState
{
    /// <summary>创建完整 Architecture 状态；只允许 Application parser 构造。</summary>
    internal WorkbenchArchitectureState(
        WorkbenchArchitectureDataSource dataSource,
        long diagnosticVersion,
        int declaredCount,
        int declaredAliveCount,
        int declaredServiceCount,
        IReadOnlyList<WorkbenchArchitectureInstance> architectures)
    {
        DataSource = dataSource;
        DiagnosticVersion = diagnosticVersion;
        DeclaredCount = declaredCount;
        DeclaredAliveCount = declaredAliveCount;
        DeclaredServiceCount = declaredServiceCount;
        Architectures = architectures;
    }

    private WorkbenchArchitectureDataSource DataSource { get; }

    /// <summary>获取目标 engine 标识。</summary>
    public string EngineId => DataSource.EngineId;

    /// <summary>获取宿主 session 标识。</summary>
    public string SessionId => DataSource.SessionId;

    /// <summary>获取宿主 generation。</summary>
    public long Generation => DataSource.Generation;

    /// <summary>获取宿主当前模式。</summary>
    public string Mode => DataSource.Mode;

    /// <summary>获取 telemetry 或 snapshot 来源。</summary>
    public string Source => DataSource.Source;

    /// <summary>获取状态更新时间。</summary>
    public DateTimeOffset UpdatedAtUtc => DataSource.UpdatedAtUtc;

    /// <summary>获取数据回落、过期或解析失败原因。</summary>
    public string StaleReason => DataSource.StaleReason;

    /// <summary>获取状态证据路径。</summary>
    public IReadOnlyList<string> EvidencePaths => DataSource.EvidencePaths;

    /// <summary>获取未经裁剪的 Architecture payload。</summary>
    public string RawPayloadJson => DataSource.RawPayloadJson;

    /// <summary>获取 Runtime 注册表诊断版本。</summary>
    public long DiagnosticVersion { get; }

    /// <summary>获取 payload 声明的 Architecture 数量。</summary>
    public int DeclaredCount { get; }

    /// <summary>获取 payload 声明的存活 Architecture 数量。</summary>
    public int DeclaredAliveCount { get; }

    /// <summary>获取 payload 声明的服务总数。</summary>
    public int DeclaredServiceCount { get; }

    /// <summary>获取全部 Architecture 实例。</summary>
    public IReadOnlyList<WorkbenchArchitectureInstance> Architectures { get; }
}

/// <summary>
/// 描述一个 Architecture 实例及其注册服务。
/// </summary>
public sealed class WorkbenchArchitectureInstance
{
    /// <summary>创建 Architecture 实例 read model。</summary>
    internal WorkbenchArchitectureInstance(
        string typeName,
        string fullName,
        string createdAtUtc,
        int instanceHash,
        bool isAlive,
        bool initialized,
        int declaredServiceCount,
        IReadOnlyList<WorkbenchArchitectureService> services)
    {
        TypeName = typeName;
        FullName = fullName;
        CreatedAtUtc = createdAtUtc;
        InstanceHash = instanceHash;
        IsAlive = isAlive;
        Initialized = initialized;
        DeclaredServiceCount = declaredServiceCount;
        Services = services;
    }

    /// <summary>获取架构类型短名称。</summary>
    public string TypeName { get; }

    /// <summary>获取架构类型完整名称。</summary>
    public string FullName { get; }

    /// <summary>获取首次登记的 UTC 时间文本。</summary>
    public string CreatedAtUtc { get; }

    /// <summary>获取实例 Hash。</summary>
    public int InstanceHash { get; }

    /// <summary>获取实例是否存活。</summary>
    public bool IsAlive { get; }

    /// <summary>获取实例是否已经释放。</summary>
    public bool IsDisposed => !IsAlive;

    /// <summary>获取实例是否完成初始化。</summary>
    public bool Initialized { get; }

    /// <summary>获取实例是否仍在等待初始化。</summary>
    public bool IsPending => !Initialized;

    /// <summary>获取 payload 声明的服务数量。</summary>
    public int DeclaredServiceCount { get; }

    /// <summary>获取注册服务列表。</summary>
    public IReadOnlyList<WorkbenchArchitectureService> Services { get; }
}

/// <summary>
/// 描述 Architecture 中一个服务契约与实现实例。
/// </summary>
public sealed class WorkbenchArchitectureService
{
    /// <summary>创建 Architecture 服务 read model。</summary>
    internal WorkbenchArchitectureService(
        string typeName,
        string fullName,
        string implementationTypeName,
        string implementationFullName,
        bool initialized,
        int instanceHash)
    {
        TypeName = typeName;
        FullName = fullName;
        ImplementationTypeName = implementationTypeName;
        ImplementationFullName = implementationFullName;
        Initialized = initialized;
        InstanceHash = instanceHash;
    }

    /// <summary>获取服务契约短名称。</summary>
    public string TypeName { get; }

    /// <summary>获取服务契约完整名称。</summary>
    public string FullName { get; }

    /// <summary>获取服务实现短名称。</summary>
    public string ImplementationTypeName { get; }

    /// <summary>获取服务实现完整名称。</summary>
    public string ImplementationFullName { get; }

    /// <summary>获取服务是否完成初始化。</summary>
    public bool Initialized { get; }

    /// <summary>获取服务是否仍在等待初始化。</summary>
    public bool IsPending => !Initialized;

    /// <summary>获取服务实例 Hash。</summary>
    public int InstanceHash { get; }
}
