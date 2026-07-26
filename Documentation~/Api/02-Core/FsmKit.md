# FsmKit

> 面向读者：需要以显式状态和生命周期组织业务流程的 Runtime 开发者
>
> 主要入口：`FSM<TEnum>`、`FSM<TEnum, TArgs>`
>
> 运行边界：跨宿主 Runtime；诊断和实例历史只在 Editor/Tools 编译
>
> 状态来源：`Documentation~/Api/00-GettingStarted/Kit_Status.md`

## 适用场景

FsmKit 用于需要明确生命周期和状态切换条件的业务流程，例如角色控制、战斗阶段、UI 流程和后台任务。`FSM<TEnum>` 同时只运行一个状态，保持简单、同步且由调用方主动驱动。

## 入口与当前状态

| 项目 | 当前值 |
|---|---|
| Runtime | 已实现，位于 `Core/Runtime/FsmKit` |
| 程序集 | Core Runtime 编入 `YokiFrame`，无宿主引用 |
| Interaction | 已实现，Provider 位于 `Core/Editor/FsmKit` |
| Workbench | 已实现，支持实例、状态、转换图和历史 |
| 状态入口 | `FsmKit/state`，详情使用稳定 `instanceId` |

## 快速上手

```csharp
using YokiFrame;

public enum PlayerState { Idle, Run }

public sealed class IdleState : AbstractState<PlayerState, object>
{
    public IdleState(FSM<PlayerState> fsm, object blackboard)
        : base(fsm, blackboard) { }

    protected override void OnEnter() { }
}

FSM<PlayerState> fsm = new();
fsm.Add(PlayerState.Idle, new IdleState(fsm, new object()));
fsm.Start(PlayerState.Idle);
fsm.Update();
fsm.Dispose();
```

实际项目通常让宿主的 `Update`、`FixedUpdate` 或自定义 tick 调用状态机对应入口；FsmKit 不会自动注册 Core FrameLoop。

## 核心 API

### 状态契约

| 类型/API | 说明 |
|---|---|
| `IState.Condition()` | 进入前检查，默认返回 `true`。 |
| `IState.Start()` | 进入状态。 |
| `IState.Suspend()` | 暂停状态。 |
| `IState.Resume()` | 恢复被挂起的状态；默认不执行任何操作，不重复触发进入副作用。 |
| `IState.Update()` / `FixedUpdate()` / `CustomUpdate()` | 三种主动更新入口。 |
| `IState.End()` | 结束状态。 |
| `IState.Dispose()` | 释放状态资源；状态机保证每次移除只释放一次。 |
| `IState.SendMessage<TMsg>(TMsg message)` | 向状态发送强类型消息。 |
| `IState<TArgs>.Start(TArgs args)` | 使用进入参数启动状态；无参进入映射为 `default(TArgs)`。 |
| `MachineState` | `End`、`Suspend`、`Running` 三个生命周期阶段。 |

`AbstractState<TEnum,TBlack>` 提供 `mFSM`、`mBlack` 和 `OnCondition`、`OnEnter`、`OnSuspend`、`OnResume`、`OnUpdate`、`OnFixedUpdate`、`OnCustomUpdate`、`OnExit`、`OnDispose`、`OnMessage<TMsg>` 覆写点。需要进入参数时使用 `AbstractState<TEnum,TBlack,TArgs>` 的 `OnEnter(TArgs args)`。

### `FSM<TEnum>`

