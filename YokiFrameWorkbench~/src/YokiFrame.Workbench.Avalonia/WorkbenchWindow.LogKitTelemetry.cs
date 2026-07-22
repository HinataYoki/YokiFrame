using Avalonia.Threading;
using YokiFrame.Tooling.Application.Models;
using YokiFrame.Tooling.Application.Models.LogKit;
using YokiFrame.Workbench.Avalonia.Diagnostics;

namespace YokiFrame.Workbench.Avalonia;

/// <summary>承载共享 100ms 循环中的 LogKit 内存 telemetry 刷新。</summary>
public sealed partial class WorkbenchWindow
{
    private LogKitTelemetryPollRequest? mLogKitTelemetryPollRequest;
    private string mLogKitTelemetryEngineId = string.Empty;
    private string mLogKitTelemetrySessionId = string.Empty;
    private long mLogKitTelemetryGeneration;
    private long mLogKitTelemetrySequence = long.MinValue;

    /// <summary>仅在 LogKit 页面激活时读取 Shared Memory 新帧，不访问日志文件。</summary>
    private async Task PollLogKitTelemetryOnceAsync(CancellationToken cancellationToken)
    {
        var request = Volatile.Read(ref mLogKitTelemetryPollRequest);
        if (request == null || !mShellViewModel.IsLogKitPage)
        {
            return;
        }

        WorkbenchLogKitTelemetryReadResult result;
        try
        {
            result = mDashboardService.PollLogKitTelemetry(
                request.EngineId,
                request.BridgeHealth,
                request.AfterSequence);
        }
        catch (Exception exception)
        {
            WorkbenchStartupTrace.Mark("logkit.telemetry.poll.failed." + exception.GetType().Name);
            await SuspendFailedLogKitRequestAsync(request, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (result.Status is WorkbenchLogKitTelemetryReadStatus.Unchanged
            or WorkbenchLogKitTelemetryReadStatus.Retryable)
        {
            return;
        }

        await DispatchLogKitResultAsync(request, result, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>把新 LogKit 帧提交到 UI；单页异常只暂停当前请求。</summary>
    private async Task DispatchLogKitResultAsync(
        LogKitTelemetryPollRequest request,
        WorkbenchLogKitTelemetryReadResult result,
        CancellationToken cancellationToken)
    {
        try
        {
            await Dispatcher.UIThread.InvokeAsync(
                () => ApplyLogKitTelemetryResult(request, result),
                DispatcherPriority.Background,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            WorkbenchStartupTrace.Mark("logkit.telemetry.apply.failed." + exception.GetType().Name);
            await SuspendFailedLogKitRequestAsync(request, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>后台读取异常时只暂停仍属于当前宿主代的请求。</summary>
    private async Task SuspendFailedLogKitRequestAsync(
        LogKitTelemetryPollRequest request,
        CancellationToken cancellationToken)
    {
        if (mIsClosed || cancellationToken.IsCancellationRequested)
        {
            return;
        }

        await Dispatcher.UIThread.InvokeAsync(
            () => SuspendLogKitTelemetryPolling(request),
            DispatcherPriority.Background,
            cancellationToken);
    }

    /// <summary>提交可接受帧，或对不可用/拒绝结果暂停到低频 dashboard 重评估。</summary>
    private void ApplyLogKitTelemetryResult(
        LogKitTelemetryPollRequest request,
        WorkbenchLogKitTelemetryReadResult result)
    {
        var currentState = mCurrentState;
        if (mIsClosed
            || currentState == null
            || !ReferenceEquals(Volatile.Read(ref mLogKitTelemetryPollRequest), request)
            || !MatchesLogKitRequest(request, currentState))
        {
            return;
        }

        if (result.Status != WorkbenchLogKitTelemetryReadStatus.Accepted)
        {
            ApplyRejectedLogKitTelemetryResult(request, result);
            return;
        }

        if (!CanApplyAcceptedLogKitFrame(result, currentState))
        {
            mShellViewModel.LogKitPage.ReportTelemetryIssue(
                "Shared Memory 返回了与当前宿主不一致的 LogKit 帧。");
            SuspendLogKitTelemetryPolling(request);
            return;
        }

        mLogKitTelemetrySequence = result.Sequence;
        UpdateLogKitTelemetryRefreshMode(currentState);
    }

    /// <summary>处理非 Accepted 结果，并仅用可信游标推进 sequence。</summary>
    private void ApplyRejectedLogKitTelemetryResult(
        LogKitTelemetryPollRequest request,
        WorkbenchLogKitTelemetryReadResult result)
    {
        if (result.HasCursor && result.Sequence > mLogKitTelemetrySequence)
        {
            mLogKitTelemetrySequence = result.Sequence;
        }

        mShellViewModel.LogKitPage.ReportTelemetryIssue(result.Diagnostic);
        SuspendLogKitTelemetryPolling(request);
    }

    /// <summary>校验并应用与当前 engine/session/generation 一致的新帧。</summary>
    private bool CanApplyAcceptedLogKitFrame(
        WorkbenchLogKitTelemetryReadResult result,
        WorkbenchDashboardState dashboardState)
    {
        var state = result.State;
        return result.HasCursor
            && result.Sequence > mLogKitTelemetrySequence
            && state != null
            && string.Equals(state.EngineId, dashboardState.SelectedEngineId, StringComparison.Ordinal)
            && string.Equals(state.SessionId, dashboardState.BridgeHealth.SessionId, StringComparison.Ordinal)
            && state.Generation == dashboardState.BridgeHealth.Generation
            && string.Equals(state.Source, "telemetry", StringComparison.OrdinalIgnoreCase)
            && mShellViewModel.LogKitPage.TryApplyTelemetryState(state);
    }

    /// <summary>根据页面激活、来源和宿主身份发布或清除 LogKit 高频请求。</summary>
    private void UpdateLogKitTelemetryRefreshMode(WorkbenchDashboardState state)
    {
        var logKitState = state.LogKitState;
        if (!mShellViewModel.IsLogKitPage
            || logKitState == null
            || state.BridgeHealth.State != WorkbenchBridgeConnectionState.Online
            || state.BridgeHealth.Generation <= 0L
            || !string.Equals(logKitState.Source, "telemetry", StringComparison.OrdinalIgnoreCase))
        {
            ClearLogKitTelemetryIdentity();
            return;
        }

        if (!MatchesLogKitIdentity(state))
        {
            mLogKitTelemetryEngineId = state.SelectedEngineId;
            mLogKitTelemetrySessionId = state.BridgeHealth.SessionId;
            mLogKitTelemetryGeneration = state.BridgeHealth.Generation;
            mLogKitTelemetrySequence = long.MinValue;
        }

        LogKitTelemetryPollRequest request = new(
            state.SelectedEngineId,
            state.BridgeHealth,
            mLogKitTelemetrySequence);
        Volatile.Write(ref mLogKitTelemetryPollRequest, request);
    }

    /// <summary>判断 dashboard 是否仍属于当前 LogKit 高频游标身份。</summary>
    private bool MatchesLogKitIdentity(WorkbenchDashboardState state)
    {
        return string.Equals(mLogKitTelemetryEngineId, state.SelectedEngineId, StringComparison.Ordinal)
            && string.Equals(mLogKitTelemetrySessionId, state.BridgeHealth.SessionId, StringComparison.Ordinal)
            && mLogKitTelemetryGeneration == state.BridgeHealth.Generation;
    }

    /// <summary>判断后台请求仍匹配当前 dashboard 身份。</summary>
    private bool MatchesLogKitRequest(
        LogKitTelemetryPollRequest request,
        WorkbenchDashboardState state)
    {
        return MatchesLogKitIdentity(state)
            && string.Equals(request.EngineId, state.SelectedEngineId, StringComparison.Ordinal)
            && string.Equals(request.BridgeHealth.SessionId, state.BridgeHealth.SessionId, StringComparison.Ordinal)
            && request.BridgeHealth.Generation == state.BridgeHealth.Generation;
    }

    /// <summary>只暂停仍为当前请求对象的 LogKit 轮询。</summary>
    private void SuspendLogKitTelemetryPolling(LogKitTelemetryPollRequest request)
    {
        if (ReferenceEquals(Volatile.Read(ref mLogKitTelemetryPollRequest), request))
        {
            Volatile.Write(ref mLogKitTelemetryPollRequest, null);
        }
    }

    /// <summary>清空 LogKit 高频请求和完整宿主身份。</summary>
    private void ClearLogKitTelemetryIdentity()
    {
        Volatile.Write(ref mLogKitTelemetryPollRequest, null);
        mLogKitTelemetryEngineId = string.Empty;
        mLogKitTelemetrySessionId = string.Empty;
        mLogKitTelemetryGeneration = 0L;
        mLogKitTelemetrySequence = long.MinValue;
    }

    /// <summary>保存一次 LogKit 轮询所需的不可变宿主身份和游标。</summary>
    private sealed class LogKitTelemetryPollRequest
    {
        /// <summary>创建单代 LogKit 轮询请求。</summary>
        internal LogKitTelemetryPollRequest(
            string engineId,
            WorkbenchBridgeHealth bridgeHealth,
            long afterSequence)
        {
            EngineId = engineId;
            BridgeHealth = bridgeHealth;
            AfterSequence = afterSequence;
        }

        /// <summary>获取目标 engine。</summary>
        internal string EngineId { get; }
        /// <summary>获取低频 dashboard 已确认宿主身份。</summary>
        internal WorkbenchBridgeHealth BridgeHealth { get; }
        /// <summary>获取最后接受或检查的 sequence 上界。</summary>
        internal long AfterSequence { get; }
    }
}
