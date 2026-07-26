# ToolClass

> 面向读者：需要复用低依赖基础类型的 Runtime 开发者
>
> 主要入口：`BindValue<T>`、`FastDictionary<TKey, TValue>`、`PooledLinkedList<T>`、`SpanSplitter`
>
> 运行边界：跨宿主 Runtime；不属于独立 Kit
>
> 状态来源：当前 `Core/Runtime/ToolClass` 源码

## 适用场景

ToolClass 提供跨 Unity、Godot 与普通 .NET 环境复用的纯 C# 基础类型。它们随 `YokiFrame` Core 主程序集进入 Runtime，不是独立 Kit，不发布 Interaction、CLI action 或 Workbench 页面。

## 入口与当前状态

| 分类 | 类型 | 位置 |
|---|---|---|
| Bindable | `IBindable<T>`、`BindValue<T>`、`BindableExtensions` | `Core/Runtime/ToolClass/Bindable` |
| Collections | `FastDictionary<TKey,TValue>` | `Core/Runtime/ToolClass/Collections` |
| Collections | `PooledLinkedList<T>`、`PooledLinkedListNode<T>` | `Core/Runtime/ToolClass/Collections` |
| Parsing | `SpanSplitter` | `Core/Runtime/ToolClass/Collections` |

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

- 构造容量表示预计元素峰值，底层会按 `0.75` 负载因子换算槽位，达到峰值前不会扩容。
- 删除使用墓碑槽，后续写入会优先复用。
- 值版 `GetOrAdd` 只执行一次哈希和探测；扩容复用槽位缓存哈希。
- `Clear` 会清除哈希、键和值，长生命周期字典不会继续持有已经清空的对象。
- 直接 `foreach` 使用结构体枚举器且不分配；通过 `IEnumerable` 接口枚举会装箱。
- `ForEach` 和枚举期间修改字典会抛出 `InvalidOperationException`。
- 字典不提供线程同步，读写由同一 owner 或外部锁管理。

### PooledLinkedList

```csharp
PooledLinkedList<Action> listeners = new(maxPoolSize: 64);
listeners.Prewarm(32);

PooledLinkedListNode<Action> lease = listeners.AddLast(OnEvent);
listeners.Remove(lease); // 租约立即失效，底层节点进入空闲链。
```

- 链表使用自有侵入式双向节点和单向空闲链，不再为每个实例创建 `LinkedList`、`Stack` 或 generation 字典。
- `Prewarm` 在进入热路径前创建节点；`MaxPoolSize` 控制最多保留数量。
- 缩小 `MaxPoolSize` 会立即裁剪，无需额外调用 `TrimPool`。
- `Remove`、`RemoveFirst`、`RemoveLast`、`Clear` 和 `RemoveAll` 都会先清空节点值再回池。
- `TrimPool` 与 `ClearPool` 只处理已回收节点，不修改活动链表。
- `PooledLinkedListNode<T>` 是包含 owner 与 generation 的值类型租约；过期副本的 `IsValid` 为 false，不能读取、修改或移除复用后的新节点。
- 直接 `foreach` 使用结构体枚举器且不分配；通过 `IEnumerable<T>` 接口枚举会装箱。
- 链表不提供线程同步；修改和枚举不能并发进行。

EventKit 的 `EasyEvent` 与 `EasyEvent<T>` 使用该链表保存监听器。稳定注册/注销峰值后，侵入式节点在空闲链中复用，不再为每次重新注册创建节点对象或查询 generation 字典。

### SpanSplitter

`SpanSplitter` 按单字符逐段返回 `ReadOnlySpan<char>`，不创建中间字符串数组。默认 `StringSplitOptions.None` 会一致保留开头、中间、末尾和唯一空片段；传入 `RemoveEmptyEntries` 会跳过全部空片段。

```csharp
foreach (ReadOnlySpan<char> segment in new SpanSplitter(source, ',', StringSplitOptions.RemoveEmptyEntries))
{
    Parse(segment);
}
```

它是 `ref struct`，直接 `foreach` 不装箱，只能在栈约束允许的同步作用域内使用，不能跨 `await` 保存。
