# `yoki` 命令参考

本文件面向 AI 执行。所有命令输出 compact JSON；失败输出 `ok=false`、稳定错误码、建议、evidence paths 和非零退出码。`--project` 指向 Unity 或 Godot 项目根；缺失 `--engine` 时只自动选择唯一 heartbeat 在线的 engine。Host 按规范化 `projectRoot + engineId` 单实例运行，第二个 Host 的启动诊断为 `HostAlreadyOwned`；`godot-editor` 与 `godot-runtime` 属于不同 engineId，可同时存在。

CLI 会在分派前执行命令级 schema：未知选项、缺失必填项、非法布尔/整数或数值越界直接返回 JSON 错误，不会静默使用默认值。进程收到 Ctrl+C 时返回 `error.code=Cancelled` 和退出码 `130`；清理等非致命问题进入同一 envelope 的 `warnings` 数组，不会向 stderr 写入普通文本。

## 只读命令面

| 目标 | 命令 | 关键选项 |
|---|---|---|
| Project Model | `project status` | `--strict`、`--detail summary|full` |
| 静态 harness | `harness status` | `--project` |
| 聚合 catalog | `harness catalog` | `--engine`、`--refresh-commands`、`--strict`、`--timeout` |
| engine 列表 | `engine list` | `--project` |
| Shared Memory | `telemetry read` | `--engine`、`--kit`、`--name`、`--generation`、`--maxPayload` |
| 文件 snapshot | `snapshot read` | `--engine`、`--kit`、`--name` |
| FileBridge 健康 | `bridge status` | `--engine` |
| 诊断报告 | `doctor` | `--engine` |
| FastChannel endpoint | `fastchannel status` | `--engine` |
| Runtime action | `command send` | `--engine`、`--kit`、`--action`、`--payload`、`--source`、`--timeout` |
| SpatialKit | `spatialkit stats|indexes|density|analyze` | `--engine`、`--index`、`--resolution`、`--timeout` |
| AudioKit 索引预览 | `audio index scan` | `--scan`、`--output`、`--manifest`、`--namespace`、`--class`、`--start-id` |
| LocalizationKit 查询 | `localization search`、`localization check` | `--source`、`--keyword`、`--missing-only`、`--limit` |
| Installer 预览 | `installer plan` | 安装模式对应的 source/target 选项 |

`spatialkit indexes` 是 CLI 名称，实际发送的 Runtime action 为 `SpatialKit/list_indexes`。其它 SpatialKit CLI 名称与 action 相同。

`fastchannel status` 只读取当前 engine 的 registry endpoint，不建立 socket 连接，也不把 endpoint `enabled` 当作已完成握手。输出必须同时核对 `protocolVersion`、`engineId`、`sessionId`、`generation`、`transport`、`endpoint`、`fallback` 和 `readOnlyCommands`；listener 未 ready、权限失败或平台不支持时应显示 disabled，并明确回退 FileBridge。

本机 FastChannel 在 Windows 使用当前用户范围 Named Pipe，在 Linux/macOS 使用 Unix Domain Socket。Godot UDS 在 `Bind` 后固定设置 `0600`；权限设置失败会关闭 listener、记录诊断并发布 disabled endpoint。CLI/Avalonia 不直接持有 pipe/socket，均通过 Application 的 `IFastChannelCommandTransport` 窄端口消费能力和结果。

## 临时生成命令

| 命令 | 临时输出 | 执行前条件 |
|---|---|---|
| `localization preview` | `Temp/LubanPreview/LocalizationKit` JSON | XML 已由 `schemaFiles` 注册；可显式提供 Luban 参数 |

预览不会改动作者 Excel 或 `luban.conf`，但会调用外部 Luban 并清理重建它自己的 Temp 目录。

## 受控写入命令

| 命令 | 写入对象 | 执行前条件 |
|---|---|---|
| `project refresh` | `.yokiframe/project/` 的生成式投影 | 指定 `--package <packageRoot>`，且有明确刷新原因 |
| `audio index generate` | 项目内 C# 与音频 manifest | 先 `scan`，确认扫描目录、输出路径、命名空间、类名和 ID 冲突 |
| `localization add` | 项目本地化源文件 | 明确 `--text-id`、`--language`、`--value`；仅 `--force` 可覆盖 |
| `localization template generate` | `schemaFiles` 下的 XML 与 `dataDir` 下三表 Excel | 明确语言；不自动改 `luban.conf`；仅 `--force` 可覆盖 |
| `installer apply` | 目标 Unity/Godot 项目 | 已审阅同参数 `installer plan`，且用户明确确认 |
| `player build --engine godot` | 项目内 Godot Player 与 `.yokiframe/builds/godot/logs` | 已存在 `project.godot`、`export_presets.cfg`、匹配版本 export templates，并明确 preset/output/configuration |
| `command send` 的非 ReadOnly action | 当前宿主 | catalog 已观察到 action，且用户意图与回退/验证路径明确 |

