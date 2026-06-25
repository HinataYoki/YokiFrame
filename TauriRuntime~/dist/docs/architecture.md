# Architecture

`Architecture` 是 YokiFrame Base 层提供的服务化架构入口。它适合把项目级服务、数据模型和跨模块依赖集中注册到一个纯 C# 架构对象里，让业务代码通过接口获取服务，而不是到处创建全局对象或互相持有具体实现。

## 定位

| 类型 | 作用 |
|------|------|
| `IArchitecture` | 架构容器接口，负责注册、获取和枚举服务。 |
| `Architecture<T>` | 架构基类，提供单例入口 `Interface` 和服务字典。 |
| `IService` | 服务接口，服务可以拿到所属 `IArchitecture`。 |
| `IModel` | 数据服务接口，继承 `IService` 和 `ISerializable`。 |
| `AbstractService` | 服务基类，封装架构注入和服务间获取逻辑。 |
| `AbstractModel` | 数据模型基类，适合承载需要序列化的项目状态。 |
| `ICanInit` | 初始化和释放契约，包含 `Initialized`、`Init()`、`Dispose()`。 |

`Architecture` 位于 `YokiFrame` 命名空间，源码在 `Assets/YokiFrame/Core/Runtime/Architecture/Architecture.cs`。它不依赖 UnityEngine 或 Godot API，可以被 Unity、Godot 或纯 C# 测试直接复用。

## 基本用法

定义一个项目架构，并在 `OnInit()` 中注册服务：

```csharp
using YokiFrame;

public sealed class GameArchitecture : Architecture<GameArchitecture>
{
    protected override void OnInit()
    {
        Register<InventoryService>(new InventoryService());
        Register<PlayerProfileModel>(new PlayerProfileModel());
    }
}
```

定义服务：

```csharp
using YokiFrame;

public sealed class InventoryService : AbstractService
{
    protected override void OnInit()
    {
        var profile = GetService<PlayerProfileModel>();
        profile.SetDisplayName("Player");
    }

    public void AddItem(string itemId)
    {
        var profile = GetService<PlayerProfileModel>();
        profile.AddOwnedItem(itemId);
    }

    public bool Contains(string itemId)
    {
        var profile = GetService<PlayerProfileModel>();
        return profile != null && profile.ContainsOwnedItem(itemId);
    }
}
```

定义模型：

```csharp
using System.Collections.Generic;
using System.Runtime.Serialization;
using YokiFrame;

public sealed class PlayerProfileModel : AbstractModel
{
    private readonly List<string> mOwnedItems = new List<string>();

    public string DisplayName { get; private set; }

    public void SetDisplayName(string displayName)
    {
        DisplayName = displayName;
    }

    public void AddOwnedItem(string itemId)
    {
        mOwnedItems.Add(itemId);
    }

    public bool ContainsOwnedItem(string itemId)
    {
        return mOwnedItems.Contains(itemId);
    }

    public override void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        info.AddValue(nameof(DisplayName), DisplayName);
        info.AddValue(nameof(mOwnedItems), mOwnedItems.ToArray());
    }
}
```

使用服务：

```csharp
var service = GameArchitecture.Interface.GetService<InventoryService>();
service.AddItem("sword");
```

第一次访问 `GameArchitecture.Interface` 时，框架会创建架构实例、执行 `OnInit()`、初始化已注册服务，然后把架构标记为已初始化。

## 生命周期

| 阶段 | 行为 |
|------|------|
| 创建架构 | 第一次访问 `Architecture<T>.Interface` 时创建单例架构实例。 |
| 注册服务 | `OnInit()` 中调用 `Register<TService>(service)`，服务会被注入当前架构。 |
| 初始化服务 | 架构完成 `OnInit()` 后依次调用已注册服务的 `Init()`。 |
| 获取服务 | `GetService<TService>()` 返回已注册服务；未注册时返回 `null`。 |
| 强制创建 | `GetService<TService>(force: true)` 会创建并注册缺失服务。 |
| 替换服务 | 重复 `Register<TService>()` 会先释放旧服务，再保存新服务。 |

推荐把稳定服务放在 `Architecture<T>.OnInit()` 中注册。`force: true` 适合少量兜底创建，不适合作为主要生命周期入口，因为动态创建的服务不会参与首次初始化批次。

## 服务间通信

`AbstractService` 会保存所属架构，服务内部可以通过 `GetService<T>()` 获取同一架构下的其他服务：

```csharp
public sealed class QuestService : AbstractService
{
    protected override void OnInit()
    {
    }

    public bool HasItem(string itemId)
    {
        var inventory = GetService<InventoryService>();
        return inventory != null && inventory.Contains(itemId);
    }
}
```

这种方式适合项目级服务互相协作。普通业务代码仍然优先调用 `EventKit`、`FsmKit`、`PoolKit`、`ResKit` 等 Kit API，不需要为了每个系统都包一层 Architecture 服务。

## 工作台与命令桥

Tauri 工作台左侧导航的 `Architecture` 分类中只有一个 `Architecture` 窗口，用来查看当前宿主里已经存活的 `Architecture<T>.Interface` 实例，以及每个实例注册的服务契约类型和实现类型。

AI 调试时可以通过 CommandBridge 查询同样的信息：

| action | 作用 |
|--------|------|
| `stats` | 返回 Architecture 实例数、存活数、初始化数和服务总数。 |
| `get_workbench_snapshot` | 返回工作台完整快照，包含 `stats` 和 `architectures` 列表。 |
| `list_architectures` | 列出每个 Architecture 及其注册服务。 |
| `get_architecture_detail` | 按 `fullName` 或 `typeName` 查询单个 Architecture。 |

这些命令只读，不会通过文件桥注册、替换或释放服务。服务生命周期仍由业务代码在 `OnInit()` 中调用 `Register<T>()` 管理。

## 什么时候使用

| 场景 | 建议 |
|------|------|
| 需要集中管理项目服务 | 使用 `Architecture<T>` 注册多个 `IService`。 |
| 数据模型需要参与保存或恢复 | 继承 `AbstractModel`，实现 `GetObjectData()`。 |
| 只是一个简单全局单例 | 优先使用 `SingletonKit<T>` 或 `Singleton<T>`。 |
| 需要 Unity / Godot 生命周期 | 使用对应 Adapter 的生命周期单例或宿主组件，不把引擎 API 放进 Base 服务。 |
| 服务需要发通知 | 服务内部可以调用 `EventKit.Type` 或 `EventKit.Enum`，保持 payload 强类型。 |

## 注意事项

- `Architecture<T>` 是类型级单例，同一个 `T` 只会创建一个架构实例。
- 服务类型必须满足 `where T : class, IService, new()`，因此服务需要无参构造函数。
- `Register<T>()` 会以服务的具体泛型类型作为 key。需要通过接口抽象获取服务时，应在注册和获取时使用同一个类型约定。
- `IModel` 只规定序列化契约，不会自动接入 SaveKit。项目仍需要在 SaveKit 或自定义存档流程中调用模型数据。
- Base 层服务不要直接引用 UnityEngine、Godot API、Tauri API 或宿主专属 JSON 库。
