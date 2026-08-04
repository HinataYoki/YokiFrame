namespace YokiFrame.Workbench.Avalonia.Services;

/// <summary>
/// 表示 Workbench 刷新请求的来源，用于区分高频 telemetry 通知和低频 Dashboard 刷新。
/// </summary>
internal enum TelemetryRefreshTrigger
{
    /// <summary>Shared Memory 有新帧，只应唤醒 telemetry 读取循环。</summary>
    TelemetryNotification,

    /// <summary>低频文件计时器到期，可受节流窗口限制。</summary>
    LowFrequencyDashboard,

    /// <summary>用户或命令流程明确要求重新读取 Dashboard。</summary>
    ExplicitDashboard,

    /// <summary>engine registry 或 heartbeat 身份发生变化。</summary>
    EngineLifecycle
}

/// <summary>
/// 表示 Workbench 刷新请求经过合并后的执行动作。
/// </summary>
internal enum TelemetryRefreshAction
{
    /// <summary>当前请求已被合并或被节流，无需启动新任务。</summary>
    None,

    /// <summary>只唤醒 Shared Memory telemetry 读取循环。</summary>
    SignalTelemetry,

    /// <summary>启动一次完整 Dashboard 读取。</summary>
    StartDashboard
}

/// <summary>
/// 在不混淆高频 telemetry 与低频 Dashboard 的前提下合并刷新请求。
/// </summary>
internal sealed class TelemetryRefreshPolicy
{
    private readonly object mSyncRoot = new();
    private readonly TimeSpan mDashboardRefreshInterval;
    private bool mTelemetrySignalPending;
    private bool mTelemetrySignalRequestedWhilePending;
    private bool mDashboardRefreshInFlight;
    private bool mDashboardRefreshPending;
    private bool mImmediateDashboardRefreshPending;
    private DateTimeOffset? mLastDashboardRefreshStartedAtUtc;

    /// <summary>
    /// 创建刷新策略并设置低频 Dashboard 读取的最小启动间隔。
    /// </summary>
    /// <param name="dashboardRefreshInterval">低频 Dashboard 读取的最小间隔。</param>
    internal TelemetryRefreshPolicy(TimeSpan dashboardRefreshInterval)
    {
        if (dashboardRefreshInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(dashboardRefreshInterval),
                "Dashboard refresh interval must be positive.");
        }

        mDashboardRefreshInterval = dashboardRefreshInterval;
    }

    /// <summary>
    /// 提交一个刷新请求，并返回调用方应执行的唯一动作。
    /// </summary>
    /// <param name="trigger">请求来源。</param>
    /// <param name="nowUtc">当前 UTC 时间，供低频节流和测试确定性使用。</param>
    /// <returns>合并后的执行动作。</returns>
    internal TelemetryRefreshAction Request(
        TelemetryRefreshTrigger trigger,
        DateTimeOffset nowUtc)
    {
        lock (mSyncRoot)
        {
            if (trigger == TelemetryRefreshTrigger.TelemetryNotification)
            {
                return RequestTelemetrySignal();
            }

            bool immediate = trigger != TelemetryRefreshTrigger.LowFrequencyDashboard;
            if (mDashboardRefreshInFlight)
            {
                mDashboardRefreshPending = true;
                mImmediateDashboardRefreshPending |= immediate;
                return TelemetryRefreshAction.None;
            }

            if (!immediate && !IsDashboardRefreshDue(nowUtc))
            {
                return TelemetryRefreshAction.None;
            }

            return StartDashboardRefresh(nowUtc);
        }
    }

    /// <summary>
    /// 标记一个 telemetry semaphore 信号已经被读取循环消费。
    /// </summary>
    internal TelemetryRefreshAction MarkTelemetrySignalConsumed()
    {
        lock (mSyncRoot)
        {
            if (!mTelemetrySignalRequestedWhilePending)
            {
                mTelemetrySignalPending = false;
                return TelemetryRefreshAction.None;
            }

            // 通知在 WaitAsync 完成到这里之间到达；保留下一代信号，
            // 由调用方重新 Release semaphore，避免高频变化被竞态吞掉。
            mTelemetrySignalRequestedWhilePending = false;
            mTelemetrySignalPending = true;
            return TelemetryRefreshAction.SignalTelemetry;
        }
    }

    /// <summary>
    /// 完成当前 Dashboard 读取，并在存在更高优先级或已到期的挂起请求时启动下一次读取。
    /// </summary>
    /// <param name="nowUtc">当前 UTC 时间。</param>
    /// <returns>是否应立即启动下一次 Dashboard 读取。</returns>
    internal TelemetryRefreshAction CompleteDashboardRefresh(DateTimeOffset nowUtc)
    {
        lock (mSyncRoot)
        {
            if (!mDashboardRefreshInFlight)
            {
                return TelemetryRefreshAction.None;
            }

            mDashboardRefreshInFlight = false;
            if (mImmediateDashboardRefreshPending
                || (mDashboardRefreshPending && IsDashboardRefreshDue(nowUtc)))
            {
                return StartDashboardRefresh(nowUtc);
            }

            return TelemetryRefreshAction.None;
        }
    }

    /// <summary>
    /// 取消窗口生命周期时清除所有挂起状态，防止关闭后的新任务被策略重新启动。
    /// </summary>
    internal void Reset()
    {
        lock (mSyncRoot)
        {
            mTelemetrySignalPending = false;
            mTelemetrySignalRequestedWhilePending = false;
            mDashboardRefreshInFlight = false;
            mDashboardRefreshPending = false;
            mImmediateDashboardRefreshPending = false;
            mLastDashboardRefreshStartedAtUtc = null;
        }
    }

    /// <summary>
    /// 合并 telemetry 通知，确保 semaphore 中同一时间最多保留一个待消费信号。
    /// </summary>
    /// <returns>需要向 semaphore 释放新信号时返回 SignalTelemetry。</returns>
    private TelemetryRefreshAction RequestTelemetrySignal()
    {
        if (mTelemetrySignalPending)
        {
            mTelemetrySignalRequestedWhilePending = true;
            return TelemetryRefreshAction.None;
        }

        mTelemetrySignalPending = true;
        return TelemetryRefreshAction.SignalTelemetry;
    }

    /// <summary>
    /// 判断低频 Dashboard 请求是否已经超过最小启动间隔。
    /// </summary>
    /// <param name="nowUtc">当前 UTC 时间。</param>
    /// <returns>没有历史启动时间或间隔已到期时返回 true。</returns>
    private bool IsDashboardRefreshDue(DateTimeOffset nowUtc)
    {
        return !mLastDashboardRefreshStartedAtUtc.HasValue
            || nowUtc - mLastDashboardRefreshStartedAtUtc.Value >= mDashboardRefreshInterval;
    }

    /// <summary>
    /// 记录一次 Dashboard 读取启动，并清除已合并的挂起标记。
    /// </summary>
    /// <param name="nowUtc">本次读取启动时间。</param>
    /// <returns>启动 Dashboard 读取动作。</returns>
    private TelemetryRefreshAction StartDashboardRefresh(DateTimeOffset nowUtc)
    {
        mDashboardRefreshInFlight = true;
        mDashboardRefreshPending = false;
        mImmediateDashboardRefreshPending = false;
        mLastDashboardRefreshStartedAtUtc = nowUtc;
        return TelemetryRefreshAction.StartDashboard;
    }
}
