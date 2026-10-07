# RoslynKit 设计：Roslyn 内存 C# 自动化与通用引擎桥接

> 版本：**v5.2，用户选定 Roslyn；取代 v5.1 自研 YokiScript 解析器方案**。
> **当前实现**：Unity editor/play 与 Godot 4.7 .NET/Tools editor/runtime 的内存 C#、LiveCode 恢复/批量/调参已落地。2026-10-06 已移除 `entry_*`、属性扫描器、Unity 落盘 eval 与入口页面投影，并增加活动 Architecture 服务目录，验证见 §14 和 LiveCode 契约。Godot Capture/Patch/Export/Bind、正式安装器分发及任意对象根发现仍未完成。
> 用户的首要要求是外部输入指令后执行，不因任务变化触发 Unity 资产编译或域重载。任务会由 Roslyn 在内存中编译，**不是“零编译”**。输入改为 C#，不再承诺执行原 TS 外观。
> 不要求用户安装外部 SDK、Node 或命令行编译器；Roslyn 及其依赖需随宿主工具功能打包，属于明确的新依赖，不再声称“无语言依赖”。
> 核心决策：**不再保留独立的用户入口执行器作为目标架构**。取消 `[YokiFrameEntry]`、入口注册/扫描和 `entry_*` API；保留并改造运行记录、异步调度、取消、结果查询和重载对账机制。
> v4 的设计、P0-P4 进度与测试计数完整保存在 [历史设计](Engine-Operation-Glue-Design-v4-History.md) 和 [历史验收](Engine-Operation-Review-Checklist-v4-History.md)。这些历史成果不等于 v5 已完成。

## 1. 需求与成功标准

### 1.1 用户需要什么

AI 通过 Skill 和 `yoki` 操作 Unity / Godot 编辑器，发现真实运行对象、读取数值、调用用户已写好的 C# 业务方法，完成跨帧测试、断言、截图和引擎控制台输出。

- 游戏的 Architecture、Model、System、View 仍然用 C#；Model 管数据，System 修改数据，View 订阅变化并显示。
- 业务代码首次创建、框架桥接安装或 C# 逻辑修改后，仍需正常编译。**不承诺新增 C# 逻辑免编译**。
- AI 的临时自动化逻辑使用**外部提交的 C# 方法体**。宿主在内存中包装、编译、加载并执行；保留发现对象、业务调用、await、断言、日志和截图的完整工作流，不把 TS 原文交给 Roslyn。
- 不生成任务 `.cs` / DLL / PDB 文件，不调用 `AssetDatabase.Refresh` 或请求引擎脚本编译，不主动重置当前游戏状态。桥接和编译器首次安装仍需正常导入/编译。
- 每个任务都不应要求新增 `demo.read_live`、`demo.change_stats`、`demo.capture` 等 C# 包装方法，也不要求业务方法添加测试专用属性。
- 验证数据来自 CLI 返回的活动对象、操作结果、场景状态与宿主日志，**不能通过读取本地业务源码推断当前值**。正常开发时阅读框架 API 文档与实现不受此限制。

### 1.2 明确不做

- 不把 YokiFrame 或游戏业务整体改写成脚本。
- 不实现 TypeScript/JavaScript 或自研解释器，不引入 Node、npm SDK、esbuild、Jint 或 PuerTS。使用 Roslyn `CSharpCompilation`，不再维护小语言的词法、语法和数值语义。
- 不保留另一套“正式 C# 测试入口”产品线，不把 `entry_run` 换个名字后继续要求用户写入口。
- 不把关闭 Play Mode Domain Reload 当成解决方案；修改 C# 与修改自动化脚本是两件事。
- 不做远程多租户脚本沙箱、CI 服务、编辑器安装、许可证或自动构建流水线。
- 不以 IL2CPP / Native AOT 为动态 C# 执行宿主。CLI 保持 Native AOT，只传输和观察任务，不加载用户程序集。
- 不把任意 C# 当成安全沙箱；授权执行后代码拥有宿主进程权限。桥接不支持时不得回退到旧落盘 eval。

## 2. 架构与执行位置

```text
AI / Skill / 用户
  |
  | C# 方法体（默认 stdin；可选项目根 scripts/engine/*.csx）
  v
yoki script：输入大小校验 -> 提交 -> 查询运行终态       [新构建已实现]
  | 复用 yoki 命令客户端，不直写桥接队列或宿主结果
  v
FileBridge v2 / 已有只读通道
  v
宿主 Policy -> Engine Gate -> 通用运行调度            [复用并扩展]
  | Roslyn：内存包装 -> CSharpCompilation -> Emit(MemoryStream)
  | 会话/目标复核 -> 内存加载 -> 主线程开始 async Task
  | 已有 C# 对象 / 等帧 / 断言 / 截图 / 日志 / 运行证据
  +-- Unity Editor：editor / play
  +-- Godot Editor：editor
  +-- Godot Runtime：runtime
  v
已编译的 C# Model、System、组件和引擎 API
```

### 2.1 选定的执行方案

| 路线 | 决策 |
|---|---|
| 每个任务新增带属性的 C# 方法 | 退出目标架构。增加/修改任务仍需编译，不符合即时操作需求 |
| Roslyn 内存 C# + 稳定宿主桥接 | **本期主线**。编译器随宿主分发，任务不进入引擎资产编译链，不要求外部 SDK |
| 自研 YokiScript 解析器 | 撤销；不再实现、测试或分发另一套语言 |
| 完整 TS/JS 运行时或 JSON 编程语言 | 不进入本期。JSON 保留为桥接传输格式；不要求用户把整段任务改成 JSON 步骤，不新增完整语言依赖 |

### 2.2 CLI 与运行环境（新构建已实现）

```text
yoki script --engine unity-editor --target play --confirm-execution --stdin --project <project>
yoki script --engine unity-editor --target editor --confirm-execution --file scripts/engine/check.csx --project <project>
```

