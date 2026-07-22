using YokiFrame.Tooling.Application.Models.FsmKit;
using YokiFrame.Workbench.Avalonia.ViewModels.FsmKit;

namespace YokiFrame.Workbench.Avalonia.ViewModels;

/// <summary>
/// 把 Application 的 FsmKit 强类型状态投影为只读工作台页面。
/// </summary>
public sealed partial class FsmKitPageViewModel : ViewModelBase, IDisposable
{
    private readonly Func<string, CancellationToken, Task<WorkbenchFsmKitState>>? mDetailsQuery;
    private CancellationTokenSource? mDetailsQueryCancellation;
    private IReadOnlyList<WorkbenchFsmStateNode> mStateTree = Array.Empty<WorkbenchFsmStateNode>();
    private IReadOnlyList<WorkbenchFsmStateEvent> mStateEvents = Array.Empty<WorkbenchFsmStateEvent>();
    private IReadOnlyList<string> mEvidencePaths = Array.Empty<string>();
    private FsmMachineListItemViewModel? mSelectedMachine;
    private WorkbenchFsmKitState? mSelectedDetailsState;
    private string mSearchText = string.Empty;
    private string mEngineId = "未选择";
    private string mSessionId = "未知";
    private string mMode = "未知";
    private string mSource = "等待数据";
    private string mTransport = string.Empty;
    private string mUpdatedAtText = "未知";
    private string mStaleReason = string.Empty;
    private string mSelectedInstanceId = string.Empty;
    private string mSelectedMachineName = "未选择";
    private string mDataChannelText = "等待数据";
    private string mMachineState = "End";
    private string mCurrentState = "未选择";
    private string mDiagnosticText = "等待 FsmKit 状态。";
    private string mRawPayload = string.Empty;
    private long mGeneration;
    private bool mIsApplyingState;
    private int mQueryVersion;

    /// <summary>
    /// 创建 FsmKit 页面状态；详情查询为空时页面保持周期只读状态。
    /// </summary>
    /// <param name="detailsQuery">按 instanceId 查询详情的 Application 用例。</param>
    public FsmKitPageViewModel(
        Func<string, CancellationToken, Task<WorkbenchFsmKitState>>? detailsQuery = null)
    {
        mDetailsQuery = detailsQuery;
    }

    /// <summary>当当前 instanceId 变化时通知窗口刷新精确命名 Telemetry 订阅。</summary>
    internal event EventHandler? SelectedInstanceIdChanged;

    /// <summary>获取或设置实例列表搜索文本。</summary>
    public string SearchText
    {
        get => mSearchText;
        set
        {
            if (SetProperty(ref mSearchText, value ?? string.Empty))
            {
                RebuildMachineList();
            }
        }
    }

    /// <summary>获取或设置当前选中的 FSM 摘要。</summary>
    public FsmMachineListItemViewModel? SelectedMachine
    {
        get => mSelectedMachine;
        set => SetSelectedMachine(value);
    }

    /// <summary>获取目标 engine 标识。</summary>
    public string EngineId { get => mEngineId; private set => SetProperty(ref mEngineId, value); }

    /// <summary>获取宿主 session 标识。</summary>
    public string SessionId { get => mSessionId; private set => SetProperty(ref mSessionId, value); }

    /// <summary>获取宿主 generation。</summary>
    public long Generation { get => mGeneration; private set => SetProperty(ref mGeneration, value); }

    /// <summary>获取宿主当前模式。</summary>
    public string Mode { get => mMode; private set => SetProperty(ref mMode, value); }

    /// <summary>获取 telemetry、snapshot 或 command 数据源。</summary>
    public string Source { get => mSource; private set => SetProperty(ref mSource, value); }

    /// <summary>获取显式详情查询实际使用的传输。</summary>
    public string Transport { get => mTransport; private set => SetProperty(ref mTransport, value); }

    /// <summary>获取当前数据的本地更新时间文本。</summary>
    public string UpdatedAtText { get => mUpdatedAtText; private set => SetProperty(ref mUpdatedAtText, value); }

    /// <summary>获取当前数据 stale 或回落原因。</summary>
    public string StaleReason { get => mStaleReason; private set => SetProperty(ref mStaleReason, value); }

