# `yoki` 命令参考

本文件面向用户游戏项目中的 AI 执行。所有命令输出 compact JSON；失败输出 `ok=false`、稳定错误码、建议、evidence paths 和非零退出码。`--project` 指向 Unity 或 Godot 项目根；缺失 `--engine` 时只自动选择唯一 heartbeat 在线的 engine。Host 按规范化 `projectRoot + engineId` 单实例运行，第二个 Host 的启动诊断为 `HostAlreadyOwned`；`godot-editor` 与 `godot-runtime` 属于不同 engineId，可同时存在。`$YOKI` 是当前项目 Runtime 缓存里的 `yoki` 可执行文件（从 `tool-manifest.json` 解析），不是开发机上的固定路径。

CLI 会在分派前执行命令级 schema：未知选项、缺失必填项、非法布尔/整数或数值越界直接返回 JSON 错误，不会静默使用默认值。进程收到 Ctrl+C 时返回 `error.code=Cancelled` 和退出码 `130`；清理等非致命问题进入同一 envelope 的 `warnings` 数组，不会向 stderr 写入普通文本。

## 默认输出与诊断分层

默认只输出 AI 判断状态所需的最小字段，协议原始字段需要显式请求。

| 分层 | 获取方式 | 内容 |
|---|---|---|
| 状态摘要 | 默认 | `state`、关键指标、`issues[]`、`nextActions[]`、`detailAvailable` |
| 完整协议字段 | `--detail full` | segment/resource 路径、CRC、ticks、原始 `payloadJson`、registry 与 FastChannel 全量字段 |

- `state` 固定为 `Ready`、`Degraded`、`Unavailable`、`Stale`、`Failed`、`Unknown`；只有 `Ready` 和 `Degraded` 时 `ok=true`
- `state` 表示**数据通道可用性**（telemetry/snapshot/命令是否读到有效结果），**不是 Kit 业务健康**。Kit 自身状态在 `summary` 里，字段形状由各 Kit 决定，没有跨 Kit 的 `status` 约定
- 因此 `state=Ready` 不等于 Kit 一切正常。例如 ResKit 的 `summary.provider.name` 为 `None` 时通道仍可能 `Ready`；判断 Kit 是否可用要读 `summary` 中该 Kit 自己的字段
- `ok=false` 一律写入 stderr 并返回非零退出码；先读 `state`、`issues` 和 `nextActions`，不要只看退出码
- `Unknown` 表示结果无法确认（例如命令超时），不得据此重放 mutation；`Stale` 表示需要刷新而不是 Kit 故障
- 每个 issue 固定包含 `code`、`severity`、`message`、`retryable`；`retryable=false` 表示重试同一命令不会自行恢复
- 状态类命令（`project status`、`kit status`、`telemetry read`、`engine list`）输出项目相对证据路径；请求类证据（`command send`、`command status`、`doctor`）保留绝对路径

### Kit 状态查询

```powershell
& $YOKI kit status --kit ResKit --engine <engineId> --project <projectRoot>
```

- 单命令完成 telemetry -> snapshot 回落；`source` 说明状态来自 `telemetry`、`snapshot` 还是 `none`
- 回落 snapshot 且 generation 与当前 engine 一致时为 `Ready`；不一致时为 `Stale`
- 两个通道都不可用时为 `Unavailable` 并返回非零退出码
- 普通状态查询优先用本命令，不要自己拼 telemetry 与 snapshot

## 只读命令面

