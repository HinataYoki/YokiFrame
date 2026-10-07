# RoslynKit 审查与验证清单（v4 历史归档）

> 历史归档：下文保留 v4 原始验收与测试记录，不代表当前要求或本轮验证结果。以 [v5 当前设计](Engine-Operation-Glue-Design.md) 和 [当前验收清单](Engine-Operation-Review-Checklist.md) 为准；不要直接执行本历史文档中的示例，尤其不要自动重试有副作用的提交或修改用户的 Main Camera。
## -1. 30 秒结论

- **范围**：P0–P3 全部实现（含 Godot 侧 GDScript eval）；P4 可选项（TestRunnerApi / 共享内存 telemetry / Unity runtime target）未做。
- **本机自动化（全绿，共 1477 例）**：RoslynKit **127** · Cli **69** · Tooling **324** · Installer.Core **138** · Avalonia **428** · Godot.Editor **30** · Godot.Runtime **96** · Godot.Player **1** · Protocol **73** · Client **83** · Packaging **108**。
- **基线记录（最近一次全量复跑）**：RoslynKit **127**（其余套件上次复跑值见上表；本节改动只影响 RoslynKit 与编译门）；Unity 编译门 `0 错误`；Godot Runtime/Editor 适配器（`-p:YokiFrameToolsBuild=True`）各 `0 错误`。任何一项对不上就说明工作区变了，先查再判。
- **Unity 适配器已可本机编译**（v4.21 起）：`dotnet build YokiFrameWorkbench~/tests/YokiFrame.Unity.CompileGate` → 期望 0 错误。它用真机 Unity 2022.3 的程序集编译 Unity 分支代码，堵住了「只有真机能编译」的盲区。
- **文档与代码一致性已核对**：动作面 14 个（三方一致）；`entry_run` = `{entry,target,args}`（Dangerous 另需 `confirmed:true`）、`entry_result` = `{runId}`、`entry_lookup` = `{requestId}`、`entry_cancel` = `{runId}`；`eval` 的 `language` 与 `--payload` 传参（真机实测 `-p` / `--source` 会 `UnknownCommand`）。
- **真机已验证（只读 / 拒绝路径）**：Unity 上 `domain_state`、`engine_capabilities`、`entry_list`、`snapshot read --detail full` 成功；执行类操作被开关拒绝；单值 target 规则、转义键名、Dangerous 确认、未知 id 错误码逐条与文档一致——见 §4「真机已确认的契约」。
- **仍未验证**：执行类操作的**正常路径**（真进播放、真改场景、真跑入口、eval 真编译执行）。需要先打开开关，再跑 §2.0 那一条命令。
- **三方一致性**：`capability.json`、真机 `engine_capabilities`、技能文档的动作面均为 **14 个**，kind 分布一致（ReadOnly 7 / Dangerous 4 / UserAction 2 / Maintenance 1）。


本文给"接手审查的人"一条最短路径：先跑自动化，再在 Unity / Godot 里做真机验证，最后看未验证项。

## 0. 已交付内容（按阶段）

| 阶段 | 内容 |
|---|---|
| P0 | RoslynKit 契约、Gate（执行开关 / Dangerous 来源 / 目标绑定）、`domain_state`、`engine_capabilities`、capability.json 一致性守卫 |
| P1 | 编辑器操作桥（`play_control` / `scene_query` / `scene_mutate` / `asset_ops`）、用户入口执行器（`entry_*`）、客户端重载适配（§12） |
| P2 | Godot Editor 命令面聚合 + 状态发布链、Godot Runtime 宿主接线、Godot 场景操作 |
| P3 | T2 动态 eval（`eval` / `eval_result` / `eval_prune`）：Unity 走 C# 生成源码编译；Godot 走 GDScript 内存编译（C# 走 T0 反射入口） |

## 1. 自动化（本机可复现，全部应为绿色）

