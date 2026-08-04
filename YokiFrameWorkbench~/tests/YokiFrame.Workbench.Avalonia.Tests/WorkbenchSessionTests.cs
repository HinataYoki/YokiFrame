using YokiFrame.Workbench.Avalonia.Services;

namespace YokiFrame.Workbench.Avalonia.Tests;

/// <summary>
/// 覆盖 Workbench 会话的刷新代次和后台任务生命周期契约。
/// </summary>
public sealed class WorkbenchSessionTests
{
    /// <summary>
    /// 验证新的刷新代次会使旧代次失效，避免晚到结果覆盖当前状态。
    /// </summary>
    [Fact]
    public async Task NewRefreshVersionInvalidatesPreviousVersion()
    {
        await using WorkbenchSession session = new();

        long firstVersion = session.BeginRefresh();
        long secondVersion = session.BeginRefresh();

        Assert.False(session.IsCurrentRefresh(firstVersion));
        Assert.True(session.IsCurrentRefresh(secondVersion));
    }

    /// <summary>
    /// 验证释放会话会取消并等待已登记任务，而不是只取消调用方等待。
    /// </summary>
    [Fact]
    public async Task DisposeAsyncCancelsAndWaitsTrackedTask()
    {
        await using WorkbenchSession session = new();
        TaskCompletionSource<bool> taskStopped = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task trackedTask = WaitForCancellationAsync(session.LifetimeToken, taskStopped);
        session.Track(trackedTask);

        await session.DisposeAsync();

        Assert.True(taskStopped.Task.IsCompletedSuccessfully);
        Assert.True(session.IsDisposed);
        await trackedTask;
    }

    /// <summary>
    /// 验证会话关闭后不能再登记新的后台任务。
    /// </summary>
    [Fact]
    public async Task TrackAfterDisposeThrows()
    {
        await using WorkbenchSession session = new();
        await session.DisposeAsync();

        Assert.Throws<ObjectDisposedException>(() => session.Track(Task.CompletedTask));
        Assert.Throws<ObjectDisposedException>(() => session.BeginRefresh());
    }

    /// <summary>
    /// 等待会话生命周期取消，并确认任务已经完成收尾通知。
    /// </summary>
    /// <param name="cancellationToken">会话生命周期令牌。</param>
    /// <param name="taskStopped">任务停止信号。</param>
    /// <returns>异步等待操作。</returns>
    private static async Task WaitForCancellationAsync(
        CancellationToken cancellationToken,
        TaskCompletionSource<bool> taskStopped)
    {
        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            taskStopped.SetResult(true);
        }
    }
}
