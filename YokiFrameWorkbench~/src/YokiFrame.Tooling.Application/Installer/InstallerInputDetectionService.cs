namespace YokiFrame.Tooling.Application.Installer;

/// <summary>
/// 抽象 Installer 输入节流等待，使 Application 测试无需依赖真实时间。
/// </summary>
public interface IInstallerDetectionDelay
{
    /// <summary>
    /// 等待指定节流时间，并在输入被替代或调用方取消时停止。
    /// </summary>
    /// <param name="delay">节流时间。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task WaitAsync(TimeSpan delay, CancellationToken cancellationToken);
}

/// <summary>
/// 对连续 Installer 输入变化执行 latest-wins 节流检测。
/// </summary>
public sealed class InstallerInputDetectionService
{
    private readonly object mSyncRoot = new();
    private readonly TimeSpan mDelay;
    private readonly IInstallerDetectionDelay mDetectionDelay;
    private CancellationTokenSource? mPendingCancellation;

    /// <summary>
    /// 使用系统延迟创建输入检测服务。
    /// </summary>
    /// <param name="delay">输入稳定后开始检测前的等待时间。</param>
    public InstallerInputDetectionService(TimeSpan delay)
        : this(delay, SystemInstallerDetectionDelay.Instance)
    {
    }

    /// <summary>
    /// 使用可控延迟创建输入检测服务。
    /// </summary>
    /// <param name="delay">输入稳定后开始检测前的等待时间。</param>
    /// <param name="detectionDelay">可取消的等待实现。</param>
    public InstallerInputDetectionService(TimeSpan delay, IInstallerDetectionDelay detectionDelay)
    {
        if (delay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(delay));
        }

        mDelay = delay;
        mDetectionDelay = detectionDelay ?? throw new ArgumentNullException(nameof(detectionDelay));
    }

    /// <summary>
    /// 调度一次输入检测；新输入会取消并静默完成尚未越过节流窗口的旧调度。
    /// </summary>
    /// <param name="options">本次输入快照。</param>
    /// <param name="detectAsync">输入稳定后执行的检测函数。</param>
    /// <param name="cancellationToken">调用方取消令牌。</param>
    /// <returns>本次调度被替代或检测完成时结束的任务。</returns>
    public Task ScheduleAsync(
        InstallerInstallOptions options,
        Func<InstallerInstallOptions, CancellationToken, Task> detectAsync,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(detectAsync);
        CancellationTokenSource current = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        CancellationTokenSource? previous;
        lock (mSyncRoot)
        {
            previous = mPendingCancellation;
            mPendingCancellation = current;
            previous?.Cancel();
        }

        return RunScheduledAsync(options, detectAsync, current, cancellationToken);
    }

    /// <summary>
    /// 等待输入稳定并执行检测；仅吞掉被更新输入替代产生的内部取消。
    /// </summary>
    /// <param name="options">本次输入快照。</param>
    /// <param name="detectAsync">检测函数。</param>
    /// <param name="current">本次调度取消源。</param>
    /// <param name="callerToken">调用方取消令牌。</param>
    private async Task RunScheduledAsync(
        InstallerInstallOptions options,
        Func<InstallerInstallOptions, CancellationToken, Task> detectAsync,
        CancellationTokenSource current,
        CancellationToken callerToken)
    {
        try
        {
            await mDetectionDelay.WaitAsync(mDelay, current.Token).ConfigureAwait(false);
            await detectAsync(options, current.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!callerToken.IsCancellationRequested)
        {
        }
        finally
        {
            ReleaseSchedule(current);
        }
    }

    /// <summary>
    /// 仅清除仍属于当前服务的调度，并释放本次 linked token source。
    /// </summary>
    /// <param name="current">已完成调度的取消源。</param>
    private void ReleaseSchedule(CancellationTokenSource current)
    {
        lock (mSyncRoot)
        {
            if (ReferenceEquals(mPendingCancellation, current))
            {
                mPendingCancellation = null;
            }
        }

        current.Dispose();
    }

    /// <summary>
    /// 使用 Task.Delay 执行生产环境节流等待。
    /// </summary>
    private sealed class SystemInstallerDetectionDelay : IInstallerDetectionDelay
    {
        /// <summary>
        /// 获取无状态系统延迟共享实例。
        /// </summary>
        public static SystemInstallerDetectionDelay Instance { get; } = new();

        /// <summary>
        /// 等待指定时间并响应取消。
        /// </summary>
        /// <param name="delay">等待时间。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        public Task WaitAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            return Task.Delay(delay, cancellationToken);
        }
    }
}
