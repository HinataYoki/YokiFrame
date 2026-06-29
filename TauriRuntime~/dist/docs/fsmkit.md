# FsmKit 状态机

## 选择状态机

| 场景 | 入口 |
|---|---|
| 同一时刻只有一个状态运行 | `FSM<TEnum>` |
| 进入或切换状态需要参数 | `FSM<TEnum,TArgs>` |
| 多个子状态并行运行、暂停或停止 | `HierarchicalSM<TEnum>` |

状态 id 优先用枚举，方便重构和编译期检查。

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

| 方法 | 什么时候触发 |
|---|---|
| `OnCondition()` | `Change()` 前检查，返回 `false` 不切换。 |
| `OnEnter()` | 状态进入。 |
| `OnUpdate()` | 业务调用 `fsm.Update()`。 |
| `OnFixedUpdate()` | 业务调用 `fsm.FixedUpdate()`。 |
| `OnCustomUpdate()` | 业务调用 `fsm.CustomUpdate()`。 |
| `OnExit()` | 状态切出或结束。 |
| `OnSuspend()` | 状态机暂停。 |
| `OnDispose()` | 状态释放。 |
| `OnMessage<TMsg>()` | 业务调用 `fsm.SendMessage(msg)`。 |

## 常用方法

| 方法 | 说明 |
|---|---|
| `Add(id, state)` | 添加或替换状态。 |
| `Start(id)` | 从指定状态开始运行。 |
| `Change(id)` | 运行中切换状态。 |
| `Update()` / `FixedUpdate()` / `CustomUpdate()` | 主动驱动当前状态。 |
| `Suspend()` | 暂停。 |
| `End()` | 停止当前状态。 |
| `Clear()` | 结束、释放并清空所有状态。 |

`Change()` 只有在状态机处于 `Running` 时生效。

## 带参数状态

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

## 层级状态机

```csharp
var hsm = new HierarchicalSM<WorldState>("WorldHSM");
hsm.Add(WorldState.Exploration, explorationState);
hsm.Add(WorldState.Combat, combatSubFsm);
hsm.Start();

hsm.Change(WorldState.Combat, MachineState.Running);
hsm.Change(WorldState.Exploration, MachineState.Suspend);
hsm.Update();
```

`HierarchicalSM<TEnum>.Change(id)` 是接口占位。控制子状态时使用 `Change(id, MachineState)`。

## 工作台诊断

FsmKit 页面用于查看活动状态机、当前状态、状态列表、转换历史和状态事件。

| 在工作台里看什么 | 用途 |
|---|---|
| 状态机列表 | 确认目标 FSM 是否已经创建并 Start。 |
| 当前状态 | 判断状态是否停在预期节点。 |
| 状态列表 | 检查目标状态是否已 Add。 |
| 转换历史 | 追踪从哪个状态切到哪个状态。 |
| 状态事件 | 查看 Enter、Exit、Update 等生命周期记录。 |

状态不切换时，先确认 FSM 在列表中，再看目标状态是否存在，最后看转换历史里是否出现失败前的状态。

## 常见坑

| 问题 | 处理方式 |
|---|---|
| `Change()` 没效果 | 确认已 `Start()`、目标状态已 `Add()`、`OnCondition()` 返回 `true`。 |
| 状态不更新 | 确认业务代码调用 `Update()` / `FixedUpdate()`。 |
| 销毁后还持有对象 | 拥有者销毁时调用 `Dispose()` 或 `Clear()`。 |
| 带参数状态没收到参数 | 状态类必须继承 `AbstractState<TEnum,TBlack,TArgs>`。 |
| HSM 单参 `Change(id)` 没效果 | 使用 `Change(id, MachineState.Running/Suspend/End)`。 |
