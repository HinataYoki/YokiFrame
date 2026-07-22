using YokiFrame.Tooling.Application.Models.FsmKit;

namespace YokiFrame.Workbench.Avalonia.Tests;

/// <summary>
/// 创建 FsmKit 页面契约测试共用的强类型 Application 模型，避免各测试复制协议解析逻辑。
/// </summary>
internal static class FsmKitContractTestData
{
    /// <summary>
    /// 创建包含两个同宿主实例、选中详情和转换历史的强类型 FsmKit 状态。
    /// </summary>
    /// <param name="selectedInstanceId">payload 当前选择的 instanceId。</param>
    /// <param name="source">数据来源。</param>
    /// <param name="rawPayload">原始 payload。</param>
    /// <param name="evidencePath">来源证据。</param>
    /// <param name="transitionTarget">默认转换目标名称。</param>
    /// <param name="engineId">宿主引擎标识。</param>
    /// <param name="sessionId">宿主会话标识。</param>
    /// <param name="generation">宿主代次。</param>
    /// <param name="transitions">可选的明确转换序列。</param>
    /// <param name="selectedCurrentState">chosen 实例的当前状态，默认保持既有契约数据。</param>
    /// <param name="updatedAtUtc">可选的宿主写入时间；为空时使用稳定测试时间。</param>
    /// <returns>测试用强类型状态。</returns>
    internal static WorkbenchFsmKitState CreateState(
        string selectedInstanceId,
        string source,
        string rawPayload,
        string evidencePath,
        string transitionTarget,
        string engineId = "unity-editor",
        string sessionId = "session-7",
        long generation = 7L,
        IReadOnlyList<(string From, string To)>? transitions = null,
        string selectedCurrentState = "ChosenState",
        DateTimeOffset? updatedAtUtc = null)
    {
        var dataSource = CreateDataSource(
            engineId,
            sessionId,
            generation,
            source,
            evidencePath,
            rawPayload,
            updatedAtUtc);
        var machines = new[]
        {
            CreateMachine("default-instance", "DefaultFSM", "DefaultState"),
            CreateMachine("chosen-instance", "ChosenFSM", selectedCurrentState)
        };
        var selected = machines.Single(machine => machine.InstanceId == selectedInstanceId);
        var details = CreateDetails(selected);
        var history = CreateHistory(transitions, transitionTarget);
        return CreateInternal<WorkbenchFsmKitState>(
            dataSource,
            selected.Name,
            selected.InstanceId,
            machines.Length,
            machines,
            details,
            history,
            history.Length,
            Array.Empty<WorkbenchFsmStateEvent>(),
            0);
    }

    /// <summary>
    /// 创建已成功读取、但当前没有注册 FSM 的快照。
    /// </summary>
    /// <returns>零实例 FsmKit 状态。</returns>
    internal static WorkbenchFsmKitState CreateEmptyState()
    {
        var dataSource = CreateDataSource(
            "unity-editor",
            "empty-session",
            8L,
            "snapshot",
            "F:/Project/fsm-empty.json",
            "{\"fsms\":[],\"count\":0}");
        return CreateInternal<WorkbenchFsmKitState>(
            dataSource,
            string.Empty,
            string.Empty,
            0,
            Array.Empty<WorkbenchFsmMachineSummary>(),
            null!,
            Array.Empty<WorkbenchFsmTransition>(),
            0,
            Array.Empty<WorkbenchFsmStateEvent>(),
            0);
    }

    /// <summary>
    /// 创建固定时间、模式和通道元数据的数据源模型。
    /// </summary>
    /// <param name="engineId">宿主引擎标识。</param>
    /// <param name="sessionId">宿主会话标识。</param>
    /// <param name="generation">宿主代次。</param>
    /// <param name="source">数据来源。</param>
    /// <param name="evidencePath">来源证据。</param>
    /// <param name="rawPayload">原始 payload。</param>
    /// <param name="updatedAtUtc">可选的宿主写入时间。</param>
    /// <returns>测试用数据源。</returns>
    private static WorkbenchFsmKitDataSource CreateDataSource(
        string engineId,
        string sessionId,
        long generation,
        string source,
        string evidencePath,
        string rawPayload,
        DateTimeOffset? updatedAtUtc = null)
    {
        return CreateInternal<WorkbenchFsmKitDataSource>(
            engineId,
            sessionId,
            generation,
            "EditMode",
            updatedAtUtc ?? DateTimeOffset.Parse("2026-07-11T10:00:00Z"),
            source,
            source == "command" ? "file-bridge" : string.Empty,
            new[] { evidencePath },
            string.Empty,
            rawPayload);
    }

    /// <summary>
    /// 将选中实例摘要投影成详情模型，保持原契约测试的数据形状。
    /// </summary>
    /// <param name="selected">当前选中的实例摘要。</param>
    /// <returns>与摘要一致的实例详情。</returns>
    private static WorkbenchFsmMachineDetails CreateDetails(WorkbenchFsmMachineSummary selected)
    {
        return CreateInternal<WorkbenchFsmMachineDetails>(
            selected.Name,
            selected.InstanceId,
            selected.MachineState,
            selected.CurrentState,
            selected.CurrentStateId,
            selected.StateCount,
            Array.Empty<WorkbenchFsmStateNode>());
    }

    /// <summary>
    /// 创建默认或调用方指定的转换历史，并保留原测试的确定时间文本。
    /// </summary>
    /// <param name="transitions">调用方指定的转换序列。</param>
    /// <param name="transitionTarget">未指定序列时使用的默认目标。</param>
    /// <returns>强类型转换历史。</returns>
    private static WorkbenchFsmTransition[] CreateHistory(
        IReadOnlyList<(string From, string To)>? transitions,
        string transitionTarget)
    {
        return (transitions ?? new[] { ("Start", transitionTarget) })
            .Select((transition, index) => CreateInternal<WorkbenchFsmTransition>(
                transition.From,
                transition.To,
                "10:00:0" + index))
            .ToArray();
    }

    /// <summary>
    /// 创建测试用 FSM 摘要。
    /// </summary>
    /// <param name="instanceId">实例唯一标识。</param>
    /// <param name="name">状态机显示名称。</param>
    /// <param name="currentState">当前状态名称。</param>
    /// <returns>测试用实例摘要。</returns>
    private static WorkbenchFsmMachineSummary CreateMachine(
        string instanceId,
        string name,
        string currentState)
    {
        return CreateInternal<WorkbenchFsmMachineSummary>(
            instanceId,
            name,
            "Running",
            currentState,
            1,
            2);
    }

    /// <summary>
    /// 调用 Application 模型的受控内部构造方法，避免测试复制 JSON parser。
    /// </summary>
    /// <typeparam name="T">待创建的 Application 模型类型。</typeparam>
    /// <param name="arguments">内部构造方法的实参数组。</param>
    /// <returns>构造完成且类型匹配的模型。</returns>
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
