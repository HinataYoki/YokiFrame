using YokiFrame.Tooling.Application.Models.ActionKit;

namespace YokiFrame.Workbench.Avalonia.ViewModels;

/// <summary>承载 ActionKit 状态来源、身份与聚合指标投影。</summary>
public sealed partial class ActionKitPageViewModel
{
    /// <summary>应用已经通过版本检查的宿主身份、来源和聚合指标。</summary>
    /// <param name="state">本轮可信 ActionKit 强类型状态。</param>
    private void ApplyStateMetadata(WorkbenchActionKitState state)
    {
        mEngineId = state.EngineId;
        mSessionId = state.SessionId;
        mGeneration = state.Generation;
        mVersion = state.Version;
        Source = state.Source;
        StaleReason = state.StaleReason;
        FrameCount = state.Stats.FrameCount;
        FinishedCount = state.Stats.FinishedCount;
        CancelledCount = state.Stats.CancelledCount;
        FaultedCount = state.Stats.FaultedCount;
        EventTotal = state.EventTotal;
        StackTraceEnabled = state.Stats.StackTraceEnabled;
        StackTraceCount = state.Stats.StackTraceCount;
        PayloadTruncated = state.RootsTruncated || state.NodesTruncated
            || state.DepthTruncated || state.StackTruncated || state.EventsTruncated;
    }
}