```powershell
cd "<包根>"   # 即 Assets/YokiFrame
dotnet test YokiFrameWorkbench~/tests/YokiFrame.RoslynKit.Tests
dotnet test YokiFrameWorkbench~/tests/YokiFrame.Cli.Tests
dotnet test YokiFrameWorkbench~/tests/YokiFrame.Tooling.Application.Tests
dotnet test YokiFrameWorkbench~/tests/YokiFrame.Installer.Core.Tests
dotnet test YokiFrameWorkbench~/tests/YokiFrame.Workbench.Avalonia.Tests
dotnet test YokiFrameWorkbench~/tests/YokiFrame.Godot.Editor.Tests
dotnet test YokiFrameWorkbench~/tests/YokiFrame.Godot.Runtime.Tests
dotnet test YokiFrameWorkbench~/tests/YokiFrame.Godot.Player.Tests
```

最近一次结果（v4.18，Godot 侧 eval 完成后）：RoslynKit **117**、Cli 68、Tooling 324、Installer.Core 138、Godot.Editor **30**、Godot.Runtime **96**、Godot.Player 1（另加 Protocol 73、Client 83、Packaging 108，三者不在上面命令清单里）；Avalonia 最近实测 **427**。

**Unity 适配器也能在本机编译了**（v4.21，用真机 Unity 2022.3 程序集；此前只有真机 Unity 能编译它）：

```powershell
dotnet build YokiFrameWorkbench~/tests/YokiFrame.Unity.CompileGate
```

期望 **0 错误**；它覆盖 `Core/Runtime` + `Core/Editor` + Unity 的 RoslynKit/FileBridge/FastChannel/Harness/Context。若你的 Unity 装在别处，改 `YokiFrame.Unity.CompileGate.csproj` 里的引用路径（或按同目录 `Assembly-CSharp-Editor.csproj` 重新生成）。

Godot 适配器要带 TOOLS 才是真编译（不带就是空编译且"成功"）：

```powershell
dotnet build Core/Adapters/Godot/Runtime/YokiFrame.Godot.Runtime.csproj -t:Rebuild -p:YokiFrameToolsBuild=True
dotnet build Core/Adapters/Godot/Editor/YokiFrame.Godot.Editor.csproj  -t:Rebuild -p:YokiFrameToolsBuild=True
```

## 2. Unity 真机验证（交互式步骤**尚未做过，请优先**）

> 2026-10-05 只读抽查已做：`.yokiframe/engines/unity-editor/snapshots/RoslynKit/state.json` 含 `hostTargets`、14 行能力矩阵、入口摘要与会话身份（`sessionId` + `generation`），Editor.log 最后一次编译成功且无 `error CS`。下面的交互式步骤仍未做。

1. 打开 Unity 工程，等待编译；确认 Console 无编译错误。
2. 定位 CLI：
   ```powershell
   $proj = "<Unity 工程根>"
   $fp = (Get-Content "$proj\.yokiframe\runtime\com.hinatayoki.yokiframe\current.json" -Raw | ConvertFrom-Json).sourceFingerprint
   $yoki = "$proj\.yokiframe\runtime\com.hinatayoki.yokiframe\$fp\win-x64-aot\yoki.exe"
   ```
3. 关闭态应被拒（**不需要改配置**）：
   ```powershell
   & $yoki command send Engine RoslynKit --action scene_query --payload '{"depth":1}' --project $proj
   # 期望 EngineOperationDisabled；domain_state / engine_capabilities / entry_list / eval_result 仍可用
   ```
4. 打开开关（**这是唯一需要你改配置的一步**）：在
   `ProjectSettings/Packages/com.hinatayoki.yokiframe/editor-settings.json` 的 `settings` 数组里加一条：
   `{"kit":"RoslynKit","key":"operations.enabled","value":"true"}`。开关**即时生效，无需重启**。