| 目标 | 命令 | 关键选项 |
|---|---|---|
| Project Model | `project status` | `--detail summary|full` |
| 静态 harness | `harness status` | `--project` |
| 聚合 catalog | `harness catalog` | `--engine`、`--refresh-commands`、`--strict`、`--timeout` |
| Kit 状态摘要 | `kit status` | `--engine`、`--kit`、`--name`、`--detail` |
| engine 列表 | `engine list` | `--detail` |
| Shared Memory | `telemetry read` | `--engine`、`--kit`、`--name`、`--generation`、`--maxPayload`、`--detail` |
| 文件 snapshot | `snapshot read` | `--engine`、`--kit`、`--name`、`--detail`（默认展开 payload 为 `summary`） |
| FileBridge 健康 | `bridge status` | `--engine` |
| 诊断报告 | `doctor` | `--engine` |
| FastChannel endpoint | `fastchannel status` | `--engine` |
| Runtime action | `command send` | `--engine`、`--kit`、`--action`、`--payload`、`--source`、`--timeout`、`--detail` |
| 命令结果查询 | `command status` | `--request-id`（必填）、`--engine` |
| SpatialKit | `spatialkit stats`、`spatialkit indexes`、`spatialkit density`、`spatialkit analyze` | `--engine`、`--index`、`--resolution`、`--timeout` |
| AudioKit 索引预览 | `audio index scan` | `--scan`、`--output`、`--manifest`、`--namespace`、`--class`、`--start-id` |
| LocalizationKit 查询 | `localization search`、`localization check` | `--source`、`--keyword`、`--missing-only`、`--limit` |
| Installer 预览 | `installer plan` | 安装模式对应的 source/target 选项 |

`spatialkit indexes` 是 CLI 名称，实际发送的 Runtime action 为 `SpatialKit/list_indexes`。其它 SpatialKit CLI 名称与 action 相同。

`fastchannel status` 只读 registry endpoint，不建立连接，也不把 `enabled` 当成已完成握手；listener 未 ready、权限失败或平台不支持时为 disabled 并回退 FileBridge。CLI 与 Workbench 都不直接持有 pipe/socket。需要 socket 协议细节时读包内开发文档，不要把传输细节写进业务代码。

## 临时生成命令

| 命令 | 临时输出 | 执行前条件 |
|---|---|---|
| `localization preview` | `Temp/LubanPreview/LocalizationKit` JSON | XML 已由 `schemaFiles` 注册；可显式提供 Luban 参数 |

预览不会改动作者 Excel 或 `luban.conf`，但会调用外部 Luban 并清理重建它自己的 Temp 目录。

## 受控写入命令

带 `--dry-run` 的命令只做校验和规划，不写盘；返回同一份失败原因和 `writes[]` 计划。

| 命令 | 写入对象 | 执行前条件 |
|---|---|---|
| `project refresh` | `.yokiframe/project/` 的生成式投影 | 指定 `--package <packageRoot>`，且有明确刷新原因 |
| `audio index generate` | 项目内 C# 与音频 manifest | 先 `scan`，确认扫描目录、输出路径、命名空间、类名和 ID 冲突 |
| `localization add` | 项目本地化源文件 | 明确 `--text-id`、`--language`、`--value`；仅 `--force` 可覆盖 |
| `localization template generate` | `schemaFiles` 下的 XML 与 `dataDir` 下三表 Excel | 明确语言；不自动改 `luban.conf`；仅 `--force` 可覆盖 |
| `installer apply` | 目标 Unity/Godot 项目 | 已审阅同参数 `installer plan`，且用户明确确认 |
| `player build --engine godot` | 项目内 Godot Player 与 `.yokiframe/builds/godot/logs` | 已存在 `project.godot`、`export_presets.cfg`、匹配版本 export templates，并明确 preset/output/configuration |
| `command send` 的非 ReadOnly action | 当前宿主 | catalog 已观察到 action，且用户意图与回退/验证路径明确 |

### dry-run

```powershell
& $YOKI project refresh --dry-run --project <projectRoot>
& $YOKI localization add --text-id 1001 --language English --value "Start" --dry-run --project <projectRoot>
& $YOKI localization template generate --languages ChineseSimplified,English --dry-run --project <projectRoot>
& $YOKI player build --engine godot --godot <godotExe> --preset "<preset>" --output Builds/Game.exe --dry-run --project <godotProject>
```

- 输出 `dryRun=true`、`state`、`writes[]`（`path` 为项目相对路径，`action` 为 `create` 或 `overwrite`）和 `nextActions`（去掉 `--dry-run` 的等价命令）
- 校验与真实执行完全同源：计划失败返回与真实执行一致的错误码和原因，不会给出可通过的错误结论
- `localization add --dry-run` 额外返回 `requiresForce`，用于判断是否需要加 `--force`
- `installer apply` 与 `audio index generate` 已有等效预演（`installer plan`、`audio index scan`），不重复提供 `--dry-run`
- 只读命令（`engine list`、`telemetry read`、`snapshot read`、`localization search` 等）不接受 `--dry-run`，会返回 `UnknownOption`

