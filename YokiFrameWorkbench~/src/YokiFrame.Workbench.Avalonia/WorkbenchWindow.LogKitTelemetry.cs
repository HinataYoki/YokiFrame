using YokiFrame.Tooling.Application.Models;
using YokiFrame.Tooling.Application.Models.LogKit;
using YokiFrame.Workbench.Avalonia.Diagnostics;
using YokiFrame.Workbench.Avalonia.Services;

namespace YokiFrame.Workbench.Avalonia;

/// <summary>把 LogKit 内存 telemetry 刷新装配为通用遥测通道，页面未激活时门控关闭。</summary>
public sealed partial class WorkbenchWindow
{
    private readonly LogKitTelemetryChannel mLogKitTelemetryChannel;

    /// <summary>根据页面激活、来源和宿主身份发布或清除 LogKit 高频请求。</summary>
    private void UpdateLogKitTelemetryRefreshMode(WorkbenchDashboardState state)
    {
        mLogKitTelemetryChannel.UpdateRefreshMode(state);
    }

    /// <summary>清空 LogKit 高频请求和完整宿主身份。</summary>
    private void ClearLogKitTelemetryIdentity()
    {
        mLogKitTelemetryChannel.ClearIdentity();
    }

    /// <summary>LogKit 遥测通道：只在页面激活时读取，不访问日志文件。</summary>
    private sealed class LogKitTelemetryChannel : WorkbenchTelemetryChannel<WorkbenchLogKitTelemetryReadResult>
    {
        private readonly WorkbenchWindow mWindow;

        /// <summary>创建通道并绑定窗口状态与 LogKit 页面诊断出口。</summary>
        public LogKitTelemetryChannel(WorkbenchWindow window)
            : base(
                () => window.mIsClosed,
                () => window.mCurrentState,
                diagnostic => window.mShellViewModel.LogKitPage.ReportTelemetryIssue(diagnostic))
        {
            mWindow = window;
        }

        /// <inheritdoc />
        protected override string TracePrefix => "logkit.telemetry";

        /// <inheritdoc />
        protected override string FrameMismatchDiagnostic => "Shared Memory 返回了与当前宿主不一致的 LogKit 帧。";

        /// <summary>仅在 LogKit 页面激活时开放高频读取。</summary>
        protected override bool IsPollGateOpen => mWindow.mShellViewModel.IsLogKitPage;

        /// <inheritdoc />
        protected override bool IsRefreshActive(WorkbenchDashboardState state)
        {
            return mWindow.mShellViewModel.IsLogKitPage
                && state.LogKitState != null
                && state.BridgeHealth.State == WorkbenchBridgeConnectionState.Online
                && state.BridgeHealth.Generation > 0L
                && string.Equals(state.LogKitState.Source, "telemetry", StringComparison.OrdinalIgnoreCase);
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
        protected override WorkbenchLogKitTelemetryReadResult Poll(PollRequest request)
        {
            return mWindow.mDashboardService.PollLogKitTelemetry(
                request.EngineId,
                request.BridgeHealth,
                request.AfterSequence);
        }

        /// <inheritdoc />
        protected override bool IsTransientRead(WorkbenchLogKitTelemetryReadResult result)
        {
            return result.Status is WorkbenchLogKitTelemetryReadStatus.Unchanged
                or WorkbenchLogKitTelemetryReadStatus.Retryable;
        }

        /// <inheritdoc />
        protected override bool IsAcceptedRead(WorkbenchLogKitTelemetryReadResult result)
        {
            return result.Status == WorkbenchLogKitTelemetryReadStatus.Accepted;
        }

        /// <inheritdoc />
        protected override bool HasTrustedCursor(WorkbenchLogKitTelemetryReadResult result)
        {
            return result.HasCursor;
        }

        /// <inheritdoc />
        protected override long ReadCursor(WorkbenchLogKitTelemetryReadResult result)
        {
            return result.Sequence;
        }

        /// <inheritdoc />
        protected override string ReadDiagnostic(WorkbenchLogKitTelemetryReadResult result)
        {
            return result.Diagnostic;
        }

        /// <inheritdoc />
        protected override bool IsFrameConsistent(
            WorkbenchLogKitTelemetryReadResult result,
            WorkbenchDashboardState dashboardState)
        {
            WorkbenchLogKitState? state = result.State;
            return result.HasCursor
                && result.Sequence > LastSequence
                && state != null
                && string.Equals(state.EngineId, dashboardState.SelectedEngineId, StringComparison.Ordinal)
                && string.Equals(state.SessionId, dashboardState.BridgeHealth.SessionId, StringComparison.Ordinal)
                && state.Generation == dashboardState.BridgeHealth.Generation
                && string.Equals(state.Source, "telemetry", StringComparison.OrdinalIgnoreCase);
        }

        /// <inheritdoc />
        protected override bool TryApplyFrame(WorkbenchLogKitTelemetryReadResult result)
        {
            return mWindow.mShellViewModel.LogKitPage.TryApplyTelemetryState(result.State!);
        }
    }
}
