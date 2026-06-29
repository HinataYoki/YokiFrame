# InputKit 输入

## 配置后端

Unity / Godot 项目通常由 bootstrap 或 installer 注入输入后端。业务代码只依赖：

```csharp
using YokiFrame;
```

手动接入项目输入系统时实现 `IInputBackend`：

```csharp
InputKit.SetBackend(new ProjectInputBackend());
```

## 读取动作

```csharp
InputKit.Update(unscaledTime);

if (InputKit.WasPressedThisFrame("Jump"))
{
    Jump();
}

if (InputKit.IsPressed("Move"))
{
    float moveValue = InputKit.GetValue("Move");
}
```

常用查询：

| 方法 | 说明 |
|---|---|
| `GetAction(actionName)` | 完整动作状态。 |
| `IsPressed(actionName)` | 当前是否按下。 |
| `WasPressedThisFrame(actionName)` | 本帧刚按下。 |
| `WasReleasedThisFrame(actionName)` | 本帧刚释放。 |
| `GetValue(actionName)` | 浮点输入值。 |

## 输入缓冲

适合跳跃、翻滚、连段等容错窗口。

```csharp
InputKit.SetBufferWindow(160f);
InputKit.BufferInput("Dodge");

if (InputKit.ConsumeBufferedInput("Dodge"))
{
    Dodge();
}
```

常用方法：`HasBufferedInput()`、`PeekBufferedInput()`、`ClearBuffer()`、`CleanupBuffer()`。

## 上下文栈

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

| 方法 | 说明 |
|---|---|
| `RegisterContext(context)` | 注册上下文。 |
| `PushContext(name)` | 压入上下文。 |
| `PopContext()` | 弹出当前上下文。 |
| `PopToContext(name)` | 弹出到指定上下文。 |
| `ClearContextStack()` | 清空栈。 |
| `IsActionBlocked(actionName)` | 检查动作是否被阻断。 |

## 工作台诊断

InputKit 页面用于查看后端、设备、动作状态、输入缓冲和上下文栈。

| 在工作台里看什么 | 用途 |
|---|---|
| Backend / Devices | 确认输入系统是否接入、设备是否识别。 |
| Actions | 查看动作是否 Pressed、Released 或有数值。 |
| Buffer | 判断按键缓冲是否进入队列、是否过期。 |
| Context Stack | 检查当前启用的是哪个输入上下文。 |

按键没反应时，先看设备，再看 Action 状态，最后看 Context Stack 是否被 UI、剧情或战斗状态切走。工作台不做按键注入或重绑定。

## 常见坑

| 问题 | 处理方式 |
|---|---|
| 动作一直 false | 确认后端已安装，并且宿主循环调用 `InputKit.Update()`。 |
| 菜单打开后战斗输入还生效 | 检查上下文优先级、`blockedActions` 和 `blockAllLowerPriority`。 |
| 缓冲输入没触发 | 检查缓冲窗口单位是毫秒，并确认没有提前 `ClearBuffer()`。 |
| 想模拟按键 | 在宿主测试后端或项目测试工具里做，不通过工作台页面注入。 |
