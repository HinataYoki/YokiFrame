# SpatialKit 空间索引

> 面向读者：需要维护动态实体索引并执行空间查询的 Runtime 开发者
>
> 主要入口：`SpatialKit`、`ISpatialIndex<T>`、`ISpatialEntity`
>
> 运行边界：跨宿主 Runtime；Provider 与 Unity Gizmo 位于独立 Editor 边界
>
> 状态来源：`Documentation~/Api/00-GettingStarted/Kit_Status.md`

## 适用场景

SpatialKit 是纯 C# 空间索引 Tool Kit，用于维护带稳定 Id 和位置的实体，并快速执行半径、包围盒、最近邻和批量空间查询。它提供三种数据结构：无固定边界的 HashGrid、固定二维边界的 Quadtree，以及固定三维边界的 Octree。

适合以下场景：

- 战斗、AI、触发器或感知系统需要反复查询附近实体。
- 大量动态实体主要在二维投影平面上移动。
- 关卡或房间有明确二维/三维边界，需要树结构分区。
- 业务需要把空间索引写入和跨线程查询分离。
- Editor/AI 需要读取当前索引数量、分区和边界等只读诊断信息。

SpatialKit 不负责实体生命周期、位置同步、线程调度、碰撞检测、物理模拟或路径规划。索引只保存实体引用和内部定位，不会替调用方驱动实体移动。

## 入口与当前状态

Runtime API、Tool Interaction、CLI 只读查询、SpatialKit 专用 Avalonia Workbench 页面和 Unity Scene Gizmo 均已实现，源码位于 `Tools/SpatialKit`。Runtime 诊断钩子在 Player 中由编辑器宏剔除，Unity/Godot Editor Provider 与 Unity Gizmo 位于独立 Editor 边界。

| 程序集 | 作用 | 编译边界 |
|---|---|---|
| `YokiFrame.SpatialKit` | `SpatialKit` 门面、索引、查询和 Snapshot | Runtime，可进入 Unity Player/Godot Runtime；Workbench 诊断代码不进入 Player |
| `YokiFrame.SpatialKit.Editor` | Tool Interaction Provider、命令处理和诊断 JSON | Unity Editor 或 Godot `TOOLS` |
| `YokiFrame.SpatialKit.Unity.Editor` | Unity Editor 自动安装 Provider，并提供 Scene View Gizmo | 仅 Unity Editor |

Runtime 只依赖 `YokiFrame` Core，不引用 `UnityEngine`、`UnityEditor` 或 Godot。实体位置使用 Core 的 `YokiVector3`，矩形和三维边界使用 `YokiRect`、`YokiBounds`，不直接使用宿主向量类型。

## 快速上手

### 定义实体并创建索引

```csharp
using System.Collections.Generic;
using YokiFrame;

public sealed class EnemySpatialEntity : ISpatialEntity
{
    public EnemySpatialEntity(int id, YokiVector3 position)
    {
        SpatialId = id;
        Position = position;
    }

    public int SpatialId { get; }
    public YokiVector3 Position { get; private set; }

    public void MoveTo(YokiVector3 position)
    {
        Position = position;
    }
}

var grid = SpatialKit.CreateHashGrid<EnemySpatialEntity>(
    cellSize: 2f,
    plane: SpatialPlane.XZ);

var enemy = new EnemySpatialEntity(1, new YokiVector3(0f, 0f, 3f));
grid.Insert(enemy);

var results = new List<EnemySpatialEntity>(16);
grid.QueryRadius(YokiVector3.Zero, 5f, results);
```

实体移动后调用 `Update(enemy)`；如果实体尚未插入，`Update` 会按插入处理。索引使用 `SpatialId` 维护实体定位，因此可安全支持位置可变的引用类型。相同索引内插入相同 Id 会替换旧实体，不会增加 `Count`。

### 选择索引

| 场景 | 工厂 | 距离与边界 |
|---|---|---|
| 大量动态实体、空间范围不固定 | `CreateHashGrid<T>` | XY/XZ 投影距离，无固定边界 |
| 固定二维区域、实体分布不均 | `CreateQuadtree<T>` | XY/XZ 投影距离，`YokiRect` 二维边界 |
| 固定三维区域、体积查询 | `CreateOctree<T>` | 完整三维距离，`YokiBounds` 三维边界 |

### Snapshot 与并行查询

