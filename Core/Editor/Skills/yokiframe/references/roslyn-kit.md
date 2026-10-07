# RoslynKit：驱动宿主与内存编译

> **实现范围**：Roslyn 与 LiveCode 已接入 Unity editor/play 和 Godot 4.7 .NET/Tools editor/runtime。Godot 支持行为、批量、调参、恢复，不支持 Patch/Export/Bind/Capture。旧 `entry_*`、属性扫描器与 Unity 落盘 eval 已移除；对象目录只覆盖活动 Architecture 服务。正式安装器分发和任意对象根发现仍未完成。设计与契约见包根 `Documentation~/Guides/Engine-Operation-Glue-Design.md`、`Engine-Roslyn-Automation-Contract.md`、`Engine-LiveCode-Contract.md`；先按 [安装说明](installer.md) 检查版本，以在线 capabilities 为准。
>
> 新任务不要为了读数、改数、截图或日志再生成任务专用 C# 入口。现有通用操作不能满足时，报告能力缺口；未经明确要求，不回退到生成 C# 或 eval。验收数据取自 CLI 的真实宿主结果，不从业务源码推断当前值。

要让 Unity / Godot 编辑器**真正动起来**（进播放、查场景、看引擎状态）时读这一页。命令走 `yoki`，RoslynKit 没有 Workbench 页面。

Play Mode 里改行为用 LiveCode，不改 `Assets` 下的业务 `.cs`。做不到的能力和必须落盘编译的动作见 [livecode.md](livecode.md) 的「给 Agent 的硬边界」；撞上限制就停止并说明，不要改真实源码再试。

## 前置：执行开关

- Unity：`ProjectSettings/Packages/com.hinatayoki.yokiframe/editor-settings.json`，条目 `{"kit":"RoslynKit","key":"operations.enabled","value":"true"}`
- Godot：`project.godot` 的 `yokiframe/engine/operations_enabled = true`（Godot 不使用 Unity 那套设置文件）
- 语义：**缺失、解析失败或非 true 一律拒绝执行类操作**（fail-closed）；诊断与取消类不受影响。开关每次执行前重新读取，**改完不需要重启编辑器**。
- 先看状态：`yoki command send --kit RoslynKit --action engine_capabilities`（`settingsState` / `executionBlocked` / 逐 target 矩阵）

## Unity 内存 C# 自动化

新任务使用 `yoki script` 的 stdin 方法体，不新增 `[YokiFrameEntry]`，不生成任务 `.cs`，也不使用旧 eval。编译在 Unity 宿主内存中完成，不触发 Unity 资产编译或域重载；业务代码/桥接首次安装的正常编译不在此保证内。

除上述 Engine 开关外，还要在同一 Unity 设置文件的 `settings` 数组中有：

```json
{"kit":"RoslynKit","key":"scripts.trustedCSharp","value":"true"}
```

只在用户授权时修改，不覆盖已有设置。任意 C# 拥有宿主进程权限，不能当作受限沙箱。每次请求仍需 `confirmed:true` 或 CLI 的 `--confirm-execution`。

| action | 语义 |
|---|---|
| `script_status` | 纯读编译器主 DLL 是否存在、授权、语言版本和当前域加载预算 |
| `script_run` | Dangerous；显式 `code`、`target`、`confirmed`；提交即返回 runId，不等于通过 |
| `run_result` | 纯读 `{"runId":"..."}`；编译诊断在 `detailsJson`，含断言、日志和异常；旧记录标 `kind=legacy` / `recordSchema=legacy-entry-v4` |
| `run_lookup` | 纯读 `{"requestId":"..."}`，不修复索引或执行用户代码 |
| `run_cancel` | 协作取消；开关关闭仍可请求，不保证抢占同步死循环 |

上下文实际 API：

