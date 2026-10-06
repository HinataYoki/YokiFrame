# 引擎操作面胶水层设计（v4 历史归档）

> 历史归档：下文保留 v4 原始设计、阶段状态和测试记录，不代表当前要求或本轮验证结果。涉及入口执行器、T2、未完成项及动作数量时，以 [v5 当前设计](Engine-Operation-Glue-Design.md) 和 [当前验收清单](Engine-Operation-Review-Checklist.md) 为准；不要直接执行本历史文档中的示例。

> 状态：**v4，待评审**（仍未实现）
> v4 按评审意见**重定主线**：编辑器操作桥 + **用户 C# 自动化入口执行器**是主线，动态源码 eval 降为**可选补充**；异步运行生命周期、执行目标、入口调用契约纳入第一期；重载适配前置。
> 已确认决策：① 引擎操作面内核；② 先设计再实现；③ 复用 FileBridge v2、不升协议；④ Dangerous 仅 cli + workbench（执行点见 §8，v4 已改判）；⑤ 双引擎同一流程纵切优先，T2 延后。

---

## 1. 目标与非目标

### 主线（第一期必须成立）
1. **内置编辑器操作**：场景查询/修改、Play 控制、资产操作。
2. **用户 C# 自动化入口执行器**：入口发现 → 调用 → **异步执行** → 查询结果，支持断言失败、异常与日志，并明确区分"命令提交成功"与"测试通过"。
3. 两条主线在 **Unity 与 Godot 上各跑通同一流程**：发现入口 → 修改场景 → 跨帧执行 C# → 返回明确的通过/失败。

### 可选扩展（非主线）
- 动态源码执行（T2 生成源码），以及标准测试框架适配（Unity 侧复用 TestRunnerApi 的发现/执行/结果回调）。
- 共享内存 telemetry、Workbench Dashboard 页面（见 §3.2）。

### 非目标
- CI 编排、编辑器版本安装、许可证、构建/测试流水线、VCS —— 属于工具链外围。
- **不排除本地执行流程**（v3 的非目标表述已修正）：本地"加载场景 → 等若干帧 → 调用业务代码 → 断言"必须能在本期契约内完成。
- 修改 wire contract（`PROTOCOL_VERSION` 保持 2）。

---

## 2. 现状与硬约束

### 2.1 就绪能力
`IYokiFrameCommandHandler.cs:14,21`、`YokiFrameKitCommandHandler.cs:12,33,92`、`YokiFrameCommandKind.cs:7`、`YokiFrameCommandPolicy.cs:122`、`HostCommandCoordinator.cs:224`、`YokiFrameEditorFileBridgePump.cs:79`、`GodotEditorPlugin.cs:35-47`、`GodotFileBridgeHost.Commands.cs:79`、`CliCommandSchema.cs:329`、`CommandExecutionService.cs:155-198`。

### 2.2 硬约束
| # | 约束 | 证据 |
|---|---|---|
| C1 | FastChannel 只执行 `ReadOnly`；Godot Editor 无 FastChannel | `Pump.FastChannel.cs:267-270,290-305`；`GodotEditorFileBridgeHost.Commands.cs:253` |
| C2 | 超时只在调用 handler 之前判定；执行期无 deadline、无 token | `YokiFrameCommandDispatcher.cs:48-53`；`IYokiFrameCommandHandler.cs:21` |
| C3 | 单条命令同步阻塞编辑器 tick；批预算 32/25ms 只在命令之间检查 | `HostCommandCoordinator.cs:194,213,250-253` |
| C4 | wire 只有 `Success/Error` 终态 | `YokiFrameEditorFileBridgePump.Results.cs:22,40`；`CommandResponseValidator.cs:97-101` |
| C5 | 域重载断连 + 释放 lease + session/generation 轮换 | `FastChannel.cs:28-29,70-74,38-56` |
| C6 | 存在源码文本断言测试与文档漂移测试 | `YokiFrameFastChannelAdapterArchitectureTests.cs:19-28,34-54,138-153`；`SkillDocumentationDriftTests.cs:19-25,61-83` |
| C7 | Policy 不做按风险的来源限制（默认来源含 codex / external-automation） | `YokiFrameCommandPolicy.cs:106-114,129-134,164-170` |
| C8 | 客户端在拿到响应**之后**才校验会话身份 | `CommandExecutionService.cs:191-199` |
| C9 | Workbench 异常路径丢失 requestId 与证据路径 | `WorkbenchDashboardService.FastChannel.cs:113-125` |
| C10 | 请求信封整文件归档，deadletter 同样保留原文 | `YokiFrameFileBridgeHostStore.cs:138-149`；`HostCommandCoordinator.cs:156-160` |
| C11 | snapshot 通用；共享内存 telemetry 仅对 `IYokiFrameVersionedKitInteractionProvider`；Workbench 是固定清单 | `Pump.Publishing.cs:140-157`（`:149`）；`WorkbenchRuntimeKitCatalog.cs:13,29` |
| C12 | Godot Editor 命令目录把全部 action 固定发布在 `System` 下 | `GodotEditorFileBridgeHost.Commands.cs:92-117`（`:114`） |
| **C13** | **Policy 由三个宿主工厂创建，Provider 没有任何传参接口**：Unity Editor `YokiFrameEditorFileBridgePump.Commands.cs:54`、Godot Editor `GodotEditorFileBridgeHost.Commands.cs:73`、Godot Runtime `GodotFileBridgeHost.Commands.cs:76` | 同上 |
| **C14** | **Unity Runtime 侧不存在 FileBridge/命令宿主**（`Core/Adapters/Unity/Runtime` 全目录无 Bridge/Host/Coordinator）；Godot 有 `godot-runtime` 宿主 | 目录扫描结果 |
| **C15** | 生成脚本的程序集归属由目录决定：`Assets/YokiFrame.Eval/*.cs` 落 `Assembly-CSharp`，只有 `Assets/**/Editor/*.cs` 才落 `Assembly-CSharp-Editor` | Unity 程序集规则 |
| **C16** | **Unity 2022.3 是兼容基线**：Unity 编译路径不得依赖 `System.Text.Json`。2022.3 不自带该程序集；工程若装 Code Coverage，其 `ReportGeneratorMerged.dll` 会把 `System.Text.Json` 以 **NotPublic** 形式合并，而 asmdef 是 `overrideReferences:false`，于是 `using System.Text.Json;` 解析到不可访问类型并报 CS0122（Unity 6 自带真实程序集，故只在 2022 报错） | 元数据扫描 `Library/PackageCache/com.unity.testtools.codecoverage@1.2.4/lib/ReportGenerator/ReportGeneratorMerged.dll`；原 RuntimeCache 两个校验器 |
| **C17** | 新增 Engine 操作的 Unity 实现必须使用 2021.2+ 皆可用的 API（如 `EditorApplication.isPlaying`），不得直接使用 Unity 6 独有 API，除非做版本探测 | 设计约定；由 §18.4 回归保证 |

---

## 3. 设计总览

```
CLI / Workbench / AI
  │ command send --kit Engine --action entry_run --payload {entry,target,...}
  ▼  CommandExecutionService（ReadOnly→FastChannel；其余→FileBridge；含重载容错 §12）
宿主主线程 Dispatcher → Policy（协议级准入）
  ▼
Engine Kit Provider（胶水层，引擎无关）
  ├─ Gate（开关 + 来源 + target 可用性，§8/§9）
  ├─ Operations：编辑器操作桥（scene/play/asset）
  ├─ Entry Registry：入口发现 + 缓存（§6）
  └─ Run Scheduler：tick 驱动异步运行器（§10）
        ├─ Unity  : Core/Adapters/Unity/Editor/EngineKit/     [editor, play]
        └─ Godot  : Core/Adapters/Godot/{Editor,Runtime}/EngineKit/  [editor, play, runtime]
```

### 3.1 接线方式
| 宿主 | 接线 | 改宿主源文件？ |
|---|---|---|
| Unity Editor | `IYokiFrameKitInteractionProvider` + `YokiFrameToolKitInteractionCatalog.Register`（`YokiFrameToolKitInteractionCatalog.cs:28`）+ `[InitializeOnLoadMethod]` 安装器（范式 `UIKitEditorInstaller.cs:12-22`） | **不改 pump**（权限不进 Policy，见 §8） |
| Godot Runtime | 同 Unity（`GodotFileBridgeHost.Commands.cs:79` 已含 `mKitInteractions`） | 不改 |
| Godot Editor | 无 Kit registry，且命令目录固定分组（C12）→ ①目录按 `descriptor.Kit` 分组；②追加 handler 与 descriptor（`GodotEditorFileBridgeHost.Commands.cs:75-84`）；③补 Engine 状态发布链 | 需改 3 处 |

### 3.2 本期能力承诺（按 C11 收窄）
| 通道 | 本期 |
|---|---|
| 命令面（`command send`/`list_commands`/`harness catalog`） | ✅ |
| CLI snapshot（`snapshot read --kit Engine`） | ✅ |
| 共享内存 telemetry、Workbench Dashboard 页面 | ❌ 移出（需版本化 Provider / 固定清单登记），列 P4 可选 |

---

## 4. 执行目标模型（editor / play / runtime）

v3 把"编辑器里跑 C#"当成唯一形态，这是错的：**Editor 内调用 C# ≠ 调用运行中游戏的代码**。v4 引入显式 `target`。

| target | 含义 | Unity | Godot |
|---|---|---|---|
| `editor` | 编辑器域、非播放态；可操作 Editor API 与资产 | ✅ `EditorApplication.update` 生态 | ✅ `GodotEditorPlugin._Process` |
| `play` | **逻辑语义 = 运行态**（可驱动场景对象与业务代码）。**承载宿主因引擎而异**：Unity 是编辑器进程内的 Play Mode；Godot 的游戏**无论是否嵌入窗口都在独立进程中运行**，因此 play 与 runtime 都由 `godot-runtime` 宿主承载，编辑器宿主只负责启动/停止与转发 | ✅ 编辑器进程 | ⚠️ 由 `godot-runtime` 承载（编辑器只执行启动/停止与转发） |
| `runtime` | **独立进程**中运行的游戏 | ❌ **本期不支持**（C14：Unity 侧无 Player 宿主，需新增，另立阶段） | ✅ 复用 `godot-runtime` 宿主 |

规则：
- 每个执行类 action 都带 `target`（默认 `editor`）；`engine_capabilities` 返回 `operation × target` 可用性矩阵；`domain_state` 返回当前 `activeTarget`、是否在 Play、域代次。
- 目标不可用返回 `EngineOperationUnsupported`（含 `target` 字段），不是静默降级。
- 跨 target 的语义差异写进文档：例如 `scene_mutate` 在 `play` 下改动会在退出 Play 时丢失。
- **`runtime` target 的入口可见性不同**：该目标的入口必须编译进游戏构建产物，并由 runtime 宿主扫描**自身进程内**已加载的程序集。因此 `entry_list` 在 `godot-runtime` 上返回的是该进程内的入口集合，可能与 Editor 侧不同——调用方必须承认 `entry_list` 是 **per-target** 的，不能跨 target 复用同一份目录（这也是 P2 验收的一部分）。

