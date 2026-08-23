using System.Collections.ObjectModel;
using YokiFrame.Tooling.Application.Models.ResKit;
using YokiFrame.Workbench.Avalonia.Services;
using YokiFrame.Workbench.Avalonia.ViewModels.ResKit;

namespace YokiFrame.Workbench.Avalonia.ViewModels;

/// <summary>承载 ResKit 资源列表、按需来源详情、卸载历史和显式诊断操作。</summary>
public sealed partial class ResKitPageViewModel : ViewModelBase, IDisposable
{
    private readonly Func<string, string, string, CancellationToken, Task<WorkbenchResKitResourceDetail>>? mGetDetailAsync;
    private readonly Func<string, bool, CancellationToken, Task<WorkbenchResKitState>>? mSetTrackingAsync;
    private readonly Func<string, CancellationToken, Task<WorkbenchResKitState>>? mClearHistoryAsync;
    private readonly Func<WorkbenchResKitLoadSource, Task>? mOpenLocationAsync;
    private readonly CancellationTokenSource mLifetimeCancellation = new();
    private string mEngineId = string.Empty;
    private string mSessionId = string.Empty;
    private long mGeneration;
    private long mVersion;
    private string mSource = GetString(CommonWaitingForKey, "等待数据");
    private string mStaleReason = string.Empty;
    private string mOperationStatusText = string.Empty;
    private int mHistoryTotal;
    private long mHistoryDroppedCount;
    private bool mTrackingEnabled;
    private bool mResourcesTruncated;
    private bool mHistoryTruncated;
    private string mLastBackgroundFailure = string.Empty;
    private long mSourceDetailVersion;
    private bool mHasRuntimeState;
    private bool mIsSourceLoading;

    /// <summary>创建可独立预览的只读 ResKit 页面。</summary>
    public ResKitPageViewModel() : this(null, null, null, null) { }

    /// <summary>创建带 Application 查询和显式诊断操作的 ResKit 页面。</summary>
    internal ResKitPageViewModel(
        Func<string, string, string, CancellationToken, Task<WorkbenchResKitResourceDetail>>? getDetailAsync,
        Func<string, bool, CancellationToken, Task<WorkbenchResKitState>>? setTrackingAsync,
        Func<string, CancellationToken, Task<WorkbenchResKitState>>? clearHistoryAsync,
        Func<WorkbenchResKitLoadSource, Task>? openLocationAsync)
    {
        mGetDetailAsync = getDetailAsync;
        mSetTrackingAsync = setTrackingAsync;
        mClearHistoryAsync = clearHistoryAsync;
        mOpenLocationAsync = openLocationAsync;
        // 订阅全局语言切换；对应解除订阅在 Dispose，由 WorkbenchWindow 关闭流程统一调用。
        WorkbenchI18nService.Instance.CultureChanged += OnCultureChanged;
        LoadSourcesCommand = new AsyncRelayCommand(LoadSourcesAsync, CanLoadSources);
        ToggleTrackingCommand = new AsyncRelayCommand(ToggleTrackingAsync, CanSetTracking);
        ClearHistoryCommand = new AsyncRelayCommand(ClearHistoryAsync, CanClearHistory);
    }

