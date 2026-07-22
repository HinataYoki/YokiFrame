using System.Collections.ObjectModel;
using YokiFrame.Tooling.Application.Models.EventKit;
using YokiFrame.Tooling.Application.Models.EventKit.Scan;
using YokiFrame.Workbench.Avalonia.ViewModels.EventKit;

namespace YokiFrame.Workbench.Avalonia.ViewModels;

/// <summary>
/// 把 EventKit 强类型状态投影为稳定事件列表、运行关系和所选时间线。
/// </summary>
public sealed partial class EventKitPageViewModel : ViewModelBase
{
    private static readonly IReadOnlyList<string> sChannelOptions =
        new[] { "全部", "Enum", "Type", "String" };
    private readonly Dictionary<string, EventKitEventListItemViewModel> mItemsByIdentity =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, WorkbenchEventKitEvent> mRuntimeEventsByIdentity =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, WorkbenchEventKitCodeRelation> mCodeRelationsByIdentity =
        new(StringComparer.Ordinal);
    private readonly Func<WorkbenchEventKitCodeLocation, Task>? mOpenLocationAsync;
    private readonly HashSet<string> mRetainedIdentities = new(StringComparer.Ordinal);
    private readonly List<string> mRemovedIdentities = new();
    private readonly List<EventKitEventListItemViewModel> mDesiredEvents = new();
    private readonly HashSet<EventKitEventListItemViewModel> mDesiredEventSet = new();
    private readonly List<WorkbenchEventKitActivity> mDesiredActivities = new();
    private readonly HashSet<WorkbenchEventKitActivity> mDesiredActivitySet = new();
    private readonly Dictionary<long, WorkbenchEventKitActivity> mActivitiesBySequence = new();
    private IReadOnlyList<WorkbenchEventKitActivity> mAllActivities =
        Array.Empty<WorkbenchEventKitActivity>();
    private EventKitEventListItemViewModel? mSelectedEvent;
    private string mSearchText = string.Empty;
    private string mSelectedChannel = "全部";
    private string mEngineId = "未选择";
    private string mSessionId = string.Empty;
    private string mMode = string.Empty;
    private string mSource = "等待数据";
    private string mStaleReason = string.Empty;
    private long mGeneration;
    private long mVersion;
    private long mSequence;
    private int mTypeEventCount;
    private int mEnumEventCount;
    private int mStringEventCount;
    private int mTotalEventCount;
    private int mTotalHandlerCount;

    /// <summary>创建不具备静态扫描与源码定位边界的设计时页面。</summary>
    public EventKitPageViewModel()
        : this(null, null)
    {
    }

    /// <summary>创建带 Application 扫描与宿主源码定位边界的真实页面。</summary>
    internal EventKitPageViewModel(
        Func<bool, CancellationToken, Task<WorkbenchEventKitCodeScan>>? scanAsync,
        Func<WorkbenchEventKitCodeLocation, Task>? openLocationAsync)
    {
        mOpenLocationAsync = openLocationAsync;
        InitializeCodeScan(scanAsync);
    }

    /// <summary>获取筛选后的稳定事件列表。</summary>
    public ObservableCollection<EventKitEventListItemViewModel> Events { get; } = new();
    /// <summary>获取当前选择的有界时间线。</summary>
    public ObservableCollection<WorkbenchEventKitActivity> SelectedActivities { get; } = new();
    /// <summary>获取可选 Runtime 通道。</summary>
    public IReadOnlyList<string> ChannelOptions => sChannelOptions;

    /// <summary>获取或设置搜索文本。</summary>
    public string SearchText
    {
        get => mSearchText;
        set
        {
            if (SetProperty(ref mSearchText, value ?? string.Empty))
            {
                ReconcileVisibleEvents();
            }
        }
    }

    /// <summary>获取或设置通道筛选。</summary>
    public string SelectedChannel
    {
        get => mSelectedChannel;
        set
        {
            if (SetProperty(ref mSelectedChannel, value ?? "全部"))
            {
                ReconcileVisibleEvents();
            }
        }
    }

    /// <summary>获取或设置当前事件选择。</summary>
    public EventKitEventListItemViewModel? SelectedEvent
    {
        get => mSelectedEvent;
        set
        {
            if (SetProperty(ref mSelectedEvent, value))
            {
                ReconcileSelectedActivities();
                NotifySelectionProperties();
            }
        }
    }

