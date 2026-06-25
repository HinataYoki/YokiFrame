# FsmKit 状态机

FsmKit 提供纯 C# 状态机能力，核心类型位于 `YokiFrame`。它不自动绑定 Unity `Update` 或 Godot `_Process`，而是由业务代码在合适的生命周期中主动驱动。

## 选择哪种状态机

| 场景 | 推荐入口 |
|------|----------|
| 同一时刻只有一个状态运行 | `FSM<TEnum>` |
| 启动或切换状态时需要传参 | `FSM<TEnum,TArgs>` + `AbstractState<TEnum,TBlack,TArgs>` |
| 多个子状态可能同时运行、暂停或停止 | `HierarchicalSM<TEnum>` |

状态 id 使用枚举。这样切换状态时可以获得编译期检查，也方便后续重构。

## 最小示例

```csharp
using UnityEngine;
using YokiFrame;

public sealed class PlayerFsmDemo : MonoBehaviour
{
    private enum PlayerState
    {
        Idle,
        Run
    }

    private FSM<PlayerState> mFsm;

    private void Awake()
    {
        mFsm = new FSM<PlayerState>("PlayerFSM");
        mFsm.Add(PlayerState.Idle, new IdleState(mFsm, this));
        mFsm.Add(PlayerState.Run, new RunState(mFsm, this));
        mFsm.Start(PlayerState.Idle);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            mFsm.Change(PlayerState.Run);
        }

        mFsm.Update();
    }

    private void FixedUpdate()
    {
        mFsm.FixedUpdate();
    }

    private void OnDestroy()
    {
        ((IState)mFsm).Dispose();
    }

    private sealed class IdleState : AbstractState<PlayerState, PlayerFsmDemo>
    {
        public IdleState(FSM<PlayerState> fsm, PlayerFsmDemo owner) : base(fsm, owner)
        {
        }

        protected override void OnEnter()
        {
            Debug.Log("enter idle");
        }
    }

    private sealed class RunState : AbstractState<PlayerState, PlayerFsmDemo>
    {
        public RunState(FSM<PlayerState> fsm, PlayerFsmDemo owner) : base(fsm, owner)
        {
        }

        protected override bool OnCondition()
        {
            return mBlack.enabled;
        }

        protected override void OnEnter()
        {
            Debug.Log("enter run");
        }
    }
}
```

## 状态生命周期

状态类继承 `AbstractState<TEnum,TBlack>` 后，可以按需重写这些方法：

| 方法 | 触发时机 |
|------|----------|
| `OnCondition()` | `Change()` 前检查。返回 `false` 时不切换。 |
| `OnEnter()` | 状态启动或切入。 |
| `OnUpdate()` | 状态机 `Running` 且业务调用 `fsm.Update()`。 |
| `OnFixedUpdate()` | 状态机 `Running` 且业务调用 `fsm.FixedUpdate()`。 |
| `OnCustomUpdate()` | 状态机 `Running` 且业务调用 `fsm.CustomUpdate()`。 |
| `OnExit()` | 状态结束或切出。 |
| `OnSuspend()` | 状态机暂停。 |
| `OnDispose()` | 状态对象被释放。 |
| `OnMessage<TMsg>()` | 业务调用 `fsm.SendMessage(msg)`。 |

状态机自身的 `MachineState` 取值为 `End`、`Suspend`、`Running`。

## FSM 常用方法

| 方法 | 说明 |
|------|------|
| `Add(id, state)` | 添加状态。若 id 已存在且不是同一对象，会释放旧状态并替换。 |
| `Remove(id)` | 移除并释放指定状态。 |
| `Get(id, out state)` | 获取指定状态，返回是否存在。 |
| `Start()` | 从当前默认状态开始运行。 |
| `Start(id)` | 从指定状态开始运行。 |
| `Change(id)` | 运行中切换到指定状态。 |
| `Change(id, args)` | 运行中切换状态并传参。 |
| `Update()` | 驱动当前状态的 `OnUpdate()`。 |
| `FixedUpdate()` | 驱动当前状态的 `OnFixedUpdate()`。 |
| `CustomUpdate()` | 驱动当前状态的 `OnCustomUpdate()`。 |
| `Suspend()` | 暂停状态机和当前状态。 |
| `End()` | 停止状态机并结束当前状态。 |
| `Clear()` | 结束、释放并清空所有状态。 |
| `SendMessage<TMsg>(msg)` | 给当前状态发送消息。 |

## FSM 属性

| 属性 | 说明 |
|------|------|
| `CurState` | 当前状态对象（`IState`）。 |
| `CurEnum` | 当前状态的枚举值。 |
| `MachineState` | 状态机当前状态：`End`、`Suspend` 或 `Running`。 |

`Change()` 只有在状态机为 `Running` 时生效。如果目标状态不存在、目标状态就是当前状态，或 `OnCondition()` 返回 `false`，本次切换不会发生。

## 带参数状态

需要在进入状态时传数据时，使用 `FSM<TEnum,TArgs>` 和 `AbstractState<TEnum,TBlack,TArgs>`。

```csharp
public sealed class SpawnArgs
{
    public int Level;
}

private sealed class SpawnState : AbstractState<PlayerState, PlayerFsmDemo, SpawnArgs>
{
    public SpawnState(FSM<PlayerState> fsm, PlayerFsmDemo owner) : base(fsm, owner)
    {
    }

    protected override void OnEnter(SpawnArgs args)
    {
        Debug.Log("spawn level " + args.Level);
    }
}

var fsm = new FSM<PlayerState, SpawnArgs>();
fsm.Add(PlayerState.Idle, new SpawnState(fsm, owner));
fsm.Start(PlayerState.Idle, new SpawnArgs { Level = 3 });
```

如果目标状态没有实现 `IState<TArgs>`，`Start(args)` 或 `Change(id,args)` 会回退到无参 `Start()`。

## 层级状态机

`HierarchicalSM<TEnum>` 可以同时管理多个子状态，每个子状态都有自己的 `MachineState`。

```csharp
private enum WorldState
{
    Exploration,
    Combat
}

var hsm = new HierarchicalSM<WorldState>("WorldHSM");
hsm.Add(WorldState.Exploration, explorationState);
hsm.Add(WorldState.Combat, combatSubFsm);
hsm.Start();

hsm.Change(WorldState.Combat, MachineState.Running);
hsm.Change(WorldState.Exploration, MachineState.Suspend);
hsm.Update();
```

注意：

- `HierarchicalSM<TEnum>.Start()` 会把已有子状态全部置为 `Running`。
- `Change(TEnum id)` 是接口占位，当前不会切换状态。
- 控制某个子状态时使用 `Change(TEnum id, MachineState targetState)`。
- 子状态可以是普通 `IState`，也可以是实现了 `IFSM` 的状态机。

## 常见问题

| 问题 | 处理方式 |
|------|----------|
| `Change()` 没有效果 | 确认先调用过 `Start()`，状态机处于 `Running`，目标状态已 `Add()`，且 `OnCondition()` 返回 `true`。 |
| 状态没有每帧执行 | 确认业务代码正在调用 `fsm.Update()`、`fsm.FixedUpdate()` 或 `fsm.CustomUpdate()`。 |
| 销毁后状态还持有对象 | 在拥有者销毁时调用 `((IState)fsm).Dispose()` 或 `fsm.Clear()`。 |
| 带参数状态没有收到参数 | 确认状态类继承的是 `AbstractState<TEnum,TBlack,TArgs>`，并重写 `OnEnter(TArgs args)`。 |
| HSM 单参 `Change(id)` 没效果 | 使用 `Change(id, MachineState.Running/Suspend/End)`。 |
