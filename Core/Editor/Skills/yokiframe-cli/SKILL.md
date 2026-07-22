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

## 前置核实

1. 定位当前项目使用的 YokiFrame 包根、`.yokiframe/runtime/com.hinatayoki.yokiframe/current.json` 和当前指纹目录的 `tool-manifest.json`
2. 选择 manifest 指向的当前平台 `yoki`；Windows 使用 `win-x64-aot`，缓存缺失或源码指纹不匹配时先执行 bootstrap
3. 对 Project Model、harness、engine、snapshot、telemetry 和 command 显式传入 `--project <projectRoot>`
4. 不依赖 `--help`；它不是稳定 CLI 契约。按 [commands.md](references/commands.md) 和当前错误建议核实命令面

## 执行步骤

1. `project status --strict`：确认生成式 Project Model 可用
2. `harness catalog --strict`：聚合安装态与在线证据，默认不发送 command
3. `engine list`：零个或多个在线 engine 时显式指定 `--engine`
4. `telemetry read`：高频状态优先；`accepted=false` 或不可用时回落 `snapshot read`
5. 只有需要详情或显式操作时才以 `harness catalog --refresh-commands` 核实 action，并使用 `command send`
6. 失败时检查 terminal response、`doctor`、`bridge status` 和 `evidencePaths`；response `status != Success` 即为失败
7. Godot Player 导出使用 `player build --engine godot`；YokiFrame CLI 当前不提供 Unity Player 构建，请使用 Unity Editor 或自行选择外部自动化工具

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

## 引用路由

| 需要的信息 | 读取位置 |
|---|---|
| 命令、参数与 action | [commands.md](references/commands.md) |
| Runtime API 或 Kit 状态 | `yokiframe` |
| Workbench 页面与 Installer UI | `yokiframe-workbench` |
| 人类入口总览 | `Documentation~/Api/00-GettingStarted/Entrypoints.md` |

## 维护触发条件

- 增删 CLI 动词、选项、默认值、错误码或输出语义
- 新增、删除或改分类 Runtime command descriptor
- 改变 Project Model、catalog、engine 选择、transport 或 evidence 语义
- 改变有副作用的写入、Installer 事务或安全边界
