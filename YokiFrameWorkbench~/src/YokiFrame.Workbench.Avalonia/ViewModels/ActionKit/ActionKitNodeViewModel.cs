using System.Collections.ObjectModel;
using YokiFrame.Tooling.Application.Models.ActionKit;
using YokiFrame.Workbench.Avalonia.ViewModels;

namespace YokiFrame.Workbench.Avalonia.ViewModels.ActionKit;

/// <summary>提供可按稳定 Action ID 原地更新的动作树节点投影。</summary>
public class ActionKitNodeViewModel : ViewModelBase
{
    private const int MAX_VISIBLE_INDENT_DEPTH = 6;

    private string mType = string.Empty;
    private string mStatus = string.Empty;
    private string mDebugInfo = string.Empty;
    private string mExecutorName = "PlayerLoop";
    private string mUpdateMode = string.Empty;
    private int mChildCount;
    private int mCurrentChildIndex = -1;
    private bool mIsCurrent;
    private bool mPaused;
    private bool mDeinited;
    private int mDepth;
    private bool mIsCurrentPath;
    private bool mIsInsideParallel;
    private bool mIsInsideRepeat;

    /// <summary>从普通子节点递归创建可绑定树。</summary>
    /// <param name="node">Application 强类型节点。</param>
    public ActionKitNodeViewModel(WorkbenchActionKitNode node)
        : this(node.ActionId)
    {
        Apply(node);
    }

    /// <summary>创建具有稳定 Action ID 的节点基类。</summary>
    /// <param name="actionId">不会经过数值转换的 Action ID。</param>
    protected ActionKitNodeViewModel(string actionId)
    {
        ActionId = actionId;
    }

    /// <summary>获取不会经过数值转换的 Action ID。</summary>
    public string ActionId { get; }

    /// <summary>获取动作类型名。</summary>
    public string Type
    {
        get => mType;
        private set
        {
            if (SetProperty(ref mType, value)) OnPropertyChanged(nameof(DisplayName));
        }
    }

    /// <summary>获取公开生命周期状态。</summary>
    public string Status
    {
        get => mStatus;
        private set
        {
            if (SetProperty(ref mStatus, value)) OnPropertyChanged(nameof(MetadataText));
        }
    }

    /// <summary>获取动作树是否暂停。</summary>
    public bool Paused { get => mPaused; private set => SetProperty(ref mPaused, value); }

    /// <summary>获取当前租约是否已释放。</summary>
    public bool Deinited { get => mDeinited; private set => SetProperty(ref mDeinited, value); }

    /// <summary>获取按需诊断摘要。</summary>
    public string DebugInfo { get => mDebugInfo; private set => SetProperty(ref mDebugInfo, value); }

    /// <summary>获取负责推进该动作的执行器名称。</summary>
    public string ExecutorName { get => mExecutorName; private set => SetProperty(ref mExecutorName, value); }

    /// <summary>获取该节点使用的更新模式。</summary>
    public string UpdateMode { get => mUpdateMode; protected set => SetProperty(ref mUpdateMode, value); }

    /// <summary>获取节点声明的直接子动作数量。</summary>
    public int ChildCount { get => mChildCount; private set => SetProperty(ref mChildCount, value); }

    /// <summary>获取容器当前执行的子动作索引。</summary>
    public int CurrentChildIndex { get => mCurrentChildIndex; private set => SetProperty(ref mCurrentChildIndex, value); }

    /// <summary>获取该节点是否是父容器当前执行的子动作。</summary>
    public bool IsCurrent { get => mIsCurrent; private set => SetProperty(ref mIsCurrent, value); }

    /// <summary>获取节点在当前根动作中的绝对深度，根节点为零。</summary>
    public int Depth
    {
        get => mDepth;
        private set
        {
            if (!SetProperty(ref mDepth, value)) return;
            OnPropertyChanged(nameof(IsDeepNode));
            OnPropertyChanged(nameof(DepthBadgeText));
        }
    }

    /// <summary>获取节点是否位于六层可见缩进之后。</summary>
    public bool IsDeepNode => Depth > MAX_VISIBLE_INDENT_DEPTH;

    /// <summary>获取缩进封顶后继续表达真实层级的深度徽章。</summary>
    public string DepthBadgeText => IsDeepNode ? "L" + Depth : string.Empty;

    /// <summary>获取节点是否处于当前仍在推进的执行路径。</summary>
    public bool IsCurrentPath
    {
        get => mIsCurrentPath;
        private set => SetProperty(ref mIsCurrentPath, value);
    }