    /// <summary>获取筛选后的稳定资源行。</summary>
    public ObservableCollection<ResKitResourceListItemViewModel> Resources { get; } = new();
    /// <summary>获取最新优先的稳定卸载历史。</summary>
    public ObservableCollection<ResKitHistoryListItemViewModel> History { get; } = new();
    /// <summary>获取按需读取的独立 lease 来源。</summary>
    public ObservableCollection<ResKitLoadSourceItemViewModel> Sources { get; } = new();
    /// <summary>获取读取来源命令。</summary>
    public AsyncRelayCommand LoadSourcesCommand { get; }
    /// <summary>获取加载位置跟踪切换命令。</summary>
    public AsyncRelayCommand ToggleTrackingCommand { get; }
    /// <summary>获取清空卸载历史命令。</summary>
    public AsyncRelayCommand ClearHistoryCommand { get; }
    /// <summary>获取卸载历史总量。</summary>
    public int HistoryTotal { get => mHistoryTotal; private set => SetProperty(ref mHistoryTotal, value); }
    /// <summary>获取被固定环覆盖的历史数量。</summary>
    public long HistoryDroppedCount { get => mHistoryDroppedCount; private set => SetHistoryDroppedCount(value); }
    /// <summary>获取加载位置跟踪是否开启。</summary>
    public bool TrackingEnabled { get => mTrackingEnabled; private set => SetTrackingEnabled(value); }
    /// <summary>获取资源列表是否被裁剪。</summary>
    public bool ResourcesTruncated { get => mResourcesTruncated; private set => SetProperty(ref mResourcesTruncated, value); }
    /// <summary>获取历史列表是否被裁剪。</summary>
    public bool HistoryTruncated { get => mHistoryTruncated; private set => SetProperty(ref mHistoryTruncated, value); }
    /// <summary>获取当前数据来源。</summary>
    public string Source { get => mSource; private set => SetProperty(ref mSource, value); }
    /// <summary>获取数据读取诊断。</summary>
    public string StaleReason { get => mStaleReason; private set => SetProperty(ref mStaleReason, value); }
    /// <summary>获取最近操作结果。</summary>
    public string OperationStatusText { get => mOperationStatusText; private set => SetProperty(ref mOperationStatusText, value); }
    /// <summary>获取当前状态是否存在裁剪、回落或后台失败提示。</summary>
    public bool HasGlobalWarning => ResourcesTruncated || HistoryTruncated
        || !string.IsNullOrWhiteSpace(StaleReason) || !string.IsNullOrWhiteSpace(mLastBackgroundFailure);
    /// <summary>获取裁剪、回落或后台失败组合提示。</summary>
    public string GlobalWarningText => CreateGlobalWarningText();
    /// <summary>获取跟踪按钮文本。</summary>
    public string TrackingButtonText => TrackingEnabled
        ? GetString(DisableLocationKey, "关闭定位")
        : GetString(EnableLocationKey, "启用定位");
    /// <summary>获取筛选计数文本。</summary>
    public string VisibleCountText => Resources.Count + " / " + mResourceTotal;
    /// <summary>获取卸载历史计数文本。</summary>
    public string HistoryCountText => History.Count + " / " + HistoryTotal;
    /// <summary>获取是否显示历史覆盖提示。</summary>
    public bool HasDroppedHistory => HistoryDroppedCount > 0;
    /// <summary>获取历史覆盖提示。</summary>
    public string DroppedHistoryText => string.Format(
        GetString("String.ResKit.DroppedHistoryTemplate", "已有 {0} 条更早记录被覆盖"), HistoryDroppedCount);
    /// <summary>获取资源列表是否为空。</summary>
    public bool IsResourceEmpty => Resources.Count == 0;
    /// <summary>获取历史列表是否为空。</summary>
    public bool IsHistoryEmpty => History.Count == 0;
    /// <summary>获取来源列表是否为空。</summary>
    public bool IsSourceEmpty => Sources.Count == 0;
    /// <summary>获取页面是否仍在等待首个 ResKit 状态。</summary>
    public bool IsWaitingForData => !mHasRuntimeState;
    /// <summary>获取 Runtime 是否确实没有资源。</summary>
    public bool HasNoRuntimeResources => mHasRuntimeState && mAllResources.Count == 0;
    /// <summary>获取搜索是否过滤掉全部 Runtime 资源。</summary>
    public bool HasNoSearchResults => mHasRuntimeState && mAllResources.Count > 0 && Resources.Count == 0;
    /// <summary>获取搜索空状态说明。</summary>
    public string SearchEmptyText => string.IsNullOrWhiteSpace(SearchText)
        ? GetString("String.ResKit.SearchEmptyNoText", "当前筛选没有匹配资源")
        : string.Format(GetString("String.ResKit.SearchEmptyTemplate", "没有匹配“{0}”的资源"), SearchText);
    /// <summary>获取 Lease 来源是否正在按需读取。</summary>
    public bool IsSourceLoading
    {
        get => mIsSourceLoading;
        private set
        {
            if (!SetProperty(ref mIsSourceLoading, value)) return;
            OnPropertyChanged(nameof(ShowSourceEmpty));
        }
    }
    /// <summary>获取是否显示 Lease 来源空状态。</summary>
    public bool ShowSourceEmpty => IsSourceEmpty && !IsSourceLoading;
    /// <summary>获取 Lease 来源空状态说明。</summary>
    public string SourceEmptyText => mSourceDetailVersion == 0L
        ? GetString(SourceReadHintKey, "点击“读取来源”获取当前资源的 Lease 位置")
        : GetString(SourceNoneKey, "当前资源没有可显示的 Lease 来源");

