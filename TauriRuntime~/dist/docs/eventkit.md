# EventKit 事件

EventKit 是运行时事件总线，适合做业务模块之间的解耦通信。当前提供 Type、Enum 和 String 三条通道，其中新代码优先使用 Type 或 Enum。

## 选择哪种通道

| 通道 | 推荐程度 | 适合场景 |
|------|----------|----------|
| `EventKit.Type` | 推荐 | 强类型业务事件，payload 类型就是事件 key。 |
| `EventKit.Enum` | 推荐 | 固定系统信号、少量枚举 key、状态通知。 |
| `EventKit.String` | 兼容保留 | 旧代码迁移。该入口标记为 `Obsolete`。 |

## Type 事件

Type 事件使用 payload 类型作为事件 key。

```csharp
using UnityEngine;
using YokiFrame;

public readonly struct DamageTakenEvent
{
    public readonly int Amount;

    public DamageTakenEvent(int amount)
    {
        Amount = amount;
    }
}

public sealed class DamageListener : MonoBehaviour
{
    private void OnEnable()
    {
        EventKit.Type.Register<DamageTakenEvent>(OnDamageTaken);
    }

    private void OnDisable()
    {
        EventKit.Type.UnRegister<DamageTakenEvent>(OnDamageTaken);
    }

    private void OnDamageTaken(DamageTakenEvent evt)
    {
        Debug.Log("damage " + evt.Amount);
    }
}

EventKit.Type.Send(new DamageTakenEvent(10));
```

`Register<T>()` 会返回 `LinkUnRegister<T>`。如果你不想在注销时重新写泛型方法，也可以保存这个返回值：

```csharp
private LinkUnRegister<DamageTakenEvent> mUnregister;

private void OnEnable()
{
    mUnregister = EventKit.Type.Register<DamageTakenEvent>(OnDamageTaken);
}

private void OnDisable()
{
    mUnregister.UnRegister();
}
```

## Enum 事件

Enum 事件适合固定 key。它同时支持无参数事件和带一个明确 payload 类型的事件。

```csharp
using YokiFrame;

public enum BattleSignal
{
    RoundStarted,
    ScoreChanged
}

private void OnEnable()
{
    EventKit.Enum.Register(BattleSignal.RoundStarted, OnRoundStarted);
    EventKit.Enum.Register<BattleSignal, int>(BattleSignal.ScoreChanged, OnScoreChanged);
}

private void OnDisable()
{
    EventKit.Enum.UnRegister(BattleSignal.RoundStarted, OnRoundStarted);
    EventKit.Enum.UnRegister<BattleSignal, int>(BattleSignal.ScoreChanged, OnScoreChanged);
}

private void OnRoundStarted()
{
}

private void OnScoreChanged(int score)
{
}

EventKit.Enum.Send(BattleSignal.RoundStarted);
EventKit.Enum.Send(BattleSignal.ScoreChanged, 100);
```

`EventKit.Enum.Send<TEnum>(key, params object[] args)` 也存在，但业务代码优先使用带明确 `TArgs` 的重载，避免接收端和发送端对参数顺序产生隐式约定。

## String 兼容路径

String 事件仍可使用，但新增业务不建议继续扩展。

```csharp
#pragma warning disable CS0618
EventKit.String.Register<string>("legacy.toast", OnToast);
EventKit.String.Send("legacy.toast", "hello");
EventKit.String.UnRegister<string>("legacy.toast", OnToast);
#pragma warning restore CS0618
```

迁移旧代码时，可以先保留 String 事件，再逐步替换为 Type 或 Enum。

## 事件诊断

`TypeEvent`、`EnumEvent` 和 `StringEvent` 都提供 `GetAllEvents()` 方法，用于查看当前已注册的事件通道：

```csharp
// 查看 Type 通道中所有已注册的事件
var typeEvents = EventKit.Type.GetAllEvents();
foreach (var kvp in typeEvents)
{
    Debug.Log($"Type: {kvp.Key}, Listeners: {kvp.Value}");
}

// 查看 Enum 通道中所有已注册的事件
var enumEvents = EventKit.Enum.GetAllEvents();
foreach (var kvp in enumEvents)
{
    Debug.Log($"Enum: {kvp.Key}, Listeners: {kvp.Value}");
}
```

这些 API 适合调试和诊断，不建议在热路径中频繁调用。

## EasyEvent 直接使用

除了通过 `EventKit.Type` / `EventKit.Enum` 通道使用事件外，也可以直接创建 `EasyEvent` 或 `EasyEvent<T>` 实例，适合模块内部事件或局部事件：

```csharp
using YokiFrame;

// 无参数事件
var onReady = new EasyEvent();
var unregister = onReady.Register(() => Debug.Log("ready"));
onReady.Trigger();
unregister.UnRegister();

// 带参数事件
var onDamage = new EasyEvent<int>();
onDamage.Register(amount => Debug.Log("damage " + amount));
onDamage.Trigger(100);
```

`EasyEvent` / `EasyEvent<T>` 提供以下成员：

| 成员 | 说明 |
|------|------|
| `Register(action)` | 注册监听器，返回 `LinkUnRegister` / `LinkUnRegister<T>`。 |
| `UnRegister(action)` | 按委托注销监听器。 |
| `Trigger()` / `Trigger(args)` | 触发事件，通知所有监听器。 |
| `UnRegisterAll()` | 注销所有监听器。 |
| `ListenerCount` | 当前监听器数量。 |
| `GetListeners()` | 获取当前监听器列表（只读快照）。 |

`UnRegisterAll()` 适合在模块关闭或重置时一次性清理所有监听器。`GetListeners()` 返回的是当前快照，不会因为触发过程中的注册/注销而改变。

## 生命周期建议

- 注册和注销成对出现，常见位置是 Unity `OnEnable` / `OnDisable`，或服务对象的显式启动/停止生命周期。
- payload 优先使用不可变结构或只读数据，避免多个监听器之间互相修改同一对象。
- 监听器内部抛异常时，底层事件容器会通过 `EventKitErrorHandler.OnError` 报告错误，不会阻断后续监听器。
- `EventKit.Type.Clear()`、`EventKit.Enum.Clear()`、`EventKit.String.Clear()` 会清空对应通道全部监听器，通常只适合测试或完整重置流程。

## 错误处理

可以在项目启动时接入统一错误处理：

```csharp
EventKitErrorHandler.OnError = message =>
{
    Debug.LogError(message);
};
```

Unity 默认启动器会把该回调接到 Unity 日志。项目有自己的日志系统时，也可以覆盖为自定义处理。

## 常见问题

| 问题 | 处理方式 |
|------|----------|
| 收不到事件 | 确认注册发生在发送之前，并且没有提前注销或调用 `Clear()`。 |
| 场景切换后回调还在 | 检查监听者是否在禁用或销毁时调用了 `UnRegister()`。 |
| Enum 事件参数收不到 | 发送端和接收端必须使用同一个 `TArgs` 类型。 |
| String 事件重构困难 | 新增代码改用 Type 或 Enum；旧代码迁移时先封装统一事件类型。 |
