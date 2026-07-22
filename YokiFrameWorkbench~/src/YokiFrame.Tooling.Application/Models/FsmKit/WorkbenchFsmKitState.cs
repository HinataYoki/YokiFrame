namespace YokiFrame.Tooling.Application.Models.FsmKit;

/// <summary>
/// 提供 Workbench 可直接绑定的 FsmKit 强类型状态。
/// </summary>
public sealed class WorkbenchFsmKitState
{
    /// <summary>
    /// 创建完整 FsmKit 状态；只允许 Application parser 构造。
    /// </summary>
    /// <param name="dataSource">数据来源与证据。</param>
    /// <param name="fsmName">payload 根选择名称。</param>
    /// <param name="instanceId">payload 根选择实例标识。</param>
    /// <param name="declaredCount">payload 声明的 FSM 数量。</param>
    /// <param name="machines">FSM 摘要列表。</param>
    /// <param name="selected">当前选中 FSM 详情。</param>
    /// <param name="history">状态转换历史。</param>
    /// <param name="historyDeclaredCount">payload 声明的历史数量。</param>
    /// <param name="stateEvents">状态生命周期事件。</param>
    /// <param name="stateEventDeclaredCount">payload 声明的事件数量。</param>
    internal WorkbenchFsmKitState(
        WorkbenchFsmKitDataSource dataSource,
        string fsmName,
        string instanceId,
        int declaredCount,
        IReadOnlyList<WorkbenchFsmMachineSummary> machines,
        WorkbenchFsmMachineDetails? selected,
        IReadOnlyList<WorkbenchFsmTransition> history,
        int historyDeclaredCount,
        IReadOnlyList<WorkbenchFsmStateEvent> stateEvents,
        int stateEventDeclaredCount)
    {
        DataSource = dataSource;
        FsmName = fsmName;
        InstanceId = instanceId;
        DeclaredCount = declaredCount;
        Machines = machines;
        Selected = selected;
        History = history;
        HistoryDeclaredCount = historyDeclaredCount;
        StateEvents = stateEvents;
        StateEventDeclaredCount = stateEventDeclaredCount;
    }

    /// <summary>获取数据来源与证据。</summary>
    public WorkbenchFsmKitDataSource DataSource { get; }

    /// <summary>获取目标 engine 标识。</summary>
    public string EngineId => DataSource.EngineId;

    /// <summary>获取宿主会话标识。</summary>
    public string SessionId => DataSource.SessionId;

    /// <summary>获取宿主 generation。</summary>
    public long Generation => DataSource.Generation;

    /// <summary>获取宿主当前模式。</summary>
    public string Mode => DataSource.Mode;

    /// <summary>获取数据源更新时间。</summary>
    public DateTimeOffset UpdatedAtUtc => DataSource.UpdatedAtUtc;

    /// <summary>获取 telemetry、snapshot 或 command 来源。</summary>
    public string Source => DataSource.Source;

    /// <summary>获取显式命令实际使用的传输。</summary>
    public string Transport => DataSource.Transport;

    /// <summary>获取状态或命令证据路径。</summary>
    public IReadOnlyList<string> EvidencePaths => DataSource.EvidencePaths;

    /// <summary>获取数据不可用或发生回落的原因。</summary>
    public string StaleReason => DataSource.StaleReason;

    /// <summary>获取未经裁剪的原始 payload。</summary>
    public string RawPayloadJson => DataSource.RawPayloadJson;

    /// <summary>获取 payload 根选择名称。</summary>
    public string FsmName { get; }

    /// <summary>获取 payload 根选择实例标识。</summary>
    public string InstanceId { get; }

    /// <summary>获取 payload 声明的 FSM 数量。</summary>
    public int DeclaredCount { get; }

    /// <summary>获取全部 FSM 摘要。</summary>
    public IReadOnlyList<WorkbenchFsmMachineSummary> Machines { get; }

    /// <summary>获取当前选中 FSM 详情；没有选择时为空。</summary>
    public WorkbenchFsmMachineDetails? Selected { get; }

    /// <summary>获取按 Runtime 或 provider 返回顺序排列的转换历史。</summary>
    public IReadOnlyList<WorkbenchFsmTransition> History { get; }

    /// <summary>获取 payload 声明的转换历史数量。</summary>
    public int HistoryDeclaredCount { get; }

    /// <summary>获取按 Runtime 或 provider 返回顺序排列的状态生命周期事件。</summary>
    public IReadOnlyList<WorkbenchFsmStateEvent> StateEvents { get; }

    /// <summary>获取 payload 声明的状态生命周期事件数量。</summary>
    public int StateEventDeclaredCount { get; }
}

/// <summary>
/// 描述 FSM 实例列表中的一项摘要。
/// </summary>
public sealed class WorkbenchFsmMachineSummary
{
    /// <summary>创建 FSM 摘要；只允许 Application parser 构造。</summary>
    internal WorkbenchFsmMachineSummary(
        string instanceId,
        string name,
        string machineState,
        string currentState,
        int currentStateId,
        int stateCount)
    {
        InstanceId = instanceId;
        Name = name;
        MachineState = machineState;
        CurrentState = currentState;
        CurrentStateId = currentStateId;
        StateCount = stateCount;
    }

    /// <summary>获取稳定实例标识。</summary>
    public string InstanceId { get; }

    /// <summary>获取诊断名称。</summary>
    public string Name { get; }