    /// <summary>获取节点是否位于 Parallel 并行分支内部。</summary>
    public bool IsInsideParallel
    {
        get => mIsInsideParallel;
        private set => SetProperty(ref mIsInsideParallel, value);
    }

    /// <summary>获取节点是否位于 Repeat 循环边界内部。</summary>
    public bool IsInsideRepeat
    {
        get => mIsInsideRepeat;
        private set => SetProperty(ref mIsInsideRepeat, value);
    }

    /// <summary>获取直接子动作。</summary>
    public ObservableCollection<ActionKitNodeViewModel> Children { get; } = new();

    /// <summary>获取当前节点是否含有子动作。</summary>
    public bool HasChildren => Children.Count > 0;

    /// <summary>获取列表主标题。</summary>
    public string DisplayName => string.IsNullOrWhiteSpace(Type) ? "Action" : Type;

    /// <summary>获取用于类型徽标的短文本。</summary>
    public string TypeBadgeText => DisplayName;

    /// <summary>获取类型是否为 Sequence 容器。</summary>
    public bool IsSequence => string.Equals(Type, "Sequence", StringComparison.OrdinalIgnoreCase);

    /// <summary>获取类型是否为 Parallel 容器。</summary>
    public bool IsParallel => string.Equals(Type, "Parallel", StringComparison.OrdinalIgnoreCase);

    /// <summary>获取类型是否为 Repeat 容器。</summary>
    public bool IsRepeat => string.Equals(Type, "Repeat", StringComparison.OrdinalIgnoreCase);

    /// <summary>获取类型是否为普通叶动作。</summary>
    public bool IsLeaf => !IsSequence && !IsParallel && !IsRepeat;

    /// <summary>获取动作是否已启动。</summary>
    public bool IsStarted => string.Equals(Status, "Started", StringComparison.OrdinalIgnoreCase)
        || string.Equals(Status, "Running", StringComparison.OrdinalIgnoreCase)
        || string.Equals(Status, "Paused", StringComparison.OrdinalIgnoreCase);

    /// <summary>获取动作是否已进入终态。</summary>
    public bool IsFinished => string.Equals(Status, "Finished", StringComparison.OrdinalIgnoreCase)
        || string.Equals(Status, "Completed", StringComparison.OrdinalIgnoreCase)
        || string.Equals(Status, "Cancelled", StringComparison.OrdinalIgnoreCase)
        || string.Equals(Status, "Faulted", StringComparison.OrdinalIgnoreCase);

    /// <summary>获取动作是否处于故障终态。</summary>
    public bool IsFaulted => string.Equals(Status, "Faulted", StringComparison.OrdinalIgnoreCase);

    /// <summary>获取动作是否尚未启动。</summary>
    public bool IsNotStarted => !IsStarted && !IsFinished;

    /// <summary>获取容器当前进度文本；旧快照没有进度时显示子数量。</summary>
    public string ProgressText
    {
        get
        {
            if (TryReadDebugProgress(out int debugCurrent, out int debugTotal))
                return debugCurrent.ToString() + "/" + debugTotal;
            if (ChildCount <= 0) return string.Empty;
            return CurrentChildIndex >= 0
                ? (CurrentChildIndex + 1).ToString() + "/" + ChildCount
                : "0/" + ChildCount;
        }
    }

    /// <summary>获取带组合语义的进度文本，Repeat 明确标注当前轮次。</summary>
    public string ProgressLabelText
    {
        get
        {
            string progress = ProgressText;
            if (string.IsNullOrEmpty(progress)) return string.Empty;
            return IsRepeat ? "轮次 " + progress : progress;
        }
    }

    /// <summary>获取状态与 ID 的紧凑文本。</summary>
    public string MetadataText => Status + "  #" + ActionId;

    /// <summary>用相同 ID 的最新 Application 节点原地更新当前投影。</summary>
    /// <param name="node">相同 Action ID 的最新节点。</param>
    internal void Apply(WorkbenchActionKitNode node)
    {
        ApplyCommon(
            node.ActionId,
            node.Type,
            node.Status,
            node.Paused,
            node.Deinited,
            node.DebugInfo,
            node.ExecutorName,
            node.UpdateMode,
            node.ChildCount,
            node.CurrentChildIndex,
            node.Children);
    }

