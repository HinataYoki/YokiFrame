using System;
using System.Collections.Generic;
using YokiFrame;

namespace YokiFrame
{
    /// <summary>
    /// 八叉树空间索引，适用于完整 3D 查询。
    /// </summary>
    public partial class Octree<T> : ISpatialIndex<T>, ISpatialIndexDiagnostics where T : ISpatialEntity
    {
        private const int DEFAULT_MAX_DEPTH = 8;
        private const int DEFAULT_MAX_ENTITIES_PER_NODE = 8;
        private const int MIN_TREE_SETTING = 0;
        private const int CHILD_COUNT = 8;
        private const int INITIAL_NODE_CAPACITY = 4;
        private const float HALF_SIZE_MULTIPLIER = 0.5f;
        private const float MIN_QUERY_DISTANCE = 0f;

        private readonly string mDiagnosticsId;
        private readonly string mEntityTypeName;
        private readonly string mCreatedAtUtc;
        private readonly int mMaxDepth;
        private readonly int mMaxEntitiesPerNode;
        private readonly OctreeNode mRoot;
        private readonly Dictionary<int, T> mEntities = new();
        private int mCount;

        /// <summary>
        /// 创建八叉树空间索引。
        /// </summary>
        /// <param name="bounds">三维索引边界。</param>
        /// <param name="maxDepth">最大树深度。</param>
        /// <param name="maxEntitiesPerNode">单节点最大实体数。</param>
        public Octree(
            YokiBounds bounds,
            int maxDepth = DEFAULT_MAX_DEPTH,
            int maxEntitiesPerNode = DEFAULT_MAX_ENTITIES_PER_NODE)
        {
            if (maxDepth <= MIN_TREE_SETTING)
            {
                throw new ArgumentOutOfRangeException(nameof(maxDepth));
            }

            if (maxEntitiesPerNode <= MIN_TREE_SETTING)
            {
                throw new ArgumentOutOfRangeException(nameof(maxEntitiesPerNode));
            }

            mDiagnosticsId = SpatialKitDiagnosticsRegistry.NextIndexId("octree");
            mEntityTypeName = typeof(T).Name;
            mCreatedAtUtc = DateTime.UtcNow.ToString("O");
            mMaxDepth = maxDepth;
            mMaxEntitiesPerNode = maxEntitiesPerNode;
            mRoot = new OctreeNode(bounds, MIN_TREE_SETTING);
        }

        /// <inheritdoc />
        public int Count => mCount;

        /// <summary>
        /// 获取八叉树根节点。
        /// </summary>
        public OctreeNode Root => mRoot;

        /// <inheritdoc />
        public string DiagnosticsId => mDiagnosticsId;

        /// <inheritdoc />
        public string IndexKind => "Octree";

        /// <inheritdoc />
        public string EntityTypeName => mEntityTypeName;

        /// <inheritdoc />
        public string PlaneName => string.Empty;

        /// <inheritdoc />
        public float CellSize => MIN_QUERY_DISTANCE;

        /// <inheritdoc />
        public int MaxDepth => mMaxDepth;

        /// <inheritdoc />
        public int MaxEntitiesPerNode => mMaxEntitiesPerNode;

        /// <inheritdoc />
        public int PartitionCount => CountNodes(mRoot);

        /// <inheritdoc />
        public bool HasCellSize => false;

        /// <inheritdoc />
        public bool HasBounds2D => false;

        /// <inheritdoc />
        public bool HasBounds3D => true;

        /// <inheritdoc />
        public YokiRect Bounds2D => default;

        /// <inheritdoc />
        public YokiBounds Bounds3D => mRoot.Bounds;

        /// <inheritdoc />
        public string CreatedAtUtc => mCreatedAtUtc;

        /// <inheritdoc />
        public void Insert(T entity)
        {
            int id = entity.SpatialId;
            if (mEntities.ContainsKey(id))
            {
                Remove(entity);
            }

            mEntities[id] = entity;
            InsertToNode(mRoot, entity);
            mCount++;
        }

        /// <inheritdoc />
        public bool Remove(T entity)
        {
            int id = entity.SpatialId;
            if (!mEntities.TryGetValue(id, out var stored))
            {
                return false;
            }

            mEntities.Remove(id);
            RemoveFromNode(mRoot, stored);
            mCount--;
            return true;
        }

        /// <inheritdoc />
        public void Update(T entity)
        {
            int id = entity.SpatialId;
            if (mEntities.TryGetValue(id, out var stored))
            {
                RemoveFromNode(mRoot, stored);
                mEntities[id] = entity;
                InsertToNode(mRoot, entity);
                return;
            }

            Insert(entity);
        }

        /// <inheritdoc />
        public void UpdateBatch(IReadOnlyList<T> entities)
        {
            for (int i = 0; i < entities.Count; i++)
            {
                Update(entities[i]);
            }
        }

        /// <inheritdoc />
        public void QueryRadius(YokiVector3 center, float radius, List<T> results)
        {
            if (mCount == 0 || radius < MIN_QUERY_DISTANCE)
            {
                return;
            }

            QueryRadiusNode(mRoot, center, radius, radius * radius, results);
        }

        /// <inheritdoc />
        public void QueryBounds(YokiBounds bounds, List<T> results)
        {
            if (mCount == 0)
            {
                return;
            }

            QueryBoundsNode(mRoot, bounds, results);
        }

