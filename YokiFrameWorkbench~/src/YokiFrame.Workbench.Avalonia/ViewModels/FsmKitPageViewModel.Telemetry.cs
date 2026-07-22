using YokiFrame.Tooling.Application.Models.FsmKit;

namespace YokiFrame.Workbench.Avalonia.ViewModels;

/// <summary>承载已由 Window 校验 sequence 的 FsmKit 命名 Telemetry 提交入口。</summary>
public sealed partial class FsmKitPageViewModel
{
    private string mSequencedTelemetryEngineId = string.Empty;
    private string mSequencedTelemetrySessionId = string.Empty;
    private string mSequencedTelemetryInstanceId = string.Empty;
    private long mSequencedTelemetryGeneration;

    /// <summary>提交命名 Telemetry 精确详情，不使用墙钟参与同一代帧排序。</summary>
    /// <param name="state">属于当前宿主、实例和 sequence 的高频详情帧。</param>
    /// <returns>帧身份精确匹配并完成页面提交时返回 true。</returns>
    public bool TryApplySequencedTelemetryState(WorkbenchFsmKitState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        InvalidateQueryForHostChange(state);
        if (!WorkbenchFsmKitDetailsRules.IsSequencedTelemetryDetailsFrame(state, SelectedInstanceId))
        {
            return false;
        }

        CancelPendingDetailsQuery();
        mSelectedDetailsState = state;
        ApplyState(state);
        EstablishSequencedTelemetryAuthority(state);
        return true;
    }

    /// <summary>记录已经由 Window 单调 sequence 校验并成功提交的宿主与实例身份。</summary>
    /// <param name="state">已经完成页面提交的命名 Telemetry 详情。</param>
    private void EstablishSequencedTelemetryAuthority(WorkbenchFsmKitState state)
    {
        mSequencedTelemetryEngineId = state.EngineId;
        mSequencedTelemetrySessionId = state.SessionId;
        mSequencedTelemetryGeneration = state.Generation;
        mSequencedTelemetryInstanceId = state.Selected!.InstanceId;
    }

    /// <summary>判断指定宿主实例是否已由单调 sequence 详情取得替换权威。</summary>
    private bool HasSequencedTelemetryAuthority(
        string engineId,
        string sessionId,
        long generation,
        string instanceId)
    {
        return !string.IsNullOrWhiteSpace(mSequencedTelemetryInstanceId)
            && string.Equals(mSequencedTelemetryEngineId, engineId, StringComparison.Ordinal)
            && string.Equals(mSequencedTelemetrySessionId, sessionId, StringComparison.Ordinal)
            && mSequencedTelemetryGeneration == generation
            && string.Equals(mSequencedTelemetryInstanceId, instanceId, StringComparison.Ordinal);
    }

    /// <summary>实例身份变化时清除旧实例权威，相同 instanceId 的列表对象刷新不受影响。</summary>
    /// <param name="instanceId">页面即将公开的新实例标识。</param>
    private void ResetSequencedTelemetryAuthorityForInstanceChange(string instanceId)
    {
        if (!string.Equals(mSequencedTelemetryInstanceId, instanceId, StringComparison.Ordinal))
        {
            ResetSequencedTelemetryAuthority();
        }
    }

    /// <summary>清空命名 Telemetry 权威，允许新宿主或新实例重新从 dashboard 详情启动。</summary>
    private void ResetSequencedTelemetryAuthority()
    {
        mSequencedTelemetryEngineId = string.Empty;
        mSequencedTelemetrySessionId = string.Empty;
        mSequencedTelemetryGeneration = 0L;
        mSequencedTelemetryInstanceId = string.Empty;
    }
}