- 第一版是**前台提交与等待**：CLI 不执行脚本；编译、调用、断言和权威结果位于选定宿主。无需用户注册脚本名，不增加脚本目录 API。
- 所有脚本执行均为 Dangerous，包括看似只读的 C#；必须显式 `--confirm-execution`。这不自动开启项目执行开关，不能用只读通道执行。
- Roslyn 与运行上下文由宿主工具包提供。生产编译不启动系统 shell、csc 子进程或 dotnet CLI，不临时下载编译器。
- 整段源码编译成功前不加载用户程序集、不执行用户代码。编译错误可以有已提交的请求/运行记录，但不得有业务副作用。
- 默认 stdin 执行一次性任务；`.csx` 是可选保存方式，内容仍是本契约的方法体，不代表支持 Roslyn Script 的 `#r` / `#load`。不写入 Assets。
- C# 可直接调用可见的公开业务 API。`engine` 的范围校验只约束助手 API，无法限制直接调用、反射、文件或网络访问。只允许受信任本机项目授权使用。
- 成功报告写 stdout，失败/未知报告写 stderr；当前宿主日志使用 `engine.ConsoleLog(string)`，不能拿外部 stdout 冒充 Unity 控制台证据。独立 `console_log` action 尚未实现。
- 现有 `yoki exec` 继续负责简单 NDJSON 顺序编排，不改变已有语义，也不强迫单条命令经过 Roslyn。

源码、引用、加载和运行边界的唯一契约见 [Roslyn 自动化契约](Engine-Roslyn-Automation-Contract.md)。原 [YokiScript 契约](YokiScript-v1-Contract.md) 仅供历史追踪。

## 3. 现状与增量

| 能力 | 修订时的基线 | v5 要做什么 |
|---|---|---|
| Policy、Gate、设置、target、会话身份、FileBridge v2 | 已有实现 | 复用，保持其他 Kit 行为不变 |
| `domain_state`、`engine_capabilities`、`scene_*`、`play_control`、`asset_ops` | 已有实现 | 复用并补齐 v5 契约测试，不承诺所有宿主语义相同 |
| `inspect` 与 root 抽象 | 已有实现；本轮示例中 service 根定位失败 | 修复活动 Architecture 解析、补对象发现与只读边界，不用专用 demo 入口绕过 |
| `entry_*`、属性、入口上下文与扫描器 | 已有实现 | 迁移依赖后退出，不作为永久兼容产品面 |
| C# T2 eval | 已有实现，依赖生成属性入口与入口调度 | 退出落盘生成和 Unity 编译路径，不能作为新任务执行的回退 |
| Godot GDScript eval | 已有独立脚本执行路径 | 可保持既有可选能力；不等于支持 Roslyn 异步 C# |
| Roslyn 编译模块、脚本提交/运行与助手 API | **Unity editor/play 已接线并真机验证** | 完成安装器分发、Godot 接线与剩余通用桥接；实际证据见 §14 |

约束继续成立：Unity 2022.3 为兼容基线；Unity 编译路径复用包内 JSON DOM/写出器，不新增 `System.Text.Json` 依赖；FileBridge `PROTOCOL_VERSION` 保持 2；优先在 Provider/Adapter 接线，不改 pump 或共享 Policy。

## 4. 目标、会话与对象身份

### 4.1 目标绑定

| 请求目标 | Unity | Godot |
|---|---|---|
| `editor` | `unity-editor`，编辑器对象与 API | `godot-editor`，编辑器对象与 API |
| `play` | `unity-editor` 内 Play Mode | 运行器先发现 `godot-runtime`，再依据能力矩阵显式映射到它承载的 `runtime` |
| `runtime` | 暂不支持；没有 Player 命令宿主 | `godot-runtime`，运行中的游戏对象 |

- Roslyn 运行器要求明确目标。请求只含单一 target，组合值只用于能力声明。
- Godot 的 `play` 是调用意图，不假定已有 runtime 宿主接受 `"target":"play"`。记录中同时保存 `requestedTarget`、`effectiveTarget` 与 `engineId`；映射依据必须来自能力矩阵。
- 查询不到目标宿主、宿主不承载目标或尚未进入所需运行态时，明确报错；禁止回退到 editor 中执行游戏代码。
- `play_control` 负责提交切换，随后通过 `domain_state` 观察结果；accepted 不是模式已经切换。

### 4.2 对象与成员句柄

发现结果绑定 `project / engineId / target / sessionId / generation / catalogRevision`。对象句柄另含实例身份与 Architecture 身份；成员句柄含声明类型与完整签名。

- 只返回**该宿主进程已加载并可见**的类型和实例。不得跨宿主、跨 target 复用目录或句柄。
- 多个 Architecture 注册相同 Model 类型时，要求明确 Architecture 身份；不能按扫描顺序选第一个。
- 对象销毁、服务注销/重建、场景替换及会话换代均会令旧句柄失效。即使 generation 不变，也必须校验对象仍存活。
- 失效后返回明确错误，由调用方重新发现；不得对新同名对象自动续写。
- 发现活动 Architecture 必须复用公开的活动实例/服务登记机制。不得读取可能懒初始化的 `Interface` / `Instance` getter 来“顺便找到或创建”实例，不绕过私有字段。

## 5. 通用发现、读取与 C# 调用

### 5.1 发现契约

`object_list` 与 `object_describe` 已实现第一批 `root=service`，分别返回已初始化 Architecture 的存活服务和公开成员元数据。场景对象仍用 `scene_query`，不声称已支持任意静态对象或组件根。下列更广的调用范围/反射绑定规则是目标契约，不等于本批已经实现：

- 对象：root、Architecture 身份、对象句柄、类型/程序集、目标、存活状态。
- 成员：memberId、名称、完整参数类型、默认值、返回类型、实例/静态、是否异步、是否允许读取/调用、拒绝原因。
- 不要求项目新增任何测试属性或逐方法注册。权限来自**项目级允许范围**，可以按程序集、类型及成员收窄；不等于暴露整个 AppDomain。
- 元数据发现不调用 getter、不执行方法、不实例化业务对象。集合数量、遍历深度、文本长度、返回字节数均有硬上限，并返回截断标记/继续查询信息。
- AI 根据真实目录选择方法，不读源码猜方法签名。重载必须按 memberId 精确绑定，不按方法名猜参数。

当前请求与响应：

```json
{"root":"service","target":"editor","architecture":"Game.Architecture","limit":25}
{"target":"editor","objectId":"<object_list返回>","limit":100}
```

