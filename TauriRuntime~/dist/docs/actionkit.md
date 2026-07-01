# ActionKit 动作

## 定位

ActionKit 是 YokiFrame 的轻量级行为编排系统，用来把“等待、回调、插值、异步任务、顺序流程、并行流程、重复流程”组合成一棵可暂停、可取消、可诊断的 Action 树。

它的核心代码不依赖 Unity 或 Godot。具体宿主只负责把每帧的 `deltaTime` 喂给 `ActionKitScheduler`：Unity 由 `UnityActionKitInstaller` / PlayerLoop 驱动，Godot 由 `GodotActionKitInstaller` 在 `_Process` 中驱动。业务层通常只需要创建 Action，然后调用 `Start()`。

```csharp
using YokiFrame;

private IActionController mController;

private void PlayIntro()
{
    mController = ActionKit.Sequence()
        .Callback(OnIntroStarted)
        .Delay(0.5f)
        .Lerp01(0.3f, t => SetFade(t))
        .Callback(OnIntroFinished)
        .Start();
}

private void StopIntro()
{
    if (mController != null)
    {
        mController.Cancel();
        mController = null;
    }
}
```

## 类型地图

### 门面和基础契约

| 类型 | 介绍 | 常见用法 |
|---|---|---|
| `ActionKit` | 静态门面，负责创建各种 Action。 | `ActionKit.Sequence()`、`ActionKit.Delay(1f, callback)`、`ActionKit.Task(LoadAsync)`。 |
| `IAction` | 所有可调度动作的基础契约，包含状态、暂停、初始化、执行、完成和释放钩子。 | 自定义动作或保存具体 Action 引用时使用。 |
| `ActionBase` | `IAction` 的抽象基类，封装公共状态。 | 自定义复杂 Action 时继承它，重写 `OnStart()`、`OnExecute()`、`OnDeinit()`。 |
| `ActionStatus` | Action 生命周期状态。 | `NotStart`、`Started`、`Finished`；暂停不是状态，而是 `Paused` 标记。 |
| `ActionUpdateModes` | 控制器使用的时间源。 | `ScaledDeltaTime` 受宿主时间缩放影响，`UnscaledDeltaTime` 不受影响。 |
| `IActionController` | `Start()` 返回的控制器。 | 暂停、恢复、取消、切换时间源、在生命周期结束时清理。 |

### 组合容器

| 类型 | 介绍 | 适合场景 |
|---|---|---|
| `ISequence` / `Sequence` | 顺序执行子 Action，当前一个完成后才进入下一个。 | 新手引导、剧情步骤、UI 打开/关闭流程、战斗阶段编排。 |
| `IParallel` / `Parallel` | 同时推进多个子 Action。`waitAll` 为 `true` 时等待全部完成，为 `false` 时任一完成即结束。 | 多个 UI 动画同时播放、加载和倒计时竞速、多个提示并发。 |
| `IRepeat` / `Repeat` | 重复执行内部序列。`repeatCount <= 0` 表示不限次数，可叠加 `condition`。 | 轮询、闪烁、循环提示、直到条件变化前重复执行。 |

### 基础动作

| 类型 | 介绍 | 创建方式 |
|---|---|---|
| `Callback` | 立即执行回调并完成。由于 `Start()` 会先用 `dt = 0` 推进一次，单独的 `Callback` 可能在 `Start()` 调用内完成。 | `ActionKit.Callback(OnDone)` 或 `.Callback(OnDone)`。 |
| `Delay` | 按秒等待，结束时可触发回调。 | `ActionKit.Delay(1f, OnDone)` 或 `.Delay(1f, OnDone)`。 |
| `DelayFrame` | 按帧等待，不关心秒级时间。 | `ActionKit.DelayFrame(3, OnDone)` 或 `.DelayFrame(3, OnDone)`。 |
| `NextFrame` | `DelayFrame(1)` 的便捷写法。 | `ActionKit.NextFrame(OnDone)` 或 `.NextFrame(OnDone)`。 |
| `Condition` | 等到条件为 `true` 时完成，相当于 Wait Until。 | `.Condition(() => isReady)`。 |
| `Lerp` | 在指定时长内做 float 插值，完成时会回调目标值。 | `ActionKit.Lerp(0f, 1f, 0.5f, Apply)` 或 `.Lerp01(0.5f, Apply)`。 |

