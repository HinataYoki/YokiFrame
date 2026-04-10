using System;
using System.Collections.Generic;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 四叉树实现（2D/2.5D 空间索引）
    /// </summary>
    public class Quadtree<T> : ISpatialIndex<T> where T : ISpatialEntity
    {
        private readonly Rect mBounds;
        private readonly int mMaxDepth;
        private readonly int mMaxEntitiesPerNode;
        private readonly SpatialPlane mPlane;
        private readonly QuadtreeNode mRoot;
        private readonly Dictionary<int, T> mEntities = new();
        private int mCount;

        public int Count => mCount;
        
        /// <summary>
        /// 当前使用的投影平面
        /// </summary>
        public SpatialPlane Plane => mPlane;

        /// <summary>
        /// 创建四叉树
        /// </summary>
        /// <param name="bounds">2D 边界（对于 XZ 平面，y 对应 Z 轴；对于 XY 平面，y 对应 Y 轴）</param>
        /// <param name="maxDepth">最大深度</param>
        /// <param name="maxEntitiesPerNode">节点分裂阈值</param>
        /// <param name="plane">投影平面，XZ 用于 2.5D，XY 用于传统 2D</param>
        public Quadtree(Rect bounds, int maxDepth = 8, int maxEntitiesPerNode = 8, SpatialPlane plane = SpatialPlane.XZ)
        {
            if (maxDepth <= 0) throw new ArgumentOutOfRangeException(nameof(maxDepth));
            if (maxEntitiesPerNode <= 0) throw new ArgumentOutOfRangeException(nameof(maxEntitiesPerNode));

            mBounds = bounds;
            mMaxDepth = maxDepth;
            mMaxEntitiesPerNode = maxEntitiesPerNode;
            mPlane = plane;
            mRoot = new QuadtreeNode(bounds, 0);
        }

        /// <summary>
        /// 插入实体到四叉树
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
        /// 从四叉树中移除实体
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
            float centerB = mPlane == SpatialPlane.XZ ? center.z : center.y;
            QueryRadiusNode(mRoot, center.x, centerB, radius, radius * radius, mPlane, results);
        }

        /// <summary>
        /// AABB 范围查询（零 GC）
        /// </summary>
        /// <param name="bounds">查询边界</param>
        /// <param name="results">结果输出列表（调用前应清空）</param>
        public void QueryBounds(Bounds bounds, List<T> results)
        {
            if (mCount == 0) return;
            float minB = mPlane == SpatialPlane.XZ ? bounds.min.z : bounds.min.y;
            float sizeB = mPlane == SpatialPlane.XZ ? bounds.size.z : bounds.size.y;
            var rect = new Rect(bounds.min.x, minB, bounds.size.x, sizeB);
            QueryBoundsNode(mRoot, rect, bounds, mPlane, results);
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
            float posB = mPlane == SpatialPlane.XZ ? position.z : position.y;
            QueryNearestNode(mRoot, position.x, posB, position, filter, mPlane, ref nearest, ref nearestDistSq);
            return nearest;
        }

        /// <summary>根节点（仅供编辑器可视化）</summary>
        public QuadtreeNode Root => mRoot;


        private void InsertToNode(QuadtreeNode node, T entity)
        {
            var pos = entity.Position;
            float posX = Mathf.Clamp(pos.x, node.Bounds.xMin, node.Bounds.xMax);
            float posB = mPlane == SpatialPlane.XZ ? pos.z : pos.y;
            posB = Mathf.Clamp(posB, node.Bounds.yMin, node.Bounds.yMax);

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
                int childIndex = node.GetChildIndex(posX, posB);
                InsertToNode(node.Children[childIndex], entity);
            }
        }

        private bool RemoveFromNode(QuadtreeNode node, T entity)
        {
            var pos = entity.Position;
            float posX = Mathf.Clamp(pos.x, node.Bounds.xMin, node.Bounds.xMax);
            float posB = mPlane == SpatialPlane.XZ ? pos.z : pos.y;
            posB = Mathf.Clamp(posB, node.Bounds.yMin, node.Bounds.yMax);

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
            int childIndex = node.GetChildIndex(posX, posB);
            return RemoveFromNode(node.Children[childIndex], entity);
        }

        private void ClearNode(QuadtreeNode node)
        {
            if (node.IsLeaf) { node.Entities.Clear(); return; }
            for (int i = 0; i < 4; i++) { ClearNode(node.Children[i]); node.Children[i] = null; }
            node.Children = null;
            node.Entities = new List<T>(4);
        }

        private static void QueryRadiusNode(QuadtreeNode node, float cx, float cb, float r, float rSq, SpatialPlane plane, List<T> results)
        {
            if (!node.IntersectsCircle(cx, cb, r)) return;
            if (node.IsLeaf)
            {
                for (int i = 0; i < node.Entities.Count; i++)
                {
                    var entity = node.Entities[i];
                    var pos = entity.Position;
                    float dx = pos.x - cx;
                    float db = (plane == SpatialPlane.XZ ? pos.z : pos.y) - cb;
                    if (dx * dx + db * db <= rSq) results.Add(entity);
                }
            }
            else { for (int i = 0; i < 4; i++) QueryRadiusNode(node.Children[i], cx, cb, r, rSq, plane, results); }
        }

        private static void QueryBoundsNode(QuadtreeNode node, Rect rect, Bounds bounds, SpatialPlane plane, List<T> results)
        {
            if (!node.IntersectsRect(rect)) return;
            if (node.IsLeaf)
            {
                for (int i = 0; i < node.Entities.Count; i++)
                {
                    var entity = node.Entities[i];
                    var pos = entity.Position;
                    if (pos.x >= bounds.min.x && pos.x <= bounds.max.x &&
                        pos.y >= bounds.min.y && pos.y <= bounds.max.y &&
                        pos.z >= bounds.min.z && pos.z <= bounds.max.z)
                        results.Add(entity);
                }
            }
            else { for (int i = 0; i < 4; i++) QueryBoundsNode(node.Children[i], rect, bounds, plane, results); }
        }

        private static void QueryNearestNode(QuadtreeNode node, float posX, float posB, Vector3 pos, Func<T, bool> filter, SpatialPlane plane, ref T nearest, ref float nearestDistSq)
        {
            if (!node.IntersectsCircle(posX, posB, Mathf.Sqrt(nearestDistSq))) return;
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
            else { for (int i = 0; i < 4; i++) QueryNearestNode(node.Children[i], posX, posB, pos, filter, plane, ref nearest, ref nearestDistSq); }
        }

        /// <summary>四叉树节点（仅供编辑器可视化）</summary>
        public class QuadtreeNode
        {
            public Rect Bounds;
            public int Depth;
            public List<T> Entities;
            public QuadtreeNode[] Children;
            public bool IsLeaf => Children == null;

            public QuadtreeNode(Rect bounds, int depth) { Bounds = bounds; Depth = depth; Entities = new List<T>(4); }

            public void Split()
            {
                float halfW = Bounds.width * 0.5f, halfH = Bounds.height * 0.5f;
                float x = Bounds.x, y = Bounds.y;
                int d = Depth + 1;
                Children = new QuadtreeNode[4];
                Children[0] = new QuadtreeNode(new Rect(x, y, halfW, halfH), d);
                Children[1] = new QuadtreeNode(new Rect(x + halfW, y, halfW, halfH), d);
                Children[2] = new QuadtreeNode(new Rect(x, y + halfH, halfW, halfH), d);
                Children[3] = new QuadtreeNode(new Rect(x + halfW, y + halfH, halfW, halfH), d);
            }

            public int GetChildIndex(float posX, float posZ)
            {
                float midX = Bounds.x + Bounds.width * 0.5f, midZ = Bounds.y + Bounds.height * 0.5f;
                int index = 0;
                if (posX >= midX) index |= 1;
                if (posZ >= midZ) index |= 2;
                return index;
            }

            public bool IntersectsCircle(float cx, float cz, float r)
            {
                float closestX = Mathf.Clamp(cx, Bounds.xMin, Bounds.xMax);
                float closestZ = Mathf.Clamp(cz, Bounds.yMin, Bounds.yMax);
                float dx = cx - closestX, dz = cz - closestZ;
                return dx * dx + dz * dz <= r * r;
            }

            public bool IntersectsRect(Rect other) => Bounds.Overlaps(other);
        }
    }
}
