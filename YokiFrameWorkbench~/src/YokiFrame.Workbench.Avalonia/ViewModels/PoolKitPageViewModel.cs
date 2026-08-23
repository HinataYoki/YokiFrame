using System.Collections.ObjectModel;
using YokiFrame.Tooling.Application.Models.PoolKit;
using YokiFrame.Workbench.Avalonia.Services;
using YokiFrame.Workbench.Avalonia.ViewModels.PoolKit;

namespace YokiFrame.Workbench.Avalonia.ViewModels;

/// <summary>承载 PoolKit 对象池列表、详情、事件流和显式诊断操作。</summary>
public sealed partial class PoolKitPageViewModel : ViewModelBase, IDisposable
{
    private readonly Func<string, bool, bool, bool, CancellationToken, Task<WorkbenchPoolKitState>>? mSetTrackingAsync;
    private readonly Func<string, CancellationToken, Task<WorkbenchPoolKitState>>? mCheckLeaksAsync;
    private readonly Func<string, CancellationToken, Task<WorkbenchPoolKitState>>? mClearHistoryAsync;
    private readonly Func<string, int, Task>? mOpenCodeLocationAsync;
    private readonly CancellationTokenSource mLifetimeCancellation = new();
    private string mEngineId = string.Empty;
    private string mSessionId = string.Empty;
    private long mGeneration;
    private long mVersion;
    private string mSource = GetString(CommonWaitingForKey, "等待数据");
    private string mStaleReason = string.Empty;
    private string mOperationStatusText = string.Empty;
    private bool mTrackingEnabled;
    private bool mStackTraceEnabled;
    private bool mEventHistoryEnabled;
    private int mPoolTotal;
    private int mEventTotal;
    private int mTotalActive;
    private int mLeakCount;
    private bool mPoolsTruncated;
    private bool mEventsTruncated;
    private bool mLeaksTruncated;

    /// <summary>创建可独立预览的只读 PoolKit 页面。</summary>
    public PoolKitPageViewModel() : this(null, null, null, null) { }

    /// <summary>创建带 Application 诊断操作的 PoolKit 页面。</summary>
    internal PoolKitPageViewModel(
        Func<string, bool, bool, bool, CancellationToken, Task<WorkbenchPoolKitState>>? setTrackingAsync,
        Func<string, CancellationToken, Task<WorkbenchPoolKitState>>? checkLeaksAsync,
        Func<string, CancellationToken, Task<WorkbenchPoolKitState>>? clearHistoryAsync,
        Func<string, int, Task>? openCodeLocationAsync)
    {
        mSetTrackingAsync = setTrackingAsync;
        mCheckLeaksAsync = checkLeaksAsync;
        mClearHistoryAsync = clearHistoryAsync;
        mOpenCodeLocationAsync = openCodeLocationAsync;
        // 订阅全局语言切换；对应解除订阅在 Dispose，由 WorkbenchWindow 关闭流程统一调用。
        WorkbenchI18nService.Instance.CultureChanged += OnCultureChanged;
        ToggleTrackingCommand = new AsyncRelayCommand(ToggleTrackingAsync, CanSetTracking);
        ToggleLocationCommand = new AsyncRelayCommand(ToggleLocationAsync, CanSetTracking);
        CheckLeaksCommand = new AsyncRelayCommand(CheckLeaksAsync, CanCheckLeaks);
        ClearHistoryCommand = new AsyncRelayCommand(ClearHistoryAsync, CanClearHistory);
    }