## Project Model

```powershell
& $YOKI project status --project <projectRoot>
& $YOKI project refresh --package <packageRoot> --project <projectRoot>
```

- `status` 不写入；模型不可用时直接返回 `ok=false`、`state=Unavailable` 和 `nextActions=["project refresh"]`
- Project Model 内部的 Missing、Stale、Partial、Blocked 分别投影为 `Unavailable`、`Stale`、`Degraded`、`Failed`
- `refresh` 通过 Client staging、原子替换和回滚提交确定性投影
- `--detail` 只能为 `summary` 或 `full`；默认 summary 已包含问题和下一步，不重复输出聚合证据路径

## Catalog、engine 与读取顺序

```powershell
& $YOKI harness catalog --strict --project <projectRoot>
& $YOKI harness catalog --engine <engineId> --refresh-commands --strict --project <projectRoot>
& $YOKI engine list --project <projectRoot>
& $YOKI kit status --kit <Kit> --engine <engineId> --project <projectRoot>
& $YOKI telemetry read --engine <engineId> --kit <Kit> --name state --project <projectRoot>
& $YOKI snapshot read --engine <engineId> --kit <Kit> --name state --project <projectRoot>
```

- `harness status` 只读静态 `.yokiframe/harness/capabilities.json`
- `harness catalog` 才聚合 Project Model、静态 capability、registry、heartbeat 和可选实时 command 目录
- 只有 `--refresh-commands` 会请求 `System/list_commands`
- `kit status` 是普通状态查询入口；`telemetry read` 与 `snapshot read` 是需要指定通道或名称时的下级入口
- 两条下级命令默认都返回 `state` + `generation`/`sequence` + 已展开的 `summary`；协议原始节点（含 `payloadJson`、CRC、绝对路径）只在 `--detail full` 出现
- `doctor` 与 `bridge status` 默认只返回队列指标和 heartbeat 新鲜度摘要；`--detail full` 才返回完整 `status`
- telemetry 未接受时回落 snapshot；不要在周期刷新中发送 command
- Godot 编辑器是 `godot-editor`；Godot Tools Play Mode 才可能出现 `godot-runtime`。Godot 导出包不发布 YokiFrame FileBridge、Telemetry 或 FastChannel Host

`command send` 对 registry 声明的 `ReadOnly` action 先尝试一次 FastChannel，失败最多回退一次 FileBridge；response 契约校验失败必须直接报协议错误，不得用 FileBridge 成功掩盖。FastChannel response/evidence 是临时的，需要可审计文件证据时直接走 FileBridge。

- `--timeout` 是 Application 总预算；线上 envelope 的 `timeoutMs` 固定规范化到 Runtime CommandPolicy 的 `1000..30000ms`
- 超时输出 `outcome=Unknown`，不得据此重放 mutation；主动 Ctrl+C 取消不重放 mutation；已开始主线程处理的请求不会被中断

用户项目 AI 不执行 Runtime 缓存发布或清理；遇到 lease 占用、缓存缺失或不一致时向用户报告，并按 `Documentation~/Guides/AI-Install.md` bootstrap。

## 当前 Runtime action

**action 面不在本文件维护**，避免与包内声明重复而漂移。事实来源有两处，按需选择：

- **静态声明**（离线可得）：包内 `Core/Editor/<Kit>/Capabilities/capability.json` 与 `Tools/<Kit>/Editor/Capabilities/capability.json` 的 `kit.commands`
- **在线观测**（需宿主运行）：`harness catalog --refresh-commands` 返回的 `commandCatalog`，以及 `capability.json` 声明与观测不一致时的 `Drifted` 标记

每个 Kit 一条可用入口见 [kit-index.md](kit-index.md) 的「如何自证」列。