- target 必须显式提供、为当前宿主活动目标；发现作为诊断豁免执行开关，但不豁免目标和载荷校验。Godot editor 即使正在播放，活动目标仍是 editor；游戏对象属于独立 runtime 宿主。
- `object_list.objects` 包含 objectId、实现类型、程序集、architecture、contracts、supported/reason。architecture/type 过滤使用完整类型名；不调用用户代码初始化服务。
- `object_describe.members` 包含 memberId、kind、名称、声明类型、signature、valueType/valueAssembly、genericArity、参数类型/程序集/修饰符/defaultKind/defaultValue、async 和 supported/reason；属性只返回访问器元数据，不取值。
- 默认值以 `defaultKind` 和 invariant 文本表示，null 单独标记；无法安全表达的值标 unavailable。公开元数据不授予执行权限；当前按目录提供的签名写强类型 C#，没有按 memberId 执行的 invoke action。
- 每页默认 25、最大 100；目录/成员最多 4096、文本最长 1024 字符、响应最多 64 KiB。超过文本/单行预算的元数据显式标不支持；继续页必须传 `nextOffset` 和 `catalogRevision`，变化报 `ObjectCatalogChanged`。`catalogTruncated=true` 时 total 只是已扫描部分，不冒充完整目录。
- objectId/memberId 绑定宿主实例、session/generation、target 和服务注册身份；替换/注销/重载后失效，Unity 无域重载 Play 切换也显式失效。失效报 `ObjectHandleExpired`，不自动绑定同名新对象。

### 5.2 `inspect` 的只读边界

以下是待收敛的目标约束，**当前旧 inspect 值读取还可能执行 getter/自定义枚举器**，不能用于严格无副作用发现；本批只将 service 根改为公开活动注册表，移除懒初始化与全程序集 Architecture 扫描。严格只读发现使用 `object_*`。

目标：`inspect` 读取已存在对象的允许字段、有界集合数据和框架已发布的值快照，不执行任意用户 getter、索引器、自定义枚举器或 `ToString()`。元数据可显示这些成员，但值标记为需要执行才能获取。

例如 Model 只公开 `Read(id)` 时，调用它仍走 `invoke`，不能把“名称像查询”当作纯读证明。通用根解析也不得触发业务初始化。无法证明无副作用的访问不得伪装为 `ReadOnly` 或通过 FastChannel 执行。

### 5.3 `invoke` 的调用边界

以下限制适用于通用 `invoke` 与上下文助手，不是任意 C# 的隔离保证。脚本可使用已经发现签名的强类型公开 API；授权 Roslyn 意味着信任整段代码，不要求每次先做反射式方法绑定。

- 对已发现的活动实例，调用被项目范围允许的**已编译公开方法**；允许范围内的静态方法也可发现与调用。
- 包括普通 C# Model/System 方法，不限静态方法，不要求返回某个 RoslynKit 专用结果类型，不要求传测试上下文。
- 第一版支持 JSON 标量、数组和可明确绑定的 DTO 参数，以及 `void`、可序列化值、`Task`、`Task<T>` 返回；参数转换失败必须在执行前拒绝。
- 重载不唯一、开放泛型、`ref/out`、指针、委托以及未提供适配的 awaitable 应在目录中标明不支持。后续按项目已有技术栈扩展 UniTask 等适配，不假称已支持任意 C# 签名。
- 所有业务调用默认 `Dangerous`，必须经过权限 Gate、`confirmed` 和宿主主线程调度；**不因方法名是 Get/Read 而自动降为只读**。
- 返回 `Task` 时提交后由调度器异步观察，不 `.Wait()` / `.Result` 阻塞 tick。首段同步代码仍无法强行抢占；方法自身必须遵守引擎线程约束。
- 方法接受 `CancellationToken` 时由宿主绑定当前运行 token，不通过 JSON 构造 token。未接受或忽略取消时，不承诺能强制停止。
- 设置字段、运行属性 getter 等可执行访问均不能混入 `inspect`；必须有明确的执行契约。第一版不增加通用私有字段写入能力，业务数值修改优先调用 System。

## 6. 目标操作面

下表是 **v5 目标契约**。标为新增/改造的行不可当成当前 CLI 已支持；实际调用前必须查询在线 capability。

| action | kind | 职责 | 状态 |
|---|---|---|---|
| `domain_state` / `engine_capabilities` | ReadOnly | 宿主状态、会话与逐 target 能力 | 复用 |
| `scene_query` | ReadOnly | 有界场景/组件读取 | 复用；不因只读自动豁免开关 |
| `scene_mutate` / `play_control` | Dangerous | 场景修改、播放控制 | 复用 |
| `asset_ops` | UserAction | 既有资产操作 | 复用；脚本执行器本身不自动触发 refresh |
| `script_run` | Dangerous | 提交 C# 方法体，内存编译并异步执行 | Unity 已接线；禁止回退旧落盘 eval |
| `script_status` | ReadOnly | 编译器主 DLL、授权、语言与加载预算 | Unity 已接线；目标无关诊断 |
| `object_list` / `object_describe` | ReadOnly | 活动实例和成员元数据发现 | 已实现 service 根；其他根未实现 |
| `inspect` | ReadOnly | §5.2 限定的值读取 | 改造 |
| `invoke` | Dangerous | 调用被允许的已编译 C# 方法，提交即返回 runId | 新增 |
| `wait_frames` | UserAction | 宿主按指定时钟推进后完成，提交即返回 runId | 新增 |
| `capture` | UserAction | 通用截图，提交即返回 runId 与可查询的产物记录 | 新增 |
| `console_log` | UserAction | 将实际数据写入指定引擎控制台，并关联 requestId | 新增 |
| `run_result` / `run_lookup` | ReadOnly | 读取脚本/历史结果，按 requestId 反查 | 通用 Run 类型，三个宿主接线；历史入口记录只读 |
| `run_cancel` | UserAction | 请求取消已有脚本任务 | 仅本宿主实际拥有的活动/待执行脚本；不取消历史入口 |

这些通用操作不是新的入口注册体系：用户不新增带属性的类、不注册入口名。脚本仅由编译器在内存包装为固定签名的方法，由编译产物直接绑定，不经过入口扫描器。

### 6.1 等帧、截图与日志

