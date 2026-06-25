# YokiFrame CommandBridge 命令目录

本文件记录当前 FileBridge v2 的命令用法和调试入口。优先使用 `.yokiframe/engines/<engineId>` engine-scoped 路径。

## 协议清单

- 发现引擎：`.yokiframe/engines/<engineId>/engine.json`。
- 命令路径：`.yokiframe/engines/<engineId>/commands/<requestId>.json`。
- 响应路径：`.yokiframe/engines/<engineId>/results/<requestId>-response.json`。
- Snapshot 路径：`.yokiframe/engines/<engineId>/snapshots/<Kit>/state.json`。
- 读写使用 UTF-8，避免破坏中文资源路径。
- 写命令时使用 temp + rename，不提交半写 `.json`。
- 读取响应时遇到 `IOException` 短暂重试；Windows 可能先暴露路径，再释放 Unity 文件句柄。
- `status` 为 `error` 时优先读 `error.code` 和 `error.message`；`errorMessage` 是兼容字段。
- 重复 `requestId` 会复用已有 terminal response，不应再次执行 handler。

## System

| Action | Payload | 用途 |
|---|---|---|
| `ping` | `{}` | 检查 command/result 路径，返回 `data.message = "pong"`。 |
| `status` | `{}` | Base 引擎状态和 uptime。 |
| `bridge_status` | `{}` | 当前 engine-scoped 队列、协议文件、stale/deadletter/result、backpressure 和 lastError 状态。 |
| `list_commands` | `{}` | 当前已注册 handler 的命令目录。 |
| `open_code_location` | `{"filePath":"Assets/...","line":12}` | 通过宿主编辑器打开代码位置；只在用户要求时使用。 |

命令超时、压力测试后或准备触碰协议恢复目录前，先看 `bridge_status`。

## 命令矩阵

| Kit | Actions | Snapshot |
|---|---|---|
| `Architecture` | `stats`, `get_workbench_snapshot`, `list_architectures`, `get_architecture_detail` | - |
| `FsmKit` | `list_all`, `get_state`, `get_history`, `get_state_events`, `get_workbench_snapshot` | `FsmKit/state` |
| `EventKit` | `list_registrations`, `get_workbench_snapshot`, `get_event`, `get_recent_events`, `fire_event`, `monitor_start` | `EventKit/state` |
| `PoolKit` | `stats`, `get_workbench_snapshot`, `list_pools`, `get_pool_detail`, `get_event_history`, `set_tracking`, `clear_history`, `check_leak` | `PoolKit/state` |
| `LogKit` | `stats`, `get_settings`, `set_settings`, `reset_settings`, `get_history`, `get_workbench_snapshot`, `clear_history`, `open_log_folder`, `decrypt_log_file`, `read_log_file` | `LogKit/state` |
| `ResKit` | `stats`, `get_workbench_snapshot`, `list_resources`, `get_resource_detail`, `diagnose_resource`, `get_unload_history`, `clear_history`, `set_tracking` | `ResKit/state` |
| `SingletonKit` | `stats`, `get_workbench_snapshot`, `list_singletons`, `get_singleton_detail` | `SingletonKit/state` |
| `ActionKit` | `stats`, `get_workbench_snapshot`, `set_stack_trace`, `clear_stack_trace` | `ActionKit/state` |
| `AudioKit` | `stats`, `list_voices`, `list_buses`, `get_history`, `get_workbench_snapshot`, `clear_history`, `stop_voice`, `stop_all`, `stop_bus`, `set_master_volume`, `set_bus_volume`, `mute_master`, `mute_bus` | `AudioKit/state` |
| `SaveKit` | `stats`, `list_slots`, `get_workbench_snapshot`, `delete_slot`, `disable_auto_save` | `SaveKit/state` |
| `LocalizationKit` | `stats`, `list_languages`, `get_workbench_snapshot`, `set_language` | `LocalizationKit/state` |
| `SceneKit` | `stats`, `list_scenes`, `get_workbench_snapshot`, `unload_scene` | `SceneKit/state` |
| `SpatialKit` | `stats`, `list_indexes`, `get_workbench_snapshot` | `SpatialKit/state` |
| `InputKit` | `stats`, `list_actions`, `list_contexts`, `get_workbench_snapshot` | `InputKit/state` |
| `UIKit` | `stats`, `list_panels`, `list_stacks`, `get_workbench_snapshot`, `get_editor_tool_state`, `create_panel_prefab`, `generate_code_for_selection`, `add_bind_to_selection`, `remove_bind_from_selection` | `UIKit/state` |

## 各 Kit 调试入口

### Architecture

优先用 `get_workbench_snapshot` 查看 architecture、model、system、utility、service 总览。使用 `list_architectures` 返回的 `fullName` 查询单个架构：

```json
{"fullName":"YokiFrame.Unity.ArchitectureTestRunner+DemoGameArchitecture"}
```

### FsmKit

优先读 `FsmKit/state`。单 FSM payload：

```json
{"fsmName":"PlayerFSM"}
```

可用于 `get_state`、`get_history`、`get_state_events`、`get_workbench_snapshot`。

### EventKit

`get_workbench_snapshot` 返回 registrations、recent events 和 diagnostics。单事件通道查询：

```json
{"channel":"Type","eventKey":"PlayerSpawnedEvent"}
```

`fire_event` 会改变运行时事件流，只在明确测试时使用。

### PoolKit

优先读 `PoolKit/state` 或 `get_workbench_snapshot`。需要额外诊断时开启：

```json
{"trackingEnabled":true,"eventHistoryEnabled":true,"stackTraceEnabled":false}
```