`LogKit set_settings`、`PoolKit set_tracking`、`ActionKit set_stack_trace` 与 UIKit Editor action 使用严格 payload。需要 payload 字段时读取对应 Provider/handler 源码，或先由 Workbench 执行同一操作；不要猜测、补齐或复用旧 payload。AudioKit 不发布 Runtime UserAction。

## 专用命令示例

```powershell
& $YOKI spatialkit density --engine <engineId> --index <diagnosticsId> --resolution 32 --project <projectRoot>

& $YOKI audio index scan --scan Assets/Art/Audio --project <projectRoot>
& $YOKI audio index generate --scan Assets/Art/Audio --output Assets/Scripts/Generated/AudioIds.cs --manifest Assets/Settings/YokiFrame/audio-index.json --namespace GameAudio --class AudioIds --start-id 1001 --project <projectRoot>

& $YOKI localization search --keyword "开始" --source Assets/Settings/YokiFrame/localization.json --project <projectRoot>
& $YOKI localization check --source Assets/Settings/YokiFrame/localization.json --project <projectRoot>
& $YOKI localization add --text-id 1001 --language English --value "Start" --project <projectRoot>
& $YOKI localization template generate --languages ChineseSimplified,English --project <projectRoot>
& $YOKI localization preview --project <projectRoot>
& $YOKI localization preview --luban-config Luban/MiniTemplate/luban.conf --luban Luban/Tools/Luban/Luban.dll --luban-workdir Luban/MiniTemplate --target client --project <projectRoot>
```

- Audio 索引保留已分配 ID；路径、常量名、重复 ID 和项目根越界均会失败
- Localization `add` 与模板生成默认拒绝覆盖；`--force` 是显式覆盖开关
- 模板固定生成 `LocalizationKit.xml`、`LocalizationKit.xlsx` 的单一 `Localization` 表：`id`、`key`、`pluralCategory` 和语言列；空分类是普通文本，复数行由 `id + pluralCategory` 唯一约束。发现 `schemaFiles` 未覆盖 XML 时只返回注册提示，不擅自改配置
- `localization preview` 只生成 `Temp/LubanPreview/LocalizationKit` 临时 JSON；传入任一 Luban 覆盖参数时，必须同时提供 `--luban-config` 与 `--luban`，相对路径以 `--project` 为基准
- 自动发现把同目录的 `Luban.dll` 与 `Luban.exe` 视为同一工具并优先 DLL；仅不同目录的多套工具需要显式指定 Luban 参数
- ResKit、ActionKit、AudioKit、UIKit 的周期观察遵循 telemetry -> snapshot；AudioKit 只读观察不发送 command，其它详情或 UserAction 才按目录发送
- SaveKit 周期观察遵循 telemetry -> snapshot；状态只包含已存在后端、自动保存和有界容器头，不读取 payload 或创建默认后端

## Installer

```powershell
& $YOKI installer plan --mode unity-local --source <packageRoot> --target <unityProject>
& $YOKI installer apply --mode unity-local --source <packageRoot> --target <unityProject>
```

- `--mode` 只接受 `unity-local`、`unity-git`、`godot-local`
- Unity Git URL 使用 `--git-url <absoluteGitUri>`；Godot local 使用 `--source`、`--target` 和按需 `--repair-godot true --enable-godot true`
- `--take-over true` 只处理已审阅的 legacy 受管内容，不能绕过用户修改冲突
- Installer 失败后检查 rollback、conflicts、logs 和 evidence，不重复覆盖目标目录

## Godot Player

```powershell
& $YOKI player build --engine godot --project <godotProject> --godot <godotDotnetExecutable> --preset "Windows Desktop" --output Builds/Game.exe --configuration debug
```

- `--configuration` 只接受 `debug` 或 `release`
- `--output` 必须位于项目根内；CLI 不覆盖项目外路径
- 成功输出包含 `outputPath`、`logPath`、`artifactBytes` 与 `durationMs`
- 导出失败检查 `error.evidencePaths` 中的日志；YokiFrame CLI 当前不提供 Unity Player 构建，请使用 Unity Editor 或自行选择外部自动化工具