### 异步和可选扩展

| 类型 | 介绍 | 注意点 |
|---|---|---|
| `CoroutineAction` | 每帧推进一个 `IEnumerator`，并支持嵌套 `IEnumerator`。 | 它不是 Unity `StartCoroutine`，不会解释 `WaitForSeconds` 等 Unity yield 指令；计时等待优先用 `.Delay()`。 |
| `TaskAction` | 包装 `System.Threading.Tasks.Task`，Task 完成后 Action 完成。 | `Cancel()` 只释放 ActionKit 控制器，不会自动取消底层 Task；底层任务需要自己接入取消令牌。 |
| `DOTweenAction` | Unity 可选扩展，把 DOTween `Tween` 包装进 ActionKit。 | 需要 DOTween 支持宏和对应 Adapter；取消时默认 `Kill(false)`，不会跳到终点。 |

### 调度和诊断

| 类型 | 介绍 | 使用边界 |
|---|---|---|
| `ActionKitScheduler` | 跨引擎调度核心，维护等待队列、执行列表、完成数、取消数和帧计数。 | 普通业务不要直接 Tick；自定义宿主或 Adapter 才需要调用 `Tick()` / `ProcessRecycle()`。 |
| `ActionEditorHooks` | Action 开始/完成时的编辑器钩子。 | 主要给工作台和编辑器诊断使用。 |
| `ActionStackTraceService` | 记录 `Start()` 调用堆栈。 | 默认关闭，只在诊断面板短时间打开，避免长期堆栈开销。 |

## 基础动作

### Delay / DelayFrame / NextFrame

`Delay` 适合秒级节奏，例如 UI 动画、提示停留、技能前摇。`DelayFrame` 适合必须跨过若干调度帧的逻辑，例如等一帧让状态写入完成。`NextFrame` 是最常见的帧等待。

```csharp
ActionKit.Sequence()
    .Callback(() => ShowTip("Ready"))
    .Delay(0.25f)
    .Callback(() => ShowTip("Go"))
    .DelayFrame(2)
    .NextFrame(() => EnableInput())
    .Start();
```

### Callback

`Callback` 用来把普通方法接入动作树。它会立即调用并完成，因此放在 `Sequence` 中通常用于切换状态、发事件、写日志或触发一个不需要等待的业务动作。

```csharp
ActionKit.Sequence()
    .Callback(() => EventKit.Type.Send(new BattleStartedEvent()))
    .Delay(1f)
    .Callback(StartEnemyWave)
    .Start();
```

### Condition

`Condition` 每次调度都会检查条件，条件为 `true` 时完成。它适合等待资源标记、UI 状态、网络结果或其它业务布尔值。

```csharp
ActionKit.Sequence()
    .Callback(BeginLoad)
    .Condition(() => mLoadFinished)
    .Callback(EnterGame)
    .Start();
```

如果要表达 “Wait While”，把条件反过来写即可：

```csharp
ActionKit.Sequence()
    .Condition(() => !mIsLoading)
    .Callback(OnLoadingStopped)
    .Start();
```

### Lerp / Lerp01

`Lerp` 用来驱动一个 float 值从 A 到 B。`Lerp01` 是从 0 到 1 的便捷形式，适合把进度传给 UI、音量、透明度或自定义曲线。

```csharp
ActionKit.Sequence()
    .Lerp(0f, 100f, 0.8f, value => SetHealthBar(value))
    .Lerp01(0.25f, t => SetFlashAlpha(1f - t), OnFlashFinished)
    .Start();
```

`Cancel()` 不会触发 `Lerp.OnFinish()`，因此不会自动把值推到终点。需要提前结束并落最终状态时，先显式设置最终值，再取消旧 controller。

