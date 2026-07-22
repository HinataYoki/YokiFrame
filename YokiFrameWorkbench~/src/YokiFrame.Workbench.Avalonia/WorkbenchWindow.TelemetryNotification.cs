using YokiFrame.Client.Telemetry.SharedMemory;
using YokiFrame.Protocol.FileBridge;
using YokiFrame.Tooling.Application.Models;
using YokiFrame.Workbench.Avalonia.Diagnostics;

namespace YokiFrame.Workbench.Avalonia;

/// <summary>
/// 承载 Workbench 项目级 Shared Memory Telemetry 变化通知。
/// </summary>
public sealed partial class WorkbenchWindow
{
    private static readonly TimeSpan TelemetryNotificationWatchdogInterval = TimeSpan.FromSeconds(1);
    private readonly SemaphoreSlim mTelemetryRefreshSignal = new(0, 1);
    private CancellationTokenSource? mTelemetryNotificationCancellation;
    private Task? mTelemetryNotificationTask;
    private SharedMemoryTelemetryNotificationListener? mTelemetryNotificationListener;
    private string mTelemetryNotificationEngineId = string.Empty;

    /// <summary>
    /// 根据当前 registry 身份建立或清理项目级通知 listener。
    /// </summary>
    /// <param name="state">最近一次 dashboard 状态。</param>
    private void UpdateTelemetryNotificationMode(WorkbenchDashboardState state)
    {
        EngineRegistryEntry? registry = state.Engines.FirstOrDefault(
            entry => string.Equals(entry.EngineId, state.SelectedEngineId, StringComparison.Ordinal));
        bool canListen = state.BridgeHealth.State == WorkbenchBridgeConnectionState.Online
            && state.BridgeHealth.Generation > 0L
            && registry != null
            && registry.Capabilities.Contains("telemetry.notify", StringComparer.OrdinalIgnoreCase);
        if (!canListen)
        {
            StopTelemetryNotificationListener();
            return;
        }

        if (mTelemetryNotificationListener != null
            && string.Equals(mTelemetryNotificationEngineId, state.SelectedEngineId, StringComparison.Ordinal))
        {
            return;
        }

        StopTelemetryNotificationListener();
        SharedMemoryTelemetryNotificationListener? listener =
            mDashboardService.CreateTelemetryNotificationListener(state.SelectedEngineId);
        if (listener == null)
        {
            return;
        }

        CancellationTokenSource cancellation = new();
        mTelemetryNotificationEngineId = state.SelectedEngineId;
        mTelemetryNotificationListener = listener;
        mTelemetryNotificationCancellation = cancellation;
        mTelemetryNotificationTask = ObserveTelemetryNotificationsAsync(listener, cancellation.Token);
    }

    /// <summary>
    /// 等待当前项目的变化信号，并同时触发 dashboard 与高频页面读取。
    /// </summary>
    /// <param name="listener">当前项目唯一通知 listener。</param>
    /// <param name="cancellationToken">Workbench 生命周期取消令牌。</param>
    private async Task ObserveTelemetryNotificationsAsync(
        SharedMemoryTelemetryNotificationListener listener,
        CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                SharedMemoryTelemetryNotificationWaitResult result = await Task.Run(
                    () => listener.Wait(TelemetryNotificationWatchdogInterval, cancellationToken),
                    cancellationToken).ConfigureAwait(false);
                if (result == SharedMemoryTelemetryNotificationWaitResult.Canceled)
                {
                    return;
                }

                if (result == SharedMemoryTelemetryNotificationWaitResult.Signaled)
                {
                    SignalTelemetryRefresh();
                    global::Avalonia.Threading.Dispatcher.UIThread.Post(QueueDashboardRefresh);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            WorkbenchStartupTrace.Mark("telemetry.notification.cancelled");
        }
        catch (Exception exception)
        {
            WorkbenchStartupTrace.Mark("telemetry.notification.failed." + exception.GetType().Name);
            global::Avalonia.Threading.Dispatcher.UIThread.Post(
                () => DisableTelemetryNotificationListener(listener));
        }
    }

    /// <summary>
    /// 合并多个快速通知，避免同一 Editor update 产生无界的等待信号。
    /// </summary>
    private void SignalTelemetryRefresh()
    {
        try
        {
            if (mTelemetryRefreshSignal.CurrentCount == 0)
            {
                mTelemetryRefreshSignal.Release();
            }
        }
        catch (SemaphoreFullException)
        {
            WorkbenchStartupTrace.Mark("telemetry.notification.signal.coalesced");
        }
        catch (ObjectDisposedException)
        {
            WorkbenchStartupTrace.Mark("telemetry.notification.signal.disposed");
        }
    }

    /// <summary>
    /// 通知任务异常退出时清除失效 listener，让下一个 watchdog 回到周期读取。
    /// </summary>
    /// <param name="listener">发生异常的通知 listener。</param>
    private void DisableTelemetryNotificationListener(
        SharedMemoryTelemetryNotificationListener listener)
    {
        if (ReferenceEquals(mTelemetryNotificationListener, listener))
        {
            StopTelemetryNotificationListener();
        }
    }

    /// <summary>
    /// 停止项目级通知 reader；下一次 dashboard 会按新身份重新打开。
    /// </summary>
    private void StopTelemetryNotificationListener()
    {
        CancellationTokenSource? cancellation = mTelemetryNotificationCancellation;
        Task? task = mTelemetryNotificationTask;
        SharedMemoryTelemetryNotificationListener? listener = mTelemetryNotificationListener;
        mTelemetryNotificationCancellation = null;
        mTelemetryNotificationTask = null;
        mTelemetryNotificationListener = null;
        mTelemetryNotificationEngineId = string.Empty;
        cancellation?.Cancel();
        if (cancellation != null && task != null)
        {
            _ = ObserveTelemetryNotificationShutdownAsync(task, cancellation, listener);
        }
        else
        {
            listener?.Dispose();
            cancellation?.Dispose();
        }
    }

    /// <summary>
    /// 等待通知任务真正退出后释放其 listener 和取消源。
    /// </summary>
    private static async Task ObserveTelemetryNotificationShutdownAsync(
        Task task,
        CancellationTokenSource cancellation,
        SharedMemoryTelemetryNotificationListener? listener)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            WorkbenchStartupTrace.Mark("telemetry.notification.stop-failed." + exception.GetType().Name);
        }
        finally
        {
            listener?.Dispose();
            cancellation.Dispose();
        }
    }
}
