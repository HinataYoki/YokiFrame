using System.Collections.ObjectModel;
using YokiFrame.Tooling.Application.Models.ActionKit;
using YokiFrame.Workbench.Avalonia.ViewModels.ActionKit;
using YokiFrame.Workbench.Avalonia.Services;

namespace YokiFrame.Workbench.Avalonia.ViewModels;

/// <summary>承载 ActionKit 活动树、终态事件、详情和显式堆栈操作。</summary>
public sealed partial class ActionKitPageViewModel : ViewModelBase, IDisposable
{
    private readonly Func<string, bool, CancellationToken, Task<WorkbenchActionKitState>>? mSetStackTraceAsync;
    private readonly Func<string, CancellationToken, Task<WorkbenchActionKitState>>? mClearStackTraceAsync;
    private readonly CancellationTokenSource mLifetimeCancellation = new();
    private ActionKitRootViewModel? mSelectedRoot;
    private ActionKitNodeViewModel? mSelectedNode;
    private string mEngineId = string.Empty;
    private string mSessionId = string.Empty;
    private long mGeneration;
    private long mVersion;
    private string mSource = WorkbenchI18nService.Instance.GetString("String.ActionKit.Status.WaitingData");
    private string mStaleReason = string.Empty;
    private string mOperationStatusText = string.Empty;
    private long mFrameCount;
    private long mFinishedCount;
    private long mCancelledCount;
    private long mFaultedCount;
    private long mEventTotal;
    private bool mStackTraceEnabled;
    private int mStackTraceCount;
    private bool mPayloadTruncated;
    private string mSearchText = string.Empty;

    /// <summary>创建可独立预览的只读 ActionKit 页面。</summary>
    public ActionKitPageViewModel() : this(null, null) { }

    /// <summary>创建带 Application 堆栈诊断操作的 ActionKit 页面。</summary>
    internal ActionKitPageViewModel(
        Func<string, bool, CancellationToken, Task<WorkbenchActionKitState>>? setStackTraceAsync,
        Func<string, CancellationToken, Task<WorkbenchActionKitState>>? clearStackTraceAsync)
    {
        mSetStackTraceAsync = setStackTraceAsync;
        mClearStackTraceAsync = clearStackTraceAsync;
        WorkbenchI18nService.Instance.CultureChanged += OnCultureChanged;
        ToggleStackTraceCommand = new AsyncRelayCommand(ToggleStackTraceAsync, CanSetStackTrace);
        ClearStackTraceCommand = new AsyncRelayCommand(ClearStackTraceAsync, CanClearStackTrace);
    }

    /// <summary>获取活动根动作，以及为保持选择而暂留的最近终态根。</summary>
    public ObservableCollection<ActionKitRootViewModel> Roots { get; } = new();

    /// <summary>获取按搜索条件筛选后的根动作。</summary>
    public ObservableCollection<ActionKitRootViewModel> FilteredRoots { get; } = new();

    /// <summary>获取最近根动作终态。</summary>
    public ObservableCollection<ActionKitEventListItemViewModel> Events { get; } = new();

    /// <summary>获取堆栈捕获切换命令。</summary>
    public AsyncRelayCommand ToggleStackTraceCommand { get; }

    /// <summary>获取清空活动堆栈命令。</summary>
    public AsyncRelayCommand ClearStackTraceCommand { get; }

    /// <summary>获取根动作搜索文本；匹配会递归检查整棵动作树。</summary>
    public string SearchText
    {
        get => mSearchText;
        set
        {
            if (SetProperty(ref mSearchText, value)) RefreshFilteredRoots();
        }
    }

    /// <summary>获取当前选择的根动作。</summary>
    public ActionKitRootViewModel? SelectedRoot
    {
        get => mSelectedRoot;
        set => SetSelectedRoot(value);
    }

    /// <summary>获取当前选择的树节点。</summary>
    public ActionKitNodeViewModel? SelectedNode
    {
        get => mSelectedNode;
        set => SetSelectedNode(value);
    }