    /// <summary>获取当前选择的稳定 instanceId。</summary>
    public string SelectedInstanceId
    {
        get => mSelectedInstanceId;
        private set
        {
            var normalizedValue = value ?? string.Empty;
            if (SetProperty(ref mSelectedInstanceId, normalizedValue))
            {
                ResetSequencedTelemetryAuthorityForInstanceChange(normalizedValue);
                SelectedInstanceIdChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    /// <summary>获取当前选中状态机的用户可见名称。</summary>
    public string SelectedMachineName { get => mSelectedMachineName; private set => SetProperty(ref mSelectedMachineName, value); }

    /// <summary>获取当前 FsmKit 页面实际使用的数据通道。</summary>
    public string DataChannelText { get => mDataChannelText; private set => SetProperty(ref mDataChannelText, value); }

    /// <summary>获取当前 FSM 生命周期状态。</summary>
    public string MachineState { get => mMachineState; private set => SetProperty(ref mMachineState, value); }

    /// <summary>获取当前状态名称。</summary>
    public string CurrentState { get => mCurrentState; private set => SetProperty(ref mCurrentState, value); }

    /// <summary>获取选中 FSM 的递归状态树。</summary>
    public IReadOnlyList<WorkbenchFsmStateNode> StateTree { get => mStateTree; private set => SetProperty(ref mStateTree, value); }

    /// <summary>获取按时间排列且集合身份稳定的状态转换记录。</summary>
    public IReadOnlyList<WorkbenchFsmTransition> Transitions => mTransitions;

    /// <summary>获取状态加入、移除等生命周期事件。</summary>
    public IReadOnlyList<WorkbenchFsmStateEvent> StateEvents { get => mStateEvents; private set => SetProperty(ref mStateEvents, value); }

    /// <summary>获取 Application 返回的完整原始 payload。</summary>
    public string RawPayload { get => mRawPayload; private set => SetProperty(ref mRawPayload, value); }

    /// <summary>获取状态文件或命令响应证据路径。</summary>
    public IReadOnlyList<string> EvidencePaths { get => mEvidencePaths; private set => SetProperty(ref mEvidencePaths, value); }

    /// <summary>获取面向用户的只读诊断摘要。</summary>
    public string DiagnosticText { get => mDiagnosticText; private set => SetProperty(ref mDiagnosticText, value); }

    /// <summary>
    /// 应用周期 dashboard 中的强类型状态，并保持当前实例详情不被 overview 覆盖。
    /// </summary>
    /// <param name="state">Application 已解析的 FsmKit 状态。</param>
    public void ApplyPeriodicState(WorkbenchFsmKitState? state)
    {
        InvalidateQueryForHostChange(state);
        if (state == null)
        {
            ResetState();
            return;
        }

        if (ShouldApplyExactDetails(state))
        {
            if (string.Equals(state.Source, "telemetry", StringComparison.OrdinalIgnoreCase))
            {
                CancelPendingDetailsQuery();
            }

            mSelectedDetailsState = state;
            ApplyState(state);
            return;
        }

        ApplyPeriodicSummary(state);
    }

    /// <summary>显示高频 Shared Memory 的有界失败说明，同时保留最后一份可用详情。</summary>
    /// <param name="diagnostic">Application 已限制长度的失败原因；为空时不改变页面。</param>
    internal void ReportTelemetryIssue(string diagnostic)
    {
        if (string.IsNullOrWhiteSpace(diagnostic))
        {
            return;
        }

        StaleReason = diagnostic;
        DiagnosticText = "Shared Memory 高频读取已暂停: " + diagnostic;
    }

    /// <summary>
    /// 应用一帧强类型状态并保持用户按 instanceId 的选择。
    /// </summary>
    /// <param name="state">Application 已解析状态。</param>
    private void ApplyState(WorkbenchFsmKitState? state)
    {
        if (state == null)
        {
            ResetState();
            return;
        }

        mIsApplyingState = true;
        try
        {
            ApplySource(state);
            ApplyMachineSummaries(state.Machines);
            var selected = FindPreferredMachine(state);
            SelectedMachine = selected;
            ApplyDetails(state, selected);
        }
        finally
        {
            mIsApplyingState = false;
        }
    }

    /// <summary>
    /// 更新宿主身份、来源、原始数据和证据。
    /// </summary>
    /// <param name="state">Application FsmKit 状态。</param>
    private void ApplySource(WorkbenchFsmKitState state)
    {
        ApplyHostIdentity(state);
        Source = WorkbenchFsmKitPresentation.CreateOptionalText(state.Source);
        Transport = state.Transport;
        DataChannelText = WorkbenchFsmKitPresentation.CreateDataChannelText(Source, Transport);
        UpdatedAtText = state.UpdatedAtUtc == DateTimeOffset.MinValue
            ? "未知"
            : state.UpdatedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff");
        StaleReason = state.StaleReason;
        RawPayload = state.RawPayloadJson;
        EvidencePaths = state.EvidencePaths;
    }

    /// <summary>
    /// 使用详情或摘要刷新当前状态与历史。
    /// </summary>
    /// <param name="state">完整 FsmKit 状态。</param>
    /// <param name="summary">当前实例摘要。</param>
    private void ApplyDetails(WorkbenchFsmKitState state, FsmMachineListItemViewModel? summary)
    {
        var details = state.Selected?.InstanceId == summary?.InstanceId ? state.Selected : null;
        SelectedInstanceId = details?.InstanceId ?? summary?.InstanceId ?? state.InstanceId;
        SelectedMachineName = details?.FsmName ?? summary?.Name ?? state.FsmName ?? "未选择";
        MachineState = details?.MachineState ?? summary?.MachineState ?? "End";
        CurrentState = details?.CurrentState ?? summary?.CurrentState ?? "未选择";
        StateTree = details?.States ?? Array.Empty<WorkbenchFsmStateNode>();
        SynchronizeTransitionHistory(state.History);
        StateEvents = state.StateEvents;
        DiagnosticText = WorkbenchFsmKitPresentation.CreateDiagnosticText(
            state,
            summary != null,
            details != null);
        UpdateWorkspacePresentation(details, summary);
    }

    /// <summary>
    /// 设置实例选择，并在用户交互时发起一次显式详情查询。
    /// </summary>
    /// <param name="machine">新选择的实例摘要。</param>
    private void SetSelectedMachine(FsmMachineListItemViewModel? machine)
    {
        if (!SetProperty(ref mSelectedMachine, machine))
        {
            return;
        }

        SelectedInstanceId = machine?.InstanceId ?? string.Empty;
        SelectedMachineName = machine?.Name ?? "未选择";
        MachineState = machine?.MachineState ?? "End";
        CurrentState = machine?.CurrentState ?? "未选择";
        RebuildMachineList();
        if (!mIsApplyingState && machine != null)
        {
            mSelectedDetailsState = null;
            ClearInstanceDetailsForQuery(machine.InstanceId);
            _ = QueryInstanceAsync(machine.InstanceId);
        }
    }

    /// <summary>
    /// 清空上一实例专属详情，避免查询期间把旧历史、原始数据或证据误认为新实例。
    /// </summary>
    /// <param name="instanceId">准备查询的稳定实例标识。</param>
    private void ClearInstanceDetailsForQuery(string instanceId)
    {
        StateTree = Array.Empty<WorkbenchFsmStateNode>();
        ClearTransitionHistory();
        StateEvents = Array.Empty<WorkbenchFsmStateEvent>();
        RawPayload = string.Empty;
        EvidencePaths = Array.Empty<string>();
        StaleReason = string.Empty;
        Source = "正在查询";
        Transport = string.Empty;
        DataChannelText = "查询中";
        DiagnosticText = "正在查询 instanceId: " + instanceId;
        GraphModel = global::YokiFrame.Workbench.Avalonia.ViewModels.FsmKit.ObservedFsmGraphModel.Empty;
        IsGraphEmpty = true;
        GraphEmptyHint = "正在读取该实例的完整状态树和转换历史。";
        HistoryCountText = "0 条转换";
    }

    /// <summary>
    /// 清空页面详情并保留可理解的等待状态。
    /// </summary>
    private void ResetState()
    {
        Interlocked.Increment(ref mQueryVersion);
        ResetSequencedTelemetryAuthority();
        mSelectedDetailsState = null;
        ClearMachineList();
        SelectedMachine = null;
        EngineId = "未选择";
        SessionId = "未知";
        Generation = 0L;
        Mode = "未知";
        Source = "等待数据";
        Transport = string.Empty;
        DataChannelText = "等待数据";
        UpdatedAtText = "未知";
        StaleReason = string.Empty;
        SelectedInstanceId = string.Empty;
        SelectedMachineName = "未选择";
        MachineState = "End";
        CurrentState = "未选择";
        StateTree = Array.Empty<WorkbenchFsmStateNode>();
        ClearTransitionHistory();
        StateEvents = Array.Empty<WorkbenchFsmStateEvent>();
        RawPayload = string.Empty;
        EvidencePaths = Array.Empty<string>();
        DiagnosticText = "等待 FsmKit 状态。";
        ResetWorkspacePresentation();
    }
}
