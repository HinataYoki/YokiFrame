using YokiFrame.Tooling.Application.Installer;

namespace YokiFrame.Tooling.Application.Tests;

/// <summary>
/// 覆盖 Installer 输入变化后的独立节流检测服务。
/// </summary>
public sealed class InstallerInputDetectionServiceTests
{
    /// <summary>
    /// 验证连续输入只检测节流窗口内最后一次选项。
    /// </summary>
    [Fact]
    public async Task ScheduleAsyncDetectsOnlyLatestInput()
    {
        var delay = new ControlledDetectionDelay();
        var service = new InstallerInputDetectionService(TimeSpan.FromMilliseconds(350), delay);
        List<InstallerInstallOptions> detectedOptions = new();
        var firstOptions = CreateOptions("C:/projects/First");
        var latestOptions = CreateOptions("C:/projects/Latest");

        var firstSchedule = service.ScheduleAsync(firstOptions, DetectAsync);
        await delay.WaitForCountAsync(1);
        var latestSchedule = service.ScheduleAsync(latestOptions, DetectAsync);
        await delay.WaitForCountAsync(2);
        delay.ReleaseLatest();

        await Task.WhenAll(firstSchedule, latestSchedule);
        Assert.Single(detectedOptions);
        Assert.Same(latestOptions, detectedOptions[0]);

        /// <summary>
        /// 记录真正越过节流窗口的检测输入。
        /// </summary>
        Task DetectAsync(InstallerInstallOptions options, CancellationToken cancellationToken)
        {
            detectedOptions.Add(options);
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// 创建指定目标路径的 Unity 本地安装选项。
    /// </summary>
    /// <param name="targetRoot">目标项目根。</param>
    /// <returns>安装选项。</returns>
    private static InstallerInstallOptions CreateOptions(string targetRoot)
    {
        return InstallerInstallOptions.CreateUnityLocal(
            "C:/packages/YokiFrame",
            targetRoot,
            InstallerLegacyPackagePolicy.Reject);
    }

    /// <summary>
    /// 提供可观测、可释放且响应取消的节流延迟。
    /// </summary>
    private sealed class ControlledDetectionDelay : IInstallerDetectionDelay
    {
        private readonly List<TaskCompletionSource> mWaits = new();

        /// <summary>
        /// 创建一次受控等待，并在被新输入取代时响应取消。
        /// </summary>
        public Task WaitAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (mWaits)
            {
                mWaits.Add(completion);
            }

            cancellationToken.Register(static state => ((TaskCompletionSource)state!).TrySetCanceled(), completion);
            return completion.Task;
        }

        /// <summary>
        /// 等待指定数量的节流窗口被创建。
        /// </summary>
        /// <param name="expectedCount">期望窗口数量。</param>
        public async Task WaitForCountAsync(int expectedCount)
        {
            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(2));
            while (!timeout.IsCancellationRequested)
            {
                lock (mWaits)
                {
                    if (mWaits.Count >= expectedCount)
                    {
                        return;
                    }
                }

                await Task.Delay(10, timeout.Token);
            }

            throw new TimeoutException("等待 Installer 节流窗口超时。");
        }

        /// <summary>
        /// 释放最后一次输入对应的节流窗口。
        /// </summary>
        public void ReleaseLatest()
        {
            TaskCompletionSource completion;
            lock (mWaits)
            {
                completion = mWaits[^1];
            }

            completion.TrySetResult();
        }
    }
}
