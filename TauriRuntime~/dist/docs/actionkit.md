# ActionKit 动作

ActionKit 用于组合延迟、回调、并行、重复、协程和 Task 等运行时动作。代码命名空间统一为 `YokiFrame`；当前核心调度位于纯 C# Runtime，Unity 和 Godot 适配器分别在宿主帧循环中驱动 tick。

## 基本概念

| 类型 | 作用 |
|------|------|
| `IAction` | 单个动作的接口，包含初始化、执行、完成和回收生命周期。 |
| `ISequence` | 顺序执行多个动作。 |
| `IParallel` | 并行执行多个动作，可选择等待全部完成或任一完成。 |
| `IRepeat` | 重复执行一组动作。 |
| `IActionController` | `Start()` 后返回的控制器，可暂停、恢复、取消。 |

## IAction 属性

| 属性 | 说明 |
|------|------|
| `ActionID` | 动作唯一标识。 |
| `ActionState` | 当前状态：`NotStart`、`Started`、`Finished`、`Cancelled`。 |
| `Paused` | 是否暂停。 |
| `Deinited` | 是否已释放。 |
| `GetDebugInfo()` | 获取调试信息字符串。 |

## IActionController 属性

| 属性 | 说明 |
|------|------|
| `CurExecuteActionID` | 当前正在执行的动作 ID。 |
| `Action` | 关联的 `IAction` 对象。 |
| `UpdateMode` | 更新模式：`ScaledDeltaTime` 或 `UnscaledDeltaTime`。 |
| `Paused` | 是否暂停。 |
| `IsCancelled` | 是否已取消。 |
| `Finish` | 完成回调。 |

动作通过对象池复用。正常使用时，不需要手动回收 Action；完成或取消后由框架回收。

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

`Start()` 默认使用 `ActionUpdateModes.ScaledDeltaTime`。需要不受 timeScale 影响时，可以修改控制器：

```csharp
var controller = ActionKit.Delay(1f, OnFinished).Start();
controller.UpdateMode = ActionUpdateModes.UnscaledDeltaTime;
```

## 并行动作

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

`waitAll: true` 表示全部子动作完成后才结束；`waitAll: false` 表示任一子动作完成后结束。

也可以直接创建并行动作：

```csharp
ActionKit.Parallel(waitAll: true)
    .Delay(0.5f)
    .Callback(OnFinished)
    .Start();
```

## 重复动作

```csharp
int count = 0;

ActionKit.Sequence()
    .Repeat(repeat =>
    {
        repeat.Callback(() => count++);
        repeat.Delay(0.2f);
    }, count: 3)
    .Start();
```

`ActionKit.Repeat(repeatCount, condition)` 和链式 `.Repeat(...)` 都支持重复次数。`repeatCount <= 0` 表示不按次数限制，通常应配合 `condition` 使用，避免无限运行。

## 常用叶子动作

| 动作 | 创建方式 | 说明 |
|------|----------|------|
| Delay | `ActionKit.Delay(seconds, callback)` 或 `.Delay(seconds, callback)` | 按秒延迟。 |
| DelayFrame | `ActionKit.DelayFrame(frameCount, callback)` 或 `.DelayFrame(frameCount, callback)` | 按帧延迟。 |
| NextFrame | `ActionKit.NextFrame(callback)` 或 `.NextFrame(callback)` | 下一帧执行。 |
| Callback | `ActionKit.Callback(callback)` 或 `.Callback(callback)` | 立即执行回调并完成。 |
| Condition | `.Condition(condition)` | 条件满足时完成。 |
| Lerp | `ActionKit.Lerp(...)` 或 `.Lerp(...)` | 按时间插值。 |
| Lerp01 | `.Lerp01(duration, onLerp, onFinish)` | 从 0 到 1 插值。 |

插值示例：

```csharp
ActionKit.Sequence()
    .Lerp01(
        duration: 0.5f,
        onLerp: t => canvasGroup.alpha = t,
        onLerpFinish: OnFadeInFinished)
    .Start();
```

## Coroutine 和 Task

```csharp
ActionKit.Sequence()
    .Coroutine(LoadRoutine)
    .Task(LoadRemoteConfigAsync)
    .Callback(OnLoaded)
    .Start();
```

也可以把已有对象转成 Action：

```csharp
IEnumerator routine = LoadRoutine();
IAction routineAction = routine.ToAction();

Task task = LoadRemoteConfigAsync();
IAction taskAction = task.ToAction();
```

Task 动作内部通过返回 `Task` 的工作方法执行；异常会被捕获、输出到宿主日志，并让动作进入结束流程。

## 控制器

```csharp
IActionController controller = ActionKit.Delay(1f, OnFinished).Start();

controller.Pause();
controller.Resume();
controller.TogglePause();
controller.Cancel();
```

注意：

- `Cancel()` 会取消后续执行并触发清理。
- `Pause()` 只暂停当前控制器关联的 Action。
- 拥有者销毁时建议取消仍在运行的控制器，避免回调访问已销毁对象。

## 常见问题

| 问题 | 处理方式 |
|------|----------|
| 动作没有继续执行 | 确认已调用 `Start()`，并且控制器没有处于暂停状态。 |
| 对象销毁后回调报错 | 在 `OnDestroy()` 或对应生命周期中调用 `Cancel()`。 |
| Repeat 无限运行 | 给 `Repeat` 设置次数，或提供会变为 `false` 的 condition。 |
| Godot 下动作不执行 | 确认 `GodotBootstrap` 或 `GodotActionKitInstaller.TickActionKit()` 已在 `_Process` 中驱动调度器。 |
