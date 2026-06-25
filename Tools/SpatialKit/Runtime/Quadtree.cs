using System;
using System.Collections.Generic;
using YokiFrame;

namespace YokiFrame
{
    /// <summary>
    /// 四叉树空间索引，适用于 2D 或 2.5D 投影查询。
    /// </summary>
    public partial class Quadtree<T> : ISpatialIndex<T>, ISpatialIndexDiagnostics where T : ISpatialEntity
    {
        private const int DEFAULT_MAX_DEPTH = 8;
        private const int DEFAULT_MAX_ENTITIES_PER_NODE = 8;
        private const int MIN_TREE_SETTING = 0;
        private const int CHILD_COUNT = 4;
        private const int INITIAL_NODE_CAPACITY = 4;
        private const float HALF_SIZE_MULTIPLIER = 0.5f;
        private const float MIN_QUERY_DISTANCE = 0f;

        private readonly string mDiagnosticsId;
        private readonly string mEntityTypeName;
        private readonly string mCreatedAtUtc;
        private readonly int mMaxDepth;
        private readonly int mMaxEntitiesPerNode;
        private readonly SpatialPlane mPlane;
        private readonly QuadtreeNode mRoot;
        private readonly Dictionary<int, T> mEntities = new();
        private int mCount;

        /// <summary>
        /// 创建四叉树空间索引。
        /// </summary>
        /// <param name="bounds">二维索引边界。</param>
        /// <param name="maxDepth">最大树深度。</param>
        /// <param name="maxEntitiesPerNode">单节点最大实体数。</param>
        /// <param name="plane">二维投影平面。</param>
        public Quadtree(
            YokiRect bounds,
            int maxDepth = DEFAULT_MAX_DEPTH,
            int maxEntitiesPerNode = DEFAULT_MAX_ENTITIES_PER_NODE,
            SpatialPlane plane = SpatialPlane.XZ)
        {
            if (maxDepth <= MIN_TREE_SETTING)
            {
                throw new ArgumentOutOfRangeException(nameof(maxDepth));
            }

            if (maxEntitiesPerNode <= MIN_TREE_SETTING)
            {
                throw new ArgumentOutOfRangeException(nameof(maxEntitiesPerNode));
            }

            mDiagnosticsId = SpatialKitDiagnosticsRegistry.NextIndexId("quadtree");
            mEntityTypeName = typeof(T).Name;
            mCreatedAtUtc = DateTime.UtcNow.ToString("O");
            mMaxDepth = maxDepth;
            mMaxEntitiesPerNode = maxEntitiesPerNode;
            mPlane = plane;
            mRoot = new QuadtreeNode(bounds, MIN_TREE_SETTING);
        }

        /// <inheritdoc />
        public int Count => mCount;

        /// <summary>
        /// 获取二维投影平面。
        /// </summary>
        public SpatialPlane Plane => mPlane;

        /// <summary>
        /// 获取四叉树根节点。
        /// </summary>
        public QuadtreeNode Root => mRoot;

        /// <inheritdoc />
        public string DiagnosticsId => mDiagnosticsId;

        /// <inheritdoc />
        public string IndexKind => "Quadtree";

        /// <inheritdoc />
        public string EntityTypeName => mEntityTypeName;

        /// <inheritdoc />
        public string PlaneName => mPlane.ToString();

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
        public bool HasBounds2D => true;

        /// <inheritdoc />
        public bool HasBounds3D => false;

        /// <inheritdoc />
        public YokiRect Bounds2D => mRoot.Bounds;

        /// <inheritdoc />
        public YokiBounds Bounds3D => default;

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

            float centerB = mPlane == SpatialPlane.XZ ? center.Z : center.Y;
            QueryRadiusNode(mRoot, center.X, centerB, radius, radius * radius, mPlane, results);
        }

        /// <inheritdoc />
        public void QueryBounds(YokiBounds bounds, List<T> results)
        {
            if (mCount == 0)
            {
                return;
            }

            float minB = mPlane == SpatialPlane.XZ ? bounds.Min.Z : bounds.Min.Y;
            float sizeB = mPlane == SpatialPlane.XZ ? bounds.Size.Z : bounds.Size.Y;
            var rect = new YokiRect(bounds.Min.X, minB, bounds.Size.X, sizeB);
            QueryBoundsNode(mRoot, rect, bounds, results);
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
            float posB = mPlane == SpatialPlane.XZ ? position.Z : position.Y;
            QueryNearestNode(mRoot, position.X, posB, position, filter, mPlane, ref nearest, ref nearestDistSq, ref found);
            return found ? nearest : default;
        }

        /// <inheritdoc />
        public void Clear()
        {
            ClearNode(mRoot);
            mEntities.Clear();
            mCount = 0;
        }

        private void InsertToNode(QuadtreeNode node, T entity)
        {
            var position = entity.Position;
            float posX = SpatialMath.Clamp(position.X, node.Bounds.XMin, node.Bounds.XMax);
            float posB = mPlane == SpatialPlane.XZ ? position.Z : position.Y;
            posB = SpatialMath.Clamp(posB, node.Bounds.YMin, node.Bounds.YMax);

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

            InsertToNode(node.Children[node.GetChildIndex(posX, posB)], entity);
        }

        private bool RemoveFromNode(QuadtreeNode node, T entity)
        {
            var position = entity.Position;
            float posX = SpatialMath.Clamp(position.X, node.Bounds.XMin, node.Bounds.XMax);
            float posB = mPlane == SpatialPlane.XZ ? position.Z : position.Y;
            posB = SpatialMath.Clamp(posB, node.Bounds.YMin, node.Bounds.YMax);

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

            return RemoveFromNode(node.Children[node.GetChildIndex(posX, posB)], entity);
        }

        private static void ClearNode(QuadtreeNode node)
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

        private static int CountNodes(QuadtreeNode node)
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

        private static void QueryRadiusNode(QuadtreeNode node, float centerX, float centerB, float radius, float radiusSq, SpatialPlane plane, List<T> results)
        {
            if (!SpatialMath.IntersectsCircle(node.Bounds, centerX, centerB, radius))
            {
                return;
            }

            if (node.IsLeaf)
            {
                for (int i = 0; i < node.Entities.Count; i++)
                {
                    var entity = node.Entities[i];
                    var position = entity.Position;
                    float dx = position.X - centerX;
                    float db = (plane == SpatialPlane.XZ ? position.Z : position.Y) - centerB;
                    if (dx * dx + db * db <= radiusSq)
                    {
                        results.Add(entity);
                    }
                }

                return;
            }

            for (int i = 0; i < CHILD_COUNT; i++)
            {
                QueryRadiusNode(node.Children[i], centerX, centerB, radius, radiusSq, plane, results);
            }
        }

        private static void QueryBoundsNode(QuadtreeNode node, YokiRect rect, YokiBounds bounds, List<T> results)
        {
            if (!node.Bounds.Overlaps(rect))
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
                QueryBoundsNode(node.Children[i], rect, bounds, results);
            }
        }

        private static void QueryNearestNode(QuadtreeNode node, float posX, float posB, YokiVector3 position, Func<T, bool> filter, SpatialPlane plane, ref T nearest, ref float nearestDistSq, ref bool found)
        {
            if (!SpatialMath.IntersectsCircle(node.Bounds, posX, posB, MathF.Sqrt(nearestDistSq)))
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
                QueryNearestNode(node.Children[i], posX, posB, position, filter, plane, ref nearest, ref nearestDistSq, ref found);
            }
        }
    }
}