5. 逐项冒烟（每行是"命令 → 期望"）：
   ```powershell
   & $yoki command send Engine RoslynKit --action engine_capabilities --project $proj
   # 期望 14 个操作；scene_query/scene_mutate 的逐 target 矩阵含 editor

   & $yoki command send Engine RoslynKit --action domain_state --project $proj
   # 期望 sessionId 非空、generation > 0、sessionIdentityAvailable=true（本轮新接入）

   & $yoki command send Engine RoslynKit --action scene_query --payload '{"depth":2}' --project $proj
   # 期望返回当前场景层级；大场景应有 truncated 标记

   & $yoki command send Engine RoslynKit --action scene_mutate --payload '{"op":"setTransform","path":"Main Camera","position":[0,1.5,0],"rotation":[0,0,0],"scale":[1,1,1],"confirmed":true}' --project $proj
   # 期望 applied:true 且 undo:registered；Unity 里 Ctrl+Z 应能撤销

   & $yoki command send Engine RoslynKit --action asset_ops --payload '{"op":"find","filter":"t:Scene","limit":5}' --project $proj

   & $yoki command send Engine RoslynKit --action entry_list --project $proj
   # 若项目里还没有入口，会返回空目录——这是正常结果，不是失败

   & $yoki command send Engine RoslynKit --action play_control --payload '{"command":"enter","confirmed":true}' --project $proj
   # 期望 accepted:true；随后 domain_state 应为 PlayMode；退出用 {"command":"exit"}
   ```
6. 入口执行器端到端（需要一个真实入口）：在任意编辑器脚本里加
   `[YokiFrameEntry("demo.smoke", Targets = YokiFrameEntryTarget.Editor)]` 的静态方法，然后
   `entry_run --payload '{"entry":"demo.smoke","target":"editor","confirmed":true}' ` → 拿到 `runId` →
   `entry_result --payload '{"runId":"..."}' ` 取结论。**提交成功不等于通过**。
7. eval（可选）：
   ```powershell
   & $yoki command send Engine RoslynKit --action eval --payload '{"id":"demo1","code":"ctx.Log("hi");
return ctx.Pass();","confirmed":true}' --project $proj
   & $yoki command send Engine RoslynKit --action eval_result --payload '{"id":"demo1"}' --project $proj
   & $yoki command send Engine RoslynKit --action eval_prune --payload '{}' --project $proj
   ```
   期望：`eval` 立即返回 `accepted`；`eval_result` 从 `Compiling` 变到 `Ready`（或 `CompileFailed` + 错误摘要）；生成目录 `Assets/YokiFrame.Eval/Editor/` 里能看到临时源码，prune 后消失。
8. 关掉开关即可恢复"只读可用、执行被拒"的初始状态。

## 2.0 一条命令跑完 Unity 冒烟（推荐先做这个）

`yoki exec` 从 stdin 读 NDJSON、**不落任何临时文件**。把下面整段粘进 PowerShell 即可按顺序执行，并逐条校验期望值（`expect.contains` 不满足即失败，并指出是哪一步）：

```powershell
$proj = "<Unity 工程根>"        # 例如 G:\...\My project (1)
$yoki = "<yoki.exe 路径>"
@'
{"command":["command","send","RoslynKit","RoslynKit","--action","domain_state"],"expect":{"contains":"engineKind"}}
{"command":["command","send","RoslynKit","RoslynKit","--action","engine_capabilities"],"expect":{"contains":"scene_query"}}
{"command":["command","send","RoslynKit","RoslynKit","--action","scene_query","--payload","{\"path\":\"/\",\"depth\":2}"],"expect":{"contains":"rootNodes"}}
{"command":["command","send","RoslynKit","RoslynKit","--action","entry_list"],"expect":{"contains":"entries"}}
{"command":["command","send","RoslynKit","RoslynKit","--action","eval","--payload","{\"confirmed\":true,\"id\":\"smoke1\",\"code\":\"ctx.Log(\\\"smoke\\\");\"}"],"retry":{"attempts":5,"delayMs":1000},"expect":{"contains":"accepted"}}
{"command":["command","send","RoslynKit","RoslynKit","--action","eval_result","--payload","{\"id\":\"smoke1\"}"],"retry":{"attempts":30,"delayMs":2000},"expect":{"contains":"Ready"}}
{"command":["command","send","RoslynKit","RoslynKit","--action","eval_prune","--payload","{}"],"expect":{"contains":"removedRecords"}}
'@ | & $yoki exec --project $proj
```

