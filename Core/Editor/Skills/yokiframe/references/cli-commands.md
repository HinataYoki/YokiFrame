# 用 yoki 做什么

`yoki` 位于当前项目 `.yokiframe/runtime/`。从 `current.json` 和 `tool-manifest.json` 得到可执行文件，下面记为 `$YOKI`。`--project` 传 Unity 或 Godot 项目根。宿主操作和内存 C# 的步骤在 [roslyn-kit.md](roslyn-kit.md)，不在本页展开。

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
| 提交 C# 方法体并等待结果 | `script --target editor\|play\|runtime --confirm-execution --stdin`。做法、开关和示例见 [roslyn-kit.md](roslyn-kit.md) |
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

## 多步编排（yoki exec）

需要「发命令 → 等域重载 → 重试 → 断言 → 再发命令」这类编排时用它。**不要把 shell 脚本写到磁盘**（`.ps1`/`.sh`/`.bat` 都不行，系统临时目录也不行）：步骤通过 stdin 交给 `yoki exec`，一行一步 NDJSON。

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

`--dry-run` 返回将要写入的 `writes[]`，失败原因与正式执行相同。`installer apply` 的预览命令是 `installer plan`，`audio index generate` 的预览命令是 `audio index scan`。RoslynKit 的开关和 `script` 示例在 [roslyn-kit.md](roslyn-kit.md)。

Runtime 缓存由 Workbench 或包根 `Documentation~/Guides/AI-Install.md` 的 bootstrap 准备。安装参数见 [installer.md](installer.md)。
