# PoolKit 对象池

## 适用场景

PoolKit 是 YokiFrame 唯一的对象池入口。对象需要高频创建和销毁、且可以明确重置状态时使用它。局部池适合一个系统独占；`PoolKit.Shared` 适合按类型注册的全局共享池。池只管理引用类型，不负责 Unity `GameObject` 的场景归属。

## 使用前提

PoolKit 只管理普通 C# 引用类型，不负责 Unity `GameObject` 或 Godot `Node` 的场景归属。Workbench 的借出对象和泄漏检查只是诊断线索，不是自动修复。

## 快速上手

```csharp
using YokiFrame;

public sealed class Bullet
{
    public int Damage;
    public void Reset() { Damage = 0; }
}

ObjectPool<Bullet> pool = PoolKit.Create<Bullet>(
    static () => new Bullet(),
    onRecycled: static bullet => bullet.Reset(),
    options: new PoolOptions(initialCount: 8, maxRetained: 32));

Bullet bullet = pool.Allocate();
bullet.Damage = 10;
pool.Recycle(bullet);
pool.Dispose();
```

如果类型本身可控，推荐实现 `IPoolable` 并使用约定重载。

## 核心 API

### `PoolKit`、`ObjectPool<T>` 和 `PoolOptions`

`PoolOptions` 是不可变 `readonly struct`。显式创建容量配置不会产生托管堆分配；省略 `options`、`default(PoolOptions)` 与 `new PoolOptions()` 都表示零预热、无限缓存。

| API | 说明 |
|---|---|
| `PoolKit.Create<T>(Func<T>, Action<T>, Action<T>, PoolOptions options = default)` | 创建调用方独占池；factory 不能返回 `null`。显式委托不会自动绑定 `IPoolable`。 |
| `PoolKit.Create<T>(PoolOptions options = default)` | 创建 `T : class, IPoolable, new()` 的标准池，并绑定 `OnAllocated`/`OnRecycled`。 |
| `PoolKit.Shared` | 全局 `SharedPoolRegistry`。 |
| `ObjectPool<T>.CurCount` | 当前缓存对象数，不包含借出对象。 |
| `ObjectPool<T>.Allocate()` | 从缓存取对象，缓存为空时调用 factory，再执行借出回调。 |
| `ObjectPool<T>.Recycle(T obj)` | 回收对象；进入缓存返回 `true`，null、重复回收或容量已满返回 `false`。 |
| `ObjectPool<T>.Clear()` | 释放缓存对象，池仍可继续使用。 |
| `ObjectPool<T>.Dispose()` | 释放缓存并注销池；之后 `Allocate`/`Recycle` 会抛 `ObjectDisposedException`。 |
| `readonly struct PoolOptions(int initialCount = 0, int maxRetained = -1)` | 零分配容量配置；负预热或预热超过有限上限会被拒绝。 |
| `PoolOptions.InitialCount` / `MaxRetained` | 读取预热数和最大缓存数。 |
| `PoolOptions.UNBOUNDED` / `PoolOptions.Default` | `-1` 表示不限制缓存；默认不预热且不限制。 |

`Recycle` 在容量已满时仍会先执行 `onRecycled`，然后不缓存对象；如果对象实现 `IDisposable`，离开缓存时会调用 `Dispose()`。null、重复回收和已释放池是不同情况：前两者返回 `false`，已释放池抛 `ObjectDisposedException`。

### 配置值语义

`initialCount` 不能为负，有限的 `maxRetained` 不能小于预热数。`default(PoolOptions)` 表示“不预热且不限制缓存”；如果希望对象归还后立即释放，请显式使用 `maxRetained: 0`。

### `IPool<T>` 与 `IPoolable`

| 类型/API | 说明 |
|---|---|
| `IPool<T>.Allocate()` / `Recycle(T)` | 最小分配和回收契约。 |
| `IPoolable.OnAllocated()` | 对象借出后恢复可用状态。 |
| `IPoolable.OnRecycled()` | 对象归还前清理业务引用。 |

生命周期回调必须可重复执行。建议在 `OnRecycled` 清理订阅、外部引用、临时列表和上一次请求状态。

### `SharedPoolRegistry`

| API | 说明 |
|---|---|
| `Count` | 已注册共享池数量。 |
| `Register<T>(Func<T>, Action<T>, Action<T>, PoolOptions options = default)` | 注册普通引用类型共享池。 |
| `Register<T>(PoolOptions options = default)` | 注册 `IPoolable, new()` 类型共享池。 |
| `Get<T>()` | 获取已注册池；未注册抛 `InvalidOperationException`。 |
| `TryGet<T>(out ObjectPool<T>)` | 查询而不抛异常。 |
| `Remove<T>()` | 移除并释放指定类型池，返回是否找到。 |
| `Clear()` | 移除并释放全部共享池。 |

同一类型只能注册一个共享池。重复注册会释放新建池并抛出 `InvalidOperationException`。共享池的注册 owner 应固定在初始化阶段；模块结束时由同一 owner 调用 `Remove` 或 `Clear`。

## 生命周期与错误边界

- 池的 owner 必须负责 `Dispose`；全局共享池由注册表 owner 负责 `Remove` 或 `Clear`。
- 不要把同一对象同时交给两个池；池使用引用相等判断重复回收。
- 工厂返回 null、生命周期回调抛异常或池已释放都会中断当前操作，不应在业务层静默吞掉异常。
- Unity 对象的 `Destroy`、场景归属和组件生命周期仍由宿主 Adapter 或业务 owner 管理。

## 在工具中查看

Workbench 可以查看池压力、借出对象和事件历史。泄漏检查只是一条排查线索，不是内存泄漏定论。

## 限制与相关资料

- PoolKit 只管理引用类型；Unity `GameObject` 的创建、销毁和场景归属不由它接管
- 追踪和堆栈只用于短时排查，结束后应关闭