    /// <summary>在当前子树中按稳定 ID 查找节点，根节点自身也参与匹配。</summary>
    /// <param name="actionId">目标 Action ID。</param>
    /// <returns>匹配节点；不存在时返回 null。</returns>
    internal ActionKitNodeViewModel? FindNode(string actionId)
    {
        if (string.Equals(ActionId, actionId, StringComparison.Ordinal)) return this;
        for (var index = 0; index < Children.Count; index++)
        {
            ActionKitNodeViewModel? match = Children[index].FindNode(actionId);
            if (match != null) return match;
        }

        return null;
    }

    /// <summary>判断当前节点或任一后代是否匹配根列表搜索文本。</summary>
    /// <param name="query">已去除首尾空白的搜索文本。</param>
    internal bool MatchesSearch(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return true;
        if (ContainsSearchValue(Type, query)
            || ContainsSearchValue(Status, query)
            || ContainsSearchValue(ActionId, query)
            || ContainsSearchValue(DebugInfo, query)
            || ContainsSearchValue(ExecutorName, query)
            || ContainsSearchValue(UpdateMode, query)) return true;

        for (var index = 0; index < Children.Count; index++)
        {
            if (Children[index].MatchesSearch(query)) return true;
        }

        return false;
    }

    /// <summary>执行不分配临时字符串的大小写不敏感匹配。</summary>
    private static bool ContainsSearchValue(string value, string query)
    {
        return value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    /// <summary>从旧 Runtime 的 DebugInfo 读取结构化字段尚未覆盖的容器进度。</summary>
    private bool TryReadDebugProgress(out int current, out int total)
    {
        current = 0;
        total = 0;
        if (string.IsNullOrWhiteSpace(DebugInfo)) return false;

        int separator = DebugInfo.IndexOf('/', StringComparison.Ordinal);
        if (separator > 0 && TryReadNumber(DebugInfo, separator - 1, out current)
            && TryReadNumber(DebugInfo, separator + 1, out total) && total > 0)
        {
            return true;
        }

        int indexMarker = DebugInfo.IndexOf("index=", StringComparison.OrdinalIgnoreCase);
        if (indexMarker < 0 || !TryReadNumber(DebugInfo, indexMarker + 6, out current)) return false;
        int open = DebugInfo.IndexOf('(', StringComparison.Ordinal);
        if (open < 0 || !TryReadNumber(DebugInfo, open + 1, out total) || total <= 0) return false;
        return true;
    }

    /// <summary>从指定位置向两侧读取一个非负整数，避免正则和临时字符串分配。</summary>
    private static bool TryReadNumber(string text, int position, out int value)
    {
        value = 0;
        int start = position;
        while (start >= 0 && start < text.Length && !char.IsDigit(text[start])) start--;
        if (start < 0 || start >= text.Length) return false;
        while (start > 0 && char.IsDigit(text[start - 1])) start--;
        int end = start;
        while (end < text.Length && char.IsDigit(text[end])) end++;
        return int.TryParse(text.AsSpan(start, end - start), out value);
    }

    /// <summary>更新公共字段并按 Action ID 复用直接子节点。</summary>
    protected void ApplyCommon(
        string actionId,
        string type,
        string status,
        bool paused,
        bool deinited,
        string debugInfo,
        string executorName,
        string updateMode,
        int childCount,
        int currentChildIndex,
        IReadOnlyList<WorkbenchActionKitNode> children)
    {
        if (!string.Equals(ActionId, actionId, StringComparison.Ordinal))
            throw new InvalidOperationException("ActionKit node identity cannot change during an in-place update.");

        Type = type;
        Status = status;
        Paused = paused;
        Deinited = deinited;
        DebugInfo = debugInfo;
        ExecutorName = string.IsNullOrWhiteSpace(executorName) ? "PlayerLoop" : executorName;
        UpdateMode = updateMode;
        ChildCount = childCount < 0 ? children.Count : childCount;
        CurrentChildIndex = currentChildIndex;
        NotifyDerivedProperties();
        bool hadChildren = Children.Count > 0;
        SynchronizeChildren(children);
        if (hadChildren != (Children.Count > 0)) OnPropertyChanged(nameof(HasChildren));
        RefreshChildHierarchy();
    }

    /// <summary>把已离开 Runtime 活动集合的节点投影更新为有证据的终态。</summary>
    /// <param name="status">由终态事件归一化后的 Action 状态。</param>
    protected void ApplyTerminalStatus(string status)
    {
        Status = status;
        Paused = false;
        NotifyDerivedProperties();
    }

    /// <summary>在类型或状态变化后刷新绑定到派生显示值的属性。</summary>
    private void NotifyDerivedProperties()
    {
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(TypeBadgeText));
        OnPropertyChanged(nameof(IsSequence));
        OnPropertyChanged(nameof(IsParallel));
        OnPropertyChanged(nameof(IsRepeat));
        OnPropertyChanged(nameof(IsLeaf));
        OnPropertyChanged(nameof(IsStarted));
        OnPropertyChanged(nameof(IsFinished));
        OnPropertyChanged(nameof(IsFaulted));
        OnPropertyChanged(nameof(IsNotStarted));
        OnPropertyChanged(nameof(ProgressText));
        OnPropertyChanged(nameof(ProgressLabelText));
        OnPropertyChanged(nameof(MetadataText));
    }

