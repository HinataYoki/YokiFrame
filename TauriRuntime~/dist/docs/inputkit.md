# InputKit 输入

InputKit 是纯 C# 输入门面。业务代码在 `YokiFrame` 命名空间中调用 `InputKit` 静态入口读取动作状态、输入值、缓冲输入和上下文栈；Unity、Godot 或未来宿主的差异放在 `IInputBackend` 后端里。

## 核心类型

| 类型 | 说明 |
|---|---|
| `InputKit` | 静态统一入口，负责后端安装、轮询更新、动作状态、缓冲输入和上下文栈。 |
| `IInputBackend` | 宿主输入后端接口，提供当前设备、手柄连接状态、`Poll()` 和 ActionMap 切换。 |
| `IInputStateWriter` | 后端在 `Poll()` 内写入动作状态的轻量 writer。 |
| `InputActionState` | 单个动作的当前帧状态，包括 `IsPressed`、`WasPressedThisFrame`、`WasReleasedThisFrame`、`Value`。 |
| `InputContext` | 输入上下文，定义优先级、启用 ActionMap、阻断动作和低优先级阻断策略。 |

## 后端状态与设备检测

| 属性/方法 | 说明 |
|-----------|------|
| `IsInitialized` | 是否已初始化。 |
| `CurrentDeviceType` | 当前输入设备类型。 |
| `IsUsingKeyboardMouse` | 是否使用键鼠。 |
| `IsUsingGamepad` | 是否使用手柄。 |
| `IsUsingTouch` | 是否使用触摸。 |
| `IsGamepadConnected` | 是否有手柄连接。 |
| `OnDeviceChanged` | 设备切换事件。 |

```csharp
if (InputKit.IsUsingGamepad)
{
    Debug.Log("Using gamepad");
}
```

## 快速使用

Unity 项目由 `UnityInputKitInstaller` 或 `UnityBootstrap` 注入后端；Godot 项目由 `GodotInputKitInstaller` 或 `GodotBootstrap` 注入后端。业务侧仍只依赖统一静态入口：

```csharp
using YokiFrame;

InputKit.Update(unscaledTime);

if (InputKit.WasPressedThisFrame("Jump"))
{
    Jump();
}

if (InputKit.IsPressed("Move"))
{
    var moveValue = InputKit.GetValue("Move");
}
```

### 动作状态查询

| 方法 | 说明 |
|------|------|
| `GetAction(actionName)` | 获取动作的完整状态对象。 |
| `IsPressed(actionName)` | 当前帧是否按下。 |
| `WasPressedThisFrame(actionName)` | 本帧是否刚按下。 |
| `WasReleasedThisFrame(actionName)` | 本帧是否刚释放。 |
| `GetValue(actionName)` | 获取动作的浮点值。 |

```csharp
InputActionState jumpState = InputKit.GetAction("Jump");
if (jumpState.IsPressed)
{
    Debug.Log($"Jump value: {jumpState.Value}");
}
```

输入缓冲适合容错窗口，例如翻滚、跳跃或连段窗口：

```csharp
InputKit.SetBufferWindow(160f);
InputKit.BufferInput("Dodge");

if (InputKit.ConsumeBufferedInput("Dodge"))
{
    Dodge();
}
```

### 缓冲查询 API

| 方法 | 说明 |
|------|------|
| `SetBufferWindow(ms)` | 设置缓冲窗口（毫秒）。 |
| `BufferInput(actionName, value)` | 缓冲一次输入。 |
| `HasBufferedInput(actionName)` | 是否有未消费的缓冲输入。 |
| `ConsumeBufferedInput(actionName)` | 消费缓冲输入，返回是否成功。 |
| `PeekBufferedInput(actionName, out timestamp, out value)` | 查看缓冲输入但不消费。 |
| `ClearBuffer()` | 清空所有缓冲。 |
| `ClearBuffer(actionName)` | 清空指定动作的缓冲。 |
| `CleanupBuffer()` | 清理过期缓冲。 |

