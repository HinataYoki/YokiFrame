# SpatialKit 空间索引

SpatialKit 是纯 C# 空间查询工具，业务代码在 `YokiFrame` 命名空间中调用 `SpatialKit` 静态入口创建索引。它不依赖 Unity `Vector3`、`Bounds` 或 Godot 节点类型，Unity/Godot 项目都使用同一套 Runtime API。

## 核心类型

| 类型 | 说明 |
|---|---|
| `SpatialKit` | 静态统一入口，创建 HashGrid、Quadtree 和 Octree。 |
| `ISpatialEntity` | 可被索引管理的实体，提供 `SpatialId` 和 `YokiVector3 Position`。 |
| `ISpatialIndex<T>` | 统一查询接口，提供插入、移除、更新、半径查询、边界查询和最近点查询。 |
| `SpatialHashGrid<T>` | 固定网格空间哈希，适合实体分布较均匀、频繁移动的场景。 |
| `Quadtree<T>` | 2D / 2.5D 投影索引，支持 `SpatialPlane.XZ` 和 `SpatialPlane.XY`。 |
| `Octree<T>` | 完整 3D 空间索引。 |

## 快速使用

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
grid.Insert(new EnemySpatialEntity(1, new YokiVector3(0f, 0f, 0f)));
grid.Insert(new EnemySpatialEntity(2, new YokiVector3(3f, 0f, 0f)));

var results = new List<EnemySpatialEntity>();
grid.QueryRadius(YokiVector3.Zero, 4f, results);
```

## 选择索引

| 场景 | 推荐索引 |
|---|---|
| 大量动态单位、查询半径稳定 | `CreateHashGrid<T>()` |
| 2D / 2.5D 地图、实体分布不均 | `CreateQuadtree<T>()` |
| 完整 3D 空间、体积边界查询 | `CreateOctree<T>()` |

`QueryRadius()`、`QueryBounds()` 会把结果写入调用方传入的 `List<T>`。高频查询时请复用列表并手动 `Clear()`，不要在循环里反复 new。

## Unity 数学类型转换

SpatialKit 的核心 API 只接收 `YokiVector3`、`YokiRect` 和 `YokiBounds`，保持 Unity/Godot 无关。Unity 项目中不要在每个调用点手写 `new YokiVector3(position.x, position.y, position.z)`；Unity Adapter 已在 `YokiFrame.Unity` 中提供双向扩展方法：

```csharp
using UnityEngine;
using YokiFrame;
using YokiFrame.Unity;

var worldBounds = new Bounds(Vector3.zero, Vector3.one * 1000f).ToYokiBounds();
var octree = SpatialKit.CreateOctree<MySpatialEntity>(worldBounds);

mQueryBuffer.Clear();
mIndex.QueryRadius(sensor.transform.position.ToYokiVector3(), sensor.Range, mQueryBuffer);
```

可用转换包括 `Vector2` / `YokiVector2`、`Vector3` / `YokiVector3`、`Rect` / `YokiRect`、`Bounds` / `YokiBounds`。转换 helper 位于 Unity Adapter，Core Runtime 仍不引用 `UnityEngine`。

## 命令桥

SpatialKit 已接入文件命令桥。AI、Tauri 和脚本优先使用 engine-scoped v2 路径：

```json
{
  "protocolVersion": 2,
  "engineId": "unity-editor",
  "source": "codex",
  "createdAtUtc": "2026-06-21T12:00:00Z",
  "requestId": "codex-spatial-001",
  "kit": "SpatialKit",
  "action": "get_workbench_snapshot",
  "payload": {}
}
```

| action | payload | 说明 |
|---|---|---|
| `get_workbench_snapshot` | `{}` | 返回 `stats` 和 `indexes`。 |
| `stats` | `{}` | 返回活动索引数量、累计创建数量、实体总数和类型分布。 |
| `list_indexes` | `{}` | 返回当前存活索引列表。 |

命令桥是只读诊断入口，不提供插入、删除、更新实体的命令。实体变更仍由运行时代码直接调用索引对象完成，避免把高频空间查询和移动同步到跨进程文件桥。

## Tauri 工作台

SpatialKit 页面读取顺序为：

1. `read_telemetry("SpatialKit", "state")`
2. `read_snapshot("SpatialKit", "state")`
3. `send_command("SpatialKit", "get_workbench_snapshot")`

Unity `KitStateSnapshotPublisher` 和 Godot `GodotKitStateSnapshotPublisher` 都通过可选 handler 发布 `SpatialKit/state`。页面只在缺少 telemetry/snapshot 或用户点击刷新时走命令桥。

## AI 诊断入口

AI 默认优先读取：

```text
.yokiframe/engines/<engineId>/snapshots/SpatialKit/state.json
```

snapshot 缺失、过期或需要显式刷新时，再发送 `SpatialKit/get_workbench_snapshot`、`SpatialKit/stats` 或 `SpatialKit/list_indexes`。空间索引的实体变更不通过 `.yokiframe` 执行。
