using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>封装 Quadtree 的只读节点视图和内部分裂操作。</summary>
    public sealed partial class Quadtree<T> where T : ISpatialEntity
    {
        private readonly Dictionary<int, T> mOverflowEntities = new Dictionary<int, T>();

        /// <summary>
        /// 判断实体投影位置是否落在根节点边界内；越界实体走独立溢出查询路径。
        /// </summary>
        /// <param name="position">需要判断的实体位置。</param>
        /// <returns>投影位置在根边界闭区间内时返回 true。</returns>
        private bool IsInsideRoot(YokiVector3 position)
        {
            float positionB = SpatialMath.GetPlaneCoordinate(position, mPlane);
            return position.X >= mRoot.Bounds.XMin
                && position.X <= mRoot.Bounds.XMax
                && positionB >= mRoot.Bounds.YMin
                && positionB <= mRoot.Bounds.YMax;
        }

        /// <summary>把溢出实体中满足投影半径的对象追加到查询结果。</summary>
        private void AppendOverflowRadius(YokiVector3 center, float radiusSquared, List<T> results)
        {
            float centerB = SpatialMath.GetPlaneCoordinate(center, mPlane);
            foreach (T entity in mOverflowEntities.Values)
            {
                float deltaA = entity.Position.X - center.X;
                float deltaB = SpatialMath.GetPlaneCoordinate(entity.Position, mPlane) - centerB;
                if (deltaA * deltaA + deltaB * deltaB <= radiusSquared)
                {
                    results.Add(entity);
                }
            }
        }

        /// <summary>把溢出实体中包含在完整三维包围盒内的对象追加到查询结果。</summary>
        private void AppendOverflowBounds(YokiBounds bounds, List<T> results)
        {
            foreach (T entity in mOverflowEntities.Values)
            {
                if (bounds.Contains(entity.Position))
                {
                    results.Add(entity);
                }
            }
        }

        /// <summary>扫描溢出实体并参与最近邻比较。</summary>
        private void QueryNearestOverflow(
            YokiVector3 position,
            Func<T, bool> filter,
            ref T nearest,
            ref float nearestDistanceSquared,
            ref bool found)
        {
            foreach (T entity in mOverflowEntities.Values)
            {
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
        }

        /// <summary>表示一个四叉树节点。</summary>
        public sealed class QuadtreeNode
        {
            private const int CHILD_COUNT = 4;
            private const float HALF_SIZE = 0.5f;
            private readonly List<T> mEntities;
            private QuadtreeNode[] mChildren;

            /// <summary>创建四叉树节点。</summary>
            /// <param name="bounds">节点边界。</param>
            /// <param name="depth">节点深度。</param>
            internal QuadtreeNode(YokiRect bounds, int depth)
            {
                Bounds = bounds;
                Depth = depth;
                mEntities = new List<T>(4);
            }

            /// <summary>获取节点边界。</summary>
            public YokiRect Bounds { get; }

            /// <summary>获取节点深度。</summary>
            public int Depth { get; }

            /// <summary>获取节点是否为叶节点。</summary>
            public bool IsLeaf { get { return mChildren == null; } }

            /// <summary>获取叶节点实体的只读视图。</summary>
            public IReadOnlyList<T> Entities { get { return mEntities; } }

            /// <summary>获取子节点只读视图；叶节点返回空列表。</summary>
            public IReadOnlyList<QuadtreeNode> Children
            {
                get { return mChildren ?? Array.Empty<QuadtreeNode>(); }
            }

            /// <summary>获取当前节点实体数量。</summary>
            internal int EntityCount { get { return mEntities.Count; } }

            /// <summary>把实体追加到当前叶节点。</summary>
            internal void AddEntity(T entity)
            {
                mEntities.Add(entity);
            }

            /// <summary>复制当前实体列表，供分裂后重新路由。</summary>
            internal List<T> CopyEntities()
            {
                return new List<T>(mEntities);
            }

            /// <summary>清空叶节点实体列表。</summary>
            internal void ClearEntities()
            {
                mEntities.Clear();
            }

            /// <summary>把叶节点分裂成四个子节点。</summary>
            internal void Split()
            {
                float halfWidth = Bounds.Width * HALF_SIZE;
                float halfHeight = Bounds.Height * HALF_SIZE;
                int childDepth = Depth + 1;
                mChildren = new QuadtreeNode[CHILD_COUNT];
                mChildren[0] = new QuadtreeNode(new YokiRect(Bounds.X, Bounds.Y, halfWidth, halfHeight), childDepth);
                mChildren[1] = new QuadtreeNode(new YokiRect(Bounds.X + halfWidth, Bounds.Y, halfWidth, halfHeight), childDepth);
                mChildren[2] = new QuadtreeNode(new YokiRect(Bounds.X, Bounds.Y + halfHeight, halfWidth, halfHeight), childDepth);
                mChildren[3] = new QuadtreeNode(new YokiRect(Bounds.X + halfWidth, Bounds.Y + halfHeight, halfWidth, halfHeight), childDepth);
            }

            /// <summary>获取二维坐标所属子节点索引。</summary>
            /// <param name="positionX">投影 X 坐标。</param>
            /// <param name="positionY">投影第二坐标。</param>
            /// <returns>0 到 3 的子节点索引。</returns>
            internal int GetChildIndex(float positionX, float positionY)
            {
                float middleX = Bounds.X + Bounds.Width * HALF_SIZE;
                float middleY = Bounds.Y + Bounds.Height * HALF_SIZE;
                int index = 0;
                if (positionX >= middleX)
                {
                    index |= 1;
                }

                if (positionY >= middleY)
                {
                    index |= 2;
                }

                return index;
            }

            /// <summary>按实体 ID 从叶节点移除实体。</summary>
            internal bool RemoveEntity(int spatialId)
            {
                for (int index = mEntities.Count - 1; index >= 0; index--)
                {
                    if (mEntities[index].SpatialId == spatialId)
                    {
                        mEntities.RemoveAt(index);
                        return true;
                    }
                }

                return false;
            }

            /// <summary>获取内部子节点数组。</summary>
            internal QuadtreeNode[] GetChildrenArray()
            {
                return mChildren;
            }

            /// <summary>获取指定子节点。</summary>
            internal QuadtreeNode GetChild(int index)
            {
                return mChildren[index];
            }

            /// <summary>释放子节点数组，使当前节点恢复为叶节点。</summary>
            internal void ClearChildren()
            {
                mChildren = null;
            }
        }
    }
}