**宿主绑定规则（target → host，评审第 1 条）**：
- `editor` → 当前项目的编辑器宿主（`unity-editor` / `godot-editor`）。
- `play` → **Unity：编辑器宿主**（Play Mode 在编辑器进程内）；**Godot：`godot-runtime`**。`play_control enter` 由 `godot-editor` 调用 `EditorInterface.play_main_scene()` **启动**游戏，随后一切运行态操作（入口执行、场景读写）都路由到该 runtime 宿主——编辑器宿主不代跑游戏代码。
- `runtime` → `godot-runtime`；Unity 本期不支持（C14）。
- 绑定方式：调用方可 `--engine` 显式指定目标宿主；未指定时由框架按 `target` + 项目路径推导（同项目的 runtime 宿主优先承载 play/runtime）。目标宿主不在线 → `EngineOperationUnavailable`（结果里带 `target` 与期望 engineKind），**不静默回退到编辑器宿主**。
- 该规则同时约束入口执行与场景操作：`entry_list`/`entry_run`/`scene_*` 都按同一绑定路由。

---

## 5. 契约设计

新增 `Core/Editor/EngineKit/`（守卫 `#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING`）：

**程序集边界（评审第 2 条）**：用户游戏脚本要引用的类型**不能**放在 Editor-only 程序集，否则 Player 构建时类型缺失。约定如下：

| 类型 | 程序集 | 位置 |
|---|---|---|
| `YokiFrameEntryAttribute`、`YokiFrameEntryTarget`(flags)、`YokiFrameEntryContext`（契约与分帧原语签名）、`YokiFrameEntryResult`、`YokiFrameEntryAssertion`、`YokiFrameEntryStatus` | **运行时程序集 `YokiFrame`**（`Core/Runtime/YokiFrame.asmdef`，`autoReferenced: true`） | `Core/Runtime/EngineKit/` |
| 入口扫描器与注册表、Run Scheduler、Gate、Provider、Settings、错误码、`entry_lookup` | Editor / Tools 程序集 | `Core/Editor/EngineKit/` |
| 各引擎的具体操作实现 | 引擎适配器程序集 | `Core/Adapters/{Unity,Godot}/**/EngineKit/` |

依赖方向：**运行时契约层不得引用 Editor/Tools 类型**；Editor 层实现运行时层声明的类型与接口。`YokiFrameEntryContext` 的**实现**在宿主侧（Unity 编辑器 / `godot-runtime`），运行层只放抽象与数据模型。

```csharp
public interface IYokiFrameEngineOperation
{
    string Operation { get; }
    YokiFrameCommandDescriptor Descriptor { get; }        // 常驻注册（§9 决议 A）
    IReadOnlyList<YokiFrameEngineExecutionTarget> Targets { get; }   // editor/play/runtime
    YokiFrameEngineOperationAvailability GetAvailability(in YokiFrameEngineOperationContext context);
    YokiFrameCommandResult Execute(in YokiFrameEngineOperationContext context);
}

public interface IYokiFrameEngineOperationProvider
{
    string EngineKind { get; }
    IReadOnlyList<IYokiFrameEngineOperation> Operations { get; }
    IYokiFrameEngineEntryRegistry Entries { get; }        // §6
    IYokiFrameEngineRunScheduler Runs { get; }           // §10
    YokiFrameEngineDomainState ReadDomainState();
}

public sealed class YokiFrameEngineKitProvider : IYokiFrameKitInteractionProvider
{
    public string Kit => "Engine";
    public IReadOnlyList<string> SnapshotNames => new[] { "state" };
    public IReadOnlyList<YokiFrameCommandDescriptor> Commands { get; }   // 常驻，与开关无关
    public string CreateSnapshot(string snapshotName);
    public bool CanHandle(YokiFrameCommandRequest request);
    public YokiFrameCommandResult Handle(YokiFrameCommandRequest request);  // → Gate(§8) → Operation
}
```

- Provider 无实例状态（Unity 侧 dispatcher 会重建，`Pump.cs:138-154`）；运行态数据放静态/持久化记录。
- 错误码：`EngineOperationDisabled`、`EngineOperationSourceNotPermitted`、`EngineOperationUnsupported`、`EngineOperationUnavailable`、`EngineOperationInvalidPayload`、`EngineOperationFailed`、`EntryNotFound`、`RunNotFound`、`EngineReloading`。

---

## 6. 用户入口执行器（主线能力）

### 6.1 入口发现与调用契约
用户在项目里标注静态方法：
```csharp
[YokiFrameEntry("combat.smoke", Description = "战斗冒烟", Targets = Target.Editor | Target.Play)]
public static async Task<YokiFrameEntryResult> RunAsync(YokiFrameEntryContext ctx)
{
    await ctx.LoadSceneAsync("Battle");
    await ctx.WaitFrames(10);
    var hp = await ctx.Call(() => BattleFacade.Current.Hp);
    ctx.Assert.Equal(100, hp, "初始血量");
    ctx.Log("ok");
    return ctx.Pass();
}
```
- **上下文** `YokiFrameEntryContext` 提供：`WaitFrames(n)`、`WaitUntil(pred, timeoutMs)`、`LoadSceneAsync`、`Call<T>()`、`Assert.*`、`Log`、`CancellationToken`、`Timeout`。所有等待都**分帧推进**，不阻塞 tick。
- **结果** `YokiFrameEntryResult`：`status(Queued|Running|CancelRequested|TimeoutRequested|Passed|Failed|Errored|Cancelled|Timeout|Detached|Unknown)`、`assertions[{name,passed,message}]`、`logs[]`、`exception{type,message,stack}`、`durationMs`、`frames`、`staleContextCalls`。
- **契约边界**：同步链无法抢占（C2）。入口自身的长耗时同步代码会阻塞编辑器 tick —— 框架提供分帧原语，但**不保证**用户代码不阻塞；文档明示。
- **程序集归属**：标注属性、target、Context 契约与结果模型都在**运行时程序集**（见 §5 边界表），游戏脚本可直接引用、Player 构建不缺类型；扫描器/执行器/调度器在 Editor 层。

### 6.2 发现契约（解决"AI 猜方法名"）
只读动作 `entry_list` 返回可机读目录（首次扫描后按程序集/时间戳缓存）：
```jsonc
{ "entries": [ {
    "name": "combat.smoke", "displayName": "战斗冒烟",
    "assembly": "Assembly-CSharp", "declaringType": "Game.Tests.CombatSmoke",
    "method": "RunAsync", "async": true, "targets": ["editor","play"],
    "parameters": [ { "name": "difficulty", "type": "int", "required": false, "default": 1 } ],
    "returns": "YokiFrameEntryResult", "description": "战斗冒烟"
} ], "scannedAssemblies": 12, "generatedAtUtc": "…" }
```
- 发现规则：扫描已加载程序集上的 `[YokiFrameEntry]` 静态方法；参数必须是 JSON 可映射的简单类型或 `YokiFrameEntryContext`；不满足者在目录中标记 `invalid` + 原因（不静默忽略）。
- `entry_run` 的 payload 只引用 `name` + `args`，不引用方法名，避免 AI 猜签名。

### 6.3 与"测试通过"的严格区分
| 事实 | 来源 | 含义 |
|---|---|---|
| 命令提交成功 | `command send` 的 `Success` + `accepted:true` + `runId` | **只表示运行已入队**，不代表通过 |
| 运行结论 | `entry_result` 的 `status` | `Passed` / `Failed`（断言失败）/ `Errored`（异常）/ `Cancelled` / `Timeout` / `Unknown` |
验收标准必须分别断言这两件事，禁止把 `command send` 的成功当成测试通过。

---

## 7. 动作清单

| action | kind | target | 说明 | Unity | Godot |
|---|---|---|---|---|---|
| `domain_state` | ReadOnly | — | 播放/编译状态、域代次、activeTarget | ✅ | ✅ |
| `engine_capabilities` | ReadOnly | — | operation × target 可用性 + 开关状态与原因 | ✅ | ✅ |
| `scene_query` | ReadOnly | editor/play | 场景层级、节点、组件快照 | ✅ | ✅ |
| `scene_mutate` | Dangerous | editor/play | 创建/删除/改属性/保存 | ✅ | ✅ |
| `play_control` | Dangerous | editor | enter/exit/pause/step；启动/停止由编辑器宿主执行，运行态操作随后路由到 runtime 宿主（§4） | ✅ | ✅ 启动/停止；运行态由 `godot-runtime` 承载 |
| `asset_ops` | UserAction | editor | 导入/刷新/检索 | ✅ | ⚠️ 评估 |
| `entry_list` | ReadOnly | — | 入口目录（§6.2） | ✅ | ✅ |
| `entry_run` | Dangerous | editor/play/runtime | 提交运行 → `accepted` + `runId`（按 §4 绑定路由） | ✅(editor,play) | ✅ editor→编辑器宿主；play/runtime→`godot-runtime` |
| `entry_result` | ReadOnly | — | **纯读**运行状态与结构化结果 | ✅ | ✅ |
| `entry_lookup` | ReadOnly | — | 按 `requestId` 查回 `runId` 与状态（§10.1） | ✅ | ✅ |
| `entry_cancel` | UserAction | — | 取消运行 | ✅ | ✅ |
| `eval` / `eval_result` | Dangerous / ReadOnly | editor | **可选补充能力**（§11） | ✅ | ✅ |
| `recompile` | Dangerous | editor | 请求脚本编译 | ✅ | ❌ |

---

## 8. 【P1-③】Dangerous 来源限制的执行点（v4 改判）

C13 证明 v3 的"Engine Kit 传入 `dangerousSources`"**无处可传**：Policy 由三个宿主工厂创建（Unity Editor `:54`、Godot Editor `:73`、Godot Runtime `:76`），Provider 没有传参接口；若改共享 Policy 的默认行为，又会波及所有 Kit 的 Dangerous 命令。

**v4 决议：限制作用域 = Engine Kit，执行点 = Engine Gate（不是 Policy）。**

理由：
- 作用域天然隔离——其他 Kit 的 Dangerous 命令零影响，不需要改共享 Policy 的语义。
- 不需要给三个宿主工厂加参数，因此**"不改 pump"这条约束才真正成立**（v3 同时坚持两者，是自相矛盾的）。
- Gate 能从 `YokiFrameCommandRequest.Source` 直接判定，错误码 `EngineOperationSourceNotPermitted` 比通用 `PolicyRejected` 更可诊断。
- 保留升级路径：若将来其他 Kit 也要同款限制，再把它提升为 Policy 的按 Kit 覆盖表（届时才需要改三处宿主工厂）。

**测试矩阵（P0 交付，落在 EngineKit 测试）**：