- `engine.RequireService<T>(architecture)`：已初始化的活动服务；可省略 architecture，但多架构同类型时必须给完整架构类型名，不触发懒初始化。
- 普通公开 C# 方法直接调用；类型需已编译并在宿主中可见。返回对象按原 C# 引用语义使用，前后比较先保存标量。
- `await engine.WaitFrames(3, clock: "gameFrame")`：要求已经在 Play；当前按真实 PlayerLoop 的 Update/LateUpdate 完成计数，1 表示至少一个后续帧完成，暂停不推进。不是渲染/物理精确步进，也不保证恰好只过一帧。旧宿主从 editor tick 采样 Time.frameCount 没有此屏障；editorTick 始终只代表调度 tick。
- `test.Equal(actual, expected, message)`：失败抛异常并记录；被用户 catch 后仍为 Failed。
- `engine.ConsoleLog(string)`：写 Unity 控制台和运行记录，最多 256 条，每条最多 16384 字符；不自动序列化对象。
- `await engine.Capture("game", ".yokiframe/automation/evidence/preview.png", autoNumber:true)`：重复名称自动编号，返回实际 PNG 相对路径；默认仍不覆盖。要求运行且未暂停的 Game view，尺寸每边不超过 4096，文件不超过 32 MiB。不改相机；尚无 SceneView/自定义相机模式。
- `cancellationToken`：可传业务 async 方法；助手只允许宿主主线程调用。

预算从 script_status 的 maxAssemblies/maxLoadedBytes、remainingAssemblies/remainingBytes 读取，不猜固定 64。当前开发源码为 4096 次加载/64 MiB，仍是宿主常量；脚本缓存未命中、Attach/同 ID 重挂、Patch 共用预算。SetField 本身不加载。删除不退还，超限明确失败；关闭 Domain Reload 时仅退出重进 Play 不重置预算，不自动重载。查不到结果先对账，不重放写操作。完整 stdin 示例见 [CLI 命令](cli-commands.md)。

重复提交相同源码且引用快照不变时，复用同一 compiler loader 的入口缓存（最多 128 项 FIFO），
不重新编译/加载；每次仍使用新的上下文、断言和日志，并校验授权。缓存不是运行结果，
复用程序集的静态状态不会清零。script_status 的 cachedScripts/maxCachedScripts、
scriptCacheHits/scriptCacheMisses 可核对是否命中。引用路径/顺序/长度/修改时间变化会失效；
首次编译加载依赖后，第二次可能再加载一次。修改源码里的数值也会失效，调参仍优先 live_set_fields。
loadedBytes 只计累计 PE+PDB，不是进程/托管堆内存；淘汰缓存不卸载程序集，不同源码持续提交仍会累积。
空闲调度不扫描历史，最近运行列表最多缓存 64 条，外部记录变更可能延迟约 5 秒；具体结果用 run_result 对账。

## Unity LiveCode（Play 里热改行为）

`engine.LiveCode` 在受信任的 `yoki script` 提交里提供 Patch、Attach、ReadField、SetField、Invoke、Export、Bind、List 和 Remove；live_status 诊断可选 HarmonyX bundle 与活动 handle，live_remove 接收 `{"id":"..."}` 且执行开关关闭时仍可用。

要在运行态新增/覆盖行为、边跑边调参、最后落盘成真 MonoBehaviour 时读 **[livecode.md](livecode.md)**，那里有成员源码写法、跨原型调用、字段可读性、Export/Bind 边界和整套调参循环的注意事项。这里只列最容易踩的几条：

- `Attach(id, target, className, members)` 收的是**类成员**，不是完整类；成员源码里**不能写 `using`**（全限定名）、**没有 `engine`**、**同名局部函数不能重载**，且每个 id 编译成独立程序集，**原型之间不能互引类型**。
- facade 是普通类，原生 SendMessage 系列不转发；跨原型用 `CallLive(id, method, args...)`，外部脚本用 `engine.LiveCode.Invoke(...)`，不按 host 顺序猜对象或缓存旧实例。
- `ReadField` / `SetField` 只认**已声明的 public 或 `[SerializeField]`** 字段；私有调参状态要镜像到 public 字段再读。
- Export 的签名是 **Export(id, outputPath)**（两个参数），写出完整 MonoBehaviour 包装，不是成员片段。原型专用 CallLive 要先迁移成正式接口/事件；Bind 会保存场景，须显式授权。
- Unity 批量落盘用 `ExportMany` 暂存 → `live_export_commit` 提交 →
  `live_export_status` 核验实际 compiled → EditMode `Bind(exportId,target)`。
  同类多实例共享一份脚本，已有组件保留当前字段；后续用 `Reexport`，不需要 Play handle，
  不删组件或 .meta。不要循环旧 Export。API、旧记录迁移和 ID 复用边界见 livecode.md。