```csharp
SpatialIndexSnapshot<EnemySpatialEntity> snapshot = grid.CreateSnapshot();

var queries = new[]
{
    new SpatialRadiusQuery(YokiVector3.Zero, 5f),
    new SpatialRadiusQuery(new YokiVector3(10f, 0f, 0f), 3f)
};
var batchResults = new List<List<EnemySpatialEntity>>
{
    new List<EnemySpatialEntity>(),
    new List<EnemySpatialEntity>()
};

snapshot.QueryRadiusBatchParallel(queries, batchResults);
```

创建 Snapshot 时不要并发修改索引。Snapshot 复制实体的捕获位置，但返回的仍是原实体引用；并行查询时每个结果列表只能由对应查询写入，不能让多个查询共享同一个 `List<T>`。

## 核心 API

### 距离和边界语义

### `SpatialPlane`

| 值 | 投影坐标 | 适用场景 |
|---|---|---|
| `XZ` | `X` 与 `Z` | 3D 地面、2.5D 场景，默认值 |
| `XY` | `X` 与 `Y` | 传统 2D 场景 |

HashGrid 和 Quadtree 的 `QueryRadius`、`QueryNearest` 使用构造时选择的投影平面，忽略另一轴的距离。Octree 的半径和最近邻查询始终使用完整三维距离。

三种索引的 `QueryBounds` 都使用完整 `YokiBounds.Contains` 判断实体当前位置；投影索引的分区只用于缩小候选范围，不会改变包围盒对未投影轴的判断。Quadtree 和 Octree 路由超出根边界的位置时会将分区坐标限制在根边界内，但查询仍按实体的实际位置判断。

### `SpatialKit` 工厂

`SpatialKit` 的公开门面只负责创建索引：

```csharp
SpatialHashGrid<T> CreateHashGrid<T>(
    float cellSize,
    SpatialPlane plane = SpatialPlane.XZ)
    where T : ISpatialEntity;

Quadtree<T> CreateQuadtree<T>(
    YokiRect bounds,
    int maxDepth = 8,
    int maxEntitiesPerNode = 8,
    SpatialPlane plane = SpatialPlane.XZ)
    where T : ISpatialEntity;

Octree<T> CreateOctree<T>(
    YokiBounds bounds,
    int maxDepth = 8,
    int maxEntitiesPerNode = 8)
    where T : ISpatialEntity;
```

对应构造函数也可直接使用。`cellSize` 必须是有限正数；树的边界必须是有限且尺寸为正的边界；`maxDepth` 和 `maxEntitiesPerNode` 必须大于零。位置、半径和距离不能使用 NaN 或负值。

### 核心契约

### `ISpatialEntity`

```csharp
public interface ISpatialEntity
{
    int SpatialId { get; }
    YokiVector3 Position { get; }
}
```

`SpatialId` 必须在同一个索引内稳定且唯一。`Position` 每次插入、更新和查询时都必须包含有限坐标；索引不会替调用方缓存移动前的位置。

### `ISpatialIndex<T>`

```csharp
public interface ISpatialIndex<T> where T : ISpatialEntity
```

| API | 说明 |
|---|---|
| `int Count` | 当前实体数量 |
| `void Insert(T entity)` | 插入实体；相同 Id 会替换旧实体 |
| `bool Remove(T entity)` | 按 `SpatialId` 移除实体；实体当前位置不参与定位 |
| `void Update(T entity)` | 更新实体位置；不存在时按插入处理 |
| `void UpdateBatch(IReadOnlyList<T> entities)` | 按输入顺序逐个更新 |
| `void QueryRadius(YokiVector3 center, float radius, List<T> results)` | 查询半径内实体并追加到结果列表 |
| `void QueryBounds(YokiBounds bounds, List<T> results)` | 查询完整三维包围盒内实体并追加结果 |
| `T QueryNearest(YokiVector3 position, float maxDistance = float.MaxValue, Func<T, bool> filter = null)` | 查询最近实体；无结果返回 `default` |
| `void Clear()` | 清空实体并恢复初始分区状态 |

查询 API 不会自动清空 `results`。`filter` 不匹配或索引为空时，`QueryNearest` 返回 `default(T)`；`float.MaxValue` 和正无穷表示不限制最大距离。

### 具体索引 API

### `SpatialHashGrid<T>`

```csharp
public sealed class SpatialHashGrid<T> :
    ISpatialIndex<T>,
    ISpatialSnapshotProvider<T>,
    ISpatialIndexDiagnostics
    where T : ISpatialEntity

SpatialHashGrid(float cellSize, SpatialPlane plane = SpatialPlane.XZ)
```

除 `ISpatialIndex<T>` 成员外，还公开：

