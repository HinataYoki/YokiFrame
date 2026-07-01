# YokiFrame 命令桥使用参考

## 发现引擎

优先读取：

```text
.yokiframe/engines/<engineId>/engine.json
.yokiframe/engines/<engineId>/status/heartbeat.json
```

Unity 当前常用 engineId 为 `unity-editor`。命令 JSON 内的 `engineId` 必须和路径中的 `<engineId>` 一致。

## 命令文件

写入路径：

```text
.yokiframe/engines/<engineId>/commands/<requestId>.json
```

响应路径：

```text
.yokiframe/engines/<engineId>/results/<requestId>-response.json
```

写入时使用临时文件加 rename；不要直接半写 `<requestId>.json`。

```json
{
  "protocolVersion": 2,
  "engineId": "unity-editor",
  "source": "codex",
  "createdAtUtc": "2026-06-21T12:00:00Z",
  "requestId": "codex-fsm-001",
  "kit": "FsmKit",
  "action": "list_all",
  "payload": {}
}
```

安全标识符只允许 ASCII 字母、数字、`.`、`_`、`-`，长度 1-128；禁止路径分隔符、空格、冒号、引号、Unicode、`.`、`..`。

## Snapshot

当前状态优先读 snapshot：

```text
.yokiframe/engines/<engineId>/snapshots/FsmKit/state.json
.yokiframe/engines/<engineId>/snapshots/EventKit/state.json
.yokiframe/engines/<engineId>/snapshots/PoolKit/state.json
.yokiframe/engines/<engineId>/snapshots/ResKit/state.json
.yokiframe/engines/<engineId>/snapshots/SingletonKit/state.json
.yokiframe/engines/<engineId>/snapshots/AudioKit/state.json
.yokiframe/engines/<engineId>/snapshots/SaveKit/state.json
.yokiframe/engines/<engineId>/snapshots/LocalizationKit/state.json
.yokiframe/engines/<engineId>/snapshots/SceneKit/state.json
.yokiframe/engines/<engineId>/snapshots/SpatialKit/state.json
.yokiframe/engines/<engineId>/snapshots/UIKit/state.json
```

snapshot 是覆盖式最新状态，不保存历史。需要显式操作、详情、历史、定位开关、诊断或 snapshot 缺失时再发送命令。

TableKit 和 GraphKit 是 Tauri 编辑器工具流，不是 Runtime command handler。不要读取 `TableKit/state`、`GraphKit/state`，也不要发送 `TableKit/*` 或 `GraphKit/*` 命令。

## 常用命令

| Kit | Action | 用途 |
|---|---|---|
| `System` | `ping` | 连通性检查 |
| `System` | `status` | Base 引擎状态 |
| `System` | `bridge_status` | 文件桥健康、队列、deadletter、lastError |
| `System` | `open_code_location` | 用宿主默认代码编辑器打开文件 |
| `FsmKit` | `list_all` | 当前注册 FSM 列表 |
| `FsmKit` | `get_state` | 指定 FSM 当前状态和状态集合 |
| `FsmKit` | `get_history` | 指定 FSM 转换历史 |
| `FsmKit` | `get_state_events` | FSM 生命周期事件 |
| `FsmKit` | `get_workbench_snapshot` | 工作台一次性快照 |
| `EventKit` | `list_registrations` | 运行时注册表 |
| `EventKit` | `get_workbench_snapshot` | 注册表、最近事件和诊断 |
| `EventKit` | `get_event` | 查询单个事件通道 |
| `EventKit` | `get_recent_events` | 最近 Send/Register/UnRegister |
| `PoolKit` | `stats` | 对象池统计和监控开关 |
| `PoolKit` | `get_workbench_snapshot` | 统计、池列表、对象详情、历史和泄漏候选 |
| `PoolKit` | `list_pools` | 对象池列表 |
| `PoolKit` | `get_pool_detail` | 单池 active/inactive 对象详情 |
| `PoolKit` | `get_event_history` | 对象池申请/回收历史 |
| `PoolKit` | `set_tracking` | 开关活动对象、事件历史和堆栈追踪 |
| `PoolKit` | `clear_history` | 清空对象池事件历史 |
| `PoolKit` | `check_leak` | 当前仍借出的疑似泄漏候选 |
| `ResKit` | `stats` | 资源后端和缓存统计 |
| `ResKit` | `get_workbench_snapshot` | 统计、已加载资源和卸载历史 |
| `ResKit` | `list_resources` | 当前缓存资源列表 |
| `ResKit` | `get_resource_detail` | 按路径和可选类型查询已加载资源 |
| `ResKit` | `diagnose_resource` | 查询资源是否加载、相关卸载记录和定位信息 |
| `ResKit` | `get_unload_history` | 最近卸载历史 |
| `ResKit` | `set_tracking` | 开关 Load 调用位置采集 |
| `ResKit` | `clear_history` | 清空资源卸载历史 |
| `SingletonKit` | `stats` | 单例数量、存活数和已释放数 |
| `SingletonKit` | `get_workbench_snapshot` | 统计和单例注册表 |
| `SingletonKit` | `list_singletons` | 已登记单例列表 |
| `SingletonKit` | `get_singleton_detail` | 按 `fullName` 或 `typeName` 查询单例详情 |
| `AudioKit` | `stats` | 音频后端、总线和播放统计 |
| `AudioKit` | `get_workbench_snapshot` | 统计、声音列表和播放历史 |
| `AudioKit` | `list_voices` | 当前活跃声音列表 |
| `AudioKit` | `get_history` | 最近播放历史 |
| `SaveKit` | `stats` | 存档后端、槽位和自动保存统计 |
| `SaveKit` | `get_workbench_snapshot` | 统计、槽位列表和自动保存状态 |
| `SaveKit` | `list_slots` | 只读槽位元数据列表 |
| `SaveKit` | `delete_slot` | 删除指定槽位 |
| `SaveKit` | `disable_auto_save` | 关闭自动保存 |
| `LocalizationKit` | `stats` | 当前语言、默认语言、provider/formatter 和缓存统计 |
| `LocalizationKit` | `get_workbench_snapshot` | 统计和语言列表 |
| `LocalizationKit` | `list_languages` | 只读语言列表 |
| `LocalizationKit` | `set_language` | 显式切换当前语言 |
| `SceneKit` | `stats` | 场景后端、激活场景、加载数量和切换状态 |
| `SceneKit` | `get_workbench_snapshot` | 统计和场景列表 |
| `SceneKit` | `list_scenes` | 只读场景列表 |
| `SceneKit` | `unload_scene` | 显式卸载指定场景 |
| `SpatialKit` | `stats` | 空间索引总数、存活数和已释放数 |
| `SpatialKit` | `get_workbench_snapshot` | 统计和空间索引诊断列表 |
| `SpatialKit` | `list_indexes` | 只读索引列表 |
| `UIKit` | `stats` | UI 后端、面板缓存、打开/隐藏/关闭统计和栈概览 |
| `UIKit` | `get_workbench_snapshot` | 统计、面板列表和栈列表 |
| `UIKit` | `list_panels` | 只读面板缓存和栈内归属 |
| `UIKit` | `list_stacks` | 只读面板栈、深度和顶部面板 |

