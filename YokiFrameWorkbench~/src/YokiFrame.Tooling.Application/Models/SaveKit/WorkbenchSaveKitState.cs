namespace YokiFrame.Tooling.Application.Models.SaveKit;

/// <summary>提供 Workbench 可绑定的 SaveKit 后端、自动保存和无 payload 容器头状态。</summary>
public sealed class WorkbenchSaveKitState
{
    /// <summary>使用已验证的 Runtime state 创建强类型只读模型。</summary>
    internal WorkbenchSaveKitState(
        WorkbenchSaveKitDataSource dataSource,
        long version,
        WorkbenchSaveKitBackend backend,
        WorkbenchSaveKitAutoSave autoSave,
        IReadOnlyList<WorkbenchSaveKitRuntimeMeta> slots,
        int slotCount,
        int slotTotal,
        bool slotsTruncated,
        IReadOnlyList<WorkbenchSaveKitRuntimeMeta> globals,
        int globalCount,
        int globalTotal,
        bool globalsTruncated,
        bool metadataAvailable,
        bool metadataReadFailed)
    {
        DataSource = dataSource;
        Version = version;
        Backend = backend;
        AutoSave = autoSave;
        Slots = slots;
        SlotCount = slotCount;
        SlotTotal = slotTotal;
        SlotsTruncated = slotsTruncated;
        Globals = globals;
        GlobalCount = globalCount;
        GlobalTotal = globalTotal;
        GlobalsTruncated = globalsTruncated;
        MetadataAvailable = metadataAvailable;
        MetadataReadFailed = metadataReadFailed;
    }

    private WorkbenchSaveKitDataSource DataSource { get; }

    /// <summary>获取目标 engine 标识。</summary>
    public string EngineId => DataSource.EngineId;

    /// <summary>获取 Runtime session 标识。</summary>
    public string SessionId => DataSource.SessionId;

    /// <summary>获取 Runtime generation。</summary>
    public long Generation => DataSource.Generation;

    /// <summary>获取当前宿主模式。</summary>
    public string Mode => DataSource.Mode;

    /// <summary>获取 telemetry、snapshot 或 command 数据来源。</summary>
    public string Source => DataSource.Source;

    /// <summary>获取显式 command 的实际传输方式；周期状态为空。</summary>
    public string Transport => DataSource.Transport;

    /// <summary>获取本地接收状态的 UTC 时间。</summary>
    public DateTimeOffset UpdatedAtUtc => DataSource.UpdatedAtUtc;

    /// <summary>获取身份变化、传输回落或解析失败原因。</summary>
    public string StaleReason => DataSource.StaleReason;

    /// <summary>获取状态读取的证据路径。</summary>
    public IReadOnlyList<string> EvidencePaths => DataSource.EvidencePaths;

    /// <summary>获取 SaveKit Runtime Snapshot 版本。</summary>
    public long Version { get; }

    /// <summary>获取当前已实例化后端的配置状态。</summary>
    public WorkbenchSaveKitBackend Backend { get; }

    /// <summary>获取当前自动保存摘要。</summary>
    public WorkbenchSaveKitAutoSave AutoSave { get; }

    /// <summary>获取有界 Slot 容器头列表。</summary>
    public IReadOnlyList<WorkbenchSaveKitRuntimeMeta> Slots { get; }

    /// <summary>获取本帧实际携带的有效 Slot 容器头数量。</summary>
    public int SlotCount { get; }

    /// <summary>获取 Storage 枚举到的 Slot 目标数量。</summary>
    public int SlotTotal { get; }

    /// <summary>获取 Slot 条目是否被 Provider 或载荷预算裁剪。</summary>
    public bool SlotsTruncated { get; }

    /// <summary>获取有界 Global 容器头列表。</summary>
    public IReadOnlyList<WorkbenchSaveKitRuntimeMeta> Globals { get; }

    /// <summary>获取本帧实际携带的有效 Global 容器头数量。</summary>
    public int GlobalCount { get; }

    /// <summary>获取 Storage 枚举到的 Global 目标数量。</summary>
    public int GlobalTotal { get; }

    /// <summary>获取 Global 条目是否被 Provider 或载荷预算裁剪。</summary>
    public bool GlobalsTruncated { get; }

    /// <summary>获取 Runtime 是否已经存在 Storage 可供读取容器头。</summary>
    public bool MetadataAvailable { get; }

    /// <summary>获取本次容器头枚举是否遇到 Storage 读取失败。</summary>
    public bool MetadataReadFailed { get; }
}

/// <summary>描述 SaveKit 当前已存在后端的安全配置摘要。</summary>
public sealed record WorkbenchSaveKitBackend(
    bool StorageConfigured,
    bool SerializerConfigured,
    bool Ready,
    string StorageType,
    string SerializerId,
    string EncryptorId);

/// <summary>描述自动保存开关、目标和计时；未启用时 Target 为空。</summary>
public sealed record WorkbenchSaveKitAutoSave(
    bool Enabled,
    WorkbenchSaveKitTarget? Target,
    float IntervalSeconds,
    float ElapsedSeconds);

/// <summary>描述一个 Slot 或 Global 存档目标，不包含任何物理路径。</summary>
public sealed record WorkbenchSaveKitTarget(string Kind, string Name, int SlotId);

/// <summary>描述一个已经验证的 SaveKit 容器头；不包含模块 payload。</summary>
public sealed record WorkbenchSaveKitRuntimeMeta(
    WorkbenchSaveKitTarget Target,
    string DisplayName,
    int ContainerVersion,
    long CreatedTimestamp,
    long LastSavedTimestamp,
    string SerializerId);
