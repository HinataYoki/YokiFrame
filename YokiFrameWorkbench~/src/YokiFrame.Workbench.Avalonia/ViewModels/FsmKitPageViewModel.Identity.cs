using YokiFrame.Tooling.Application.Models.FsmKit;

namespace YokiFrame.Workbench.Avalonia.ViewModels;

/// <summary>
/// 维护 FsmKit 页面跨周期刷新和显式查询之间的宿主身份一致性。
/// 合并规则委托 Application <see cref="WorkbenchFsmKitDetailsRules"/>，此处只绑定页面字段。
/// </summary>
public sealed partial class FsmKitPageViewModel
{
    /// <summary>
    /// 更新宿主身份字段，不改变当前选中实例详情的来源和证据。
    /// </summary>
    /// <param name="state">最新周期或命令状态。</param>
    private void ApplyHostIdentity(WorkbenchFsmKitState state)
    {
        EngineId = WorkbenchFsmKitPresentation.CreateOptionalText(state.EngineId);
        SessionId = WorkbenchFsmKitPresentation.CreateOptionalText(state.SessionId);
        Generation = state.Generation;
        Mode = WorkbenchFsmKitPresentation.CreateOptionalText(state.Mode);
    }

    /// <summary>
    /// 宿主身份变化或状态清空时使旧查询失效，并清除不能跨 session 继承的实例选择。
    /// </summary>
    /// <param name="state">最新周期状态。</param>
    private void InvalidateQueryForHostChange(WorkbenchFsmKitState? state)
    {
        if (state != null && IsSameVisibleHost(state))
        {
            return;
        }

        CancelPendingDetailsQuery();
        Interlocked.Increment(ref mQueryVersion);
        ResetSequencedTelemetryAuthority();
        mSelectedDetailsState = null;
        ClearMachineList();
        SelectedMachine = null;
        SelectedInstanceId = string.Empty;
    }

    /// <summary>
    /// 判断周期状态是否属于页面当前可见宿主身份。
    /// </summary>
    private bool IsSameVisibleHost(WorkbenchFsmKitState state)
    {
        return string.Equals(
                EngineId,
                WorkbenchFsmKitPresentation.CreateOptionalText(state.EngineId),
                StringComparison.Ordinal)
            && string.Equals(
                SessionId,
                WorkbenchFsmKitPresentation.CreateOptionalText(state.SessionId),
                StringComparison.Ordinal)
            && Generation == state.Generation;
    }

    /// <summary>
    /// 校验详情响应仍属于查询开始时的宿主，避免旧 session 覆盖新页面。
    /// </summary>
    private bool MatchesQueryHost(
        WorkbenchFsmKitState state,
        string engineId,
        string sessionId,
        long generation)
    {
        return WorkbenchFsmKitDetailsRules.MatchesQueryHost(
            state,
            EngineId,
            SessionId,
            Generation,
            engineId,
            sessionId,
            generation);
    }

    /// <summary>
    /// 判断周期帧是否携带当前选择的精确详情，并遵守来源对应的顺序权威。
    /// </summary>
    private bool ShouldApplyExactDetails(WorkbenchFsmKitState state)
    {
        string expectedInstanceId = WorkbenchFsmKitDetailsRules.GetExpectedInstanceId(
            SelectedInstanceId,
            state);
        return WorkbenchFsmKitDetailsRules.ShouldApplyExactDetails(
            state,
            SelectedInstanceId,
            mSelectedDetailsState,
            HasSequencedTelemetryAuthority(
                state.EngineId,
                state.SessionId,
                state.Generation,
                expectedInstanceId));
    }

    /// <summary>判断候选状态是否携带页面当前选择的精确实例详情，不参与来源顺序判断。</summary>
    private bool HasExpectedDetails(WorkbenchFsmKitState state)
    {
        return WorkbenchFsmKitDetailsRules.IsExpectedDetailsState(
            state,
            WorkbenchFsmKitDetailsRules.GetExpectedInstanceId(SelectedInstanceId, state));
    }

    /// <summary>验证一帧详情确实属于目标 instanceId。</summary>
    private static bool IsExpectedDetailsState(WorkbenchFsmKitState state, string instanceId)
    {
        return WorkbenchFsmKitDetailsRules.IsExpectedDetailsState(state, instanceId);
    }

    /// <summary>判断候选详情是否不早于页面已经接受的同实例详情。</summary>
    private bool IsDetailsStateCurrent(WorkbenchFsmKitState state, string instanceId)
    {
        return WorkbenchFsmKitDetailsRules.IsDetailsStateCurrent(
            mSelectedDetailsState,
            state,
            instanceId);
    }

    /// <summary>判断高频命名 Telemetry 是否已经取得当前实例详情。</summary>
    private bool HasSequencedTelemetryDetails(string instanceId)
    {
        return HasSequencedTelemetryAuthority(EngineId, SessionId, Generation, instanceId);
    }

    /// <summary>
    /// 只合并周期实例列表和宿主身份，避免默认实例覆盖显式 command 详情。
    /// </summary>
    private void ApplyPeriodicSummary(WorkbenchFsmKitState state)
    {
        var selected = state.Machines.FirstOrDefault(
            machine => machine.InstanceId == SelectedInstanceId);
        if (selected == null)
        {
            mSelectedDetailsState = null;
            ApplyState(state);
            return;
        }

        mIsApplyingState = true;
        try
        {
            ApplyHostIdentity(state);
            ApplyMachineSummariesPreservingSelected(state.Machines);
            SelectedMachine = FindMachine(selected.InstanceId);
        }
        finally
        {
            mIsApplyingState = false;
        }
    }
}