- `wait_frames` 明确 `clock`。`editorTick` 只表示编辑器调度 tick；`gameFrame` 才表示游戏帧。暂停时不得把编辑器 tick 冒充游戏推进；不支持的时钟返回不可用。
- `capture` 区分 Game/Viewport、SceneView、临时相机等 capture mode，通过能力矩阵声明支持范围，不假定 Unity/Godot 完全一致。临时相机需最终释放，不改现有场景相机，不自动保存场景。
- 截图尺寸、像素数、输出字节数有上限。输出限项目内受控路径，校验规范化路径与链接逃逸；返回产物路径、尺寸、hash、实际模式和宿主身份。
- `console_log` 明确级别、长度上限和截断信息，走宿主日志后端。日志含业务数据时不得声称已脱敏；单有 CLI stdout 不算通过。
- 所有操作都返回自身是否真正完成；“命令已接受”不能等同于图像已写完、日志已到达或游戏已推进。

## 7. 权限与开关

复用 v4 的分层：Policy 做协议级准入和 Dangerous 确认，Engine Gate 做本 Kit 的开关、来源、目标和调用范围检查。不修改其他 Kit 的默认权限。

- 新增通用调用范围默认 fail-closed；启用 `operations.enabled` 不等于允许调用任意已加载类型。项目显式允许的范围必须可查询、可审计。
- Dangerous 仅接受 `cli` / `workbench` 来源且需 `confirmed:true`。脚本提交通过已有客户端，不伪造桥接记录绕过 Gate。
- `script_run` 另外要求项目显式启用 `RoslynKit/scripts.trustedCSharp`；Unity 在同一设置文件的 `settings` 数组配置。`operations.enabled` 单独打开不足以开放任意 C#；缺失、false 或非法配置默认拒绝，Gate 和能力矩阵同步反映。
- 脚本确认覆盖整段受信任 C#；不谎称逐方法 Gate 能拦截直接业务调用。未确认时在派发前拒绝，确认不会修改项目配置。
- `source` 仍是审计字段，不是认证凭据；本设计面向受信任的本机项目操作。
- 开关关闭、缺失或解析失败时，继续允许诊断、元数据发现、按 §5.2 收敛后的只读 inspect、结果查询和取消；拒绝新的执行、日志写入、截图和队列认领。
- 对已运行操作请求协作取消，不声称立刻终止任意 C#。取消不会回滚已发生的业务修改。
- 豁免仅跳过执行开关，不绕过 payload、目标、句柄及范围校验；Dangerous 永不自豁免。
- 只读资格必须由实现保证。需要主线程访问的值读取沿用主线程调度，不在后台线程直接触碰 Unity/Godot 对象。

错误继续区分：载荷非法、未允许调用、目标不支持、宿主不可用、对象已失效、运行不存在、超时和未知结局；不能全部包装成 `RoslynOperationFailed`。

## 8. 运行、结果与重载

### 8.1 谁负责什么

| 层 | 运行标识与权威记录 | 成功语义 |
|---|---|---|
| Roslyn 脚本任务 | 宿主创建 `runId`，保存 requestId、源码 hash、编译诊断、步骤、断言及最终报告；CLI 只观察 | 编译成功且 Task 完成、断言通过才是 `Passed` |
| 宿主异步操作 | 宿主创建 `runId`；记录 `requestId`、操作、句柄、目标、会话与结果 | `Succeeded` 仅证明该操作完成，不证明整个脚本测试通过 |

复用同一运行基础设施；编译成功、命令接受、脚本成功是不同状态。`run_result` 不负责启动、编译或推进脚本，不把同步 GDScript 的 Ready/note 当成异步 C# 的终态模型。

- `invoke` / `wait_frames` / `capture` 先持久化记录与 requestId 索引，再返回 `accepted + runId`；同步方法也不能在提交响应前无界执行。
- `script_run` 提交后先返回记录，再推进编译与执行；用户代码不在提交处理器内同步运行。
- `await` 使用真实 C# Task 语义。宿主在主线程开始调用；框架助手保证自身线程语义，不对用户 `Task.Run` / `ConfigureAwait(false)` 后直接触碰引擎 API 作自动修正。
- 编译可在后台使用已捕获的程序集引用快照；加载和调用前重新核验目标、会话、开关。不得在后台枚举 Unity 对象。
- 相同宿主身份作用域内的 requestId 只能指向一个运行；相同 id 携带不同载荷应报冲突，而不是执行新操作。
- 记录持久化、独占认领、租约、终态 CAS 与迟到结果机制复用已有实现，但类型不再绑定 EntryContext 或入口名称。
- 同步方法异常/异步故障返回异常类型、消息和栈；脚本断言失败与调用异常分开记录。

### 8.2 状态与取消

```text
Queued -> Compiling -> CompileFailed
              |
              +-> Running -> Passed / Failed / Errored / Unknown
            |
            +-> CancelRequested / TimeoutRequested
                    +-> 观察到退出：Cancelled / Timeout
                    +-> 宽限期后仍未退出：Detached
```

- 状态推进由宿主调度器完成；`run_result`、`run_lookup` 和 snapshot 都是纯读，不能调用用户代码或认领任务。
- 取消/超时先是请求。默认宽限期沿用 5 秒，超时仍未退出则 `Detached`，明确用户代码可能继续运行。
- 终态后拒绝该运行后续经桥接发起的操作；迟到结果作为附注，不覆盖终态。无法撤销此前已经交给用户 C# 的对象引用或直接调用。
- CLI 中断或超时时请求宿主取消；Task/助手协作响应，不承诺强制停止同步循环或任意后台任务。CLI 进程直接被杀时依靠记录/租约对账，不承诺清理必然执行。

### 8.3 重载与重试

- 外部 C# 任务修改只触发 Roslyn 内存编译，不请求引擎资产刷新或重载；用户代码自己调用刷新/重载 API 不在此保证内。用户改 Assets 内 C#、切换 Play、换场景仍可能改变会话或对象身份。
- 切换 Play 后先观察 `domain_state`，再重新发现对象。可以继续外部编排，但不能自动恢复旧对象上的未完成调用。
- 已有终态记录在换代后仍可查询；没有完成证据的旧运行标为 `Unknown`。查询保留原宿主身份，不能在另一个 runtime 上找同名 runId。
- 只读观察可以有界重试；写操作响应丢失时，先用 requestId 查命令/运行证据。**禁止自动用新 requestId 重放**。
- `run_lookup` 未命中、记录已过保留期或宿主离线，不足以证明业务代码没有执行；此时仍需明确未知结局。
- “任务脚本无需重载”不等于“引擎永不重载”，也不等于“C# 可以被强制抢占”。

