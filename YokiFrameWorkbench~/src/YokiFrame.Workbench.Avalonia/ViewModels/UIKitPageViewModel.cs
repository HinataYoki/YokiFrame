using System.Collections.ObjectModel;
using YokiFrame.Tooling.Application.Models.UIKit;
using YokiFrame.Tooling.Application.Services.UIKit;

namespace YokiFrame.Workbench.Avalonia.ViewModels;

/// <summary>承载 Unity UIKit Runtime 面板、栈、缓存和模态只读诊断页面。</summary>
public sealed partial class UIKitPageViewModel : ViewModelBase
{
    private readonly Func<string, Task>? mCopyTextAsync;
    private IReadOnlyList<WorkbenchUIKitPanel> mAllPanels = Array.Empty<WorkbenchUIKitPanel>();
    private IReadOnlyList<WorkbenchUIKitStack> mAllStacks = Array.Empty<WorkbenchUIKitStack>();
    private WorkbenchUIKitState? mState;
    private WorkbenchUIKitPanel? mSelectedPanel;
    private WorkbenchUIKitStack? mSelectedStack;
    private int mSelectedCollectionIndex;
    private string mSearchText = string.Empty;
    private string mSource = "等待数据";
    private string mUpdatedAtText = "--";
    private string mStaleReason = string.Empty;
    private string mCopyStatusText = string.Empty;

    /// <summary>创建不依赖系统剪贴板的 UIKit 页面状态。</summary>
    public UIKitPageViewModel() : this(null, null, null) { }

    /// <summary>创建带可选系统剪贴板回调的 UIKit 页面状态。</summary>
    /// <param name="copyTextAsync">平台剪贴板写入回调；为空时复制命令禁用。</param>
    public UIKitPageViewModel(Func<string, Task>? copyTextAsync) : this(copyTextAsync, null, null) { }

    /// <summary>创建同时具备 Runtime 诊断和强类型 Unity Editor Tools 的页面状态。</summary>
    /// <param name="copyTextAsync">平台剪贴板写入回调。</param>
    /// <param name="editorActionAsync">Application 强类型 UIKit Editor action 用例。</param>
    internal UIKitPageViewModel(
        Func<string, Task>? copyTextAsync,
        Func<WorkbenchUIKitEditorAction, WorkbenchUIKitPanelGenerationRequest?, CancellationToken, Task<WorkbenchUIKitEditorResult>>? editorActionAsync)
        : this(copyTextAsync, editorActionAsync, null)
    {
    }

    /// <summary>创建同时具备 Runtime 诊断和 Editor Tools 的页面状态。</summary>
    /// <param name="copyTextAsync">平台剪贴板写入回调。</param>
    /// <param name="editorActionAsync">Application 强类型 UIKit Editor action 用例。</param>
    /// <param name="editorSettingsService">Unity UIKit Editor Tools 项目设置服务。</param>
    internal UIKitPageViewModel(
        Func<string, Task>? copyTextAsync,
        Func<WorkbenchUIKitEditorAction, WorkbenchUIKitPanelGenerationRequest?, CancellationToken, Task<WorkbenchUIKitEditorResult>>? editorActionAsync,
        UIKitEditorSettingsService? editorSettingsService = null)
    {
        mCopyTextAsync = copyTextAsync;
        mEditorActionAsync = editorActionAsync;
        mEditorSettingsService = editorSettingsService;
        CopySnapshotCommand = new AsyncRelayCommand(CopySnapshotAsync, CanCopySnapshot);
        CopySelectedCommand = new AsyncRelayCommand(CopySelectedAsync, CanCopySelected);
        InitializeEditorCommands();
    }

    /// <summary>获取当前筛选后的面板列表。</summary>
    public ObservableCollection<WorkbenchUIKitPanel> Panels { get; } = new();

    /// <summary>获取当前筛选后的命名栈列表。</summary>
    public ObservableCollection<WorkbenchUIKitStack> Stacks { get; } = new();

    /// <summary>获取或设置 Panels/Stacks 集合选项卡索引。</summary>
    public int SelectedCollectionIndex
    {
        get => mSelectedCollectionIndex;
        set
        {
            int normalized = value == 1 ? 1 : 0;
            if (!SetProperty(ref mSelectedCollectionIndex, normalized)) return;
            EnsureCurrentSelection();
            NotifyCollectionPresentationChanged();
        }
    }

