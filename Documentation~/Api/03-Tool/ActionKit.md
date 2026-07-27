# ActionKit 动作编排

## 适用场景

ActionKit 是纯 C# 行为编排 Tool Kit。需要把等待、回调、条件、插值、异步任务和组合流程绑定到同一宿主生命周期时使用它；业务代码不需要直接引用 Unity 或 Godot 类型。

## 使用前提

ActionKit 可用于 Unity 与 Godot .NET Runtime。宿主负责驱动 scheduler；UniTask、DOTween 和 Unity Coroutine 是按需安装的扩展。Workbench 只读展示动作树和终态。

## 快速上手

```csharp
using YokiFrame;

IActionController controller = ActionKit.Sequence()
    .Callback(ShowLoading)
    .Delay(0.5f)
    .Condition(IsReady)
    .Lerp01(0.25f, SetProgress, OnFinished)
    .Start();

controller.Pause();
controller.Resume();
controller.Cancel();
```

首次使用门面会注册当前宿主的调度器。Unity PlayerLoop 或 Godot `_Process` 负责推进时间，业务不需要再创建第二个 scheduler。

## 核心 API

### `ActionKit`

| API | 说明 |
|---|---|
| `Sequence()` | 创建顺序容器。 |
| `Parallel(bool waitAll = true)` | 创建并行容器；`true` 等待全部分支，`false` 任一分支完成即结束。 |
| `Repeat(int repeatCount = -1, Func<bool> condition = null)` | 创建重复容器；次数小于等于 0 表示无限，condition 在每轮结束后判断。 |
| `Delay(float seconds, Action callback = null)` | 创建秒级延迟；负数、NaN 和 Infinity 会被拒绝。 |
| `DelayFrame(int frameCount, Action onDelayFinish = null)` | 创建按调度帧数计数的延迟；小于等于 0 时立即完成。 |
| `NextFrame(Action onNextFrame = null)` | `DelayFrame(1)` 的便捷入口。 |
| `Lerp(float a, float b, float duration, Action<float> onLerp, Action onLerpFinish = null)` | 创建 float 线性插值。 |
| `Callback(Action callback)` | 首次推进执行回调并完成。 |
| `Condition(Func<bool> condition)` | 条件为 true 后完成；null 条件会被拒绝。 |
| `Coroutine(Func<IEnumerator>)` / `Coroutine(IEnumerator)` | 包装 IEnumerator，不解释 Unity yield 指令。 |
| `Task(Func<Task>)` / `Task(Task)` | 包装 Task；Action 取消不自动取消底层 Task。 |

### UniTask Integration

安装 UniTask 后，`YOKIFRAME_UNITASK_SUPPORT` 启用独立 `YokiFrame.ActionKit.UniTask`：

```csharp
IActionController controller = ActionKitUniTask
    .From(async cancellationToken =>
    {
        await UniTask.Delay(500, cancellationToken: cancellationToken);
    })
    .Start();
```

| API | 说明 |
|---|---|
| `ActionKitUniTask.From(Func<UniTask>)` | 每次执行轮次创建 UniTask；ActionKit 只观察终态。 |
| `ActionKitUniTask.From(Func<CancellationToken, UniTask>)` | 每轮创建 token source；取消、故障、Repeat 重启和宿主重置时只取消仍活动任务，正常完成只释放资源。 |
| `ActionKitUniTask.From(UniTask)` / `UniTask.ToAction()` | 直接包装一次性 UniTask source；不得放入多轮 Repeat。 |
| `ISequence.UniTask(...)` | 向容器追加 UniTask factory Action。 |

没有恢复旧 `DelayUniTask`、`WaitUntil` 等重复门面：延时、帧等待和条件应继续使用 ActionKit 原生 `Delay`、`DelayFrame`、`Condition`，只有真实 UniTask 业务进入该 Integration。

### Unity 原生 Coroutine Adapter

`YokiFrame.ActionKit.Unity` 使用 Unity `StartCoroutine` 解释 `YieldInstruction`、`CustomYieldInstruction`、`AsyncOperation` 和嵌套 IEnumerator：

```csharp
IActionController controller = ActionKitUnityCoroutine
    .From(LoadWithUnityYield)
    .Start();

IEnumerator LoadWithUnityYield()
{
    yield return new WaitForSeconds(0.5f);
}
```

| API | 说明 |
|---|---|
| `ActionKitUnityCoroutine.From(Func<IEnumerator>)` | 每次执行轮次创建原生 Unity Coroutine，适用于 Repeat。 |
| `ActionKitUnityCoroutine.From(IEnumerator)` / `IEnumerator.ToUnityAction()` | 直接包装一次性枚举器；不得放入多轮 Repeat。 |
| `ISequence.UnityCoroutine(...)` | 向容器追加 Unity Coroutine factory Action。 |

纯 C# `ActionKit.Coroutine` 仍按每次宿主 Tick 推进，不解释 Unity yield；只有确实需要 Unity yield 语义时使用 Adapter。

### Fluent 容器