TableKit 和 GraphKit 不在上表中：它们没有 runtime command handler。TableKit 通过 Tauri 后端执行 Luban 验证/生成，GraphKit 通过 Tauri 页面编辑图数据并导出 Luban XML / GraphRuntime contract。

## Kit 示例

### FsmKit

```json
{
  "protocolVersion": 2,
  "engineId": "unity-editor",
  "source": "codex",
  "createdAtUtc": "2026-06-21T12:00:00Z",
  "requestId": "codex-fsm-workbench-001",
  "kit": "FsmKit",
  "action": "get_workbench_snapshot",
  "payload": { "fsmName": "PlayerFSM" }
}
```

返回 `data.fsms`、`data.detail`、`data.history`、`data.events`。

### EventKit

```json
{
  "protocolVersion": 2,
  "engineId": "unity-editor",
  "source": "codex",
  "createdAtUtc": "2026-06-21T12:00:00Z",
  "requestId": "codex-eventkit-001",
  "kit": "EventKit",
  "action": "get_workbench_snapshot",
  "payload": {}
}
```

返回 `data.counts`、`data.registrations`、`data.recentEvents`、`data.diagnostics`。`diagnostics.runtimeListenerCount` 只用于 AI 验证运行时注册状态；UI 的发送方、接收方和注销方关系仍以代码扫描结果为准。

### PoolKit

```json
{
  "protocolVersion": 2,
  "engineId": "unity-editor",
  "source": "codex",
  "createdAtUtc": "2026-06-21T12:00:00Z",
  "requestId": "codex-poolkit-001",
  "kit": "PoolKit",
  "action": "get_workbench_snapshot",
  "payload": {}
}
```

返回 `data.stats`、`data.list.pools`、`data.details.pools`、`data.events.events`、`data.leaks.suspectedLeaks`。`check_leak` 只是“当前仍借出对象”的候选列表，不等同于真实内存泄漏。

### ResKit

```json
{
  "protocolVersion": 2,
  "engineId": "unity-editor",
  "source": "codex",
  "createdAtUtc": "2026-06-21T12:00:00Z",
  "requestId": "codex-reskit-001",
  "kit": "ResKit",
  "action": "diagnose_resource",
  "payload": {
    "path": "Configs/GameConfig",
    "typeName": "MyConfig"
  }
}
```

返回 `isLoaded`、`resource`、`latestUnload`、`relatedUnloadCount`、`loadedCount` 和 `totalRefCount`。Unity 默认 provider 为 `Unity.Resources`，Godot 默认 provider 为 `Godot.ResourceLoader`；项目可通过 `ResKit.SetProvider()` 替换为 YooAsset、Addressables 或其它后端。

