using Avalonia.Threading;
using YokiFrame.Tooling.Application.Models;
using YokiFrame.Workbench.Avalonia.Diagnostics;

namespace YokiFrame.Workbench.Avalonia.Services;

/// <summary>
/// Kit 级 Shared Memory 遥测通道的通用骨架。
/// 统一持有宿主身份（engine/session/generation）、单调帧游标和后台轮询请求，
/// 固定「低频评估发布请求 → 高频读取 → UI 投影 → 失败暂停到低频重评估」的生命周期；
/// 具体 Kit 只提供读取用例、结果判定与页面投影回调，不再各自复制骨架。
/// 空闲 tick 不复制 payload，未变化帧不调度 UI。
/// </summary>
/// <typeparam name="TResult">Application 遥测读取用例的结果类型。</typeparam>
internal abstract class WorkbenchTelemetryChannel<TResult>
    where TResult : class
{
    /// <summary>一次后台轮询所需的不可变请求：宿主身份、游标上界和可选实例选择。</summary>
    internal sealed class PollRequest
    {
        /// <summary>创建单次轮询请求；每次发布都产生新实例，供引用比较判断请求代次。</summary>
        internal PollRequest(
            string engineId,
            WorkbenchBridgeHealth bridgeHealth,
            long afterSequence,
            string? selectedInstanceId = null)
        {
            EngineId = engineId;
            BridgeHealth = bridgeHealth;
            AfterSequence = afterSequence;
            SelectedInstanceId = selectedInstanceId;
        }

        /// <summary>获取目标 engine。</summary>
        internal string EngineId { get; }

        /// <summary>获取低频 dashboard 已确认的宿主身份。</summary>
        internal WorkbenchBridgeHealth BridgeHealth { get; }

        /// <summary>获取最后接受或检查的 sequence 上界。</summary>
        internal long AfterSequence { get; }

        /// <summary>获取目标实例；仅 FsmKit 详情通道使用，为空时读取 overview。</summary>
        internal string? SelectedInstanceId { get; }
    }

    private readonly Func<bool> mIsClosed;
    private readonly Func<WorkbenchDashboardState?> mCurrentState;
    private readonly Action<string> mReportIssue;
    private PollRequest? mCurrentRequest;
    private string mEngineId = string.Empty;
    private string mSessionId = string.Empty;
    private long mGeneration;
    private long mSequence = long.MinValue;

    /// <summary>创建遥测通道；窗口状态以委托注入，避免通道反向依赖窗口类型。</summary>
    /// <param name="isClosed">窗口是否已关闭。</param>
    /// <param name="currentState">最近一次低频 dashboard 状态。</param>
    /// <param name="reportIssue">向对应 Kit 页面提交可显示诊断的回调。</param>
    protected WorkbenchTelemetryChannel(
        Func<bool> isClosed,
        Func<WorkbenchDashboardState?> currentState,
        Action<string> reportIssue)
    {
        mIsClosed = isClosed;
        mCurrentState = currentState;
        mReportIssue = reportIssue;
    }

    /// <summary>获取诊断追踪前缀，例如 "eventkit.telemetry"。</summary>
    protected abstract string TracePrefix { get; }

    /// <summary>获取帧与当前宿主不一致时的可显示诊断。</summary>
    protected abstract string FrameMismatchDiagnostic { get; }

    /// <summary>获取合法宿主帧被页面拒绝时的可显示诊断；默认与宿主不一致诊断相同。</summary>
    protected virtual string PageRejectDiagnostic => FrameMismatchDiagnostic;

    /// <summary>获取高频轮询是否开放；页面未激活等门控在此返回 false。</summary>
    protected virtual bool IsPollGateOpen => true;

    /// <summary>获取最近一次接受或检查的帧游标。</summary>
    protected long LastSequence => mSequence;

    /// <summary>低频 dashboard 评估来源是否需要高频刷新；不满足时通道整体清空。</summary>
    /// <param name="state">本轮低频 dashboard 状态。</param>
    protected abstract bool IsRefreshActive(WorkbenchDashboardState state);

    /// <summary>按最新 dashboard 身份和当前游标创建下一次后台轮询请求。</summary>
    /// <param name="state">本轮低频 dashboard 状态。</param>
    protected abstract PollRequest CreateRequest(WorkbenchDashboardState state);

    /// <summary>同步执行一次内存段读取；由骨架负责异常转换与暂停。</summary>
    /// <param name="request">当前生效的后台轮询请求。</param>
    protected abstract TResult Poll(PollRequest request);

    /// <summary>判断结果是否属于可忽略的暂态读（未变化或可重试）。</summary>
    protected abstract bool IsTransientRead(TResult result);

    /// <summary>判断结果是否为 parser 后可信的接受帧。</summary>
    protected abstract bool IsAcceptedRead(TResult result);

    /// <summary>判断结果是否携带可信游标；只有可信游标允许推进 sequence。</summary>
    protected abstract bool HasTrustedCursor(TResult result);

    /// <summary>读取结果携带的帧序号。</summary>
    protected abstract long ReadCursor(TResult result);

    /// <summary>读取结果附带的可显示诊断。</summary>
    protected abstract string ReadDiagnostic(TResult result);

    /// <summary>校验 Accepted 帧的宿主身份、实例选择与游标新旧，不含页面应用。</summary>
    protected abstract bool IsFrameConsistent(TResult result, WorkbenchDashboardState state);

    /// <summary>把已通过一致性校验的帧应用到 Kit 页面；false 表示页面拒绝。</summary>
    protected abstract bool TryApplyFrame(TResult result);

    /// <summary>判断 dashboard 身份（含 Kit 扩展维度）是否仍与已捕获身份一致。</summary>
    protected virtual bool IdentityMatches(WorkbenchDashboardState state)
    {
        return string.Equals(mEngineId, state.SelectedEngineId, StringComparison.Ordinal)
            && string.Equals(mSessionId, state.BridgeHealth.SessionId, StringComparison.Ordinal)
            && mGeneration == state.BridgeHealth.Generation;
    }

    /// <summary>捕获新的宿主身份；Kit 扩展维度（选择等）在此一并保存。</summary>
    protected virtual void CaptureIdentity(WorkbenchDashboardState state)
    {
        mEngineId = state.SelectedEngineId;
        mSessionId = state.BridgeHealth.SessionId;
        mGeneration = state.BridgeHealth.Generation;
    }

    /// <summary>校验后台请求仍属于当前 dashboard 身份与页面状态。</summary>
    protected virtual bool RequestMatches(PollRequest request, WorkbenchDashboardState state)
    {
        return IdentityMatches(state)
            && string.Equals(request.EngineId, state.SelectedEngineId, StringComparison.Ordinal)
            && string.Equals(request.BridgeHealth.SessionId, state.BridgeHealth.SessionId, StringComparison.Ordinal)
            && request.BridgeHealth.Generation == state.BridgeHealth.Generation;
    }

    /// <summary>帧成功应用后的后续动作；默认只重新评估本通道刷新模式。</summary>
    protected virtual void OnFrameApplied(WorkbenchDashboardState state)
    {
        UpdateRefreshMode(state);
    }

    /// <summary>根据低频 dashboard 状态发布或清除高频请求；身份变化时重置帧游标。</summary>
    /// <param name="state">本轮低频 dashboard 状态。</param>
    internal void UpdateRefreshMode(WorkbenchDashboardState state)
    {
        if (!IsRefreshActive(state))
        {
            ClearIdentity();
            return;
        }

        if (!IdentityMatches(state))
        {
            CaptureIdentity(state);
            mSequence = long.MinValue;
        }

        Volatile.Write(ref mCurrentRequest, CreateRequest(state));
    }

    /// <summary>清空后台请求、完整宿主身份和帧游标。</summary>
    internal void ClearIdentity()
    {
        Volatile.Write(ref mCurrentRequest, null);
        mEngineId = string.Empty;
        mSessionId = string.Empty;
        mGeneration = 0L;
        mSequence = long.MinValue;
    }

    /// <summary>在共享遥测 tick 中执行一次高频读取；空闲或门控关闭时不做任何工作。</summary>
    /// <param name="cancellationToken">绑定窗口关闭生命周期的取消令牌。</param>
    internal async Task PollOnceAsync(CancellationToken cancellationToken)
    {
        PollRequest? request = Volatile.Read(ref mCurrentRequest);
        if (request == null || !IsPollGateOpen)
        {
            return;
        }

        TResult result;
        try
        {
            result = Poll(request);
        }
        catch (Exception exception)
        {
            WorkbenchStartupTrace.Mark(TracePrefix + ".poll.failed." + exception.GetType().Name);
            await SuspendFailedRequestAsync(request, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (IsTransientRead(result))
        {
            return;
        }

        await DispatchResultAsync(request, result, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>把新帧提交到 UI；单次投影异常只暂停当前请求，不影响持久轮询任务。</summary>
    private async Task DispatchResultAsync(
        PollRequest request,
        TResult result,
        CancellationToken cancellationToken)
    {
        try
        {
            await Dispatcher.UIThread.InvokeAsync(
                () => ApplyResult(request, result),
                DispatcherPriority.Background,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            WorkbenchStartupTrace.Mark(TracePrefix + ".apply.failed." + exception.GetType().Name);
            await SuspendFailedRequestAsync(request, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>在 UI 线程校验并应用一帧：身份守卫 → 拒绝降频 → 一致性 → 页面应用 → 推进游标。</summary>
    private void ApplyResult(PollRequest request, TResult result)
    {
        var currentState = mCurrentState();
        if (mIsClosed()
            || currentState == null
            || !ReferenceEquals(Volatile.Read(ref mCurrentRequest), request)
            || !RequestMatches(request, currentState))
        {
            return;
        }

        if (!IsAcceptedRead(result))
        {
            if (HasTrustedCursor(result) && ReadCursor(result) > mSequence)
            {
                mSequence = ReadCursor(result);
            }

            mReportIssue(ReadDiagnostic(result));
            SuspendRequest(request);
            return;
        }

        if (!IsFrameConsistent(result, currentState))
        {
            mReportIssue(FrameMismatchDiagnostic);
            SuspendRequest(request);
            return;
        }

        if (!TryApplyFrame(result))
        {
            mReportIssue(PageRejectDiagnostic);
            SuspendRequest(request);
            return;
        }

        // 页面可能改写请求代次；只有请求仍为当前代时才推进游标并触发后续刷新评估。
        if (!ReferenceEquals(Volatile.Read(ref mCurrentRequest), request)
            || !RequestMatches(request, currentState))
        {
            return;
        }

        mSequence = ReadCursor(result);
        OnFrameApplied(currentState);
    }

    /// <summary>读取异常时仅在窗口存活的前提下暂停仍匹配的请求。</summary>
    private async Task SuspendFailedRequestAsync(PollRequest request, CancellationToken cancellationToken)
    {
        if (mIsClosed() || cancellationToken.IsCancellationRequested)
        {
            return;
        }

        await Dispatcher.UIThread.InvokeAsync(
            () => SuspendRequest(request),
            DispatcherPriority.Background,
            cancellationToken);
    }

    /// <summary>只暂停仍为当前请求对象的轮询，避免旧失败结果关闭新选择的读取。</summary>
    private void SuspendRequest(PollRequest request)
    {
        if (ReferenceEquals(Volatile.Read(ref mCurrentRequest), request))
        {
            Volatile.Write(ref mCurrentRequest, null);
        }
    }
}
