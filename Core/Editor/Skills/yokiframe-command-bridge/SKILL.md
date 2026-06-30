---
name: yokiframe-command-bridge
description: 通过 YokiFrame 文件命令桥查询和调试框架 Kit 状态、snapshot、命令响应、文件桥健康、Unity/Godot engine registry、TableKit/Luban 环境、GraphKit 编辑器产物边界，以及 System、Architecture、FsmKit、EventKit、PoolKit、LogKit、ResKit、SingletonKit、ManagedRuntimeKit、ActionKit、AudioKit、SaveKit、LocalizationKit、SceneKit、SpatialKit、InputKit、UIKit 的命令桥入口。
---

# YokiFrame CommandBridge - AI 文件命令桥

CommandBridge 使用 `.yokiframe/` 文件 I/O 协议。AI 或 Tauri 写入命令 JSON，Unity/Godot Adapter 轮询处理，结果写入 JSON 响应文件。不要使用 WebSocket、TCP、Named Pipes 或 Unity MCP 替代框架状态查询。

## 快速流程

1. 从 `.yokiframe/engines/<engineId>/engine.json` 发现在线引擎；Unity Editor 通常是 `unity-editor`。
2. 查询当前状态优先读 snapshot：

```text
.yokiframe/engines/<engineId>/snapshots/<Kit>/state.json
```

3. 只有需要请求响应、详情、历史、显式操作、monitor 控制或 snapshot 缺失/过期时才发送命令。
4. 原子写入命令：

```text
.yokiframe/engines/<engineId>/commands/<requestId>.json
```

5. 读取响应：

```text
.yokiframe/engines/<engineId>/results/<requestId>-response.json
```

6. 超时时先发送 `System/bridge_status`，再看 engine-scoped `commands/processing`、`commands/deadletter` 和 response 的 `error.code`。

当前命令/响应不再走 root `.yokiframe/commands` 或 `.yokiframe/results` fallback；没有可用 engine registry 时应报告连接错误，而不是写 root 命令。

## 命令 Envelope

`requestId`、`engineId`、`source`、`kit`、`action` 必须是 1-128 位安全 ASCII 标识符，只允许字母、数字、`.`、`_`、`-`；禁止 `.`, `..`、空格、路径分隔符、冒号、引号和 Unicode。

```json
{
  "protocolVersion": 2,
  "engineId": "unity-editor",
  "source": "codex",
  "createdAtUtc": "2026-06-24T00:00:00Z",
  "requestId": "codex-system-ping-001",
  "kit": "System",
  "action": "ping",
  "payload": {}
}
```

写入时先写 `<requestId>.json.tmp`，UTF-8 flush/close 后 rename 成 `<requestId>.json`。Windows 上读取响应时，如果文件已出现但仍被 Unity 短暂占用，应延迟几十毫秒重试。

## 当前覆盖

2026-06-24 Play Mode 命令桥验证覆盖：

```text
System, Architecture, FsmKit, EventKit, PoolKit, LogKit, ResKit, SingletonKit,
ManagedRuntimeKit, ActionKit, AudioKit, SaveKit, LocalizationKit, SceneKit, SpatialKit, InputKit, UIKit
```

已验证 snapshot：

```text
FsmKit, EventKit, PoolKit, ResKit, SingletonKit, AudioKit, LogKit,
SaveKit, LocalizationKit, SceneKit, SpatialKit, InputKit, UIKit, ActionKit
```

TableKit 和 GraphKit 是 Tauri 编辑器工具流，不是 Runtime command handler；AI 只读 `engine.json` 的 optional dependency、Tauri 页面状态和项目生成目录，不发送 `TableKit/*`、`GraphKit/*` 命令，不读取 `TableKit/state` 或 `GraphKit/state`。

## 命令速查

