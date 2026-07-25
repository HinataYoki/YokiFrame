using YokiFrame.Workbench.Avalonia.Services;
using YokiFrame.Workbench.Avalonia.ViewModels;

namespace YokiFrame.Workbench.Avalonia.Tests;

/// <summary>
/// 覆盖 Workbench Runtime 后台更新检测与窗口关闭取消契约。
/// </summary>
public sealed class WorkbenchRuntimeUpdateViewModelTests
{
    /// <summary>
    /// 验证构建入口明确表示检测到可构建新版，避免误导为无条件重新编译。
    /// </summary>
    [Fact]
    public void ButtonTextDescribesAvailableNewVersion()
    {
        using WorkbenchRuntimeUpdateViewModel viewModel = new(
            Environment.CurrentDirectory,
            Environment.CurrentDirectory,
            new PendingUpdateService());

        Assert.Equal("有新版可编译", viewModel.ButtonText);
    }

    /// <summary>
    /// 验证 Workbench 关闭时取消正在运行的后台检测，且取消结果不再更新 UI 状态。
    /// </summary>
    [Fact]
    public async Task DisposeCancelsInFlightUpdateCheck()
    {
        PendingUpdateService service = new();
        using WorkbenchRuntimeUpdateViewModel viewModel = new(
            Environment.CurrentDirectory,
            Environment.CurrentDirectory,
            service);

        var checkTask = viewModel.StartCheckAsync();
        await service.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        viewModel.Dispose();
        await checkTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(service.CancellationObserved);
        Assert.False(viewModel.IsVisible);
        Assert.False(viewModel.IsStatusVisible);
    }

    /// <summary>
    /// 提供等待取消的可控更新服务，用于观察窗口生命周期令牌。
    /// </summary>
    private sealed class PendingUpdateService : IWorkbenchRuntimeUpdateService
    {
        /// <summary>获取检测已开始的同步信号。</summary>
        internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>获取是否已观察到生命周期取消。</summary>
        internal bool CancellationObserved { get; private set; }

        /// <summary>
        /// 持续等待直到 ViewModel 释放并取消令牌。
        /// </summary>
        public async Task<WorkbenchRuntimeUpdateCheck> CheckAsync(
            string sourcePackageRoot,
            string projectRoot,
            string runningFingerprint,
            CancellationToken cancellationToken)
        {
            Started.TrySetResult();
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                CancellationObserved = true;
                throw;
            }

            return new WorkbenchRuntimeUpdateCheck(string.Empty, false);
        }

        /// <summary>
        /// 当前测试不触发构建，调用时显式失败。
        /// </summary>
        public Task<string> RebuildAsync(
            string sourcePackageRoot,
            string projectRoot,
            CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Rebuild is not expected in this test.");
        }
    }
}
