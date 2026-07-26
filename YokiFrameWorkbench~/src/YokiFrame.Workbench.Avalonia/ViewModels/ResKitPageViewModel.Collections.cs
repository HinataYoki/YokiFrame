using System.Collections.ObjectModel;
using YokiFrame.Tooling.Application.Models.ResKit;
using YokiFrame.Workbench.Avalonia.ViewModels.ResKit;

namespace YokiFrame.Workbench.Avalonia.ViewModels;

/// <summary>承载 ResKit 稳定集合、搜索和选择协调。</summary>
public sealed partial class ResKitPageViewModel
{
    private readonly Dictionary<string, ResKitResourceListItemViewModel> mResourcesByIdentity = new(StringComparer.Ordinal);
    private readonly List<ResKitResourceListItemViewModel> mAllResources = new();
    private readonly List<ResKitResourceListItemViewModel> mDesiredResources = new();
    private readonly Dictionary<string, ResKitHistoryListItemViewModel> mHistoryByIdentity = new(StringComparer.Ordinal);
    private readonly List<ResKitHistoryListItemViewModel> mDesiredHistory = new();
    private readonly List<string> mStaleKeys = new();
    private string mSearchText = string.Empty;
    private ResKitResourceListItemViewModel? mSelectedResource;
    private int mResourceTotal;
    private string mSourceStatusText = "选择资源后可按需读取 lease 来源";

    /// <summary>获取或设置资源搜索文本。</summary>
    public string SearchText
    {
        get => mSearchText;
        set
        {
            if (!SetProperty(ref mSearchText, value ?? string.Empty)) return;
            OnPropertyChanged(nameof(SearchEmptyText));
            ReconcileVisibleResources();
        }
    }

    /// <summary>获取或设置当前资源选择。</summary>
    public ResKitResourceListItemViewModel? SelectedResource
    {
        get => mSelectedResource;
        set
        {
            if (!SetProperty(ref mSelectedResource, value)) return;
            ShowSelectedSourcePreview();
            NotifySelectionProperties();
            RaiseCommandStates();
        }
    }

    /// <summary>获取来源查询状态文本。</summary>
    public string SourceStatusText { get => mSourceStatusText; private set => SetProperty(ref mSourceStatusText, value); }
    /// <summary>获取是否存在当前选择。</summary>
    public bool HasSelection => SelectedResource != null;
    /// <summary>获取是否等待选择。</summary>
    public bool IsSelectionEmpty => !HasSelection;
    /// <summary>获取当前资源路径。</summary>
    public string SelectedPath => SelectedResource?.Path ?? "未选择资源";
    /// <summary>获取当前资源类型。</summary>
    public string SelectedTypeName => SelectedResource?.TypeName ?? "--";
    /// <summary>获取当前资源状态。</summary>
    public string SelectedState => SelectedResource?.State ?? "--";
    /// <summary>获取当前资源 lease 数。</summary>
    public int SelectedLeaseCount => SelectedResource?.LeaseCount ?? 0;
    /// <summary>获取当前资源 Provider。</summary>
    public string SelectedProviderName => SelectedResource?.ProviderName ?? "--";
    /// <summary>获取当前资源 Provider 代次。</summary>
    public long SelectedProviderGeneration => SelectedResource?.ProviderGeneration ?? 0L;
    /// <summary>获取当前已跟踪来源数量。</summary>
    public int SelectedTrackedSourceCount => SelectedResource?.TrackedSourceCount ?? 0;
    /// <summary>获取来源数量文本。</summary>
    public string SourceCountText => Sources.Count + " 条";

    /// <summary>协调完整资源集合并复用同身份行。</summary>
    private void ReconcileResources(WorkbenchResKitState state)
    {
        HashSet<string> retained = new(StringComparer.Ordinal);
        mAllResources.Clear();
        foreach (WorkbenchResKitResource resource in state.Resources)
        {
            retained.Add(resource.Identity);
            if (!mResourcesByIdentity.TryGetValue(resource.Identity, out ResKitResourceListItemViewModel? row))
            {
                row = new ResKitResourceListItemViewModel(resource);
                mResourcesByIdentity.Add(resource.Identity, row);
            }

            row.Update(resource);
            mAllResources.Add(row);
        }

        mStaleKeys.Clear();
        foreach (string identity in mResourcesByIdentity.Keys)
        {
            if (!retained.Contains(identity)) mStaleKeys.Add(identity);
        }
        foreach (string identity in mStaleKeys) mResourcesByIdentity.Remove(identity);

        ReconcileVisibleResources();
    }

    /// <summary>按搜索条件增量协调可见资源集合。</summary>
    private void ReconcileVisibleResources()
    {
        string selectedIdentity = SelectedResource?.Identity ?? string.Empty;
        mDesiredResources.Clear();
        foreach (ResKitResourceListItemViewModel row in mAllResources)
        {
            if (row.Matches(SearchText)) mDesiredResources.Add(row);
        }

        ReconcileCollection(Resources, mDesiredResources);
        SelectedResource = Resources.FirstOrDefault(item => item.Identity == selectedIdentity)
            ?? Resources.FirstOrDefault();
        OnPropertyChanged(nameof(VisibleCountText));
        OnPropertyChanged(nameof(IsResourceEmpty));
        NotifyResourceEmptyState();
    }

