#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// SpatialKit 查询 API
    /// </summary>
    internal static class SpatialKitDocQuery
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "查询 API",
                Description = "所有查询零 GC，结果写入外部 List。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "范围查询",
                        Code = @"private readonly List<MyEntity> mResults = new(64);

// 球形范围
mResults.Clear();
index.QueryRadius(center, radius, mResults);

// AABB 范围
index.QueryBounds(bounds, mResults);",
                        Explanation = "QueryRadius 球形查询，QueryBounds 立方体查询。"
                    },
                    new()
                    {
                        Title = "最近邻查询",
                        Code = @"// 无距离限制
var nearest = index.QueryNearest(position);

// 限制距离
var nearest = index.QueryNearest(position, maxDistance: 50f);

// 带过滤（static lambda 避免 GC）
var nearest = index.QueryNearest(position, 100f, 
    static e => e.Faction != myFaction);

// 判断是否找到
if (nearest.SpatialId != 0) { /* 找到 */ }",
                        Explanation = "返回单个实体，无结果返回 default(T)。"
                    },
                    new()
                    {
                        Title = "索引类型选择",
                        Code = @"// HashGrid：均匀分布、频繁移动
// - cellSize ≈ 最大查询半径
// - O(1) 查询，Update 最快

// Quadtree：不均匀分布、2D/2.5D
// - 自动在密集区域细分
// - 适合 RTS、MOBA

// Octree：需要 Y 轴精度
// - 飞行游戏、多层建筑
// - 内存开销较大",
                        Explanation = "根据场景选择合适的索引类型。"
                    }
                }
            };
        }
    }
}
#endif