### SingletonKit

```json
{
  "protocolVersion": 2,
  "engineId": "unity-editor",
  "source": "codex",
  "createdAtUtc": "2026-06-21T12:00:00Z",
  "requestId": "codex-singleton-001",
  "kit": "SingletonKit",
  "action": "get_singleton_detail",
  "payload": {
    "fullName": "Game.ConfigService"
  }
}
```

返回 `typeName`、`fullName`、`backend`、`source`、`createdAtUtc`、`instanceHash`、`isAlive`。列表为空只表示当前没有实例登记，不表示项目中没有单例类型。

### LocalizationKit

```json
{
  "protocolVersion": 2,
  "engineId": "unity-editor",
  "source": "codex",
  "createdAtUtc": "2026-06-21T12:00:00Z",
  "requestId": "codex-localization-001",
  "kit": "LocalizationKit",
  "action": "get_workbench_snapshot",
  "payload": {}
}
```

返回 `data.stats` 和 `data.languages`。AI 默认优先读 `LocalizationKit/state` snapshot；只有用户明确要求切换语言时才发送 `set_language`，payload 可使用 `{"language":"English"}` 或 `{"languageId":2}`。

### SceneKit

```json
{
  "protocolVersion": 2,
  "engineId": "unity-editor",
  "source": "codex",
  "createdAtUtc": "2026-06-21T12:00:00Z",
  "requestId": "codex-scene-001",
  "kit": "SceneKit",
  "action": "get_workbench_snapshot",
  "payload": {}
}
```

返回 `data.stats` 和 `data.scenes`。AI 默认优先读 `SceneKit/state` snapshot；只有用户明确要求维护场景时才发送 `unload_scene`，payload 可使用 `{"sceneName":"Menu"}` 或 `{"name":"Menu"}`。Unity/Godot 的加载差异应留在 `ISceneBackend` 后端实现中，业务和工具侧都继续使用统一 `SceneKit` 静态入口。

### SpatialKit

```json
{
  "protocolVersion": 2,
  "engineId": "unity-editor",
  "source": "codex",
  "createdAtUtc": "2026-06-21T12:00:00Z",
  "requestId": "codex-spatial-001",
  "kit": "SpatialKit",
  "action": "get_workbench_snapshot",
  "payload": {}
}
```

返回 `data.stats` 和 `data.indexes`。AI 默认优先读 `SpatialKit/state` snapshot；命令桥只做只读诊断，展示 `SpatialKit.CreateHashGrid()`、`CreateQuadtree()`、`CreateOctree()` 创建出的索引类型、实体类型、实体数量、分区数量、平面和边界。实体插入、更新、删除和查询仍在运行时代码里通过 `ISpatialIndex<T>` 完成，不通过 `.yokiframe` 文件桥传输高频操作。

### UIKit

```json
{
  "protocolVersion": 2,
  "engineId": "unity-editor",
  "source": "codex",
  "createdAtUtc": "2026-06-23T12:00:00Z",
  "requestId": "codex-uikit-001",
  "kit": "UIKit",
  "action": "get_workbench_snapshot",
  "payload": {}
}
```

返回 `data.stats`、`data.panels` 和 `data.stacks`。AI 默认优先读 `UIKit/state` snapshot；命令桥只做只读诊断，不通过 `.yokiframe` 打开、关闭、显示、隐藏、压栈或弹栈面板。

### TableKit / GraphKit

不要发送这些命令：

```text
TableKit/stats
TableKit/get_workbench_snapshot
GraphKit/stats
GraphKit/get_workbench_snapshot
```

TableKit 排查路径：

1. 读取 `.yokiframe/engines/<engineId>/engine.json`，查看 `optionalDependencies.luban` 是否可用。
2. 打开 Tauri TableKit 页面，检查 Luban 工作目录、`Luban.dll`、输出目录、运行时路径模式和控制台日志。
3. 检查用户项目生成目录，通常是 `Assets/Scripts/TableKit/` 和表数据输出目录。

GraphKit 排查路径：

1. 打开 Tauri GraphKit 页面，检查 graph project、node types、blackboard、edges、subgraphs、placemats 和 issues。
2. 查看 GraphKit 导出的 Luban Definition/Data XML 是否符合预期。
3. 查看 GraphRuntime contract，确认 graph、handler、portal route 和字段契约。
4. 若导出到 TableKit，再回到 TableKit 页面验证和生成。

## 故障排查

1. 查 `System/bridge_status`。
2. 查看 engine-scoped `commands/processing` 是否有 stale 命令。
3. 查看 `commands/deadletter` 和 response 的 `error.code`。
4. 确认 `requestId` 与 `engineId` 安全且一致。
5. 确认 Unity/Godot adapter 心跳仍在更新。
6. 高频状态优先读 snapshot/telemetry，不要用 `send_command` 高频轮询 UI。
7. TableKit/GraphKit 问题优先看 Tauri 页面和生成产物，不走 `.yokiframe` runtime 命令。