    /// <summary>应用低频 dashboard 状态并拒绝同宿主旧版本。</summary>
    public void ApplyPeriodicState(WorkbenchResKitState? state)
    {
        if (state == null) { ResetRuntimeState(); return; }
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

    /// <summary>更新跟踪状态并通知按钮文本。</summary>
    private void SetTrackingEnabled(bool value)
    {
        if (SetProperty(ref mTrackingEnabled, value)) OnPropertyChanged(nameof(TrackingButtonText));
    }

    /// <summary>更新历史覆盖数量并通知提示属性。</summary>
    private void SetHistoryDroppedCount(long value)
    {
        if (!SetProperty(ref mHistoryDroppedCount, value)) return;
        OnPropertyChanged(nameof(HasDroppedHistory));
        OnPropertyChanged(nameof(DroppedHistoryText));
    }

    /// <summary>组合裁剪、回落和后台失败诊断。</summary>
    private string CreateGlobalWarningText()
    {
        List<string> messages = new();
        if (ResourcesTruncated) messages.Add(GetString(ResourcesTruncatedKey, "资源列表已裁剪"));
        if (HistoryTruncated) messages.Add(GetString(HistoryTruncatedKey, "卸载历史已裁剪"));
        if (!string.IsNullOrWhiteSpace(StaleReason)) messages.Add(StaleReason);
        if (!string.IsNullOrWhiteSpace(mLastBackgroundFailure))
        {
            messages.Add(string.Format(
                GetString(BackgroundFailedTemplateKey, "后台加载失败: {0}"), mLastBackgroundFailure));
        }

        return string.Join(" · ", messages);
    }

    /// <summary>按当前语言重新投影 ResKit 的动态展示文本；payload 数据与用户选择不变。</summary>
    private void OnCultureChanged()
    {
        // 未收到任何 Runtime 状态时，来源占位随语言重投影。
        if (!mHasRuntimeState)
        {
            Source = GetString(CommonWaitingForKey, "等待数据");
        }

        OnPropertyChanged(nameof(TrackingButtonText));
        OnPropertyChanged(nameof(DroppedHistoryText));
        OnPropertyChanged(nameof(SearchEmptyText));
        OnPropertyChanged(nameof(GlobalWarningText));
        OnPropertyChanged(nameof(SourceEmptyText));

        // 来源查询提示依赖选择与读取版本：未读取时直接重投影空闲或占位文案。
        if (mSourceDetailVersion == 0L)
        {
            ShowSelectedSourcePreview();
        }

        for (var index = 0; index < mAllResources.Count; index++)
        {
            mAllResources[index].RefreshLocalization();
        }

        foreach (ResKitLoadSourceItemViewModel source in Sources)
        {
            source.RefreshLocalization();
        }
    }

    /// <summary>判断状态是否属于当前宿主身份。</summary>
    private bool MatchesIdentity(WorkbenchResKitState state)
    {
        return string.Equals(mEngineId, state.EngineId, StringComparison.Ordinal)
            && string.Equals(mSessionId, state.SessionId, StringComparison.Ordinal)
            && mGeneration == state.Generation;
    }

    /// <summary>等待 Runtime 首帧数据时使用的通用占位文案资源 key。</summary>
    private const string CommonWaitingForKey = "String.Common.WaitingForData";

    /// <summary>关闭定位按钮文案资源 key。</summary>
    private const string DisableLocationKey = "String.ResKit.DisableLocation";

    /// <summary>启用定位按钮文案资源 key。</summary>
    private const string EnableLocationKey = "String.ResKit.EnableLocation";

    /// <summary>资源列表裁剪提示资源 key。</summary>
    private const string ResourcesTruncatedKey = "String.ResKit.ResourcesTruncated";

    /// <summary>卸载历史裁剪提示资源 key。</summary>
    private const string HistoryTruncatedKey = "String.ResKit.HistoryTruncated";

    /// <summary>后台加载失败模板资源 key。</summary>
    private const string BackgroundFailedTemplateKey = "String.ResKit.BackgroundFailedTemplate";

    /// <summary>来源读取入口提示资源 key。</summary>
    private const string SourceReadHintKey = "String.ResKit.SourceReadHint";

    /// <summary>无可见 lease 来源提示资源 key。</summary>
    private const string SourceNoneKey = "String.ResKit.SourceNone";

    /// <summary>从当前语言资源读取 ResKit 文案，保留测试与无资源环境的中文兜底。</summary>
    private static string GetString(string key, string fallback)
    {
        return WorkbenchI18nService.Instance.GetString(key, fallback);
    }
}