    /// <summary>协调最新优先历史集合并复用同身份行。</summary>
    private void ReconcileHistory(WorkbenchResKitState state)
    {
        HashSet<string> retained = new(StringComparer.Ordinal);
        mDesiredHistory.Clear();
        foreach (WorkbenchResKitUnloadRecord record in state.UnloadHistory)
        {
            retained.Add(record.Identity);
            if (!mHistoryByIdentity.TryGetValue(record.Identity, out ResKitHistoryListItemViewModel? row))
            {
                row = new ResKitHistoryListItemViewModel(record);
                mHistoryByIdentity.Add(record.Identity, row);
            }

            row.Update(record);
            mDesiredHistory.Add(row);
        }

        mStaleKeys.Clear();
        foreach (string identity in mHistoryByIdentity.Keys)
        {
            if (!retained.Contains(identity)) mStaleKeys.Add(identity);
        }
        foreach (string identity in mStaleKeys) mHistoryByIdentity.Remove(identity);

        ReconcileCollection(History, mDesiredHistory);
        OnPropertyChanged(nameof(HistoryCountText));
        OnPropertyChanged(nameof(IsHistoryEmpty));
    }

    /// <summary>使用 Move/Insert/Remove 更新集合，避免等价帧替换 ItemsSource。</summary>
    private static void ReconcileCollection<T>(ObservableCollection<T> target, IReadOnlyList<T> desired)
        where T : class
    {
        // O(n²) — acceptable for bounded list sizes; revisit if list sizes grow beyond ~100
        for (var index = 0; index < desired.Count; index++)
        {
            T item = desired[index];
            int current = target.IndexOf(item);
            if (current < 0) target.Insert(index, item);
            else if (current != index) target.Move(current, index);
        }

        while (target.Count > desired.Count) target.RemoveAt(target.Count - 1);
    }

    /// <summary>清空来源并更新查询提示。</summary>
    private void ClearSources(string statusText)
    {
        Sources.Clear();
        mSourceDetailVersion = 0L;
        SourceStatusText = statusText;
        NotifySourceProperties();
    }

    /// <summary>用周期 state 中的一条有界来源立即填充详情，完整列表仍由显式命令读取。</summary>
    private void ShowSelectedSourcePreview()
    {
        Sources.Clear();
        mSourceDetailVersion = 0L;
        ResKitResourceListItemViewModel? selected = SelectedResource;
        if (selected == null)
        {
            SourceStatusText = "选择资源后可读取 lease 来源";
            NotifySourceProperties();
            return;
        }

        foreach (WorkbenchResKitLoadSource source in selected.SourcePreview)
        {
            Sources.Add(new ResKitLoadSourceItemViewModel(source, mOpenLocationAsync));
        }

        SourceStatusText = CreateSourcePreviewStatus(selected);
        NotifySourceProperties();
    }

    /// <summary>根据预览数量和 Runtime 总量说明当前来源完整度。</summary>
    private string CreateSourcePreviewStatus(ResKitResourceListItemViewModel selected)
    {
        if (Sources.Count == 0)
        {
            return selected.TrackedSourceCount > 0
                ? "当前状态尚无来源预览，可点击读取完整来源"
                : "当前资源没有已跟踪的 lease 来源";
        }

        return selected.SourcesTruncated
            ? "v" + mVersion + " · 已预览 " + Sources.Count + " / " + selected.SourceTotal + " 条来源，点击读取完整来源"
            : "v" + mVersion + " · 已从实时状态读取 " + Sources.Count + " 条来源";
    }

    /// <summary>通知来源集合、空状态和计数绑定重新读取。</summary>
    private void NotifySourceProperties()
    {
        OnPropertyChanged(nameof(SourceCountText));
        OnPropertyChanged(nameof(IsSourceEmpty));
        OnPropertyChanged(nameof(ShowSourceEmpty));
        OnPropertyChanged(nameof(SourceEmptyText));
    }

    /// <summary>通知依赖当前选择的详情属性。</summary>
    private void NotifySelectionProperties()
    {
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(IsSelectionEmpty));
        OnPropertyChanged(nameof(SelectedPath));
        OnPropertyChanged(nameof(SelectedTypeName));
        OnPropertyChanged(nameof(SelectedState));
        OnPropertyChanged(nameof(SelectedLeaseCount));
        OnPropertyChanged(nameof(SelectedProviderName));
        OnPropertyChanged(nameof(SelectedProviderGeneration));
        OnPropertyChanged(nameof(SelectedTrackedSourceCount));
    }

    /// <summary>通知等待、Runtime 空集合和搜索无结果三种互斥状态。</summary>
    private void NotifyResourceEmptyState()
    {
        OnPropertyChanged(nameof(IsWaitingForData));
        OnPropertyChanged(nameof(HasNoRuntimeResources));
        OnPropertyChanged(nameof(HasNoSearchResults));
        OnPropertyChanged(nameof(SearchEmptyText));
    }
}
