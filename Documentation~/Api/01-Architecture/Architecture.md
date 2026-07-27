# Architecture 项目架构

## 适用场景

Architecture 是按具体类型建立的服务容器。它负责服务注册、架构注入、一次性初始化、替换释放和服务查询；不负责存档、线程调度或宿主对象生命周期。

## 使用前提

Architecture 是跨 Unity 与 Godot .NET 的纯 C# Runtime 能力。它没有独立 Workbench 页面；需要查看项目状态时使用 Workbench 的“框架”页。

## 快速上手

```csharp
using YokiFrame;

public sealed class GameArchitecture : Architecture<GameArchitecture>
{
    protected override void OnInit()
    {
        Register<PlayerModel>(new PlayerModel());
        Register<InventoryService>(new InventoryService());
    }
}

public sealed class InventoryService : AbstractService
{
    protected override void OnInit() { }

    public void AddItem(string itemId)
    {
        GetService<PlayerModel>().Add(itemId);
    }
}

public sealed class PlayerModel : AbstractModel
{
    protected override void OnInit() { }

    public void Add(string itemId) { }

    public override void GetObjectData(
        System.Runtime.Serialization.SerializationInfo info,
        System.Runtime.Serialization.StreamingContext context)
    {
        info.AddValue("version", 1);
    }
}

InventoryService inventory =
    GameArchitecture.Interface.GetService<InventoryService>();
```

第一次访问 `GameArchitecture.Interface` 时创建架构、执行 `OnInit()`，再初始化已经注册的服务。服务依赖通过 `GetService<T>()` 查询，不要在服务中重新创建其它服务。

## 核心 API

### `Architecture<T>`

| API | 说明 |
|---|---|
| `static IArchitecture Interface` | 获取类型级架构单例；首次访问会创建并初始化。 |
| `bool Initialized` | 当前架构是否完成初始化。 |
| `void Register<K>(K service)` | 注册服务；同一类型重复注册会先释放旧服务。`service` 不能为 null。 |
| `K GetService<K>(bool force = false)` | 查询服务；未注册时返回 null。`force=true` 会使用无参构造函数创建、注册并初始化；同一类型的并发强制请求共享一次创建。 |
| `IEnumerable<IService> GetAllServices()` | 返回当前服务快照，不暴露内部字典。 |
| `Dispose()` | 释放架构和全部服务，并允许下一次访问重新创建。已释放实例上的 `Register` 或 `GetService<T>(true)` 抛出 `ObjectDisposedException`。 |
| `protected OnInit()` | 子类注册服务的入口，只执行一次。 |
| `protected OnDispose()` | 子类释放架构级资源的可选入口。 |

约束：`T` 必须继承 `Architecture<T>` 并提供无参构造函数；服务类型必须是引用类型、实现 `IService` 并提供无参构造函数。

### 契约与基类

| 类型 | API | 说明 |
|---|---|---|
| `ICanInit` | `bool Initialized` / `void Init()` / `Dispose()` | 所有架构和服务共用的一次初始化、释放契约。 |
| `IService` | `IArchitecture Architecture` / `SetArchitecture(IArchitecture)` | 服务所属架构由注册流程注入，业务不应自行替换。 |
| `IModel` | `ISerializable` | 在服务生命周期之外增加 `GetObjectData` 序列化契约。 |
| `AbstractService` | `GetService<K>()` | 提供架构查询和 `OnInit`、`OnDispose` 覆写点。未注入架构时查询返回 null。 |
| `AbstractModel` | `GetObjectData(SerializationInfo, StreamingContext)` | 在 `AbstractService` 基础上要求模型实现序列化。 |

## 生命周期与错误边界

1. `Interface` 首次访问创建架构。
2. 架构执行 `OnInit()`，其中调用 `Register`。
3. 架构初始化所有尚未初始化的服务；服务初始化期间新增的服务也会被处理。
4. 重复注册同一类型时，新服务先注入架构，旧服务在容器锁外 `Dispose()`。
5. 释放架构时清空服务表并逐个释放服务。

`force=true` 适合明确的延迟创建依赖，不要用它掩盖初始化顺序问题。同一服务类型的并发强制请求共享同一创建结果；创建期间若调用方已显式完成 `Register`，显式实例优先，未采用的候选会被释放。稳定服务优先在 `OnInit()` 显式注册。

## 在工具中查看

Architecture 没有独立 Workbench 页面。需要确认项目连接和运行态状态时，查看 Workbench 的“框架”页；服务的注册、替换和释放仍由 Runtime 代码负责。

## 限制与相关资料

| 问题 | 处理 |
|---|---|
| 获取结果为 null | 检查类型是否在 `OnInit()` 注册，或明确使用 `GetService<T>(true)`。 |
| 服务初始化顺序不稳定 | 让依赖服务先注册，或把依赖读取放入 `OnInit` 之后的业务阶段。 |
| 替换后旧服务仍回调 | 把事件、计时器等外部订阅放到 `OnDispose()` 清理。 |
| 模型序列化失败 | `IModel` 只提供序列化契约，实际存档流程仍由项目或 SaveKit 负责。 |
