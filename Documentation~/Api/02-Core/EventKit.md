# EventKit

> 面向读者：需要用强类型事件解耦业务模块的 Runtime 开发者
>
> 主要入口：`EventKit.Type`、`EventKit.Enum`
>
> 运行边界：跨宿主 Runtime；观察能力只在 Editor/Tools 编译
>
> 状态来源：`Documentation~/Api/00-GettingStarted/Kit_Status.md`

## 适用场景

EventKit 是跨模块通知用的事件基础设施。需要让发布方和订阅方解耦、又不希望引入宿主类型时使用它。新代码优先使用强类型 `TypeEvent`；固定协议信号使用 `EnumEvent`；`StringEvent` 仅用于旧代码兼容。

EventKit 不负责命令总线、请求-响应、跨进程消息或把事件自动变成 Workbench/CLI 可写操作。页面与 CLI 只读观察，不会代替业务注销监听器。

## 入口与当前状态

| 项目 | 当前值 |
|---|---|
| Runtime | 已实现，位于 `Core/Runtime/EventKit` |
| 程序集 | Core Runtime 编入 `YokiFrame`，无宿主引用 |
| Interaction | 已实现，Provider 位于 `Core/Editor/EventKit` |
| Workbench | 已实现：静态源码关系、监听数、Runtime 活动时间线 |
| 状态入口 | `EventKit/state` |

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
| `EventKit.Clear()` | 清空三个总线 | 仅测试隔离或完整会话重置 | 会摘掉全部监听 |

### `TypeEvent`

| API | 用途 | 约束 | 失败语义 |
|---|---|---|---|
| `Send<T>(T args = default)` | 发送强类型 payload | `T` 可为值类型、引用类型或空 struct；建议不可变 payload | 无监听器时无操作 |
| `Register<T>(Action<T>)` | 注册监听器 | 返回 `LinkUnRegister<T>`；发送端与接收端须同一类型定义 | 正常返回令牌 |
| `UnRegister<T>(Action<T>)` | 按委托注销 | 与注册委托同一实例/目标 | 未找到则无效果 |
| `Clear()` | 清空 Type 总线 | 同上全局风险 | — |
| `GetAllEvents()` | 按 `Type` 取 `IEasyEvent` 快照 | 主要用于 Editor/Tools 诊断 | — |

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
| `Clear()` / `GetAllEvents()` | 清空或取快照 | `GetAllEvents` 以 `EnumEventKey` 为索引 | — |

`EnumEventKey` 是公开只读值类型（`EnumType`、`EnumValue`、`Equals`、`GetHashCode`）。业务通常不直接构造它。

### `StringEvent`

业务事件使用强类型 `Send`、`Register` 与 `UnRegister`；`Clear` 和 `GetAllEvents` 用于完整会话重置或 Editor/Tools 诊断。

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
| `EasyEvent.GetListeners()` | 委托快照 | Editor/Tools | — |
| `EasyEvent<T>` | 带 payload 的局部事件 | API 对称：`Register`/`UnRegister`/`Trigger(T)` 等 | — |
| `EasyEvents.GetEvent<T>()` | 查询已存在容器 | **不创建** | 不存在则按实现返回 |
| `EasyEvents.GetOrAddEvent<T>()` | 获取或创建 `IEasyEvent` | `T : new()` | — |
| `EasyEvents.Clear()` / `GetAllEvents()` | 清空或读取容器 | — | — |
| `IEasyEvent.UnRegisterAll()` / `ListenerCount` | 清理契约；监听数 | `ListenerCount` 面向 Editor/Tools | — |

`IUnRegister.UnRegister()` 是统一注销入口。`LinkUnRegister`、`LinkUnRegister<T>` 与 `CustomUnRegister(Action)` 支持重复调用且不重复执行注销逻辑。

`EasyEvent` / `EasyEvent<T>` 使用侵入式 `PooledLinkedList<T>` 保存监听节点。节点注销后清空委托引用，并进入最多保留 64 个节点的空闲链；值类型节点租约同时保存 owner 与 generation，旧令牌副本不能注销已复用后的新监听器。业务直接使用 `PooledLinkedList<T>` 时可按峰值 `Prewarm`；EventKit 自有链表按需扩展并复用近期注销节点。

## 生命周期与错误边界

- EventKit 按宿主主线程设计；后台线程应切回宿主线程后再注册、发送或清理。
- 监听器异常由 `EventKitErrorHandler.OnError` 接收，也可调用 `Report(string)`；不要用空回调吞掉异常。
- 模块级注销用 `LinkUnRegister`；`Clear()` 清空整个总线，通常只用于测试或完整会话重置。
- Player 的发送、注册、注销和清理路径不包含观察调用。Editor/Tools 开启观察后，Runtime 只发送无业务负载的最小通知；类型/枚举展示文本、活动对象和 JSON 只在 Editor 侧按需生成。

## 宿主与工具入口

完整观察代码只在 Unity Editor 或 Godot Tools 编译。`Core/Runtime/EventKit` 仅保留宏包裹的最小 `EasyEventEditorHook` 通知契约；活动历史、类型格式化、快照、JSON、命令和 Provider 位于 `Core/Editor/EventKit`。

首次创建 Editor/Tools Provider 后才开始记录 `register`、`send`、`unregister`、`clear` 活动。`EventKit/state` 包含静态源码关系、按类型/枚举/字符串分组的监听数，以及最多 200 条近期活动。没有平行的 `EventKitEditor` 通用事件总线。枚举键常见值会按类型有限缓存，避免稳定发送路径重复装箱。

Workbench EventKit 页面只读：展示“发送方源码 → 事件身份 → 注册方源码”的静态关系，并叠加当前 Runtime 监听数与最近活动。CLI 示例：

静态扫描以项目 `Assets` 为调用点范围，并读取 `Library/ScriptAssemblies` 中已编译的 YokiFrame 程序集作为包内类型语义上下文。无命名空间类型直接显示类型名，不显示 Roslyn 的 `<global namespace>` 占位文本；枚举常量显示为 `类型.成员`，`mFSM.CurEnum` 这类运行时才能确定成员值的表达式显示其枚举类型。

```powershell
yoki telemetry read --engine <engineId> --kit EventKit --name state --project <projectRoot>
yoki command send --engine <engineId> --kit EventKit --action get_workbench_snapshot --project <projectRoot>
```

页面不会触发事件，也不会代替业务注销监听器。

## 限制与相关资料

- 业务协议使用 `EventKit.Type`；固定协议信号使用带强类型 payload 的 `EnumEvent`。
- 注册与注销必须成对；宿主 owner 退出时释放令牌，避免泄漏监听。
- 不提供请求-响应、跨进程总线，或由 Workbench/CLI 写业务事件。
- 诊断侧不存在第二套“编辑器事件总线”；观察能力只挂在 Provider 路径上。

## 维护

| 项 | 值 |
|---|---|
| 源码根 | `Core/Runtime/EventKit`；Provider `Core/Editor/EventKit` |
| 状态入口 | `EventKit/state` |
| 改 API 时同步 | 本文、`kit-index.md`、`yokiframe` / `yokiframe-cli` / `yokiframe-workbench` Skill |