    /// <summary>获取目标 engine。</summary>
    public string EngineId { get => mEngineId; private set => SetProperty(ref mEngineId, value); }
    /// <summary>获取宿主 session。</summary>
    public string SessionId { get => mSessionId; private set => SetProperty(ref mSessionId, value); }
    /// <summary>获取宿主 generation。</summary>
    public long Generation { get => mGeneration; private set => SetProperty(ref mGeneration, value); }
    /// <summary>获取宿主模式。</summary>
    public string Mode { get => mMode; private set => SetProperty(ref mMode, value); }
    /// <summary>获取当前数据源。</summary>
    public string Source { get => mSource; private set => SetSource(value); }
    /// <summary>获取回落或解析失败原因。</summary>
    public string StaleReason { get => mStaleReason; private set => SetProperty(ref mStaleReason, value); }
    /// <summary>获取 Runtime 状态版本。</summary>
    public long Version { get => mVersion; private set => SetProperty(ref mVersion, value); }
    /// <summary>获取 Runtime 活动 sequence。</summary>
    public long Sequence { get => mSequence; private set => SetProperty(ref mSequence, value); }
    /// <summary>获取 Type 事件数。</summary>
    public int TypeEventCount { get => mTypeEventCount; private set => SetProperty(ref mTypeEventCount, value); }
    /// <summary>获取 Enum 事件数。</summary>
    public int EnumEventCount { get => mEnumEventCount; private set => SetProperty(ref mEnumEventCount, value); }
    /// <summary>获取 String 事件数。</summary>
    public int StringEventCount { get => mStringEventCount; private set => SetProperty(ref mStringEventCount, value); }
    /// <summary>获取全部事件数。</summary>
    public int TotalEventCount { get => mTotalEventCount; private set => SetTotalEventCount(value); }
    /// <summary>获取全部监听器数。</summary>
    public int TotalHandlerCount { get => mTotalHandlerCount; private set => SetTotalHandlerCount(value); }

    /// <summary>获取标题栏数据通道文本。</summary>
    public string DataChannelText => string.Equals(Source, "telemetry", StringComparison.OrdinalIgnoreCase)
        ? "Shared Memory"
        : (string.Equals(Source, "snapshot", StringComparison.OrdinalIgnoreCase) ? "FileBridge" : Source);
    /// <summary>获取页面是否有任何事件。</summary>
    public bool HasEvents => mItemsByIdentity.Count > 0;
    /// <summary>获取页面是否显示空状态。</summary>
    public bool IsEmpty => !HasEvents;
    /// <summary>获取是否存在当前选择。</summary>
    public bool HasSelection => SelectedEvent != null;
    /// <summary>获取是否显示未选择状态。</summary>
    public bool IsSelectionEmpty => !HasSelection;
    /// <summary>获取筛选结果与总量。</summary>
    public string VisibleCountText => Events.Count + " / " + mItemsByIdentity.Count;
    /// <summary>获取事件总量统计文本。</summary>
    public string TotalEventCountText => mItemsByIdentity.Count.ToString();
    /// <summary>获取监听器总量统计文本。</summary>
    public string TotalHandlerCountText => TotalHandlerCount.ToString();
    /// <summary>获取近期活动数量。</summary>
    public string RecentActivityCountText => mAllActivities.Count.ToString();
    /// <summary>获取所选事件键。</summary>
    public string SelectedEventKey => SelectedEvent?.EventKeyDisplay ?? "未选择";
    /// <summary>获取所选事件是否属于 Type 通道。</summary>
    public bool SelectedIsType => SelectedEvent?.IsType == true;
    /// <summary>获取所选事件是否属于 Enum 通道。</summary>
    public bool SelectedIsEnum => SelectedEvent?.IsEnum == true;
    /// <summary>获取所选事件是否属于 String 通道。</summary>
    public bool SelectedIsString => SelectedEvent?.IsString == true;
    /// <summary>获取所选负载。</summary>
    public string SelectedPayloadText => SelectedEvent?.PayloadDisplay ?? "--";
    /// <summary>获取与中央事件节点一致的所选负载摘要。</summary>
    public string SelectedPayloadSummaryText => "参数: " + SelectedPayloadText;
    /// <summary>获取所选监听器数量。</summary>
    public string SelectedHandlerCountText => SelectedEvent?.HandlerCountText ?? "0 个监听器";
    /// <summary>获取所选发送活动数量。</summary>
    public string SelectedSendCountText => CountSelectedActivities("send") + " 次发送";
    /// <summary>获取所选时间线数量。</summary>
    public string SelectedActivityCountText => SelectedActivities.Count + " 条活动";
    /// <summary>获取无数据时的真实说明。</summary>
    public string EmptyDescription => string.IsNullOrWhiteSpace(StaleReason)
        ? "尚未扫描到 EventKit 调用，也没有观察到运行时活动。"
        : "EventKit 状态不可用：" + StaleReason;

    /// <summary>应用低频 dashboard 状态；同宿主的旧版本不会覆盖较新的 Telemetry。</summary>
    public void ApplyPeriodicState(WorkbenchEventKitState? state)
    {
        if (state == null)
        {
            ResetState();
            return;
        }

        if (IsOlderSameHostState(state))
        {
            return;
        }

        ApplyState(state);
    }

    /// <summary>应用 Shared Memory 新帧，并拒绝不匹配当前宿主身份的结果。</summary>
    internal bool TryApplyTelemetryState(WorkbenchEventKitState state)
    {
        if (state == null
            || (!string.IsNullOrEmpty(SessionId)
                && (!string.Equals(SessionId, state.SessionId, StringComparison.Ordinal)
                    || Generation != state.Generation)))
        {
            return false;
        }

        ApplyState(state);
        return true;
    }