| API | 说明 |
|---|---|
| `FSM<TEnum>(string name = null)` | 创建空状态机；签名在所有构建一致，名称只由 Editor/Tools 保存并用于诊断。 |
| `CurState` / `CurEnum` | 当前状态实例和当前/最近选择的枚举值。 |
| `MachineState` | 当前为 `End`、`Suspend` 或 `Running`。 |
| `Get(TEnum id, out IState state)` | 查询状态，不存在时输出空值。 |
| `Add(TEnum id, IState state)` | 添加或替换状态；替换当前状态时先闭合旧生命周期。 |
| `Remove(TEnum id)` | 移除并释放指定状态；移除当前状态后回到 `End`。 |
| `Start()` / `Start(TEnum id)` | 启动当前选择或指定状态；运行中、目标不存在或条件失败时不切换。 |
| `Change(TEnum id)` | 运行中切换到目标状态；目标不存在或条件失败时不切换。 |
| `Change<TArgs>(TEnum id, TArgs args)` | 带参数切换；目标支持 `IState<TArgs>` 时传参，否则按无参进入。 |
| `Suspend()` / `End()` | 暂停或结束当前状态，保留当前选择。 |
| `Resume()` | 恢复被挂起的当前状态并回到 `Running`；非 `Suspend` 阶段为 no-op。 |
| `Update()` / `FixedUpdate()` / `CustomUpdate()` | 仅在 `Running` 时转发给当前状态。 |
| `SendMessage<TMsg>(TMsg message)` | 仅在 `Running` 时转发消息。 |
| `Clear()` | 结束、释放并清空全部状态。 |
| `Dispose()` | 释放状态机并注销诊断登记；重复调用幂等，之后复用实例会抛 `ObjectDisposedException`。 |

### 带启动参数的 `FSM<TEnum,TArgs>`

它继承 `FSM<TEnum>`，额外提供：

| API | 说明 |
|---|---|
| `Start(TArgs args)` | 使用参数启动当前选择。 |
| `Start(TEnum id, TArgs args)` | 使用参数启动指定状态。 |

```csharp
public sealed class SpawnState : AbstractState<PlayerState, object, int>
{
    public SpawnState(FSM<PlayerState> fsm, object blackboard)
        : base(fsm, blackboard) { }

    protected override void OnEnter(int level)
    {
        LogKit.Info("spawn level=" + level);
    }
}

FSM<PlayerState, int> spawnFsm = new();
spawnFsm.Add(PlayerState.Idle, new SpawnState(spawnFsm, new object()));
spawnFsm.Start(PlayerState.Idle, 3);
```

## 生命周期与错误边界

| 阶段 | `FSM` |
|---|---|
| `End` | 不转发更新或消息，保留最近选择。 |
| `Suspend` | 不转发更新或消息，保留当前选择；`Resume()` 恢复为 `Running`。 |
| `Running` | 只转发当前状态。 |

状态对象被 `Add` 后由状态机拥有；移除、`Clear` 或 `Dispose` 时不要再由业务重复释放同一个状态。状态中的外部订阅应在 `OnDispose` 解除。
`Start`、`End`、`Suspend`、`Resume`、`Dispose` 等生命周期回调尚未结束时，不允许对同一 FSM 发起嵌套状态变更；回调异常会继续抛给调用方，并把机器收敛到 `End`。

## 宿主与工具入口

Editor/Tools 构建中的 `IFSM` 额外提供 `Name`、`EnumType`、`CurrentState`、`CurrentStateId`、`GetAllStates()` 和 `GetStateOrderIndex(int)`。`FsmKitRegistry`、状态事件与历史不进入 Player。

Workbench 通过 `FsmKit/state` 展示实例列表、当前状态、已观测转换图和有限历史。命令按稳定实例 id 读取详情：

```powershell
yoki command send --engine <engineId> --kit FsmKit --action list_all --project <projectRoot>
yoki command send --engine <engineId> --kit FsmKit --action get_state --payload '{"instanceId":"<id>"}' --project <projectRoot>
yoki command send --engine <engineId> --kit FsmKit --action get_history --payload '{"instanceId":"<id>"}' --project <projectRoot>
```

可用只读 action 还包括 `get_state_events` 和 `get_workbench_snapshot`。每个实例最多保留 200 条转换历史；图只表示已观测关系，不宣称完整静态状态图。Workbench 不提供远程强制切换状态的命令。

## 限制与相关资料

| 问题 | 处理 |
|---|---|
| `Change` 没效果 | 确认目标已 `Add`、状态机已启动、目标 `Condition()` 返回 true。 |
| 状态不更新 | 在宿主生命周期中调用匹配的 `Update`、`FixedUpdate` 或 `CustomUpdate`。 |
| 销毁后仍有回调 | 在状态的 `OnDispose` 解除外部订阅，并调用 `Dispose` 或 `Clear`。 |
| 出现 `ObjectDisposedException` | 不要复用已经 Dispose 的实例；需要新流程时创建新的 FSM。 |