    /// <summary>获取当前调度帧。</summary>
    public long FrameCount { get => mFrameCount; private set => SetProperty(ref mFrameCount, value); }

    /// <summary>获取累计完成根数量。</summary>
    public long FinishedCount { get => mFinishedCount; private set => SetProperty(ref mFinishedCount, value); }

    /// <summary>获取累计取消根数量。</summary>
    public long CancelledCount { get => mCancelledCount; private set => SetProperty(ref mCancelledCount, value); }

    /// <summary>获取累计故障根数量。</summary>
    public long FaultedCount { get => mFaultedCount; private set => SetProperty(ref mFaultedCount, value); }

    /// <summary>获取累计终态事件数量。</summary>
    public long EventTotal { get => mEventTotal; private set => SetProperty(ref mEventTotal, value); }

    /// <summary>获取堆栈捕获是否开启。</summary>
    public bool StackTraceEnabled
    {
        get => mStackTraceEnabled;
        private set => SetStackTraceEnabled(value);
    }

    /// <summary>获取当前活动堆栈数量。</summary>
    public int StackTraceCount { get => mStackTraceCount; private set => SetProperty(ref mStackTraceCount, value); }

    /// <summary>获取任一诊断预算是否发生裁剪。</summary>
    public bool PayloadTruncated { get => mPayloadTruncated; private set => SetProperty(ref mPayloadTruncated, value); }

    /// <summary>获取当前数据来源。</summary>
    public string Source { get => mSource; private set => SetProperty(ref mSource, value); }

    /// <summary>获取数据读取诊断。</summary>
    public string StaleReason { get => mStaleReason; private set => SetProperty(ref mStaleReason, value); }

    /// <summary>获取最近显式操作结果。</summary>
    public string OperationStatusText
    {
        get => mOperationStatusText;
        private set => SetProperty(ref mOperationStatusText, value);
    }

    /// <summary>获取堆栈切换按钮文本。</summary>
    public string StackTraceButtonText => WorkbenchI18nService.Instance.GetString(
        StackTraceEnabled ? "String.ActionKit.Status.CloseStack" : "String.ActionKit.Status.CaptureStack");

    /// <summary>获取是否存在活动根。</summary>
    public bool HasRoots => Roots.Count > 0;

    /// <summary>获取当前搜索结果是否为空。</summary>
    public bool IsFilteredEmpty => FilteredRoots.Count == 0;

    /// <summary>获取活动根列表是否为空。</summary>
    public bool IsEmpty => !HasRoots;

    /// <summary>获取是否存在当前选择。</summary>
    public bool HasSelection => SelectedRoot != null;

    /// <summary>获取终态事件列表是否为空。</summary>
    public bool IsEventEmpty => Events.Count == 0;

    /// <summary>获取当前选择根的直接子节点。</summary>
    public IReadOnlyList<ActionKitNodeViewModel> SelectedChildren => SelectedRoot == null
        ? Array.Empty<ActionKitNodeViewModel>()
        : SelectedRoot.Children;

    /// <summary>获取包含根动作本身的执行流程起点，保证流程图显示完整根节点。</summary>
    public ObservableCollection<ActionKitNodeViewModel> SelectedFlowNodes { get; } = new();

    /// <summary>获取执行流程是否完全没有可显示的根节点。</summary>
    public bool IsFlowEmpty => SelectedFlowNodes.Count == 0;

    /// <summary>获取当前动作树是否没有可显示的子动作。</summary>
    public bool IsTreeEmpty => SelectedChildren.Count == 0;

    /// <summary>获取当前选择根的调用堆栈。</summary>
    public IReadOnlyList<WorkbenchActionKitStackFrame> SelectedStackTrace =>
        SelectedRoot?.StackTrace ?? Array.Empty<WorkbenchActionKitStackFrame>();

    /// <summary>获取当前根动作是否没有已捕获的调用帧。</summary>
    public bool IsStackTraceEmpty => SelectedStackTrace.Count == 0;

