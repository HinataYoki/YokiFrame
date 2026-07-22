namespace YokiFrame.Tooling.Application.Models.FsmKit;

/// <summary>
/// FsmKit 页面跨周期 overview、命名 Telemetry 与显式详情命令之间的合并规则。
/// 纯函数，不持有 UI 状态；ViewModel 只传入当前选择与权威缓存。
/// </summary>
public static class WorkbenchFsmKitDetailsRules
{
    /// <summary>
    /// 解析页面当前期望的实例；首次详情允许采用 payload 的明确选择。
    /// </summary>
    /// <param name="selectedInstanceId">页面当前选中 instanceId；可为空。</param>
    /// <param name="state">候选状态。</param>
    /// <returns>需要由精确详情匹配的稳定 instanceId。</returns>
    public static string GetExpectedInstanceId(string? selectedInstanceId, WorkbenchFsmKitState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return string.IsNullOrWhiteSpace(selectedInstanceId)
            ? state.Selected?.InstanceId ?? state.InstanceId ?? string.Empty
            : selectedInstanceId;
    }

    /// <summary>
    /// 验证一帧详情确实属于目标 instanceId，阻止 overview 或错误响应污染当前实例。
    /// </summary>
    public static bool IsExpectedDetailsState(WorkbenchFsmKitState state, string? instanceId)
    {
        ArgumentNullException.ThrowIfNull(state);
        return !string.IsNullOrWhiteSpace(instanceId)
            && string.Equals(state.Selected?.InstanceId, instanceId, StringComparison.Ordinal);
    }

    /// <summary>
    /// 判断候选详情是否不早于页面已经接受的同实例详情。
    /// </summary>
    public static bool IsDetailsStateCurrent(
        WorkbenchFsmKitState? cachedDetails,
        WorkbenchFsmKitState candidate,
        string? instanceId)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        return cachedDetails == null
            || cachedDetails.Selected?.InstanceId != instanceId
            || candidate.UpdatedAtUtc >= cachedDetails.UpdatedAtUtc;
    }

    /// <summary>
    /// 判断周期帧是否携带当前选择的精确详情，并遵守命名 Telemetry 的顺序权威。
    /// </summary>
    /// <param name="state">最新周期状态。</param>
    /// <param name="selectedInstanceId">页面当前选中 instanceId。</param>
    /// <param name="cachedDetails">已接受的详情缓存。</param>
    /// <param name="hasSequencedTelemetryAuthority">当前宿主实例是否已由单调 sequence 取得权威。</param>
    /// <returns>该帧可安全替换当前详情时返回 true。</returns>
    public static bool ShouldApplyExactDetails(
        WorkbenchFsmKitState state,
        string? selectedInstanceId,
        WorkbenchFsmKitState? cachedDetails,
        bool hasSequencedTelemetryAuthority)
    {
        ArgumentNullException.ThrowIfNull(state);
        string expectedInstanceId = GetExpectedInstanceId(selectedInstanceId, state);
        return IsExpectedDetailsState(state, expectedInstanceId)
            && !hasSequencedTelemetryAuthority
            && IsDetailsStateCurrent(cachedDetails, state, expectedInstanceId);
    }

    /// <summary>
    /// 判断详情响应是否仍属于查询开始时的宿主身份。
    /// </summary>
    public static bool MatchesQueryHost(
        WorkbenchFsmKitState state,
        string visibleEngineId,
        string visibleSessionId,
        long visibleGeneration,
        string queryEngineId,
        string querySessionId,
        long queryGeneration)
    {
        ArgumentNullException.ThrowIfNull(state);
        string stateEngine = WorkbenchFsmKitPresentation.CreateOptionalText(state.EngineId);
        string stateSession = WorkbenchFsmKitPresentation.CreateOptionalText(state.SessionId);
        return string.Equals(visibleEngineId, queryEngineId, StringComparison.Ordinal)
            && string.Equals(visibleSessionId, querySessionId, StringComparison.Ordinal)
            && visibleGeneration == queryGeneration
            && string.Equals(stateEngine, queryEngineId, StringComparison.Ordinal)
            && string.Equals(stateSession, querySessionId, StringComparison.Ordinal)
            && state.Generation == queryGeneration;
    }

    /// <summary>
    /// 判断一帧是否可作为命名 Telemetry 精确详情提交（source + selected 实例）。
    /// </summary>
    public static bool IsSequencedTelemetryDetailsFrame(WorkbenchFsmKitState state, string? expectedInstanceId)
    {
        ArgumentNullException.ThrowIfNull(state);
        return string.Equals(state.Source, "telemetry", StringComparison.OrdinalIgnoreCase)
            && IsExpectedDetailsState(state, GetExpectedInstanceId(expectedInstanceId, state));
    }
}