| Kit | Actions | Snapshot |
|---|---|---|
| `System` | `ping`, `status`, `bridge_status`, `list_commands`, `open_code_location` | - |
| `Architecture` | `stats`, `get_workbench_snapshot`, `list_architectures`, `get_architecture_detail` | - |
| `FsmKit` | `list_all`, `get_state`, `get_history`, `get_state_events`, `get_workbench_snapshot` | `FsmKit/state` |
| `EventKit` | `list_registrations`, `get_workbench_snapshot`, `get_event`, `get_recent_events`, `fire_event`, `monitor_start` | `EventKit/state` |
| `PoolKit` | `stats`, `get_workbench_snapshot`, `list_pools`, `get_pool_detail`, `get_event_history`, `set_tracking`, `clear_history`, `check_leak` | `PoolKit/state` |
| `LogKit` | `stats`, `get_settings`, `set_settings`, `reset_settings`, `get_history`, `get_workbench_snapshot`, `clear_history`, `open_log_folder`, `decrypt_log_file`, `read_log_file` | `LogKit/state` |
| `ResKit` | `stats`, `get_workbench_snapshot`, `list_resources`, `get_resource_detail`, `diagnose_resource`, `get_unload_history`, `clear_history`, `set_tracking` | `ResKit/state` |
| `SingletonKit` | `stats`, `get_workbench_snapshot`, `list_singletons`, `get_singleton_detail` | `SingletonKit/state` |
| `ManagedRuntimeKit` | `get_workbench_snapshot`, `list_backends`, `validate`, `select_backend`, `run_action`, `get_backend_settings`, `save_backend_settings` | - |
| `ActionKit` | `stats`, `get_workbench_snapshot`, `set_stack_trace`, `clear_stack_trace` | `ActionKit/state` |
| `AudioKit` | `stats`, `list_voices`, `list_buses`, `get_history`, `get_workbench_snapshot`, `clear_history`, `stop_voice`, `stop_all`, `stop_bus`, `set_master_volume`, `set_bus_volume`, `mute_master`, `mute_bus` | `AudioKit/state` |
| `SaveKit` | `stats`, `list_slots`, `get_workbench_snapshot`, `delete_slot`, `disable_auto_save` | `SaveKit/state` |
| `LocalizationKit` | `stats`, `list_languages`, `get_workbench_snapshot`, `set_language` | `LocalizationKit/state` |
| `SceneKit` | `stats`, `list_scenes`, `get_workbench_snapshot`, `unload_scene` | `SceneKit/state` |
| `SpatialKit` | `stats`, `list_indexes`, `get_workbench_snapshot` | `SpatialKit/state` |
| `InputKit` | `stats`, `list_actions`, `list_contexts`, `get_workbench_snapshot` | `InputKit/state` |
| `UIKit` | `stats`, `list_panels`, `list_stacks`, `get_workbench_snapshot`, `get_editor_tool_state`, `create_panel_prefab`, `generate_code_for_selection`, `add_bind_to_selection`, `remove_bind_from_selection` | `UIKit/state` |

## Kit 调试入口

