# ActionKit 动作

## 核心概念

| 类型 | 作用 |
|---|---|
| `IAction` | 单个动作。 |
| `ISequence` | 顺序执行多个动作。 |
| `IParallel` | 并行执行多个动作。 |
| `IRepeat` | 重复一组动作。 |
| `IActionController` | `Start()` 返回的控制器，用于暂停、恢复、取消。 |

## 顺序动作

```csharp
using UnityEngine;
using YokiFrame;

public sealed class SequenceDemo : MonoBehaviour
{
    private IActionController mController;

    private void Start()
    {
        mController = ActionKit.Sequence()
            .Callback(() => Debug.Log("step 1"))
            .Delay(0.5f)
            .Callback(() => Debug.Log("step 2"))
            .Start();
    }

    private void OnDestroy()
    {
        mController.Cancel();
    }
}
```

## 并行和重复

```csharp
ActionKit.Sequence()
    .Parallel(parallel =>
    {
        parallel.Delay(0.3f, OnFastFinished);
        parallel.Delay(1.0f, OnSlowFinished);
    }, waitAll: true)
    .Callback(OnAllFinished)
    .Start();
```

```csharp
ActionKit.Sequence()
    .Repeat(repeat =>
    {
        repeat.Callback(OnTick);
        repeat.Delay(0.2f);
    }, count: 3)
    .Start();
```

`waitAll: true` 等全部子动作完成；`false` 任一子动作完成即结束。无限重复必须有会结束的 condition。

## 常用动作

| 动作 | 创建方式 |
|---|---|
| 延迟秒 | `Delay(seconds, callback)` |
| 延迟帧 | `DelayFrame(frameCount, callback)` |
| 下一帧 | `NextFrame(callback)` |
| 回调 | `Callback(callback)` |
| 条件 | `.Condition(condition)` |
| 插值 | `Lerp(...)` / `.Lerp01(...)` |
| 协程 | `Coroutine(Func<IEnumerator>)` |
| Task | `Task(Func<Task>)` |

## 控制器

```csharp
IActionController controller = ActionKit.Delay(1f, OnFinished).Start();

controller.Pause();
controller.Resume();
controller.TogglePause();
controller.Cancel();
```

不受 `timeScale` 影响：

```csharp
controller.UpdateMode = ActionUpdateModes.UnscaledDeltaTime;
```

### 生命周期语义

`Start(onFinish)` 会创建 `IActionController` 并交给 `ActionKitScheduler`。调度器会立刻用 `dt = 0` 推进一次，所以 `Callback`、空 `Sequence`、已满足的 `Condition` 等可能在 `Start()` 调用内直接完成；未完成的动作会进入等待执行队列，并在下一次宿主 tick 时转入执行列表。Unity 由 `UnityActionKitInstaller` 驱动，Godot 由 `GodotActionKitInstaller` 在 `_Process` 中驱动。

正常完成的顺序是：Action 内部调用 `this.Finish()` 或状态变为 `ActionStatus.Finished`，随后当前 Action 的 `OnFinish()` 被调用，调度器再触发 `Start(onFinish)` 传入的 controller 完成回调，最后执行 `OnEnd()` / `OnDeinit()` 并等待回收。只有正常完成会触发 `Start(onFinish)`。

`Cancel()` 表示取消 controller，不表示完成。取消会让调度器结束并释放当前 Action，`OnDeinit()` 会执行，但不会调用当前 Action 的 `OnFinish()`，也不会调用 `Start(onFinish)` 的完成回调。取消已进入等待队列但尚未经过首次宿主 tick 的 controller，也会正确走释放流程，避免零帧启动过的子动作泄漏。

`IAction.Finish()` 是 action 级 API，不是 controller 级 API。它适合自定义 Action 内部表达“我已正常完成”，或在业务层保留了具体 `IAction` 引用时主动标记该 Action 完成。当前没有 `IActionController.Complete()` 这类“让 controller 立刻完成整棵动作树”的公共 API；如果业务需要“提前完成并落到最终状态”，应显式设置业务最终状态，或保留底层动作 / tween 引用并使用其完成语义。

### 互斥动作

同一个目标属性同一时间只应由一个动作通道控制。比如角色移动反复触发时，保存当前移动 controller，下一次移动前先取消旧 controller，再启动新动作：

```csharp
private IActionController mMoveController;

private void MoveTo(Vector2 targetPos)
{
    if (mMoveController != null)
    {
        mMoveController.Cancel();
        mMoveController = null;
    }

    mMoveController = ActionKit.Sequence()
        .Lerp(0f, 1f, 0.7f, t => ApplyMove(targetPos, t))
        .Start(_ => mMoveController = null);
}
```

接入 DOTween 时，`Sequence().DOTween(tween, killOnCancel: true)` 默认会在取消或释放时 `Kill(false)` 未完成的 tween：它不会跳到终点，也不会触发 DOTween complete。如果目标对象还被其它原生 DOTween 直接操作，启动新动作前仍应按 DOTween 规则清理同目标旧 tween，例如 `rectTransform.DOKill(false)`。

## 工作台诊断

ActionKit 页面用于查看当前动作是否正在运行、是否卡住、是否忘记取消。

| 在工作台里看什么 | 用途 |
|---|---|
| 动作树 / 控制器列表 | 确认 `Start()` 后是否真的创建了动作。 |
| 运行状态 | 区分 Running、Paused、Completed、Cancelled。 |
| 最近动作事件 | 查看动作开始、完成、取消的顺序。 |
| Stack Trace 开关 | 短时间开启，用于定位是谁创建了长期未结束的动作。 |

排查顺序：先看控制器是否存在，再看状态是否暂停，最后看创建来源。对象销毁后仍有动作运行时，回到拥有者生命周期里补 `Cancel()`。

## 常见坑

| 问题 | 处理方式 |
|---|---|
| 动作不执行 | 确认调用了 `Start()`，宿主 Adapter 正在驱动 ActionKit。 |
| 对象销毁后回调报错 | 在 `OnDestroy()` 或停止生命周期中 `Cancel()`。 |
| 取消后完成回调没执行 | 这是预期语义；`Cancel()` 只清理，不触发 `OnFinish()` 或 `Start(onFinish)`。 |
| 想提前完成并设置最终值 | ActionKit 没有 controller 级 `Complete()`；手动设置最终状态，或保留具体 `IAction` / DOTween 引用使用对应完成语义。 |
| 同一目标移动互相覆盖 | 用一个 controller 管理一个互斥动作通道，启动新移动前先取消旧移动。 |
| Repeat 无限运行 | 设置次数或会变为 `false` 的 condition。 |
| Godot 下不动 | 确认 `GodotBootstrap` 或 installer 在 `_Process` 中 tick。 |
