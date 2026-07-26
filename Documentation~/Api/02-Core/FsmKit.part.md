## 实践与诊断

FsmKit 是 YokiFrame Core 中的纯 C# 状态机能力，提供普通 FSM、带参 FSM 和只读诊断。Unity 与 Godot 只负责生命周期接入，不改变公开 API。

## 最小示例

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
        var context = new GameFlowContext();
        var fsm = new FSM<GameFlowState>("GameFlow");
        fsm.Add(GameFlowState.Menu, new MenuState(fsm, context));
        fsm.Start(GameFlowState.Menu);
        return fsm;
    }
}
```

首个 `Add` 会建立默认选择，但不会自动进入 Running。使用 `Start()` 或 `Start(state)` 启动；只有 Running 状态机能够通过 `Change` 切换和转发 tick。

## 生命周期

```text
Add -> Start -> Update/FixedUpdate/CustomUpdate
             -> Suspend -> Resume
             -> Change: old End -> new Start
             -> End
Clear: End -> Dispose all states -> reset
Dispose: unregister diagnostics -> Clear
```

- `Condition()` 在 Start 或 Change 前执行；返回 false 时保持当前状态。
- `End()` 保留最近选择，允许后续再次 `Start()`。
- `Clear()` 释放全部业务状态；Editor/Tools 观察分支同时清除选择和诊断历史，Player 不编译这部分状态。
- `Dispose()` 首次调用结束、释放并注销，重复调用保持幂等；之后复用该实例会抛出 `ObjectDisposedException`。
- 生命周期回调执行期间不能嵌套修改同一 FSM；回调异常会继续抛出，并把机器状态收敛到 `End`。
- `Suspend()` 停止 tick 与消息转发；`Resume()` 只把机器恢复为 `Running`，不重复触发进入逻辑。
- 状态机不拥有引擎生命周期。宿主 owner 必须转发 tick，并在退出时调用 `Dispose()`。

## Unity

```csharp
using UnityEngine;
using YokiFrame;

public sealed class GameFlowDriver : MonoBehaviour
{
    private FSM<GameFlowState> mFsm;

    private void Awake()
    {
        mFsm = new FSM<GameFlowState>("GameFlow");
        var context = new GameFlowContext();
        mFsm.Add(GameFlowState.Menu, new MenuState(mFsm, context));
        mFsm.Start(GameFlowState.Menu);
    }

    private void Update()
    {
        mFsm.Update();
    }

    private void OnDestroy()
    {
        mFsm.Dispose();
    }
}
```

把 `MonoBehaviour` 限制为生命周期和输入编排；状态规则、黑板与切换条件继续保留在普通 C# 类型中。

## Godot

```csharp
using Godot;
using YokiFrame;

public partial class GameFlowDriver : Node
{
    private FSM<GameFlowState> mFsm;

    public override void _Ready()
    {
        mFsm = new FSM<GameFlowState>("GameFlow");
        var context = new GameFlowContext();
        mFsm.Add(GameFlowState.Menu, new MenuState(mFsm, context));
        mFsm.Start(GameFlowState.Menu);
    }

    public override void _Process(double delta)
    {
        mFsm.Update();
    }

