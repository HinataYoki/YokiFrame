---
name: yokiframe-cli
description: Use when Codex needs YokiFrame Project Model, capability catalog, engine, telemetry, snapshot, FastChannel, FileBridge, supported runtime commands, Godot Player export, AudioKit index generation, LocalizationKit operations, SpatialKit queries, or Installer plan/apply. Route Runtime API design to yokiframe and Avalonia UI navigation to yokiframe-workbench.
---

# YokiFrame CLI

## 职责与非目标

- 负责通过项目 Runtime 缓存中的 `yoki` 查询项目与运行态，并执行当前宿主声明的受控操作
- 不直接创建、编辑、清理 `.yokiframe` 协议文件
- 不把 CLI 用作 Unity 编译、Scene/Prefab/Asset、Play Mode、截图或输入自动化接口
- 不根据静态文档猜测在线 action；当前 `System/list_commands` 和 capability catalog 才是 Runtime command 事实
- Host 按规范化 `projectRoot + engineId` 单实例运行；第二个 Host 的启动诊断为 `HostAlreadyOwned`，不得覆盖现有 registry、heartbeat 或 command evidence

## 前置核实

1. 定位当前项目使用的 YokiFrame 包根、`.yokiframe/runtime/com.hinatayoki.yokiframe/current.json` 和当前指纹目录的 `tool-manifest.json`
2. 选择 manifest 指向的当前平台 `yoki`；Windows 使用 `win-x64-aot`，缓存缺失或源码指纹不匹配时先执行 bootstrap
3. 对 Project Model、harness、engine、snapshot、telemetry 和 command 显式传入 `--project <projectRoot>`
4. 不依赖 `--help`；它不是稳定 CLI 契约。按 [commands.md](references/commands.md) 和当前错误建议核实命令面
5. CLI 在进入任何业务模块前执行命令级 schema：未知选项、缺失必填项、非法布尔/整数和越界值都会返回 JSON `error`，不会静默回落默认值
6. FastChannel 能力由 Application 的 `IFastChannelCommandTransport` 窄端口提供；CLI 不直接持有 Named Pipe 或 Unix Domain Socket。只读 action 必须先确认 registry 的 endpoint、session/generation 和 `readOnlyCommands`，其它请求保持 FileBridge。
7. Workbench 运行期间可能持有当前 fingerprint 的 `.runtime.lease`；CLI/Packaging 清理缓存时必须尊重活动 lease，不把“目录未能删除”误报为发布成功。

## 执行步骤

1. `project status --strict`：确认生成式 Project Model 可用
2. `harness catalog --strict`：聚合安装态与在线证据，默认不发送 command
3. `engine list`：零个或多个在线 engine 时显式指定 `--engine`
4. `telemetry read`：高频状态优先；`accepted=false` 或不可用时回落 `snapshot read`
5. 只有需要详情或显式操作时才以 `harness catalog --refresh-commands` 核实 action，并使用 `command send`；endpoint 声明为 `ReadOnly` 的 action 才允许先尝试 FastChannel，连接/超时/Host 生命周期故障最多回退一次 FileBridge，响应契约错误必须直接报告
6. 失败时检查 terminal response、`doctor`、`bridge status` 和 `evidencePaths`；response `status != Success` 即为失败。FastChannel response/evidence 是 ephemeral，需审计证据时以 FileBridge 的 file-backed evidence 为准；超时投影为 `Unknown`，主动取消不重放 mutation
7. Godot Player 导出使用 `player build --engine godot`；YokiFrame CLI 当前不提供 Unity Player 构建，请使用 Unity Editor 或自行选择外部自动化工具
8. 长任务可用 Ctrl+C 取消；CLI 会取消根级 CTS、终止可取消的子进程/IO，并返回 `error.code=Cancelled` 与退出码 `130`。Host 队列只取消尚未开始主线程处理的 FastChannel 请求，已开始请求不会被中断

## 副作用边界

| 操作 | 规则 |
|---|---|
| `project refresh` | 重建 Project Model；仅在用户明确要求或当前变更流程需要时执行 |
| `command send` | `ReadOnly` 用于查询；`Maintenance`、`UserAction`、`Dangerous` 需要明确意图和验证方式 |
| `audio index generate` | 写入项目内 C# 与 manifest；先 `scan` 并检查冲突 |
| `localization add` / `template generate` | 默认拒绝覆盖；只在用户指定文本或生成目标时执行 |
| `installer apply` | 先完成并审阅 `installer plan`，再获得明确安装或更新意图 |
| `player build` | 只导出 Godot；明确 preset、debug/release、Godot .NET 可执行文件和项目内输出路径 |
| 任何失败 | 不把 command 已接受、response 文件存在或部分输出当成业务成功 |

本机 FastChannel 只使用当前用户范围的 Windows Named Pipe 或 Unix Domain Socket；Godot Unix socket 在 `Bind` 后设置 `0600`，权限设置失败会禁用 endpoint 并回退 FileBridge。CLI 只消费 Application 返回的统一结果，不自行解释 socket、队列或 wire JSON。

CLI 的 `--timeout` 是 Application 总预算；FastChannel 内部可以使用更短的本地操作期限，但线上 command envelope 的 `timeoutMs` 必须保持在 Runtime CommandPolicy 的 `1000..30000ms` 范围内。不要把 FastChannel 本地期限直接写入协议字段。

## 引用路由

| 需要的信息 | 读取位置 |
|---|---|
| 命令、参数与 action | [commands.md](references/commands.md) |
| 源码编译、Runtime bootstrap、Installer 安装 | `Documentation~/Guides/AI-Install.md` |
| Runtime API 或 Kit 状态 | `yokiframe` |
| Workbench 页面与 Installer UI | `yokiframe-workbench` |
| 人类入口总览 | 包根 `README.md`；快速上手之后进入对应 Kit 文档 |

## 维护触发条件

- 增删 CLI 动词、选项、默认值、错误码或输出语义
- 修改命令 schema、Ctrl+C 取消传播、退出码或 warning envelope
- 新增、删除或改分类 Runtime command descriptor
- 改变 Project Model、catalog、engine 选择、transport 或 evidence 语义
- 改变有副作用的写入、Installer 事务或安全边界
