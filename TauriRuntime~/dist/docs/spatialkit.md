# SpatialKit 空间索引

## 选择索引

| 场景 | 推荐 |
|---|---|
| 大量动态单位、查询半径稳定 | `CreateHashGrid<T>()` |
| 2D / 2.5D 地图、实体分布不均 | `CreateQuadtree<T>()` |
| 完整 3D 空间、体积查询 | `CreateOctree<T>()` |

实体实现 `ISpatialEntity`，使用 `YokiVector3`，不直接依赖 Unity 或 Godot 类型。

## 最小示例

```csharp
using System.Collections.Generic;
using YokiFrame;

public readonly struct EnemySpatialEntity : ISpatialEntity
{
    public EnemySpatialEntity(int id, YokiVector3 position)
    {
        SpatialId = id;
        Position = position;
    }

    public int SpatialId { get; }
    public YokiVector3 Position { get; }
}

var grid = SpatialKit.CreateHashGrid<EnemySpatialEntity>(cellSize: 2f);
grid.Insert(new EnemySpatialEntity(1, YokiVector3.Zero));

var results = new List<EnemySpatialEntity>();
grid.QueryRadius(YokiVector3.Zero, 4f, results);
```

高频查询复用 `List<T>`，不要在循环里反复 new。

## 常用操作

| 方法 | 说明 |
|---|---|
| `Insert(entity)` | 插入实体。 |
| `Remove(entity)` | 移除实体。 |
| `Update(entity)` | 位置变化后更新。 |
| `QueryRadius(center, radius, results)` | 半径查询。 |
| `QueryBounds(bounds, results)` | 边界查询。 |
| `QueryNearest(position, maxDistance, out result)` | 最近实体。 |
| `Clear()` | 清空索引。 |

## 工作台诊断

SpatialKit 页面用于查看索引类型、实体数量、分区数量、平面和边界。

| 在工作台里看什么 | 用途 |
|---|---|
| Index 列表 | 查看当前有哪些 HashGrid、Quadtree 或 Octree。 |
| Entity Count | 判断实体是否插入成功、是否忘记删除。 |
| Partition / Node 数量 | 判断索引是否过度分裂或范围设置不合理。 |
| Bounds / Plane | 检查查询空间是否覆盖游戏区域。 |

查询结果异常时，先确认实体数量，再看边界是否覆盖目标位置。实体插入、删除、更新属于运行时热路径，在业务代码里调用索引对象完成。

## 常见坑

| 问题 | 处理方式 |
|---|---|
| 查询不到移动后的实体 | 位置变化后调用 `Update(entity)`。 |
| 查询频繁分配 | 复用结果列表，查询前 `Clear()`。 |
| 2.5D 轴不对 | 创建 Quadtree 时确认 `SpatialPlane.XZ` 或 `XY`。 |
| 想通过工作台改实体 | 不支持；实体变化要在运行时代码里更新索引。 |