- 每个 id 编译成一个程序集并**不卸载**：迭代多轮会耗尽域的脚本程序集预算。详见 [livecode.md](livecode.md) 的「调参循环的工程注意」。
- Play 切换、域重载、授权撤销会清掉临时 handle，导出文件和记录保留。
- Unity 已接 `AttachMany`、Dangerous `live_set_fields`、显式 `live_tuning_bind/refresh`、
  ReadOnly `live_tuning_status` 和取消 `live_tuning_unbind`。调参绑定 session/generation/revision，
  不编译；预算预警不自动重载。载荷和受控文件路径见 livecode.md。
- behaviour Snapshot/Restore 第一版已实现：直接 `live_snapshot`（UserAction，payload target=play）
  不加载新程序集，返回受控文件与 complete/errors；新会话里显式 Restore，源码 hash 必须一致，
  临时目标/引用提供映射，不自动重载。详见 livecode.md；稳定代码仍优先正常 C# 编译。

宿主侧完整边界与验证记录：包根 `Documentation~/Guides/Engine-LiveCode-Contract.md`。

## 只读对象和方法发现

先查 `domain_state`，使用当前 target：

```bash
yoki command send --kit RoslynKit --action object_list --payload '{"root":"service","target":"editor","limit":25}'
yoki command send --kit RoslynKit --action object_describe --payload '{"target":"editor","objectId":"<object_list 返回的 ID>","limit":100}'
```

- 当前只列出**已初始化 Architecture 的存活服务**，不会创建架构。`architecture` 按完整架构类型名过滤，`type` 按实现/注册类型全名过滤；场景对象继续用 `scene_query`。
- 目录返回 objectId、类型/程序集、架构、注册类型；describe 的 `members` 返回公开方法/属性/字段的元数据、精确签名、参数默认值、返回类型、async、支持状态及原因。默认值是 `defaultKind` 加 invariant 文本，不是任意对象序列化。
- 不调用 getter、业务方法、属性构造器、枚举器或 `ToString`，不返回业务值。`supported` 仅指当前 C# 调用示例是否适用，不授予执行权限；通过明确授权的 `yoki script` 调用发现的公开签名。
- 每页 1..100 条，默认 25；目录/成员最多 4096，文本最长 1024 字符，响应最多 64 KiB。`truncated` / `catalogTruncated` 表示不完整；继续页传 `nextOffset` 和原 `catalogRevision`，目录变化报 `ObjectCatalogChanged`，重新从 0 查询。
- ID 绑定宿主实例、会话、generation、target 和服务注册；服务替换/注销、目标切换或重载后报 `ObjectHandleExpired`，重新发现，不按同名自动续接。
- 诊断类发现不依赖执行开关，也不依赖 Roslyn 包。Godot editor/runtime 分别查询自己进程的服务；执行需各自的 trusted C# 授权。

## 现有基础操作

| action | 风险 | 目标 | 作用 |
|---|---|---|---|
| `domain_state` | ReadOnly | 目标无关 | 引擎版本、模式、是否播放/编译/忙、开关状态 |
| `engine_capabilities` | ReadOnly | 目标无关 | 宿主承载目标 + 每个操作的逐 target 可用性矩阵 |
| `play_control` | Dangerous | editor | 播放控制：`{"command":"enter"\|"exit"\|"pause"\|"resume"\|"step","confirmed":true}` |
| `scene_query` | ReadOnly | editor \| play | 当前场景层级：`{"path":"Root/Child","depth":2,"includeInactive":true}` |
| `scene_mutate` | Dangerous | editor \| play | 单对象改场景：`{"op":"create\|delete\|setActive\|setTransform\|save","path":"Root/Child",...}`，登记 Undo 并标脏 |
| `asset_ops` | UserAction | editor | 资产：`{"op":"refresh\|import\|find\|inspect","path":"Assets/...","filter":"t:Prefab","limit":50}` |
| `inspect` | ReadOnly（现有描述符） | 按在线能力判断 | service 根已改为活动注册表、不懒初始化；旧值读取仍可能执行 getter，不用于严格只读发现 |

- 非诊断类只读操作也受开关约束（如 `scene_query`）；`play_control` 需要 `"confirmed":true`，且只允许 cli / workbench 来源。
- `scene_query` 有硬上限：深度 ≤8（默认 2）、节点 ≤2000、每节点组件 ≤24，超限返回 `truncated:true`。
- `inspect` 当前标为诊断豁免，但不能据此保证任意属性访问无副作用；不要用它触发懒初始化。v5 要求发现不执行 getter，无法证明纯读的业务访问必须走受控调用。

