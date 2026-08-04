using YokiFrame.Workbench.Avalonia.Services;

namespace YokiFrame.Workbench.Avalonia.Tests;

/// <summary>
/// 验证 telemetry 通知与低频 Dashboard 刷新之间的合并和节流边界。
/// </summary>
public sealed class TelemetryRefreshPolicyTests
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(1);
    private static readonly DateTimeOffset StartTime = new(2026, 8, 3, 0, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// 验证 telemetry 通知只唤醒高频读取循环，不会启动完整 Dashboard 读取。
    /// </summary>
    [Fact]
    public void TelemetryNotificationDoesNotStartDashboardRefresh()
    {
        TelemetryRefreshPolicy policy = new(RefreshInterval);

        TelemetryRefreshAction action = policy.Request(
            TelemetryRefreshTrigger.TelemetryNotification,
            StartTime);

        Assert.Equal(TelemetryRefreshAction.SignalTelemetry, action);
        Assert.Equal(
            TelemetryRefreshAction.None,
            policy.CompleteDashboardRefresh(StartTime));
    }

    /// <summary>
    /// 验证尚未消费的多个 telemetry 通知只产生一个待消费信号。
    /// </summary>
    [Fact]
    public void TelemetryNotificationsAreCoalescedUntilConsumed()
    {
        TelemetryRefreshPolicy policy = new(RefreshInterval);

        Assert.Equal(
            TelemetryRefreshAction.SignalTelemetry,
            policy.Request(TelemetryRefreshTrigger.TelemetryNotification, StartTime));
        Assert.Equal(
            TelemetryRefreshAction.None,
            policy.Request(TelemetryRefreshTrigger.TelemetryNotification, StartTime.AddMilliseconds(10)));

        Assert.Equal(
            TelemetryRefreshAction.SignalTelemetry,
            policy.MarkTelemetrySignalConsumed());

        // 模拟轮询循环重新等待并消费了竞态窗口中保留的下一代信号。
        Assert.Equal(
            TelemetryRefreshAction.None,
            policy.MarkTelemetrySignalConsumed());

        Assert.Equal(
            TelemetryRefreshAction.SignalTelemetry,
            policy.Request(TelemetryRefreshTrigger.TelemetryNotification, StartTime.AddMilliseconds(20)));
    }

    /// <summary>
    /// 验证低频 Dashboard 请求按最小间隔节流，而显式请求仍能立即执行。
    /// </summary>
    [Fact]
    public void ExplicitRefreshBypassesLowFrequencyThrottle()
    {
        TelemetryRefreshPolicy policy = new(RefreshInterval);

        Assert.Equal(
            TelemetryRefreshAction.StartDashboard,
            policy.Request(TelemetryRefreshTrigger.ExplicitDashboard, StartTime));
        Assert.Equal(
            TelemetryRefreshAction.None,
            policy.CompleteDashboardRefresh(StartTime));

        Assert.Equal(
            TelemetryRefreshAction.None,
            policy.Request(TelemetryRefreshTrigger.LowFrequencyDashboard, StartTime.AddMilliseconds(500)));
        Assert.Equal(
            TelemetryRefreshAction.StartDashboard,
            policy.Request(TelemetryRefreshTrigger.ExplicitDashboard, StartTime.AddMilliseconds(500)));
    }

    /// <summary>
    /// 验证生命周期请求在低频读取受限时仍会挂起并在当前读取完成后启动。
    /// </summary>
    [Fact]
    public void EngineLifecycleRefreshIsNotDroppedDuringInFlightRead()
    {
        TelemetryRefreshPolicy policy = new(RefreshInterval);

        Assert.Equal(
            TelemetryRefreshAction.StartDashboard,
            policy.Request(TelemetryRefreshTrigger.ExplicitDashboard, StartTime));
        Assert.Equal(
            TelemetryRefreshAction.None,
            policy.Request(TelemetryRefreshTrigger.EngineLifecycle, StartTime.AddMilliseconds(100)));
        Assert.Equal(
            TelemetryRefreshAction.StartDashboard,
            policy.CompleteDashboardRefresh(StartTime.AddMilliseconds(100)));
    }

    /// <summary>
    /// 验证低频请求在读取完成过早时保留挂起标记，并在下一个到期 tick 启动。
    /// </summary>
    [Fact]
    public void PendingLowFrequencyRefreshStartsWhenNextIntervalIsDue()
    {
        TelemetryRefreshPolicy policy = new(RefreshInterval);

        Assert.Equal(
            TelemetryRefreshAction.StartDashboard,
            policy.Request(TelemetryRefreshTrigger.LowFrequencyDashboard, StartTime));
        Assert.Equal(
            TelemetryRefreshAction.None,
            policy.Request(TelemetryRefreshTrigger.LowFrequencyDashboard, StartTime.AddMilliseconds(100)));
        Assert.Equal(
            TelemetryRefreshAction.None,
            policy.CompleteDashboardRefresh(StartTime.AddMilliseconds(100)));
        Assert.Equal(
            TelemetryRefreshAction.StartDashboard,
            policy.Request(TelemetryRefreshTrigger.LowFrequencyDashboard, StartTime.AddSeconds(1)));
    }
}
