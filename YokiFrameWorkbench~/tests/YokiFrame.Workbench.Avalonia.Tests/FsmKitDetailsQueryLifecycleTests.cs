using YokiFrame.Tooling.Application.Models.FsmKit;
using YokiFrame.Workbench.Avalonia.ViewModels;

namespace YokiFrame.Workbench.Avalonia.Tests;

/// <summary>覆盖 FsmKit 一次性详情查询的取消生命周期和 Telemetry 主来源所有权。</summary>
public sealed class FsmKitDetailsQueryLifecycleTests
{
    /// <summary>验证页面释放会取消未完成查询，且取消不会被显示为用户错误。</summary>
    [Fact]
    public async Task DisposeCancelsPendingDetailsQuery()
    {
        TaskCompletionSource queryStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken queryCancellationToken = default;
        FsmKitPageViewModel viewModel = new(async (_, cancellationToken) =>
        {
            queryCancellationToken = cancellationToken;
            queryStarted.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("unreachable");
        });
        viewModel.ApplyPeriodicState(CreateState("default-instance", "snapshot", "initial"));

        var queryTask = viewModel.QueryInstanceAsync("chosen-instance");
        await queryStarted.Task;
        viewModel.Dispose();
        await queryTask;

        Assert.True(queryCancellationToken.IsCancellationRequested);
        Assert.DoesNotContain("失败", viewModel.DiagnosticText, StringComparison.Ordinal);
    }

    /// <summary>验证命名 Telemetry 先取得详情后，稍晚的 FileBridge 初始化响应不会回退来源与图。</summary>
    [Fact]
    public async Task TelemetryDetailsWinOverLateCommandResponse()
    {
        TaskCompletionSource<WorkbenchFsmKitState> commandResponse = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource queryStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken queryCancellationToken = default;
        FsmKitPageViewModel viewModel = new(async (_, cancellationToken) =>
        {
            queryCancellationToken = cancellationToken;
            queryStarted.SetResult();
            return await commandResponse.Task.WaitAsync(cancellationToken);
        });
        viewModel.ApplyPeriodicState(CreateState("default-instance", "snapshot", "initial"));
        var queryTask = viewModel.QueryInstanceAsync("chosen-instance");
        await queryStarted.Task;

        viewModel.ApplyPeriodicState(CreateState("chosen-instance", "telemetry", "live"));
        commandResponse.SetResult(CreateState("chosen-instance", "command", "late-command"));
        await queryTask;

        Assert.True(queryCancellationToken.IsCancellationRequested);
        Assert.Equal("telemetry", viewModel.Source);
        Assert.Equal("{\"source\":\"live\"}", viewModel.RawPayload);
        Assert.Contains(viewModel.Transitions, transition => transition.To == "live");
    }

    /// <summary>创建同一宿主下指定实例和来源的强类型测试状态。</summary>
    /// <param name="selectedInstanceId">当前详情实例。</param>
    /// <param name="source">snapshot、telemetry 或 command。</param>
    /// <param name="marker">写入 payload 与转换目标的可识别标记。</param>
    /// <returns>可直接提交给页面的状态。</returns>
    private static WorkbenchFsmKitState CreateState(
        string selectedInstanceId,
        string source,
        string marker)
    {
        return FsmKitContractTestData.CreateState(
            selectedInstanceId,
            source,
            "{\"source\":\"" + marker + "\"}",
            "F:/Project/" + marker + ".json",
            marker);
    }
}