上下文栈用于菜单、战斗、对话等输入域切换：

```csharp
var gameplay = new InputContext(
    "Gameplay",
    enabledActionMaps: new[] { "Gameplay" });

var menu = new InputContext(
    "Menu",
    priority: 100,
    enabledActionMaps: new[] { "UI" },
    blockedActions: new[] { "Attack", "Dodge" },
    blockAllLowerPriority: true);

InputKit.RegisterContext(gameplay);
InputKit.RegisterContext(menu);
InputKit.PushContext("Gameplay");
InputKit.PushContext("Menu");
InputKit.PopContext();
```

### 上下文管理 API

| 属性/方法 | 说明 |
|-----------|------|
| `CurrentContext` | 当前活跃上下文。 |
| `ContextDepth` | 上下文栈深度。 |
| `OnContextChanged` | 上下文切换事件。 |
| `RegisterContext(context)` | 注册上下文。 |
| `UnregisterContext(name)` | 注销上下文。 |
| `PushContext(context)` / `PushContext(name)` | 压入上下文。 |
| `PopContext()` | 弹出当前上下文。 |
| `PopToContext(name)` | 弹出到指定上下文。 |
| `ClearContextStack()` | 清空上下文栈。 |
| `IsActionBlocked(actionName)` | 检查动作是否被阻断。 |
| `HasContext(name)` | 是否存在指定上下文。 |

### ActionMap 管理

| 方法 | 说明 |
|------|------|
| `SwitchActionMap(mapName)` | 切换到指定 ActionMap。 |
| `EnableActionMaps(maps)` | 启用多个 ActionMap。 |
| `DisableAllActionMaps()` | 禁用所有 ActionMap。 |
| `GetEnabledActionMaps()` | 获取当前启用的 ActionMap 列表。 |

## 命令桥

InputKit 已接入文件命令桥。AI、Tauri 和脚本优先使用 engine-scoped v2 路径：

```json
{
  "protocolVersion": 2,
  "engineId": "unity-editor",
  "source": "codex",
  "createdAtUtc": "2026-06-23T12:00:00Z",
  "requestId": "codex-input-001",
  "kit": "InputKit",
  "action": "get_workbench_snapshot",
  "payload": {}
}
```

| action | payload | 说明 |
|---|---|---|
| `get_workbench_snapshot` | `{}` | 返回 `stats`、`actions` 和 `contexts`。 |
| `stats` | `{}` | 返回后端、设备、动作数量、缓冲数量和上下文数量。 |
| `list_actions` | `{}` | 返回当前动作状态列表。 |
| `list_contexts` | `{}` | 返回 active / registered context 和启用 ActionMap。 |

命令桥是只读诊断入口，不提供按键注入、输入模拟或重绑定命令。真实输入仍由宿主后端轮询并通过 `IInputStateWriter` 写入。

## Tauri 工作台

InputKit 页面读取顺序为：

1. `read_telemetry("InputKit", "state")`
2. `read_snapshot("InputKit", "state")`
3. `send_command("InputKit", "get_workbench_snapshot")`

Unity `KitStateSnapshotPublisher` 和 Godot `GodotKitStateSnapshotPublisher` 都通过可选 handler 发布 `InputKit/state`。页面只在缺少 telemetry/snapshot 或用户点击刷新时走命令桥，避免用高频命令轮询输入状态。

## AI 诊断入口

AI 默认优先读取：

```text
.yokiframe/engines/<engineId>/snapshots/InputKit/state.json
```

snapshot 缺失、过期或需要显式刷新时，再发送 `InputKit/get_workbench_snapshot`、`InputKit/stats`、`InputKit/list_actions` 或 `InputKit/list_contexts`。输入模拟、重绑定和设备写入不通过 `.yokiframe` 执行。
