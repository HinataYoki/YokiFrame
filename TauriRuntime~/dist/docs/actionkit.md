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
| Repeat 无限运行 | 设置次数或会变为 `false` 的 condition。 |
| Godot 下不动 | 确认 `GodotBootstrap` 或 installer 在 `_Process` 中 tick。 |
