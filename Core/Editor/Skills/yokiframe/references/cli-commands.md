# 用 yoki 做什么

> Roslyn 内存 C# 的 Unity editor/play 路径已实现并真机验证：CLI 只传输/观察，宿主编译与执行。新构建支持 `yoki script`，已安装的旧 CLI 不会因 Skill 更新自动升级。`yoki exec` 仍是 NDJSON 编排器。先查在线 capabilities 和 [安装前置](installer.md)，不为临时任务生成属性入口，不回退到落盘 eval。设计与执行契约在包根 `Documentation~/Guides/Engine-Operation-Glue-Design.md`、`Engine-Roslyn-Automation-Contract.md`。

`yoki` 位于当前项目 `.yokiframe/runtime/`。从 `current.json` 和 `tool-manifest.json` 得到可执行文件，下面记为 `$YOKI`。`--project` 传 Unity 或 Godot 项目根。

在线 engine 只有一个时可以省略 `--engine`。有多个时写明 engine。Godot 编辑器是 `godot-editor`，Godot 游戏运行态是 `godot-runtime`，两者可以同时在线。读取和诊断面向编辑器或 Tools 里的宿主。

## 怎么读结果

命令返回 compact JSON。

- 先看 `ok`、`state`、`issues`、`nextActions`。
- `state` 表示这次有没有读到数据：`Ready` 和 `Degraded` 时 `ok=true`。其余为 `Unavailable`、`Stale`、`Failed`、`Unknown`。
- Kit 是否在工作，看 `summary` 里该 Kit 自己的字段。
- `Stale` 时按 `nextActions` 刷新。`Unknown` 表示超时或取消，写操作重新确认后再执行。
- 默认返回摘要。要原始协议字段时加 `--detail full`。
- 选项名、必填项和数值范围以命令 schema 为准，错误会写在 JSON 里。

## 要查什么就用哪条命令

日常查看一个 Kit 用 `kit status`。它先读 telemetry，读不到再读 snapshot。

| 要做的事 | 命令 |
|---|---|
| 看静态能力 | `harness status` |
| 看项目模型、能力、心跳和在线 action | `harness catalog`。在线 action 加 `--refresh-commands` |
| 看项目模型 | `project status` |
| 重建项目模型 | `project refresh`。`--package` 可选，只在需要指定包根时使用。先加 `--dry-run` 查看 `writes[]` |
| 列出 engine | `engine list` |
| 看某个 Kit | `kit status` |
| 直接读共享内存 | `telemetry read` |
| 直接读 snapshot 文件 | `snapshot read` |
| 看命令桥 | `bridge status` |
| 看引擎状态（模式、播放/编译、执行开关） | `command send --kit RoslynKit --action domain_state` |
| 看引擎操作可用性矩阵与开关原因 | `command send --kit RoslynKit --action engine_capabilities` |
| 不读源码发现存活服务与公开签名 | `command send --kit RoslynKit --action object_list --payload '{"target":"editor","root":"service"}'`，再用 `object_describe` 传 objectId；运行态显式改 target |
| 看诊断摘要 | `doctor` |
| 看 FastChannel 端点 | `fastchannel status`。这里给出的是登记的端点 |
| 执行已声明 action | `command send`。action 来自 `harness catalog --refresh-commands` 的 `commandCatalog`，payload 与 Workbench 上同一次操作一致 |
| 提交外部 C# 方法体并等待结果 | `script --target editor\|play\|runtime --confirm-execution --stdin`；Unity 选 editor/play，Godot 选 editor/runtime；可用项目内 `--file` 代替 stdin，默认超时 30000ms |
| 查某次命令结果 | `command status`，带 `--request-id` |
| 看空间索引的数量、列表、密度和分析 | `spatialkit stats`、`spatialkit indexes`、`spatialkit density`、`spatialkit analyze`。`spatialkit indexes` 对应 Runtime action `list_indexes` |
| 预览音频 ID | `audio index scan` |
| 生成音频 ID 代码和 manifest | `audio index generate`。先 scan，确认目录、类名和已有 ID |
| 搜索或检查本地化 | `localization search`、`localization check` |
| 预览 Luban 本地化 JSON | `localization preview`，输出在 `Temp/LubanPreview/LocalizationKit` |
| 增加一条本地化 | `localization add`。先 `--dry-run`。覆盖已有文本时加 `--force` |
| 生成本地化 XML 和 Excel 模板 | `localization template generate`。先 `--dry-run`。覆盖已有文件时加 `--force` |
| 预览安装 | `installer plan` |
| 执行安装 | `installer apply`。参数与已查看的 plan 相同，用户确认后执行 |
| 导出 Godot 玩家包 | `player build`。`--configuration` 用 `debug` 或 `release`，`--output` 放在项目根内。先 `--dry-run`。Unity 玩家包使用 Unity 的构建流程 |