## 9. 数据与证据

| 内容 | 位置/策略 |
|---|---|
| 临时 C# 源码 | 默认 stdin；不强制创建源码文件 |
| 用户维护的 C# 自动化测试 | 可选项目根 `scripts/engine/*.csx`；不进入 Unity 资产编译 |
| 包装源码 / Roslyn 语法树 / DLL / PDB | 仅在宿主内存中；不写中间文件 |
| CLI 报告 | stdout 返回；需要留证时保存报告，不作为宿主运行状态真值 |
| 宿主运行记录 | 沿用 `.yokiframe/engine/runs/` 基础设施，加入 schemaVersion、kind 和宿主身份，避免多宿主 id 混淆 |
| 截图、结构化结果、日志证据 | `.yokiframe/automation/evidence/` 或请求指定的项目内受控目录 |
| FileBridge 命令归档 | 沿用现有 archive/deadletter 保留策略 |

源码不进入引擎资产编译链，不等于零磁盘 IO。FileBridge 的 payload/archive 可能包含源码，记录与截图也会落盘；不得宣称源码字节绝不接触磁盘。禁止把记录/缓存当成可重放的脚本注册表。

Unity 的内存加载程序集不承诺逐个卸载。限制源码/产物大小和每代加载次数，达到预算时明确拒绝，**不得为释放缓存自动重载**。Godot 可回收加载上下文也须单独验证引擎对象引用、后台任务和事件订阅不会阻止回收。

## 10. 旧入口体系的退出

**已移除 `entry_*` 兼容面、属性/扫描器、Unity T2 生成路径**，Roslyn 不依赖旧入口。通用 Run 类型保留结果/调度能力；历史记录只读，响应标 `kind=legacy`、`recordSchema=legacy-entry-v4` 和 legacyEntry，不认领、不重放、不改写。框架不删除用户脚本或历史结果。升级其他项目必须先检查旧属性/上下文引用和生成残留；Installer 自动阻断旧引用的预检尚未实现。

| 旧依赖 | 迁移要求 |
|---|---|
| `YokiFrameEntryAttribute`、入口扫描器/目录 | 删除；Roslyn 编译产物直接绑定固定方法，不经属性或全程序集入口扫描 |
| `YokiFrameEntryContext`、专用结果类型 | 游戏方法不再依赖；脚本获得中立自动化上下文，宿主运行结果改为通用数据结构 |
| `entry_list/run/result/lookup/cancel` | 从目标命令目录、descriptor、capability.json、内建函数和活跃示例中移除；结果/取消机制迁到通用操作 |
| Entry 专用调度、索引、认领、取消 | 抽出已有可复用机制，不重写第二套调度器；保留已有行为测试并改为通用契约 |
| Unity C# eval 生成器与类型探测 | 退出落盘/Refresh/类型轮询路径；新内存包装无属性，不因接口缺失回退旧路径 |
| Godot 可选 GDScript eval | 若继续发布，解耦 Entry 依赖并独立回归；不要求用户写 C# 包装入口 |
| Workbench 入口列表、`entryCount/entries`、最近入口运行 | 改为能力、活动对象和通用运行投影；不能靠更名仍展示废弃目录 |
| Skill、CLI schema、漂移守卫、样例 | 与新命令同步更新；旧代码未退出前如实标注过渡状态，不把拟定命令教成可执行命令 |
| 既有用户 `[YokiFrameEntry]` 脚本与生成残留 | 升级预检列出文件和调用方，提供迁移说明；未处理则阻止退出步骤，不自动改写或删除用户代码 |
| 已有运行/评审证据 | 原样保留，只读按旧 schema 标注；不认领、不重放，不为读历史结果保留旧执行 API |

迁移发布是显式的破坏性工具 API 变更，需要升级说明、能力版本协商及引用扫描。不能先删除属性导致用户项目编译失败，再把修复留给用户猜测。

## 11. 实施顺序与验收门

旧 P0-P4 是历史阶段；以下使用 V5 编号，逐项报告实际证据，不沿用旧测试总数证明完成。

| 阶段 | 交付范围 | 必须提供的证据 |
|---|---|---|
| V5-0 契约与依赖盘点 | 锁定 Roslyn C# 方法体、受信任执行、内存加载预算与打包边界 | 不把 TS 输入、零编译或安全沙箱当作承诺；旧用户代码不被自动改写 |
| V5-0a 内存编译核心 | 独立宿主编译组件，固定依赖版本、完整源码编译、显式引用、内存 PE/PDB 和诊断 | 真实 Roslyn 测试：编译/错误定位/引用/异步/取消/无中间文件；此门不代表宿主接线完成 |
| V5-1 通用发现与读取 | 修复 service 根、多个 Architecture、对象/成员发现、句柄、有界 inspect | 不初始化业务对象；读取来自真实宿主；跨代/已销毁对象拒绝；getter 副作用测试 |
| V5-2 通用调用与运行 | invoke、等帧、运行查询/取消、主线程调度、请求索引、权限 | 普通无属性 Model/System 可调用；异步不阻塞 tick；提交成功不等于完成；取消/重载/重复提交测试 |
| V5-3 Unity Roslyn 纵切 | 依赖分发、CLI 提交、主线程异步上下文、截图/日志、加载预算 | 完整 C# 工作流真机执行；只改任务仍保留 Model 状态，无 Unity 资产编译/域重载 |
| V5-4 Godot 同流程 | Editor/Runtime 路由、普通 C# 调用、时钟与截图适配 | 同一脚本使用 capability 显式处理差异；不落 C#，不跨进程复用对象；不支持项明确失败 |
| V5-5 退出旧入口 | 移除旧公开 API、属性/扫描器、T2 依赖与旧投影；更新 Skill/示例/清单 | 当前调用链无 Entry 依赖，无兼容别名；历史数据保留；用户迁移预检与完整回归通过 |