    /// <summary>获取状态机生命周期状态。</summary>
    public string MachineState { get; }

    /// <summary>获取当前状态名称。</summary>
    public string CurrentState { get; }

    /// <summary>获取当前状态整数标识。</summary>
    public int CurrentStateId { get; }

    /// <summary>获取状态节点数量。</summary>
    public int StateCount { get; }
}

/// <summary>
/// 描述当前选中 FSM 及其递归状态树。
/// </summary>
public sealed class WorkbenchFsmMachineDetails
{
    /// <summary>创建 FSM 详情；只允许 Application parser 构造。</summary>
    internal WorkbenchFsmMachineDetails(
        string fsmName,
        string instanceId,
        string machineState,
        string currentState,
        int currentStateId,
        int stateCount,
        IReadOnlyList<WorkbenchFsmStateNode> states)
    {
        FsmName = fsmName;
        InstanceId = instanceId;
        MachineState = machineState;
        CurrentState = currentState;
        CurrentStateId = currentStateId;
        StateCount = stateCount;
        States = states;
    }

    /// <summary>获取诊断名称。</summary>
    public string FsmName { get; }

    /// <summary>获取稳定实例标识。</summary>
    public string InstanceId { get; }

    /// <summary>获取状态机生命周期状态。</summary>
    public string MachineState { get; }

    /// <summary>获取当前状态名称。</summary>
    public string CurrentState { get; }

    /// <summary>获取当前状态整数标识。</summary>
    public int CurrentStateId { get; }

    /// <summary>获取状态节点数量。</summary>
    public int StateCount { get; }

    /// <summary>获取按加入顺序排列的递归状态树。</summary>
    public IReadOnlyList<WorkbenchFsmStateNode> States { get; }
}

/// <summary>
/// 描述 FSM 状态树中的普通或复合状态节点。
/// </summary>
public sealed class WorkbenchFsmStateNode
{
    /// <summary>创建状态树节点；只允许 Application parser 构造。</summary>
    internal WorkbenchFsmStateNode(
        int id,
        int orderIndex,
        string name,
        long entryCount,
        string stateType,
        bool isCurrent,
        bool isComposite,
        string childMachineName,
        string machineState,
        string currentState,
        int currentStateId,
        int stateCount,
        IReadOnlyList<WorkbenchFsmStateNode> children)
    {
        Id = id;
        OrderIndex = orderIndex;
        Name = name;
        EntryCount = entryCount;
        StateType = stateType;
        IsCurrent = isCurrent;
        IsComposite = isComposite;
        ChildMachineName = childMachineName;
        MachineState = machineState;
        CurrentState = currentState;
        CurrentStateId = currentStateId;
        StateCount = stateCount;
        Children = children;
    }

    /// <summary>获取状态整数标识。</summary>
    public int Id { get; }

    /// <summary>获取首次加入顺序。</summary>
    public int OrderIndex { get; }

    /// <summary>获取状态名称。</summary>
    public string Name { get; }

    /// <summary>获取 Runtime 自本次记录清理后累计进入该状态的次数。</summary>
    public long EntryCount { get; }

    /// <summary>获取状态实现类型名称。</summary>
    public string StateType { get; }

    /// <summary>获取该节点是否为所属状态机当前状态。</summary>
    public bool IsCurrent { get; }

    /// <summary>获取该节点是否为复合状态机。</summary>
    public bool IsComposite { get; }

    /// <summary>获取复合状态机名称；普通状态为空。</summary>
    public string ChildMachineName { get; }

    /// <summary>获取复合状态机生命周期状态；普通状态为空。</summary>
    public string MachineState { get; }

    /// <summary>获取复合状态机当前状态名称；普通状态为空。</summary>
    public string CurrentState { get; }

    /// <summary>获取复合状态机当前状态标识；普通状态为 -1。</summary>
    public int CurrentStateId { get; }

    /// <summary>获取复合状态机状态数量；普通状态为 0。</summary>
    public int StateCount { get; }

    /// <summary>获取复合状态机子节点；普通状态为空列表。</summary>
    public IReadOnlyList<WorkbenchFsmStateNode> Children { get; }
}

/// <summary>
/// 描述一次 FSM 状态转换历史。
/// </summary>
public sealed class WorkbenchFsmTransition
{
    /// <summary>创建转换历史；只允许 Application parser 构造。</summary>
    internal WorkbenchFsmTransition(string from, string to, string time)
    {
        From = from;
        To = to;
        Time = time;
    }

    /// <summary>获取来源状态。</summary>
    public string From { get; }

    /// <summary>获取目标状态。</summary>
    public string To { get; }

    /// <summary>获取 Runtime 保留的原始时间文本。</summary>
    public string Time { get; }
}

/// <summary>
/// 描述状态加入、移除等生命周期事件。
/// </summary>
public sealed class WorkbenchFsmStateEvent
{
    /// <summary>创建状态事件；只允许 Application parser 构造。</summary>
    internal WorkbenchFsmStateEvent(string eventName, string state, string time)
    {
        EventName = eventName;
        State = state;
        Time = time;
    }

    /// <summary>获取稳定事件名称。</summary>
    public string EventName { get; }

    /// <summary>获取关联状态名称。</summary>
    public string State { get; }

    /// <summary>获取 Runtime 保留的原始时间文本。</summary>
    public string Time { get; }
}
