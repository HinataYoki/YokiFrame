using YokiFrame.Tooling.Application.Models.FsmKit;
using YokiFrame.Workbench.Avalonia.ViewModels;

namespace YokiFrame.Workbench.Avalonia.Tests;

/// <summary>
/// 覆盖 FsmKit 页面状态投影、选择查询和宿主身份隔离契约。
/// </summary>
public sealed class FsmKitPageViewModelContractTests
{
    /// <summary>
    /// 验证显式 command 详情不会被宿主下一帧默认选择的周期 snapshot 覆盖。
    /// </summary>
    [Fact]
    public async Task ApplyPeriodicState_已有显式详情_默认周期选择不覆盖()
    {
        var periodic = FsmKitContractTestData.CreateState(
            "default-instance",
            "snapshot",
            "{\"source\":\"periodic\"}",
            "F:/Project/default.json",
            "DefaultState");
        var explicitDetails = FsmKitContractTestData.CreateState(
            "chosen-instance",
            "command",
            "{\"source\":\"command\"}",
            "F:/Project/chosen-response.json",
            "ChosenState");
        using FsmKitPageViewModel viewModel = new((_, _) => Task.FromResult(explicitDetails));

        viewModel.ApplyPeriodicState(periodic);
        await viewModel.QueryInstanceAsync("chosen-instance");
        viewModel.ApplyPeriodicState(periodic);

        Assert.Equal("chosen-instance", viewModel.SelectedInstanceId);
        Assert.Equal("command", viewModel.Source);
        Assert.Equal("{\"source\":\"command\"}", viewModel.RawPayload);
        Assert.Contains("F:/Project/chosen-response.json", viewModel.EvidencePaths);
        Assert.Contains(viewModel.Transitions, transition => transition.To == "ChosenState");
    }

    /// <summary>
    /// 验证用户切换 instance 后立即清空上一实例详情，查询完成前不会混看旧证据。
    /// 未完成查询随页面释放取消，不在断言后手动完成挂起任务。
    /// </summary>
    [Fact]
    public async Task SelectedMachine_切换实例_查询完成前清空旧详情()
    {
        var periodic = FsmKitContractTestData.CreateState(
            "default-instance",
            "snapshot",
            "{\"source\":\"periodic\"}",
            "F:/Project/default.json",
            "DefaultState");
        var explicitDetails = FsmKitContractTestData.CreateState(
            "chosen-instance",
            "command",
            "{\"source\":\"command\"}",
            "F:/Project/chosen-response.json",
            "ChosenState");
        Task<WorkbenchFsmKitState> pending = new TaskCompletionSource<WorkbenchFsmKitState>(
            TaskCreationOptions.RunContinuationsAsynchronously).Task;
        var queriedInstanceId = string.Empty;
        using FsmKitPageViewModel viewModel = new((instanceId, cancellationToken) =>
        {
            queriedInstanceId = instanceId;
            return instanceId == "chosen-instance"
                ? Task.FromResult(explicitDetails)
                : pending.WaitAsync(cancellationToken);
        });

        viewModel.ApplyPeriodicState(periodic);
        await viewModel.QueryInstanceAsync("chosen-instance");
        viewModel.SelectedMachine = viewModel.Machines.Single(
            machine => machine.InstanceId == "default-instance");

        Assert.Equal("default-instance", queriedInstanceId);
        Assert.Empty(viewModel.StateTree);
        Assert.Empty(viewModel.Transitions);
        Assert.Empty(viewModel.StateEvents);
        Assert.Equal(string.Empty, viewModel.RawPayload);
        Assert.Empty(viewModel.EvidencePaths);
        Assert.Contains("default-instance", viewModel.DiagnosticText);
    }

    /// <summary>
    /// 验证切换到无 FsmKit 数据的 engine 时不会残留上一宿主身份和证据。
    /// </summary>
    [Fact]
    public void ApplyPeriodicState_空状态_清空宿主与证据()
    {
        using FsmKitPageViewModel viewModel = new();
        viewModel.ApplyPeriodicState(FsmKitContractTestData.CreateState(
            "default-instance",
            "telemetry",
            "{\"active\":true}",
            "YokiFrame.Telemetry.unity-editor.FsmKit.state.v1",
            "Running"));

        viewModel.ApplyPeriodicState(null);

        Assert.Equal("未选择", viewModel.EngineId);
        Assert.Equal("未知", viewModel.SessionId);
        Assert.Equal(0, viewModel.Generation);
        Assert.Equal("等待数据", viewModel.Source);
        Assert.Equal(string.Empty, viewModel.RawPayload);
        Assert.Empty(viewModel.EvidencePaths);
        Assert.Equal(string.Empty, viewModel.SelectedInstanceId);
    }