        /// <inheritdoc />
        public T QueryNearest(YokiVector3 position, float maxDistance = float.MaxValue, Func<T, bool> filter = null)
        {
            if (mCount == 0)
            {
                return default;
            }

            if (maxDistance < MIN_QUERY_DISTANCE)
            {
                maxDistance = MIN_QUERY_DISTANCE;
            }

            T nearest = default;
            float nearestDistSq = SpatialMath.IsUnboundedDistance(maxDistance)
                ? float.MaxValue
                : maxDistance * maxDistance;
            bool found = false;
            QueryNearestNode(mRoot, position, filter, ref nearest, ref nearestDistSq, ref found);
            return found ? nearest : default;
        }

        /// <inheritdoc />
        public void Clear()
        {
            ClearNode(mRoot);
            mEntities.Clear();
            mCount = 0;
        }

        private void InsertToNode(OctreeNode node, T entity)
        {
            var position = entity.Position;
            if (!node.Bounds.Contains(position))
            {
                position = SpatialMath.Clamp(position, node.Bounds.Min, node.Bounds.Max);
            }

            if (node.IsLeaf)
            {
                node.Entities.Add(entity);
                if (node.Entities.Count > mMaxEntitiesPerNode && node.Depth < mMaxDepth)
                {
                    node.Split();
                    var oldEntities = node.Entities;
                    node.Entities = new List<T>(INITIAL_NODE_CAPACITY);
                    for (int i = 0; i < oldEntities.Count; i++)
                    {
                        InsertToNode(node, oldEntities[i]);
                    }
                }

                return;
            }

            InsertToNode(node.Children[node.GetChildIndex(position)], entity);
        }

        private bool RemoveFromNode(OctreeNode node, T entity)
        {
            var position = SpatialMath.Clamp(entity.Position, node.Bounds.Min, node.Bounds.Max);
            if (node.IsLeaf)
            {
                for (int i = node.Entities.Count - 1; i >= 0; i--)
                {
                    if (node.Entities[i].SpatialId == entity.SpatialId)
                    {
                        node.Entities.RemoveAt(i);
                        return true;
                    }
                }

                return false;
            }

            return RemoveFromNode(node.Children[node.GetChildIndex(position)], entity);
        }

        private static void ClearNode(OctreeNode node)
        {
            if (node.IsLeaf)
            {
                node.Entities.Clear();
                return;
            }

            for (int i = 0; i < CHILD_COUNT; i++)
            {
                ClearNode(node.Children[i]);
                node.Children[i] = null;
            }

            node.Children = null;
            node.Entities = new List<T>(INITIAL_NODE_CAPACITY);
        }

        private static int CountNodes(OctreeNode node)
        {
            if (node == null)
            {
                return 0;
            }

            if (node.IsLeaf)
            {
                return 1;
            }

            int count = 1;
            for (int i = 0; i < CHILD_COUNT; i++)
            {
                count += CountNodes(node.Children[i]);
            }

            return count;
        }

        private static void QueryRadiusNode(OctreeNode node, YokiVector3 center, float radius, float radiusSq, List<T> results)
        {
            if (!SpatialMath.IntersectsSphere(node.Bounds, center, radius))
            {
                return;
            }

            if (node.IsLeaf)
            {
                for (int i = 0; i < node.Entities.Count; i++)
                {
                    var entity = node.Entities[i];
                    if ((entity.Position - center).SqrMagnitude <= radiusSq)
                    {
                        results.Add(entity);
                    }
                }

                return;
            }

            for (int i = 0; i < CHILD_COUNT; i++)
            {
                QueryRadiusNode(node.Children[i], center, radius, radiusSq, results);
            }
        }

        private static void QueryBoundsNode(OctreeNode node, YokiBounds bounds, List<T> results)
        {
            if (!node.Bounds.Intersects(bounds))
            {
                return;
            }

            if (node.IsLeaf)
            {
                for (int i = 0; i < node.Entities.Count; i++)
                {
                    var entity = node.Entities[i];
                    if (bounds.Contains(entity.Position))
                    {
                        results.Add(entity);
                    }
                }

                return;
            }

            for (int i = 0; i < CHILD_COUNT; i++)
            {
                QueryBoundsNode(node.Children[i], bounds, results);
            }
        }

        private static void QueryNearestNode(OctreeNode node, YokiVector3 position, Func<T, bool> filter, ref T nearest, ref float nearestDistSq, ref bool found)
        {
            if (!SpatialMath.IntersectsSphere(node.Bounds, position, MathF.Sqrt(nearestDistSq)))
            {
                return;
            }

            if (node.IsLeaf)
            {
                for (int i = 0; i < node.Entities.Count; i++)
                {
                    var entity = node.Entities[i];
                    if (filter != null && !filter(entity))
                    {
                        continue;
                    }

                    float distSq = (entity.Position - position).SqrMagnitude;
                    if (distSq <= nearestDistSq)
                    {
                        nearestDistSq = distSq;
                        nearest = entity;
                        found = true;
                    }
                }

                return;
            }

            for (int i = 0; i < CHILD_COUNT; i++)
            {
                QueryNearestNode(node.Children[i], position, filter, ref nearest, ref nearestDistSq, ref found);
            }
        }
    }
}
