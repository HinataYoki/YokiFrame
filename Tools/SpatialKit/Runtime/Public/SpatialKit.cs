using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 空间索引工具包 - 静态工厂入口
    /// </summary>
    public static class SpatialKit
    {
        /// <summary>
        /// 创建空间哈希网格
        /// </summary>
        /// <param name="cellSize">格子大小，建议等于最大查询半径</param>
        /// <param name="plane">投影平面，XZ 用于 2.5D，XY 用于传统 2D</param>
        public static SpatialHashGrid<T> CreateHashGrid<T>(float cellSize, SpatialPlane plane = SpatialPlane.XZ) where T : ISpatialEntity
            => new(cellSize, plane);

        /// <summary>
        /// 创建四叉树（2D/2.5D）
        /// </summary>
        /// <param name="bounds">2D 边界（对于 XZ 平面，y 对应 Z 轴；对于 XY 平面，y 对应 Y 轴）</param>
        /// <param name="maxDepth">最大深度</param>
        /// <param name="maxEntitiesPerNode">节点分裂阈值</param>
        /// <param name="plane">投影平面，XZ 用于 2.5D，XY 用于传统 2D</param>
        public static Quadtree<T> CreateQuadtree<T>(Rect bounds, int maxDepth = 8, int maxEntitiesPerNode = 8, SpatialPlane plane = SpatialPlane.XZ) where T : ISpatialEntity
            => new(bounds, maxDepth, maxEntitiesPerNode, plane);

        /// <summary>
        /// 创建八叉树（完整 3D）
        /// </summary>
        public static Octree<T> CreateOctree<T>(Bounds bounds, int maxDepth = 8, int maxEntitiesPerNode = 8) where T : ISpatialEntity
            => new(bounds, maxDepth, maxEntitiesPerNode);
    }
}