    /// <summary>获取或设置面板搜索文本。</summary>
    public string SearchText
    {
        get => mSearchText;
        set
        {
            if (!SetProperty(ref mSearchText, value ?? string.Empty)) return;
            RebuildFilteredCollections();
        }
    }

    /// <summary>获取或设置当前选中面板。</summary>
    public WorkbenchUIKitPanel? SelectedPanel
    {
        get => mSelectedPanel;
        set
        {
            if (!SetProperty(ref mSelectedPanel, value)) return;
            NotifySelectionChanged();
        }
    }

    /// <summary>获取或设置当前选中命名栈。</summary>
    public WorkbenchUIKitStack? SelectedStack
    {
        get => mSelectedStack;
        set
        {
            if (!SetProperty(ref mSelectedStack, value)) return;
            NotifySelectionChanged();
        }
    }

    /// <summary>获取当前数据源名称。</summary>
    public string Source { get => mSource; private set => SetProperty(ref mSource, value); }

    /// <summary>获取当前状态的本地更新时间。</summary>
    public string UpdatedAtText { get => mUpdatedAtText; private set => SetProperty(ref mUpdatedAtText, value); }

    /// <summary>获取回落、过期或解析失败原因。</summary>
    public string StaleReason { get => mStaleReason; private set => SetProperty(ref mStaleReason, value); }

    /// <summary>获取最近一次复制操作状态。</summary>
    public string CopyStatusText { get => mCopyStatusText; private set => SetProperty(ref mCopyStatusText, value); }

    /// <summary>获取当前是否已读取 UIKit 强类型状态。</summary>
    public bool HasState => mState != null;
    /// <summary>获取当前 UIKit Root 是否存在。</summary>
    public bool RootExists => mState?.Root.Exists == true;
    /// <summary>获取是否已读取状态但 UIKit Root 不存在。</summary>
    public bool RootMissing => HasState && !RootExists;
    /// <summary>获取 Root 状态文本。</summary>
    public string RootStatusText => RootExists ? "根节点在线" : HasState ? "未发现根节点" : "根节点状态未知";
    /// <summary>获取面板总量。</summary>
    public int PanelCount => mState?.Stats.PanelCount ?? 0;
    /// <summary>获取命名栈总量。</summary>
    public int StackCount => mState?.Stats.StackCount ?? 0;
    /// <summary>获取全部栈成员数量。</summary>
    public int StackMembershipCount => mState?.Stats.StackMembershipCount ?? 0;
    /// <summary>获取命名栈数量与栈成员数量的紧凑摘要。</summary>
    public string StackCoverageText => StackCount + " / " + StackMembershipCount;
    /// <summary>获取 Preloaded 面板数量。</summary>
    public int PreloadedCount => mState?.Stats.States.Preloaded ?? 0;
    /// <summary>获取 Opening 面板数量。</summary>
    public int OpeningCount => mState?.Stats.States.Opening ?? 0;
    /// <summary>获取 Open 面板数量。</summary>
    public int OpenCount => mState?.Stats.States.Open ?? 0;
    /// <summary>获取 Hiding 面板数量。</summary>
    public int HidingCount => mState?.Stats.States.Hiding ?? 0;
    /// <summary>获取 Hidden 面板数量。</summary>
    public int HiddenCount => mState?.Stats.States.Hidden ?? 0;
    /// <summary>获取 Closing 面板数量。</summary>
    public int ClosingCount => mState?.Stats.States.Closing ?? 0;
    /// <summary>获取 Cached 面板数量。</summary>
    public int CachedCount => mState?.Stats.States.Cached ?? 0;
    /// <summary>获取 Closed 面板数量。</summary>
    public int ClosedCount => mState?.Stats.States.Closed ?? 0;
    /// <summary>获取 Reusable 缓存容量。</summary>
    public int CacheCapacity => mState?.Cache.Capacity ?? 0;
    /// <summary>获取 Transient 面板数量。</summary>
    public int TransientCount => mState?.Cache.Transient ?? 0;
    /// <summary>获取 Reusable 面板数量。</summary>
    public int ReusableCount => mState?.Cache.Reusable ?? 0;
    /// <summary>获取已缓存 Reusable 面板数量。</summary>
    public int ReusableCachedCount => mState?.Cache.ReusableCached ?? 0;
    /// <summary>获取 Persistent 面板数量。</summary>
    public int PersistentCount => mState?.Cache.Persistent ?? 0;
    /// <summary>获取当前模态面板数量。</summary>
    public int ModalPanelCount => mState?.Modal.PanelCount ?? 0;
    /// <summary>获取 Modal blocker 是否处于活动状态。</summary>
    public bool ModalBlockerActive => mState?.Modal.BlockerActive == true;
    /// <summary>获取 Modal blocker 状态文本。</summary>
    public string ModalStatusText => ModalBlockerActive ? "遮罩已启用" : "无遮罩";
    /// <summary>获取面板集合是否被裁剪。</summary>
    public bool PanelsTruncated => mState?.PanelsTruncated == true;
    /// <summary>获取命名栈集合是否被裁剪。</summary>
    public bool StacksTruncated => mState?.StacksTruncated == true;
    /// <summary>获取当前选项卡集合是否被裁剪。</summary>
    public bool CurrentCollectionTruncated => IsPanelsView ? PanelsTruncated : StacksTruncated;
    /// <summary>获取当前集合覆盖率。</summary>
    public string CoverageText => IsPanelsView
        ? CreateCoverageText(mState?.PanelReturned ?? 0, mState?.PanelTotal ?? 0)
        : CreateCoverageText(mState?.StackReturned ?? 0, mState?.StackTotal ?? 0);
    /// <summary>获取当前是否展示 Panels 集合。</summary>
    public bool IsPanelsView => SelectedCollectionIndex == 0;
    /// <summary>获取当前是否展示 Stacks 集合。</summary>
    public bool IsStacksView => !IsPanelsView;
    /// <summary>获取当前选项卡是否有选中项。</summary>
    public bool HasSelection => IsPanelsView ? SelectedPanel != null : SelectedStack != null;
    /// <summary>获取是否显示面板详情。</summary>
    public bool ShowPanelDetails => IsPanelsView && SelectedPanel != null;
    /// <summary>获取是否显示命名栈详情。</summary>
    public bool ShowStackDetails => IsStacksView && SelectedStack != null;
    /// <summary>获取当前选项卡是否没有选中项。</summary>
    public bool ShowNoSelection => !IsEmpty && !HasSelection;
    /// <summary>获取当前筛选结果是否为空。</summary>
    public bool IsEmpty => IsPanelsView ? Panels.Count == 0 : Stacks.Count == 0;
    /// <summary>获取是否存在 stale 或解析错误。</summary>
    public bool HasStaleReason => !string.IsNullOrWhiteSpace(StaleReason);
    /// <summary>获取是否存在可显示的复制状态。</summary>
    public bool HasCopyStatus => !string.IsNullOrWhiteSpace(CopyStatusText);
    /// <summary>获取当前集合空状态标题。</summary>
    public string EmptyTitleText => CreateEmptyTitle();
    /// <summary>获取当前集合空状态说明。</summary>
    public string EmptyHintText => CreateEmptyHint();
    /// <summary>获取复制完整快照的命令。</summary>
    public AsyncRelayCommand CopySnapshotCommand { get; }
    /// <summary>获取复制当前选中项的命令。</summary>
    public AsyncRelayCommand CopySelectedCommand { get; }

