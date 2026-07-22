using System;
using System.Collections.Generic;
using System.Globalization;

namespace YokiFrame
{
    /// <summary>固定三维边界的八叉树空间索引。</summary>
    public sealed partial class Octree<T> : ISpatialIndex<T>, ISpatialSnapshotProvider<T>
        where T : ISpatialEntity
    {
        private const int DEFAULT_MAX_DEPTH = 8;
        private const int DEFAULT_MAX_ENTITIES_PER_NODE = 8;
        private const int ROOT_DEPTH = 0;
        private const int CHILD_COUNT = 8;

#if UNITY_EDITOR || (GODOT && TOOLS)
        private readonly string mDiagnosticsId;
        private readonly string mEntityTypeName;
        private readonly string mCreatedAtUtc;
#endif
        private readonly int mMaxDepth;
        private readonly int mMaxEntitiesPerNode;
        private readonly OctreeNode mRoot;
        private readonly Dictionary<int, T> mEntities = new Dictionary<int, T>();
        private readonly Dictionary<int, OctreeNode> mEntityNodes = new Dictionary<int, OctreeNode>();
        private int mCount;

        /// <summary>创建固定三维边界的八叉树。</summary>
        /// <param name="bounds">必须为有限正尺寸的三维边界。</param>
        /// <param name="maxDepth">大于零的最大树深度。</param>
        /// <param name="maxEntitiesPerNode">大于零的单节点实体上限。</param>
        public Octree(
            YokiBounds bounds,
            int maxDepth = DEFAULT_MAX_DEPTH,
            int maxEntitiesPerNode = DEFAULT_MAX_ENTITIES_PER_NODE)
        {
            SpatialMath.ValidateBounds(bounds, nameof(bounds));
            if (maxDepth <= ROOT_DEPTH)
            {
                throw new ArgumentOutOfRangeException(nameof(maxDepth), "Max depth must be greater than zero.");
            }

            if (maxEntitiesPerNode <= ROOT_DEPTH)
            {
                throw new ArgumentOutOfRangeException(nameof(maxEntitiesPerNode), "Node capacity must be greater than zero.");
            }

#if UNITY_EDITOR || (GODOT && TOOLS)
            mDiagnosticsId = SpatialKitDiagnosticsRegistry.NextIndexId("octree");
            mEntityTypeName = typeof(T).FullName ?? typeof(T).Name;
            mCreatedAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
#endif
            mMaxDepth = maxDepth;
            mMaxEntitiesPerNode = maxEntitiesPerNode;
            mRoot = new OctreeNode(bounds, ROOT_DEPTH);
#if UNITY_EDITOR || (GODOT && TOOLS)
            SpatialKitDiagnosticsRegistry.Register(this);
#endif
        }

        /// <summary>获取当前实体数量。</summary>
        public int Count { get { return mCount; } }

        /// <summary>获取只读根节点视图。</summary>
        public OctreeNode Root { get { return mRoot; } }

#if UNITY_EDITOR || (GODOT && TOOLS)
        /// <summary>获取诊断编号。</summary>
        public string DiagnosticsId { get { return mDiagnosticsId; } }

        /// <summary>获取索引类型名称。</summary>
        public string IndexKind { get { return "Octree"; } }

        /// <summary>获取实体完整类型名称。</summary>
        public string EntityTypeName { get { return mEntityTypeName; } }

        /// <summary>八叉树不使用二维平面，固定返回空字符串。</summary>
        public string PlaneName { get { return string.Empty; } }

        /// <summary>八叉树没有网格尺寸，固定返回零。</summary>
        public float CellSize { get { return 0f; } }

        /// <summary>获取最大树深度。</summary>
        public int MaxDepth { get { return mMaxDepth; } }

        /// <summary>获取单节点最大实体数。</summary>
        public int MaxEntitiesPerNode { get { return mMaxEntitiesPerNode; } }

        /// <summary>获取当前树节点数量。</summary>
        public int PartitionCount { get { return CountNodes(mRoot); } }

        /// <summary>表示八叉树不包含网格尺寸。</summary>
        public bool HasCellSize { get { return false; } }

        /// <summary>表示八叉树不包含二维边界。</summary>
        public bool HasBounds2D { get { return false; } }

        /// <summary>表示八叉树包含三维边界。</summary>
        public bool HasBounds3D { get { return true; } }

        /// <summary>八叉树没有二维边界，返回默认值。</summary>
        public YokiRect Bounds2D { get { return default(YokiRect); } }

        /// <summary>获取三维根边界。</summary>
        public YokiBounds Bounds3D { get { return mRoot.Bounds; } }

        /// <summary>获取创建时间。</summary>
        public string CreatedAtUtc { get { return mCreatedAtUtc; } }
#endif

        /// <summary>插入实体并在节点分裂后更新所有实体定位。</summary>
        /// <param name="entity">待插入实体。</param>
        public void Insert(T entity)
        {
            ValidateEntity(entity);
            int id = entity.SpatialId;
            if (mEntities.ContainsKey(id))
            {
                RemoveById(id);
            }

            mEntities[id] = entity;
            if (IsInsideRoot(entity.Position))
            {
                InsertToNode(mRoot, entity);
            }
            else
            {
                mOverflowEntities[id] = entity;
            }
            mCount++;
#if UNITY_EDITOR || (GODOT && TOOLS)
            SpatialKitDiagnosticsRegistry.MarkChanged();
#endif
        }

        /// <summary>按 ID 从定位节点移除实体。</summary>
        /// <param name="entity">包含目标 ID 的实体。</param>
        /// <returns>找到并移除时返回 true。</returns>
        public bool Remove(T entity)
        {
            ValidateEntity(entity);
            return RemoveById(entity.SpatialId);
        }

        /// <summary>按 ID 删除旧实体后重新插入，支持可变引用实体。</summary>
        /// <param name="entity">已更新位置的实体。</param>
        public void Update(T entity)
        {
            ValidateEntity(entity);
            if (mEntities.ContainsKey(entity.SpatialId))
            {
                RemoveById(entity.SpatialId);
            }

            mEntities[entity.SpatialId] = entity;
            if (IsInsideRoot(entity.Position))
            {
                InsertToNode(mRoot, entity);
            }
            else
            {
                mOverflowEntities[entity.SpatialId] = entity;
            }
            mCount++;
#if UNITY_EDITOR || (GODOT && TOOLS)
            SpatialKitDiagnosticsRegistry.MarkChanged();
#endif
        }

        /// <summary>按输入顺序批量更新实体。</summary>
        /// <param name="entities">待更新实体列表。</param>
        public void UpdateBatch(IReadOnlyList<T> entities)
        {
            if (entities == null)
            {
                throw new ArgumentNullException(nameof(entities));
            }

            for (int index = 0; index < entities.Count; index++)
            {
                Update(entities[index]);
            }
        }

        /// <summary>捕获当前八叉树的只读位置快照，供批量查询线程使用。</summary>
        /// <returns>保存实体引用和捕获时位置的快照。</returns>
        public SpatialIndexSnapshot<T> CreateSnapshot()
        {
            T[] entities = new T[mEntities.Count];
            int index = 0;
            foreach (T entity in mEntities.Values)
            {
                entities[index++] = entity;
            }

            return new SpatialIndexSnapshot<T>(
                entities,
                SpatialPlane.XZ,
                true,
#if UNITY_EDITOR || (GODOT && TOOLS)
                SpatialKit.GetDiagnosticsVersion());
#else
                0L);
#endif
        }

