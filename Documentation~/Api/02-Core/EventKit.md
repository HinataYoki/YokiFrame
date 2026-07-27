# EventKit 事件

## 适用场景

EventKit 是跨模块通知用的事件基础设施。需要让发布方和订阅方解耦、又不希望引入宿主类型时使用它。新代码优先使用强类型 `TypeEvent`；固定协议信号使用 `EnumEvent`；`StringEvent` 仅用于旧代码兼容。

EventKit 不负责命令总线、请求-响应、跨进程消息或把事件自动变成 Workbench/CLI 可写操作。页面与 CLI 只读观察，不会代替业务注销监听器。

## 使用前提

EventKit 可直接用于 Unity 与 Godot .NET Runtime。Workbench 只读展示事件关系和活动信息，不会替业务发送事件或注销监听器。

## 快速上手

```csharp
using YokiFrame;

public readonly struct DamageTaken
{
    public DamageTaken(int amount) { Amount = amount; }
    public int Amount { get; }
}

LinkUnRegister<DamageTaken> link =
    EventKit.Type.Register<DamageTaken>(OnDamageTaken);

EventKit.Type.Send(new DamageTaken(10));
link.UnRegister();

static void OnDamageTaken(DamageTaken value)
{
    LogKit.Info("damage=" + value.Amount);
}
```

Unity 组件通常在 `OnEnable` 注册、在 `OnDisable` 调用令牌的 `UnRegister()`。不要用全局 `Clear()` 代替单个模块的注销。对象内部生命周期事件优先用 `EasyEvent` / `EasyEvent<T>`，不要强行进全局总线。

## 核心 API

### `EventKit`

| API | 用途 | 约束 | 失败语义 |
|---|---|---|---|
| `EventKit.Type` | 全局 `TypeEvent`，payload 运行时类型为 key | 主线程；跨模块默认入口 | 无监听时 `Send` 无操作 |
| `EventKit.Enum` | 全局 `EnumEvent`，枚举类型+值构成 key | 注册与发送的 `TEnum`/`TArgs` 必须一致 | 类型或 `TArgs` 不一致时不会命中同一槽位 |
| `EventKit.Clear()` | 清空三个总线 | 完整会话重置 | 会摘掉全部监听 |

### `TypeEvent`

| API | 用途 | 约束 | 失败语义 |
|---|---|---|---|
| `Send<T>(T args = default)` | 发送强类型 payload | `T` 可为值类型、引用类型或空 struct；建议不可变 payload | 无监听器时无操作 |
| `Register<T>(Action<T>)` | 注册监听器 | 返回 `LinkUnRegister<T>`；发送端与接收端须同一类型定义 | 正常返回令牌 |
| `UnRegister<T>(Action<T>)` | 按委托注销 | 与注册委托同一实例/目标 | 未找到则无效果 |
| `Clear()` | 清空 Type 总线 | 同上全局风险 | — |

### `EnumEvent`

```csharp
public enum BattleSignal { Started, ScoreChanged }

EventKit.Enum.Register(BattleSignal.Started, OnStarted);
EventKit.Enum.Register<BattleSignal, int>(BattleSignal.ScoreChanged, OnScoreChanged);
EventKit.Enum.Send(BattleSignal.Started);
EventKit.Enum.Send(BattleSignal.ScoreChanged, 100);
```

| API | 用途 | 约束 | 失败语义 |
|---|---|---|---|
| `Send<TEnum>(TEnum key)` | 无 payload 枚举事件 | `TEnum` 为枚举 | 无监听时无操作 |
| `Send<TEnum,TArgs>(TEnum key, TArgs args)` | 单一强类型 payload | 推荐新代码路径 | 无监听时无操作 |
| `Register<TEnum>(...)` / `Register<TEnum,TArgs>(...)` | 注册监听 | 返回 `LinkUnRegister` / `LinkUnRegister<TArgs>` | — |
| `UnRegister<TEnum>(TEnum key)` | 清空该 key 全部监听 | — | — |
| `UnRegister<TEnum>(...)` / `UnRegister<TEnum,TArgs>(...)` | 按委托注销 | — | — |
| `Clear()` | 清空该枚举总线 | 会移除该总线的全部监听 | — |

`EnumEventKey` 是公开只读值类型（`EnumType`、`EnumValue`、`Equals`、`GetHashCode`）。业务通常不直接构造它。

### `StringEvent`

业务事件使用强类型 `Send`、`Register` 与 `UnRegister`；`StringEvent` 只在需要字符串键时使用。

### `EasyEvent` 与注销令牌

`EasyEvent` 不进入全局总线，适合对象内部生命周期：

```csharp
EasyEvent ready = new();
LinkUnRegister link = ready.Register(OnReady);
ready.Trigger();
link.UnRegister();
```

| 类型/API | 用途 | 约束 | 失败语义 |
|---|---|---|---|
| `EasyEvent.Register(Action)` | 无参监听 | 返回 `LinkUnRegister` | — |
| `EasyEvent.UnRegister(Action)` | 按委托注销 | — | 返回是否移除成功 |
| `EasyEvent.Trigger()` | 触发当前监听 | 主线程 | 监听器异常见错误处理 |
| `EasyEvent.UnRegisterAll()` | 清空当前事件 | — | — |
| `EasyEvent<T>` | 带 payload 的局部事件 | API 对称：`Register`/`UnRegister`/`Trigger(T)` 等 | — |
| `EasyEvents.GetEvent<T>()` | 查询已存在容器 | **不创建** | 不存在则按实现返回 |
| `EasyEvents.GetOrAddEvent<T>()` | 获取或创建 `IEasyEvent` | `T : new()` | — |
| `EasyEvents.Clear()` / `GetAllEvents()` | 清空或读取容器 | — | — |
| `IEasyEvent.UnRegisterAll()` / `ListenerCount` | 清理契约；监听数 | — | — |

`IUnRegister.UnRegister()` 是统一注销入口。`LinkUnRegister`、`LinkUnRegister<T>` 与 `CustomUnRegister(Action)` 支持重复调用且不重复执行注销逻辑。

注销令牌可以重复调用而不会重复执行清理。监听器由事件对象持有，订阅方退出时仍必须主动注销；不要依赖底层存储实现来管理业务对象生命周期。

## 生命周期与错误边界

- EventKit 按宿主主线程设计；后台线程应切回宿主线程后再注册、发送或清理。
- 监听器异常由 `EventKitErrorHandler.OnError` 接收，也可调用 `Report(string)`；不要用空回调吞掉异常。
- 模块级注销用 `LinkUnRegister`；`Clear()` 清空整个总线，只用于完整会话重置。
- 事件总线不会替你管理业务对象的生命周期；订阅方退出时必须主动注销自己的令牌。

## 在工具中查看

Workbench 的 EventKit 页面只读展示事件关系、当前监听数量和最近活动，不会触发事件，也不会代替业务注销监听器。

## 限制与相关资料

- 业务协议使用 `EventKit.Type`；固定协议信号使用带强类型 payload 的 `EnumEvent`。
- 注册与注销必须成对；宿主 owner 退出时释放令牌，避免泄漏监听。
- 不提供请求-响应或跨进程消息；需要这类通信时使用项目自己的消息层。
