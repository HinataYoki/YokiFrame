using Avalonia.Threading;
using YokiFrame.Tooling.Application.Models;
using YokiFrame.Tooling.Application.Models.FsmKit;
using YokiFrame.Workbench.Avalonia.Diagnostics;

namespace YokiFrame.Workbench.Avalonia;

/// <summary>承载 Workbench 的 FsmKit Shared Memory 高频刷新。</summary>
public sealed partial class WorkbenchWindow
{
    private static readonly TimeSpan SharedMemoryRefreshInterval = TimeSpan.FromMilliseconds(100);
    private CancellationTokenSource? mFsmTelemetryPollingCancellation;
    private Task? mFsmTelemetryPollingTask;
    private FsmTelemetryPollRequest? mFsmTelemetryPollRequest;
    private string mFsmTelemetryEngineId = string.Empty;
    private string mFsmTelemetrySessionId = string.Empty;
    private long mFsmTelemetryGeneration;
    private string mFsmTelemetrySource = string.Empty;
    private string mFsmTelemetrySelectionId = string.Empty;
    private long mFsmTelemetrySequence = long.MinValue;

    /// <summary>窗口打开后创建唯一后台轮询循环，空闲 tick 不再创建 Task 或捕获闭包。</summary>
    private void StartFsmTelemetryPolling()
    {
        if (mFsmTelemetryPollingTask != null)
        {
            return;
        }

        CancellationTokenSource cancellation = new();
        mFsmTelemetryPollingCancellation = cancellation;
        mFsmTelemetryPollingTask = PollFsmTelemetryAsync(cancellation.Token);
    }

    /// <summary>窗口关闭时取消后台轮询并清除当前请求，阻止后续访问 UI。</summary>
    private void StopFsmTelemetryPolling()
    {
        ClearEventKitTelemetryIdentity();
        ClearLogKitTelemetryIdentity();
        Volatile.Write(ref mFsmTelemetryPollRequest, null);
        var cancellation = mFsmTelemetryPollingCancellation;
        var pollingTask = mFsmTelemetryPollingTask;
        mFsmTelemetryPollingCancellation = null;
        mFsmTelemetryPollingTask = null;
        cancellation?.Cancel();
        if (cancellation != null && pollingTask != null)
        {
            _ = ObserveFsmTelemetryPollingShutdownAsync(pollingTask, cancellation);
            return;
        }

        cancellation?.Dispose();
    }