先完成 V5-0/0a，再推进 V5-1/2 的真实桥接与 V5-3 接线。不要为最早的编译验证强行修改共享 Gate、pump 或当前 Unity 插件引用。新路径不得经过旧入口扫描器或落盘 eval。

**验收主场景**：

1. 在 `Assets/Scripts/` 编写并编译一次 Architecture、Model、System、View；注册 Model/System，创建三个演示物体，数据与显示分离。
2. 仅通过 Skill/CLI 发现活动 Model/System、成员和当前值，不读取业务源码当作数据证据。
3. 通过普通 System 方法修改一个物体数值，按游戏帧等待，重新读取 Model 与 View，断言同步。
4. 同一份真实值写入 Unity/Godot 控制台，并输出可检查的截图。
5. 只修改外部 C# 的参数/断言再次执行；分别记录 Roslyn 内存编译和 Unity 编译事件，证明没有生成任务 `.cs`、刷新资产、域重载或意外重置数据。
6. 加入失败断言、无效参数、拒绝调用、对象失效、取消和重载中断用例；结果必须区分失败、异常、取消和未知。
7. 不修改用户现有相机，不自动保存脏场景；结束时恢复最初播放状态，清理仅限本任务临时资源。

详见 [当前验收清单](Engine-Operation-Review-Checklist.md)。

## 12. 第一版完整验收脚本

以下上下文 API 已接入 Unity。只输入 C# 方法体，不写类、入口属性或 `export default`。`Game.*` 是业务类型示意，先通过 `object_list/object_describe` 发现 service 根中的真实类型和签名；不把示意名称当作存在的服务。

```csharp
var model = engine.RequireService<Game.DemoStatsModel>();
var system = engine.RequireService<Game.DemoStatsSystem>();

// Read 是业务执行；整段脚本已获得受信任 C# 执行授权。
var before = model.Read("Sentinel");
var beforeHealth = before.Health;
system.ApplyChange("Sentinel", 35, 7);
await engine.WaitFrames(3, clock: "gameFrame");
var after = model.Read("Sentinel");
test.Equal(after.Health, beforeHealth - 35, "health changed");
engine.ConsoleLog($"health: {beforeHealth} -> {after.Health}");
await engine.Capture(mode: "game", path: ".yokiframe/automation/evidence/demo.png");
```

前提：目标游戏正在运行；项目已允许受信任 C#；Sentinel 初始血量不小于 35，且没有外部并发修改。`WaitFrames(gameFrame)` 不负责启动游戏。

直接 C# 调用遵守原方法的引用语义：`Read` 若返回可变对象，`before` 可能随业务变动，所以示例先复制标量 `beforeHealth`；不再承诺每次调用自动生成 JSON 快照。`test.Equal` 记录失败并抛断言异常；C# 允许 try/finally/catch，但已记录失败不能因捕获异常变成 Passed。

Skill 的职责是先发现能力/对象/签名，再生成可执行脚本；不负责代替业务方法执行，也不能靠本地源码填充结果。正式 C# 测试框架以后若接入，应使用其原生发现与执行协议，不重新引入 YokiFrame 入口属性。

## 13. 修订记录

- v5.1 -> v5.2：用户选定 Roslyn。撤销自研解析器和 TS 外观承诺，改为宿主内存编译 C#；CLI 保持 AOT 传输客户端，用户无需外部 SDK。新增受信任执行、依赖分发、程序集生命周期与内存预算边界。
- v4 -> v5：按用户实际操作需求，将主线改为外部 TS 编排 + 通用引擎桥接；明确修改任务不应触发 Unity 编译或重载。
- v5 -> v5.1：按用户最终要求，保留上述完整调用写法，改为自研 YokiScript v1 解析与解释；撤销外部 TS SDK/Node/JS 引擎方案。JSON 只作为传输数据，不替代用户脚本。主验收是任务变化不触发编译/域重载。
- 用户确认不需要 `entry_*`：目标架构移除独立入口体系，而非保留“正式 C# 测试入口”分支。
- 保留已验证的门禁、调度、运行证据与重载语义；区分脚本任务结论、宿主操作完成及游戏业务状态。
- 补齐 Entry、C# eval、Workbench、Skill、旧用户脚本之间的退出依赖；代码退出已在 §14.0 完成，跨项目升级预检仍未完成。Roslyn 纵切不生成或调用属性入口。
- 原有阶段记录归档，历史测试计数不作为 v5 完成证明。

## 14. 本轮实现与真机证据

### 14.0 旧入口退出与发现增量（2026-10-06）

用户选定两个增量：退出旧入口与 Unity 落盘 eval；增加只读对象/方法发现。移除属性、扫描器和旧 action，不保留执行别名；调度/结果类型改为通用 Run，旧磁盘记录只读保留，不认领、不重放。Godot GDScript eval 保持独立。

第一批发现范围为 `root=service` 的已初始化 Architecture 服务，不覆盖场景组件/任意静态类型。`object_list` 分页列出活动实例，`object_describe` 按会话/target 绑定的 objectId 返回有界成员元数据，具体 wire 契约见 §5.1。发现不调用 getter、方法、属性构造器、用户枚举器或 ToString；执行仍走明确授权的 Roslyn。

共享 Registry 提供稳定注册 ID 与活动性复核。Workbench 已移除入口目录，展示能力、活动服务和通用运行。生产源码扫描不再包含旧入口类型/注册或 Unity eval 生成路径；旧磁盘 JSON 的 entry 字段仅为历史兼容读取保留。

本增量测试：RoslynKit 157/157；Godot Editor 31/31、Runtime 96/96；Roslyn 7/7；CLI 74/74；Workbench Engine 投影 4/4；Skill 安装 6/6；Unity 编译门 0 错误/0 警告。测试数量因删除旧属性扫描/生成器测试并迁移生命周期覆盖而变化，不能与旧 158 简单相加。