    /// <summary>按目标顺序移动、更新或创建子节点，并删除不再存在的尾项。</summary>
    private void SynchronizeChildren(IReadOnlyList<WorkbenchActionKitNode> nodes)
    {
        for (var targetIndex = 0; targetIndex < nodes.Count; targetIndex++)
        {
            WorkbenchActionKitNode node = nodes[targetIndex];
            int existingIndex = FindChildIndex(node.ActionId, targetIndex);
            ActionKitNodeViewModel viewModel;
            if (existingIndex < 0)
            {
                viewModel = new ActionKitNodeViewModel(node);
                Children.Insert(targetIndex, viewModel);
            }
            else
            {
                if (existingIndex != targetIndex) Children.Move(existingIndex, targetIndex);
                viewModel = Children[targetIndex];
                viewModel.Apply(node);
            }
        }

        while (Children.Count > nodes.Count) Children.RemoveAt(Children.Count - 1);
        for (var childIndex = 0; childIndex < Children.Count; childIndex++)
        {
            Children[childIndex].SetCurrent(childIndex == CurrentChildIndex);
        }
    }

    /// <summary>设置节点绝对层级与继承的组合边界，并递归刷新后代执行路径。</summary>
    /// <param name="depth">节点在根动作中的绝对深度。</param>
    /// <param name="isInsideParallel">父级链是否已进入 Parallel。</param>
    /// <param name="isInsideRepeat">父级链是否已进入 Repeat。</param>
    /// <param name="isCurrentPath">当前节点是否仍处于活动执行路径。</param>
    protected void SetHierarchy(
        int depth,
        bool isInsideParallel,
        bool isInsideRepeat,
        bool isCurrentPath)
    {
        Depth = Math.Max(depth, 0);
        IsInsideParallel = isInsideParallel;
        IsInsideRepeat = isInsideRepeat;
        IsCurrentPath = isCurrentPath;
        RefreshChildHierarchy();
    }

    /// <summary>按 Sequence、Parallel 与 Repeat 的推进语义更新直接子节点层级和活动路径。</summary>
    private void RefreshChildHierarchy()
    {
        for (var childIndex = 0; childIndex < Children.Count; childIndex++)
        {
            ActionKitNodeViewModel child = Children[childIndex];
            bool childIsCurrentPath = IsCurrentPath && IsChildOnCurrentPath(child, childIndex);
            child.SetHierarchy(
                Depth + 1,
                IsInsideParallel || IsParallel,
                IsInsideRepeat || IsRepeat,
                childIsCurrentPath);
        }
    }

    /// <summary>根据当前容器类型判断指定子节点是否仍属于活动执行路径。</summary>
    /// <param name="child">待检查的直接子节点。</param>
    /// <param name="childIndex">子节点在父容器中的顺序。</param>
    /// <returns>子节点应显示活动路径导轨时返回 true。</returns>
    private bool IsChildOnCurrentPath(ActionKitNodeViewModel child, int childIndex)
    {
        if (IsSequence || IsRepeat) return childIndex == CurrentChildIndex;
        if (IsParallel) return child.IsStarted && !child.IsFinished;
        return false;
    }

    /// <summary>由父容器刷新当前执行子节点标记。</summary>
    private void SetCurrent(bool value)
    {
        IsCurrent = value;
    }

    /// <summary>从尚未对齐的区间查找目标子节点。</summary>
    private int FindChildIndex(string actionId, int startIndex)
    {
        for (var index = startIndex; index < Children.Count; index++)
        {
            if (string.Equals(Children[index].ActionId, actionId, StringComparison.Ordinal)) return index;
        }

        return -1;
    }
}
