using YokiFrame;

namespace YokiFrame
{
    /// <summary>
    /// 空间索引工具包静态入口。
    /// </summary>
    public static class SpatialKit
    {
        private const int DEFAULT_MAX_DEPTH = 8;
        private const int DEFAULT_MAX_ENTITIES_PER_NODE = 8;

        /// <summary>
        /// 创建固定网格空间哈希索引。
        /// </summary>
        /// <typeparam name="T">空间实体类型。</typeparam>
        /// <param name="cellSize">网格尺寸。</param>
        /// <param name="plane">二维投影平面。</param>
        /// <returns>空间哈希索引。</returns>
        public static SpatialHashGrid<T> CreateHashGrid<T>(float cellSize, SpatialPlane plane = SpatialPlane.XZ)
            where T : ISpatialEntity
        {
            var index = new SpatialHashGrid<T>(cellSize, plane);
            SpatialKitDiagnosticsRegistry.Register(index);
            return index;
        }

        /// <summary>
        /// 创建四叉树空间索引。
        /// </summary>
        /// <typeparam name="T">空间实体类型。</typeparam>
        /// <param name="bounds">二维索引边界。</param>
        /// <param name="maxDepth">最大树深度。</param>
        /// <param name="maxEntitiesPerNode">单节点最大实体数。</param>
        /// <param name="plane">二维投影平面。</param>
        /// <returns>四叉树空间索引。</returns>
        public static Quadtree<T> CreateQuadtree<T>(
            YokiRect bounds,
            int maxDepth = DEFAULT_MAX_DEPTH,
            int maxEntitiesPerNode = DEFAULT_MAX_ENTITIES_PER_NODE,
            SpatialPlane plane = SpatialPlane.XZ)
            where T : ISpatialEntity
        {
            var index = new Quadtree<T>(bounds, maxDepth, maxEntitiesPerNode, plane);
            SpatialKitDiagnosticsRegistry.Register(index);
            return index;
        }

        /// <summary>
        /// 创建八叉树空间索引。
        /// </summary>
        /// <typeparam name="T">空间实体类型。</typeparam>
        /// <param name="bounds">三维索引边界。</param>
        /// <param name="maxDepth">最大树深度。</param>
        /// <param name="maxEntitiesPerNode">单节点最大实体数。</param>
        /// <returns>八叉树空间索引。</returns>
        public static Octree<T> CreateOctree<T>(
            YokiBounds bounds,
            int maxDepth = DEFAULT_MAX_DEPTH,
            int maxEntitiesPerNode = DEFAULT_MAX_ENTITIES_PER_NODE)
            where T : ISpatialEntity
        {
            var index = new Octree<T>(bounds, maxDepth, maxEntitiesPerNode);
            SpatialKitDiagnosticsRegistry.Register(index);
            return index;
        }

        internal static SpatialKitDiagnosticsSnapshot CreateDiagnosticsSnapshot()
        {
            return SpatialKitDiagnosticsRegistry.CreateSnapshot();
        }
    }
}