    /// <summary>应用 Dashboard 周期状态，并按强类型字段更新页面。</summary>
    /// <param name="state">本轮 UIKit 强类型状态；为空时清空页面。</param>
    public void ApplyPeriodicState(WorkbenchUIKitState? state)
    {
        string selectedPanelKey = CreatePanelKey(SelectedPanel);
        string selectedStackKey = SelectedStack?.Name ?? string.Empty;
        mState = state;
        if (state == null)
        {
            ResetState();
            return;
        }

        Source = string.IsNullOrWhiteSpace(state.Source) ? "snapshot" : state.Source;
        UpdatedAtText = state.UpdatedAtUtc == DateTimeOffset.MinValue
            ? "--"
            : state.UpdatedAtUtc.ToLocalTime().ToString("HH:mm:ss");
        StaleReason = state.StaleReason;
        mAllPanels = WorkbenchUIKitPresentation.OrderPanels(state.Panels);
        mAllStacks = WorkbenchUIKitPresentation.OrderStacks(state.Stacks);
        RebuildFilteredCollections(selectedPanelKey, selectedStackKey);
        NotifySummaryChanged();
    }

    /// <summary>按当前搜索文本重建两个可见集合并保持用户选择。</summary>
    private void RebuildFilteredCollections()
    {
        RebuildFilteredCollections(CreatePanelKey(SelectedPanel), SelectedStack?.Name ?? string.Empty);
    }