    public override void _ExitTree()
    {
        mFsm.Dispose();
    }
}
```

Godot 支持 .NET 版本，当前验证基线为 4.7 .NET。FsmKit Core API 不引用 Godot；Node 只承担 `_Ready`、`_Process` 和 `_ExitTree` 映射。

## Workbench 诊断

FsmKit 通过 `FsmKitInteractionProvider` 接入所有 Kit 共用的 `YokiFrameKitInteractionRegistry`。Provider 复用 `FsmKitCommandHandler`、诊断注册表和 JSON writer；Unity/Godot 宿主只遍历 Registry，不包含 FsmKit 专用字段或分支。`instanceId`、转换历史和状态事件仍是 FsmKit 自有 payload 语义，不属于所有 Kit 的公共字段。

FsmKit 页面以 `instanceId` 选择实例，同名 FSM 不会互相覆盖。页面使用三条职责独立的数据路径，不把它们串成每轮依次执行的 fallback：

1. 1 秒 dashboard 读取 `FsmKit/state` 总览，并在 Shared Memory 不可用时使用 FileBridge snapshot；它只更新实例摘要和宿主身份，不覆盖已经取得的当前实例详情。
2. 用户显式选择实例时最多发送一次 `FsmKit/get_workbench_snapshot` 补齐首次详情；选择改变、宿主改变、Telemetry 抵达或窗口关闭会取消过期查询，晚到 command 不能覆盖更新的 Telemetry。
3. 当前实例使用独立 100ms 后台循环读取 `FsmKit/<instanceId>` 命名 Shared Memory，持续更新完整图与有界历史，不持续发送 command。

页面只读展示左侧实例列表与搜索、中间 MachineState/当前状态和大尺寸已观测转换图、右侧转换历史。左侧使用稳定 `ObservableCollection`，列表项按 `instanceId` 复用，刷新与搜索不会通过替换 `ItemsSource` 丢失当前选择。图是单个自绘 `Control`：模型或主题变化时生成 `DrawingGroup` 快照，等价模型和普通 `Render` 不重建节点、边或整棵视觉树；嵌套 FSM 节点用机器递归路径和状态标识作为内部身份，父子同名显示不会合并。每个 `states[]` 节点的 `entryCount` 由 Editor/Tools 观察分支在清理后持续累计，节点显示“进入 N 次”；它不从有界历史反推，Player 不维护该计数。右侧历史使用稳定集合增量同步，追加和有界窗口滚动不整体替换 `ItemsSource`。

高频读取先比较并验证 header；未变化帧在复制 payload、CRC、UTF-8、JSON 和 UI 调度前短路。Client 以 segment + generation 绑定轻量 map/accessor lease，同代复用，代次变化、读取失败、缓存淘汰或窗口结束时释放；预热后的空闲读取不产生托管分配。游标绑定 engine/session/generation/instance，同一身份内只以 `sequence` 判断新帧，写入时间只用于诊断。`Writing/HalfWrite` 不推进游标并在下一次 100ms 周期重试；命名段不可用、generation mismatch、协议坏帧或读取异常会暂停高频请求，等待 1 秒 dashboard 重新确认通道。底层协议拒绝不会采用不可信 header 游标；只有协议已经接受、随后被 JSON parser 或实例身份校验拒绝的稳定帧才允许负向推进，避免重复解析同一坏 payload。

Provider 状态变化会推动宿主在下一次 update 发布。Editor/Tools 诊断注册表每实例最多保留 200 条转换，因此 UI 刷新周期不负责捕获事件；正常容量内可从有界历史补齐两个 latest frame 之间的转换，但超过 200 条后最旧记录仍会被淘汰，不能把该通道描述为绝对无损。边上的同向转换次数只统计当前历史窗口，节点 `entryCount` 则独立累计，不会在历史窗口填满后停滞。Player 不编译转换历史、计数或 Provider。递归状态树、状态事件、原始 payload 和 evidence 仍由 Application read model 保留，但不作为页面固定区域。首版不提供强制切换状态。

## CLI 复现

读取最新 snapshot：

```powershell
yoki snapshot read --engine unity-editor --kit FsmKit --name state --project F:\Project
```

按实例读取完整详情：

```powershell
yoki command send --engine unity-editor --kit FsmKit --action get_workbench_snapshot --payload '{"instanceId":"fsm-00000001"}' --project F:\Project
```

`list_all`、`get_state`、`get_history`、`get_state_events` 和 `get_workbench_snapshot` 都是只读 action。宿主 capability 声明可用时优先走 FastChannel，连接或协议失败只回落一次可靠 FileBridge。

## 外部运行态验证

YokiFrame 不提供 Workflow Acceptance、Play Mode 切换或 Runtime target 枚举。需要自动进入 Play Mode、驱动场景并验证 FsmKit 状态时，由自行选择的外部 Unity 自动化工具按其自身规则执行，再通过上述 FsmKit 只读 action 查询框架语义。

## 常见错误

| 错误码 | 原因 | 处理 |
|---|---|---|
| `InvalidPayload` | payload 不是有效对象，或缺少可用身份 | 使用 `instanceId`；只在兼容调用时使用 `fsmName` |
| `FsmNotFound` | 当前 session 中不存在目标实例 | 刷新实例列表，检查 Domain Reload 后的新 `sessionId` 和 `generation` |
| `UnknownAction` | action 不属于 FsmKit 只读目录 | 读取宿主命令目录并使用上述五个 action |

排查时先确认 Workbench 页头的 `engineId`、`sessionId`、`generation`、数据源和 stale 原因，再检查 command/response evidence 路径。
