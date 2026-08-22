using Avalonia.Threading;
using YokiFrame.Tooling.Application.Models;
using YokiFrame.Tooling.Application.Models.FsmKit;
using YokiFrame.Workbench.Avalonia.Diagnostics;
using YokiFrame.Workbench.Avalonia.Services;

namespace YokiFrame.Workbench.Avalonia;

/// <summary>
/// 承载 Workbench 唯一的 Shared Memory 高频轮询泵和 FsmKit 详情通道。
/// 泵以「通知信号优先 + watchdog 周期兜底」驱动；EventKit 与 LogKit 通道挂在同一 tick 上。
/// </summary>
public sealed partial class WorkbenchWindow
{
    private static readonly TimeSpan SharedMemoryRefreshInterval = TimeSpan.FromMilliseconds(100);
    private readonly FsmKitTelemetryChannel mFsmTelemetryChannel;
    private CancellationTokenSource? mFsmTelemetryPollingCancellation;
    private Task? mFsmTelemetryPollingTask;

    /// <summary>窗口打开后创建唯一后台轮询循环，空闲 tick 不再创建 Task 或捕获闭包。</summary>
    private void StartFsmTelemetryPolling()
    {
        if (mFsmTelemetryPollingTask != null)
        {
            return;
        }

        CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(
            mSession.LifetimeToken);
        mFsmTelemetryPollingCancellation = cancellation;
        mFsmTelemetryPollingTask = PollFsmTelemetryAsync(cancellation.Token);
        mSession.Track(mFsmTelemetryPollingTask);
    }

    /// <summary>窗口关闭时取消后台轮询并清除各通道请求，阻止后续访问 UI。</summary>
    private Task StopFsmTelemetryPolling()
    {
        mEventKitTelemetryChannel.ClearIdentity();
        mLogKitTelemetryChannel.ClearIdentity();
        mFsmTelemetryChannel.ClearIdentity();
        var cancellation = mFsmTelemetryPollingCancellation;
        var pollingTask = mFsmTelemetryPollingTask;
        mFsmTelemetryPollingCancellation = null;
        mFsmTelemetryPollingTask = null;
        cancellation?.Cancel();
        if (cancellation != null && pollingTask != null)
        {
            return ObserveFsmTelemetryPollingShutdownAsync(pollingTask, cancellation);
        }

        cancellation?.Dispose();
        return Task.CompletedTask;
    }