Unity 刷新框架后真实发现 `RoslynKitDemo.DemoStatsModel` / `DemoStatsSystem`，通过 FastChannel 返回 `Read(System.String)`、`ApplyChange(System.String,System.Int32,System.Int32)` 签名。在线能力目录无 entry_* / Unity eval。随后 runId `a26c412e4f2b455386b190fd14e3dc7c` 直接调用服务、等待 3 editorTick 并记录控制台日志，6 条断言通过，Health=120、Attack=18，未修改业务值。会话 `f07cce63bdb64548b737c2e2ed120b8d`、generation `639268677319497854`；脚本体内观察窗口的 compilation/reload 事件均为 0，不把该窗口冒充覆盖第一次 Roslyn 编译的全程。

最终源码刷新后，于 2026-10-06 15:32（Asia/Shanghai）再次执行 runId `c4a68756192b4a42ad9f7483201630b0`，requestId `cli-1791271968047-fea2eee3`：6 条断言通过，控制台日志为 `discovery-final Sentinel health=120, attack=18`。前后 sessionId 均为 `c13e6246279b43d78acd225d4d378a98`、generation 均为 `639268684982929316`，脚本体内编译/重载事件为 0。最终 Validation 为 Ready、编译 Idle、issues 为空、控制台错误数 0；Engine snapshot 已增量反映本次 Passed 运行和两个活动服务，不含旧 entries 目录。

迁移专项真机验证：历史 eval runId `e21e668ef2d34516877bfa019d650155` 可经 `run_result` 和 `run_lookup` 读取，明确标 `kind=legacy` / `recordSchema=legacy-entry-v4`，记录与结果文件查询前后 SHA-256 不变。发现目录逐条分页可取得两个服务；不同作用域的句柄被拒为 `ObjectHandleExpired`，EditMode 查询 play 目标被拒为 `RoslynOperationUnavailable`。项目 `Assets/Scripts` 未检出旧 Entry API 引用；此扫描不代替其他项目的升级预检。

已同步包内 Skill 的五个变更文档到当前项目 `custom/skills/yokiframe/`，SHA-256 一致，两份 Skill 各 16 个本地文档链接校验通过。只更新操作说明，没有因同步 Skill 自动安装编译器或修改授权。

本轮没有开关改动、Play 切换、场景保存或相机操作；开始验收时两个 Engine 授权项已经为 true，保持原值。Godot 本轮是编译守卫下的测试，不是实际 Godot 进程验收。

### 先前 Roslyn 纵切基线

本轮优先完成最小的 Roslyn Unity 纵切，而不是先实现另一套通用方法调用协议。C# 可以直接使用已加载业务类型；`RequireService<T>` 通过公开的弱引用活动服务表解析，拒绝歧义、已释放服务及未初始化架构。

| 范围 | 本轮结果 |
|---|---|
| 编译 | Roslyn 4.8.0 / netstandard2.0 / C# 9；MemoryStream PE/PDB，显式宿主引用，无任务文件和外部编译进程 |
| 接口 | `script_run`、`script_status`、`run_result`、`run_lookup`、`run_cancel`；`yoki script` 前台提交/观察 |
| 生命周期 | 复用瞬态 work 调度；编译在后台，加载/入口和助手在主线程；独立信任开关、请求去重、取消和换代 Unknown |
| 测试 | RoslynKit 158/158；Roslyn 编译组件 7/7；CLI 全量 74/74；Skill 安装 6/6；Unity 编译门 0 错误、0 警告 |
| 真机 | Unity 2022.3.16f1，SampleScene；不读取本地业务源码，通过 CLI 执行并取回真实数值 |
| 未完成（当前） | Godot Capture/Patch/Export/Bind、安装器自动分发/升级预检、Native AOT 新版发布回归、其他对象根、inspect 值读取收敛、独立 invoke/wait/capture/log actions |

### 14.1 连续任务

验收先进入 Play 并初始化已有示例；Play 切换本身允许正常域重载，不计入任务变化的观察窗口。两段任务均走 stdin，没有任务属性或注册名。

- 第一段 runId `35119041ec464015bf8f168ec7549f12`：Sentinel Health `120 -> 85`、Attack `18 -> 25`；实际 Time.frameCount 推进 3，Model/View 5 条断言通过。
- 第二段 runId `6bce7c4ff4dd4693b5eb29cdece57b46`：同一 Model/System 引用，Health `85 -> 80`、Attack `25 -> 27`；前一段数据保留、视图更新，10 条断言通过。
- 两段 sessionId 均为 `e72efd0d62a6446c9141fb5302790bcd`，generation 均为 `639268220601324743`。第一段末安装的编译/重载事件观察器覆盖第二段编译执行：`compilationStarted=0`、`beforeAssemblyReload=0`；观察器已在 finally 中注销。
- 实际 PNG `.yokiframe/automation/evidence/roslyn-live-demo.png` 已打开检查，Sentinel 显示 `HP 80/120、ATK 27`，其余两个物体可见；截图未更改相机或保存场景。
- 控制台由 `Debug.Log` 写入，运行记录同步包含 before/after 数值。`frames` 报告字段是调度 tick 数，不能代替上述 Time.frameCount 断言。

负向结果：错误 C# 为 `CompileFailed` 且有 input.csx 行列；捕获失败断言后仍为 `Failed`；目标 editor 与当前 play 不符时拒绝；主动取消 token 等待产生 `Cancelled`。这些预期失败不计为测试通过的业务任务。

### 14.2 安装与过渡

编译器包已通过开发构建放在项目 `.yokiframe/automation/compiler/roslyn-4.8.0/`。当前正常框架安装事务尚不分发它；更新 Skill 不等于更新 CLI 或安装编译器。安装说明已写入包内 Skill 的 `references/installer.md`。

先前纵切曾保留 Entry 内部类型；§14.0 增量已迁到通用 Run，并退出旧公开入口、Unity 落盘 eval 和 Workbench 入口投影。仍不能描述成 v5 全部完成。

先前纵切结束时，曾将 Skill 的四个变更文档同步到当前项目 `custom/skills/yokiframe/`，SHA-256 一致，Skill 验证器与 14 个本地链接校验通过。当时临时 `scripts.trustedCSharp` 条目已移除，原 Engine 开关保留；Play 已退出且曾复核 EditMode，没有保存场景。这是历史收尾状态，最新 Skill 与设置观察以 §14.0 为准。

