using YokiFrame.Protocol.Telemetry.SharedMemory;

namespace YokiFrame.Client.Telemetry.SharedMemory;

/// <summary>
/// 描述 Shared Memory Telemetry 通知等待的结果。
/// </summary>
public enum SharedMemoryTelemetryNotificationWaitResult
{
    Signaled,
    TimedOut,
    Canceled
}

/// <summary>
/// 通过项目级 Windows Named Event 等待 Shared Memory 最新帧变化。
/// </summary>
public sealed class SharedMemoryTelemetryNotificationListener : IDisposable
{
    private EventWaitHandle? mSignal;

    private SharedMemoryTelemetryNotificationListener(EventWaitHandle signal, string name)
    {
        mSignal = signal;
        Name = name;
    }

    /// <summary>
    /// 获取当前项目和 engine 对应的系统通知名称。
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 尝试打开宿主已经创建的项目级通知事件。
    /// </summary>
    /// <param name="projectRoot">当前项目根目录。</param>
    /// <param name="engineId">当前选中的 engine 标识。</param>
    /// <param name="listener">成功打开的 listener；不可用时为空。</param>
    /// <param name="diagnostic">失败原因，供调用方记录或降级。</param>
    /// <returns>事件存在且当前平台支持时返回 true。</returns>
    public static bool TryOpen(
        string projectRoot,
        string engineId,
        out SharedMemoryTelemetryNotificationListener? listener,
        out string diagnostic)
    {
        listener = null;
        diagnostic = string.Empty;
        if (!OperatingSystem.IsWindows())
        {
            diagnostic = "Shared Memory telemetry notifications are only enabled on Windows.";
            return false;
        }

        try
        {
            string projectScopeId = YokiFrameSharedMemoryTelemetryProjectScopeId.Compute(projectRoot);
            string name = YokiFrameSharedMemoryTelemetryNotificationName.Create(projectScopeId, engineId);
            listener = new SharedMemoryTelemetryNotificationListener(
                EventWaitHandle.OpenExisting(name),
                name);
            return true;
        }
        catch (WaitHandleCannotBeOpenedException)
        {
            diagnostic = "The telemetry notification event is not available yet.";
            return false;
        }
        catch (UnauthorizedAccessException exception)
        {
            diagnostic = "The telemetry notification event cannot be opened: " + exception.Message;
            return false;
        }
        catch (PlatformNotSupportedException exception)
        {
            diagnostic = "The telemetry notification event is unsupported: " + exception.Message;
            return false;
        }
        catch (ArgumentException exception)
        {
            diagnostic = "The telemetry notification name is invalid: " + exception.Message;
            return false;
        }
    }

    /// <summary>
    /// 等待宿主发出变化信号；超时只表示 watchdog 到期，不代表数据失败。
    /// </summary>
    /// <param name="timeout">最长等待时长。</param>
    /// <param name="cancellationToken">Workbench 生命周期取消令牌。</param>
    /// <returns>信号、超时或取消结果。</returns>
    public SharedMemoryTelemetryNotificationWaitResult Wait(
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        EventWaitHandle signal = mSignal
            ?? throw new ObjectDisposedException(nameof(SharedMemoryTelemetryNotificationListener));
        if (cancellationToken.IsCancellationRequested)
        {
            return SharedMemoryTelemetryNotificationWaitResult.Canceled;
        }

        WaitHandle[] handles = { signal, cancellationToken.WaitHandle };
        int index = WaitHandle.WaitAny(handles, timeout);
        return index switch
        {
            0 => SharedMemoryTelemetryNotificationWaitResult.Signaled,
            1 => SharedMemoryTelemetryNotificationWaitResult.Canceled,
            WaitHandle.WaitTimeout => SharedMemoryTelemetryNotificationWaitResult.TimedOut,
            _ => SharedMemoryTelemetryNotificationWaitResult.TimedOut
        };
    }

    /// <summary>
    /// 释放当前项目的系统通知句柄。
    /// </summary>
    public void Dispose()
    {
        Interlocked.Exchange(ref mSignal, null)?.Dispose();
    }
}
