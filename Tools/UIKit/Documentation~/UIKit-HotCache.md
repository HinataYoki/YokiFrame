# UIKit 热度缓存系统

> 最后更新: 2026-03-24

## 概述

UIKit 使用**热度（Hot）机制**管理面板的生命周期缓存。当面板关闭时，不一定立即销毁，而是根据热度值和缓存模式决定是否保留 GameObject，以便再次打开时跳过加载流程，提升响应速度。

## 核心概念

### 热度值（Hot）

每个面板的 `PanelHandler` 持有一个 `int Hot` 字段，代表该面板的"活跃程度"：

| 行为 | 热度变化 | 默认值 |
|------|----------|--------|
| 打开面板（`OpenPanel`） | `+OpenHot` | +3 |
| 获取面板（`GetPanel`） | `+GetHot` | +2 |
| 每秒自动衰减 | `-Weaken` | -1 |

当面板关闭后，若热度 > 0，面板 GameObject 保留在场景中（SetActive(false)）。热度持续衰减至 0 时，面板被真正销毁并释放资源。

### 缓存模式（PanelCacheMode）

每个面板可通过 `PanelHandler.CacheMode` 设置缓存策略：

| 模式 | 说明 | 关闭行为 |
|------|------|----------|
| `Hot`（默认） | 热度驱动 | 热度 > 0 保留，= 0 销毁 |
| `Persistent` | 常驻 | 永不自动销毁 |
| `Temporary` | 临时 | 关闭后立即销毁 |

### 衰减机制

`UIRoot` 内部通过 `UpdateHotWeaken(float deltaTime)` 每隔 `WEAKEN_INTERVAL`（1 秒）执行一次全局热度衰减：

1. 遍历 `mOpenedCache` 中所有 Handler
2. `handler.Hot -= Weaken`
3. 若 `Hot <= 0` 且面板状态为 `PanelState.Close`：
   - `Persistent` 模式跳过
   - 其他模式执行 `DestroyPanelInternal` 并从缓存移除

### 缓存命中

当调用 `OpenPanel` 时，若缓存中已有该类型面板（热度未衰减完）：

- 更新 `Data`、`Hot`
- 应用新的 `Level` 和 `Tag`（若与缓存值不同，会移动到新层级并更新 Tag 索引）
- 调用 `Open` + `Show` 重新激活

## API 参考

### 全局开关

```csharp
// 是否启用热度缓存机制（默认 true）
// 关闭后，Hot 模式的面板在关闭时立即销毁，不做热度保留
// Persistent 和 Temporary 模式不受影响
UIKit.HotCacheEnabled = false;

// 也可通过 Inspector 配置 UIRootConfig.EnableHotCache
```

**关闭后的行为变化：**

| 场景 | 启用（默认） | 关闭 |
|------|-------------|------|
| Hot 模式面板关闭 | 热度 > 0 时保留 | 立即销毁 |
| WeakenAllHot 调用 | 执行衰减和淘汰 | 跳过，不执行 |
| UpdateHotWeaken 定时器 | 正常计时衰减 | 跳过，不执行 |
| Persistent 模式 | 不受影响 | 不受影响 |
| Temporary 模式 | 不受影响 | 不受影响 |

### 热度配置（UIKit 静态属性）

```csharp
// 创建/打开面板时赋予的热度增量（默认 3）
UIKit.OpenHot = 5;

// 获取面板时赋予的热度增量（默认 2）
UIKit.GetHot = 3;

// 每次衰减的热度值（默认 1）
UIKit.Weaken = 1;
```

### 缓存容量

```csharp
// 获取/设置预加载缓存容量（默认 10）
UIKit.SetCacheCapacity(20);
int capacity = UIKit.GetCacheCapacity();
```

### 缓存查询

```csharp
// 检查面板是否已缓存（已打开或预加载）
bool cached = UIKit.IsPanelCached<MyPanel>();
bool cached = UIKit.IsPanelCached(typeof(MyPanel));

// 获取所有已缓存的面板类型
IReadOnlyCollection<Type> types = UIKit.GetCachedPanelTypes();

// 获取所有已缓存的面板实例
IReadOnlyList<IPanel> panels = UIKit.GetCachedPanels();
```

### 预加载

```csharp
// 异步预加载面板（不显示，缓存备用）
UIKit.PreloadPanelAsync<MyPanel>(UILevel.Common, success => {
    Debug.Log($"预加载结果: {success}");
});

// UniTask 版本
bool ok = await UIKit.PreloadPanelUniTaskAsync<MyPanel>(UILevel.Common, ct);

// 清理指定预加载面板
UIKit.ClearPreloadedCache<MyPanel>();

// 清理所有预加载面板
UIKit.ClearAllPreloadedCache();
```

### UIRootConfig 配置项

在 UIRoot 的 Inspector 面板中可直接配置：

| 字段 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `EnableHotCache` | bool | true | 是否启用热度缓存机制 |
| `CacheCapacity` | int | 10 | 预加载缓存上限（LRU 淘汰） |
| `OpenHot` | int | 3 | 打开面板增加的热度 |
| `GetHot` | int | 2 | 获取面板增加的热度 |
| `Weaken` | int | 1 | 每秒衰减的热度 |

## 生命周期流程

```
首次 OpenPanel<T>()
  → LoadPanel → Init → AddToOpenedCache → RegisterToLevel
  → handler.Hot += OpenHot  (Hot = 3)
  → Open → Show

ClosePanel<T>()
  → Close → Hide → SetActive(false)
  → Hot > 0 → 保留在 mOpenedCache 中（PanelState.Close）

热度衰减（每秒）
  → Hot -= Weaken
  → Hot = 2 → 1 → 0
  → Hot <= 0 && State == Close → DestroyPanelInternal → Recycle

再次 OpenPanel<T>()（Hot > 0 时）
  → TryGetCachedHandler 命中
  → 更新 Data / Hot / Level / Tag
  → SetActive(true) → Open → Show
```

## 注意事项

- 热度衰减是全局的：每次 `OpenPanel` 或定时器触发时都会执行 `WeakenAllHot`
- 缓存命中时会应用新的 `Level` 和 `Tag` 参数，确保面板显示在正确的层级
- `Persistent` 模式的面板不受热度衰减影响，不会被自动淘汰
- 预加载缓存独立管理，使用 LRU 淘汰策略，容量由 `CacheCapacity` 控制
