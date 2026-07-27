# FsmKit 状态机

## 适用场景

FsmKit 用于需要明确生命周期和状态切换条件的业务流程，例如角色控制、战斗阶段、UI 流程和后台任务。`FSM<TEnum>` 同时只运行一个状态，保持简单、同步且由调用方主动驱动。

## 使用前提

FsmKit 可直接用于 Unity 与 Godot .NET Runtime。状态机由业务主动驱动；Unity 在生命周期中调用 `Update`，Godot 在 `_Process` 中调用对应更新入口。Workbench 只读展示实例和已观测历史。

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
| `FSM<TEnum>(string name = null)` | 创建空状态机；可选名称用于日志和排查。 |
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

## 实践模式

### 一个可复用的状态机

```csharp
using YokiFrame;

public enum GameFlowState
{
    Menu,
    Playing
}

public sealed class GameFlowContext
{
}

public sealed class MenuState : AbstractState<GameFlowState, GameFlowContext>
{
    public MenuState(FSM<GameFlowState> fsm, GameFlowContext context)
        : base(fsm, context)
    {
    }

    protected override void OnEnter()
    {
    }
}

public static class GameFlowFactory
{
    public static FSM<GameFlowState> Create()
    {
        GameFlowContext context = new();
        FSM<GameFlowState> fsm = new("GameFlow");
        fsm.Add(GameFlowState.Menu, new MenuState(fsm, context));
        fsm.Start(GameFlowState.Menu);
        return fsm;
    }
}
```

`Add` 只建立状态和默认选择，不会自动进入 `Running`。使用 `Start` 后，才可以用 `Change` 切换并转发 tick。

### 宿主中驱动 tick

Unity 组件只负责生命周期和调用：

```csharp
private FSM<GameFlowState> mFsm;

private void Update()
{
    mFsm.Update();
}

private void OnDestroy()
{
    mFsm.Dispose();
}
```

Godot .NET 对应在 `_Process` 中调用 `Update()`，在 `_ExitTree` 中释放。状态切换规则和黑板数据继续放在普通 C# 类型。

## 生命周期与错误边界

| 阶段 | `FSM` |
|---|---|
| `End` | 不转发更新或消息，保留最近选择。 |
| `Suspend` | 不转发更新或消息，保留当前选择；`Resume()` 恢复为 `Running`。 |
| `Running` | 只转发当前状态。 |

状态对象被 `Add` 后由状态机拥有；移除、`Clear` 或 `Dispose` 时不要再由业务重复释放同一个状态。状态中的外部订阅应在 `OnDispose` 解除。
`Start`、`End`、`Suspend`、`Resume`、`Dispose` 等生命周期回调尚未结束时，不允许对同一 FSM 发起嵌套状态变更；回调异常会继续抛给调用方，并把机器收敛到 `End`。

## 在工具中查看

Workbench 可以只读查看当前实例、状态和已观测转换；它不会替业务强制切换状态。

## 限制与相关资料

| 问题 | 处理 |
|---|---|
| `Change` 没效果 | 确认目标已 `Add`、状态机已启动、目标 `Condition()` 返回 true。 |
| 状态不更新 | 在宿主生命周期中调用匹配的 `Update`、`FixedUpdate` 或 `CustomUpdate`。 |
| 销毁后仍有回调 | 在状态的 `OnDispose` 解除外部订阅，并调用 `Dispose` 或 `Clear`。 |
| 出现 `ObjectDisposedException` | 不要复用已经 Dispose 的实例；需要新流程时创建新的 FSM。 |