| API | 说明 |
|---|---|
| `ISequence.Append(IAction action)` | 追加尚未被其它父容器或 controller 拥有的 Action。 |
| `IParallel.Append(IAction action)` | 追加并行子 Action 并返回 `IParallel`。 |
| `ISequence.Sequence(Action<ISequence> configure = null)` | 创建并追加嵌套顺序容器。 |
| `ISequence.Parallel(Action<ISequence> configure, bool waitAll = true)` | 创建并追加嵌套并行容器。 |
| `ISequence.Repeat(Action<IRepeat> configure, int count = -1, Func<bool> condition = null)` | 创建并追加嵌套重复容器。 |
| `ISequence.Delay` / `DelayFrame` / `NextFrame` | 追加时间或帧等待。 |
| `ISequence.Callback` / `Condition` / `Lerp` / `Lerp01` | 追加回调、条件和插值节点。 |
| `ISequence.Coroutine` / `Task` | 追加 IEnumerator 或 Task 节点。 |
| `IEnumerator.ToAction()` / `Task.ToAction()` | 把已有异步对象包装成一次性 Action。 |

配置回调只应追加新建节点。重复 Append 同一个 Action、修改已启动容器、把树追加成环都会抛异常；框架不会静默复制节点。

### Action 与 controller

### `IAction`、`ActionBase`

| API | 说明 |
|---|---|
| `ActionID` | 当前执行租约的非零 ID；自定义 `IAction` 不继承 `ActionBase` 时必须自行提供。 |
| `ActionState` | `NotStart`、`Started`、`Finished`。取消和故障由 controller 表达。 |
| `Paused` / `Deinited` | 当前节点是否暂停、是否已经释放。 |
| `OnInit()` | 根启动或 Repeat 新一轮前重置状态。 |
| `OnStart()` | 当前轮首次推进时调用。 |
| `OnExecute(float dt)` | 每次推进调用；正常完成时调用 `Finish()`。 |
| `OnFinish()` | 只在正常完成时调用一次。 |
| `OnDeinit()` | 正常、取消、故障或宿主重置时清理引用。 |
| `ActionBase.OnPause()` / `OnResume()` | 暂停和恢复钩子。 |
| `ActionBase.OnUpdateModeChanged(ActionUpdateModes)` | 时间源变化钩子。 |

自定义节点最小形式：

```csharp
public sealed class WaitForFlag : ActionBase
{
    private readonly Func<bool> mReady;

    public WaitForFlag(Func<bool> ready)
    {
        mReady = ready ?? throw new ArgumentNullException(nameof(ready));
    }

    public override void OnExecute(float dt)
    {
        if (mReady()) this.Finish();
    }
}
```

### `IActionController`

| API | 说明 |
|---|---|
| `CurExecuteActionID` | 根 Action 的稳定租约 ID；controller 不会被复用给其它根。 |
| `Action` | 当前根 Action；终结清理后为 `null`。 |
| `UpdateMode` | `ScaledDeltaTime` 或 `UnscaledDeltaTime`。 |
| `Paused` | 读取或设置根树暂停状态。 |
| `IsCancelled` | 是否已请求或完成取消。 |
| `IsCompleted` | 是否已经离开 scheduler。 |
| `IsFaulted` | 是否因生命周期异常结束。 |
| `Finish` | 仅正常完成时调用的 controller 回调。 |
| `Cancel()` | 可从任意线程请求取消；宿主线程会同步清理仍在准备队列的 controller，其它情况由 Scheduler Tick 串行终结。 |

扩展 `Start(onFinish)` 会同步执行一次 dt=0 首推。`IAction.Update(dt)` 是脱离调度器时的手动推进入口，不能和 scheduler 同时持有。扩展 `Finish()` 只标记正常完成。`Pause`、`Resume` 和 `UpdateMode` 修改要求宿主线程。

### 内置节点语义

| 节点 | 语义 |
|---|---|
| `Callback` | 首次推进执行回调并完成，可能在 `Start()` 内完成。 |
| `Delay` | 按 controller 当前时间源累计秒数。 |
| `DelayFrame` | 按实际 scheduler 推进次数计数，与 dt 大小无关。 |
| `Condition` | 每次推进检查一次，满足后完成。 |
| `Lerp` | 首次输出起点，完成时输出精确终点；取消不会自动跳到终点。 |
| `Sequence` | 当前节点完成后推进下一个。 |
| `Parallel` | 同时推进子节点，按 `waitAll` 决定完成条件。 |
| `Repeat` | 原地重启内部子树；无限重复必须取消或让 condition 返回 false。 |
| `TaskAction` | 观察 Task 终态；异常或取消形成 fault/cancel，不伪装成 completed。 |
| `CoroutineAction` | 推进 IEnumerator 和嵌套 IEnumerator，不调用 Unity `StartCoroutine`。 |
| `UniTaskAction` | 在宿主 Tick 观察 awaiter；token factory 由 ActionKit 终态传播取消。 |
| `UnityCoroutineAction` | Unity 解释 yield，ActionKit Tick 观察完成/故障；取消执行 Stop 和一次性 Dispose。 |

## 生命周期与错误边界