    /// <summary>按指定选择键重建两个集合，避免刷新后详情焦点跳动。</summary>
    private void RebuildFilteredCollections(string selectedPanelKey, string selectedStackKey)
    {
        WorkbenchUIKitPanel[] panels = mAllPanels.Where(MatchesPanelSearch).ToArray();
        WorkbenchUIKitStack[] stacks = mAllStacks.Where(MatchesStackSearch).ToArray();
        ReplaceCollection(Panels, panels);
        ReplaceCollection(Stacks, stacks);
        SelectedPanel = panels.FirstOrDefault(item => CreatePanelKey(item) == selectedPanelKey)
            ?? panels.FirstOrDefault();
        SelectedStack = stacks.FirstOrDefault(item => item.Name == selectedStackKey)
            ?? stacks.FirstOrDefault();
        NotifyCollectionPresentationChanged();
    }

    /// <summary>判断面板是否匹配当前搜索文本。</summary>
    private bool MatchesPanelSearch(WorkbenchUIKitPanel panel)
    {
        if (string.IsNullOrWhiteSpace(SearchText)) return true;
        return ContainsSearch(panel.Name)
            || ContainsSearch(panel.Type)
            || ContainsSearch(panel.State)
            || ContainsSearch(panel.Level)
            || ContainsSearch(panel.StackName);
    }

    /// <summary>判断命名栈是否匹配当前搜索文本。</summary>
    private bool MatchesStackSearch(WorkbenchUIKitStack stack)
    {
        if (string.IsNullOrWhiteSpace(SearchText)) return true;
        return ContainsSearch(stack.Name)
            || ContainsSearch(stack.TopPanelName)
            || ContainsSearch(stack.TopPanelType);
    }

