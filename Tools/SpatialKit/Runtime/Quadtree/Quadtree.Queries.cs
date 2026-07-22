using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>提供四叉树的节点路由、查询递归和输入校验实现。</summary>
    public sealed partial class Quadtree<T> where T : ISpatialEntity
    {
        /// <summary>把实体按投影位置路由到叶节点，并维护 ID 定位表。</summary>
        private void InsertToNode(QuadtreeNode node, T entity)
        {
            float positionX = SpatialMath.Clamp(entity.Position.X, node.Bounds.XMin, node.Bounds.XMax);
            float positionB = SpatialMath.Clamp(
                SpatialMath.GetPlaneCoordinate(entity.Position, mPlane),
                node.Bounds.YMin,
                node.Bounds.YMax);

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

            InsertToNode(node.GetChild(node.GetChildIndex(positionX, positionB)), entity);
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

            if (!mEntityNodes.TryGetValue(spatialId, out QuadtreeNode node)
                || !node.RemoveEntity(spatialId))
            {
                throw new InvalidOperationException("Quadtree lost the entity node for ID " + spatialId + ".");
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
        private static void ClearNode(QuadtreeNode node)
        {
            if (node.IsLeaf)
            {
                node.ClearEntities();
                return;
            }

            QuadtreeNode[] children = node.GetChildrenArray();
            for (int index = 0; index < CHILD_COUNT; index++)
            {
                ClearNode(children[index]);
            }

            node.ClearChildren();
            node.ClearEntities();
        }

        /// <summary>统计当前树的节点数量，用于诊断。</summary>
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
            QuadtreeNode[] children = node.GetChildrenArray();
            for (int index = 0; index < CHILD_COUNT; index++)
            {
                count += CountNodes(children[index]);
            }

            return count;
        }

        /// <summary>递归执行圆形投影相交裁剪和实体距离过滤。</summary>
        private void QueryRadiusNode(
            QuadtreeNode node,
            float centerX,
            float centerB,
            float radius,
            float radiusSquared,
            List<T> results)
        {
            if (!SpatialMath.IntersectsCircle(node.Bounds, centerX, centerB, radius))
            {
                return;
            }

            if (node.IsLeaf)
            {
                IReadOnlyList<T> entities = node.Entities;
                for (int index = 0; index < entities.Count; index++)
                {
                    YokiVector3 entityPosition = entities[index].Position;
                    float deltaA = entityPosition.X - centerX;
                    float deltaB = SpatialMath.GetPlaneCoordinate(entityPosition, mPlane) - centerB;
                    if (deltaA * deltaA + deltaB * deltaB <= radiusSquared)
                    {
                        results.Add(entities[index]);
                    }
                }

                return;
            }

            QuadtreeNode[] children = node.GetChildrenArray();
            for (int index = 0; index < CHILD_COUNT; index++)
            {
                QueryRadiusNode(children[index], centerX, centerB, radius, radiusSquared, results);
            }
        }

        /// <summary>递归执行二维节点与三维查询包围盒的双重过滤。</summary>
        private void QueryBoundsNode(QuadtreeNode node, YokiRect rect, YokiBounds bounds, List<T> results)
        {
            if (!node.Bounds.Overlaps(rect))
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

            QuadtreeNode[] children = node.GetChildrenArray();
            for (int index = 0; index < CHILD_COUNT; index++)
            {
                QueryBoundsNode(children[index], rect, bounds, results);
            }
        }

        /// <summary>递归搜索最近投影实体，并用当前最小距离裁剪节点。</summary>
        private void QueryNearestNode(
            QuadtreeNode node,
            float positionX,
            float positionB,
            YokiVector3 position,
            Func<T, bool> filter,
            ref T nearest,
            ref float nearestDistanceSquared,
            ref bool found)
        {
            if (SpatialMath.DistanceSquaredToRect(node.Bounds, positionX, positionB) > nearestDistanceSquared)
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

                    float distanceSquared = SpatialMath.GetProjectedDistanceSquared(entity.Position, position, mPlane);
                    if (distanceSquared <= nearestDistanceSquared)
                    {
                        nearest = entity;
                        nearestDistanceSquared = distanceSquared;
                        found = true;
                    }
                }

                return;
            }

            QuadtreeNode[] children = node.GetChildrenArray();
            for (int index = 0; index < CHILD_COUNT; index++)
            {
                QueryNearestNode(children[index], positionX, positionB, position, filter, ref nearest, ref nearestDistanceSquared, ref found);
            }
        }

        /// <summary>验证实体位置和引用。</summary>
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