`get_pool_detail` 和 `get_event_history` 使用 `{"poolName":"..."}`。`check_leak` 返回当前仍借出的对象候选，不等于真实内存泄漏证明。

### LogKit

常规诊断使用 `LogKit/state`、`stats`、`get_settings`、`get_history`、`get_workbench_snapshot`。Unity Editor 额外支持 `open_log_folder`、`decrypt_log_file`、`read_log_file`。

常用 payload：

```json
{"enabled":true,"minimumLevel":"Debug","saveLogInEditor":false}
{"kind":"editor"}
{"fileName":"YokiFrame.Editor.log","decrypt":true}
```

`set_settings`、`reset_settings`、`clear_history` 都会改变 LogKit 状态。

### ResKit

优先读 `ResKit/state` 或 `get_workbench_snapshot`。单资源上下文：

```json
{"path":"Configs/GameConfig","typeName":"MyConfig"}
```

可用于 `get_resource_detail` 或 `diagnose_resource`。Load 定位：

```json
{"loadLocationTrackingEnabled":true}
```

Load 定位只影响开启后的新加载。

### SingletonKit

优先读 `SingletonKit/state` 或 `get_workbench_snapshot`。详情 payload：

```json
{"fullName":"Game.ConfigService"}
{"typeName":"ConfigService"}
```

列表为空只代表当前没有实例登记，不代表代码库不存在单例类型。

### ActionKit

使用 `ActionKit/state`、`stats`、`get_workbench_snapshot` 查看 action controller 数量和嵌套 action outline。Stack trace 默认关闭：

```json
{"enabled":true}
```

聚焦排查后使用 `clear_stack_trace` 清理。

### AudioKit

使用 `AudioKit/state`、`stats`、`list_buses`、`list_voices`、`get_history`、`get_workbench_snapshot`。变更型控制：

```json
{"voiceId":1}
{"bus":"Sfx"}
{"volume":0.8}
{"bus":"Sfx","volume":0.7}
{"bus":"Sfx","muted":false}
{"muted":true}
```

停止、音量、静音命令只在用户明确要求或验证场景中使用。

### SaveKit

使用 `SaveKit/state`、`stats`、`list_slots`、`get_workbench_snapshot`。维护命令：

```json
{"slotId":1}
```

`delete_slot` 和 `disable_auto_save` 需要明确用户意图或隔离测试 fixture。存档业务数据不通过此命令桥暴露。

### LocalizationKit

使用 `LocalizationKit/state`、`stats`、`list_languages`、`get_workbench_snapshot`。切换语言只在用户要求时执行：

```json
{"language":"English"}
{"languageId":2}
```

### SceneKit

使用 `SceneKit/state`、`stats`、`list_scenes`、`get_workbench_snapshot`。卸载场景只在用户要求时执行：

```json
{"sceneName":"Menu"}
{"name":"Menu"}
```

加载、切换和预加载场景应留在运行时代码或明确项目工具中。

### SpatialKit

使用 `SpatialKit/state`、`stats`、`list_indexes`、`get_workbench_snapshot`。命令桥只读，不通过 `.yokiframe` 插入、更新、删除或查询实体。

### InputKit

使用 `InputKit/state`、`stats`、`list_actions`、`list_contexts`、`get_workbench_snapshot`。命令桥只读，不通过 `.yokiframe` 注入按键、模拟输入、重绑定或切换上下文。

### UIKit

运行时状态使用 `UIKit/state`、`stats`、`list_panels`、`list_stacks`、`get_workbench_snapshot`。Unity Editor 额外暴露工具命令：

```text
get_editor_tool_state
create_panel_prefab
generate_code_for_selection
add_bind_to_selection
remove_bind_from_selection
```

Runtime 命令桥不打开、关闭、显示、隐藏、压栈或弹栈面板。Editor 工具命令需要用户意图和当前 Unity Selection。

## TableKit

TableKit 是 Luban/Tauri 编辑器流程，不是 runtime command handler。不要发送 `TableKit/stats`、`TableKit/get_workbench_snapshot`，也不要读取 `TableKit/state`。检查：

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

然后检查配置的生成代码目录，通常是 `Assets/Scripts/TableKit/`，是否存在 Luban 生成代码和 `TableKit.cs`。

## 压力与验证

项目验证脚本：

```powershell
powershell -ExecutionPolicy Bypass -File .\.aibridge\scripts\run-command-bridge-validation.ps1 -SkipCompile -WarmupSeconds 8 -StressCommandCount 300
```

脚本会确保 Play Mode 场景 runner，发送所有已注册 Kit 命令组，检查 snapshot，覆盖错误路径、重复 `requestId`，并批量写入命令文件。报告写入：

```text
.yokiframe/tests/command-bridge-validation-report.json
```

压力后用 `System/bridge_status` 判断健康度。健康结果应为 pending/processing 为 0、deadletter 为 0、无 `BridgeBusy`、无 payload/result size failure、无持续 backpressure。

## 常见错误码

| Code | 含义 |
|---|---|
| `UnknownKit` | 没有注册对应 `kit` handler。 |
| `HandlerException` | Handler 拒绝 action/payload 或抛出异常，查看 `error.message`。 |
| `InvalidSource`, `InvalidKit`, `InvalidAction`, `InvalidRequestId` | 协议标识符不安全。 |
| `EngineIdMismatch` | Envelope `engineId` 与 engine-scoped 路径/adapter 不一致。 |
| `IncompleteCommandJson`, `InvalidCommandJson` | Writer 提交了半写或非法 JSON。 |
| `BridgeBusy` | pending 队列压力；退避并检查 `bridge_status`。 |
| `PayloadTooLarge`, `ResultTooLarge` | 命令或响应超过大小预算。 |
