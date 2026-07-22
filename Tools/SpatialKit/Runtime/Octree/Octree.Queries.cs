using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>提供八叉树的节点路由、查询递归和输入校验实现。</summary>
    public sealed partial class Octree<T> where T : ISpatialEntity
    {
        /// <summary>把实体按包围盒位置路由到叶节点并维护定位表。</summary>
        private void InsertToNode(OctreeNode node, T entity)
        {
            YokiVector3 position = SpatialMath.Clamp(entity.Position, node.Bounds.Min, node.Bounds.Max);
            if (node.IsLeaf)
            {
                node.AddEntity(entity);
                mEntityNodes[entity.SpatialId] = node;
                if (node.EntityCount > mMaxEntitiesPerNode && node.Depth < mMaxDepth)
                {
                    List<T> oldEntities = node.CopyEntities();
                    node.Split();
                    node.ClearEntities();
                    for (int index = 0; index < oldEntities.Count; index++)
                    {
                        InsertToNode(node, oldEntities[index]);
                    }
                }

                return;
            }

            InsertToNode(node.GetChild(node.GetChildIndex(position)), entity);
        }

        /// <summary>按定位节点移除实体并更新索引计数。</summary>
        private bool RemoveById(int spatialId)
        {
            if (!mEntities.ContainsKey(spatialId))
            {
                return false;
            }

            if (mOverflowEntities.Remove(spatialId))
            {
                mEntities.Remove(spatialId);
                mCount--;
#if UNITY_EDITOR || (GODOT && TOOLS)
                SpatialKitDiagnosticsRegistry.MarkChanged();
#endif
                return true;
            }

            if (!mEntityNodes.TryGetValue(spatialId, out OctreeNode node)
                || !node.RemoveEntity(spatialId))
            {
                throw new InvalidOperationException("Octree lost the entity node for ID " + spatialId + ".");
            }

            mEntityNodes.Remove(spatialId);
            mEntities.Remove(spatialId);
            mCount--;
#if UNITY_EDITOR || (GODOT && TOOLS)
            SpatialKitDiagnosticsRegistry.MarkChanged();
#endif
            return true;
        }

        /// <summary>递归清空节点并释放子树引用。</summary>
        private static void ClearNode(OctreeNode node)
        {
            if (node.IsLeaf)
            {
                node.ClearEntities();
                return;
            }

            OctreeNode[] children = node.GetChildrenArray();
            for (int index = 0; index < CHILD_COUNT; index++)
            {
                ClearNode(children[index]);
            }

            node.ClearChildren();
            node.ClearEntities();
        }

        /// <summary>统计当前树的节点数量，用于诊断。</summary>
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
            OctreeNode[] children = node.GetChildrenArray();
            for (int index = 0; index < CHILD_COUNT; index++)
            {
                count += CountNodes(children[index]);
            }

            return count;
        }

        /// <summary>递归执行球体相交裁剪和三维距离过滤。</summary>
        private static void QueryRadiusNode(OctreeNode node, YokiVector3 center, float radius, float radiusSquared, List<T> results)
        {
            if (!SpatialMath.IntersectsSphere(node.Bounds, center, radius))
            {
                return;
            }

            if (node.IsLeaf)
            {
                IReadOnlyList<T> entities = node.Entities;
                for (int index = 0; index < entities.Count; index++)
                {
                    if ((entities[index].Position - center).SqrMagnitude <= radiusSquared)
                    {
                        results.Add(entities[index]);
                    }
                }

                return;
            }

            OctreeNode[] children = node.GetChildrenArray();
            for (int index = 0; index < CHILD_COUNT; index++)
            {
                QueryRadiusNode(children[index], center, radius, radiusSquared, results);
            }
        }

        /// <summary>递归执行包围盒相交裁剪和实体包含过滤。</summary>
        private static void QueryBoundsNode(OctreeNode node, YokiBounds bounds, List<T> results)
        {
            if (!node.Bounds.Intersects(bounds))
            {
                return;
            }

            if (node.IsLeaf)
            {
                IReadOnlyList<T> entities = node.Entities;
                for (int index = 0; index < entities.Count; index++)
                {
                    if (bounds.Contains(entities[index].Position))
                    {
                        results.Add(entities[index]);
                    }
                }

                return;
            }

            OctreeNode[] children = node.GetChildrenArray();
            for (int index = 0; index < CHILD_COUNT; index++)
            {
                QueryBoundsNode(children[index], bounds, results);
            }
        }

        /// <summary>递归搜索最近实体，并按当前最小距离裁剪节点。</summary>
        private static void QueryNearestNode(
            OctreeNode node,
            YokiVector3 position,
            Func<T, bool> filter,
            ref T nearest,
            ref float nearestDistanceSquared,
            ref bool found)
        {
            if (SpatialMath.DistanceSquaredToBounds(node.Bounds, position) > nearestDistanceSquared)
            {
                return;
            }

            if (node.IsLeaf)
            {
                IReadOnlyList<T> entities = node.Entities;
                for (int index = 0; index < entities.Count; index++)
                {
                    T entity = entities[index];
                    if (filter != null && !filter(entity))
                    {
                        continue;
                    }

                    float distanceSquared = (entity.Position - position).SqrMagnitude;
                    if (distanceSquared <= nearestDistanceSquared)
                    {
                        nearest = entity;
                        nearestDistanceSquared = distanceSquared;
                        found = true;
                    }
                }

                return;
            }

            OctreeNode[] children = node.GetChildrenArray();
            for (int index = 0; index < CHILD_COUNT; index++)
            {
                QueryNearestNode(children[index], position, filter, ref nearest, ref nearestDistanceSquared, ref found);
            }
        }

        /// <summary>验证实体引用和位置。</summary>
        private static void ValidateEntity(T entity)
        {
            if (ReferenceEquals(entity, null))
            {
                throw new ArgumentNullException(nameof(entity));
            }

            SpatialMath.ValidatePosition(entity.Position, nameof(entity));
        }

        /// <summary>验证半径查询输入。</summary>
        private static void ValidateQuery(YokiVector3 center, float radius, List<T> results)
        {
            SpatialMath.ValidatePosition(center, nameof(center));
            if (float.IsNaN(radius) || float.IsInfinity(radius) || radius < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(radius), "Radius must be finite and non-negative.");
            }

            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }
        }

        /// <summary>验证最近邻最大距离。</summary>
        private static void ValidateDistance(float distance)
        {
            if (float.IsNaN(distance) || distance < 0f || float.IsNegativeInfinity(distance))
            {
                throw new ArgumentOutOfRangeException(nameof(distance), "Maximum distance must be non-negative.");
            }
        }
    }
}
