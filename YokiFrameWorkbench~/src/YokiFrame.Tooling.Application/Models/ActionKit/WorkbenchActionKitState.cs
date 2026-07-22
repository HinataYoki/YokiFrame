namespace YokiFrame.Tooling.Application.Models.ActionKit;

/// <summary>提供 Workbench 可直接绑定的 ActionKit 强类型状态。</summary>
public sealed class WorkbenchActionKitState
{
    /// <summary>创建完整 ActionKit 状态；只允许 Application parser 构造。</summary>
    internal WorkbenchActionKitState(
        WorkbenchActionKitDataSource dataSource,
        long version,
        WorkbenchActionKitStats stats,
        IReadOnlyList<WorkbenchActionKitRoot> roots,
        IReadOnlyList<WorkbenchActionKitEvent> events,
        int rootTotal,
        long eventTotal,
        bool rootsTruncated,
        bool nodesTruncated,
        bool depthTruncated,
        bool stackTruncated,
        bool eventsTruncated)
    {
        DataSource = dataSource;
        Version = version;
        Stats = stats;
        Roots = roots;
        Events = events;
        RootTotal = rootTotal;
        EventTotal = eventTotal;
        RootsTruncated = rootsTruncated;
        NodesTruncated = nodesTruncated;
        DepthTruncated = depthTruncated;
        StackTruncated = stackTruncated;
        EventsTruncated = eventsTruncated;
    }

    private WorkbenchActionKitDataSource DataSource { get; }

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

    /// <summary>获取 ActionKit 聚合指标。</summary>
    public WorkbenchActionKitStats Stats { get; }

    /// <summary>获取有界活动根动作树。</summary>
    public IReadOnlyList<WorkbenchActionKitRoot> Roots { get; }

    /// <summary>获取最新优先的根动作终态。</summary>
    public IReadOnlyList<WorkbenchActionKitEvent> Events { get; }

    /// <summary>获取 Runtime 活动根总量。</summary>
    public int RootTotal { get; }

    /// <summary>获取当前会话累计终态总量。</summary>
    public long EventTotal { get; }

    /// <summary>获取根列表是否被裁剪。</summary>
    public bool RootsTruncated { get; }

    /// <summary>获取节点总预算是否被裁剪。</summary>
    public bool NodesTruncated { get; }

    /// <summary>获取树深度是否被裁剪。</summary>
    public bool DepthTruncated { get; }

    /// <summary>获取堆栈根或帧是否被裁剪。</summary>
    public bool StackTruncated { get; }

    /// <summary>获取终态事件是否被裁剪。</summary>
    public bool EventsTruncated { get; }
}

/// <summary>描述 ActionKit 累计指标和堆栈诊断状态。</summary>
public sealed record WorkbenchActionKitStats(
    long FrameCount,
    int ActiveRootCount,
    long FinishedCount,
    long CancelledCount,
    long FaultedCount,
    long TerminalEventCount,
    bool StackTraceEnabled,
    int StackTraceCount);

/// <summary>描述一个活动根动作及其 controller 配置。</summary>
public sealed record WorkbenchActionKitRoot(
    string ActionId,
    string Type,
    string Status,
    bool Paused,
    bool Deinited,
    string DebugInfo,
    string UpdateMode,
    bool CancelRequested,
    IReadOnlyList<WorkbenchActionKitStackFrame> StackTrace,
    IReadOnlyList<WorkbenchActionKitNode> Children,
    int ChildCount = 0,
    int CurrentChildIndex = -1,
    string ExecutorName = "PlayerLoop");

/// <summary>描述动作树中的一个非根节点。</summary>
public sealed record WorkbenchActionKitNode(
    string ActionId,
    string Type,
    string Status,
    bool Paused,
    bool Deinited,
    string DebugInfo,
    IReadOnlyList<WorkbenchActionKitNode> Children,
    int ChildCount = 0,
    int CurrentChildIndex = -1,
    string ExecutorName = "PlayerLoop",
    string UpdateMode = "");

/// <summary>描述不包含宿主绝对路径的 Action Start 调用帧。</summary>
public sealed record WorkbenchActionKitStackFrame(string Method, string File, int Line);

/// <summary>描述一个根 Action 的最近终态。</summary>
public sealed record WorkbenchActionKitEvent(
    string ActionId,
    string ActionType,
    string Outcome,
    long Frame,
    string ErrorMessage);