| source | 只读动作 | Dangerous + `confirmed:true` | Dangerous 无 `confirmed` |
|---|---|---|---|
| `cli` | 通过 | 通过 | `ConfirmationRequired`（Policy） |
| `workbench` | 通过 | 通过 | `ConfirmationRequired`（Policy） |
| `codex` | 通过 | **`EngineOperationSourceNotPermitted`（Gate）** | `ConfirmationRequired`（Policy 先拦） |
| `external-automation` | 通过 | **`EngineOperationSourceNotPermitted`（Gate）** | `ConfirmationRequired` |
| 未登记来源 | `PolicyRejected` | `PolicyRejected` | `PolicyRejected` |

另需两条回归：① 其他 Kit 的 Dangerous 命令行为**逐条不变**（Policy 未改）；② Gate 拒绝时不得执行任何用户代码。

**信任边界**：`source` 是调用者自填的审计字段（`YokiFrameCommandSourceContract` 注释已声明"来源不是认证凭据"）。本限制是**防误配置的护栏，不是安全边界**；真正的边界是"本机文件/管道访问 + 项目开关 + `confirmed`"。

---

## 9. 开关语义与错误码契约

**决议 A：descriptor 常驻注册，开关判定在 Gate，统一 `EngineOperationDisabled`。**
- 取消"关闭时从 Policy 删除动作"：那样会被 Policy 先判 `UnknownCommand`，两个错误码不可能同时成立。
- 代价：关闭时 `list_commands` 仍列出 Engine 动作名；**名字可见 ≠ 可执行**。收益：`engine_capabilities` 可给出 `DisabledBySettings` 与开启方法。
- `UnknownCommand` 只留给真的没注册的 action。

**决议 B：Gate 每次执行读设置（按 mtime 缓存），与 catalog revision 解耦。**
- 开关的开与关都**立即生效**；文件缺失/解析失败 = 关闭（fail-closed）并在 `engine_capabilities` 报告原因。
- 禁止依赖 `YokiFrameToolKitInteractionCatalog.Register` 的 revision 刷新：同一 Provider 重复注册幂等、**不增 revision**（`YokiFrameToolKitInteractionCatalog.cs:36-50`）。

**决议 C：关闭状态下的例外（评审第 5 条）**。开关关闭、配置缺失或解析失败时：
- **仍然可用**（不受开关阻断）：`engine_capabilities`、`domain_state`、`entry_list`、`entry_result`、`eval_result`、`entry_lookup`，以及 `entry_cancel`（取消既有运行）。这条是 P0 验收"关闭时仍能查询 capabilities"的依据。
- **一律禁止**：新提交（`entry_run` / `eval` / `scene_mutate` / `play_control` / `asset_ops` / `recompile`）与**新认领**——调度器不再认领 `Queued` 记录。
- **运行中的任务**：按**协作取消**处理（发取消请求 → `CancelRequested`，见 §10.2），框架不承诺用户代码已停止。
- 实现对应：豁免由 descriptor 上的 `isDiagnostic` / `isCancellation` 声明，`ExemptFromExecutionSwitch = (isDiagnostic || isCancellation) && Kind != Dangerous` —— **Dangerous 操作永不自豁免**（已有测试覆盖）。

---

## 10. 异步运行生命周期（run 状态机）

**持久化根**：`<project>/.yokiframe/engine/runs/`（非 Assets，不触发导入）。
- 运行记录 `<runId>.json`：`runId, requestId, source, entry, target, args, state, submittedAtUtc, startedAtUtc, updatedAtUtc, expiresAtUtc, ownerSessionId, generation, attempt, resultPath, errorCode, steps[], cancelRequestedAtUtc, detachedAtUtc, lateCompletionAtUtc, lateResultPath, staleContextCalls`。
- **请求索引** `index/<requestId>.json`：`requestId → runId, createdAtUtc`（原子写；同一 requestId 只能指向一个 runId）。

### 10.1 请求与运行的关联（评审第 3 条）
- `entry_run` 的 handler **先写运行记录与请求索引，再返回** `accepted + runId`。协调器是先执行 handler、再写响应（`HostCommandCoordinator.cs:337`），因此存在"运行已入队、accepted 响应尚未落盘"的窗口——先写记录即可让调用方凭自己已知的 `requestId` 找回运行。
- 反查路径：`entry_lookup`（ReadOnly，payload 只带 `requestId`）返回 `runId` + 当前状态；查不到返回 `RunNotFound`，并说明"该请求未创建运行"。
- **幂等**：同一 `requestId` 重复提交 → 返回已存在的 `runId`，**不创建第二个运行**。索引用"不存在才创建"的原子语义（独占创建 / 同卷 `File.Move`），冲突时读回既有 runId 原样返回。
- 崩溃窗口的三种结局：① 记录 + 索引已写、响应丢失 → 可反查，运行照常推进；② 记录已写、索引未写 → 启动对账按记录里的 `requestId` 补写索引；③ 二者都没写 → 反查返回 `RunNotFound`。

```
Queued ──调度器认领──▶ Running ──┬─▶ Passed                     (全部断言通过)
                                  ├─▶ Failed                     (断言失败，含全部断言明细)
                                  ├─▶ Errored                    (未捕获异常 + stack)
                                  └─▶ Unknown                    (重载后无终态记录，禁止自动重跑)

          entry_cancel / 超时 ──▶ CancelRequested / TimeoutRequested   (非终态：只表示"已请求")
                                        ├─ 宽限期内观察到退出 ─▶ Cancelled / Timeout   (终态)
                                        └─ 宽限期外仍未退出   ─▶ Detached             (终态：用户代码可能仍在运行)
```

### 10.2 状态机规则（含取消语义，评审第 4 条）
1. **tick 驱动**：调度器在 `EditorApplication.update`（Unity）/ `_Process`（Godot）推进运行的每一步，`WaitFrames/WaitUntil` 只登记等待条件，**不阻塞 tick**（C3）。
2. **提交即返回**：`entry_run` 只入队并返回 `accepted + runId`，与结论严格分离（§6.3）。
3. **跨域重载**：运行记录持久化；重载后按 `generation` 判断归属。若重载前已写终态 → 保持；若停在 `Running` 且无终态 → `Unknown`（**绝不自动重放**，需显式重新 `entry_run`）。
4. **认领防重复**：原子 `File.Move`（同卷）或独占句柄；owner 失效且超 `leaseMs=60s` → `Unknown`。
5. **对账不跑用户代码**（回答评审的追问）：状态推进只由**调度器**在 tick 中做；`entry_result`/`eval_result` 是**纯读**动作，**永不调用用户代码**，因此保持 `ReadOnly` 语义与 FastChannel 资格。此条列入契约测试。
6. **超时与取消都是"请求"，不是"已停止"**：步骤级/运行级超时先置 `TimeoutRequested`，`entry_cancel` 先置 `CancelRequested`；**只有调度器观察到用户代码退出**才落终态 `Timeout` / `Cancelled`。
7. **协作性明示**：`Task` 取消需要用户代码配合（忽略 token 就无法停止）。宽限期 `graceMs`（默认 5s）内未观察到退出 → 置终态 `Detached`，语义是"框架已放弃等待，用户代码可能仍在运行"。
8. **迟到完成不得覆盖终态**：结果写入必须 CAS（仅当记录仍非终态）。记录已是终态时，把迟到结果写入 `lateResultPath` + `lateCompletionAtUtc`，`status` **保持不变**；`entry_result` 只能把它作为附注展示。
9. **失效 Context**：运行进入任一终态、或收到取消/超时请求后，`YokiFrameEntryContext` 立即失效；失效后的 `Assert`/`Log`/`WaitFrames`/`WaitUntil`/`Call`/`LoadSceneAsync` 等框架操作一律拒绝并累加 `staleContextCalls`，保证"记录已终态后用户代码不能再用框架能力改场景"，该计数随结果返回。
10. **清理**：终态记录与索引按 TTL 回收（复用 `YokiFrameFileBridgePruner` 模式），报告回收条数。

---

## 11. 动态源码 eval（可选补充能力，v4 降级）

| 档 | 同步性 | 说明 |
|---|---|---|
| T0' 入口调用 | 分帧异步 | **即 §6 的入口执行器**，不是"任意 C#" |
| T2 生成源码 | 两阶段 | 生成 `.cs` → 编译 → 反射调用 → 写结果 → 合并清理 |
| T1 Roslyn | — | 延后（需改 `YokiFrame.Editor.asmdef`，与自带 Roslyn 冲突） |

**C15 修正（v3 的错误）**：v3 把源码生成到 `Assets/YokiFrame.Eval/*.cs` 却固定探测 `Assembly-CSharp-Editor`，二者不匹配——该目录实际落 `Assembly-CSharp`。v4 规则：
- 生成目录固定为 `Assets/YokiFrame.Eval/Editor/`（Unity 特殊目录 → `Assembly-CSharp-Editor`，可引用运行时程序集），**不额外创建 asmdef**（避免与用户 asmdef 冲突）。
- 探测**不按程序集名**：遍历 `AppDomain.CurrentDomain.GetAssemblies()` 找类型 `YokiFrame.Eval.Generated.Eval_<id>` 并比对内嵌 `Token` 常量，命中即 `Ready`。程序集改名/归属变化都不影响。
- **用户代码引用规则**：预定义 Editor 程序集只自动引用 `autoReferenced=true` 的程序集。若用户代码在自己 asmdef 里且 `autoReferenced=false`，eval 生成代码无法引用 → 目录中标记 `unresolved` 并报 `EvalCompileFailed(reason=assembly-not-referenced)`，同时文档要求"可作为 eval 目标的用户代码应放在 autoReferenced 程序集，或把入口放在 §6 的入口执行器覆盖范围内"。
- 状态机沿用 §10 的规则集（持久化记录、编译批次归属、失败不依赖重载、崩溃 → `Unknown`、清理合并）。

---

## 12. 【P1-⑥】客户端重载适配（前置到首次引入重载的阶段）

问题（C8/C9）：`CommandExecutionService.cs:191-199` 在拿到响应后才校验身份，会话换代即抛 `HostIdentityChanged`，丢弃已落盘的成功响应；`WorkbenchDashboardService.FastChannel.cs:113-125` 异常分支还丢失 `requestId` 与证据路径。

改动：
1. `CommandExecutionService`：身份变化时，若该 `requestId` 的 terminal 响应已存在 → 返回该响应 + `ReloadedSession` 警告 + `nextActions: command status --request-id`；否则返回 `Unknown`（**不是 Failed**），保留 `requestId`/`commandPath`/`responsePath`。
2. `WorkbenchDashboardService.FastChannel.cs` 异常分支：补 `requestId` 与证据路径；重载类错误映射 `CommandOutcomeState.Unknown`，UI 不提供重放。
3. CLI：`HostIdentityChanged`、`EngineReloading` 纳入"→ `Unknown` + 先查证"映射（现有同款语义见 `CliJsonOutput.cs:220-234`）。

**交付顺序（评审第 6 点）**：最小重载适配**与首个引入重载的功能同批交付**（P1 含 `play_control`），不再单独排在后面。验收：开域重载时 `play_control enter` → CLI 返回 accepted/Unknown 且 `requestId` 可查 → `command status` 可取回终态。

---

## 13. 数据保留与脱敏