| API | 说明 |
|---|---|
| `SpatialPlane Plane` | 获取二维投影平面 |
| `SpatialIndexSnapshot<T> CreateSnapshot()` | 捕获当前实体引用和位置，创建只读快照 |
| `string DiagnosticsId` | 获取进程内稳定诊断编号 |
| `string IndexKind` | 固定为 `HashGrid` |
| `string EntityTypeName` | 获取实体 CLR 完整类型名 |
| `string PlaneName` | 获取投影平面名称 |
| `float CellSize` | 获取网格尺寸 |
| `int MaxDepth` | 固定返回 `0`，HashGrid 不使用树深度 |
| `int MaxEntitiesPerNode` | 固定返回 `0`，HashGrid 不使用节点容量 |
| `int PartitionCount` | 当前非空网格分区数量 |
| `bool HasCellSize` | 固定为 `true` |
| `bool HasBounds2D` / `bool HasBounds3D` | 均固定为 `false` |
| `YokiRect Bounds2D` / `YokiBounds Bounds3D` | 无固定边界，返回默认值 |
| `string CreatedAtUtc` | 创建时间的 UTC ISO 文本 |

HashGrid 按投影平面和 `cellSize` 计算网格分区，适合范围动态变化的实体集合。

### `Quadtree<T>`

```csharp
public sealed class Quadtree<T> :
    ISpatialIndex<T>,
    ISpatialSnapshotProvider<T>,
    ISpatialIndexDiagnostics
    where T : ISpatialEntity

Quadtree(
    YokiRect bounds,
    int maxDepth = 8,
    int maxEntitiesPerNode = 8,
    SpatialPlane plane = SpatialPlane.XZ)
```

除统一索引成员外，还公开：

| API | 说明 |
|---|---|
| `SpatialPlane Plane` | 获取二维投影平面 |
| `QuadtreeNode Root` | 获取只读根节点视图 |
| `SpatialIndexSnapshot<T> CreateSnapshot()` | 创建只读位置快照 |
| `int MaxDepth` | 获取最大树深度 |
| `int MaxEntitiesPerNode` | 获取单节点实体上限 |
| `YokiRect Bounds2D` | 获取根二维边界 |
| `int PartitionCount` | 获取当前树节点数量 |

