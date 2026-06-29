# Architecture

## 最小结构

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

服务：

```csharp
using YokiFrame;

public sealed class InventoryService : AbstractService
{
    public void AddItem(string itemId)
    {
        var profile = GetService<PlayerProfileModel>();
        profile.AddOwnedItem(itemId);
    }
}
```

模型：

```csharp
using System.Collections.Generic;
using System.Runtime.Serialization;
using YokiFrame;

public sealed class PlayerProfileModel : AbstractModel
{
    private readonly List<string> mOwnedItems = new List<string>();

    public void AddOwnedItem(string itemId)
    {
        mOwnedItems.Add(itemId);
    }

    public override void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        info.AddValue(nameof(mOwnedItems), mOwnedItems.ToArray());
    }
}
```

使用：

```csharp
var inventory = GameArchitecture.Interface.GetService<InventoryService>();
inventory.AddItem("sword");
```

第一次访问 `GameArchitecture.Interface` 时会创建架构、执行 `OnInit()`、初始化已注册服务。

## 核心类型

| 类型 | 作用 |
|---|---|
| `IArchitecture` | 架构容器接口。 |
| `Architecture<T>` | 类型级架构单例基类。 |
| `IService` | 服务契约。 |
| `IModel` | 数据模型契约，继承 `IService` 和 `ISerializable`。 |
| `AbstractService` | 服务基类，提供架构注入和 `GetService<T>()`。 |
| `AbstractModel` | 数据模型基类。 |
| `ICanInit` | `Init()` / `Dispose()` 生命周期契约。 |

## 生命周期

| 阶段 | 行为 |
|---|---|
| 创建 | 第一次访问 `Architecture<T>.Interface`。 |
| 注册 | 在 `OnInit()` 中调用 `Register<TService>()`。 |
| 初始化 | 架构 `OnInit()` 完成后初始化已注册服务。 |
| 获取 | `GetService<TService>()` 未注册时返回 `null`。 |
| 替换 | 重复注册同一服务类型会释放旧服务。 |

## 工作台诊断

Architecture 页面用于确认当前宿主里有哪些架构实例、是否初始化、注册了哪些服务。

| 在工作台里看什么 | 用途 |
|---|---|
| 架构实例列表 | 确认当前宿主里创建了哪些 Architecture。 |
| 初始化状态 | 判断 `OnInit()` 是否已经执行。 |
| 服务数量和服务列表 | 检查 `Register<T>()` 是否漏注册、重复注册或类型不一致。 |
| 架构详情 | 点开单个架构，查看服务实现类型和生命周期状态。 |

如果服务拿不到，先在 Architecture 页面确认实例存在，再检查服务列表里的注册类型。工作台只显示状态，不负责注册、替换或释放服务。

## 常见坑

| 问题 | 处理方式 |
|---|---|
| 服务拿不到 | 确认在 `OnInit()` 注册，并且获取类型和注册类型一致。 |
| 服务依赖 Unity 对象 | 放到 Unity Adapter、MonoBehaviour 或专门后端，不放进 Base 服务。 |
| `IModel` 自动存档失败 | `IModel` 只定义序列化契约，仍需 SaveKit 或项目存档流程调用。 |
| 滥用 `force: true` | 稳定服务优先在 `OnInit()` 注册，避免运行中偷偷创建生命周期不清的服务。 |
