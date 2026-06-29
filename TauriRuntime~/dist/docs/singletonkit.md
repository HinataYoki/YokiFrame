# SingletonKit 单例

## 选择入口

| 类型 | 场景 | 命名空间 |
|---|---|---|
| `SingletonKit<T>` | 不想继承基类的纯 C# 服务 | `YokiFrame` |
| `Singleton<T>` | 可继承的纯 C# 服务 | `YokiFrame` |
| `MonoSingleton<T>` | 需要 `GameObject`、`Transform`、Unity 生命周期 | `YokiFrame.Unity` |
| `GodotSingleton<T>` | 需要 Godot `Node` 生命周期 | `YokiFrame.Godot` |

## 纯 C# 单例

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

继承式写法：

```csharp
using YokiFrame;

public sealed class AudioConfig : Singleton<AudioConfig>
{
    public float Volume;

    public override void OnSingletonInit()
    {
        Volume = 0.8f;
    }
}
```

## Unity 单例

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
```

`MonoSingleton<T>.Instance` 会先找场景中已有对象，找不到才创建新的 `GameObject`。

## Godot 单例

```csharp
using YokiFrame.Godot;

public partial class AudioRoot : GodotSingleton<AudioRoot>
{
    public override void OnSingletonInit()
    {
    }
}
```

`GodotSingleton<T>` 需要 Godot `Node` 生命周期。普通配置或服务仍用纯 C# 单例。

## 工作台诊断

SingletonKit 页面用于查看已登记单例、后端来源、存活状态和释放状态。

| 在工作台里看什么 | 用途 |
|---|---|
| 单例列表 | 确认目标单例是否已经创建。 |
| 类型 / 来源 | 区分纯 C#、Unity MonoSingleton、GodotSingleton。 |
| Alive / Disposed | 判断生命周期是否已经结束。 |
| 创建信息 | 排查重复创建或初始化顺序问题。 |

列表为空只说明当前没有实例登记，不代表项目里没有单例类型。

## 常见坑

| 问题 | 处理方式 |
|---|---|
| 纯 C# 单例创建失败 | 确认实现 `ISingleton` 并有无参构造函数。 |
| IL2CPP 下无法反射创建 | 显式引用或 Preserve 构造函数。 |
| Unity 出现两个对象 | 检查场景手动挂载对象和自动创建对象是否并存。 |
| 配置服务用了 `MonoSingleton` | 改成 `Singleton<T>` 或 `SingletonKit<T>`。 |