先前纵切之后补充的宿主归属隔离、加载锁与 CLI 未知结果处理通过相应单测/编译门；该次收尾时 Unity 进程已离线，未重新启动。之后已完成 §14.0 的真机回归；本节旧 runId 仍只代表当时的纵切证据，不冒充最后源码状态的完整 Play Mode 再验收。

### 14.3 LiveCode 实测问题收敛与恢复方向（2026-10-06）

新增按 ID 的 `CallLive` / `engine.LiveCode.Invoke`、复数组件查询；明确原生 Unity
SendMessage 不转发到普通 facade。Faulted 行为可同 ID 修复重挂，不能手动 enabled
复活。Unity gameFrame 改为实际 Update/LateUpdate 完成屏障，截图支持显式自动编号。
script_status 报预算最大/已用/剩余值，保留用户的 4096 次上限，不把历史 64 当成现值。

RoslynKit 182/182、Roslyn 7/7，Unity 编译门 0 错误/0 警告；两次真机提交分别通过
32 与 7 条断言，覆盖跨原型调用/替换、连续 12 次等帧、截图编号、故障恢复与暂停。
Export 的公共两参签名和完整 MonoBehaviour 包装已核实；asset_ops refresh 的旧
UnknownCommand 在稳定 FileBridge 未复现。详情与 runId 见
[LiveCode 契约 §9](Engine-LiveCode-Contract.md#9-验证证据)。

长期迭代主线调整为可预期预算与现场恢复。Snapshot/Restore 第一版已交付：
`live_snapshot` 为无编译、写盘的 UserAction；恢复由新会话受信任脚本显式提交，
核对源码 hash/项目/目标/预算，全部字段恢复后才激活。临时目标与字段引用显式映射，
不能仅凭非零 GlobalObjectId 判断对象已经保存。接口、限制和真机证据见该契约 §8/§9。
后续已完成 Unity AttachMany、无编译字段命令、预算预警和 TuningBinder，仍不自动重载或重放。
实际签名与验证见 LiveCode 契约 §8.2；总剩余清单见 `Engine-LiveCode-Completion.md`，
Godot Roslyn/LiveCode 恢复接线已完成真实 Editor/Runtime 验证，范围见下一节。

### 14.5 Godot 恢复与长期迭代（2026-10-06）

Godot 4.7 .NET/Tools 两个宿主安装同一套 Script/LiveCode/Tuning 操作，目标分别为
editor/runtime。程序集加载到游戏 AssemblyLoadContext；内存程序集缺 Location 时从
匹配的 Debug 构建产物取得引用。两进程快照/恢复、字段先于 Ready、真实 CLI 等帧和日志
已经验证；暂停帧号跳跃不计，重建宿主不重置预算。
Snapshot 的 Node/Resource 一律显式映射，不猜 NodePath 或旧 InstanceId。
Godot 没有 Capture/Patch/Export/Bind，也不承诺内建 Build 热重载期间自动恢复或 GC 回收；
isCompiling=false 不是可靠构建中守卫。进程重启恢复与程序集单独卸载不是同一能力。
完整证据与限制见 [LiveCode 契约](Engine-LiveCode-Contract.md) 与交付清单；正式分发另行验收。

### 14.6 Unity 版本化导出与多目标绑定（2026-10-06）

ExportMany 暂存和 CommitExport 提交分离，一次写入后统一请求导入；live_export_status
纯读批次/源码/实际编译版本。旧 Export 两参签名保留，批量不循环调用它。
同路径/同类/同源码合并，逐目标保存字段；Bind 支持共享类和额外目标，已有组件不重置。
Reexport 可在 EditMode 无 handle 更新，比较旧源码与 meta 后原位替换，保留 GUID 和历史。
字段结构变更不保证无损，GlobalObjectId 复用不能当成可检测的组件替换。

Core 263/263、Roslyn 7/7、Godot 31/31 + 96/96，Unity 编译门通过；真实 Unity 已验证
6 原型/4 脚本批量、三目标共享、第四目标默认值、代码更新后 GUID/组件/字段/引用保留，
以及旧记录不改写、脏场景拒绝和纯读查询。详细契约、runId、失败测试暴露的边界见
[版本化导出契约](Engine-LiveCode-Export-Contract.md)。Godot Export/Bind 仍不支持。

### 14.7 Unity 结构化字段（2026-10-07）

零编译调参扩展到一维数组、List、Serializable 数据 class/struct；整字段替换，不做
索引路径 patch。重挂、Snapshot/Restore、导出/绑定复用递归字段状态，嵌套引用按路径
关联对象身份。每集合 256 元素、深度 8、状态 1024 节点；原请求/快照字节限制保留。
字典、多维/交错数组、直接嵌套集合、多态/继承和循环/共享托管对象明确拒绝。
数组或 List 的元素若还需集合，用 Serializable 数据类包装。JSON 不写 Unity 对象引用。
Core 282/282、Godot Runtime 96/96、Unity 编译门通过，实机隔离回归 67/67。
精确限制、示例、验证范围见 LiveCode 契约和 `Engine-LiveCode-Completion.md`；
本次不扩展 Godot 字段类型，不宣称已重新完成正式场景导出/编译/绑定/重开验收。

### 14.8 重复脚本复用与空闲调度（2026-10-07）

script_run 增加每 loader 128 项入口缓存，相同源码/引用快照命中后不编译或加载；
上下文/结果仍逐次创建，静态状态不清零。首次编译加载依赖可能让第二次引用快照改变。
Roslyn 引用元数据显式 Dispose；包内编译器已同步。script_status 新增缓存统计，并声明
loadedBytes 仅累计 PE+PDB，不能用于判断进程内存。Attach/Patch/不同源码仍累积，
缓存淘汰不卸载程序集，不自动重载。

调度认领仅访问本域待执行记录；空闲 tick 不扫历史。最近列表缓存最多 64 条，
本地写入失效，外部修改约 5 秒刷新；结果对账仍纯读。RoslynKit 290/290、Roslyn 7/7、
Godot Runtime 96/96、Unity 编译门通过。Unity 连续 8 次相同脚本 Passed，加载数
1/2/2/2/2/2/2/2，后 6 次命中，Mono heap 采样不变。测试证据、内存范围与未完成的
EditorLoop 性能对照见 `Engine-LiveCode-Completion.md`；不宣称所有内存增长均已消除。
