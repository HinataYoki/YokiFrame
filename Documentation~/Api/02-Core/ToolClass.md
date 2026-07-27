# ToolClass 通用类型

## 适用场景

ToolClass 提供跨 Unity、Godot 与普通 .NET 环境复用的纯 C# 基础类型。它们不是独立 Kit，也没有 Workbench 页面。

## 使用前提

ToolClass 是随 Core 提供的低依赖基础类型，不是独立 Kit，也没有 Workbench 页面。只有在需要绑定值、低分配集合或 Span 文本切分时才直接使用它。

## 接入方式

这些类型按需直接使用，不需要初始化入口或 Workbench 配置。优先让创建它们的业务 owner 持有实例，并在 owner 结束时释放注销令牌、移除节点或丢弃集合。

## 核心 API

### BindValue

```csharp
BindValue<int> health = new(100);
LinkUnRegister<int> token = health.BindWithCallback(OnHealthChanged);

health.Value = 80;
token.UnRegister();
```

- `Value` 只在比较结果不相等时触发回调。
- `Bind` 返回 EventKit 注销令牌；`BindWithCallback` 注册后立即回放当前值。
- `SetValueWithoutEvent` 只更新值，适合反序列化或批量同步。
- `SetCompareFunc` 按闭合泛型类型替换默认比较函数，不是单实例设置。
- 构造重载 `BindValue(T value, Func<T, T, bool> compareFunc)` 设置仅作用于当前实例的比较函数，优先于 `SetCompareFunc` 的默认值。

### FastDictionary

`FastDictionary<TKey,TValue>` 使用线性探测开放寻址，提供索引器、`Add`、`TryAdd`、`TryGetValue`、`GetValueOrDefault`、`GetOrAdd`、`ContainsKey`、`Remove`、`Clear` 和 `ForEach`。

- 构造容量按预计元素峰值设置；字典不提供线程同步，读写由同一 owner 或外部锁管理。
- `ForEach` 和枚举期间不要修改字典。

### PooledLinkedList

```csharp
PooledLinkedList<Action> listeners = new(maxPoolSize: 64);
listeners.Prewarm(32);

PooledLinkedListNode<Action> lease = listeners.AddLast(OnEvent);
listeners.Remove(lease); // 租约立即失效，底层节点进入空闲链。
```

- `Prewarm` 可在进入高频路径前准备节点；`MaxPoolSize` 控制最多保留数量。
- `Remove`、`Clear` 和 `RemoveAll` 会使对应节点租约失效；修改和枚举不能并发进行。

### SpanSplitter

`SpanSplitter` 按单字符逐段返回 `ReadOnlySpan<char>`，不创建中间字符串数组。默认 `StringSplitOptions.None` 会一致保留开头、中间、末尾和唯一空片段；传入 `RemoveEmptyEntries` 会跳过全部空片段。

```csharp
foreach (ReadOnlySpan<char> segment in new SpanSplitter(source, ',', StringSplitOptions.RemoveEmptyEntries))
{
    Parse(segment);
}
```

它是 `ref struct`，直接 `foreach` 不装箱，只能在栈约束允许的同步作用域内使用，不能跨 `await` 保存。

## 生命周期与错误边界

- `BindValue` 返回的注销令牌由创建绑定的 owner 负责调用 `UnRegister()`。
- `PooledLinkedListNode<T>` 在 `Remove`、`Clear` 或 `RemoveAll` 后失效，不要继续保存或复用。
- `FastDictionary` 和 `PooledLinkedList` 不提供线程同步；并发访问由业务 owner 负责协调。
- `SpanSplitter` 只能在同步栈作用域内消费，不能把 `ReadOnlySpan<char>` 保存到异步流程或堆对象中。

## 在工具中查看

ToolClass 没有独立 Workbench 页面；这些类型的行为直接体现在调用它们的 Runtime 或工具代码中。

## 限制与相关资料

ToolClass 不负责对象池、事件总线或跨线程调度。需要注销规则时参阅 [EventKit](EventKit.md)，需要可复用对象生命周期时参阅 [PoolKit](PoolKit.md)。
