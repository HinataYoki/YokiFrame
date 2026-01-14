#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// SpatialKit 概述与快速上手
    /// </summary>
    internal static class SpatialKitDocOverview
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "SpatialKit 快速上手",
                Description = "高性能空间索引，用于范围查询、邻居检测。替代 Physics.OverlapSphere，零 GC。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "1. 定义实体（struct 推荐）",
                        Code = @"public readonly struct MyEntity : ISpatialEntity
{
    public int SpatialId { get; }
    public Vector3 Position { get; }
    
    public MyEntity(int id, Vector3 pos)
    {
        SpatialId = id;
        Position = pos;
    }
}",
                        Explanation = "实现 ISpatialEntity 接口。struct 零 GC，Position 是快照值，移动后需调用 Update()。"
                    },
                    new()
                    {
                        Title = "2. 创建索引",
                        Code = @"// 空间哈希（均匀分布最快）
var index = SpatialKit.CreateHashGrid<MyEntity>(cellSize: 10f);

// 四叉树（2D/2.5D，自适应分区）
var index = SpatialKit.CreateQuadtree<MyEntity>(
    new Rect(-500, -500, 1000, 1000));

// 八叉树（完整 3D）
var index = SpatialKit.CreateOctree<MyEntity>(
    new Bounds(Vector3.zero, new Vector3(1000, 200, 1000)));

// 2D 游戏用 XY 平面
var index = SpatialKit.CreateHashGrid<MyEntity>(10f, SpatialPlane.XY);",
                        Explanation = "HashGrid O(1) 查询；Quadtree/Octree 自适应分区。默认 XZ 平面（2.5D），传统 2D 用 XY。"
                    },
                    new()
                    {
                        Title = "3. 增删改",
                        Code = @"// 插入
var entity = new MyEntity(1, new Vector3(10, 0, 20));
index.Insert(entity);

// 移动后更新（必须调用！）
var moved = new MyEntity(1, newPosition);
index.Update(moved);

// 移除
index.Remove(entity);",
                        Explanation = "struct 是值类型，移动后必须创建新实例并调用 Update() 更新索引。"
                    },
                    new()
                    {
                        Title = "4. 查询",
                        Code = @"// 范围查询（复用 List 零 GC）
private readonly List<MyEntity> mResults = new(64);

void Query(Vector3 center, float radius)
{
    mResults.Clear();
    index.QueryRadius(center, radius, mResults);
    
    for (int i = 0; i < mResults.Count; i++)
        Process(mResults[i]);
}

// 最近邻
var nearest = index.QueryNearest(position, maxDistance: 50f);",
                        Explanation = "结果写入外部 List，避免 GC。用 for 循环代替 foreach。"
                    }
                }
            };
        }
    }
}
#endif
