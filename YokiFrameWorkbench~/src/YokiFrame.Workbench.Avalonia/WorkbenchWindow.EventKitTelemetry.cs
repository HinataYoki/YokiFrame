using YokiFrame.Tooling.Application.Models;
using YokiFrame.Tooling.Application.Models.EventKit;
using YokiFrame.Workbench.Avalonia.Diagnostics;
using YokiFrame.Workbench.Avalonia.Services;

namespace YokiFrame.Workbench.Avalonia;

/// <summary>把 EventKit Shared Memory 刷新装配为通用遥测通道，窗口只保留模式入口。</summary>
public sealed partial class WorkbenchWindow
{
    private readonly EventKitTelemetryChannel mEventKitTelemetryChannel;



    /// <summary>根据低频 dashboard 的来源与宿主身份发布或清除 EventKit 高频请求。</summary>
    private void UpdateEventKitTelemetryRefreshMode(WorkbenchDashboardState state)
    {
        mEventKitTelemetryChannel.UpdateRefreshMode(state);
    }

    /// <summary>清空 EventKit 高频请求和完整宿主身份。</summary>
    private void ClearEventKitTelemetryIdentity()
    {
        mEventKitTelemetryChannel.ClearIdentity();
    }

    /// <summary>EventKit 遥测通道：提供读取用例、结果判定与页面投影。</summary>
    private sealed class EventKitTelemetryChannel : WorkbenchTelemetryChannel<WorkbenchEventKitTelemetryReadResult>
    {
        private readonly WorkbenchWindow mWindow;

        /// <summary>创建通道并绑定窗口状态与 EventKit 页面诊断出口。</summary>
        public EventKitTelemetryChannel(WorkbenchWindow window)
            : base(
                () => window.mIsClosed,
                () => window.mCurrentState,
                diagnostic => window.mShellViewModel.EventKitPage.ReportTelemetryIssue(diagnostic))
        {
            mWindow = window;
        }

        /// <inheritdoc />
        protected override string TracePrefix => "eventkit.telemetry";

        /// <inheritdoc />
        protected override string FrameMismatchDiagnostic => "Shared Memory 返回了与当前宿主不一致的 EventKit 帧。";

        /// <inheritdoc />
        protected override bool IsRefreshActive(WorkbenchDashboardState state)
        {
            return state.EventKitState != null
                && state.BridgeHealth.State == WorkbenchBridgeConnectionState.Online
                && state.BridgeHealth.Generation > 0L
                && string.Equals(state.EventKitState.Source, "telemetry", StringComparison.OrdinalIgnoreCase);
        }

        /// <inheritdoc />
        protected override PollRequest CreateRequest(WorkbenchDashboardState state)
        {
            return new PollRequest(
                state.SelectedEngineId,
                state.BridgeHealth,
                LastSequence);
        }

        /// <inheritdoc />
        protected override WorkbenchEventKitTelemetryReadResult Poll(PollRequest request)
        {
            return mWindow.mDashboardService.PollEventKitTelemetry(
                request.EngineId,
                request.BridgeHealth,
                request.AfterSequence);
        }

        /// <inheritdoc />
        protected override bool IsTransientRead(WorkbenchEventKitTelemetryReadResult result)
        {
            return result.Status is WorkbenchEventKitTelemetryReadStatus.Unchanged
                or WorkbenchEventKitTelemetryReadStatus.Retryable;
        }

        /// <inheritdoc />
        protected override bool IsAcceptedRead(WorkbenchEventKitTelemetryReadResult result)
        {
            return result.Status == WorkbenchEventKitTelemetryReadStatus.Accepted;
        }

        /// <inheritdoc />
        protected override bool HasTrustedCursor(WorkbenchEventKitTelemetryReadResult result)
        {
            return result.HasCursor;
        }

        /// <inheritdoc />
        protected override long ReadCursor(WorkbenchEventKitTelemetryReadResult result)
        {
            return result.Sequence;
        }

        /// <inheritdoc />
        protected override string ReadDiagnostic(WorkbenchEventKitTelemetryReadResult result)
        {
            return result.Diagnostic;
        }

        /// <inheritdoc />
        protected override bool IsFrameConsistent(
            WorkbenchEventKitTelemetryReadResult result,
            WorkbenchDashboardState dashboardState)
        {
            WorkbenchEventKitState? state = result.State;
            return result.HasCursor
                && result.Sequence > LastSequence
                && state != null
                && string.Equals(state.EngineId, dashboardState.SelectedEngineId, StringComparison.Ordinal)
                && string.Equals(state.SessionId, dashboardState.BridgeHealth.SessionId, StringComparison.Ordinal)
                && state.Generation == dashboardState.BridgeHealth.Generation
                && string.Equals(state.Source, "telemetry", StringComparison.OrdinalIgnoreCase);
        }

        /// <inheritdoc />
        protected override bool TryApplyFrame(WorkbenchEventKitTelemetryReadResult result)
        {
            return mWindow.mShellViewModel.EventKitPage.TryApplyTelemetryState(result.State!);
        }
    }
}