| 类别 | 位置 | 含原文 | 策略 |
|---|---|---|---|
| 传输证据 | `commands/` → `archive/`；失败 `deadletter/` | **是**（C10） | pruner 按 TTL 清理；deadletter 需显式 purge |
| 审计日志 | 结果字段与宿主日志 | 否（仅 hash/长度/source/requestId） | 长期 |
| 运行记录 | `.yokiframe/engine/runs/` | 否（仅入口名与参数摘要） | TTL 回收 |
| T2 临时源码 | `Assets/YokiFrame.Eval/Editor/` | 是 | 合并清理；残留由 `eval_prune` 回收 |

**文档措辞**："框架**不承诺**代码原文不落盘"；需要避免时用 `codeFile` 模式（payload 只带项目内路径 + hash）并及时 purge。`logs` 与 `result.json` 同样可能含业务数据。

---

## 14. 实施清单

| 阶段 | 内容 | 验收 |
|---|---|---|
| **P0 契约与权限** | EngineKit 契约 + Gate（开关/来源/target）+ `domain_state` + `engine_capabilities` + Unity 接线与 snapshot + descriptor/capability.json 一致性测试 | §8 矩阵全绿且其他 Kit 行为不变；`snapshot read --kit Engine` 有数据；`harness catalog` 出现 Engine 动作；关闭时 `engine_capabilities` 报 `DisabledBySettings`、执行返回 `EngineOperationDisabled` |
| **P1 Unity 纵切** | 编辑器操作桥（`scene_query`/`scene_mutate`/`play_control`/`asset_ops`）+ **入口执行器全流程**（`entry_list`/`entry_run`/`entry_result`/`entry_cancel`，异步、断言、异常、日志）+ **最小重载适配（§12）** | 跑通"发现入口 → 改场景 → 跨帧执行 → 明确 Passed/Failed"；断言失败给全量明细；异常 → `Errored`+`stack`；提交成功与运行通过分别断言；域重载下 `requestId` 不丢 |
| **P2 Godot 纵切** | 同一流程在 Godot Editor 跑通（含 C12 目录分组、状态发布链、`target` 支持）+ Godot Runtime 的 `entry_run`（runtime target） | 同一 `entry_list`/`entry_run`/`entry_result` 流程在 Godot 可用；Unity 侧零改动 |
| **P3 动态 eval（可选）** | T2 状态机 + §11 程序集归属修正 + `eval_result` | 编译失败不依赖重载即落 `CompileFailed`；崩溃窗口 → `Unknown` 不自动重放；`eval_result` 纯读 |
| **P4 可选扩展** | TestRunnerApi 适配（EditMode/PlayMode 测试发现与结果）、共享内存 telemetry、Workbench 页面、Unity `runtime` target（需新增 Player 宿主） | 按需评估 |

**回归红线**：`YokiFrameFastChannelAdapterArchitectureTests`、`SkillDocumentationDriftTests` 保持绿。

---

## 15. 风险

| # | 风险 | 处置 |
|---|---|---|
| R1 | 域重载被误读为失败 | §12 前置交付 + `Unknown`/`ReloadedSession` 语义 |
| R2 | 用户入口阻塞 tick（无法抢占，C2） | 提供分帧原语 + 文档明示 + 步骤超时 |
| R3 | Policy 改动波及其他 Kit | v4 已改判不在 Policy 实施（§8） |
| R4 | 生成源码程序集归属（C15） | 固定 `Editor/` 目录 + 类型/Token 探测 + 引用规则文档化 |
| R5 | descriptor / capability.json 漂移 | P0 一致性测试 |
| R6 | Godot 接线不对称（C12 + 无 snapshot 链） | P2 三项全做 |
| R7 | 框架范围声明冲突 | 更新 `FrameworkOverview.md:18,46` |
| R8 | 归档保留原文（C10） | §13 |
| R9 | `source` 可伪造 | §8 信任边界声明 |
| R10 | Unity `runtime` target 缺宿主（C14） | 明确不支持，P4 另立 |

---

## 16. 改动文件清单

**新增**
- `Core/Runtime/EngineKit/`（**运行时程序集**，评审第 2 条）：`YokiFrameEntryAttribute`、`YokiFrameEntryTarget`、`YokiFrameEntryContext`（契约与分帧原语签名）、`YokiFrameEntryResult`、`YokiFrameEntryAssertion`、`YokiFrameEntryStatus`
- `Core/Editor/EngineKit/`：Provider、Operation/Provider 契约、Gate、Errors、DomainState、入口扫描器与注册表、Run Scheduler、运行记录与**请求索引**存储、`entry_lookup`
- `Core/Editor/CommandBridge/Capabilities/Engine/capability.json`
- `Core/Adapters/Unity/Editor/EngineKit/`（Provider + 安装器 + 编辑器操作 + 入口执行器接线 + T2 子系统）
- `Core/Adapters/Godot/{Editor,Runtime}/EngineKit/`（P2）
- `Core/Tests/Editor/EngineKit/`（含 §8 矩阵、§10 状态机、`entry_list` 目录契约测试）

**修改**
- `YokiFrameWorkbench~/src/YokiFrame.Tooling.Application/Services/Commands/CommandExecutionService.cs`（§12.1）
- `YokiFrameWorkbench~/src/YokiFrame.Tooling.Application/Services/Dashboard/WorkbenchDashboardService.FastChannel.cs`（§12.2）
- `YokiFrameWorkbench~/src/YokiFrame.Cli/CliJsonOutput.cs`（§12.3）
- `Core/Adapters/Godot/Editor/FileBridge/GodotEditorFileBridgeHost.Commands.cs`（P2：目录分组 + 接线）
- `Core/Adapters/Unity/Editor/Settings/UnityYokiFrameEditorSettingsOverlay.cs`（开关读取）
- `Documentation~/Api/00-GettingStarted/FrameworkOverview.md:18,46`、`Core/Editor/Skills/yokiframe/references/cli-commands.md`

**刻意不改**：`YokiFrameEditorFileBridgePump*.cs`（Kit registry 自动聚合；权限在 Gate，故无需改 pump —— v4 与 §8 自洽）、`YokiFrameCommandPolicy.cs`（其他 Kit 零影响）、`YokiFrameFileBridgeContract`。

---

## 17. 修订记录

**v3 → v4（本轮评审）**

| 评审项 | 结论 | 落点 |
|---|---|---|
| ① 缺 C# 自动化测试执行契约 | 主线改为"入口执行器"：`entry_list`/`entry_run`/`entry_result`/`entry_cancel` + 异步 runId + 状态机 + 结构化断言结果；非目标修正 | §1、§6、§7、§10 |
| ② 编辑器 ≠ 运行时执行 | 新增 `target: editor/play/runtime` 模型；Unity `runtime` 明确不支持（C14） | §4 |
| ③ Dangerous 限制无接线点 | C13 证实 Provider 无传参路径；**改判为 Gate 执行、作用域 = Engine Kit**，pump 因此确实不用改 | §8 |
| ④ 生成目录与程序集探测不匹配 | C15 证实；固定 `Editor/` 目录 + 全程序集类型/Token 探测 + 引用规则 | §11 |
| ⑤ AI 无法发现入口契约 | `entry_list` 机读目录（参数/异步/target/程序集/说明），`entry_run` 只引用 name | §6.2 |
| ⑥ 重载适配晚于依赖它的功能 | 前置：与 `play_control` 同批交付，并在验收里断言 | §12、§14 |
| 追问：`eval_result` 对账是否跑用户代码 | **不会**：对账只由调度器 tick 做；查询动作纯读、永不含执行，保持 ReadOnly | §10.5 |

**v2 → v3**：7 项评审（Policy 权限、T2 状态机、客户端重载、数据保留、开关错误码、Kit 注册承诺、Godot 接线）+ 2 边界（开关生效时机、T0 性能语义）。
**v1 → v2**：`confirmed` 字段名、FastChannel 只读、T2 两阶段、Kit registry 接线、超时/阻塞语义、测试红线。

---

## 18. 版本兼容性基线与回归手段（v4.1）

### 18.1 事件
同一份源码：Unity 6000 编译通过；Unity 2022.3.16f1 报 38 个 `CS0122`（`JsonElement` / `JsonDocument` 不可访问）。

### 18.2 根因（已用元数据扫描定位，非推测）
1. Unity 2022.3 不自带 `System.Text.Json` 程序集。
2. 工程装了 `com.unity.testtools.codecoverage@1.2.4`，其 `lib/ReportGenerator/ReportGeneratorMerged.dll` 把 `System.Text.Json.JsonDocument` / `JsonElement` 以 **NotPublic（internal）** 合并进自身。
3. `YokiFrame.Editor.asmdef` 为 `overrideReferences: false`，Unity 会自动引用工程内预编译程序集 → `using System.Text.Json;` 解析到该 internal 类型 → CS0122。
4. Unity 6 自带真实 `System.Text.Json`，因此同一份代码在 6000 无此问题——这就是「6000 不报、2022 报」的原因。

### 18.3 修复（已实施）
- 新增包内最小只读 JSON DOM：`Core/Editor/RuntimeCache/Json/YokiFrameManifestJsonDom.cs`（`JsonDocument` / `JsonElement` / `JsonValueKind` / `JsonDocumentOptions` / `JsonException`），不依赖任何外部程序集。
- `RuntimeManifestIntegrityValidator.cs` 与 `RuntimeManifestFileSetValidator.cs` 的 `using System.Text.Json;` 改为 `using YokiFrame.RuntimeCache.Json;`，**调用点未改动**。
- 有意保留的行为差异：重复属性在解析阶段即被拒绝（原先取值由 System.Text.Json 决定）；对完整性校验而言更安全。

### 18.4 回归手段（本机无 Unity 时同样可执行）
1. `dotnet build Core/Editor/YokiFrame.Editor.csproj -t:Rebuild` —— 工具构建。
2. 构造去掉 `System.Text.Json` 包引用的变体 csproj 并构建 —— 必须 0 错误，**等价 Unity 2022.3 的处境**。
3. `dotnet test YokiFrameWorkbench~/tests/YokiFrame.EngineKit.Tests` —— Gate 策略矩阵。

### 18.5 对 EngineKit 的约束
后续所有 Unity 侧引擎实现（场景操作、Play 控制、入口执行器、T2 生成源码）都必须满足 18.4 的三条回归；任何 Unity 6 独有 API 需按版本探测后再使用。

### 18.6 工作副本
自 v4.1 起，设计与实现的工作副本是实际 Unity 工程内的包：`<UnityProject>/Assets/YokiFrame`（即本文档所在位置）。

**v4 → v4.1**：迁移到实际 Unity 工程；修复 Unity 2022.3 的 CS0122（包内 JSON DOM 取代 System.Text.Json）；新增约束 C16/C17 与第 18 节。
**v4.1 → v4.2**：按评审第 1–5 条修订——Godot 的 play/runtime 改由 `godot-runtime` 宿主承载并定义宿主绑定规则（§4）；用户入口契约移入运行时程序集（§5、§16）；运行记录持久化 `requestId` 并新增请求索引、`entry_lookup` 与幂等提交（§10.1）；取消/超时改为"请求 + 观察"，新增 `Detached`、迟到完成 CAS 与失效 Context（§10.2）；明确关闭态例外清单（§9 决议 C）。