宿主线程负责 Start、Pause、Resume 和时间推进；其它线程只提交 `IActionController.Cancel()`。动作树最大深度为 1024；同一 Action 不能同时属于两个父容器。

正常宿主线程负责 Start、Pause、Resume、时间源切换和 scheduler tick；其它线程只提交 `IActionController.Cancel()`。Action 生命周期钩子抛异常会形成一次 Faulted 终态并停止继续推进，清理仍由 scheduler 完成。

Task、UniTask 和 Unity Coroutine 都属于已经提交给外部调度器的异步工作。ActionKit 暂停会停止动作树消费终态，但不会伪装成能够冻结底层 Task、UniTask 或 Unity yield 计时；恢复后的下一 Tick 会处理已发生的终态。取消只有在 API 明确拥有取消能力时向下传播：UniTask token factory 请求 token 取消，Unity Coroutine 执行 Stop，普通 Task 仍由业务持有取消源。

UniTask、DOTween 和 Unity Coroutine 都是可选扩展；未安装时，纯 C# ActionKit 仍可使用。

## 实践模式

### 复杂组合

```csharp
private IActionController mIntro;

private void PlayIntro()
{
    StopIntro();

    mIntro = ActionKit.Sequence()
        .Callback(LockInput)
        .Parallel(parallel =>
        {
            parallel
                .Sequence(ui => ui.Lerp01(0.35f, SetCurtainAlpha))
                .Sequence(audio => audio.Delay(0.1f).Callback(PlayWarningSound));
        })
        .Condition(IsBossLoaded)
        .Callback(StartBattle)
        .Start(_ => mIntro = null);
}

private void StopIntro()
{
    mIntro?.Cancel();
    mIntro = null;
    UnlockInput();
}
```

`Sequence` 负责顺序，`Parallel` 负责同时推进，`Condition` 负责等待，`Repeat` 适合有限轮次的重复。无限 `Repeat` 必须保存 controller，并在流程结束或对象销毁时取消。

### 自定义 Action

```csharp
public sealed class WaitForFlag : ActionBase
{
    private readonly Func<bool> mReady;

    public WaitForFlag(Func<bool> ready)
    {
        mReady = ready ?? throw new ArgumentNullException(nameof(ready));
    }

    public override void OnExecute(float deltaTime)
    {
        if (mReady())
        {
            Finish();
        }
    }
}
```

自定义节点在 `OnExecute` 中决定何时 `Finish()`，在 `OnDeinit` 中释放引用。一个 Action 只能属于一个父容器或活动根；不要重复 Append 已经被拥有的节点。

### 互斥动作

同一个目标属性同时只保留一个 controller。启动新动作前先取消旧动作：

```csharp
private IActionController mMove;

private void MoveTo(float from, float to)
{
    mMove?.Cancel();
    mMove = ActionKit.Sequence()
        .Lerp(from, to, 0.7f, SetPosition)
        .Start(_ => mMove = null);
}
```

`Cancel()` 只结束动作树，不会自动调用正常完成回调，也不会替普通 `Task` 取消底层工作。需要到达最终值时，先显式设置最终状态，再取消 controller。

### 异步与宿主线程

- `Start`、`Pause`、`Resume`、时间源切换和 scheduler tick 在同一宿主线程执行。
- 其它线程只提交 `Cancel()`。
- 普通 `Task` 的取消权属于业务自己的 `CancellationTokenSource`。
- 使用 UniTask 时，优先使用接收 `CancellationToken` 的 factory，让动作取消能传递到任务。
- 纯 C# Coroutine 不解释 Unity `YieldInstruction`；需要 Unity yield 语义时使用 Unity Coroutine 接入。

## 在工具中查看

Workbench 可以只读查看活动根、动作树和终态；页面不会替业务启动、暂停或取消动作。

## 限制与相关资料

| 问题 | 处理 |
|---|---|
| 动作不动 | 检查宿主是否接入 Core FrameLoop；不要创建第二个 scheduler。 |
| `Start()` 后立刻完成 | `Callback`、零延迟和已满足的 `Condition` 的设计行为。 |
| 取消后仍执行完成回调 | 正常完成回调只对 completed 触发；检查 `IsCancelled` 和 `IsFaulted`。 |
| 取消 Task 后底层仍运行 | ActionKit 不拥有底层 Task 的取消权，请由业务传入 `CancellationToken`。 |
| 需要取消 UniTask | 使用 `ActionKitUniTask.From(Func<CancellationToken, UniTask>)`，不要用无 token factory。 |
| Coroutine 里的 `WaitForSeconds` 只等一帧 | 纯 C# Coroutine 不解释 Unity yield；Unity 项目改用 `ActionKitUnityCoroutine`。 |
| 暂停后外部异步仍完成 | 暂停只门控 ActionKit 观察；外部 Task、UniTask 和 Unity yield 继续由各自调度器推进。 |
| 自定义 Action 重复启动 | 一个 Action 只能属于一个父容器或活动根；新租约在 `OnInit` 重置状态。 |

复杂嵌套、自定义 Action 和互斥动作请先阅读本页“实践模式”。