## 组合容器

`Sequence`、`Parallel`、`Repeat` 的配置闭包都建议继续使用链式调用：把闭包参数当作当前容器的 builder，从它开始一路 `.Delay()`、`.Callback()`、`.Parallel()` 接下去。这样示例和真实业务代码会更接近 ActionKit 的 fluent API 风格。只有需要 `if` / `for` 动态装配、提前 return 或复用临时 Action 时，再拆成多条语句。

### Sequence

`Sequence` 按顺序执行所有子 Action。它是最常用的流程编排容器。

```csharp
IActionController controller = ActionKit.Sequence()
    .Callback(LockInput)
    .Delay(0.2f)
    .Callback(PlayOpenSound)
    .Lerp01(0.3f, t => SetMenuProgress(t))
    .Callback(UnlockInput)
    .Start();
```

也可以用 `.Append()` 追加已经创建好的 Action：

```csharp
IAction fadeIn = ActionKit.Lerp(0f, 1f, 0.2f, SetAlpha);

ActionKit.Sequence()
    .Append(fadeIn)
    .Delay(1f)
    .Append(ActionKit.Lerp(1f, 0f, 0.2f, SetAlpha))
    .Start();
```

### Parallel

`Parallel` 同时推进多个子 Action。默认 `waitAll: true`，全部子 Action 都完成后才完成自身。

```csharp
ActionKit.Sequence()
    .Parallel(parallel =>
    {
        parallel
            .Lerp01(0.25f, t => SetPanelAlpha(t))
            .Lerp(0f, 32f, 0.25f, y => SetPanelOffset(y))
            .Delay(0.1f, PlayOpenSound);
    })
    .Callback(OnPanelOpened)
    .Start();
```

`waitAll: false` 适合竞速：任意一个子 Action 先完成，整个 `Parallel` 就结束。常见例子是“资源加载完成或超时，谁先到就进入下一步”。

```csharp
ActionKit.Sequence()
    .Parallel(parallel =>
    {
        parallel
            .Condition(() => mLoadFinished)
            .Delay(3f, () => mLoadTimeout = true);
    }, waitAll: false)
    .Callback(() =>
    {
        if (mLoadTimeout)
            ShowRetry();
        else
            ShowContent();
    })
    .Start();
```

### Repeat

`Repeat` 内部持有一段序列，序列跑完一轮后判断是否继续。`count` 小于等于 0 表示无限重复，必须有外部 `Cancel()` 或会变为 `false` 的 `condition`。

```csharp
int tickCount = 0;

IActionController controller = ActionKit.Sequence()
    .Repeat(repeat =>
    {
        repeat
            .Callback(() => tickCount++)
            .Delay(0.2f);
    }, count: 5)
    .Callback(() => ShowTip("Done"))
    .Start();
```

`condition` 是每轮结束后判断的继续条件；它不会阻止第一轮开始。因此如果第一轮也需要受条件保护，把 `Condition` 放在 `Repeat` 前面。

```csharp
ActionKit.Sequence()
    .Condition(() => mCanPulse)
    .Repeat(repeat =>
    {
        repeat
            .Lerp(0.8f, 1f, 0.1f, SetScale)
            .Lerp(1f, 0.8f, 0.1f, SetScale);
    }, condition: () => mCanPulse)
    .Start();
```

## 异步动作

### CoroutineAction

`CoroutineAction` 每帧推进一次 `IEnumerator`，适合把轻量状态机或跨帧流程接入 ActionKit。它支持 `yield return` 另一个 `IEnumerator` 作为嵌套流程。

```csharp
ActionKit.Sequence()
    .Callback(BeginWarmup)
    .Coroutine(WarmupRoutine)
    .Callback(EndWarmup)
    .Start();

private IEnumerator WarmupRoutine()
{
    for (int i = 0; i < 3; i++)
    {
        StepWarmup(i);
        yield return null;
    }

    yield return NestedRoutine();
}

private IEnumerator NestedRoutine()
{
    ApplyNestedStep();
    yield return null;
}
```