---

### 19.24 通用只读原语 inspect 与「根」抽象重构（v4.24）

**动机**：查看运行时状态是高频只读操作，而 eval 依赖工程编译（只能编辑器、每次落盘 + 编译）。需要一个**零编译**的通用读取原语，且不能为每个 Model 写专用读取器。

**高层**：inspect 动作（第 15 个，ReadOnly + 诊断 → **执行开关关闭也能用**）。payload 为 root / selector / path / depth / limit；路径语法为 成员.成员 与 [字典键] 与 [索引]；无 path 时列举成员与值；上限 members ≤200、depth ≤3、文本 256；错误码 InspectTargetNotFound 与 InspectMemberNotFound。

**重构（设计模式）**：把原先「两个硬编码根 + 一个插件接口」统一成**一个契约 + 一个注册表 + 三个策略**：

| 角色 | 类型 | 说明 |
|---|---|---|
| 契约 | IYokiFrameInspectRoot | 唯一底层抽象：Name 与 TryResolve(selector, out instance, out error) |
| 高层 | YokiFrameInspectRootRegistry | 组合 + 按名查表 + 诊断清单 |
| 策略 | ArchitectureInspectRoot（根名 service） | 框架 Architecture 注册的 Model/Service（扫程序集找 IArchitecture 具体类型） |
| 策略 | StaticTypeInspectRoot（根名 static） | 类型静态成员 |
| 策略 | AccessorInspectRoot（任意根名） | **引擎差异收口**：Unity Mono 单例 / Godot C# AutoLoad / 游戏容器只差「怎么查」，因此统一成「名字 + 访问器」 |

于是 inspect 操作里**没有任何机制特例**（TryResolveRoot 只委托注册表）；新增一类来源 = 新增一个策略。Unity 适配器用 UnityInspectRoots 注册内置 singleton 根（SingletonRegistry 只存诊断快照、不持有实例，因此按名字找类型读静态 Instance）。

**验证**：EngineKit 134/134（含 7 例 inspect：路径读成员、字典键、列表索引、静态根、缺失成员 vs 未知根、未命中不崩、自定义根、无路径列成员）；Godot.Editor 31/31（新增锁定用例：Godot 宿主命令面含 inspect）；Godot.Runtime 96/96；Tooling 324/324；Unity 编译门 0 错误；Godot 两个适配器真编译 0 错误。

**引擎通用性**：inspect 引擎无关 → Godot 宿主同样可用（且 Godot 已有 Runtime 宿主，因此 Godot Player 也能读）；限制是只能读 C# 侧对象，GDScript 对象读不到（用 GDScript eval 或 C# 入口）。
## 19. P0 实施进度（v4.3）

### 19.1 已实施并有自动化验证
| 项 | 位置 | 验证 |
|---|---|---|
| Gate：开关 → 来源 → target 判定顺序与错误码 | `Core/Editor/EngineKit/YokiFrameEngineGate.cs` | 49 个测试（真实 Dispatcher → Policy → Gate） |
| 豁免模型：诊断/取消类豁免开关，Dangerous 永不豁免 | `YokiFrameEngineOperationDescriptor.cs` | `EngineSwitchExemptionTests` |
| 执行目标模型与宿主绑定判定 | `YokiFrameEngineExecutionTarget.cs`、Gate ④ 段 | `EngineTargetRoutingTests` |
| 内建真实操作：`domain_state`、`engine_capabilities` | `YokiFrameEngineDomainStateOperation.cs`、`YokiFrameEngineCapabilitiesOperation.cs` | `EngineCatalogProviderTests` |
| Kit Provider：命令面 + state snapshot 组合 | `YokiFrameEngineKitProvider.cs` | 同上 |
| 包内 JSON DOM（替代 System.Text.Json，修 Unity 2022.3 CS0122） | `Core/Editor/CommandBridge/Json/YokiFrameJsonDom.cs` | 无 System.Text.Json 变体构建 + Packaging 测试 |
| Unity 接线：安装器 + Unity 引擎 Provider | `Core/Adapters/Unity/Editor/EngineKit/` | **待 Unity 内验证**（本机无 Unity，dotnet 不编译 Unity 适配器） |

### 19.2 待实施（P0 收尾）
1. `capability.json`（Workbench 项目模型 / `harness catalog` 的声明式来源）与 descriptor 一致性测试。
2. 会话身份（`sessionId`/`generation`）接入：当前 `SessionIdentityAvailable=false`，来源在 pump 私有静态字段，需要一次受控的内部访问器改动（会触碰 pump，需单独评审）。
3. `entry_*` 系列动作（入口执行器）——属 P1 主线。
4. Unity 内的端到端验收：`harness catalog --refresh-commands` 出现 `Engine/domain_state`；`command send --kit Engine --action domain_state` / `engine_capabilities` 有返回；`snapshot read --kit Engine` 有数据；开关关闭时危险动作返回 `EngineOperationDisabled`。

### 19.3 验收口径（用户确认）
当前只有 **Gate 的开关与来源门禁**部分可验收；**P0 整体未完成**。已有测试不证明真实运行可被取消（取消语义在 §10 设计，尚未实现）。

**v4.2 → v4.3**：补 P0 剩余实现（target 模型、`domain_state`/`engine_capabilities` 真实操作、Kit Provider、Unity 安装器与 Provider、JSON DOM 迁移到中立命名空间）；新增第 19 节进度与验收口径。

### 19.4 Workbench 可见性与一致性（v4.4）
| 面 | 机制 | 状态 |
|---|---|---|
| Overview 实时数据卡片 | `WorkbenchRuntimeKitCatalog.SnapshotStateKits` 加入 `Engine`，Overview 的 `CreateSnapshotCards` 按 `state.Snapshots` 动态渲染——与 LogKit/FsmKit 同一条路 | ✅ 已实施（`WorkbenchDashboardServiceTests` 通过） |
| 命令面与 state snapshot | Kit registry 注册 Provider，`snapshot read --kit Engine` / `command send --kit Engine --action …` | ✅ Unity 内实测通过 |
| 工程模型 capabilities 清单 | `ProjectModelSourceScanner` 只扫 `Core/Runtime` 与 `Tools`；`Core/Editor` 下的描述符（System/Validation/Architecture/…/Engine）一律不进清单 | 保持现状（与其它 Core Kit 一致，且安装器会扫描 kit 引用，扩扫描根有波及） |
| 专属页面 | 每 Kit 手写页面（VM + axaml）；Engine 暂无，Overview 卡片已覆盖当前只读能力 | 待 P1 出现执行类操作后再评估 |

**v4.3 → v4.4**：Workbench 可见性对齐（Snapshot 清单 + 卡片机制说明）；发现并修复 `project refresh` 的 CRLF 哈希不匹配（校验器改为 LF 归一化）；修复 JSON DOM 搬迁导致的 `YokiFrame.Workbench.RuntimeCache.csproj` 编译失败；skill 的 `cli-commands.md` 补 Engine 动作与开关说明。

### 19.5 目标绑定与快照修正（v4.5，按评审复现修订）
| 评审 | 问题 | 修复 |
|---|---|---|
| ① 组合 target 误放行 | 请求只接受**单一** target（`YokiFrameEngineExecutionTargets.IsSingleTarget`）；组合值仅用于能力声明。原先的操作/宿主/请求三条交集检查改为「操作声明 ∩ 宿主承载」的显式判定，不再存在共同目标缺失仍放行的路径 |
| ② 豁免越过 target 校验 | 豁免**只跳过执行开关**；载荷解析、来源限制、目标绑定一律保留。目标无关行为（诊断与能力查询）改由 descriptor 的 `IsTargetAgnostic` **显式声明**，不再靠豁免隐式获得 |
| ③ 转义键名丢 target | 弃用 `JsonHelper` 顶层快速扫描，改用包内 JSON DOM 直接 `TryGetProperty`（`\u0074arget` 解码后同样命中）；payload 非法 JSON、非对象、未知或组合 target 一律 `EngineOperationInvalidPayload`，不再静默回退 editor |
| ④ snapshot 不增量刷新 | Provider 实现 `IYokiFrameSnapshotVersionedKitInteractionProvider`，`StateVersion` 由「引擎状态 + 执行开关」指纹驱动，宿主只在变化时重写文件快照；**无需修改 pump** |
| ⑤ 能力报告非逐 target | 报告改为 `targetAvailability[]` 逐目标矩阵（`Enabled` / `DisabledBySettings` / `UnsupportedByTarget` / `UnsupportedByHostTarget`），并与 Gate 共用 `YokiFrameEngineAvailabilityRules` 单一事实源，避免两处漂移 |

**测试**：EngineKit 60/60 通过（新增组合 target、转义键名、载荷校验、豁免越权、目标无关诊断、逐目标矩阵与 `StateVersion` 推进用例）。

**v4.4 → v4.5**：按评审复现修正组合 target、豁免越权、转义键名、快照增量版本与逐 target 能力矩阵；Workbench 新增 Engine 页面模块（`WorkbenchDefaultPageModules`，静态 section 投影，无需新 XAML）。

### 19.6 P1 实施进度（v4.6）
| 面 | 状态 | 说明 |
|---|---|---|
| 编辑器操作桥 | **进行中** | ✅ Unity `play_control`（提交即返回 + 域重载语义 + 幂等状态提示）；✅ Unity `scene_query`（有界遍历：深度 ≤8、节点 ≤2000、每节点组件 ≤24，超限 `truncated`，支持 `path`/`depth`/`includeInactive`）；⏳ `scene_mutate`、`asset_ops` |
| 共享 JSON 写出器 | ✅ | `YokiFrameEngineJsonBuilder`（公开，Core 与各适配器共用）；`YokiFrameEngineJsonWriter` 已收敛到它，字段名与顺序不变 |
| 用户入口执行器 | ⏳ 未开始 | 运行时契约层（属性/target/Context/结果模型）+ 扫描器 + Run Scheduler + `entry_*` + 请求索引 |
| 客户端重载适配 | ⏳ 未开始 | §12：Client/Workbench/CLI 三处改动，需与首个引入重载的功能同批交付 |

**验证提示**：`play_control` / `scene_query` 属 Unity 适配器，本机无 Unity 无法编译；请在 Unity 中编译后用 CLI 复核：

```
yoki command send --kit Engine --action engine_capabilities          # 应看到 play_control / scene_query 的逐 target 矩阵
yoki command send --kit Engine --action scene_query --payload '{"depth":2}'
yoki command send --kit Engine --action play_control --payload '{"command":"enter","confirmed":true}'
yoki command send --kit Engine --action domain_state                 # 复核是否真的进入 PlayMode
```

⚠️ `play_control` 与 `scene_query` 需要开关打开（`Engine/operations.enabled = true`）才能执行；只读的 `scene_query` 也受开关约束（它绑定 editor|play 目标，不是诊断豁免）。