说明：

- 前置条件：Engine 执行开关已打开（§2 第 2 步）。开关没开时 `eval` 会被判 `EngineOperationDisabled` 而失败——这本身也是一次有效的负向验证。
- `eval` 那一步带 `retry`，随后由 `eval_result` 轮询到 `Ready`（Unity 编译是异步的，通常几秒）。
- `scene_query` 只断言 `rootCount` 出现，不断言具体场景名（换工程也不会假失败）。
- exec 限制：单次最多 256 步 / 120 次重试 / 单步等待 60s；**exec 里不能再嵌 exec**。
- 想只做「只读体检」就删掉 `scene_query`、`eval`、`eval_prune` 三行：**`scene_query` 是 UserAction，开关关闭时会被判 `EngineOperationDisabled`**（真机实测），只有 `domain_state` / `engine_capabilities` / `entry_list` / `eval_result` 这类只读或诊断操作在关开关时仍可用。
- 真机实测（2026-10，开关关闭）：`domain_state` 通过、`engine_capabilities` 通过、`scene_query` 返回 `EngineOperationDisabled`，且 exec 按契约在失败步停止、不继续执行后续步骤。
- **本脚本涉及的 payload 形状已在真机逐个验证**（利用「payload 解析先于开关判定」这条 gate 顺序）：`scene_query {path,depth}`、`eval {confirmed,id,code}`（含转义代码）、`eval_result {id}`、`eval_prune {}`、`entry_list` 都越过了 payload 解析（分别得到 `EngineOperationDisabled` 或 `RunNotFound`），没有任何一个被判 `InvalidPayload`。
- 关开关时**真正可用**的只有只读/诊断操作（`domain_state`、`engine_capabilities`、`entry_list`、`eval_result`）；`eval_prune` 属 Maintenance，**同样被开关拦住**。

## 3. Godot 真机验证（可选）

- 开关在 `project.godot`：`yokiframe/engine/operations_enabled = true`（**Godot 不用 Unity 那套 editor-settings.json**）。
- 编辑器：`engine_capabilities` 应列出 scene_query/scene_mutate；`list_commands` 的 `kits` 应含 `System` 与 `Engine` 两组；`snapshot read Engine RoslynKit` 应能读到数据。
- 运行时：`entry_run` 用 `"target":"runtime"`；该宿主**只承载 runtime**，请求 editor 会被判 `EngineOperationUnavailable`；`scene_mutate save` 同样返回不可用（runtime 不能保存场景）。
- eval（编辑器，v4.18 新增）：
  ```powershell
  & $yoki command send Engine RoslynKit --action eval --payload '{"confirmed":true,"language":"gdscript","id":"gd1","code":"return 1"}' --project $proj
  & $yoki command send Engine RoslynKit --action eval_result --payload '{"id":"gd1"}' --project $proj
  & $yoki command send Engine RoslynKit --action eval_prune --payload '{}' --project $proj
  ```
  期望：`eval` 返回时状态**已经是** `Ready` 或 `CompileFailed`（GDScript 同步编译，不需要轮询）；`eval_result` 的 `sourcePath` 是内存标记、`note` 是返回值 JSON（调用失败时是失败原因）；`eval_prune` 回报 `removedScripts`。**Godot 侧不落盘**，没有生成目录。
- eval 的 `language` 缺省 `csharp`：在 Godot 上会得到 `EngineOperationUnavailable`（Godot 不做 C# 动态编译；要跑 C# 用 `entry_*`）。

## 4. 明确未验证 / 未做（审查时请按此判断范围）

