## 进阶示例

> 本文面向已经掌握 [ActionKit 主页面](../Api/03-Tool/ActionKit.md) 的开发者，补充复杂组合、自定义 Action、互斥流程与诊断方式。

## 复杂嵌套示例

下面是一个“战斗开场演出”的完整编排：先锁输入并淡入 UI，同时播放字幕、预加载 Boss、闪烁提示；之后等待资源完成或超时；最后最多做三轮 Boss 脉冲动画，如果在某轮结束时已跳过，就不进入下一轮。任意时刻跳过或对象销毁时，都通过 controller 取消整棵动作树。

```csharp
using System.Numerics;
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

#if UNITY_EDITOR || (GODOT && TOOLS)
    public override string GetDebugInfo()
    {
        return "WaitCounter(" + mCurrentCount + "/" + mTargetCount + ")";
    }
#endif
}

ActionKit.Sequence()
    .Append(new WaitCounterAction(3))
    .Callback(OnCounterFinished)
    .Start();
```

`GetDebugInfo()` 只属于 Unity Editor / Godot Tools 诊断面，自定义 Action 的覆盖也必须使用相同整段宏，避免调试文本 API 进入 Player。项目内高频创建的自定义 Action 建议参考内置动作接入对象池，避免频繁分配。

## 互斥动作

同一个目标属性同一时间只应由一个动作通道控制。角色移动、UI 透明度、音量渐变等都适合保存当前 controller，启动新动作前取消旧动作。

```csharp
private IActionController mMoveController;

private void MoveTo(Vector3 from, Vector3 to)
{
    if (mMoveController != null)
    {
        mMoveController.Cancel();
        mMoveController = null;
    }

    mMoveController = ActionKit.Sequence()
        .Lerp01(0.7f, t => SetPosition(Vector3.Lerp(from, to, t)))
        .Start(_ => mMoveController = null);
}
```

需要“提前完成并落最终状态”时，不要期待 `Cancel()` 替你触发完成逻辑。先显式写最终状态，再取消 controller。

```csharp
private void CompleteMoveImmediately(Vector3 target)
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
| 活动根列表 | 确认 `Start()` 后是否真的创建了根控制器，并按 ID、状态或摘要检索。 |
| 递归 Action 树 | 通过 Sequence、Parallel、Repeat 导轨确认当前执行路径、并行分支和循环轮次。 |
| 运行状态 | 区分等待、执行、暂停、完成和取消。 |
| 节点详情 | 查看选中节点的类型、执行器、时间源、进度、深度和诊断摘要。 |
| 最近终态 | 查看动作完成、取消和故障的顺序。 |
| Stack Trace 开关 | 短时间开启，用于定位是谁创建了长期未结束的动作。 |
| `FrameCount` / `FinishedCount` / `CancelledCount` | 判断调度器是否仍在推进，以及完成/取消是否增长。 |

页面在紧凑宽度下显示“活动根 + 动作树”，节点详情与 Start 调用帧、最近终态一起进入底部可调诊断抽屉；宽屏时节点详情移到右侧 Inspector，抽屉继续承载调用帧与终态。深层组合树的缩进会在第六级封顶，并通过 `L7/L8/...` 显示真实深度，节点名称和状态不会因为嵌套过深而丢失。

排查顺序：先看活动根是否存在，再沿组合语义导轨确认当前执行路径，然后检查暂停、循环轮次和创建来源。对象销毁后仍有动作运行时，回到拥有者生命周期里补 `Cancel()`。

`Start()`、暂停/恢复、时间源切换和 Scheduler Tick 必须来自同一个宿主线程。其它线程只允许调用 `Cancel()` 提交原子请求；宿主线程取消仍在准备队列中的 controller 时，会在 `Cancel()` 返回前完成清理和终态，其它情况仍由 Scheduler Tick 串行终结。清理钩子或枚举器 Dispose 抛错时，ActionKit 会继续释放其余节点，并把终态记为 Faulted。

运行态通过通用 Tool Provider 发布 `ActionKit/state`，Workbench 与 CLI 按 telemetry、snapshot、显式 command 的顺序消费。周期刷新不发送命令；只有切换或清空 Start 堆栈时才发送 UserAction。

```powershell
& $YOKI telemetry read --engine <engineId> --kit ActionKit --name state --project <projectRoot>
& $YOKI snapshot read --engine <engineId> --kit ActionKit --name state --project <projectRoot>
& $YOKI command send --engine <engineId> --kit ActionKit --action stats --project <projectRoot>
& $YOKI command send --engine <engineId> --kit ActionKit --action set_stack_trace --payload '{"enabled":true}' --project <projectRoot>
```

当前 action 为两个 ReadOnly：`stats`、`get_workbench_snapshot`；两个 UserAction：`set_stack_trace`、`clear_stack_trace`。调用前仍以当前 `System/list_commands` 和宿主身份为准。Provider 在当前进程首次使用 ActionKit 后注册，因此静态安装态可用不等于尚未触达 ActionKit 的进程已经发布在线 action。

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
| Coroutine 里的 `WaitForSeconds` 不按秒等待 | 纯 `CoroutineAction` 不解释 Unity yield；一般计时用 `.Delay(seconds)`，必须使用 Unity yield 时改用 `ActionKitUnityCoroutine`。 |
| Task 取消后后台任务还在跑 | ActionKit 取消的是 controller；底层 Task 需要业务自己的 `CancellationTokenSource`。 |
| UniTask 需要跟随 ActionKit 取消 | 使用 `ActionKitUniTask.From(Func<CancellationToken, UniTask>)`；直接 UniTask 和无 token factory 不伪造取消能力。 |
| 暂停后异步工作仍在推进 | 暂停只门控 ActionKit 对终态的消费，已经提交给 Task、UniTask 或 Unity Coroutine 的工作由各自调度器继续推进。 |
| 后台线程直接 `Start()` 或暂停时报错 | 把调度与控制切回 Unity/Godot 宿主线程；后台线程只提交 `Cancel()`。 |
| 自定义 Action 无法启动 | 检查活动树内 Action ID 是否重复，以及树深度是否超过统一生命周期上限。 |
| Godot 下不动 | 确认 `GodotBootstrap` 或 installer 在 `_Process` 中 tick。 |