**v4.5 → v4.6**：按 §14 进入 P1；新增公开 JSON 写出器与 Unity `play_control`/`scene_query`，写出器单元测试 6 例。

### 19.7 无落盘编排：yoki exec（v4.7）
问题：多步编排（发命令 → 等域重载 → 重试 → 断言）过去只能靠外部 shell 脚本，会在本地留下 `.ps1`/`.sh` 临时文件。
方案：新增 CLI 动词 `exec`，从 **stdin** 读 NDJSON 步骤并在 CLI 进程内顺序执行：
- 步骤：`{"command":[...]}`（自动继承外层 `--project`）、`{"wait":毫秒}`、`retry{attempts,delayMs}`、`expect{contains}`；
- 输出：每一步一行 NDJSON（`step`/`exitCode`/`ok`/`result`）+ 最后一行汇总；默认遇错停止，`--continue-on-error` 继续；
- 上限：256 步、单步重试 ≤120 次、间隔 ≤60s；禁止嵌套 `exec`；未重定向 stdin 时返回 `ExecInputMissing`；
- 实现：`Program.Main` 拆出 `internal static ExecuteAsync(args, cts)` 供 exec 进程内复用，逐步用 `Console.SetOut` 捕获输出后并入步骤结果。

**落盘策略（写进 Skill 文档）**：编排一律走 stdin，不写临时脚本；必须落盘时（如 T2 给 Unity 编译的生成源码）只允许项目内 `.yokiframe/` 受控目录，由 pruner 回收，禁止写系统临时目录。

**v4.6 → v4.7**：新增 `yoki exec`（stdin NDJSON 多步编排，零落盘）与 Skill 文档章节、6 个 CLI 用例；设计文档补落盘策略。

### 19.8 P1 主线完成：入口执行器 + 重载适配（v4.8）

**入口执行器（§6 / §10）已实现并自动化验证**：

| 层 | 内容 |
|---|---|
| 运行时契约（`Core/Runtime/EngineKit/`，进 Player） | `YokiFrameEntryAttribute`、`YokiFrameEntryTarget`、`YokiFrameEntryStatus`、`YokiFrameEntryAssertion`、`YokiFrameEntryResult`、`YokiFrameEntryContext`（分帧原语签名 + 失效钩子） |
| 编辑器层（`Core/Editor/EngineKit/Entry/`） | 目录扫描器与缓存、参数绑定与调用器、运行记录与请求索引存储（原子写/独占建索引/TTL 回收）、分帧上下文实现、Run Scheduler、`entry_list`/`entry_run`/`entry_result`/`entry_lookup`/`entry_cancel` |
| Unity 接线（`Core/Adapters/Unity/Editor/EngineKit/`） | `UnityEngineEntryHost`（编辑态同步开场景、播放态异步加载由 update 观察）、`UnityEngineEntrySchedulerDriver`（每帧 Tick + 每次域加载一次 Reconcile）、安装器注入目录/存储/调度器 |

对齐 §10.2 的关键实现点：提交即返回（先写记录与索引）；同一 `requestId` 幂等；`entry_result`/`entry_lookup` 纯读；取消与超时都是"请求"，宽限期内观察到用户代码退出才落 `Cancelled`/`Timeout`，否则 `Detached`；迟到完成只写 `lateResultPath` 不改终态；重载后孤儿运行判 `Unknown` 绝不重放；上下文失效后框架调用被拒绝并计入 `staleContextCalls`。

**本轮修正的一处设计缺陷（测试暴露）**：上下文失效时若只是停止推进帧，停在 `WaitFrames` 上的用户代码永远观察不到取消，只能等宽限期被判 `Detached`——协作取消形同虚设。修法：失效时**先触发取消令牌、再唤醒已登记的帧等待**，让用户代码有机会自行退出，`Cancelled`/`Timeout` 才真正可达。

**§12 客户端重载适配已实现**：`CommandExecutionService` 在身份变化时读回已落盘终态响应（标注 `ReloadedSession`），读不到则以保留 `commandPath`/`responsePath` 证据的方式抛 `HostIdentityChanged`；CLI 把 `HostIdentityChanged`/`EngineReloading` 映射为 `Unknown`（先查证、不重放）。`CommandExecutionResult` 新增可选 `Warning` 与结论覆盖。

**R5 漂移守卫**：新增 `EngineCapabilityManifestTests`——① `capability.json` 的动作集合必须与 Provider 注册的命令面一致；② 清单 `sourceHash` 必须等于 `YokiFrameEngineKitProvider.cs`（LF 归一化）的 SHA-256；③ 每个动作指向存在的校验配方。清单已同步为 9 个动作 / 6 个配方。

**本轮验证**：EngineKit 93/93；Tooling 324/324；CLI 68/68；Installer.Core 138/138；Avalonia 424/424；`Core/Editor` 构建 0 警告 0 错误。

**仍未完成（诚实清单）**：`scene_mutate` / `asset_ops` 尚未实现；会话身份（`sessionId`/`generation`）仍需给 pump 加受控访问器；`entry_run` 的 runtime target 在 Unity 无宿主（§4/§14 已声明）；Godot 纵切（P2）未开始；§12 的重载行为只在自动化层面覆盖，真机需在 Unity 里用 `play_control enter` 复核。

**v4.7 → v4.8**：补齐 P1 主线（入口执行器 + 重载适配 + 漂移守卫），并把"取消必须唤醒帧等待"这条写进 §10.2 的实现注记。

### 19.9 P2 第一半：Godot Runtime 的 Engine Kit（v4.9）

**已实现（`Core/Adapters/Godot/Runtime/EngineKit/`）**：

| 文件 | 作用 |
|---|---|
| `GodotEngineSettingsSource` | Godot 宿主开关：读 `project.godot` 的 `yokiframe/engine/operations_enabled`；缺失/类型不符/异常一律 fail-closed。访问器可注入，因此判定逻辑可脱离 Godot 进程单测 |




### 19.10 P2 第二半：Godot Editor 的命令面聚合（v4.10）

**改动**：`GodotEditorFileBridgeHost` 的命令面从"只有 System 三个只读命令"改为 **System + catalog 显式注册的 Kit Provider**：

- `CreateCommandDispatcher` 聚合 `YokiFrameToolKitInteractionCatalog` 的 Provider（不引入 `YokiFrameCoreKitInteractions.CreateDefault`，避免一次性把 6 个 Core Kit 塞进编辑器，保持最小姿态）；
- 命令目录按 Kit 分组：System 组只列 System 自己的描述符，其余按 Provider 的 Kit 分组（**这条是既有测试抓出来的**：聚合后 `policy.AllowedCommands` 会把 Kit 命令混进 System 组）；
- 新增 `RefreshCatalogKitsIfNeeded`：catalog revision 变化时重建命令策略，因此**插件启动顺序无关**，后注册的 Kit 也无需重启宿主即可被服务。

**Godot Editor Engine Kit**（`Core/Adapters/Godot/Editor/EngineKit/`）：`GodotEditorEngineOperationProvider`（`HostTargets = editor`，播放时报告 `PlayMode` + runtime 目标）、`GodotEditorEntryHost`（EditorInterface 同步开场景）、`GodotEditorEngineKitInstaller`（catalog 注册 + Process 推进 + 退出释放）。插件在 `_EnterTree` **建立 Editor Host 之前**安装，`_Process` 推进，`_ExitTree` 释放。

**状态发布链（C12 第三项）**：新增 `GodotEditorFileBridgeHost.Snapshots.cs` —— 按 catalog Provider 的 `SnapshotNames` 写 `snapshots/<kit>/<name>.json`，信封字段与 Godot Runtime 宿主一致（`engineId`/`kit`/`name`/`generation`/`sequence`/`writtenAtUtc`/`payloadJson`；类型各自持有，因为两侧是不同程序集，契约是 JSON 形状）。版本化 Provider 只在 `StateVersion` 变化时落盘（`telemetryAvailable=false`，编辑器没有共享内存遥测），未声明版本的 Provider 跟随 catalog revision 写一次。

**验证**：Godot.Editor 21/21（新增 4 例：editor 目标/身份/模式、Engine+entry 命令面且 runtime 目标被拒、catalog 分组与 revision 重建、快照链的发布/不重写/更新）；Godot.Runtime 80/80。

### 19.11 P2 收官：Godot 场景操作（v4.11）

**新增**（`Core/Adapters/Godot/{Runtime,Editor}/EngineKit/`）：

| 文件 | 作用 |
|---|---|
| `GodotSceneQuerySupport`（Runtime 程序集，共享） | 路径解析、有界遍历与 JSON 投影：深度 ≤8、节点 ≤2000、每节点子节点 ≤256；编辑器与 Runtime 共用同一份实现与同一份 payload |
| `GodotRuntimeSceneQueryOperation` / `GodotEditorSceneQueryOperation` | 分别读运行中场景树与 EditorInterface 编辑中场景；节点信息含 `name`/`path`/`class`/`active`/`childCount`/`groups`/`scenePath`，2D/3D 节点附加 `transform` |
| `GodotSceneMutateSupport` + `GodotRuntimeSceneMutateOperation` / `GodotEditorSceneMutateOperation` | create/delete/setActive/setTransform/save |

**与 Unity 的契约一致性**：payload 完全相同（`path`/`depth`/`includeInactive`；`op`/`path`/`name`/`active`/`position`/`rotation`/`scale`）。差异只在引擎语义，且都**如实回报**：Godot 用 `nodeType`（Unity 用 `components`）；删除是延迟释放 `deferred=true`；**Runtime 没有 Undo、也不能保存场景**（`undo=unavailable-in-runtime`，save → `EngineOperationUnavailable`）；编辑器用 `EditorUndoRedoManager` 登记动作并 `MarkSceneAsUnsaved`，登记失败时降级并回报 `undo=unavailable`。

**验证**：Godot.Runtime **90/90**、Godot.Editor **29/29**、EngineKit **93/93**。

**覆盖边界（诚实说明）**：Godot 场景操作与快照的正常路径需要**活的 Godot 进程**（合法载荷会触碰 `EditorInterface`/`SceneTree` 等原生单例，在测试进程里可能直接终止进程）。本机覆盖的是操作描述符、载荷校验的全部失败路径、能力矩阵逐 target 可用性与共享内核边界。

### 19.12 P3（可选）：T2 动态 eval（v4.12）

**新增**：

| 层 | 内容 |
|---|---|
| Core（`Core/Editor/EngineKit/Eval/`） | `YokiFrameEngineEvalSourceGenerator`（生成源码，含 Token 常量与 `[YokiFrameEntry]` 声明）、`YokiFrameEngineEvalRequest`（内联 code / 项目内 codeFile + hash 校验）、`YokiFrameEngineEvalTypeProbe`（**类型全名 + Token** 双匹配，不按程序集名）、`YokiFrameEngineEvalStore`（记录持久化）、`YokiFrameEngineEvalService`（编译阶段状态机）、`eval` / `eval_result` / `eval_prune` 三个操作 |
| Unity 接线 | `UnityEngineEvalHost`（写 `Assets/YokiFrame.Eval/Editor/`、`AssetDatabase.Refresh`、用 `CompilationPipeline.assemblyCompilationFinished` 只收集生成目录内的编译错误）、安装器装配 eval 服务、每帧驱动同时推进编译阶段 |

