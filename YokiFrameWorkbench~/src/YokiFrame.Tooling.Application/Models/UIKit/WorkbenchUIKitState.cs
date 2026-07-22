namespace YokiFrame.Tooling.Application.Models.UIKit;

/// <summary>提供 Workbench 可直接绑定的 UIKit 强类型只读状态。</summary>
public sealed class WorkbenchUIKitState
{
    /// <summary>创建完整 UIKit 状态；只允许 Application parser 构造。</summary>
    internal WorkbenchUIKitState(
        WorkbenchUIKitDataSource source,
        int schemaVersion,
        WorkbenchUIKitRoot root,
        WorkbenchUIKitStats stats,
        WorkbenchUIKitCache cache,
        WorkbenchUIKitModal modal,
        IReadOnlyList<WorkbenchUIKitPanel> panels,
        IReadOnlyList<WorkbenchUIKitStack> stacks,
        int panelTotal,
        int panelReturned,
        bool panelsTruncated,
        int stackTotal,
        int stackReturned,
        bool stacksTruncated)
    {
        DataSource = source;
        SchemaVersion = schemaVersion;
        Root = root;
        Stats = stats;
        Cache = cache;
        Modal = modal;
        Panels = panels;
        Stacks = stacks;
        PanelTotal = panelTotal;
        PanelReturned = panelReturned;
        PanelsTruncated = panelsTruncated;
        StackTotal = stackTotal;
        StackReturned = stackReturned;
        StacksTruncated = stacksTruncated;
    }

    private WorkbenchUIKitDataSource DataSource { get; }

    /// <summary>获取目标 engine。</summary>
    public string EngineId => DataSource.EngineId;
    /// <summary>获取宿主 session。</summary>
    public string SessionId => DataSource.SessionId;
    /// <summary>获取宿主 generation。</summary>
    public long Generation => DataSource.Generation;
    /// <summary>获取宿主模式。</summary>
    public string Mode => DataSource.Mode;
    /// <summary>获取 telemetry 或 snapshot 来源。</summary>
    public string Source => DataSource.Source;
    /// <summary>获取显式命令实际传输；周期状态为空。</summary>
    public string Transport => DataSource.Transport;
    /// <summary>获取本地观察更新时间。</summary>
    public DateTimeOffset UpdatedAtUtc => DataSource.UpdatedAtUtc;
    /// <summary>获取回落、过期或解析失败原因。</summary>
    public string StaleReason => DataSource.StaleReason;
    /// <summary>获取来源证据路径。</summary>
    public IReadOnlyList<string> EvidencePaths => DataSource.EvidencePaths;
    /// <summary>获取 UIKit payload schema 版本。</summary>
    public int SchemaVersion { get; }
    /// <summary>获取 UIRoot 存在性。</summary>
    public WorkbenchUIKitRoot Root { get; }
    /// <summary>获取面板、栈和生命周期统计。</summary>
    public WorkbenchUIKitStats Stats { get; }
    /// <summary>获取缓存策略统计。</summary>
    public WorkbenchUIKitCache Cache { get; }
    /// <summary>获取模态面板和 blocker 状态。</summary>
    public WorkbenchUIKitModal Modal { get; }
    /// <summary>获取有界面板列表。</summary>
    public IReadOnlyList<WorkbenchUIKitPanel> Panels { get; }
    /// <summary>获取有界命名栈列表。</summary>
    public IReadOnlyList<WorkbenchUIKitStack> Stacks { get; }
    /// <summary>获取 Runtime 面板总量。</summary>
    public int PanelTotal { get; }
    /// <summary>获取 payload 实际返回的面板数量。</summary>
    public int PanelReturned { get; }
    /// <summary>获取面板集合是否因 payload 上限被裁剪。</summary>
    public bool PanelsTruncated { get; }
    /// <summary>获取 Runtime 命名栈总量。</summary>
    public int StackTotal { get; }
    /// <summary>获取 payload 实际返回的命名栈数量。</summary>
    public int StackReturned { get; }
    /// <summary>获取命名栈集合是否因 payload 上限被裁剪。</summary>
    public bool StacksTruncated { get; }
}

/// <summary>描述当前 UIKit Root 是否已存在。</summary>
public sealed record WorkbenchUIKitRoot(bool Exists);

/// <summary>描述 UIKit 数量和生命周期状态桶。</summary>
public sealed record WorkbenchUIKitStats(
    int PanelCount,
    int StackCount,
    int StackMembershipCount,
    WorkbenchUIKitPanelStates States);

/// <summary>描述每个公开 PanelState 的当前实例数量。</summary>
public sealed record WorkbenchUIKitPanelStates(
    int Preloaded,
    int Opening,
    int Open,
    int Hiding,
    int Hidden,
    int Closing,
    int Cached,
    int Closed);

/// <summary>描述显式缓存策略和 Reusable 缓存占用。</summary>
public sealed record WorkbenchUIKitCache(
    int Capacity,
    int Transient,
    int Reusable,
    int ReusableCached,
    int Persistent);

/// <summary>描述当前模态面板和 blocker 状态。</summary>
public sealed record WorkbenchUIKitModal(bool BlockerActive, int PanelCount);

/// <summary>描述一个已加载面板的公开只读状态。</summary>
public sealed record WorkbenchUIKitPanel(
    string Type,
    string Name,
    string State,
    string Level,
    int LevelOrder,
    int SubLevel,
    string CachePolicy,
    bool IsModal,
    string? StackName);

/// <summary>描述一个命名栈的深度和顶部面板。</summary>
public sealed record WorkbenchUIKitStack(
    string Name,
    int Depth,
    string? TopPanelType,
    string? TopPanelName);