    /// <summary>获取筛选后的稳定对象池行。</summary>
    public ObservableCollection<PoolKitPoolListItemViewModel> Pools { get; } = new();
    /// <summary>获取所选对象池的最新事件流。</summary>
    public ObservableCollection<PoolKitEventListItemViewModel> Events { get; } = new();
    /// <summary>获取跟踪切换命令。</summary>
    public AsyncRelayCommand ToggleTrackingCommand { get; }
    /// <summary>获取堆栈定位切换命令。</summary>
    public AsyncRelayCommand ToggleLocationCommand { get; }
    /// <summary>获取疑似泄漏检查命令。</summary>
    public AsyncRelayCommand CheckLeaksCommand { get; }
    /// <summary>获取清空事件历史命令。</summary>
    public AsyncRelayCommand ClearHistoryCommand { get; }
    /// <summary>获取当前对象池总量。</summary>
    public int PoolTotal { get => mPoolTotal; private set => SetProperty(ref mPoolTotal, value); }
    /// <summary>获取当前事件历史总量。</summary>
    public int EventTotal { get => mEventTotal; private set => SetProperty(ref mEventTotal, value); }
    /// <summary>获取当前借出对象总量。</summary>
    public int TotalActive { get => mTotalActive; private set => SetProperty(ref mTotalActive, value); }
    /// <summary>获取疑似未归还对象池数量。</summary>
    public int LeakCount { get => mLeakCount; private set => SetProperty(ref mLeakCount, value); }
    /// <summary>获取诊断跟踪是否开启。</summary>
    public bool TrackingEnabled { get => mTrackingEnabled; private set => SetTrackingEnabled(value); }
    /// <summary>获取堆栈定位是否开启。</summary>
    public bool StackTraceEnabled { get => mStackTraceEnabled; private set => SetStackTraceEnabled(value); }
    /// <summary>获取事件历史是否开启。</summary>
    public bool EventHistoryEnabled
    {
        get => mEventHistoryEnabled;
        private set
        {
            if (SetProperty(ref mEventHistoryEnabled, value)) OnPropertyChanged(nameof(EventHistoryStatusText));
        }
    }
    /// <summary>获取对象池列表是否被裁剪。</summary>
    public bool PoolsTruncated { get => mPoolsTruncated; private set => SetProperty(ref mPoolsTruncated, value); }
    /// <summary>获取事件列表是否被裁剪。</summary>
    public bool EventsTruncated { get => mEventsTruncated; private set => SetProperty(ref mEventsTruncated, value); }
    /// <summary>获取疑似未归还候选池明细是否被裁剪。</summary>
    public bool LeaksTruncated { get => mLeaksTruncated; private set => SetProperty(ref mLeaksTruncated, value); }
    /// <summary>获取当前数据来源。</summary>
    public string Source { get => mSource; private set => SetProperty(ref mSource, value); }
    /// <summary>获取数据读取诊断。</summary>
    public string StaleReason { get => mStaleReason; private set => SetProperty(ref mStaleReason, value); }
    /// <summary>获取页面是否仍在等待 Runtime 提供首帧对象池数据。</summary>
    public bool IsWaitingForData => string.IsNullOrWhiteSpace(mEngineId);
    /// <summary>获取最近操作结果。</summary>
    public string OperationStatusText { get => mOperationStatusText; private set => SetProperty(ref mOperationStatusText, value); }
    /// <summary>获取跟踪按钮文本。</summary>
    public string TrackingButtonText => TrackingEnabled
        ? GetString("String.PoolKit.StopTracking", "停止跟踪")
        : GetString("String.PoolKit.EnableTracking", "启用跟踪");
    /// <summary>获取定位按钮文本。</summary>
    public string LocationButtonText => StackTraceEnabled
        ? GetString("String.PoolKit.DisableLocation", "关闭定位")
        : GetString("String.PoolKit.EnableLocation", "启用定位");
    /// <summary>获取对象跟踪开关的紧凑状态文本。</summary>
    public string TrackingStatusText => TrackingEnabled
        ? GetString("String.PoolKit.TrackingOn", "跟踪 开")
        : GetString("String.PoolKit.TrackingOff", "跟踪 关");
    /// <summary>获取堆栈定位开关的紧凑状态文本。</summary>
    public string LocationStatusText => StackTraceEnabled
        ? GetString("String.PoolKit.LocationOn", "定位 开")
        : GetString("String.PoolKit.LocationOff", "定位 关");
    /// <summary>获取事件历史开关的紧凑状态文本。</summary>
    public string EventHistoryStatusText => EventHistoryEnabled
        ? GetString("String.PoolKit.EventHistoryOn", "事件 开")
        : GetString("String.PoolKit.EventHistoryOff", "事件 关");
    /// <summary>获取泄漏告警文本。</summary>
    public string LeakWarningText => string.Format(
            GetString(
                "String.PoolKit.LeakWarningTemplate",
                "本次检查发现 {0} 个仍有借出对象的候选池；这不是内存泄漏定论。"),
            LeakCount)
        + (LeaksTruncated ? GetString("String.PoolKit.LeakTruncatedSuffix", " 候选明细已裁剪。") : string.Empty);
    /// <summary>获取是否显示泄漏告警。</summary>
    public bool HasLeakWarning => LeakCount > 0;
    /// <summary>获取列表是否为空。</summary>
    public bool IsEmpty => Pools.Count == 0;
    /// <summary>获取筛选计数文本。</summary>
    public string VisibleCountText => Pools.Count + " / " + PoolTotal;