## Project Model

```powershell
& $YOKI project status --strict --detail summary --project <projectRoot>
& $YOKI project refresh --strict --package <packageRoot> --project <projectRoot>
```

- `status` 不写入；状态可能为 Ready、Missing、Stale、Partial 或 Blocked
- `refresh` 通过 Client staging、原子替换和回滚提交确定性投影
- `--detail` 只能为 `summary` 或 `full`

## Catalog、engine 与读取顺序

```powershell
& $YOKI harness catalog --strict --project <projectRoot>
& $YOKI harness catalog --engine <engineId> --refresh-commands --strict --project <projectRoot>
& $YOKI engine list --project <projectRoot>
& $YOKI telemetry read --engine <engineId> --kit <Kit> --name state --project <projectRoot>
& $YOKI snapshot read --engine <engineId> --kit <Kit> --name state --project <projectRoot>
```

- `harness status` 只读静态 `.yokiframe/harness/capabilities.json`
- `harness catalog` 才聚合 Project Model、静态 capability、registry、heartbeat 和可选实时 command 目录
- 只有 `--refresh-commands` 会请求 `System/list_commands`
- telemetry 未接受时回落 snapshot；不要在周期刷新中发送 command
- Godot 编辑器是 `godot-editor`；Godot Tools Play Mode 才可能出现 `godot-runtime`。Godot 导出包不发布 YokiFrame FileBridge、Telemetry 或 FastChannel Host

`command send` 对 registry 明确声明的 `ReadOnly` action 先尝试一次 FastChannel；连接、超时、endpoint/session 淘汰、队列忙碌或 Host 生命周期故障最多回退一次 FileBridge。FastChannel response 必须匹配 `protocolVersion`、`requestId`、`engineId` 且 status 为 `Success` 或 `Error`；损坏、错配、版本/status 校验失败或未知 Error frame 必须直接报告为协议错误，不得用 FileBridge 成功掩盖。FastChannel 的即时 response/evidence 是 ephemeral，要求可审计文件证据的请求直接使用 FileBridge。超时输出 `outcome=Unknown`，主动 Ctrl+C 取消不重放 mutation；Host 队列只取消尚未开始主线程处理的请求，已开始处理的请求继续完成。

`--timeout` 表示 Application 的总命令预算。FastChannel 可以在该预算内使用更短的本地连接/响应期限；写入 FastChannel frame 的 `timeoutMs` 仍会被规范化到 Runtime CommandPolicy 的 `1000..30000ms` 范围，不能把本地快速通道预算误认为协议期限。FastChannel 失败后只有在总预算仍足够时才回退 FileBridge。

Runtime 缓存发布或清理必须先遵守项目 Runtime 根锁和事务恢复；Workbench 运行期间持有当前 fingerprint 的 `.runtime.lease`，清理器遇到活动 lease 会保留目录并返回待后续清理项。lease 不替代 `current.json`、manifest、源码指纹和完整性校验，缓存缺失或不一致仍需先 bootstrap。

## 当前 Runtime action

每次发送前仍以 catalog 为准。下表记录当前源码声明面，不能覆盖 session/generation、heartbeat 或 drift 校验。

| Kit | ReadOnly | 受控 action |
|---|---|---|
| System | `ping`、`bridge_status`、`list_commands`、`get_environment` | `refresh_snapshots`（Maintenance）、`open_project_folder`、`open_log`、`open_code_location`（UserAction） |
| Validation | `inspect_status`、`get_console_errors`（仅 Unity） | 无 |
| Architecture | `list_architectures`、`get_workbench_snapshot` | 无 |
| EventKit | `get_workbench_snapshot` | 无 |
| FsmKit | `list_all`、`get_state`、`get_history`、`get_state_events`、`get_workbench_snapshot` | 无 |
| LogKit | `get_workbench_snapshot`、`read_log_file` | `set_settings`、`reset_settings`、`clear_history` |
| PoolKit | `get_workbench_snapshot`、`check_leak` | `set_tracking`、`clear_history` |
| ResKit | `stats`、`get_workbench_snapshot`、`list_resources`、`get_resource_detail`、`diagnose_resource`、`get_unload_history` | `set_tracking`、`clear_history` |
| ActionKit | `stats`、`get_workbench_snapshot` | `set_stack_trace`、`clear_stack_trace` |
| SaveKit | `stats`、`get_workbench_snapshot` | 无 |
| AudioKit | `stats`、`get_workbench_snapshot` | 无 |
| SpatialKit | `stats`、`list_indexes`、`density`、`analyze`、`get_workbench_snapshot` | 无 |
| UIKit | `stats`、`get_workbench_snapshot`、`get_editor_context`（仅 Unity Editor） | `create_panel_prefab`、`generate_code_for_selection`、`add_bind_to_selection`、`remove_bind_from_selection` |

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