Quadtree 还实现 `ISpatialIndexDiagnostics`，其公共诊断成员见[诊断契约](#诊断契约)。

### `Octree<T>`

```csharp
public sealed class Octree<T> :
    ISpatialIndex<T>,
    ISpatialSnapshotProvider<T>,
    ISpatialIndexDiagnostics
    where T : ISpatialEntity

Octree(
    YokiBounds bounds,
    int maxDepth = 8,
    int maxEntitiesPerNode = 8)
```

除统一索引成员外，还公开：

| API | 说明 |
|---|---|
| `OctreeNode Root` | 获取只读根节点视图 |
| `SpatialIndexSnapshot<T> CreateSnapshot()` | 创建只读位置快照 |
| `int MaxDepth` | 获取最大树深度 |
| `int MaxEntitiesPerNode` | 获取单节点实体上限 |
| `YokiBounds Bounds3D` | 获取根三维边界 |
| `int PartitionCount` | 获取当前树节点数量 |

Octree 使用完整三维边界和距离，适合体积查询。它同样实现 `ISpatialIndexDiagnostics`。

### 树节点只读视图

Quadtree 和 Octree 的 `Root` 只用于查看树结构，不提供公开修改节点的方法。节点构造函数和分裂/合并操作均为内部实现。

### `Quadtree<T>.QuadtreeNode`

| API | 说明 |
|---|---|
| `YokiRect Bounds` | 当前节点二维边界 |
| `int Depth` | 当前节点深度，根节点为 `0` |
| `bool IsLeaf` | 是否为叶节点 |
| `IReadOnlyList<T> Entities` | 叶节点实体只读视图 |
| `IReadOnlyList<QuadtreeNode> Children` | 子节点只读视图；叶节点返回空列表 |

### `Octree<T>.OctreeNode`

| API | 说明 |
|---|---|
| `YokiBounds Bounds` | 当前节点三维边界 |
| `int Depth` | 当前节点深度，根节点为 `0` |
| `bool IsLeaf` | 是否为叶节点 |
| `IReadOnlyList<T> Entities` | 叶节点实体只读视图 |
| `IReadOnlyList<OctreeNode> Children` | 子节点只读视图；叶节点返回空列表 |

### Snapshot API

### `SpatialRadiusQuery`

```csharp
SpatialRadiusQuery(YokiVector3 center, float radius)
YokiVector3 Center { get; }
float Radius { get; }
```

这是 Snapshot 批量半径查询的输入描述。半径会在实际查询时校验为非负有限值。

### `SpatialIndexSnapshot<T>`

```csharp
public sealed class SpatialIndexSnapshot<T> where T : ISpatialEntity
```

| API | 说明 |
|---|---|
| `int Count` | 捕获的实体数量 |
| `SpatialPlane Plane` | 捕获时的投影平面；Octree 保留默认平面字段但使用三维距离 |
| `long SourceVersion` | 创建快照时的 SpatialKit 状态版本 |
| `void QueryRadius(YokiVector3 center, float radius, List<T> results)` | 执行单个快照半径查询 |
| `void QueryBounds(YokiBounds bounds, List<T> results)` | 执行单个完整三维包围盒查询 |
| `T QueryNearest(YokiVector3 position, float maxDistance = float.MaxValue, Func<T, bool> filter = null)` | 查询快照内最近实体 |
| `void QueryRadiusBatch(IReadOnlyList<SpatialRadiusQuery> queries, IList<List<T>> results)` | 按顺序执行多个半径查询 |
| `void QueryRadiusBatchParallel(IReadOnlyList<SpatialRadiusQuery> queries, IList<List<T>> results)` | 并行执行多个独立半径查询 |
| `void QueryBoundsBatch(IReadOnlyList<YokiBounds> queries, IList<List<T>> results)` | 按顺序执行多个包围盒查询 |
| `void QueryBoundsBatchParallel(IReadOnlyList<YokiBounds> queries, IList<List<T>> results)` | 并行执行多个独立包围盒查询 |

批量查询要求 `results.Count == queries.Count`，且每个结果列表非空。查询只追加结果，不会自动清空列表。并行版本要求每个结果列表只被对应查询写入。

### `ISpatialSnapshotProvider<T>` 与 `SpatialIndexSnapshotExtensions`

```csharp
public interface ISpatialSnapshotProvider<T> where T : ISpatialEntity
{
    SpatialIndexSnapshot<T> CreateSnapshot();
}

public static SpatialIndexSnapshot<T> CreateSnapshot<T>(
    this ISpatialIndex<T> index)
    where T : ISpatialEntity;
```

三个内置索引都实现 `ISpatialSnapshotProvider<T>`。`SpatialIndexSnapshotExtensions.CreateSnapshot` 会检查索引是否支持 Snapshot；传入 `null` 抛出 `ArgumentNullException`，不支持时抛出 `NotSupportedException`。

### 诊断契约

### `ISpatialIndexDiagnostics`

三个内置索引都实现该接口，Editor Provider 使用它生成只读状态：

| API | 说明 |
|---|---|
| `string DiagnosticsId` | 进程内稳定索引编号 |
| `string IndexKind` | `HashGrid`、`Quadtree` 或 `Octree` |
| `string EntityTypeName` | 实体 CLR 类型名 |
| `int Count` | 当前实体数量 |
| `string PlaneName` | 投影平面；三维索引为空 |
| `float CellSize` | HashGrid 网格尺寸，其它为 `0` |
| `int MaxDepth` | 树最大深度，HashGrid 为 `0` |
| `int MaxEntitiesPerNode` | 树节点容量，HashGrid 为 `0` |
| `int PartitionCount` | 分区或节点数量 |
| `bool HasCellSize` | 是否有网格尺寸 |
| `bool HasBounds2D` / `bool HasBounds3D` | 是否有对应边界 |
| `YokiRect Bounds2D` / `YokiBounds Bounds3D` | 对应边界，没有时返回默认值 |
| `string CreatedAtUtc` | 创建时间的 UTC ISO 文本 |

### `SpatialIndexDiagnosticsSnapshot`

这是 `ISpatialIndexDiagnostics` 的只读值快照，公开属性与接口字段一致：`DiagnosticsId`、`IndexKind`、`EntityTypeName`、`Count`、`PlaneName`、`CellSize`、`MaxDepth`、`MaxEntitiesPerNode`、`PartitionCount`、`HasCellSize`、`HasBounds2D`、`HasBounds3D`、`Bounds2D`、`Bounds3D` 和 `CreatedAtUtc`。构造函数为内部 API，由 SpatialKit 诊断采样创建。

### `SpatialKitDiagnosticsSnapshot`

| API | 说明 |
|---|---|
| `int TotalCreatedIndexCount` | 进程启动以来创建的索引总数 |
| `IReadOnlyList<SpatialIndexDiagnosticsSnapshot> Indexes` | 当前仍存活的索引摘要 |
| `int ReleasedIndexCount` | 采样时清理的弱引用数量累计值 |

该类型的构造函数为内部 API。诊断注册表持有索引弱引用，不会因为 Tool Interaction 采样阻止索引被回收。

## 宿主与工具入口

### Tool Interaction

Editor Provider 通过 `SpatialKitEditorInstaller.EnsureInstalled()` 幂等注册到统一 Tool Interaction catalog。Unity Editor 由 `[InitializeOnLoadMethod]` 自动安装；Godot Tools 由宿主插件启动入口调用。

当前公开给 Tool Interaction 的只读入口：

| 类型 | 名称 | 说明 |
|---|---|---|
| Snapshot | `SpatialKit/state` | 当前索引状态快照 |
| Command | `SpatialKit/stats` | 聚合索引数量、实体数量和分区统计 |
| Command | `SpatialKit/list_indexes` | 当前索引详情列表 |
| Command | `SpatialKit/density` | 按 diagnosticsId/resolution 获取有界二维密度 bin 和热点 |
| Command | `SpatialKit/analyze` | 聚合 stats 与全部索引密度，供 CLI/AI 诊断 |
| Command | `SpatialKit/get_workbench_snapshot` | 完整 Workbench 状态 JSON |

所有命令都是只读操作，不会插入、更新、删除实体，也不会执行空间查询。状态 JSON 使用 `schemaVersion: 1`，并包含单调 `version`；索引列表最多输出 128 项，超出时通过 `indexesTruncated: true` 表示裁剪。state 默认携带 8x8 密度 bin；`density` 默认 32x32，分辨率限制为 4-64，详细结果只在显式请求时生成。

常见统计字段包括 `activeIndexCount`、`totalCreatedIndexCount`、`releasedIndexCount`、`entityCount`、`partitionCount`、`hashGridCount`、`quadtreeCount` 和 `octreeCount`。索引详情包括诊断 Id、索引类型、实体类型、数量、平面、网格/树参数、分区数量、创建时间和可选边界。

### Workbench 与 CLI

Workbench SpatialKit 页面以只读方式展示运行中的索引实例、实体/分区汇总、二维密度热力图、热点健康摘要、P95/最大占用和 stale/session/generation。HashGrid、Quadtree 和 Octree 都输出二维聚合；Octree 将全部高度上的实体沿 Y 轴聚合到 XZ 投影，热力图不是八叉树节点结构。列表显示 `XZ投影`，详情显示 `XZ 投影 · Y 轴聚合`。页面消费 `Tooling.Application` 强类型 read model，不直接解析 JSON，也不会发送周期 command。

页面采用 SpatialKit 专属的 Master-Detail 工作区，保留“空间索引列表 + 密度热力图”的识别性：左侧是可调宽度、可独立滚动的运行中索引列表，列表项同时显示索引类型、实体类型、诊断 Id、投影平面和实体数；右侧按索引身份、参数上下文、密度指标和方形热力图组织详情。热力图使用当前详情视口的可用边长保持方形，避免空间坐标被拉伸；无索引、未选择、无密度数据和 stale 分别使用独立空状态或警告表达，不把 stale 数据伪装成实时结果。1280x820 与 1700x1060 验收尺寸均禁止横向滚动，文本字号不随窗口缩放。

CLI/AI 使用以下入口：

```powershell
yoki spatialkit stats
yoki spatialkit indexes
yoki spatialkit density --index hash-grid-1 --resolution 32
yoki spatialkit analyze
```

CLI 输出保留 command/response evidence，AI 应先用 `stats` 判断实例规模，再用 `density` 定位热点，用 `analyze` 解释 P95、最大占用和分区倾斜。stale、generation 不一致或 payload 被裁剪时必须按诊断状态报告，不能当作零实体。

### Unity Scene Gizmo

Unity Scene View 的 `SpatialKit` Overlay 在 Unity Editor 启动或脚本重载完成后隐藏，并覆盖 Unity 布局保存的旧可见状态；需要时通过 `YokiFrame/SpatialKit/Open Overlay Menu` 显示并展开。面板提供 `Spatial` Toggle 和索引下拉框：Toggle 默认关闭且只控制线框绘制，下拉框可以显示全部索引，或按 `diagnosticsId` 只显示一个索引。Overlay 可通过标题栏关闭按钮或 Scene View 的 Overlays 菜单再次隐藏；开关和筛选只保存在当前 Unity Editor 会话，不写入 Runtime Settings、Project Settings 或协议文件。

三类索引的绘制语义：

| 索引 | Scene View 绘制 |
|---|---|
| HashGrid | 已占用的 XY/XZ 网格边界，以及投影后的实体点 |
| Quadtree | 根节点和子节点的 XY/XZ 边界，以及投影后的实体点 |
| Octree | 真实三维节点 AABB，以及实体实际三维位置 |

节点按深度使用不同颜色，直接持有实体的叶节点提高亮度。绘制器按诊断版本和 0.2 秒最小间隔缓存快照；每个索引最多复制 2048 个节点和 4096 个实体，超出时标签追加 `truncated`。Editor-only 快照只复制节点边界、实体 Id 和位置，不持有业务实体引用，不进入 Unity Player 或 Godot 无 `TOOLS` 构建。

## 生命周期与错误边界

- 内置索引本体没有线程安全保证；由一个明确 owner 负责写入、更新和清理。
- `CreateSnapshot` 复制实体位置数组，适合把查询工作转移到其它线程；创建期间不得并发修改索引。
- Snapshot 返回原实体引用，不会复制实体对象本身；如果其它线程修改实体字段，业务仍需自行保证线程安全。
- 查询结果列表由调用方拥有，空间查询不会自动分配结果列表或清空已有内容；高频查询应复用 `List<T>`。
- `UpdateBatch` 按输入顺序逐个调用 `Update`，不会自动合并重复 Id，也不提供事务回滚。
- `Clear` 清空实体和分区；树索引恢复为叶根节点，HashGrid 清空所有非空网格。
- Editor/Tools 诊断注册表通过弱引用在下一次采样时移除回收索引；`UNITY_EDITOR || (GODOT && TOOLS)` 之外不编译诊断字段、注册表和版本标记，Player 热路径不执行诊断调用。
- Unity Scene Gizmo 只在启用且 Scene View 发生 Repaint 时采样和绘制；关闭时不创建几何快照。

## 限制与相关资料

| 问题 | 处理 |
|---|---|
| 移动实体后查询不到新位置 | 修改 `Position` 后必须调用 `Update(entity)` |
| `Count` 比预期多 | 检查同一实体是否使用稳定且唯一的 `SpatialId`；相同 Id 插入才会替换 |
| 2D 查询受 Y/Z 影响 | 检查 `SpatialPlane`；HashGrid/Quadtree 半径和最近邻只使用投影平面 |
| `QueryBounds` 结果与半径查询不同 | `QueryBounds` 始终按完整三维 `YokiBounds.Contains` 判断 |
| Snapshot 并行查询结果互相覆盖 | 每个查询必须使用独立的 `List<T>`，并保证结果槽位与查询槽位一一对应 |
| 创建树时抛出参数异常 | 检查边界坐标和尺寸是否有限且为正，`maxDepth`/`maxEntitiesPerNode` 是否大于零 |
| Workbench 没有 SpatialKit 页面 | 确认 Workbench 已加载 SpatialKit 页面，并检查 snapshot 是否为 stale |
| Octree 热力图看不到高度差异 | 热力图固定沿 Y 轴聚合到 XZ；启用 Unity Scene View `SpatialKit` Gizmo 查看真实三维 AABB 和实体高度 |
| Scene View 没有空间线框 | 执行 `YokiFrame/SpatialKit/Open Overlay Menu` 打开面板，再开启 `Spatial`，并确认下拉框没有筛选到已结束的索引 |

### 验证

Editor 测试位于 `Tools/SpatialKit/Tests/Editor`，覆盖：

- HashGrid 投影距离语义。
- Quadtree、Octree 可变实体更新不重复。
- 相同 `SpatialId` 插入替换旧实体。
- 非法网格尺寸校验。
- Snapshot 捕获位置后不受实体后续移动影响。
- 并行批量查询保持查询槽位对应关系。
- Gizmo 快照保留二维投影边界和 Octree 三维 AABB，并正确报告节点/实体预算裁剪。

Workbench/Application 测试另外覆盖 state 密度 bin、热点解析、坏 payload stale 处理、页面选择保留、Octree 投影标签和热点健康提示。

新增索引策略或修改空间语义时，应同时验证运行时查询、Snapshot 批量查询、边界裁剪、诊断 JSON、密度 bin、CLI 和 Tool Interaction catalog，并扫描 Player 产物确认不存在诊断注册表。
