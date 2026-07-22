namespace YokiFrame.Tooling.Application.Models.PoolKit;

/// <summary>提供 Workbench 可直接绑定的 PoolKit 强类型状态。</summary>
public sealed class WorkbenchPoolKitState
{
    /// <summary>创建完整 PoolKit 状态；只允许 Application parser 构造。</summary>
    internal WorkbenchPoolKitState(
        WorkbenchPoolKitDataSource dataSource,
        long version,
        WorkbenchPoolKitStats stats,
        IReadOnlyList<WorkbenchPoolKitPool> pools,
        IReadOnlyList<WorkbenchPoolKitEvent> events,
        WorkbenchPoolKitLeakReport leaks,
        int poolTotal,
        int eventTotal,
        bool poolsTruncated,
        bool eventsTruncated)
    {
        DataSource = dataSource;
        Version = version;
        Stats = stats;
        Pools = pools;
        Events = events;
        Leaks = leaks;
        PoolTotal = poolTotal;
        EventTotal = eventTotal;
        PoolsTruncated = poolsTruncated;
        EventsTruncated = eventsTruncated;
    }

    private WorkbenchPoolKitDataSource DataSource { get; }
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
    /// <summary>获取聚合统计和诊断开关。</summary>
    public WorkbenchPoolKitStats Stats { get; }
    /// <summary>获取有界对象池列表。</summary>
    public IReadOnlyList<WorkbenchPoolKitPool> Pools { get; }
    /// <summary>获取最新优先的有界事件流。</summary>
    public IReadOnlyList<WorkbenchPoolKitEvent> Events { get; }
    /// <summary>获取显式泄漏检查投影。</summary>
    public WorkbenchPoolKitLeakReport Leaks { get; }
    /// <summary>获取 Runtime 当前对象池总量。</summary>
    public int PoolTotal { get; }
    /// <summary>获取 Runtime 当前事件历史总量。</summary>
    public int EventTotal { get; }
    /// <summary>获取对象池列表是否被 payload 上限裁剪。</summary>
    public bool PoolsTruncated { get; }
    /// <summary>获取事件列表是否被 payload 上限裁剪。</summary>
    public bool EventsTruncated { get; }
    /// <summary>获取对象池总量，供页面和测试使用。</summary>
    public int PoolCount => Stats.PoolCount;
    /// <summary>获取当前借出对象总量。</summary>
    public int TotalActive => Stats.TotalActive;
}

/// <summary>描述 PoolKit 聚合指标和诊断开关。</summary>
public sealed record WorkbenchPoolKitStats(
    int PoolCount,
    int TotalCount,
    int TotalActive,
    int TotalInactive,
    int TotalPeak,
    bool TrackingEnabled,
    bool StackTraceEnabled,
    bool EventHistoryEnabled,
    int EventHistoryCount);

/// <summary>描述一个对象池的指标和有界对象明细。</summary>
public sealed record WorkbenchPoolKitPool(
    string Identity,
    string Name,
    string TypeName,
    int TotalCount,
    int ActiveCount,
    int InactiveCount,
    int PeakCount,
    int MaxCacheCount,
    double UsageRate,
    string HealthStatus,
    int ActiveObjectTotal,
    bool ActiveObjectTruncated,
    int InactiveObjectTotal,
    bool InactiveObjectTruncated,
    IReadOnlyList<WorkbenchPoolKitObject> ActiveObjects,
    IReadOnlyList<WorkbenchPoolKitObject> InactiveObjects)
{
    /// <summary>获取 Runtime 会话内稳定对象池标识；旧 payload 缺失时回退到帧内 identity。</summary>
    public string PoolId { get; init; } = string.Empty;
    /// <summary>获取用于跨帧关联的稳定对象池标识。</summary>
    public string StablePoolId => string.IsNullOrWhiteSpace(PoolId) ? Identity : PoolId;
    /// <summary>获取最大缓存数量展示文本。</summary>
    public string MaxCacheCountText => MaxCacheCount < 0 ? "不限" : MaxCacheCount.ToString();
}

/// <summary>描述一个借出或池内对象。</summary>
public sealed record WorkbenchPoolKitObject(
    string ObjectName,
    double SpawnTime,
    string SourceFile,
    int SourceLine)
{
    /// <summary>获取是否存在可显示源码位置。</summary>
    public bool HasSourceLocation => !string.IsNullOrWhiteSpace(SourceFile) && SourceLine > 0;
}

/// <summary>描述一条借出、归还或强制归还事件。</summary>
public sealed record WorkbenchPoolKitEvent(
    string EventType,
    double Timestamp,
    string PoolName,
    string ObjectName,
    string SourceFile,
    int SourceLine)
{
    /// <summary>获取事件所属对象池的稳定标识；旧 payload 可为空。</summary>
    public string PoolId { get; init; } = string.Empty;
    /// <summary>获取是否存在可显示源码位置。</summary>
    public bool HasSourceLocation => !string.IsNullOrWhiteSpace(SourceFile) && SourceLine > 0;
}

/// <summary>描述一次疑似未归还对象检查结果。</summary>
public sealed record WorkbenchPoolKitLeakReport(
    IReadOnlyList<WorkbenchPoolKitSuspectedLeak> SuspectedLeaks,
    int Count,
    bool TrackingEnabled)
{
    /// <summary>获取所有疑似未归还池的真实总数，不受 payload 明细预算限制。</summary>
    public int Total { get; init; } = Count;
    /// <summary>获取疑似未归还明细是否因为 payload 预算被裁剪。</summary>
    public bool Truncated { get; init; }
}

/// <summary>描述一个仍有借出对象的对象池线索。</summary>
public sealed record WorkbenchPoolKitSuspectedLeak(string PoolName, int ActiveCount, int PeakCount)
{
    /// <summary>获取候选对象池的稳定标识；旧 payload 可为空。</summary>
    public string PoolId { get; init; } = string.Empty;
}
