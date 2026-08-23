using YokiFrame.Tooling.Application.Models.ActionKit;
using YokiFrame.Workbench.Avalonia.Services;

namespace YokiFrame.Workbench.Avalonia.ViewModels.ActionKit;

/// <summary>扩展根动作的 controller 时间源和调用堆栈。</summary>
public sealed class ActionKitRootViewModel : ActionKitNodeViewModel
{
    private bool mCancelRequested;
    private IReadOnlyList<WorkbenchActionKitStackFrame> mStackTrace =
        Array.Empty<WorkbenchActionKitStackFrame>();

    /// <summary>从 Application 根模型创建可绑定根动作。</summary>
    /// <param name="root">强类型根动作。</param>
    public ActionKitRootViewModel(WorkbenchActionKitRoot root)
        : base(root.ActionId)
    {
        Apply(root);
    }

    /// <summary>获取 controller 是否已请求取消。</summary>
    public bool CancelRequested
    {
        get => mCancelRequested;
        private set => SetProperty(ref mCancelRequested, value);
    }

    /// <summary>获取有界 Start 调用堆栈。</summary>
    public IReadOnlyList<WorkbenchActionKitStackFrame> StackTrace
    {
        get => mStackTrace;
        private set => SetProperty(ref mStackTrace, value);
    }

    /// <summary>获取时间源和子节点数量摘要。</summary>
    public string RootSummaryText => UpdateMode + "  ·  " + ChildCount + " "
        + WorkbenchI18nService.Instance.GetString("String.ActionKit.Status.ChildNodes");

    /// <summary>刷新语言相关的根摘要及其子节点派生文本。</summary>
    internal void RefreshLocalization()
    {
        OnPropertyChanged(nameof(RootSummaryText));
        OnPropertyChanged(nameof(UpdateModeShortText));
        RefreshNodeLocalization();
    }

    /// <summary>获取适合紧凑根列表显示的时间源名称。</summary>
    public string UpdateModeShortText => UpdateMode switch
    {
        "ScaledDeltaTime" => "Scaled",
        "UnscaledDeltaTime" => "Unscaled",
        _ => UpdateMode
    };

    /// <summary>用相同 ID 的最新根模型原地更新字段和子树。</summary>
    /// <param name="root">相同 Action ID 的最新根模型。</param>
    internal void Apply(WorkbenchActionKitRoot root)
    {
        ApplyCommon(
            root.ActionId,
            root.Type,
            root.Status,
            root.Paused,
            root.Deinited,
            root.DebugInfo,
            root.ExecutorName,
            root.UpdateMode,
            root.ChildCount,
            root.CurrentChildIndex,
            root.Children);
        UpdateMode = root.UpdateMode;
        CancelRequested = root.CancelRequested;
        StackTrace = root.StackTrace;
        SetHierarchy(0, false, false, IsStarted && !IsFinished);
        OnPropertyChanged(nameof(RootSummaryText));
        OnPropertyChanged(nameof(UpdateModeShortText));
    }

    /// <summary>使用终态历史更新暂留根，保留完成前最后一次完整子树和调用帧。</summary>
    /// <param name="terminalEvent">与当前根 Action ID 匹配的终态事件。</param>
    internal void ApplyTerminalEvent(WorkbenchActionKitEvent terminalEvent)
    {
        if (!string.Equals(ActionId, terminalEvent.ActionId, StringComparison.Ordinal))
            throw new InvalidOperationException("ActionKit terminal event identity must match the retained root.");

        string status = terminalEvent.Outcome switch
        {
            "Completed" => "Finished",
            "Cancelled" => "Cancelled",
            "Faulted" => "Faulted",
            _ => terminalEvent.Outcome
        };
        ApplyTerminalStatus(status);
        SetHierarchy(0, false, false, false);
    }
}
