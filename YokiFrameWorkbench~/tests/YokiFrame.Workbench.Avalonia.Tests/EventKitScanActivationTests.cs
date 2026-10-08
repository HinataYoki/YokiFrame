using YokiFrame.Tooling.Application.Models.EventKit.Scan;
using YokiFrame.Workbench.Avalonia.ViewModels;

namespace YokiFrame.Workbench.Avalonia.Tests;

/// <summary>覆盖 EventKit 自动扫描与页面激活状态之间的生命周期约束。</summary>
public sealed class EventKitScanActivationTests
{
    /// <summary>验证旧扫描晚完成时不会结束或覆盖切换范围后启动的新扫描。</summary>
    [Fact]
    public async Task SetPageActive_旧扫描晚完成_不覆盖新扫描()
    {
        TaskCompletionSource<WorkbenchEventKitCodeScan> firstScan = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<WorkbenchEventKitCodeScan> secondScan = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var invocationCount = 0;
        var firstCompleted = false;
        EventKitPageViewModel viewModel = CreateViewModel(async (_, _) =>
        {
            if (++invocationCount != 1)
            {
                return await secondScan.Task;
            }

            WorkbenchEventKitCodeScan result = await firstScan.Task;
            firstCompleted = true;
            return result;
        });
        viewModel.SetProjectRoot("C:/Project");

        viewModel.SetPageActive(true);
        viewModel.ExcludeEditor = false;
        Assert.Equal(2, invocationCount);

        firstScan.SetResult(CreateScan("OldEvent"));
        await WaitUntilAsync(() => firstCompleted);
        Assert.True(viewModel.IsScanning);
        Assert.Empty(viewModel.Events);

        secondScan.SetResult(CreateScan("LatestEvent"));
        await WaitUntilAsync(() => !viewModel.IsScanning);
        Assert.Equal("LatestEvent", Assert.Single(viewModel.Events).EventKey);
    }

    /// <summary>验证 Dashboard 在页面停留期间重复同步激活状态不会重复扫描。</summary>
    [Fact]
    public async Task SetPageActive_停留期间重复激活_只扫描一次()
    {
        var invocationCount = 0;
        EventKitPageViewModel viewModel = CreateViewModel((_, _) =>
        {
            invocationCount++;
            return Task.FromResult(CreateScan());
        });
        viewModel.SetProjectRoot("C:/Project");

        viewModel.SetPageActive(true);
        await WaitUntilAsync(() => !viewModel.IsScanning);
        viewModel.SetPageActive(true);

        Assert.Equal(1, invocationCount);
    }

    /// <summary>验证离开后重新进入页面会按最新源码重新执行扫描。</summary>
    [Fact]
    public async Task SetPageActive_离开后重新进入_重新扫描()
    {
        var invocationCount = 0;
        EventKitPageViewModel viewModel = CreateViewModel((_, _) =>
        {
            invocationCount++;
            return Task.FromResult(CreateScan());
        });
        viewModel.SetProjectRoot("C:/Project");

        viewModel.SetPageActive(true);
        await WaitUntilAsync(() => !viewModel.IsScanning);
        viewModel.SetPageActive(false);
        viewModel.SetPageActive(true);
        await WaitUntilAsync(() => !viewModel.IsScanning);

        Assert.Equal(2, invocationCount);
    }

    /// <summary>验证离开 EventKit 页面会立刻取消仍在运行的后台扫描。</summary>
    [Fact]
    public async Task SetPageActive_离开页面_取消运行中扫描()
    {
        CancellationToken capturedToken = default;
        EventKitPageViewModel viewModel = CreateViewModel(async (_, token) =>
        {
            capturedToken = token;
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return CreateScan();
        });
        viewModel.SetProjectRoot("C:/Project");
        viewModel.SetPageActive(true);
        await WaitUntilAsync(() => capturedToken.CanBeCanceled);

        viewModel.SetPageActive(false);

        Assert.True(capturedToken.IsCancellationRequested);
        Assert.False(viewModel.IsScanning);
    }

    /// <summary>验证页面先激活而项目根后就绪时仍会自动启动一次扫描。</summary>
    [Fact]
    public async Task SetProjectRoot_页面已激活_自动扫描一次()
    {
        var invocationCount = 0;
        EventKitPageViewModel viewModel = CreateViewModel((_, _) =>
        {
            invocationCount++;
            return Task.FromResult(CreateScan());
        });

        viewModel.SetPageActive(true);
        viewModel.SetProjectRoot("C:/Project");
        await WaitUntilAsync(() => !viewModel.IsScanning);

        Assert.Equal(1, invocationCount);
    }

    /// <summary>创建带可控扫描边界的真实页面 ViewModel。</summary>
    private static EventKitPageViewModel CreateViewModel(
        Func<bool, CancellationToken, Task<WorkbenchEventKitCodeScan>> scanAsync)
    {
        return new EventKitPageViewModel(scanAsync, null);
    }

    /// <summary>创建不含关系的最小合法扫描结果。</summary>
    private static WorkbenchEventKitCodeScan CreateScan()
    {
        return new WorkbenchEventKitCodeScan(
            "C:/Project",
            true,
            0,
            0,
            TimeSpan.Zero,
            Array.Empty<WorkbenchEventKitCodeRelation>());
    }

    /// <summary>创建包含指定 Type 事件的最小扫描结果。</summary>
    private static WorkbenchEventKitCodeScan CreateScan(string eventKey)
    {
        var relation = new WorkbenchEventKitCodeRelation(
            "Type",
            eventKey,
            eventKey,
            Array.Empty<WorkbenchEventKitCodeLocation>(),
            Array.Empty<WorkbenchEventKitCodeLocation>(),
            Array.Empty<WorkbenchEventKitCodeLocation>());
        return new WorkbenchEventKitCodeScan(
            "C:/Project",
            true,
            1,
            1,
            TimeSpan.Zero,
            new[] { relation });
    }

    /// <summary>等待异步状态满足条件，并在超时后给出明确断言失败。</summary>
    private static async Task WaitUntilAsync(Func<bool> predicate)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(2);
        while (!predicate() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }

        Assert.True(predicate());
    }
}
