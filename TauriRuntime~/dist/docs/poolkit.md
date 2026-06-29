# PoolKit 对象池

## 选择入口

| 入口 | 适合场景 | 注意 |
|---|---|---|
| `SimplePoolKit<T>` | 局部池、普通 C# 对象 | 不防重复回收。 |
| `SafePoolKit<T>` | 全局共享、实现 `IPoolable` 的对象 | 防 `null` 和重复回收。 |
| `Pool.List<T>` / `Pool.Dictionary<TKey,TValue>` / `Pool.Set<T>` | 短作用域临时集合 | action 结束后不要再持有引用。 |
| `ListPool<T>` / `DictPool<TKey,TValue>` / `SetPool<T>` | 集合需要跨方法持有 | 用 `try/finally` 手动释放。 |

## 普通对象池

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

var pool = new SimplePoolKit<BulletToken>(
    factoryMethod: () => new BulletToken(),
    resetMethod: token => token.Reset(),
    initCount: 16);

var token = pool.Allocate();
token.Damage = 10;
pool.Recycle(token);
```

`SimplePoolKit<T>` 适合所有权清楚的局部池。它当前不会检查同一对象是否被回收两次。

## 安全池

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
SafePoolKit<DamageTextToken>.Instance.Recycle(token);
```

`Recycle()` 遇到 `null` 或 `IsRecycled == true` 会返回 `false`。

## 集合池

短作用域：

```csharp
Pool.List<int>(list =>
{
    list.Add(1);
    Use(list);
});
```

跨方法持有：

```csharp
var list = ListPool<int>.Get();
try
{
    list.Add(1);
    Use(list);
}
finally
{
    ListPool<int>.Release(list);
}
```

`Release()` 会先 `Clear()` 集合，再放回池。

## 工作台诊断

PoolKit 页面用于观察池容量、借出对象、回收数量、峰值和疑似泄漏。

| 在工作台里看什么 | 用途 |
|---|---|
| 池列表 | 找到具体对象池，确认容量和活跃数量。 |
| Borrowed / Active 数量 | 判断是否有对象长期未归还。 |
| 峰值 | 判断初始容量是否太小、运行时是否频繁扩容。 |
| 事件历史 | 查看 Spawn、Return、Forced 回收顺序。 |
| 泄漏候选 | 快速定位仍借出的对象。 |

需要定位对象未回收时临时开启：

```csharp
PoolDebugger.EnableTracking = true;
PoolDebugger.EnableEventHistory = true;
PoolDebugger.EnableStackTrace = true;
```

| 开关 | 成本 |
|---|---|
| `EnableTracking` | 记录 active / inactive 对象。 |
| `EnableEventHistory` | 记录 Spawn / Return / Forced 事件。 |
| `EnableStackTrace` | 记录堆栈，成本最高，只短时间开启。 |

堆栈记录成本最高，排查完及时关闭。

## 常见坑

| 问题 | 处理方式 |
|---|---|
| `SimplePoolKit` 数量异常 | 检查是否重复 `Recycle()` 同一对象。 |
| `SafePoolKit.Recycle()` 返回 `false` | 对象为 `null`、已回收，或超过最大缓存。 |
| 集合数据串了 | 不要在 action 结束后继续持有集合引用。 |
| 打开堆栈后卡顿 | `EnableStackTrace` 只在定位问题时短时间开启。 |
| `check_leak` 报疑似泄漏 | 它表示“当前仍借出”，不等同于真实内存泄漏。 |
