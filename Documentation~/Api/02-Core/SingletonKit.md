# SingletonKit 单例

## 适用场景

SingletonKit 解决“整个宿主会话只需要一个纯 C# 服务实例”的场景，例如配置缓存、无状态协调器和轻量基础服务。它不应该替代有明确 owner 的依赖注入或 `Architecture<T>` 服务注册；需要 Unity `GameObject`、`Transform` 或 Godot `Node` 生命周期时使用对应宿主 Adapter 的单例类型。

## 使用前提

SingletonKit 是跨 Unity 与 Godot .NET 的纯 C# Runtime 能力，没有独立 Workbench 页面。需要按依赖关系组织多个服务时，优先考虑 `Architecture<T>`。

## 快速上手

```csharp
using YokiFrame;

public sealed class SettingsService : Singleton<SettingsService>
{
    public int MaxLevel { get; private set; }

    public override void OnSingletonInit()
    {
        MaxLevel = 10;
    }
}

SettingsService settings = SettingsService.Instance;
Singleton<SettingsService>.Dispose();
```

不继承基类时，实现 `ISingleton` 并通过 `SingletonKit<T>` 访问：

```csharp
public sealed class CacheService : ISingleton
{
    private CacheService() { }

    public void OnSingletonInit() { }

    public static CacheService Instance =>
        SingletonKit<CacheService>.Instance;
}
```

## 核心 API

### `SingletonKit<T>`

要求 `T : class, ISingleton`。实例通过线程安全的懒初始化创建，允许公开或私有无参构造函数：

| API | 说明 |
|---|---|
| `SingletonKit<T>.Instance` | 获取或创建实例，成功后调用一次 `OnSingletonInit()`。 |
| `HasInstance` | 查询是否已创建，不触发创建。 |
| `TryGetInstance(out T instance)` | 获取已有实例，不触发创建。 |
| `Dispose()` | 清除当前实例引用；下一次访问 `Instance` 会重新创建。 |

构造函数缺失、构造函数内部异常或 `OnSingletonInit` 抛异常时，不会缓存半初始化实例。构造函数依赖外部服务时，应改用 Architecture 显式注册，而不是通过反射单例隐藏依赖。

### `Singleton<T>` 与 `ISingleton`

| API | 说明 |
|---|---|
| `Singleton<T>.Instance` | `SingletonKit<T>.Instance` 的继承式入口。 |
| `Singleton<T>.Dispose()` | 清除当前类型实例。 |
| `Singleton<T>.OnSingletonInit()` | 可覆写的初始化回调，默认不执行操作。 |
| `ISingleton.OnSingletonInit()` | 非继承式单例必须实现的初始化契约。 |

`Dispose()` 只清除框架缓存和诊断存活标记，不会自动调用额外的 `OnDispose`。业务有文件、线程或订阅等外部资源时，必须提供显式释放方法并由 owner 在 `Dispose()` 前调用。

### 宿主单例

| 类型 | 适用边界 | 说明 |
|---|---|---|
| `MonoSingleton<T>` | Unity `GameObject`、`Transform` 和场景生命周期 | 由 Unity Adapter 查找或创建场景对象。 |
| `GodotSingleton<T>` | Godot `Node` 生命周期 | 由 Godot Adapter 管理节点接入。 |

宿主单例不是 `SingletonKit<T>` 的别名。Unity/Godot 类型不应泄漏到 Core Runtime 的纯 C# 服务中。

## 生命周期与错误边界

- `Instance` 是懒创建入口；仅读取 `HasInstance` 或 `TryGetInstance` 不会创建实例。
- `Dispose()` 后旧实例仍可能被业务变量引用，框架不会替换这些外部引用；owner 必须停止继续使用旧对象。
- 单例只保证每个闭合泛型类型一份实例，不解决跨类型初始化顺序。
- 需要替换实现或表达显式依赖关系时，优先使用 Architecture 或普通构造函数注入。

## 在工具中查看

SingletonKit 没有独立 Workbench 页面或 CLI 操作。需要确认项目连接状态时，使用 Workbench 的“框架”页；单例的创建和释放仍由业务 owner 负责。

## 限制与相关资料

- SingletonKit 不创建或管理 Unity `GameObject`、`Transform`、Godot `Node` 等宿主对象
- 没有独立的 Workbench 页面；需要观察单例时请在业务代码中提供自己的诊断入口
- 复杂服务组合优先使用 [Architecture](../01-Architecture/Architecture.md)