    /// <summary>获取所选节点 ID。</summary>
    public string SelectedActionId => SelectedNode?.ActionId ?? string.Empty;

    /// <summary>获取所选节点类型。</summary>
    public string SelectedActionType => SelectedNode?.Type ?? string.Empty;

    /// <summary>获取所选节点状态。</summary>
    public string SelectedActionStatus => SelectedNode?.Status ?? string.Empty;

    /// <summary>获取所选节点诊断摘要。</summary>
    public string SelectedDebugInfo => SelectedNode?.DebugInfo ?? string.Empty;

    /// <summary>应用低频 dashboard 状态并拒绝同宿主旧版本。</summary>
    /// <param name="state">本轮 ActionKit 强类型状态。</param>
    public void ApplyPeriodicState(WorkbenchActionKitState? state)
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

        if (MatchesIdentity(state) && state.Version == mVersion)
        {
            Source = state.Source;
            StaleReason = state.StaleReason;
            return;
        }

        ApplyState(state);
    }

    /// <summary>取消页面仍在执行的诊断操作。</summary>
    public void Dispose()
    {
        WorkbenchI18nService.Instance.CultureChanged -= OnCultureChanged;
        mLifetimeCancellation.Cancel();
        mLifetimeCancellation.Dispose();
    }

    /// <summary>语言切换时刷新页面派生文本；协议原始字段保持不变。</summary>
    private void OnCultureChanged()
    {
        if (string.Equals(Source, "等待数据", StringComparison.Ordinal)
            || string.Equals(Source, "Waiting for data", StringComparison.Ordinal))
        {
            Source = WorkbenchI18nService.Instance.GetString("String.ActionKit.Status.WaitingData");
        }

        OnPropertyChanged(nameof(StackTraceButtonText));
        OnPropertyChanged(nameof(OperationStatusText));
        for (var index = 0; index < Roots.Count; index++)
        {
            Roots[index].RefreshLocalization();
        }
    }

    /// <summary>应用完整状态并尽量保持根 Action 选择。</summary>
    private void ApplyState(WorkbenchActionKitState state)
    {
        bool sameIdentity = MatchesIdentity(state);
        string selectedRootId = sameIdentity ? SelectedRoot?.ActionId ?? string.Empty : string.Empty;
        string selectedNodeId = sameIdentity ? SelectedNode?.ActionId ?? string.Empty : string.Empty;
        WorkbenchActionKitEvent? selectedTerminalEvent = sameIdentity
            ? FindTerminalEventForMissingRoot(state.Roots, state.Events, selectedRootId)
            : null;
        ActionKitRootViewModel? retainedSelectedRoot = selectedTerminalEvent == null
            ? null
            : SelectedRoot;
        if (!sameIdentity)
        {
            Roots.Clear();
            FilteredRoots.Clear();
            Events.Clear();
            OperationStatusText = string.Empty;
        }

        SynchronizeRoots(state.Roots, retainedSelectedRoot);
        if (retainedSelectedRoot != null && selectedTerminalEvent != null)
        {
            retainedSelectedRoot.ApplyTerminalEvent(selectedTerminalEvent);
        }
        RefreshFilteredRoots();
        SynchronizeEvents(state.Events);
        ApplyStateMetadata(state);
        ActionKitRootViewModel? selectedRoot = FindRoot(selectedRootId);
        if (selectedRoot == null
            && Roots.Count > 0
            && (!sameIdentity || string.IsNullOrEmpty(selectedRootId)))
        {
            selectedRoot = Roots[0];
        }
        SelectedRoot = selectedRoot;
        ActionKitNodeViewModel? selectedNode = selectedRoot?.FindNode(selectedNodeId);
        if (selectedNode == null
            && (!sameIdentity || string.IsNullOrEmpty(selectedNodeId)))
        {
            selectedNode = selectedRoot;
        }

        SelectedNode = selectedNode;
        NotifySelectionProjectionChanged();
        NotifyCollectionStateChanged();
        RaiseOperationCommands();
    }

    /// <summary>按搜索文本更新左栏根动作集合，保持根对象引用稳定。</summary>
    private void RefreshFilteredRoots()
    {
        string query = SearchText.Trim();
        List<ActionKitRootViewModel> targets = new();
        for (var index = 0; index < Roots.Count; index++)
        {
            ActionKitRootViewModel root = Roots[index];
            if (root.MatchesSearch(query)) targets.Add(root);
        }

        for (var targetIndex = 0; targetIndex < targets.Count; targetIndex++)
        {
            ActionKitRootViewModel target = targets[targetIndex];
            int existingIndex = FindFilteredRootIndex(target, targetIndex);
            if (existingIndex < 0)
            {
                FilteredRoots.Insert(targetIndex, target);
            }
            else if (existingIndex != targetIndex)
            {
                FilteredRoots.Move(existingIndex, targetIndex);
            }
        }

        while (FilteredRoots.Count > targets.Count)
        {
            FilteredRoots.RemoveAt(FilteredRoots.Count - 1);
        }

        OnPropertyChanged(nameof(IsFilteredEmpty));
    }

    /// <summary>从尚未对齐的筛选结果区间按根对象引用查找目标。</summary>
    private int FindFilteredRootIndex(ActionKitRootViewModel target, int startIndex)
    {
        for (var index = startIndex; index < FilteredRoots.Count; index++)
        {
            if (ReferenceEquals(FilteredRoots[index], target)) return index;
        }

        return -1;
    }

    /// <summary>按 Action ID、Outcome 与 Frame 复用终态事件行。</summary>
    private void SynchronizeEvents(IReadOnlyList<WorkbenchActionKitEvent> events)
    {
        for (var targetIndex = 0; targetIndex < events.Count; targetIndex++)
        {
            WorkbenchActionKitEvent item = events[targetIndex];
            int existingIndex = FindEventIndex(item, targetIndex);
            ActionKitEventListItemViewModel viewModel;
            if (existingIndex < 0)
            {
                viewModel = new ActionKitEventListItemViewModel(item);
                Events.Insert(targetIndex, viewModel);
            }
            else
            {
                if (existingIndex != targetIndex) Events.Move(existingIndex, targetIndex);
                viewModel = Events[targetIndex];
                viewModel.Apply(item);
            }
        }

        while (Events.Count > events.Count) Events.RemoveAt(Events.Count - 1);
    }

    /// <summary>从尚未对齐的根区间按 Action ID 查找节点。</summary>
    private int FindRootIndex(string actionId, int startIndex)
    {
        for (var index = startIndex; index < Roots.Count; index++)
        {
            if (string.Equals(Roots[index].ActionId, actionId, StringComparison.Ordinal)) return index;
        }

        return -1;
    }

    /// <summary>从尚未对齐的事件区间查找同一终态。</summary>
    private int FindEventIndex(WorkbenchActionKitEvent item, int startIndex)
    {
        for (var index = startIndex; index < Events.Count; index++)
        {
            if (Events[index].Matches(item)) return index;
        }

        return -1;
    }

    /// <summary>清空已离线 Runtime 的页面状态。</summary>
    private void ResetRuntimeState()
    {
        Roots.Clear();
        FilteredRoots.Clear();
        SelectedFlowNodes.Clear();
        Events.Clear();
        SelectedRoot = null;
        mEngineId = string.Empty;
        mSessionId = string.Empty;
        mGeneration = 0L;
        mVersion = 0L;
        Source = WorkbenchI18nService.Instance.GetString("String.ActionKit.Status.WaitingData");
        StaleReason = string.Empty;
        OperationStatusText = string.Empty;
        FrameCount = 0L;
        FinishedCount = 0L;
        CancelledCount = 0L;
        FaultedCount = 0L;
        EventTotal = 0L;
        StackTraceEnabled = false;
        StackTraceCount = 0;
        PayloadTruncated = false;
        NotifyCollectionStateChanged();
        RaiseOperationCommands();
    }

    /// <summary>按稳定 Action ID 查找本轮根。</summary>
    private ActionKitRootViewModel? FindRoot(string actionId)
    {
        for (var index = 0; index < Roots.Count; index++)
        {
            if (string.Equals(Roots[index].ActionId, actionId, StringComparison.Ordinal)) return Roots[index];
        }

        return null;
    }

    /// <summary>判断状态是否属于当前宿主身份。</summary>
    private bool MatchesIdentity(WorkbenchActionKitState state)
    {
        return string.Equals(mEngineId, state.EngineId, StringComparison.Ordinal)
            && string.Equals(mSessionId, state.SessionId, StringComparison.Ordinal)
            && mGeneration == state.Generation;
    }

    /// <summary>替换根选择并同步节点详情、子树与堆栈。</summary>
    private void SetSelectedRoot(ActionKitRootViewModel? value)
    {
        if (!SetProperty(ref mSelectedRoot, value))
        {
            return;
        }

        SelectedNode = value;
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(SelectedChildren));
        SynchronizeSelectedFlowNodes(value);
        OnPropertyChanged(nameof(SelectedFlowNodes));
        OnPropertyChanged(nameof(IsFlowEmpty));
        OnPropertyChanged(nameof(IsTreeEmpty));
        OnPropertyChanged(nameof(SelectedStackTrace));
        OnPropertyChanged(nameof(IsStackTraceEmpty));
    }

    /// <summary>将选中根动作作为流程树唯一顶层节点，子树由 TreeView 递归展开。</summary>
    private void SynchronizeSelectedFlowNodes(ActionKitRootViewModel? root)
    {
        SelectedFlowNodes.Clear();
        if (root != null) SelectedFlowNodes.Add(root);
    }

    /// <summary>替换树节点选择并刷新详情字段。</summary>
    private void SetSelectedNode(ActionKitNodeViewModel? value)
    {
        if (!SetProperty(ref mSelectedNode, value))
        {
            return;
        }

        OnPropertyChanged(nameof(SelectedActionId));
        OnPropertyChanged(nameof(SelectedActionType));
        OnPropertyChanged(nameof(SelectedActionStatus));
        OnPropertyChanged(nameof(SelectedDebugInfo));
    }

    /// <summary>刷新由复用选择对象间接投影的页面级绑定属性。</summary>
    private void NotifySelectionProjectionChanged()
    {
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(SelectedChildren));
        OnPropertyChanged(nameof(SelectedFlowNodes));
        OnPropertyChanged(nameof(IsFlowEmpty));
        OnPropertyChanged(nameof(IsTreeEmpty));
        OnPropertyChanged(nameof(SelectedStackTrace));
        OnPropertyChanged(nameof(IsStackTraceEmpty));
        OnPropertyChanged(nameof(SelectedActionId));
        OnPropertyChanged(nameof(SelectedActionType));
        OnPropertyChanged(nameof(SelectedActionStatus));
        OnPropertyChanged(nameof(SelectedDebugInfo));
    }

    /// <summary>更新堆栈状态并刷新命令文本。</summary>
    private void SetStackTraceEnabled(bool value)
    {
        if (SetProperty(ref mStackTraceEnabled, value))
        {
            OnPropertyChanged(nameof(StackTraceButtonText));
        }
    }

    /// <summary>通知依赖集合数量的空态属性。</summary>
    private void NotifyCollectionStateChanged()
    {
        OnPropertyChanged(nameof(HasRoots));
        OnPropertyChanged(nameof(IsFilteredEmpty));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(IsFlowEmpty));
        OnPropertyChanged(nameof(IsTreeEmpty));
        OnPropertyChanged(nameof(IsEventEmpty));
        OnPropertyChanged(nameof(IsStackTraceEmpty));
    }
}