    /// <summary>
    /// 验证已读取但没有注册 FSM 的快照会显示可行动的空状态，而不是留下空白工作区。
    /// </summary>
    [Fact]
    public void ApplyPeriodicState_无注册状态机_显示可行动空状态()
    {
        using FsmKitPageViewModel viewModel = new();

        viewModel.ApplyPeriodicState(FsmKitContractTestData.CreateEmptyState());

        Assert.False(viewModel.HasMachines);
        Assert.True(viewModel.IsEmptyWorkspaceVisible);
        Assert.Equal("未发现活动状态机", viewModel.EmptyStateTitle);
        Assert.Contains("FsmKit", viewModel.EmptyStateDescription);
        Assert.Empty(viewModel.GraphModel.Nodes);
        Assert.Empty(viewModel.GraphModel.Edges);
    }

    /// <summary>
    /// 验证真实状态和历史会投影为稳定的观测转换图节点与边，不让 View 自行推导业务数据。
    /// </summary>
    [Fact]
    public void ApplyPeriodicState_有状态和历史_投影观测图()
    {
        using FsmKitPageViewModel viewModel = new();

        viewModel.ApplyPeriodicState(FsmKitContractTestData.CreateState(
            "chosen-instance",
            "snapshot",
            "{\"active\":true}",
            "F:/Project/fsm-state.json",
            "Ready"));

        Assert.True(viewModel.HasMachines);
        Assert.False(viewModel.IsEmptyWorkspaceVisible);
        Assert.Equal("ChosenFSM", viewModel.SelectedMachineName);
        Assert.Equal("文件 Snapshot", viewModel.DataChannelText);
        Assert.Contains(viewModel.GraphModel.Nodes, node => node.Name == "Start");
        Assert.Contains(viewModel.GraphModel.Nodes, node => node.Name == "ChosenState" && node.IsCurrent);
        Assert.Contains(
            viewModel.GraphModel.Edges,
            edge => edge.From == "Start" && edge.To == "Ready" && edge.Count == 1);
    }

    /// <summary>
    /// 验证切换宿主后，旧 session 尚未完成的详情响应不会覆盖新宿主页面。
    /// </summary>
    [Fact]
    public async Task QueryInstanceAsync_宿主已切换_忽略旧详情响应()
    {
        TaskCompletionSource<WorkbenchFsmKitState> response = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using FsmKitPageViewModel viewModel = new((_, _) => response.Task);
        var oldHost = FsmKitContractTestData.CreateState(
            "default-instance", "snapshot", "{\"host\":\"unity\"}", "F:/Project/unity.json", "UnityState");
        var newHost = FsmKitContractTestData.CreateState(
            "default-instance",
            "snapshot",
            "{\"host\":\"godot\"}",
            "F:/Project/godot.json",
            "GodotState",
            "godot-editor",
            "godot-session",
            12L);

        viewModel.ApplyPeriodicState(oldHost);
        var oldItems = viewModel.Machines.ToArray();
        var queryTask = viewModel.QueryInstanceAsync("chosen-instance");
        viewModel.ApplyPeriodicState(newHost);
        response.SetResult(FsmKitContractTestData.CreateState(
            "chosen-instance",
            "command",
            "{\"host\":\"unity-command\"}",
            "F:/Project/unity-response.json",
            "ChosenState"));
        await queryTask;

        Assert.Equal("godot-editor", viewModel.EngineId);
        Assert.Equal("godot-session", viewModel.SessionId);
        Assert.Equal(12L, viewModel.Generation);
        Assert.Equal("{\"host\":\"godot\"}", viewModel.RawPayload);
        Assert.DoesNotContain(viewModel.Machines, item => oldItems.Contains(item));
    }

    /// <summary>
    /// 验证实例身份变化会发起一次强类型详情查询，而不是只切换本地摘要。
    /// </summary>
    [Fact]
    public async Task QueryInstanceAsync_指定实例_按精确instanceId查询并报告失败()
    {
        var requestedInstanceId = string.Empty;
        Func<string, CancellationToken, Task<WorkbenchFsmKitState>> query = (instanceId, _) =>
        {
            requestedInstanceId = instanceId;
            return Task.FromException<WorkbenchFsmKitState>(new InvalidOperationException("query failed"));
        };
        using FsmKitPageViewModel viewModel = new(query);

        await viewModel.QueryInstanceAsync("fsm-instance-42");

        Assert.Equal("fsm-instance-42", requestedInstanceId);
        Assert.Equal("fsm-instance-42", viewModel.SelectedInstanceId);
        Assert.Contains("query failed", viewModel.DiagnosticText, StringComparison.Ordinal);
    }
}