    /// <summary>显示高频读取问题并保留最后一份可用状态。</summary>
    internal void ReportTelemetryIssue(string diagnostic)
    {
        if (!string.IsNullOrWhiteSpace(diagnostic))
        {
            StaleReason = diagnostic;
        }
    }

    /// <summary>提交一帧宿主身份、统计、稳定事件项和历史。</summary>
    private void ApplyState(WorkbenchEventKitState state)
    {
        string selectedIdentity = SelectedEvent?.Identity ?? string.Empty;
        ApplySource(state);
        ReconcileEventItems(state.Events);
        mAllActivities = state.RecentActivities;
        ReconcileVisibleEvents();
        RestoreSelection(selectedIdentity);
        NotifySummaryProperties();
    }

    /// <summary>更新宿主元数据和统计。</summary>
    private void ApplySource(WorkbenchEventKitState state)
    {
        EngineId = state.EngineId;
        SessionId = state.SessionId;
        Generation = state.Generation;
        Mode = state.Mode;
        Source = state.Source;
        StaleReason = state.StaleReason;
        Version = state.Version;
        Sequence = state.Sequence;
        TypeEventCount = state.TypeEventCount;
        EnumEventCount = state.EnumEventCount;
        StringEventCount = state.StringEventCount;
        TotalEventCount = state.TotalEventCount;
        TotalHandlerCount = state.TotalHandlerCount;
    }

    /// <summary>判断低频状态是否属于同宿主但版本落后。</summary>
    private bool IsOlderSameHostState(WorkbenchEventKitState state)
    {
        return Generation > 0L
            && Generation == state.Generation
            && string.Equals(SessionId, state.SessionId, StringComparison.Ordinal)
            && state.Version < Version;
    }

    /// <summary>清空所有页面状态并恢复等待文案。</summary>
    private void ResetState()
    {
        mRuntimeEventsByIdentity.Clear();
        ReconcileEventItems(Array.Empty<WorkbenchEventKitEvent>());
        SelectedActivities.Clear();
        mAllActivities = Array.Empty<WorkbenchEventKitActivity>();
        EngineId = "未选择";
        SessionId = string.Empty;
        Generation = 0L;
        Mode = string.Empty;
        Source = "等待数据";
        StaleReason = string.Empty;
        Version = 0L;
        Sequence = 0L;
        TypeEventCount = 0;
        EnumEventCount = 0;
        StringEventCount = 0;
        TotalEventCount = 0;
        TotalHandlerCount = 0;
        ReconcileVisibleEvents();
        RestoreSelection(SelectedEvent?.Identity ?? string.Empty);
        NotifySummaryProperties();
    }

    /// <summary>更新来源并通知标题栏通道派生属性。</summary>
    private void SetSource(string value)
    {
        if (SetProperty(ref mSource, value)) OnPropertyChanged(nameof(DataChannelText));
    }

    /// <summary>更新事件总数并通知展示文本。</summary>
    private void SetTotalEventCount(int value)
    {
        if (SetProperty(ref mTotalEventCount, value)) OnPropertyChanged(nameof(TotalEventCountText));
    }

    /// <summary>更新监听器总数并通知展示文本。</summary>
    private void SetTotalHandlerCount(int value)
    {
        if (SetProperty(ref mTotalHandlerCount, value)) OnPropertyChanged(nameof(TotalHandlerCountText));
    }

    /// <summary>通知事件总览派生属性。</summary>
    private void NotifySummaryProperties()
    {
        OnPropertyChanged(nameof(HasEvents));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(VisibleCountText));
        OnPropertyChanged(nameof(RecentActivityCountText));
        OnPropertyChanged(nameof(EmptyDescription));
    }

    /// <summary>通知当前选择派生属性。</summary>
    private void NotifySelectionProperties()
    {
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(IsSelectionEmpty));
        OnPropertyChanged(nameof(SelectedEventKey));
        OnPropertyChanged(nameof(SelectedIsType));
        OnPropertyChanged(nameof(SelectedIsEnum));
        OnPropertyChanged(nameof(SelectedIsString));
        OnPropertyChanged(nameof(SelectedPayloadText));
        OnPropertyChanged(nameof(SelectedPayloadSummaryText));
        OnPropertyChanged(nameof(SelectedHandlerCountText));
        OnPropertyChanged(nameof(SelectedSendCountText));
        OnPropertyChanged(nameof(SelectedActivityCountText));
    }

    /// <summary>执行不区分大小写的包含匹配。</summary>
    private static bool Contains(string value, string query)
    {
        return value.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>返回 Enum、Type、String 的稳定排序权重。</summary>
    private static int GetChannelRank(string channel)
    {
        if (channel == "Enum") return 0;
        if (channel == "Type") return 1;
        if (channel == "String") return 2;
        return 3;
    }
}
