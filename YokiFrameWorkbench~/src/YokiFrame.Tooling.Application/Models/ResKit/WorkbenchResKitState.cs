namespace YokiFrame.Tooling.Application.Models.ResKit;

/// <summary>提供 Workbench 可直接绑定的 ResKit 强类型状态。</summary>
public sealed class WorkbenchResKitState
{
    /// <summary>创建完整 ResKit 状态；只允许 Application parser 构造。</summary>
    internal WorkbenchResKitState(
        WorkbenchResKitDataSource dataSource,
        long version,
        WorkbenchResKitProvider provider,
        WorkbenchResKitStats stats,
        IReadOnlyList<WorkbenchResKitResource> resources,
        IReadOnlyList<WorkbenchResKitUnloadRecord> unloadHistory,
        int resourceTotal,
        int historyTotal,
        long historyDroppedCount,
        bool resourcesTruncated,
        bool historyTruncated,
        string lastBackgroundFailure)
    {
        DataSource = dataSource;
        Version = version;
        Provider = provider;
        Stats = stats;
        Resources = resources;
        UnloadHistory = unloadHistory;
        ResourceTotal = resourceTotal;
        HistoryTotal = historyTotal;
        HistoryDroppedCount = historyDroppedCount;
        ResourcesTruncated = resourcesTruncated;
        HistoryTruncated = historyTruncated;
        LastBackgroundFailure = lastBackgroundFailure;
    }

    private WorkbenchResKitDataSource DataSource { get; }
    /// <summary>获取目标 engine。</summary>
    public string EngineId => DataSource.EngineId;
    /// <summary>获取宿主 session。</summary>
    public string SessionId => DataSource.SessionId;
    /// <summary>获取宿主 generation。</summary>
    public long Generation => DataSource.Generation;
    /// <summary>获取宿主模式。</summary>
    public string Mode => DataSource.Mode;
    /// <summary>获取 telemetry、snapshot 或 command 来源。</summary>
    public string Source => DataSource.Source;
    /// <summary>获取命令实际传输；周期状态为空。</summary>
    public string Transport => DataSource.Transport;
    /// <summary>获取本地观察更新时间。</summary>
    public DateTimeOffset UpdatedAtUtc => DataSource.UpdatedAtUtc;
    /// <summary>获取回落、过期或解析失败原因。</summary>
    public string StaleReason => DataSource.StaleReason;
    /// <summary>获取来源证据路径。</summary>
    public IReadOnlyList<string> EvidencePaths => DataSource.EvidencePaths;
    /// <summary>获取 Runtime 诊断版本。</summary>
    public long Version { get; }
    /// <summary>获取当前 Provider 信息。</summary>
    public WorkbenchResKitProvider Provider { get; }
    /// <summary>获取聚合统计和跟踪开关。</summary>
    public WorkbenchResKitStats Stats { get; }
    /// <summary>获取有界已加载资源列表。</summary>
    public IReadOnlyList<WorkbenchResKitResource> Resources { get; }
    /// <summary>获取最新优先的有界卸载历史。</summary>
    public IReadOnlyList<WorkbenchResKitUnloadRecord> UnloadHistory { get; }
    /// <summary>获取 Runtime 已加载资源总量。</summary>
    public int ResourceTotal { get; }
    /// <summary>获取 Runtime 卸载历史总量。</summary>
    public int HistoryTotal { get; }
    /// <summary>获取固定历史环已覆盖记录数。</summary>
    public long HistoryDroppedCount { get; }
    /// <summary>获取资源列表是否被 payload 上限裁剪。</summary>
    public bool ResourcesTruncated { get; }
    /// <summary>获取历史列表是否被 payload 上限裁剪。</summary>
    public bool HistoryTruncated { get; }
    /// <summary>获取最近后台加载失败摘要。</summary>
    public string LastBackgroundFailure { get; }
}

/// <summary>描述 ResKit Provider 身份、代次与 raw bytes/text 能力。</summary>
public sealed record WorkbenchResKitProvider(
    string Name,
    long Generation,
    bool SupportsRawBytes,
    bool SupportsRawText);

/// <summary>描述 ResKit 聚合指标和加载位置跟踪开关。</summary>
public sealed record WorkbenchResKitStats(
    int LoadedCount,
    int InFlightCount,
    int TotalLeaseCount,
    int UnloadHistoryCount,
    bool LoadLocationTrackingEnabled);

/// <summary>描述一个已加载资源及按需返回的独立 lease 来源。</summary>
public sealed record WorkbenchResKitResource(
    string Identity,
    string Path,
    string TypeName,
    string State,
    int LeaseCount,
    string ProviderName,
    long ProviderGeneration,
    int TrackedSourceCount,
    IReadOnlyList<WorkbenchResKitLoadSource> Sources,
    int SourceTotal,
    bool SourcesTruncated);

/// <summary>描述一次按需资源详情及其原子诊断版本。</summary>
public sealed record WorkbenchResKitResourceDetail(
    long Version,
    WorkbenchResKitResource Resource);

/// <summary>描述一条独立资源 lease 的加载位置。</summary>
public sealed record WorkbenchResKitLoadSource(
    string Display,
    string FilePath,
    int Line,
    int RefCount,
    bool IsAnonymous,
    bool IsTracked)
{
    /// <summary>获取是否存在可显示源码位置。</summary>
    public bool HasSourceLocation => !string.IsNullOrWhiteSpace(FilePath) && Line > 0;
}

/// <summary>描述一条不可变卸载历史记录。</summary>
public sealed record WorkbenchResKitUnloadRecord(
    string Identity,
    string Path,
    string TypeName,
    string ProviderName,
    string UnloadTimeUtc);