## 内存 C# 自动化（yoki script）

需要普通 C#、调用已注册服务、真实等帧与断言时使用。先运行 `command send --kit RoslynKit --action script_status`，确认宿主已接线、编译器已安装、受信任 C# 已获授权。

```powershell
@'
await engine.WaitFrames(2, clock: "editorTick");
test.Equal(21 * 2, 42, "arithmetic");
engine.ConsoleLog("automation completed");
'@ | yoki script --engine unity-editor --target editor --confirm-execution --stdin --project <projectRoot>
```

- 输入是 C# 方法体，不是 TS 或带属性的入口；128 KiB UTF-8 上限。可选文件必须位于项目根内，推荐 `scripts/engine/*.csx`，不写进 Assets。用 `--file` 时按 UTF-8 读取；管道喂 stdin 时非 ASCII 可能被按本地代码页解码，含中文的脚本优先用 `--file`。
- `--confirm-execution` 不开启项目设置。执行要求 `RoslynKit/operations.enabled` 与 `RoslynKit/scripts.trustedCSharp` 都为 true；Roslyn 不是沙箱。默认等待 30000ms，长任务加 `--timeout`（上限 600000）。
- CLI 仅在终态 `Passed` 时返回成功；`CompileFailed` / `Failed` / `Errored` / `Cancelled` / `Timeout` 分开报告。无结论或 `Detached` 报未知，不能自动重新提交。
- accepted 返回丢失后用 `run_lookup` 加原 requestId 对账；取得 runId 后用 `run_result`。中断等待时 CLI 尝试 `run_cancel`，不声称任意 C# 已被强制停止。
- 已安装 CLI 暂无 `script`、但宿主已有新操作时，可用 `command send --kit RoslynKit --action script_run --payload '{"code":"test.Equal(1,1);","target":"editor","confirmed":true}'` 提交，再用 `run_result` 查终态。这同样是内存路径，不是 eval 回退。
- Roslyn 已接入 Unity editor/play 与 Godot 4.7 .NET/Tools editor/runtime。Godot 须额外设置 `yokiframe/engine/trusted_csharp=true`，不使用 Unity 的 JSON 设置文件；Godot Capture/Patch/Export/Bind 未实现。助手 API、限制和结果查询见 [roslyn-kit.md](roslyn-kit.md)。
- 要在 Play 里热改/新增行为、边跑边调参、最后落盘成 MonoBehaviour，用同一受信任提交里的 `engine.LiveCode`。成员源码怎么写、跨原型怎么调用、Export/Bind 的真实边界见 [livecode.md](livecode.md)。

## 多步编排不要落盘（yoki exec）

需要「发命令 → 等域重载 → 重试 → 断言 → 再发命令」这类编排时用它。**不要把 shell 脚本写到磁盘**（`.ps1`/`.sh`/`.bat` 都不行，系统临时目录也不行）：步骤通过 stdin 交给 `yoki exec`，一行一步 NDJSON。

### 为什么

- 落盘脚本会残留、会被误提交、会污染用户机器；用户明确要求这类编排零落盘。
- 编排逻辑留在 stdin 上，步骤与结果都是 JSON，agent 生成和解析都更可靠。

### 步骤格式（一行一步）

