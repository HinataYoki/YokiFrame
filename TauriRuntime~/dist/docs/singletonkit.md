# SingletonKit 单例

SingletonKit 提供纯 C# 单例门面，也配合 Unity / Godot 运行时适配器提供引擎生命周期单例。核心规则是：没有引擎依赖的服务使用 Base 单例，需要宿主对象生命周期时才使用对应 Adapter 单例。

## 选择哪种单例

| 类型 | 适合场景 | 命名空间 |
|------|----------|----------|
| `SingletonKit<T>` | 不想继承基类的纯 C# 服务 | `YokiFrame` |
| `Singleton<T>` | 可继承的纯 C# 服务 | `YokiFrame` |
| `MonoSingleton<T>` | 需要 `GameObject`、`Transform`、Unity 生命周期 | `YokiFrame.Unity` |
| `GodotSingleton<T>` | 需要 Godot `Node` 生命周期 | `YokiFrame.Godot` |

## SingletonKit<T>

`SingletonKit<T>` 要求 `T : class, ISingleton`。实例通过反射创建，因此可以使用私有无参构造函数。

```csharp
using YokiFrame;

public sealed class ConfigService : ISingleton
{
    private ConfigService()
    {
    }

    public static ConfigService Instance => SingletonKit<ConfigService>.Instance;

    public int MaxLevel { get; private set; }

    public void OnSingletonInit()
    {
        MaxLevel = 10;
    }
}

ConfigService.Instance.ToString();
SingletonKit<ConfigService>.Dispose();
```

行为细节：

- 第一次访问 `Instance` 时创建实例。
- 创建完成后调用 `OnSingletonInit()`。
- 缺少无参构造函数会抛出 `InvalidOperationException`。
- `Dispose()` 会清除当前缓存引用；下一次访问会重新创建。
- IL2CPP / AOT 项目需要显式引用或 Preserve 相关类型，避免被裁剪。

## Singleton<T>

如果你愿意继承框架基类，可以使用 `Singleton<T>`。

```csharp
using YokiFrame;

public sealed class AudioConfig : Singleton<AudioConfig>
{
    public float Volume = 1f;

    public override void OnSingletonInit()
    {
        Volume = 0.8f;
    }
}

AudioConfig.Instance.Volume = 0.5f;
AudioConfig.Dispose();
```

`Singleton<T>` 内部仍然由 `SingletonKit<T>` 管理。

## Unity MonoSingleton

需要 Unity 对象生命周期时使用 `MonoSingleton<T>`。

```csharp
using UnityEngine;
using YokiFrame.Unity;

public sealed class AudioRoot : MonoSingleton<AudioRoot>
{
    public override void OnSingletonInit()
    {
        DontDestroyOnLoad(gameObject);
    }
}

AudioRoot.Instance.ToString();
AudioRoot.Dispose();
```

行为细节：

- `Instance` 会先查找场景中已有的 `T`。
- 找不到时自动创建名为 `typeof(T).Name` 的 `GameObject` 并添加组件。
- `Dispose()` 会销毁关联 `GameObject` 并清除实例引用。
- `OnDestroy()` 会清除实例引用。

只需要普通全局服务时，不要使用 `MonoSingleton<T>`；直接用纯 C# 单例即可。

## Godot Singleton

Godot 运行时使用 `GodotSingleton<T>`。

```csharp
using YokiFrame.Godot;

public partial class AudioRoot : GodotSingleton<AudioRoot>
{
    public override void OnSingletonInit()
    {
    }
}

AudioRoot.Instance.ToString();
AudioRoot.Dispose();
```

当前 Godot 行为：

- `T` 约束为 `where T : GodotSingleton<T>, new()`。
- `Instance` 不存在时会创建节点，并尝试挂到当前 `SceneTree.Root`。
- `_EnterTree()` 会注册实例。
- `_ExitTree()` 会清除实例引用。
- 如果已有实例，再注册另一个不同实例会释放后者。

## 常见问题

| 问题 | 处理方式 |
|------|----------|
| 纯 C# 单例创建失败 | 确认类型实现 `ISingleton`，并提供无参构造函数。 |
| IL2CPP 下无法创建 | 显式引用类型，或使用 Preserve 防止构造函数被裁剪。 |
| Unity 中出现两个单例对象 | 检查场景中手动挂载对象和自动创建对象是否并存。 |
| 只需要配置服务却用了 `MonoSingleton` | 改为 `Singleton<T>` 或 `SingletonKit<T>`，减少不必要的宿主依赖。 |
