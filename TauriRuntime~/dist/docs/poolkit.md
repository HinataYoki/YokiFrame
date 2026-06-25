# PoolKit 对象池

PoolKit 用于复用高频创建的对象和短生命周期集合。核心入口位于 `YokiFrame`，不依赖 Unity 对象池。

## 选择哪种池

| 入口 | 适合场景 | 特点 |
|------|----------|------|
| `SimplePoolKit<T>` | 局部池、普通 C# 对象、自定义 factory/reset | 不要求接口；当前不防重复回收。 |
| `SafePoolKit<T>` | 全局共享、实现 `IPoolable` 的对象 | 防 `null` 和重复回收；支持最大缓存数量。 |
| `Pool.List<T>` / `Pool.Dictionary<TKey,TValue>` / `Pool.Set<T>` | 短作用域临时集合 | action 正常返回后自动 `Clear()` 并归还。 |
| `ListPool<T>` / `DictPool<TKey,TValue>` / `SetPool<T>` | 集合需要跨方法持有 | 手动 `Get()` / `Release()`，建议配合 `try/finally`。 |

## SimplePoolKit

```csharp
using YokiFrame;

public sealed class BulletToken
{
    public int Damage;

    public void Reset()
    {
        Damage = 0;
    }
}

private readonly SimplePoolKit<BulletToken> mBulletPool =
    new SimplePoolKit<BulletToken>(
        factoryMethod: () => new BulletToken(),
        resetMethod: token => token.Reset(),
        initCount: 16);

var token = mBulletPool.Allocate();
token.Damage = 10;
mBulletPool.Recycle(token);
```

行为细节：

- `initCount` 会预创建对象并放入池中。
- `Allocate()` 在池空时调用 factory 创建新对象。
- `Recycle()` 会先调用 `resetMethod`，再把对象压回池。
- 当前 `SimplePoolKit<T>` 不检查重复回收。同一个对象回收两次会被压入两次。
- `CurCount` 属性返回当前池中可用对象数量。

`PoolKit<T>` 基类还提供 `SetObjectFactory(IObjectFactory<T>)` 方法，适合需要更复杂创建逻辑的场景：

```csharp
pool.SetObjectFactory(new CustomObjectFactory());
```

## SafePoolKit

`SafePoolKit<T>` 要求 `T : IPoolable, new()`。

```csharp
using YokiFrame;

public sealed class DamageTextToken : IPoolable
{
    public bool IsRecycled { get; set; }
    public int Value;

    public void OnRecycled()
    {
        Value = 0;
    }
}

SafePoolKit<DamageTextToken>.Instance.Init(initCount: 8, maxCount: 32);

var token = SafePoolKit<DamageTextToken>.Instance.Allocate();
token.Value = 100;

bool recycled = SafePoolKit<DamageTextToken>.Instance.Recycle(token);
```

行为细节：

- `Allocate()` 会把 `IsRecycled` 设为 `false`。
- `Recycle()` 遇到 `null` 或 `IsRecycled == true` 会返回 `false`。
- 回收时会调用 `OnRecycled()`。
- 超过 `MaxCacheCount` 时仍会调用 `OnRecycled()`，但对象不会进入缓存池。
- `SafePoolKit<T>.Instance` 由 `SingletonKit<SafePoolKit<T>>` 创建。

## 集合池

短作用域推荐 action 版本。它会在 action 正常返回后清空并归还集合；如果 action 可能抛异常，使用手动版本配合 `try/finally`。

```csharp
Pool.List<int>(list =>
{
    list.Add(1);
    list.Add(2);
    Use(list);
});

Pool.Dictionary<string, int>(map =>
{
    map["score"] = 100;
});

Pool.Set<int>(set =>
{
    set.Add(1);
});
```

集合需要跨方法持有时，使用手动版本：

```csharp
var list = ListPool<int>.Get();
try
{
    list.Add(1);
    list.Add(2);
    Use(list);
}
finally
{
    ListPool<int>.Release(list);
}
```

`Release()` 会先 `Clear()` 集合，再放回池。

## 运行时跟踪开关

PoolKit 默认不记录活跃对象和堆栈。需要排查回收问题时，可以临时打开 `PoolDebugger`。

```csharp
PoolDebugger.EnableTracking = true;
PoolDebugger.EnableEventHistory = true;
PoolDebugger.EnableStackTrace = true;
```

| 开关 | 影响 |
|------|------|
| `EnableTracking` | 记录 active / inactive 对象、数量和峰值。 |
| `EnableEventHistory` | 记录最多 `PoolDebugger.MAX_EVENT_HISTORY` 条 Spawn / Return / Forced 事件。 |
| `EnableStackTrace` | 记录借出位置和堆栈，成本最高，只建议短时间开启。 |

可读取的数据：

```csharp
var pools = new List<PoolDebugInfo>();
PoolDebugger.GetAllPools(pools);

var events = new List<PoolEvent>();
PoolDebugger.GetEventHistory(events);
```

跟踪只影响开启之后的分配和回收，已经借出的对象不会补录旧堆栈。

## 常见问题

| 问题 | 处理方式 |
|------|----------|
| `SimplePoolKit` 数量异常 | 检查是否重复 `Recycle()` 同一个对象。 |
| `SafePoolKit.Recycle()` 返回 `false` | 对象是 `null`、已回收，或缓存已超过 `MaxCacheCount`。 |
| 临时集合数据串了 | 确认没有在 action 结束后继续持有集合引用。 |
| 打开堆栈后性能下降 | `EnableStackTrace` 会采集堆栈，只在定位问题时短时间开启。 |