    /// <summary>应用低频 dashboard 状态并拒绝同宿主旧版本。</summary>
    public void ApplyPeriodicState(WorkbenchPoolKitState? state)
    {
        if (state == null)
        {
            ResetRuntimeState();
            return;
        }

        if (MatchesIdentity(state) && state.Version < mVersion)
        {
            StaleReason = state.StaleReason;
            return;
        }

        ApplyState(state);
    }

    /// <summary>取消页面仍在执行的诊断操作并解除语言事件订阅。</summary>
    public void Dispose()
    {
        WorkbenchI18nService.Instance.CultureChanged -= OnCultureChanged;
        mLifetimeCancellation.Cancel();
        mLifetimeCancellation.Dispose();
    }

    /// <summary>按当前语言重新投影 PoolKit 的动态展示文本与既有列表行。</summary>
    private void OnCultureChanged()
    {
        if (IsWaitingForData)
        {
            Source = GetString(CommonWaitingForKey, "等待数据");
        }

        OnPropertyChanged(nameof(TrackingButtonText));
        OnPropertyChanged(nameof(LocationButtonText));
        OnPropertyChanged(nameof(TrackingStatusText));
        OnPropertyChanged(nameof(LocationStatusText));
        OnPropertyChanged(nameof(EventHistoryStatusText));
        OnPropertyChanged(nameof(LeakWarningText));
        OnPropertyChanged(nameof(SearchEmptyText));
        OnPropertyChanged(nameof(SelectedName));
        OnPropertyChanged(nameof(SelectedEventCountText));
        for (var index = 0; index < mAllPools.Count; index++)
        {
            mAllPools[index].RefreshRecentEventText(FindRecentEventText(mAllPools[index].Pool, mAllEvents));
        }

        foreach (PoolKitEventListItemViewModel row in Events)
        {
            row.RefreshLocalization();
        }
    }

    /// <summary>更新跟踪状态并通知按钮文本。</summary>
    private void SetTrackingEnabled(bool value)
    {
        if (!SetProperty(ref mTrackingEnabled, value)) return;
        OnPropertyChanged(nameof(TrackingButtonText));
        OnPropertyChanged(nameof(TrackingStatusText));
    }

    /// <summary>更新定位状态并通知按钮文本。</summary>
    private void SetStackTraceEnabled(bool value)
    {
        if (!SetProperty(ref mStackTraceEnabled, value)) return;
        OnPropertyChanged(nameof(LocationButtonText));
        OnPropertyChanged(nameof(LocationStatusText));
    }

    /// <summary>判断状态是否属于当前宿主身份。</summary>
    private bool MatchesIdentity(WorkbenchPoolKitState state)
    {
        return string.Equals(mEngineId, state.EngineId, StringComparison.Ordinal)
            && string.Equals(mSessionId, state.SessionId, StringComparison.Ordinal)
            && mGeneration == state.Generation;
    }

    /// <summary>等待 Runtime 首帧数据时使用的通用占位文案资源 key。</summary>
    private const string CommonWaitingForKey = "String.Common.WaitingForData";

    /// <summary>从当前语言资源读取 PoolKit 文案，保留测试与无资源环境的中文兜底。</summary>
    private static string GetString(string key, string fallback)
    {
        return WorkbenchI18nService.Instance.GetString(key, fallback);
    }
}