**关键设计**：把"编译"抽象成 `IYokiFrameEngineEvalHost` 接缝后，eval 状态机（Compiling → Ready / CompileFailed → 提交运行 → 复用 entry_run 的调度与结论语义）**可以在没有引擎进程时被完整单测**。

**规则落地**：① 生成目录固定（Unity 特殊目录 → Assembly-CSharp-Editor，不建 asmdef）；② 探测只认 Token，同名残留不会被误判；③ 编译失败**不依赖域重载**即可落 `CompileFailed`（读到错误或等待超时）；④ 就绪后经入口目录提交运行，断言/异常/取消语义与 `entry_run` 一致；⑤ `eval_result` 纯读；⑥ `eval_prune` 回收生成源码与记录（§13）。

**验证**：EngineKit **99/99**（新增 6 例）。**诚实边界**：Unity 侧"真实写盘 → 编译 → 反射调用"需要活的 Unity 编辑器；**Godot 侧 eval 未做**。
### 19.13 会话身份缺口关闭：读已发布 registry，零改 pump（v4.13）

§19.2 一直把"会话身份（`sessionId`/`generation`）"列为待实施，理由是 pump 把它们存在私有静态字段里；文档同时把"不改 pump"当成硬约束，两者相互矛盾。

**解法**：不需要访问 pump —— pump **本来就把身份写进了已发布的 `engine.json`**（`sessionId` / `generation` 为根级字段）。新增 `YokiFrameEngineHostIdentityReader`（Core/Editor，引擎无关）按 (存在性, mtime, 长度) 缓存读取，Unity Provider 在 `ReadDomainState` 时用它填 `sessionId`/`generation`/`sessionIdentityAvailable`。

- 读不到时**不伪造**：`SessionIdentityAvailable=false`，并给出原因（文件缺失 / JSON 非法 / 尚未发布 sessionId 三种）。
- 缓存按 mtime + 长度失效，宿主每帧的状态指纹不会带来每帧文件读取。
- 该读取器对任何宿主通用（Godot 的 `engine.json` 同样是这个形状），后续可复用。

**验证**：EngineKit **103/103**（新增 4 例：正常读取、字符串代次兼容、三种不可用原因、缓存跟随更新）。

**v4.12 → v4.13**：关闭会话身份缺口（零改 pump），并补齐审查清单文档。

---

### 19.14 Workbench Engine 页面接入真实数据（v4.14）

Workbench 只读快照文件、看不到命令返回值。与其为页面新开一条命令管线，**把能力矩阵与入口目录摘要并入 Engine state snapshot**：

- `YokiFrameEngineSnapshotWriter`（Core/Editor）：domain_state 字段 + `hostTargets` + `capabilities[]` + 入口目录摘要（最多 50 条，超出置 `truncatedEntries`）；
- 矩阵与 `engine_capabilities` 共用 `YokiFrameEngineCapabilityProjection`（判定仍来自与 Gate 同源的 `YokiFrameEngineAvailabilityRules`），两处不会漂移；
- `domain_state` 保持精简（高频只读命令不带矩阵），两者共用 `WriteDomainStateInto` 保证 domain 字段逐字一致；
- 快照版本指纹扩展到**命令面与入口目录**：新增操作或入口失效都会让快照重写，页面不会停在旧矩阵。

页面侧 `CreateEngineSections` 现在展示：承载目标、**逐操作一行**（kind → `editor:Enabled, play:DisabledBySettings`）、入口数量与清单（含 invalid 原因）、会话标识与代次；命令提示降级为"看明细"。

**验证**：EngineKit **105/105**（新增 2 例：快照携带矩阵与入口摘要、命令面变化推进版本；并修正了 `WriteDomainState` 缺少闭合对象导致 JSON 损坏的缺陷）；Avalonia 侧新增 2 例端到端投影（真实 dashboard 解析快照信封 → 反射调用投影器断言矩阵与入口行，2/2 通过）。

**v4.13 → v4.14**：Engine 页面接入真实数据（能力矩阵 + 入口目录），并把命令面/入口纳入快照版本指纹。

**v4.14 → v4.15（核对清理）**：本节的锚点插入被执行了两次且落在 `## 19.` 之前，已去重并移到 §19.13 之后；删除两处死代码（`YokiFrameEngineCapabilitiesOperation.ResolveTargets`、`YokiFrameEngineKitProvider.ToArray`）；Engine 页面取消 Unity 专属可见性（Godot Editor / Runtime 同样注册 Engine Kit 并发布 state 快照），快照缺失分支补恢复提示，并补上入口 invalid 原因与截断标记的断言；刷新 Engine（因删死代码）与 EventKit（工作副本行尾变化、内容未变）的 `capability.json` sourceHash。

**复跑结果**：EngineKit **105/105**、Protocol **73/73**、Avalonia **427/427**、Cli 68/68、Tooling 324/324、Installer.Core 138/138、Packaging 108/108、Client 83/83、Godot.Editor 29/29、Godot.Runtime 90/90、Godot.Player 1/1。Unity 侧只读抽查：`.yokiframe/engines/unity-editor/snapshots/Engine/state.json` 含 `hostTargets`、14 行能力矩阵、入口摘要与会话身份，Editor.log 最后一次编译成功。
### 19.16 Unity-only 代码的编译审计（v4.16）

**触发**：真机 Unity 首次编译报 `CS0165: Use of unassigned local variable 'sessionId'/'generation'`——我把 `out` 变量写在 `&&` 短路右侧，左侧为 false 时它们从未被赋值。

**为什么会漏**：`Core/Adapters/Unity/Editor/**` 既无 csproj（dotnet 套件编不到），也**无 asmdef**（Unity 并入 Assembly-CSharp-Editor）。这一层只有真机 Unity 能编译——属结构性验证盲区，不是偶发。

**修复**：`out` 变量在短路判断之前声明。

**无需 Unity 也能做的审计（本轮全部执行）**：

| 检查 | 方法 | 结果 |
|---|---|---|
| 跨程序集可见性 | 实测 asmdef 数量与边界 | 包内 **52 个** asmdef；`Core/Editor` 是单一 asmdef `YokiFrame.Editor`，Unity 适配器是 `YokiFrame.Unity.Editor` 且引用它——**跨程序集，internal 默认不可见**；关键引用（路径集、`ENGINE_ID`、identity reader、eval 服务）的可见性由真机编译裁决：**已通过**（快照写出 `sessionId` 即证明 identity 链路可用） |
| 预处理器配对 | 逐文件数 `#if` / `#endif` | 9/9 文件均为 1:1 |
| 命名空间 | 逐文件读 `namespace` | 全部 `namespace YokiFrame` |
| 裸引擎类型 | 对无 `using UnityEngine` 的文件搜 `Object`/`Debug`/`Application`/`Vector3`/`Mathf`… | 无命中（唯一候选是 `JsonValueKind.Object`，JSON 枚举） |
| 接口实现完整性 | 接口定义 × 实现逐成员对照 | `IYokiFrameEngineOperationProvider` **7/7**；`IYokiFrameEngineEvalProvider.Eval`、`IYokiFrameEngineEvalHostProvider.EvalProjectRoot` 均在 |
| 短路 `out` 全量扫描 | `Core` 全树 grep `&&…out` / `\|\|…out` | 其余命中均在 `if` 条件内使用，明确赋值成立；Unity 适配器仅此一处 |

**验证**：Core/Editor 0 错误；EngineKit 105/105；Godot Runtime/Editor（`-p:YokiFrameToolsBuild=True`，真编译）各 0 错误；Avalonia 全量 **427/427**（此前 1 例 TableKit 失败单跑 17/17，确认偶发）。真机侧：`.yokiframe/engines/unity-editor/snapshots/Engine/state.json` 可解析出 `hostTargets` 与能力矩阵，与 Workbench 投影契约一致。

**纪律（v4.21 更新）**：任何新增/修改 Unity 适配器代码后，**先跑 §19.21 的本地编译门**（它是真正的编译器），再补跑本表六项语义检查。
### 19.21 Unity 本地编译门（v4.21，堵住最大的验证盲区）

**背景**：`Core/Adapters/Unity/**` 既没有 csproj（dotnet 套件编不到），也没有独立 asmdef 边界，过去**只有真机 Unity 能编译它**——CS0165 就是这么漏出去的。

**做法**：本机存在真机 Unity 2022.3.16f1（`G:\Unity\2022.3.16f1`），于是新增 `YokiFrameWorkbench~/tests/YokiFrame.Unity.CompileGate/`：

1. **照抄 Unity 生成的 `Assembly-CSharp-Editor.csproj` 的引用集**（231 个引用，含 UnityEngine 模块、UnityEditor、System.Memory 等）——脚本生成，避免手工维护；
2. 采用与 Unity 相同的目标框架与语言版本（`net471` / C# 9）；
3. **刻意不定义 `YOKIFRAME_TOOLING`**，只定义 `UNITY_EDITOR` 系列宏，精确模拟 Unity 侧编译环境；
4. 编译 `Core/Runtime` + `Core/Editor` + `Core/Adapters/Unity/Editor/{EngineKit,FileBridge,FastChannel,Harness,Context}`。

**结果**：0 错误 0 警告 ✅。**从此 Unity 适配器的语法/类型/接口错误可在本机发现**，不再依赖真机编译兜底。

**覆盖面（刻意限定）**：只覆盖引擎操作面及其依赖，**不**覆盖整个 Unity 适配器。原因：Unity 用 5 个 asmdef（Shared / Runtime / Editor / Inspector.Runtime / Inspector.Editor）分开编译，而 **asmdef 的引用边界会影响名字解析**——把 `Editor/Inspector/**` 一并塞进单程序集门后，实测出现 `CS0118「Editor」是命名空间但被当作类型使用`：在 Unity 里 Inspector.Editor 不引用 Editor asmdef，`Editor` 解析为 `UnityEditor.Editor`；合并后 `YokiFrame.Unity.Editor` 这个命名空间可见了，于是解析结果改变。**结论：要覆盖其它子树，应按 asmdef 各建一个门，而不是合并。**

**踩坑记录**（供复用时参考）：① 根目录 `Managed\UnityEngine.dll` 是旧门面，与模块 DLL 并存会 `CS0433`，必须引 `Managed\UnityEngine\UnityEngine.dll`；② 2022.3 的 `UnityEditor` 没有模块拆分，`Managed\UnityEditor.dll` 即编辑器程序集；③ `netstandard2.1` 缺命名管道 ACL 重载（`NamedPipeServerStream` 10 参），必须 `net472`；④ FastChannel 需要 `System.IO.Pipes.AccessControl` / `System.Security.Principal.Windows`（Unity 生成物里已含）。

### 19.17 Godot 侧 eval 第一片：脚本 eval 接缝与服务（v4.17）

