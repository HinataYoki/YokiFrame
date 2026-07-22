namespace YokiFrame
{
    /// <summary>提供 SpatialKit 三种索引的统一创建入口。</summary>
    public static partial class SpatialKit
    {
        private const int DEFAULT_MAX_DEPTH = 8;
        private const int DEFAULT_MAX_ENTITIES_PER_NODE = 8;

        /// <summary>创建使用投影平面的固定网格索引。</summary>
        /// <typeparam name="T">空间实体类型。</typeparam>
        /// <param name="cellSize">必须为有限正数的网格尺寸。</param>
        /// <param name="plane">HashGrid 的二维投影平面。</param>
        /// <returns>新建的 HashGrid。</returns>
        public static SpatialHashGrid<T> CreateHashGrid<T>(float cellSize, SpatialPlane plane = SpatialPlane.XZ)
            where T : ISpatialEntity
        {
            return new SpatialHashGrid<T>(cellSize, plane);
        }

        /// <summary>创建固定二维边界的四叉树索引。</summary>
        /// <typeparam name="T">空间实体类型。</typeparam>
        /// <param name="bounds">二维投影边界。</param>
        /// <param name="maxDepth">最大树深度。</param>
        /// <param name="maxEntitiesPerNode">单节点实体上限。</param>
        /// <param name="plane">四叉树的二维投影平面。</param>
        /// <returns>新建的 Quadtree。</returns>
        public static Quadtree<T> CreateQuadtree<T>(
            YokiRect bounds,
            int maxDepth = DEFAULT_MAX_DEPTH,
            int maxEntitiesPerNode = DEFAULT_MAX_ENTITIES_PER_NODE,
            SpatialPlane plane = SpatialPlane.XZ)
            where T : ISpatialEntity
        {
            return new Quadtree<T>(bounds, maxDepth, maxEntitiesPerNode, plane);
        }

        /// <summary>创建固定三维边界的八叉树索引。</summary>
        /// <typeparam name="T">空间实体类型。</typeparam>
        /// <param name="bounds">三维索引边界。</param>
        /// <param name="maxDepth">最大树深度。</param>
        /// <param name="maxEntitiesPerNode">单节点实体上限。</param>
        /// <returns>新建的 Octree。</returns>
        public static Octree<T> CreateOctree<T>(
            YokiBounds bounds,
            int maxDepth = DEFAULT_MAX_DEPTH,
            int maxEntitiesPerNode = DEFAULT_MAX_ENTITIES_PER_NODE)
            where T : ISpatialEntity
        {
            return new Octree<T>(bounds, maxDepth, maxEntitiesPerNode);
        }

#if UNITY_EDITOR || (GODOT && TOOLS)
        /// <summary>创建供 Tool Editor 生成只读快照的内部诊断采样。</summary>
        /// <returns>当前所有存活索引的诊断摘要。</returns>
        internal static SpatialKitDiagnosticsSnapshot CreateDiagnosticsSnapshot()
        {
            return SpatialKitDiagnosticsRegistry.CreateSnapshot();
        }

        /// <summary>获取供 Editor/Tools Provider 使用的单调诊断版本。</summary>
        /// <returns>当前索引状态版本。</returns>
        internal static long GetDiagnosticsVersion()
        {
            return SpatialKitDiagnosticsRegistry.Version;
        }
#endif
    }
}