        /// <summary>查询完整三维球体半径内实体。</summary>
        /// <param name="center">查询中心。</param>
        /// <param name="radius">非负有限半径。</param>
        /// <param name="results">接收结果的列表。</param>
        public void QueryRadius(YokiVector3 center, float radius, List<T> results)
        {
            ValidateQuery(center, radius, results);
            if (mCount == 0)
            {
                return;
            }

            QueryRadiusNode(mRoot, center, radius, radius * radius, results);
            AppendOverflowRadius(center, radius * radius, results);
        }

        /// <summary>查询完整三维包围盒内实体。</summary>
        /// <param name="bounds">有限且正尺寸的查询包围盒。</param>
        /// <param name="results">接收结果的列表。</param>
        public void QueryBounds(YokiBounds bounds, List<T> results)
        {
            SpatialMath.ValidateBounds(bounds, nameof(bounds));
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            if (mCount == 0)
            {
                return;
            }

            QueryBoundsNode(mRoot, bounds, results);
            AppendOverflowBounds(bounds, results);
        }

        /// <summary>查询完整三维空间内的最近实体。</summary>
        /// <param name="position">查询位置。</param>
        /// <param name="maxDistance">最大三维距离。</param>
        /// <param name="filter">可选过滤器。</param>
        /// <returns>最近实体或 default。</returns>
        public T QueryNearest(YokiVector3 position, float maxDistance = float.MaxValue, Func<T, bool> filter = null)
        {
            SpatialMath.ValidatePosition(position, nameof(position));
            ValidateDistance(maxDistance);
            if (mCount == 0)
            {
                return default(T);
            }

            T nearest = default(T);
            float nearestDistanceSquared = SpatialMath.IsUnboundedDistance(maxDistance)
                ? float.MaxValue
                : maxDistance * maxDistance;
            bool found = false;
            QueryNearestOverflow(
                mOverflowEntities,
                position,
                filter,
                ref nearest,
                ref nearestDistanceSquared,
                ref found);
            QueryNearestNode(mRoot, position, filter, ref nearest, ref nearestDistanceSquared, ref found);
            return found ? nearest : default(T);
        }

        /// <summary>清空实体并释放所有子节点。</summary>
        public void Clear()
        {
            ClearNode(mRoot);
            mEntities.Clear();
            mEntityNodes.Clear();
            mOverflowEntities.Clear();
            mCount = 0;
#if UNITY_EDITOR || (GODOT && TOOLS)
            SpatialKitDiagnosticsRegistry.MarkChanged();
#endif
        }

    }
}