    /// <summary>以持久循环等待通知信号或 watchdog 到期，然后驱动三个 Kit 通道各读取一次。</summary>
    /// <param name="cancellationToken">绑定窗口关闭生命周期的取消令牌。</param>
    private async Task PollFsmTelemetryAsync(CancellationToken cancellationToken)
    {
        Task signalTask = mTelemetryRefreshSignal.WaitAsync(cancellationToken);
        Task watchdogTask = CreateTelemetryWatchdogTask(cancellationToken);
        try
        {
            while (true)
            {
                Task completedTask = await Task.WhenAny(signalTask, watchdogTask).ConfigureAwait(false);
                if (completedTask == signalTask)
                {
                    await signalTask.ConfigureAwait(false);
                    if (mTelemetryRefreshPolicy.MarkTelemetrySignalConsumed()
                        == TelemetryRefreshAction.SignalTelemetry)
                    {
                        ReleaseTelemetryRefreshSignal();
                    }
                    signalTask = mTelemetryRefreshSignal.WaitAsync(cancellationToken);
                }

                if (completedTask == watchdogTask)
                {
                    await watchdogTask.ConfigureAwait(false);
                    watchdogTask = CreateTelemetryWatchdogTask(cancellationToken);
                }

                await mFsmTelemetryChannel.PollOnceAsync(cancellationToken).ConfigureAwait(false);
                await mEventKitTelemetryChannel.PollOnceAsync(cancellationToken).ConfigureAwait(false);
                await mLogKitTelemetryChannel.PollOnceAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            WorkbenchStartupTrace.Mark("fsm.telemetry.poll.cancelled");
        }
    }

    /// <summary>
    /// 通知可用时使用 1 秒 watchdog；通知未发布时保留旧的 100ms 周期兜底。
    /// </summary>
    /// <param name="cancellationToken">Workbench 生命周期取消令牌。</param>
    /// <returns>下一次响应式唤醒或 watchdog 到期任务。</returns>
    private Task CreateTelemetryWatchdogTask(CancellationToken cancellationToken)
    {
        TimeSpan interval = mTelemetryNotificationListener == null
            ? SharedMemoryRefreshInterval
            : TelemetryNotificationWatchdogInterval;
        return Task.Delay(interval, cancellationToken);
    }

    /// <summary>观察停止中的后台任务并在任务真正结束后释放取消源，避免静默遗失 fault。</summary>
    /// <param name="pollingTask">正在结束的唯一轮询任务。</param>
    /// <param name="cancellation">该任务独占的取消源。</param>
    private static async Task ObserveFsmTelemetryPollingShutdownAsync(
        Task pollingTask,
        CancellationTokenSource cancellation)
    {
        try
        {
            await pollingTask.ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            WorkbenchStartupTrace.Mark("fsm.telemetry.poll.stop-failed." + exception.GetType().Name);
        }
        finally
        {
            cancellation.Dispose();
        }
    }

    /// <summary>仅在任一 Kit 来源需要高频刷新时重新评估全部通道模式。</summary>
    /// <param name="state">本轮低频 dashboard 状态。</param>
    private void UpdateSharedMemoryRefreshMode(WorkbenchDashboardState state)
    {
        UpdateTelemetryNotificationMode(state);
        UpdateEventKitTelemetryRefreshMode(state);
        UpdateLogKitTelemetryRefreshMode(state);
        mFsmTelemetryChannel.UpdateRefreshMode(state);
    }

    /// <summary>选择变化时立即轮换 FsmKit 命名段请求，不等待下一次 1 秒 dashboard。</summary>
    private void OnFsmTelemetrySelectionChanged(object? sender, EventArgs eventArgs)
    {
        if (!mIsClosed && mCurrentState != null)
        {
            UpdateSharedMemoryRefreshMode(mCurrentState);
        }
    }

    /// <summary>FsmKit 详情遥测通道：在通用骨架上补充实例选择身份与双重页面诊断。</summary>
    private sealed class FsmKitTelemetryChannel : WorkbenchTelemetryChannel<WorkbenchFsmKitTelemetryReadResult>
    {
        private readonly WorkbenchWindow mWindow;
        private string mTelemetrySource = string.Empty;
        private string mSelectionId = string.Empty;

        /// <summary>创建通道并绑定窗口状态与 FsmKit 页面诊断出口。</summary>
        public FsmKitTelemetryChannel(WorkbenchWindow window)
            : base(
                () => window.mIsClosed,
                () => window.mCurrentState,
                diagnostic => window.mShellViewModel.FsmKitPage.ReportTelemetryIssue(diagnostic))
        {
            mWindow = window;
        }

        /// <inheritdoc />
        protected override string TracePrefix => "fsm.telemetry";

        /// <inheritdoc />
        protected override string FrameMismatchDiagnostic => "Shared Memory 返回了与当前宿主或实例不一致的详情帧。";

        /// <inheritdoc />
        protected override string PageRejectDiagnostic => "Shared Memory 详情未被当前页面身份接受。";

        /// <inheritdoc />
        protected override bool IsRefreshActive(WorkbenchDashboardState state)
        {
            return state.FsmKitState != null
                && state.BridgeHealth.State == WorkbenchBridgeConnectionState.Online
                && state.BridgeHealth.Generation > 0L
                && string.Equals(state.FsmKitState.Source, "telemetry", StringComparison.OrdinalIgnoreCase);
        }

        /// <inheritdoc />
        protected override bool IdentityMatches(WorkbenchDashboardState state)
        {
            return base.IdentityMatches(state)
                && string.Equals(mTelemetrySource, state.FsmKitState?.Source, StringComparison.OrdinalIgnoreCase)
                && string.Equals(mSelectionId, mWindow.mShellViewModel.FsmKitPage.SelectedInstanceId, StringComparison.Ordinal);
        }

        /// <inheritdoc />
        protected override void CaptureIdentity(WorkbenchDashboardState state)
        {
            base.CaptureIdentity(state);
            mTelemetrySource = state.FsmKitState?.Source ?? string.Empty;
            mSelectionId = mWindow.mShellViewModel.FsmKitPage.SelectedInstanceId;
        }

        /// <inheritdoc />
        protected override PollRequest CreateRequest(WorkbenchDashboardState state)
        {
            return new PollRequest(
                state.SelectedEngineId,
                state.BridgeHealth,
                LastSequence,
                mWindow.mShellViewModel.FsmKitPage.SelectedInstanceId);
        }

        /// <inheritdoc />
        protected override bool RequestMatches(PollRequest request, WorkbenchDashboardState state)
        {
            return base.RequestMatches(request, state)
                && string.Equals(
                    mWindow.mShellViewModel.FsmKitPage.SelectedInstanceId,
                    request.SelectedInstanceId,
                    StringComparison.Ordinal);
        }

        /// <inheritdoc />
        protected override WorkbenchFsmKitTelemetryReadResult Poll(PollRequest request)
        {
            return mWindow.mDashboardService.PollFsmKitTelemetry(
                request.EngineId,
                request.BridgeHealth,
                request.SelectedInstanceId!,
                request.AfterSequence);
        }

        /// <inheritdoc />
        protected override bool IsTransientRead(WorkbenchFsmKitTelemetryReadResult result)
        {
            return result.Status is WorkbenchFsmKitTelemetryReadStatus.Unchanged
                or WorkbenchFsmKitTelemetryReadStatus.Retryable;
        }

        /// <inheritdoc />
        protected override bool IsAcceptedRead(WorkbenchFsmKitTelemetryReadResult result)
        {
            return result.Status == WorkbenchFsmKitTelemetryReadStatus.Accepted;
        }

        /// <inheritdoc />
        protected override bool HasTrustedCursor(WorkbenchFsmKitTelemetryReadResult result)
        {
            return result.HasCursor;
        }

        /// <inheritdoc />
        protected override long ReadCursor(WorkbenchFsmKitTelemetryReadResult result)
        {
            return result.Sequence;
        }

        /// <inheritdoc />
        protected override string ReadDiagnostic(WorkbenchFsmKitTelemetryReadResult result)
        {
            return result.Diagnostic;
        }

        /// <inheritdoc />
        protected override bool IsFrameConsistent(
            WorkbenchFsmKitTelemetryReadResult result,
            WorkbenchDashboardState dashboardState)
        {
            if (!result.HasCursor
                || result.Sequence <= LastSequence
                || result.State == null)
            {
                return false;
            }

            // 校验强类型状态仍属于当前宿主和页面选择，避免串段帧污染详情页。
            var selectedInstanceId = mWindow.mShellViewModel.FsmKitPage.SelectedInstanceId;
            return string.Equals(result.State.EngineId, dashboardState.SelectedEngineId, StringComparison.Ordinal)
                && string.Equals(result.State.SessionId, dashboardState.BridgeHealth.SessionId, StringComparison.Ordinal)
                && result.State.Generation == dashboardState.BridgeHealth.Generation
                && string.Equals(result.State.Source, "telemetry", StringComparison.OrdinalIgnoreCase)
                && (string.IsNullOrWhiteSpace(selectedInstanceId)
                    || string.Equals(result.State.Selected?.InstanceId, selectedInstanceId, StringComparison.Ordinal));
        }

        /// <inheritdoc />
        protected override bool TryApplyFrame(WorkbenchFsmKitTelemetryReadResult result)
        {
            return mWindow.mShellViewModel.FsmKitPage.TryApplySequencedTelemetryState(result.State!);
        }

        /// <summary>FsmKit 帧应用后需要重新评估通知与全部 Kit 通道的刷新模式。</summary>
        protected override void OnFrameApplied(WorkbenchDashboardState state)
        {
            mWindow.UpdateSharedMemoryRefreshMode(state);
        }
    }
}
