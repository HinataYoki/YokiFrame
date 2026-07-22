using YokiFrame.Tooling.Application.Models.FsmKit;
using YokiFrame.Workbench.Avalonia.ViewModels;
using YokiFrame.Workbench.Avalonia.ViewModels.FsmKit;

namespace YokiFrame.Workbench.Avalonia.Tests;

/// <summary>
/// 覆盖普通 FSM 嵌套另一个 FSM 时，同名状态节点的身份与历史端点映射。
/// </summary>
public sealed class FsmKitNestedGraphIdentityTests
{
    /// <summary>
    /// 验证父子机器的 Idle/Running 保持为独立节点，根实例历史只连接根机器同名状态。
    /// </summary>
    [Fact]
    public void SameNamedParentAndChildStatesKeepIndependentGraphIdentity()
    {
        var state = CreateNestedState();
        FsmKitPageViewModel viewModel = new();

        viewModel.ApplyPeriodicState(state);

        Assert.Equal(5, viewModel.GraphModel.Nodes.Count);
        var rootIdle = Assert.Single(
            viewModel.GraphModel.Nodes,
            static node => node.Name == "Idle" && !node.IsCurrent);
        var childIdle = Assert.Single(
            viewModel.GraphModel.Nodes,
            static node => node.Name == "Idle" && node.IsCurrent);
        var rootRunning = Assert.Single(
            viewModel.GraphModel.Nodes,
            static node => node.Name == "Running" && node.IsCurrent);
        var childRunning = Assert.Single(
            viewModel.GraphModel.Nodes,
            static node => node.Name == "Running" && !node.IsCurrent);
        var edge = Assert.Single(viewModel.GraphModel.Edges);

        Assert.Equal(1L, rootIdle.EntryCount);
        Assert.Equal(0L, childIdle.EntryCount);
        Assert.Equal(2L, rootRunning.EntryCount);
        Assert.Equal(0L, childRunning.EntryCount);
        Assert.True(Distance(edge.StartX, edge.StartY, rootIdle) < Distance(edge.StartX, edge.StartY, childIdle));
        Assert.True(Distance(edge.EndX, edge.EndY, rootRunning) < Distance(edge.EndX, edge.EndY, childRunning));
    }

    /// <summary>
    /// 创建根机器及其复合子机器均含 Idle/Running 的强类型状态，并保留根机器转换历史。
    /// </summary>
    /// <returns>可直接应用到页面 ViewModel 的嵌套 FSM 状态。</returns>
    private static WorkbenchFsmKitState CreateNestedState()
    {
        var childStates = new[]
        {
            CreateNode(0, 0, "Idle", true, false, 0L, Array.Empty<WorkbenchFsmStateNode>()),
            CreateNode(1, 1, "Running", false, false, 0L, Array.Empty<WorkbenchFsmStateNode>())
        };
        var rootStates = new[]
        {
            CreateNode(0, 0, "Idle", false, false, 1L, Array.Empty<WorkbenchFsmStateNode>()),
            CreateNode(1, 1, "Running", true, false, 2L, Array.Empty<WorkbenchFsmStateNode>()),
            CreateNode(2, 2, "Nested", false, true, 0L, childStates)
        };
        var baseline = FsmKitContractTestData.CreateState(
            "chosen-instance",
            "snapshot",
            "{}",
            "test://nested-fsm-identity",
            "Running",
            transitions: new[] { ("Idle", "Running") });
        var selected = CreateInternal<WorkbenchFsmMachineDetails>(
            "ChosenFSM", "chosen-instance", "Running", "Running", 1, 5, rootStates);
        return CreateInternal<WorkbenchFsmKitState>(
            baseline.DataSource,
            baseline.FsmName,
            baseline.InstanceId,
            baseline.DeclaredCount,
            baseline.Machines,
            selected,
            baseline.History,
            baseline.HistoryDeclaredCount,
            baseline.StateEvents,
            baseline.StateEventDeclaredCount);
    }

    /// <summary>
    /// 创建一个测试状态节点；复合节点的子状态数组代表其子机器状态树。
    /// </summary>
    private static WorkbenchFsmStateNode CreateNode(
        int id,
        int orderIndex,
        string name,
        bool isCurrent,
        bool isComposite,
        long entryCount,
        IReadOnlyList<WorkbenchFsmStateNode> children)
    {
        return CreateInternal<WorkbenchFsmStateNode>(
            id,
            orderIndex,
            name,
            entryCount,
            "TestState",
            isCurrent,
            isComposite,
            isComposite ? "NestedFSM" : string.Empty,
            isComposite ? "Running" : string.Empty,
            isComposite ? "Idle" : string.Empty,
            isComposite ? 0 : -1,
            children.Count,
            children);
    }

    /// <summary>计算边端点到候选节点中心的距离，用于验证历史实际连接的同名节点。</summary>
    private static double Distance(double x, double y, ObservedFsmGraphNode node)
    {
        var deltaX = x - node.CenterX;
        var deltaY = y - node.CenterY;
        return Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
    }

    /// <summary>调用 Application read model 的内部构造方法，保持生产模型公开边界不因测试扩张。</summary>
    private static T CreateInternal<T>(params object[] arguments)
    {
        var instance = Activator.CreateInstance(
            typeof(T),
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
            null,
            arguments,
            null);
        return Assert.IsType<T>(instance);
    }
}