## 目标模型（容易踩的地方）

- 请求里的 `target` **只接受单一值**：`editor`、`play`、`runtime`。传 `editor|play` 这类组合会得到 `RoslynOperationInvalidPayload`；组合值只出现在能力声明里。
- 判定顺序固定：载荷与目标 → 执行开关 → Dangerous 来源 → 操作声明目标 → 宿主承载目标。
- `domain_state` / `engine_capabilities` 是**目标无关**操作：不受宿主承载限制（Unity 宿主上也能用 target=runtime 查），但载荷仍然校验。
- payload 的键按 JSON 语义解析：`{"\u0074arget":"play"}` 与 `{"target":"play"}` 等价。

## 提交即返回

`play_control` 只回答「这次请求是否被接受」和「之前是什么状态」；真正切换发生在后续 tick，进播放还会触发脚本域重载。**必须**再用 `domain_state` 复核，别把响应里的 `isPlaying` 当成结果。

## 多步编排不落盘

等重载、重试、断言都用 `yoki exec` 从 **stdin** 喂步骤（NDJSON），不要写 `.ps1`/`.sh` 临时脚本：

```bash
@'
{"command":["command","send","--kit","RoslynKit","--action","engine_capabilities"]}
{"command":["command","send","--kit","RoslynKit","--action","play_control","--payload","{\"command\":\"enter\",\"confirmed\":true}"]}
{"wait":5000}
{"command":["command","send","--kit","RoslynKit","--action","domain_state"],"retry":{"attempts":10,"delayMs":3000},"expect":{"contains":"\"isPlaying\":true"}}
{"command":["command","send","--kit","RoslynKit","--action","play_control","--payload","{\"command\":\"exit\",\"confirmed\":true}"]}
'@ | yoki exec --project <项目根>
```

运行证据写入项目 `.yokiframe/`；可复用的 C# 方法体可放项目根 `scripts/engine/*.csx`。不要向 Assets 生成临时任务脚本。

## Godot 上的差异（同一 payload，不同引擎语义）

Roslyn 额外要求 `project.godot` 布尔项 `yokiframe/engine/trusted_csharp=true`。
CLI 分别用 `--engine godot-editor --target editor` 或 `--engine godot-runtime --target runtime`。
script_status 会返回实际 trustedSetting；ConsoleLog 写 GD.Print 与运行记录。
gameFrame 只供 runtime，editor 用 editorTick。Godot LiveCode 的生命周期、显式引用映射、
暂停/恢复及未实现能力见 [livecode.md](livecode.md) 末节。不要并行构建游戏 DLL 与执行脚本；
Godot 的 isCompiling=false 尚不是可靠的构建中守卫。

`scene_query` / `scene_mutate` 在 Godot 与 Unity 用**同一份 payload**，但引擎语义不同，响应里都会如实标注：

| 差异点 | Unity | Godot |
|---|---|---|
| 创建时的类型来源 | `components`：组件类型名数组 | `nodeType`：Godot 类名（如 `Node3D`），缺省 `Node` |
| 删除 | 立即（Undo 可回滚） | `QueueFree` 延迟释放，响应 `deferred=true` |
| Undo | 编辑器 Undo | 编辑器走 EditorUndoRedoManager；**Runtime 无 Undo** → `undo=unavailable-in-runtime` |
| 保存场景 | `scene_mutate save` | 仅 godot-editor 可保存；godot-runtime 返回 `RoslynOperationUnavailable` |
| 节点字段 | 组件名数组 | `class` / `groups` / `scenePath`，2D/3D 节点带 `transform` |

Godot 宿主的正常路径目前**只在真实 Godot 编辑器/运行时里验证过编译与契约**，本机自动化测试覆盖描述符、载荷校验与能力矩阵。

## 旧 API 迁移

`entry_*`、`YokiFrameEntryAttribute`、EntryContext、注册/扫描器已退出，无兼容别名。新请求使用 Roslyn；旧 `.yokiframe/engine/runs` 记录用 `run_result/run_lookup` 只读访问，不认领、不取消、不改写、不重放。旧非终态记录仍保留原状态，不表示当前还在执行。

升级其他项目之前检查业务脚本中这些旧类型引用及 `Assets/YokiFrame.Eval/Editor/` 生成残留，先迁移业务调用方。框架不会删除用户脚本、历史结果或自动清理旧生成文件；当前 Installer 没有自动阻止旧引用的升级预检。