如果需要等待秒数，不要在这里写 `yield return new WaitForSeconds(1f)`；ActionKit 的 `CoroutineAction` 不解释 Unity yield 指令。用 `.Delay(1f)` 放在动作树里更明确，也更跨引擎。

### TaskAction

`TaskAction` 适合包装项目中已有的 `Task` 异步方法。Task 正常完成或异常被记录后，Action 都会结束。

```csharp
ActionKit.Sequence()
    .Callback(ShowLoading)
    .Task(LoadProfileAsync)
    .Callback(HideLoading)
    .Start();

private async System.Threading.Tasks.Task LoadProfileAsync()
{
    await mProfileService.LoadAsync();
}
```

如果底层任务需要取消，请在业务层持有 `CancellationTokenSource`，并在取消 ActionKit controller 时同步取消它。

```csharp
private IActionController mLoadController;
private System.Threading.CancellationTokenSource mLoadCts;

private void StartLoad()
{
    StopLoad();

    mLoadCts = new System.Threading.CancellationTokenSource();
    mLoadController = ActionKit.Task(() => mProfileService.LoadAsync(mLoadCts.Token))
        .Start(_ =>
        {
            mLoadController = null;
            mLoadCts.Dispose();
            mLoadCts = null;
        });
}

private void StopLoad()
{
    if (mLoadCts != null)
    {
        mLoadCts.Cancel();
        mLoadCts.Dispose();
        mLoadCts = null;
    }

    if (mLoadController != null)
    {
        mLoadController.Cancel();
        mLoadController = null;
    }
}
```

### DOTweenAction

安装 DOTween 并启用对应可选支持后，Unity 侧可以把 `Tween` 包进 ActionKit，让 Tween 和其它 Action 一起参与序列、并行、取消和诊断。

```csharp
using DG.Tweening;
using YokiFrame;

ActionKit.Sequence()
    .Callback(() => mPanelCanvas.alpha = 0f)
    .DOTween(mPanelCanvas.DOFade(1f, 0.25f))
    .Delay(1f)
    .DOTween(mPanelCanvas.DOFade(0f, 0.25f))
    .Start();
```

默认 `killOnCancel: true`。取消时会 `Kill(false)`，不会把 Tween 补到终点，也不会触发 DOTween complete。需要最终状态时先手动设置，或用 DOTween 自己的完成语义。

## 控制器和生命周期

`Start()` 返回 `IActionController`。它是业务代码管理动作生命周期的入口。

```csharp
IActionController controller = ActionKit.Delay(1f, OnFinished).Start();

controller.Pause();
controller.Resume();
controller.TogglePause();
controller.Cancel();
```

不受宿主时间缩放影响：

```csharp
controller.UpdateMode = ActionUpdateModes.UnscaledDeltaTime;
```

`Start(onFinish)` 会创建 controller 并交给 `ActionKitScheduler`。调度器会立刻用 `dt = 0` 推进一次，所以 `Callback`、空 `Sequence`、已经满足的 `Condition` 等可能在 `Start()` 调用内直接完成。

正常完成时，Action 内部会进入 `ActionStatus.Finished`，然后调用当前 Action 的 `OnFinish()`，调度器再触发 `Start(onFinish)` 传入的完成回调，最后执行 `OnEnd()` / `OnDeinit()` 并等待回收。只有正常完成会触发 `Start(onFinish)`。

`Cancel()` 表示取消并释放 controller，不表示完成。取消会执行 `OnDeinit()`，但不会调用当前 Action 的 `OnFinish()`，也不会调用 `Start(onFinish)` 的完成回调。

## 复杂嵌套示例

下面是一个“战斗开场演出”的完整编排：先锁输入并淡入 UI，同时播放字幕、预加载 Boss、闪烁提示；之后等待资源完成或超时；最后最多做三轮 Boss 脉冲动画，如果在某轮结束时已跳过，就不进入下一轮。任意时刻跳过或对象销毁时，都通过 controller 取消整棵动作树。

