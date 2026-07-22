using Avalonia.Threading;
using YokiFrame.Tooling.Application.Models;
using YokiFrame.Tooling.Application.Models.EventKit;
using YokiFrame.Workbench.Avalonia.Diagnostics;

namespace YokiFrame.Workbench.Avalonia;

/// <summary>承载 Workbench 共享轮询循环中的 EventKit Shared Memory 刷新。</summary>
public sealed partial class WorkbenchWindow
{
    private EventKitTelemetryPollRequest? mEventKitTelemetryPollRequest;
    private string mEventKitTelemetryEngineId = string.Empty;
    private string mEventKitTelemetrySessionId = string.Empty;
    private long mEventKitTelemetryGeneration;
    private long mEventKitTelemetrySequence = long.MinValue;

    /// <summary>在共享 100ms tick 中读取 EventKit 新 header；空闲时不复制 payload。</summary>
    private async Task PollEventKitTelemetryOnceAsync(CancellationToken cancellationToken)
    {
        EventKitTelemetryPollRequest? request = Volatile.Read(ref mEventKitTelemetryPollRequest);
        if (request == null)
        {
            return;
        }

        WorkbenchEventKitTelemetryReadResult result;
        try
        {
            result = mDashboardService.PollEventKitTelemetry(
                request.EngineId,
                request.BridgeHealth,
                request.AfterSequence);
        }
        catch (Exception exception)
        {
            WorkbenchStartupTrace.Mark("eventkit.telemetry.poll.failed." + exception.GetType().Name);
            await SuspendFailedEventKitRequestAsync(request, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (result.Status is WorkbenchEventKitTelemetryReadStatus.Unchanged
            or WorkbenchEventKitTelemetryReadStatus.Retryable)
        {
            return;
        }

        await DispatchEventKitResultAsync(request, result, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>把 EventKit 新帧提交到 UI；页面异常只暂停当前请求。</summary>
    private async Task DispatchEventKitResultAsync(
        EventKitTelemetryPollRequest request,
        WorkbenchEventKitTelemetryReadResult result,
        CancellationToken cancellationToken)
    {
        try
        {
            await Dispatcher.UIThread.InvokeAsync(
                () => ApplyEventKitTelemetryResult(request, result),
                DispatcherPriority.Background,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            WorkbenchStartupTrace.Mark("eventkit.telemetry.apply.failed." + exception.GetType().Name);
            await SuspendFailedEventKitRequestAsync(request, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>后台读取异常时仅暂停仍属于当前宿主代的请求。</summary>
    private async Task SuspendFailedEventKitRequestAsync(
        EventKitTelemetryPollRequest request,
        CancellationToken cancellationToken)
    {
        if (mIsClosed || cancellationToken.IsCancellationRequested)
        {
            return;
        }

        await Dispatcher.UIThread.InvokeAsync(
            () => SuspendEventKitTelemetryPolling(request),
            DispatcherPriority.Background,
            cancellationToken);
    }

    /// <summary>提交可接受新帧，或对不可用/拒绝结果暂停到低频 dashboard 重评估。</summary>
    private void ApplyEventKitTelemetryResult(
        EventKitTelemetryPollRequest request,
        WorkbenchEventKitTelemetryReadResult result)
    {
        WorkbenchDashboardState? currentState = mCurrentState;
        if (mIsClosed
            || currentState == null
            || !ReferenceEquals(Volatile.Read(ref mEventKitTelemetryPollRequest), request)
            || !MatchesEventKitRequest(request, currentState))
        {
            return;
        }

        if (result.Status != WorkbenchEventKitTelemetryReadStatus.Accepted)
        {
            if (result.HasCursor && result.Sequence > mEventKitTelemetrySequence)
            {
                mEventKitTelemetrySequence = result.Sequence;
            }

            mShellViewModel.EventKitPage.ReportTelemetryIssue(result.Diagnostic);
            SuspendEventKitTelemetryPolling(request);
            return;
        }

        if (!CanApplyAcceptedEventKitFrame(result, currentState))
        {
            mShellViewModel.EventKitPage.ReportTelemetryIssue(
                "Shared Memory 返回了与当前宿主不一致的 EventKit 帧。");
            SuspendEventKitTelemetryPolling(request);
            return;
        }

        mEventKitTelemetrySequence = result.Sequence;
        UpdateEventKitTelemetryRefreshMode(currentState);
    }

    /// <summary>校验并应用一帧与当前 engine/session/generation 一致的 EventKit 状态。</summary>
    private bool CanApplyAcceptedEventKitFrame(
        WorkbenchEventKitTelemetryReadResult result,
        WorkbenchDashboardState dashboardState)
    {
        WorkbenchEventKitState? state = result.State;
        return result.HasCursor
            && result.Sequence > mEventKitTelemetrySequence
            && state != null
            && string.Equals(state.EngineId, dashboardState.SelectedEngineId, StringComparison.Ordinal)
            && string.Equals(state.SessionId, dashboardState.BridgeHealth.SessionId, StringComparison.Ordinal)
            && state.Generation == dashboardState.BridgeHealth.Generation
            && string.Equals(state.Source, "telemetry", StringComparison.OrdinalIgnoreCase)
            && mShellViewModel.EventKitPage.TryApplyTelemetryState(state);
    }

    /// <summary>根据低频 dashboard 的来源与宿主身份发布或清除 EventKit 高频请求。</summary>
    private void UpdateEventKitTelemetryRefreshMode(WorkbenchDashboardState state)
    {
        WorkbenchEventKitState? eventState = state.EventKitState;
        if (eventState == null
            || state.BridgeHealth.State != WorkbenchBridgeConnectionState.Online
            || state.BridgeHealth.Generation <= 0L
            || !string.Equals(eventState.Source, "telemetry", StringComparison.OrdinalIgnoreCase))
        {
            ClearEventKitTelemetryIdentity();
            return;
        }

        if (!MatchesEventKitIdentity(state))
        {
            mEventKitTelemetryEngineId = state.SelectedEngineId;
            mEventKitTelemetrySessionId = state.BridgeHealth.SessionId;
            mEventKitTelemetryGeneration = state.BridgeHealth.Generation;
            mEventKitTelemetrySequence = long.MinValue;
        }

        EventKitTelemetryPollRequest request = new(
            state.SelectedEngineId,
            state.BridgeHealth,
            mEventKitTelemetrySequence);
        Volatile.Write(ref mEventKitTelemetryPollRequest, request);
    }

    /// <summary>判断 dashboard 是否仍属于当前 EventKit 高频游标身份。</summary>
    private bool MatchesEventKitIdentity(WorkbenchDashboardState state)
    {
        return string.Equals(mEventKitTelemetryEngineId, state.SelectedEngineId, StringComparison.Ordinal)
            && string.Equals(mEventKitTelemetrySessionId, state.BridgeHealth.SessionId, StringComparison.Ordinal)
            && mEventKitTelemetryGeneration == state.BridgeHealth.Generation;
    }

    /// <summary>判断后台请求仍匹配当前 dashboard 身份。</summary>
    private bool MatchesEventKitRequest(
        EventKitTelemetryPollRequest request,
        WorkbenchDashboardState state)
    {
        return MatchesEventKitIdentity(state)
            && string.Equals(request.EngineId, state.SelectedEngineId, StringComparison.Ordinal)
            && string.Equals(request.BridgeHealth.SessionId, state.BridgeHealth.SessionId, StringComparison.Ordinal)
            && request.BridgeHealth.Generation == state.BridgeHealth.Generation;
    }

    /// <summary>只暂停仍为当前请求对象的 EventKit 轮询。</summary>
    private void SuspendEventKitTelemetryPolling(EventKitTelemetryPollRequest request)
    {
        if (ReferenceEquals(Volatile.Read(ref mEventKitTelemetryPollRequest), request))
        {
            Volatile.Write(ref mEventKitTelemetryPollRequest, null);
        }
    }

    /// <summary>清空 EventKit 高频请求和完整宿主身份。</summary>
    private void ClearEventKitTelemetryIdentity()
    {
        Volatile.Write(ref mEventKitTelemetryPollRequest, null);
        mEventKitTelemetryEngineId = string.Empty;
        mEventKitTelemetrySessionId = string.Empty;
        mEventKitTelemetryGeneration = 0L;
        mEventKitTelemetrySequence = long.MinValue;
    }

    /// <summary>保存 EventKit 一次轮询所需的不可变宿主身份和游标。</summary>
    private sealed class EventKitTelemetryPollRequest
    {
        /// <summary>创建单代 EventKit 轮询请求。</summary>
        internal EventKitTelemetryPollRequest(
            string engineId,
            WorkbenchBridgeHealth bridgeHealth,
            long afterSequence)
        {
            EngineId = engineId;
            BridgeHealth = bridgeHealth;
            AfterSequence = afterSequence;
        }

        internal string EngineId { get; }
        internal WorkbenchBridgeHealth BridgeHealth { get; }
        internal long AfterSequence { get; }
    }
}