    /// <summary>按 ordinal ignore-case 语义匹配一段可空文本。</summary>
    private bool ContainsSearch(string? value)
    {
        return !string.IsNullOrEmpty(value)
            && value.Contains(SearchText.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>确保当前选项卡至少选择第一个可见条目。</summary>
    private void EnsureCurrentSelection()
    {
        if (IsPanelsView && SelectedPanel == null) SelectedPanel = Panels.FirstOrDefault();
        if (IsStacksView && SelectedStack == null) SelectedStack = Stacks.FirstOrDefault();
    }

    /// <summary>复制完整强类型 UIKit 诊断文本，不回传任何 Runtime 命令。</summary>
    private async Task CopySnapshotAsync()
    {
        if (mCopyTextAsync == null || mState == null) return;
        try
        {
            await mCopyTextAsync(WorkbenchUIKitPresentation.CreateSnapshotText(mState));
            CopyStatusText = "已复制 UIKit 诊断";
        }
        catch (Exception exception)
        {
            CopyStatusText = "复制失败: " + exception.Message;
        }
        OnPropertyChanged(nameof(HasCopyStatus));
    }

    /// <summary>复制当前选中 Panel 或 Stack 的只读字段。</summary>
    private async Task CopySelectedAsync()
    {
        if (mCopyTextAsync == null || !HasSelection) return;
        string text = IsPanelsView
            ? WorkbenchUIKitPresentation.CreatePanelText(SelectedPanel!)
            : WorkbenchUIKitPresentation.CreateStackText(SelectedStack!);
        try
        {
            await mCopyTextAsync(text);
            CopyStatusText = "已复制当前项";
        }
        catch (Exception exception)
        {
            CopyStatusText = "复制失败: " + exception.Message;
        }
        OnPropertyChanged(nameof(HasCopyStatus));
    }

    /// <summary>判断完整快照复制命令是否可用。</summary>
    private bool CanCopySnapshot() => mCopyTextAsync != null && mState != null;

    /// <summary>判断选中项复制命令是否可用。</summary>
    private bool CanCopySelected() => mCopyTextAsync != null && HasSelection;

    /// <summary>构造当前集合覆盖率文本。</summary>
    private static string CreateCoverageText(int returned, int total)
    {
        return WorkbenchUIKitPresentation.CreateCoverageText(returned, total);
    }

    /// <summary>构造面板选择稳定键。</summary>
    private static string CreatePanelKey(WorkbenchUIKitPanel? panel)
    {
        return WorkbenchUIKitPresentation.CreatePanelKey(panel);
    }

    private static void ReplaceCollection<T>(ObservableCollection<T> target, IReadOnlyList<T> source)
    {
        target.Clear();
        for (int index = 0; index < source.Count; index++) target.Add(source[index]);
    }

    /// <summary>构造当前集合空状态标题。</summary>
    private string CreateEmptyTitle()
    {
        if (!HasState) return "等待 UIKit 运行时数据";
        if (!RootExists) return "当前未发现 UIKit Root";
        if (!string.IsNullOrWhiteSpace(SearchText)) return "没有匹配项";
        return IsPanelsView ? "当前没有已加载面板" : "当前没有命名栈";
    }

    /// <summary>构造当前集合空状态说明。</summary>
    private string CreateEmptyHint()
    {
        if (!HasState) return "等待 Unity Editor 发布 telemetry 或 snapshot";
        if (!RootExists) return "运行时尚未创建或注册 UIKit 根节点";
        if (!string.IsNullOrWhiteSpace(SearchText)) return "调整搜索条件查看其它运行时条目";
        return IsPanelsView ? "面板加载后会出现在这里" : "命名栈创建后会出现在这里";
    }

    /// <summary>清空已离线或未选中宿主的 UIKit 页面状态。</summary>
    private void ResetState()
    {
        mAllPanels = Array.Empty<WorkbenchUIKitPanel>();
        mAllStacks = Array.Empty<WorkbenchUIKitStack>();
        Panels.Clear();
        Stacks.Clear();
        SelectedPanel = null;
        SelectedStack = null;
        Source = "等待数据";
        UpdatedAtText = "--";
        StaleReason = string.Empty;
        CopyStatusText = string.Empty;
        NotifySummaryChanged();
        NotifyCollectionPresentationChanged();
    }

    /// <summary>通知全部指标和状态条重新读取当前强类型状态。</summary>
    private void NotifySummaryChanged()
    {
        OnPropertyChanged(nameof(HasState));
        OnPropertyChanged(nameof(RootExists));
        OnPropertyChanged(nameof(RootMissing));
        OnPropertyChanged(nameof(RootStatusText));
        OnPropertyChanged(nameof(PanelCount));
        OnPropertyChanged(nameof(StackCount));
        OnPropertyChanged(nameof(StackMembershipCount));
        OnPropertyChanged(nameof(StackCoverageText));
        OnPropertyChanged(nameof(PreloadedCount));
        OnPropertyChanged(nameof(OpeningCount));
        OnPropertyChanged(nameof(OpenCount));
        OnPropertyChanged(nameof(HidingCount));
        OnPropertyChanged(nameof(HiddenCount));
        OnPropertyChanged(nameof(ClosingCount));
        OnPropertyChanged(nameof(CachedCount));
        OnPropertyChanged(nameof(ClosedCount));
        OnPropertyChanged(nameof(CacheCapacity));
        OnPropertyChanged(nameof(TransientCount));
        OnPropertyChanged(nameof(ReusableCount));
        OnPropertyChanged(nameof(ReusableCachedCount));
        OnPropertyChanged(nameof(PersistentCount));
        OnPropertyChanged(nameof(ModalPanelCount));
        OnPropertyChanged(nameof(ModalBlockerActive));
        OnPropertyChanged(nameof(ModalStatusText));
        OnPropertyChanged(nameof(PanelsTruncated));
        OnPropertyChanged(nameof(StacksTruncated));
        OnPropertyChanged(nameof(CurrentCollectionTruncated));
        OnPropertyChanged(nameof(CoverageText));
        OnPropertyChanged(nameof(HasStaleReason));
        OnPropertyChanged(nameof(HasCopyStatus));
        CopySnapshotCommand.RaiseCanExecuteChanged();
    }

    /// <summary>通知列表、详情、覆盖率和空状态重新计算。</summary>
    private void NotifyCollectionPresentationChanged()
    {
        OnPropertyChanged(nameof(IsPanelsView));
        OnPropertyChanged(nameof(IsStacksView));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(ShowPanelDetails));
        OnPropertyChanged(nameof(ShowStackDetails));
        OnPropertyChanged(nameof(ShowNoSelection));
        OnPropertyChanged(nameof(CurrentCollectionTruncated));
        OnPropertyChanged(nameof(CoverageText));
        OnPropertyChanged(nameof(EmptyTitleText));
        OnPropertyChanged(nameof(EmptyHintText));
        CopySelectedCommand.RaiseCanExecuteChanged();
    }

    /// <summary>通知详情和复制命令跟随当前选中项刷新。</summary>
    private void NotifySelectionChanged()
    {
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(ShowPanelDetails));
        OnPropertyChanged(nameof(ShowStackDetails));
        OnPropertyChanged(nameof(ShowNoSelection));
        CopySelectedCommand.RaiseCanExecuteChanged();
    }
}