## 宿主覆盖情况

| 宿主 | RoslynKit | 说明 |
|---|---|---|
| `unity-editor` | ✅ | editor/play 目标；`runtime` 无宿主 |
| `godot-editor` | ✅ | 只承载 editor；命令面按 catalog 聚合（System 之外的 Kit 由 Provider 声明） |
| `godot-runtime` | ✅ | 只承载 runtime；播放态在 Godot 由它承担 |

三个宿主都会写 `snapshots/<kit>/state.json`，因此 `snapshot read Engine RoslynKit` 在任一在线宿主上都能取到数据。

## Godot：独立 GDScript eval

Unity 不再发布 `eval/eval_result/eval_prune`。Godot 保留独立 GDScript eval，不依赖旧入口；要求显式 `language=gdscript` 或 `script`，无 C# 回退。

```bash
yoki command send --kit RoslynKit --action eval --payload '{"confirmed":true,"language":"gdscript","code":"return 1"}'
```

- **同步**：GDScript 在内存里编译（`SourceCode` + `Reload()` + `Call()`），所以 `eval` 返回时状态已经是 `Ready` 或 `CompileFailed`，不需要轮询等待编译。
- **结论在 note 上**：调用返回值（JSON）或调用失败原因写在 `eval_result` 的 `note` 字段里。
- **不落盘**：没有生成目录、没有源码需要清理；`eval_prune` 只回收内存实例与记录（回报 `removedScripts`）。
- **只支持内联 code**：脚本 eval 不接受 `codeFile`（内存编译不读项目文件）。
- 传参用 `--payload`（CLI 只认这个；`-p` / `--source` 会返回 `UnknownCommand`）。
- Dangerous 操作不带 `confirmed:true` 会被拒（`ConfirmationRequired`，既有验证记录）；执行开关关闭时执行类操作被拒（`RoslynOperationDisabled`），只有显式诊断/取消豁免照常可用，不能仅凭 ReadOnly 推断豁免。

## 域重载（进播放/编译）时的正确姿势

- 收到 `HostIdentityChanged` / `RoslynReloading` 表示**会话换代，不是失败**：结论是 `Unknown`，先查证再决定。
- 命令响应若已落盘，框架会直接读回并附 `ReloadedSession` 警告。
- 反查手段：`command status --request-id <id>`，或 `run_lookup` 拿 `runId` 再 `run_result`。
- 不要因为一次 `Unknown` 就重放有副作用的操作（尤其 `script_run`、`play_control`）。

## 错误码

| 码 | 含义 | 怎么办 |
|---|---|---|
| `RoslynOperationDisabled` | 开关关闭、缺失或解析失败 | 确认 `operations.enabled=true`；诊断类不受影响 |
| `RoslynOperationInvalidPayload` | 载荷非法：不是 JSON 对象、target 未知/组合、命令不支持 | 按上表修正 payload |
| `RoslynOperationUnsupported` | 操作未声明该目标 | 换目标或换操作 |
| `RoslynOperationUnavailable` | 宿主不承载该目标 | 用 `engine_capabilities` 看 `hostTargets` |
| `RoslynOperationSourceNotPermitted` | Dangerous 操作来源不受信 | 用 cli / workbench 发起 |

## 什么时候不要用它

- 只想读运行态和诊断 → 用 `snapshot read` / `telemetry read`（不受开关限制，也更便宜）。
- 想改游戏逻辑 → 写代码。引擎操作是"让编辑器动起来"，不是替代业务实现。

## 已知边界

- `domain_state` 已接入已发布的会话身份；以实际 `sessionIdentityAvailable`、`sessionId` / `generation` 判断，不能假定始终为 0。
- `scene_mutate`、`asset_ops` 的宿主支持以在线目录和能力矩阵为准，不沿用固定动作数量。
- 对象目录当前仅 service 根；任意场景组件/静态对象目录及独立 invoke/wait/capture/log actions 尚未实现。旧 inspect 值读取的 getter 副作用仍需单独收敛。
- Roslyn 已接入 Unity editor/play 和 Godot .NET/Tools editor/runtime；安装器分发与 Native AOT 新版发布回归未完成。Unity 独立 Player 仍无命令宿主；导出 Player / IL2CPP / Native AOT 不支持宿主动态执行，CLI 的 Native AOT 不受此限制。
