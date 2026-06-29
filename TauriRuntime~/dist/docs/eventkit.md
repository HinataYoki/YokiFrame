# EventKit 事件

## 选择通道

| 通道 | 什么时候用 | 建议 |
|---|---|---|
| `EventKit.Type` | payload 类型就是事件 key | 新业务默认选择。 |
| `EventKit.Enum` | 固定系统信号、少量枚举 key | 适合 UI、流程、系统状态。 |
| `EventKit.String` | 旧代码兼容 | 不建议新增。 |

## Type 事件

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

想减少注销时的重复代码，可以保存返回值：

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

```csharp
using YokiFrame;

public enum BattleSignal
{
    RoundStarted,
    ScoreChanged
}

EventKit.Enum.Register(BattleSignal.RoundStarted, OnRoundStarted);
EventKit.Enum.Register<BattleSignal, int>(BattleSignal.ScoreChanged, OnScoreChanged);

EventKit.Enum.Send(BattleSignal.RoundStarted);
EventKit.Enum.Send(BattleSignal.ScoreChanged, 100);

EventKit.Enum.UnRegister(BattleSignal.RoundStarted, OnRoundStarted);
EventKit.Enum.UnRegister<BattleSignal, int>(BattleSignal.ScoreChanged, OnScoreChanged);
```

带参数事件优先使用明确的 `TArgs`，不要靠 `object[]` 约定参数顺序。

## 局部事件

模块内部事件可以直接用 `EasyEvent` / `EasyEvent<T>`，不用挂到全局 EventKit。

```csharp
var onReady = new EasyEvent();
var unregister = onReady.Register(OnReady);

onReady.Trigger();
unregister.UnRegister();
```

## 工作台诊断

EventKit 页面用于看当前事件注册、最近事件、发送方/接收方代码扫描结果。

| 在工作台里看什么 | 用途 |
|---|---|
| 注册列表 | 确认事件 key、payload 类型和 handler 数量。 |
| 最近事件 | 判断事件是否真的发送过、发送频率是否异常。 |
| 代码扫描 | 找到发送、监听、注销位置。 |
| 排除 Editor | 减少编辑器工具事件干扰，更接近游戏业务关系。 |

收不到事件时，先看注册列表里有没有监听者，再看最近事件里有没有发送记录。场景切换后还回调时，用代码扫描检查注销路径。

## 常见坑

| 问题 | 处理方式 |
|---|---|
| 收不到事件 | 确认注册发生在发送前，且没有提前注销或 `Clear()`。 |
| 场景切换后还回调 | 在 `OnDisable` / `OnDestroy` 或服务停止时注销。 |
| Enum 参数收不到 | 发送端和接收端必须使用同一个 `TArgs`。 |
| String 事件难维护 | 新增事件改 Type / Enum，旧代码逐步迁移。 |
| 监听器异常影响排查 | 接入 `EventKitErrorHandler.OnError` 到项目日志。 |

## 边界

- `Clear()` 会清空整个通道，通常只在测试或完整重置流程中用。
- `GetAllEvents()` 是诊断 API，不要在热路径频繁调用。
- payload 尽量不可变，避免多个监听器互相修改同一对象。