**为什么不能照搬 Unity 路径**：Unity 的 eval 依赖 `AssetDatabase.Refresh` + `CompilationPipeline` + 域重载后的类型探测；Godot 的 C# 由 .NET SDK 编译、程序集在进程启动时装载，没有等价的「改完即生效」通道。按既有决策⑥，Godot 侧走 **GDScript 运行时编译**，C# 走 T0（`entry_*` 反射入口，不做动态编译）。

**新增接缝（引擎无关）**：`IYokiFrameEngineScriptEvalHost` —— `TryCompile(id, token, code)` / `TryInvoke(id, token, out resultJson)` / `Unload(id)` / `Language`。任何「运行时能编译脚本」的宿主都能复用。

**新增服务**：`YokiFrameEngineScriptEvalService`（+ `IYokiFrameEngineScriptEvalService` / `IYokiFrameEngineScriptEvalProvider`）。与 C# eval 的差异来自编译模型：① 编译同步，`Submit` 返回即终态（Ready / CompileFailed），无轮询；② 不落盘、不做类型探测、不受域重载影响（`SourcePath` 记为内存标记）；③ 调用结论直接写在记录的 `Note` 上（C# 路径的结论来自运行调度器）。记录复用同一个 `YokiFrameEngineEvalStore`，因此 `eval_result` / `eval_prune` 对两类 eval 行为一致。

**命令面接线**：`eval` 新增可选 `language`（缺省 `csharp`；`script` 或宿主自身语言走脚本路径，未知语言 → `EngineOperationUnsupported`；脚本路径拒绝 `codeFile` → `InvalidPayload`）。Provider 在**任一** eval 子系统可用时注册 eval 三件套；`eval_result` 先查 C# 服务再查脚本服务；`eval_prune` 同时回收两者并回报 `removedScripts`。**动作面仍是 14 个**（是 `eval` 的 payload 扩展，不是新动作）。

**验证**：EngineKit **117/117**（新增 12 例：服务 6 + 接线 6）。
### 19.18 Godot 侧 eval 第二片：GDScript 真宿主与接线（v4.18）

**真宿主**：`GodotScriptEvalHost` 把用户代码包成 `extends RefCounted` + 令牌注释 + `func run():`（函数体制表符缩进），编译后立即调用；令牌不匹配（同名重编译）直接拒绝。`Unload` 只释放句柄——**不落盘、无生成目录**。

**原生调用隔离**：`IGodotScriptCompiler` 接缝 + `GodotGdScriptCompiler` 真实现（`GDScript.SourceCode` → `Reload()` → `Call("new")` → `Call("run")` → `Json.Stringify`）。宿主逻辑（包装、令牌校验、错误归类、卸载）在注入假编译器下**全部单测**，真实现只做调用不做判断——延续「Godot 静态 API 不碰测试进程」的纪律。

**接线**：Godot Runtime/Editor 两个 Provider 都实现 `IYokiFrameEngineScriptEvalProvider`，安装器在构造 Kit Provider **之前**接上 `YokiFrameEngineScriptEvalService` + 记录存储。eval 操作声明的是 Editor 目标，因此运行时宿主上仍不可用（与 Unity 一致）。

**踩坑**：`YokiFrame.Json` 命名空间会遮蔽 Godot 的 `Json` 类（`CS0234`）——Godot 类型在适配器里必须用 `Godot.` 前缀显式限定；已把 `Error` / `Variant` / `GodotObject` / `Json` 全部限定。

**验证**：Godot.Runtime **96/96**（新增 6 例宿主单测：包装与缩进、令牌校验、同 id 重编译释放旧句柄、编译失败不建句柄、Unload、调用失败传播）；Godot.Editor **30/30**（含锁定用例：编辑器命令面含 eval 三件套 + 端到端 GDScript 提交 → Ready → eval_result 可读）；两个适配器 `-p:YokiFrameToolsBuild=True` 真编译各 0 错误；EngineKit 117/117。
**接线回归（锁定用例抓到的真 bug）**：`YokiFrameEngineKitProvider` 装配 eval 三件套的条件是「宿主实现 `IYokiFrameEngineEvalProvider` 且（C# 服务或脚本服务非空）」。Godot 两个 Provider 起初**只**实现了 `IYokiFrameEngineScriptEvalProvider`，于是条件不成立——**eval 根本没注册**，而当时的单测全绿（它们直接构造 Provider 绕过装配）。补的锁定用例（Godot Editor 命令面含 eval + 端到端 GDScript 提交与读取）当场复现了这个空档。修法：Godot Runtime/Editor 两个 Provider 同时实现 `IYokiFrameEngineEvalProvider`，`Eval` 如实返回 null（Godot 不做 C# 动态编译，C# 走 T0）。

**教训**：接缝装配的正确性只能由「经过装配路径」的用例保证——直接构造 Provider 的用例覆盖不到它。



**下一步**：Godot 真宿主（见 §19.18，已完成）。
### 19.19 快照追加最近入口运行（v4.19）

**动机**：Workbench 只读快照文件，看不到 `entry_*` 命令的返回值；审查者想知道「入口执行器到底跑了什么」只能自己发命令。

**改动**：① `IYokiFrameEngineRunScheduler` 增加纯读的 `ReadRecentRuns(limit)`（按提交时间倒序，调度器从运行存储 `ReadAll()` 取）；② 快照写出器追加 `recentRunCount` / `truncatedRuns` / `recentRuns[]`（每条含 runId / requestId / entry / target / state / errorCode / resultPath / 提交与更新时间，最多 10 条）；③ 版本指纹纳入最近运行（新运行或状态变化都会让快照重写，页面不会停在旧记录）；④ Engine 页面新增「Recent runs」段落。

**验证**：EngineKit **118/118**（新增 1 例：空快照 `recentRunCount=0` → 提交并跑完一个入口后 `recentRunCount≥1`、快照里出现 runId、且 `StateVersion` 推进）；Avalonia 全量 **428/428**（投影用例断言 `Recent runs: 1 recorded` 与 `combat.smoke: Succeeded @ editor (run-42)`）。

### 19.23 eval 生成代码的 CS0161（v4.23，用户真机编译发现）

**现象**：用户跑冒烟到 `eval` 这一步，Unity 报 `Assets/YokiFrame.Eval/Editor/Eval_smoke1.cs(17,56): error CS0161: 'Eval_smoke1.Run(YokiFrameEntryContext)': not all code paths return a value`。

**根因**：生成器把用户代码直接放进 `public static async Task<YokiFrameEntryResult> Run(...)` 的方法体，但没有兜底 `return`。于是**只做副作用的 eval（例如文档示例里那句 `ctx.Log(...)`）必然编译失败**——文档中最基本的用法就是坏的。

**修复**：生成器在用户代码之后追加三行：`#pragma warning disable CS0162, CS1998` / `return ctx.Pass();` / `#pragma warning restore CS0162, CS1998`。`ctx.Pass()` 是框架的结果工厂；同时压掉「不可达代码 / 缺少 await」两类警告，避免用户工程开 warnings-as-errors 时误伤。另外把用户当时已生成的 `Eval_smoke1.cs` 就地补上同样三行，使其无需等待重新生成即可编译。

**验证**：Unity 编译门 0 错误；EngineKit **127/127**。**教训**：生成器产出的代码必须至少编译一次——这次是真机替我们发现的（本机编译门只覆盖手写代码，不覆盖生成产物）。
### 19.22 原子写回归单源（v4.22）

**发现**：共享原子写 `YokiFrameAtomicFileWriter` 的注释明确写着「禁止在调用方再复制私有原子写实现」，但 `YokiFrameEngineRunStore` 与 `YokiFrameEngineEvalStore` **各私有复制了一份**——而且复制版用**固定临时文件名** `path + ".tmp"`：两个宿主同时写同一条记录会互相踩；同时缺少共享版的 flush 与「备份-提交-恢复」兜底。

**改动**：两处私有实现删除，改为调用共享写出器（调用点不变）。

**验证**：EngineKit **127/127**（新增 3 例，从**公开存储入口**验证原子写行为：覆盖成功、**目录内不残留 `.tmp`**、UTF-8 无 BOM；共享实现是 internal，因此走真实调用点而不是直接测内部）；Unity 编译门 0 错误；原有存储用例全部通过，说明行为兼容。

**性能修正（v4.20.x）**：版本指纹会被宿主泵**每帧**读取（Unity 泵挂在 `EditorApplication.update` 上），而运行枚举要读目录——因此 `ReadRecentRuns` 加了 250ms TTL 缓存，避免每帧一次目录枚举；新增用例断言「TTL 内不重新枚举、超过 TTL 后刷新到最新」。

**续（v4.20.x）**：入口目录与最近运行做了 join——每个入口就地显示「`last run: Succeeded @ editor (run-42)`」，审查者加完 `[YokiFrameEntry]` 就能一眼看到它上次跑成什么样（Avalonia 投影用例已断言该文本）。

**环境提示**：本轮有一次 Avalonia 全量出现 12 个偶发失败，随后两次复跑均为 428/428、`FAILCOUNT=0`；怀疑与 Unity 编辑器在测试期间重编译包、抢占文件有关（不是代码回归）。
### 19.20 运行记录的跨进程认领（v4.20）

**缺口**：`ClaimNextQueued` 只按记录状态取待运行项。同一项目目录下若有两个宿主（旧编辑器会话、另一个编辑器实例）同时扫描，两者都会看到同一条 `Queued` 记录并各自 `Claim` → **重复执行用户代码**。原设计只在域内加锁，跨进程没有互斥。

**改动**：① `YokiFrameEngineRunStore.TryClaim(runId, owner, generation, lease)` —— 用 `FileMode.CreateNew` 独占创建 `<runId>.claim` 提供原子性（只有一个进程能创建成功）；文件已存在时，**只有租约过期或内容不可读**才允许接管；② 认领文件刻意不带 `.json` 后缀，因此不会被 `ReadAll` 当成运行记录；③ 调度器在扫描时先认领再执行，认领失败直接跳过；终态记录惰性释放认领文件；④ 租约只需覆盖 `Queued → Running` 窗口（30s），因为一旦进入 `Running`，`TryClaim` 会因状态不符而拒绝。

**验证**：EngineKit **123/123**（新增 5 例：外来未过期认领阻止本地执行且记录保持 `Queued`；过期认领可被接管且**只**产生一次 `claim` 步；终态后认领文件被惰性清理；第二个宿主随后扫描不会二次执行；**owner 含引号/反斜杠时认领文件仍是合法 JSON**（修掉一处失败开放：破损 JSON 曾被当作租约过期而允许接管）——`claim` 步数与 `Attempt` 都保持 1）。Godot.Runtime 96/96、Godot.Editor 30/30、Tooling 324/324、Godot 适配器真编译 0 错误。



**勘误（同日）**：本节初稿把「包内 asmdef 为 0」当作结论，实际是我用的 glob 模式没匹配上（包内实为 52 个）。已按实测重写该行——结论不变（无 CS0122），但依据从「单编译单元」改为「真机编译通过 + asmdef 边界实测」。教训：**glob 返回空不等于不存在**，凡用作结论必须用第二种手段复核。