```csharp
using YokiFrame;

private IActionController mIntroController;
private bool mBossLoaded;
private bool mSkipIntro;

private void PlayBattleIntro()
{
    StopBattleIntro();
    mBossLoaded = false;
    mSkipIntro = false;

    mIntroController = ActionKit.Sequence()
        .Callback(() =>
        {
            LockInput();
            SetCurtainAlpha(0f);
            BeginLoadBoss();
        })
        .Parallel(parallel =>
        {
            parallel
                .Sequence(ui =>
                {
                    ui
                        .Lerp01(0.35f, t => SetCurtainAlpha(t))
                        .Callback(() => ShowTitle("WARNING"))
                        .Delay(0.4f)
                        .Lerp(1f, 0f, 0.2f, t => SetTitleAlpha(t));
                })
                .Sequence(audio =>
                {
                    audio
                        .Delay(0.1f)
                        .Callback(PlayWarningSound)
                        .Delay(0.5f)
                        .Callback(PlayBossTheme);
                })
                .Repeat(flashes =>
                {
                    flashes
                        .Callback(() => SetWarningVisible(true))
                        .Delay(0.08f)
                        .Callback(() => SetWarningVisible(false))
                        .Delay(0.08f);
                }, count: 4);
        }, waitAll: true)
        .Parallel(parallel =>
        {
            parallel
                .Condition(() => mBossLoaded)
                .Delay(2f, () => mSkipIntro = true);
        }, waitAll: false)
        .Repeat(pulse =>
        {
            pulse
                .Parallel(parallel =>
                {
                    parallel
                        .Lerp(0.9f, 1.08f, 0.12f, SetBossScale)
                        .Lerp(0.2f, 1f, 0.12f, SetBossShadow);
                })
                .Parallel(parallel =>
                {
                    parallel
                        .Lerp(1.08f, 1f, 0.18f, SetBossScale)
                        .Lerp(1f, 0.2f, 0.18f, SetBossShadow);
                })
                .Delay(0.05f);
        }, count: 3, condition: () => !mSkipIntro)
        .Callback(() =>
        {
            HideTitle();
            UnlockInput();
            StartBattle();
        })
        .Start(_ => mIntroController = null);
}

private void StopBattleIntro()
{
    if (mIntroController != null)
    {
        mIntroController.Cancel();
        mIntroController = null;
    }

    SetWarningVisible(false);
    UnlockInput();
}
```

这个例子里同时用到了 `Sequence`、`Parallel`、`Repeat`、`Delay`、`Condition`、`Callback` 和 `Lerp`：

| 片段 | 作用 |
|---|---|
| 顶层 `Sequence` | 串起“准备 -> 并行动画 -> 等加载/超时 -> 脉冲 -> 收尾”。 |
| 第一段 `Parallel(waitAll: true)` | UI、音频、闪烁提示同时执行，全部完成后进入下一步。 |
| 第二段 `Parallel(waitAll: false)` | Boss 加载完成或 2 秒超时，任意一个先到就继续。 |
| `Repeat(..., count: 3, condition: () => !mSkipIntro)` | 最多脉冲三轮，但超时或跳过后不继续下一轮。 |
| `StopBattleIntro()` | 控制整棵动作树的互斥和生命周期清理。 |

## 自定义 Action

当内置动作无法表达你的流程时，可以继承 `ActionBase`。自定义 Action 应该在 `OnExecute()` 中决定何时 `this.Finish()`，并在 `OnDeinit()` 中释放引用或归还对象池。

```csharp
public sealed class WaitCounterAction : ActionBase
{
    private readonly int mTargetCount;
    private int mCurrentCount;

    public WaitCounterAction(int targetCount)
    {
        mTargetCount = targetCount;
    }

    public override void OnInit()
    {
        base.OnInit();
        mCurrentCount = 0;
    }

    public override void OnExecute(float dt)
    {
        mCurrentCount++;
        if (mCurrentCount >= mTargetCount)
            this.Finish();
    }

    public override void OnDeinit()
    {
    }

    public override string GetDebugInfo()
    {
        return "WaitCounter(" + mCurrentCount + "/" + mTargetCount + ")";
    }
}

ActionKit.Sequence()
    .Append(new WaitCounterAction(3))
    .Callback(OnCounterFinished)
    .Start();
```