| 形态 | 含义 |
|---|---|
| `{"command":["command","send","--kit","RoslynKit","--action","domain_state"]}` | 执行一条 yoki 子命令；**自动继承外层 `--project`** |
| `{"wait":5000}` | 等待毫秒（上限 60s，超出按 60s 截断） |
| `{"command":[...],"retry":{"attempts":10,"delayMs":3000}}` | 失败重试；attempts ≤120 |
| `{"command":[...],"expect":{"contains":"\"isPlaying\":true"}}` | 断言输出包含指定文本 |

空行与 `#` 开头的行会被忽略。**禁止嵌套 `exec`**（会被判为非法步骤）。

### 输出格式

每一步一行 NDJSON：`{"command":"exec","step":N,"input":"...","exitCode":0,"ok":true,"result":{...}}`，
最后一行汇总：`{"command":"exec","steps":N,"failed":K,"ok":true}`。
`result` 是该步输出的 JSON（无法解析时原样作为字符串，超过 256 KiB 会截断并置 `truncated:true`）。
默认**遇错即停**；加 `--continue-on-error` 继续跑剩余步骤。

### 例子：进播放 → 复核 → 退出（不落盘）

```bash
@'
{"command":["command","send","--kit","RoslynKit","--action","engine_capabilities"]}
{"command":["command","send","--kit","RoslynKit","--action","play_control","--payload","{\"command\":\"enter\",\"confirmed\":true}"]}
{"wait":5000}
{"command":["command","send","--kit","RoslynKit","--action","domain_state"],"retry":{"attempts":10,"delayMs":3000},"expect":{"contains":"\"isPlaying\":true"}}
{"command":["command","send","--kit","RoslynKit","--action","play_control","--payload","{\"command\":\"exit\",\"confirmed\":true}"]}
{"wait":5000}
{"command":["command","send","--kit","RoslynKit","--action","domain_state"],"expect":{"contains":"\"mode\":\"EditMode\""}}
'@ | yoki exec --project <项目根>
```

### 错误码

| 码 | 含义 |
|---|---|
| `ExecInputMissing` | stdin 没有重定向；exec 必须靠管道或重定向喂步骤 |
| `ExecStepInvalid` | 步骤不是 JSON 对象、command 数组为空、或嵌套 exec |
| `ExecStepFailed` | 该步退出码非 0 |
| `ExecStepExpectationFailed` | 该步退出码为 0 但 `expect.contains` 未命中 |

### 什么时候不要用它

- 单条命令：直接 `yoki ...` 就行，别套 exec。
- 需要复杂控制流（分支、变量、文件读写）：exec 刻意只做顺序编排 + 等待 + 重试 + 断言；真需要编程时用 `.yokiframe/` 受控目录或项目自身构建脚本，而不是临时脚本。

### 落盘策略

运行记录和截图写入项目 `.yokiframe/` 受控目录；需要维护的 C# 任务可放项目根 `scripts/engine/*.csx`。不要生成 Assets 下的任务 `.cs` 或将任务编译为磁盘 DLL/PDB，也不要把一次性 shell 编排写到系统临时目录。

## 引擎操作开关（RoslynKit）

RoslynKit 的执行类操作默认关闭，开关写在工程设置文件 `ProjectSettings/Packages/com.hinatayoki.yokiframe/editor-settings.json` 里：

```json
{ "kit": "RoslynKit", "key": "operations.enabled", "value": "true" }
```

- 每次命令执行都会重新读取该开关，改完立即生效，无需重启编辑器。
- 关闭、配置缺失或解析失败时：`engine_capabilities`、`domain_state` 等诊断与结果查询仍可用；执行类操作返回 `RoslynOperationDisabled`。
- `--action engine_capabilities` 可查看每个操作的可用性（`Enabled` / `DisabledBySettings` / `UnsupportedByHostTarget`）。

`--dry-run` 返回将要写入的 `writes[]`，失败原因与正式执行相同。`installer apply` 的预览命令是 `installer plan`，`audio index generate` 的预览命令是 `audio index scan`。

Runtime 缓存由 Workbench 或包根 `Documentation~/Guides/AI-Install.md` 的 bootstrap 准备。安装参数见 [installer.md](installer.md)。