| 项 | 状态 |
|---|---|
| Unity 侧全部适配器代码（入口宿主、调度器驱动、场景操作、asset_ops、eval 宿主） | 已在 Unity 内编译通过；**只读路径已在真机验证**：`engine_capabilities` 成功（14 个操作、`hostTargets=editor\|play`、按 kind 计数 ReadOnly 7 / Dangerous 4 / UserAction 2 / Maintenance 1、逐目标矩阵含 `DisabledBySettings`）；`domain_state` 成功（`PlayMode`、`sessionId` 已填充）；`snapshot read Engine RoslynKit --detail full` 成功（新字段 `hostTargets`/`capabilities`/`entryCount`/`entries` 齐全）；`entry_list` 成功（扫描 192 个程序集）；`eval` 不带 `confirmed` 被 `ConfirmationRequired` 拒绝。**执行类操作仍未逐个跑过** |
| 快照摘要预算 | `snapshot read` 默认摘要会报 `SnapshotSummaryLimited`（载荷变大，属预期）；要原始节点加 `--detail full`，Workbench 页面读的是原始载荷、不受影响 |
| Unity 侧正常路径（真进播放、真改场景、真生成+编译 eval） | 未验证（尚未在 Unity 里实际执行这些操作）；实测当前 `settingsState=Disabled`、`executionBlocked=true`，所以执行类操作会先被开关拒绝——跑冒烟前记得先打开开关 |
| Godot 场景操作的正常路径与快照发布 | 未在 Godot 进程里验证（测试进程触碰原生单例会崩，因此只覆盖描述符/校验/矩阵） |
| Godot 侧 eval 的正常路径 | 已实现（GDScript 内存编译：Core 服务 6 例 + 接线 6 例 + 宿主 6 例 + 编辑器锁定 1 例）；**未在 Godot 进程里验证**（原生调用集中在 `GodotGdScriptCompiler` 一个文件） |
| P4（TestRunnerApi 适配、共享内存 telemetry、Workbench 专用页面、Unity `runtime` target） | 未开始 |
| 运行记录跨进程认领 | **已实现**（v4.20）：`<runId>.claim` 独占创建提供跨进程互斥，租约过期可接管；孤儿运行仍由 `Reconcile` 按租约判 `Unknown` 且不自动重放 |
**真机已确认的契约（2026-10 只读/拒绝路径，Unity 2022.3.16f1，开关关闭）**：

| 场景 | 真机结果 |
|---|---|
| 只读 / 诊断操作（`domain_state`、`engine_capabilities`、`entry_list`、`snapshot read`） | 成功 |
| 执行类操作（`scene_query`） | `EngineOperationDisabled`（开关关闭）；错误信息指出 settings 缺 `operations.enabled` |
| 组合 target（`"editor\|play"`） | `EngineOperationInvalidPayload`——**payload 校验先于开关判定**（gate 顺序 ①→② 得到确认） |
| 转义键名（`{"\\u0074arget":"editor"}`） | 成功（转义被正确识别为 `target` 并绑定） |
| Dangerous 不带 `confirmed`（`eval`） | `ConfirmationRequired` |
| 未知 id / runId / requestId | `RunNotFound`（`eval_result` / `entry_result` / `entry_lookup` 各自给出可操作提示） |
| Engine state 快照 | `state=Ready`、`generation` 与 `domain_state` 一致；`--detail full` 可取证原始载荷（含 14 行矩阵、`hostTargets`、入口摘要、会话身份） |
| `yoki exec` 编排 | 逐步执行、`expect` 生效、**失败即停止后续步骤** |


## 5. 安全与副作用须知

- `play_control` / `scene_mutate` / `entry_run` / `eval` 都是执行类操作：**开关关闭时一律拒绝**，且需要 `confirmed:true`；Dangerous 操作只允许 `cli` 与 `workbench` 来源。
- `eval` 在 **Unity 侧会把代码写到项目里**（`Assets/YokiFrame.Eval/Editor/`）：框架不承诺代码原文不落盘；需要脱敏就用 `codeFile` + `hash` 模式并及时 `eval_prune`。
- `eval` 在 **Godot 侧不落盘**（GDScript 在内存编译），没有生成目录；`eval_prune` 只回收内存实例与记录。
- 取消/超时都是"请求"：宽限期内观察到用户代码退出才落 `Cancelled`/`Timeout`，否则 `Detached`（用户代码可能仍在跑）。
- 域重载不等于失败：`HostIdentityChanged`/`EngineReloading` 映射为 `Unknown`，先查证再决定，**不要重放有副作用的操作**。