项目内高频创建的自定义 Action 建议参考内置动作接入对象池，避免频繁分配。

## 互斥动作

同一个目标属性同一时间只应由一个动作通道控制。角色移动、UI 透明度、音量渐变等都适合保存当前 controller，启动新动作前取消旧动作。

```csharp
private IActionController mMoveController;

private void MoveTo(YokiVector3 from, YokiVector3 to)
{
    if (mMoveController != null)
    {
        mMoveController.Cancel();
        mMoveController = null;
    }

    mMoveController = ActionKit.Sequence()
        .Lerp01(0.7f, t => SetPosition(YokiVector3.Lerp(from, to, t)))
        .Start(_ => mMoveController = null);
}
```

需要“提前完成并落最终状态”时，不要期待 `Cancel()` 替你触发完成逻辑。先显式写最终状态，再取消 controller。

```csharp
private void CompleteMoveImmediately(YokiVector3 target)
{
    SetPosition(target);

    if (mMoveController != null)
    {
        mMoveController.Cancel();
        mMoveController = null;
    }
}
```

## 工作台诊断

ActionKit 页面用于查看当前动作是否正在运行、是否卡住、是否忘记取消。

| 在工作台里看什么 | 用途 |
|---|---|
| 动作树 / 控制器列表 | 确认 `Start()` 后是否真的创建了动作。 |
| 运行状态 | 区分等待、执行、暂停、完成和取消。 |
| 最近动作事件 | 查看动作开始、完成、取消的顺序。 |
| Stack Trace 开关 | 短时间开启，用于定位是谁创建了长期未结束的动作。 |
| `FrameCount` / `FinishedCount` / `CancelledCount` | 判断调度器是否仍在推进，以及完成/取消是否增长。 |

排查顺序：先看控制器是否存在，再看状态是否暂停，然后看创建来源。对象销毁后仍有动作运行时，回到拥有者生命周期里补 `Cancel()`。

## 常见坑

| 问题 | 处理方式 |
|---|---|
| 动作不执行 | 确认调用了 `Start()`，并确认宿主 Adapter 正在驱动 ActionKit。 |
| `Callback` 在 `Start()` 内立刻执行 | 这是预期行为；`Start()` 会先用 `dt = 0` 推进一次。 |
| 对象销毁后回调报错 | 保存 controller，并在 `OnDisable()`、`OnDestroy()` 或业务停止生命周期中 `Cancel()`。 |
| 取消后完成回调没执行 | 这是预期语义；`Cancel()` 只清理，不触发 `OnFinish()` 或 `Start(onFinish)`。 |
| 想提前完成并设置最终值 | 手动设置最终状态，或保留具体 `IAction` / DOTween 引用使用对应完成语义。 |
| 同一目标移动互相覆盖 | 用一个 controller 管理一个互斥动作通道，启动新动作前先取消旧动作。 |
| `Repeat` 无限运行 | 设置次数，或设置会变为 `false` 的 `condition`，或保存 controller 后显式取消。 |
| `Repeat` 条件为 false 仍执行了一次 | `condition` 在每轮结束后判断；如果第一轮也要受保护，先放一个 `Condition`。 |
| Coroutine 里的 `WaitForSeconds` 不按秒等待 | `CoroutineAction` 不解释 Unity yield 指令；用 `.Delay(seconds)` 表达计时。 |
| Task 取消后后台任务还在跑 | ActionKit 取消的是 controller；底层 Task 需要业务自己的 `CancellationTokenSource`。 |
| Godot 下不动 | 确认 `GodotBootstrap` 或 installer 在 `_Process` 中 tick。 |