    /// <summary>以持久 PeriodicTimer 读取新 header；未变化帧不调度 UI，也不复制 payload。</summary>
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
                    signalTask = mTelemetryRefreshSignal.WaitAsync(cancellationToken);
                }

                if (completedTask == watchdogTask)
                {
                    await watchdogTask.ConfigureAwait(false);
                    watchdogTask = CreateTelemetryWatchdogTask(cancellationToken);
                }

                var request = Volatile.Read(ref mFsmTelemetryPollRequest);
                if (request != null)
                {
                    if (!TryPollFsmTelemetry(request, out var result))
                    {
                        await SuspendFailedFsmTelemetryRequestAsync(request, cancellationToken).ConfigureAwait(false);
                    }
                    else if (result.Status is not WorkbenchFsmKitTelemetryReadStatus.Unchanged
                        and not WorkbenchFsmKitTelemetryReadStatus.Retryable)
                    {
                        await DispatchFsmTelemetryPollResultAsync(request, result, cancellationToken)
                            .ConfigureAwait(false);
                    }
                }

                await PollEventKitTelemetryOnceAsync(cancellationToken).ConfigureAwait(false);
                await PollLogKitTelemetryOnceAsync(cancellationToken).ConfigureAwait(false);
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

    /// <summary>把新帧提交到 UI；单次页面异常只暂停当前请求，不终止持久轮询任务。</summary>
    private async Task DispatchFsmTelemetryPollResultAsync(
        FsmTelemetryPollRequest request,
        WorkbenchFsmKitTelemetryReadResult result,
        CancellationToken cancellationToken)
    {
        try
        {
            await Dispatcher.UIThread.InvokeAsync(
                () => ApplyFsmTelemetryPollResult(request, result),
                DispatcherPriority.Background,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            WorkbenchStartupTrace.Mark("fsm.telemetry.apply.failed." + exception.GetType().Name);
            await SuspendFailedFsmTelemetryRequestAsync(request, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>同步执行一次内存段读取；异常转换为暂停信号并记录类型。</summary>
    /// <param name="request">UI 线程发布的不可变读取请求。</param>
    /// <param name="result">成功读取时返回明确轮询结果。</param>
    /// <returns>读取用例正常返回时为 true；发生非预期异常时为 false。</returns>
    private bool TryPollFsmTelemetry(
        FsmTelemetryPollRequest request,
        out WorkbenchFsmKitTelemetryReadResult result)
    {
        try
        {
            result = mDashboardService.PollFsmKitTelemetry(
                request.EngineId,
                request.BridgeHealth,
                request.SelectedInstanceId,
                request.AfterSequence);
        }
        catch (Exception exception)
        {
            WorkbenchStartupTrace.Mark("fsm.telemetry.poll.failed." + exception.GetType().Name);
            result = null!;
            return false;
        }

        return true;
    }

    /// <summary>读取异常时仅暂停仍匹配的请求，等待 1 秒 dashboard 重新评估通道。</summary>
    private async Task SuspendFailedFsmTelemetryRequestAsync(
        FsmTelemetryPollRequest request,
        CancellationToken cancellationToken)
    {
        if (mIsClosed || cancellationToken.IsCancellationRequested)
        {
            return;
        }

        await Dispatcher.UIThread.InvokeAsync(
            () => SuspendFsmTelemetryPolling(request),
            DispatcherPriority.Background,
            cancellationToken);
    }

    /// <summary>在身份仍匹配时提交新帧，或对 unavailable/rejected 结果执行有界降频。</summary>
    private void ApplyFsmTelemetryPollResult(
        FsmTelemetryPollRequest request,
        WorkbenchFsmKitTelemetryReadResult result)
    {
        var currentState = mCurrentState;
        if (mIsClosed
            || currentState == null
            || !ReferenceEquals(Volatile.Read(ref mFsmTelemetryPollRequest), request)
            || !MatchesFsmTelemetryRequest(request, currentState))
        {
            return;
        }

        if (result.Status != WorkbenchFsmKitTelemetryReadStatus.Accepted)
        {
            ApplyRejectedFsmTelemetryResult(request, result);
            return;
        }

        if (!result.HasCursor
            || !IsFsmTelemetryCursorNewer(result.Sequence)
            || result.State == null
            || !MatchesTelemetryFrame(result.State, currentState, request.SelectedInstanceId))
        {
            mShellViewModel.FsmKitPage.ReportTelemetryIssue(
                "Shared Memory 返回了与当前宿主或实例不一致的详情帧。");
            SuspendFsmTelemetryPolling(request);
            return;
        }

        if (!mShellViewModel.FsmKitPage.TryApplySequencedTelemetryState(result.State))
        {
            mShellViewModel.FsmKitPage.ReportTelemetryIssue(
                "Shared Memory 详情未被当前页面身份接受。");
            SuspendFsmTelemetryPolling(request);
            return;
        }

        if (!ReferenceEquals(Volatile.Read(ref mFsmTelemetryPollRequest), request)
            || !MatchesFsmTelemetryRequest(request, currentState))
        {
            return;
        }

        AdvanceFsmTelemetryCursor(result.Sequence);
        UpdateSharedMemoryRefreshMode(currentState);
    }

    /// <summary>提交可诊断的拒绝结果；只有 parser 后可信游标才允许推进 sequence。</summary>
    /// <param name="request">产生本结果的不可变读取请求。</param>
    /// <param name="result">非 Accepted 的轮询结果。</param>
    private void ApplyRejectedFsmTelemetryResult(
        FsmTelemetryPollRequest request,
        WorkbenchFsmKitTelemetryReadResult result)
    {
        if (result.HasCursor
            && IsFsmTelemetryCursorNewer(result.Sequence))
        {
            AdvanceFsmTelemetryCursor(result.Sequence);
        }

        mShellViewModel.FsmKitPage.ReportTelemetryIssue(result.Diagnostic);
        SuspendFsmTelemetryPolling(request);
    }

    /// <summary>仅在当前 FsmKit 来源为 telemetry 时发布 100ms 后台读取请求。</summary>
    /// <param name="state">本轮低频 dashboard 状态。</param>
    private void UpdateSharedMemoryRefreshMode(WorkbenchDashboardState state)
    {
        UpdateTelemetryNotificationMode(state);
        UpdateEventKitTelemetryRefreshMode(state);
        UpdateLogKitTelemetryRefreshMode(state);
        var selectedInstanceId = mShellViewModel.FsmKitPage.SelectedInstanceId;
        if (EnsureFsmTelemetryCursorIdentity(state, selectedInstanceId))
        {
            PublishFsmTelemetryPollRequest(state, selectedInstanceId);
            return;
        }

        ClearFsmTelemetryCursorIdentity();
    }

    /// <summary>选择变化时立即轮换命名段请求，不等待下一次 1 秒 dashboard。</summary>
    private void OnFsmTelemetrySelectionChanged(object? sender, EventArgs eventArgs)
    {
        if (!mIsClosed && mCurrentState != null)
        {
            UpdateSharedMemoryRefreshMode(mCurrentState);
        }
    }

    /// <summary>身份变化时重置帧游标；相同低频 dashboard 不打断高频增量读取。</summary>
    private bool EnsureFsmTelemetryCursorIdentity(
        WorkbenchDashboardState state,
        string selectedInstanceId)
    {
        var fsmState = state.FsmKitState;
        if (fsmState == null
            || state.BridgeHealth.State != WorkbenchBridgeConnectionState.Online
            || state.BridgeHealth.Generation <= 0L
            || !string.Equals(fsmState.Source, "telemetry", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (MatchesFsmTelemetryIdentity(state, selectedInstanceId))
        {
            return true;
        }

        mFsmTelemetryEngineId = state.SelectedEngineId;
        mFsmTelemetrySessionId = state.BridgeHealth.SessionId;
        mFsmTelemetryGeneration = state.BridgeHealth.Generation;
        mFsmTelemetrySource = fsmState.Source;
        mFsmTelemetrySelectionId = selectedInstanceId;
        ResetFsmTelemetryFrameCursor();
        return true;
    }

    /// <summary>发布包含宿主身份、选择和单调游标的不可变后台请求。</summary>
    private void PublishFsmTelemetryPollRequest(
        WorkbenchDashboardState state,
        string selectedInstanceId)
    {
        FsmTelemetryPollRequest request = new(
            state.SelectedEngineId,
            state.BridgeHealth,
            selectedInstanceId,
            mFsmTelemetrySequence);
        Volatile.Write(ref mFsmTelemetryPollRequest, request);
    }

    /// <summary>只暂停仍为当前代的请求，避免旧失败结果关闭新选择的轮询。</summary>
    private void SuspendFsmTelemetryPolling(FsmTelemetryPollRequest request)
    {
        if (ReferenceEquals(Volatile.Read(ref mFsmTelemetryPollRequest), request))
        {
            Volatile.Write(ref mFsmTelemetryPollRequest, null);
        }
    }

    /// <summary>判断 dashboard 的 engine、session、generation、来源和选择是否与游标一致。</summary>
    private bool MatchesFsmTelemetryIdentity(
        WorkbenchDashboardState state,
        string selectedInstanceId)
    {
        return string.Equals(mFsmTelemetryEngineId, state.SelectedEngineId, StringComparison.Ordinal)
            && string.Equals(mFsmTelemetrySessionId, state.BridgeHealth.SessionId, StringComparison.Ordinal)
            && mFsmTelemetryGeneration == state.BridgeHealth.Generation
            && string.Equals(mFsmTelemetrySource, state.FsmKitState?.Source, StringComparison.OrdinalIgnoreCase)
            && string.Equals(mFsmTelemetrySelectionId, selectedInstanceId, StringComparison.Ordinal);
    }

    /// <summary>校验后台请求仍属于当前 dashboard 身份与页面选择。</summary>
    private bool MatchesFsmTelemetryRequest(
        FsmTelemetryPollRequest request,
        WorkbenchDashboardState state)
    {
        return MatchesFsmTelemetryIdentity(state, request.SelectedInstanceId)
            && string.Equals(
                mShellViewModel.FsmKitPage.SelectedInstanceId,
                request.SelectedInstanceId,
                StringComparison.Ordinal)
            && string.Equals(request.EngineId, state.SelectedEngineId, StringComparison.Ordinal)
            && string.Equals(request.BridgeHealth.SessionId, state.BridgeHealth.SessionId, StringComparison.Ordinal)
            && request.BridgeHealth.Generation == state.BridgeHealth.Generation;
    }

    /// <summary>校验强类型状态仍属于当前宿主和页面选择。</summary>
    private static bool MatchesTelemetryFrame(
        WorkbenchFsmKitState fsmState,
        WorkbenchDashboardState dashboardState,
        string selectedInstanceId)
    {
        return string.Equals(fsmState.EngineId, dashboardState.SelectedEngineId, StringComparison.Ordinal)
            && string.Equals(fsmState.SessionId, dashboardState.BridgeHealth.SessionId, StringComparison.Ordinal)
            && fsmState.Generation == dashboardState.BridgeHealth.Generation
            && string.Equals(fsmState.Source, "telemetry", StringComparison.OrdinalIgnoreCase)
            && (string.IsNullOrWhiteSpace(selectedInstanceId)
                || string.Equals(fsmState.Selected?.InstanceId, selectedInstanceId, StringComparison.Ordinal));
    }

    /// <summary>在当前 session/generation 内只按单调 sequence 判断新帧，避免系统时钟回拨漏读。</summary>
    private bool IsFsmTelemetryCursorNewer(long sequence)
    {
        return sequence > mFsmTelemetrySequence;
    }

    /// <summary>保存当前 session/generation 内实际接受帧的 sequence。</summary>
    private void AdvanceFsmTelemetryCursor(long sequence)
    {
        mFsmTelemetrySequence = sequence;
    }

    /// <summary>清空完整身份、后台请求和帧游标，供来源离开 telemetry 或宿主下线时使用。</summary>
    private void ClearFsmTelemetryCursorIdentity()
    {
        Volatile.Write(ref mFsmTelemetryPollRequest, null);
        mFsmTelemetryEngineId = string.Empty;
        mFsmTelemetrySessionId = string.Empty;
        mFsmTelemetryGeneration = 0L;
        mFsmTelemetrySource = string.Empty;
        mFsmTelemetrySelectionId = string.Empty;
        ResetFsmTelemetryFrameCursor();
    }

    /// <summary>只重置帧位置，不改动已经确认的宿主与选择身份。</summary>
    private void ResetFsmTelemetryFrameCursor()
    {
        mFsmTelemetrySequence = long.MinValue;
    }

    /// <summary>保存后台一次轮询所需的不可变宿主身份、选择和游标。</summary>
    private sealed class FsmTelemetryPollRequest
    {
        /// <summary>创建单代后台轮询请求。</summary>
        public FsmTelemetryPollRequest(
            string engineId,
            WorkbenchBridgeHealth bridgeHealth,
            string selectedInstanceId,
            long afterSequence)
        {
            EngineId = engineId;
            BridgeHealth = bridgeHealth;
            SelectedInstanceId = selectedInstanceId;
            AfterSequence = afterSequence;
        }

        /// <summary>获取目标 engine。</summary>
        public string EngineId { get; }

        /// <summary>获取低频 dashboard 已确认的宿主身份。</summary>
        public WorkbenchBridgeHealth BridgeHealth { get; }

        /// <summary>获取目标实例；为空时读取 overview。</summary>
        public string SelectedInstanceId { get; }

        /// <summary>获取最后接受或检查的 sequence 上界。</summary>
        public long AfterSequence { get; }

    }
}
