using YokiFrame.Client.Telemetry.SharedMemory;
using YokiFrame.Protocol.Telemetry.SharedMemory;

namespace YokiFrame.Client.Tests.Telemetry.SharedMemory;

/// <summary>
/// 覆盖项目级 Shared Memory notification listener 的唤醒、取消和平台回落行为。
/// </summary>
public sealed class SharedMemoryTelemetryNotificationTests
{
    /// <summary>
    /// 验证同项目 publisher 发出信号后 listener 会立即返回。
    /// </summary>
    [Fact]
    public async Task ListenerWakesWhenPublisherSignals()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        string projectRoot = Path.Combine(
            Path.GetTempPath(),
            "YokiFrame-notification-" + Guid.NewGuid().ToString("N"));
        string scope = YokiFrameSharedMemoryTelemetryProjectScopeId.Compute(projectRoot);
        string name = YokiFrameSharedMemoryTelemetryNotificationName.Create(scope, "unity-editor");
        using EventWaitHandle publisher = new(false, EventResetMode.AutoReset, name, out _);
        Assert.True(
            SharedMemoryTelemetryNotificationListener.TryOpen(
                projectRoot,
                "unity-editor",
                out var listener,
                out var diagnostic),
            diagnostic);

        SharedMemoryTelemetryNotificationListener openedListener = listener
            ?? throw new InvalidOperationException("Notification listener was not opened.");
        using (openedListener)
        {
            Task<SharedMemoryTelemetryNotificationWaitResult> waitTask = Task.Run(
                () => openedListener.Wait(TimeSpan.FromSeconds(2), CancellationToken.None));
            publisher.Set();

            Assert.Equal(
                SharedMemoryTelemetryNotificationWaitResult.Signaled,
                await waitTask);
        }
    }

    /// <summary>
    /// 验证 Workbench 关闭令牌可以打断通知等待，不需要等待 watchdog。
    /// </summary>
    [Fact]
    public void ListenerWaitHonorsCancellation()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        string projectRoot = Path.Combine(
            Path.GetTempPath(),
            "YokiFrame-notification-cancel-" + Guid.NewGuid().ToString("N"));
        string scope = YokiFrameSharedMemoryTelemetryProjectScopeId.Compute(projectRoot);
        string name = YokiFrameSharedMemoryTelemetryNotificationName.Create(scope, "unity-editor");
        using EventWaitHandle publisher = new(false, EventResetMode.AutoReset, name, out _);
        Assert.True(
            SharedMemoryTelemetryNotificationListener.TryOpen(
                projectRoot,
                "unity-editor",
                out var listener,
                out var diagnostic),
            diagnostic);

        SharedMemoryTelemetryNotificationListener openedListener = listener
            ?? throw new InvalidOperationException("Notification listener was not opened.");
        using (openedListener)
        using (CancellationTokenSource cancellation = new())
        {
            cancellation.Cancel();
            Assert.Equal(
                SharedMemoryTelemetryNotificationWaitResult.Canceled,
                openedListener.Wait(TimeSpan.FromSeconds(2), cancellation.Token));
        }
    }
}
