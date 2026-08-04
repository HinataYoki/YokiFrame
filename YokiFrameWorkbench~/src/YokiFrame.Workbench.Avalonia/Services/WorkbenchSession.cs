namespace YokiFrame.Workbench.Avalonia.Services;

/// <summary>
/// 管理 Workbench 窗口范围内的取消、刷新代次和后台任务等待。
/// </summary>
public sealed class WorkbenchSession : IAsyncDisposable
{
    private readonly object mGate = new();
    private readonly CancellationTokenSource mLifetimeCancellation = new();
    private readonly HashSet<Task> mTrackedTasks = new();
    private long mRefreshVersion;
    private bool mDisposed;

    /// <summary>
    /// 获取当前 Workbench 会话的生命周期取消令牌。
    /// </summary>
    public CancellationToken LifetimeToken => mLifetimeCancellation.Token;

    /// <summary>
    /// 获取会话是否已经进入关闭状态。
    /// </summary>
    public bool IsDisposed
    {
        get
        {
            lock (mGate)
            {
                return mDisposed;
            }
        }
    }

    /// <summary>
    /// 开始一次新的刷新代次；旧代次完成后不得再提交 UI 状态。
    /// </summary>
    /// <returns>单调递增的刷新代次。</returns>
    public long BeginRefresh()
    {
        lock (mGate)
        {
            ThrowIfDisposed();
            return ++mRefreshVersion;
        }
    }

    /// <summary>
    /// 判断刷新代次是否仍是当前会话允许提交的代次。
    /// </summary>
    /// <param name="refreshVersion">后台操作捕获的刷新代次。</param>
    /// <returns>代次仍有效且会话未关闭时返回 true。</returns>
    public bool IsCurrentRefresh(long refreshVersion)
    {
        lock (mGate)
        {
            return !mDisposed && mRefreshVersion == refreshVersion;
        }
    }

    /// <summary>
    /// 请求会话内所有可取消工作停止，但保留等待和资源释放阶段。
    /// </summary>
    public void Cancel()
    {
        bool shouldCancel;
        lock (mGate)
        {
            shouldCancel = !mDisposed;
        }

        if (shouldCancel)
        {
            try
            {
                mLifetimeCancellation.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // 并发 Dispose 已完成取消和资源释放，调用方无需重复处理。
            }
        }
    }

    /// <summary>
    /// 登记一个必须在窗口释放前等待结束的后台任务。
    /// </summary>
    /// <param name="task">需要纳入会话生命周期的任务。</param>
    public void Track(Task task)
    {
        ArgumentNullException.ThrowIfNull(task);

        lock (mGate)
        {
            ThrowIfDisposed();
            mTrackedTasks.Add(task);
        }

        _ = RemoveCompletedTaskAsync(task);
    }

    /// <summary>
    /// 取消并等待所有已登记任务，再释放会话取消源。
    /// </summary>
    /// <returns>异步释放操作。</returns>
    public async ValueTask DisposeAsync()
    {
        Task[] tasks;
        lock (mGate)
        {
            if (mDisposed)
            {
                return;
            }

            mDisposed = true;
            mRefreshVersion++;
            tasks = mTrackedTasks.ToArray();
        }

        mLifetimeCancellation.Cancel();

        try
        {
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        finally
        {
            lock (mGate)
            {
                mTrackedTasks.Clear();
            }

            mLifetimeCancellation.Dispose();
        }
    }

    /// <summary>
    /// 等待任务结束并从会话登记表移除；此处观察异常避免形成未观察任务。
    /// </summary>
    /// <param name="task">需要观察的后台任务。</param>
    private async Task RemoveCompletedTaskAsync(Task task)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch (Exception)
        {
            // 原始任务仍由调用方或 DisposeAsync 观察；此 continuation 只负责登记表清理。
        }
        finally
        {
            lock (mGate)
            {
                mTrackedTasks.Remove(task);
            }
        }
    }

    /// <summary>
    /// 在会话已关闭时拒绝新增工作，避免后台任务逃逸生命周期。
    /// </summary>
    private void ThrowIfDisposed()
    {
        if (mDisposed)
        {
            throw new ObjectDisposedException(nameof(WorkbenchSession));
        }
    }
}
