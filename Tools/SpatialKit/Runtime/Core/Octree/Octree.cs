using System;
using System.Collections.Generic;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 八叉树实现（完整 3D 空间索引）
    /// </summary>
    public class Octree<T> : ISpatialIndex<T> where T : ISpatialEntity
    {
        private readonly Bounds mBounds;
        private readonly int mMaxDepth;
        private readonly int mMaxEntitiesPerNode;
        private readonly OctreeNode mRoot;
        private readonly Dictionary<int, T> mEntities = new();
        private int mCount;

        public int Count => mCount;

        /// <summary>
        /// 创建八叉树
        /// </summary>
        /// <param name="bounds">3D 世界边界</param>
        /// <param name="maxDepth">最大深度，默认 8</param>
        /// <param name="maxEntitiesPerNode">节点分裂阈值，默认 8</param>
        public Octree(Bounds bounds, int maxDepth = 8, int maxEntitiesPerNode = 8)
        {
            if (maxDepth <= 0) throw new ArgumentOutOfRangeException(nameof(maxDepth));
            if (maxEntitiesPerNode <= 0) throw new ArgumentOutOfRangeException(nameof(maxEntitiesPerNode));

            mBounds = bounds;
            mMaxDepth = maxDepth;
            mMaxEntitiesPerNode = maxEntitiesPerNode;
            mRoot = new OctreeNode(bounds, 0);
        }

        /// <summary>
        /// 插入实体到八叉树
        /// </summary>
        /// <param name="entity">要插入的实体，相同 SpatialId 会覆盖旧实体</param>
        public void Insert(T entity)
        {
            var id = entity.SpatialId;
            if (mEntities.ContainsKey(id)) Remove(entity);
            mEntities[id] = entity;
            InsertToNode(mRoot, entity);
            mCount++;
        }

        /// <summary>
        /// 从八叉树中移除实体
        /// </summary>
        /// <param name="entity">要移除的实体</param>
        /// <returns>移除成功返回 true，实体不存在返回 false</returns>
        public bool Remove(T entity)
        {
            var id = entity.SpatialId;
            if (!mEntities.TryGetValue(id, out var stored)) return false;
            mEntities.Remove(id);
            RemoveFromNode(mRoot, stored);
            mCount--;
            return true;
        }

        /// <summary>
        /// 更新实体位置（实体移动后必须调用）
        /// </summary>
        /// <param name="entity">位置已变化的实体（struct 需创建新实例）</param>
        public void Update(T entity)
        {
            var id = entity.SpatialId;
            if (mEntities.TryGetValue(id, out var stored))
            {
                RemoveFromNode(mRoot, stored);
                mEntities[id] = entity;
                InsertToNode(mRoot, entity);
            }
            else Insert(entity);
        }
        
        /// <summary>
        /// 批量更新实体位置
        /// </summary>
        /// <param name="entities">位置已变化的实体列表</param>
        public void UpdateBatch(IReadOnlyList<T> entities)
        {
            for (int i = 0; i < entities.Count; i++)
            {
                Update(entities[i]);
            }
        }

        /// <summary>
        /// 清空所有实体
        /// </summary>
        public void Clear()
        {
            ClearNode(mRoot);
            mEntities.Clear();
            mCount = 0;
        }

        /// <summary>
        /// 球形范围查询（零 GC）
        /// </summary>
        /// <param name="center">查询中心点</param>
        /// <param name="radius">查询半径</param>
        /// <param name="results">结果输出列表（调用前应清空）</param>
        public void QueryRadius(Vector3 center, float radius, List<T> results)
        {
            if (mCount == 0) return;
            QueryRadiusNode(mRoot, center, radius, radius * radius, results);
        }

        /// <summary>
        /// AABB 范围查询（零 GC）
        /// </summary>
        /// <param name="bounds">查询边界</param>
        /// <param name="results">结果输出列表（调用前应清空）</param>
        public void QueryBounds(Bounds bounds, List<T> results)
        {
            if (mCount == 0) return;
            QueryBoundsNode(mRoot, bounds, results);
        }

        /// <summary>
        /// 最近邻查询
        /// </summary>
        /// <param name="position">查询位置</param>
        /// <param name="maxDistance">最大搜索距离，默认无限制</param>
        /// <param name="filter">可选过滤条件（使用 static lambda 避免 GC）</param>
        /// <returns>最近的实体，无结果返回 default(T)</returns>
        public T QueryNearest(Vector3 position, float maxDistance = float.MaxValue, Func<T, bool> filter = null)
        {
            if (mCount == 0) return default;
            T nearest = default;
            float nearestDistSq = maxDistance * maxDistance;
            QueryNearestNode(mRoot, position, filter, ref nearest, ref nearestDistSq);
            return nearest;
        }

        /// <summary>根节点（仅供编辑器可视化）</summary>
        public OctreeNode Root => mRoot;


        private void InsertToNode(OctreeNode node, T entity)
        {
            var pos = entity.Position;
            if (!node.Bounds.Contains(pos))
                pos = new Vector3(
                    Mathf.Clamp(pos.x, node.Bounds.min.x, node.Bounds.max.x),
                    Mathf.Clamp(pos.y, node.Bounds.min.y, node.Bounds.max.y),
                    Mathf.Clamp(pos.z, node.Bounds.min.z, node.Bounds.max.z));

            if (node.IsLeaf)
            {
                node.Entities.Add(entity);
                if (node.Entities.Count > mMaxEntitiesPerNode && node.Depth < mMaxDepth)
                {
                    node.Split();
                    var entities = node.Entities;
                    node.Entities = new List<T>();
                    for (int i = 0; i < entities.Count; i++)
                        InsertToNode(node, entities[i]);
                }
            }
            else
            {
                int childIndex = node.GetChildIndex(pos);
                InsertToNode(node.Children[childIndex], entity);
            }
        }

        private bool RemoveFromNode(OctreeNode node, T entity)
        {
            var pos = entity.Position;
            pos = new Vector3(
                Mathf.Clamp(pos.x, node.Bounds.min.x, node.Bounds.max.x),
                Mathf.Clamp(pos.y, node.Bounds.min.y, node.Bounds.max.y),
                Mathf.Clamp(pos.z, node.Bounds.min.z, node.Bounds.max.z));

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
            int childIndex = node.GetChildIndex(pos);
            return RemoveFromNode(node.Children[childIndex], entity);
        }

        private void ClearNode(OctreeNode node)
        {
            if (node.IsLeaf) { node.Entities.Clear(); return; }
            for (int i = 0; i < 8; i++) { ClearNode(node.Children[i]); node.Children[i] = null; }
            node.Children = null;
            node.Entities = new List<T>(4);
        }

        private static void QueryRadiusNode(OctreeNode node, Vector3 center, float r, float rSq, List<T> results)
        {
            if (!node.IntersectsSphere(center, r)) return;
            if (node.IsLeaf)
            {
                for (int i = 0; i < node.Entities.Count; i++)
                {
                    var entity = node.Entities[i];
                    if ((entity.Position - center).sqrMagnitude <= rSq) results.Add(entity);
                }
            }
            else { for (int i = 0; i < 8; i++) QueryRadiusNode(node.Children[i], center, r, rSq, results); }
        }

        private static void QueryBoundsNode(OctreeNode node, Bounds bounds, List<T> results)
        {
            if (!node.IntersectsBounds(bounds)) return;
            if (node.IsLeaf)
            {
                for (int i = 0; i < node.Entities.Count; i++)
                {
                    var entity = node.Entities[i];
                    if (bounds.Contains(entity.Position)) results.Add(entity);
                }
            }
            else { for (int i = 0; i < 8; i++) QueryBoundsNode(node.Children[i], bounds, results); }
        }

        private static void QueryNearestNode(OctreeNode node, Vector3 pos, Func<T, bool> filter, ref T nearest, ref float nearestDistSq)
        {
            if (!node.IntersectsSphere(pos, Mathf.Sqrt(nearestDistSq))) return;
            if (node.IsLeaf)
            {
                for (int i = 0; i < node.Entities.Count; i++)
                {
                    var entity = node.Entities[i];
                    if (filter != null && !filter(entity)) continue;
                    float distSq = (entity.Position - pos).sqrMagnitude;
                    if (distSq < nearestDistSq) { nearestDistSq = distSq; nearest = entity; }
                }
            }
            else { for (int i = 0; i < 8; i++) QueryNearestNode(node.Children[i], pos, filter, ref nearest, ref nearestDistSq); }
        }

        /// <summary>八叉树节点（仅供编辑器可视化）</summary>
        public class OctreeNode
        {
            public Bounds Bounds;
            public int Depth;
            public List<T> Entities;
            public OctreeNode[] Children;
            public bool IsLeaf => Children == null;

            public OctreeNode(Bounds bounds, int depth) { Bounds = bounds; Depth = depth; Entities = new List<T>(4); }

            public void Split()
            {
                var center = Bounds.center;
                var halfSize = Bounds.extents * 0.5f;
                int d = Depth + 1;
                Children = new OctreeNode[8];
                for (int i = 0; i < 8; i++)
                {
                    var offset = new Vector3(
                        (i & 1) == 0 ? -halfSize.x : halfSize.x,
                        (i & 2) == 0 ? -halfSize.y : halfSize.y,
                        (i & 4) == 0 ? -halfSize.z : halfSize.z);
                    Children[i] = new OctreeNode(new Bounds(center + offset, Bounds.extents), d);
                }
            }

            public int GetChildIndex(Vector3 pos)
            {
                var center = Bounds.center;
                int index = 0;
                if (pos.x >= center.x) index |= 1;
                if (pos.y >= center.y) index |= 2;
                if (pos.z >= center.z) index |= 4;
                return index;
            }

            public bool IntersectsSphere(Vector3 center, float radius)
            {
                var closest = new Vector3(
                    Mathf.Clamp(center.x, Bounds.min.x, Bounds.max.x),
                    Mathf.Clamp(center.y, Bounds.min.y, Bounds.max.y),
                    Mathf.Clamp(center.z, Bounds.min.z, Bounds.max.z));
                return (center - closest).sqrMagnitude <= radius * radius;
            }

            public bool IntersectsBounds(Bounds other) => Bounds.Intersects(other);
        }
    }
}