- Architecture：`get_workbench_snapshot` 看架构/模型/系统/服务总览，`get_architecture_detail` 使用 `list_architectures` 返回的 `fullName`。
- FsmKit：优先 `FsmKit/state`，单 FSM payload 使用 `{"fsmName":"PlayerFSM"}`。
- EventKit：优先 `get_workbench_snapshot`；`get_event` 可用 `{"channel":"Type","eventKey":"PlayerSpawnedEvent"}`；`fire_event` 是运行时变更命令。
- PoolKit：优先 `PoolKit/state`；`set_tracking` payload 为 `{"trackingEnabled":true,"eventHistoryEnabled":true,"stackTraceEnabled":false}`；`check_leak` 只是仍借出对象候选。
- LogKit：优先 `LogKit/state`、`stats`、`get_settings`、`get_history`；Unity Editor 额外支持 `open_log_folder`、`decrypt_log_file`、`read_log_file`。
- ResKit：单资源 payload 使用 `{"path":"Configs/GameConfig","typeName":"MyConfig"}`；`set_tracking` 使用 `{"loadLocationTrackingEnabled":true}`。
- SingletonKit：详情 payload 使用 `{"fullName":"..."}` 或 `{"typeName":"..."}`；只显示已登记实例。
- ManagedRuntimeKit：`get_workbench_snapshot` 返回当前后端、后端列表、工作流动作和可选后端设置；`get_backend_settings`/`save_backend_settings` 读写宿主后端暴露的设置 JSON，例如 Unity LeanCLR 的 `ProjectSettings/LeanCLR.asset`；`run_action` 使用 `{"backendId":"LeanCLR","actionId":"enable_backend","payload":{"confirmed":true}}` 这类 payload。Unity LeanCLR 的 IL2CPP 安装、Player Build 等变更型动作必须有明确用户确认；Godot 通过自己的后端暴露同名动作槽位，不套用 Unity IL2CPP 替换流程。
- ActionKit：`set_stack_trace` 使用 `{"enabled":true}`，默认关闭，排查后用 `clear_stack_trace`。
- AudioKit：停止、音量和静音命令会改变运行时状态，只在用户明确要求或测试场景中执行。
- SaveKit：`delete_slot` 和 `disable_auto_save` 是维护命令；存档业务 payload 不通过文件桥暴露。
- LocalizationKit：`set_language` 使用 `{"language":"English"}` 或 `{"languageId":2}`，只在用户要求切换语言时执行。
- SceneKit：`unload_scene` 使用 `{"sceneName":"Menu"}` 或 `{"name":"Menu"}`，只在用户要求维护场景时执行。
- SpatialKit：命令桥只读，不通过 `.yokiframe` 插入、更新、删除或查询实体。
- InputKit：命令桥只读，不通过 `.yokiframe` 注入按键、模拟输入、重绑定或切换上下文。
- UIKit：运行时只读；Unity Editor 工具命令需要明确用户意图和当前 Selection。

## TableKit / GraphKit / Luban

TableKit 是 Luban 配置表 Tauri 编辑器流程，不是 Runtime Kit。排查时读取：

```text
.yokiframe/engines/<engineId>/engine.json
```

关注：

```json
{
  "optionalDependencies": {
    "luban": {
      "available": true,
      "define": "YOKIFRAME_LUBAN_SUPPORT"
    }
  }
}
```

若可用，再检查项目配置代码输出目录，通常是 `Assets/Scripts/TableKit/`，是否存在 Luban 生成代码和 `TableKit.cs`。运行时找不到表时继续检查数据输出目录、`RuntimePathPattern` 和 ResKit Provider。

GraphKit 是 Tauri 图编辑页面，不是 Runtime Kit。它编辑 graph project、node types、ports、fields、blackboard、placemats、notes、edges、subgraph/portal，并导出：

```text
Luban Definition XML
Luban Data XML
GraphRuntime contract JSON
```

排查 GraphKit 时看 Tauri GraphKit 页面、导出的 XML/contract、TableKit 生成产物和项目侧 graph runtime/handler 代码。不要发送 `GraphKit/*` 命令。

## 压力测试与验证

项目验证脚本：

```powershell
powershell -ExecutionPolicy Bypass -File .\.aibridge\scripts\run-command-bridge-validation.ps1 -SkipCompile -WarmupSeconds 8 -StressCommandCount 300
```

报告路径：

```text
.yokiframe/tests/command-bridge-validation-report.json
```

健康压力结果应满足 pending/processing 为 0、deadletter 为 0、没有 `BridgeBusy`、没有 payload/result size failure、没有持续 backpressure。

## 安全规则

- 命令/响应文件是可靠控制面，不是高频运行时数据总线。
- UI 当前状态优先 telemetry/snapshot，不要高频 `send_command`。
- TableKit/GraphKit 问题优先看 Tauri 页面与生成产物，不走 runtime command。
- 不手动删除 `processing`、`archive`、`deadletter` 或 `results`，除非用户明确要求恢复/清理。
- 变更型命令必须有明确用户意图或隔离测试场景。
- 路径、registry 或协议字段异常时，停止并检查 `System/bridge_status` 与 engine registry。
