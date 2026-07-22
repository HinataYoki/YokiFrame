using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>封装 Octree 的只读节点视图和内部分裂操作。</summary>
    public sealed partial class Octree<T> where T : ISpatialEntity
    {
        private readonly Dictionary<int, T> mOverflowEntities = new Dictionary<int, T>();

        /// <summary>
        /// 判断实体位置是否落在根三维边界内；越界实体走独立溢出查询路径。
        /// </summary>
        /// <param name="position">需要判断的实体位置。</param>
        /// <returns>位置在根边界闭区间内时返回 true。</returns>
        private bool IsInsideRoot(YokiVector3 position)
        {
            return mRoot.Bounds.Contains(position);
        }

        /// <summary>把溢出实体中满足三维半径的对象追加到查询结果。</summary>
        private void AppendOverflowRadius(YokiVector3 center, float radiusSquared, List<T> results)
        {
            foreach (T entity in mOverflowEntities.Values)
            {
                float distanceSquared = (entity.Position - center).SqrMagnitude;
                if (distanceSquared <= radiusSquared)
                {
                    results.Add(entity);
                }
            }
        }

        /// <summary>把溢出实体中包含在三维包围盒内的对象追加到查询结果。</summary>
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
        private static void QueryNearestOverflow(
            IReadOnlyDictionary<int, T> overflowEntities,
            YokiVector3 position,
            Func<T, bool> filter,
            ref T nearest,
            ref float nearestDistanceSquared,
            ref bool found)
        {
            foreach (T entity in overflowEntities.Values)
            {
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
        }

        /// <summary>表示一个八叉树节点。</summary>
        public sealed class OctreeNode
        {
            private const int CHILD_COUNT = 8;
            private const float HALF_SIZE = 0.5f;
            private readonly List<T> mEntities;
            private OctreeNode[] mChildren;

            /// <summary>创建八叉树节点。</summary>
            /// <param name="bounds">节点边界。</param>
            /// <param name="depth">节点深度。</param>
            internal OctreeNode(YokiBounds bounds, int depth)
            {
                Bounds = bounds;
                Depth = depth;
                mEntities = new List<T>(4);
            }

            /// <summary>获取节点边界。</summary>
            public YokiBounds Bounds { get; }

            /// <summary>获取节点深度。</summary>
            public int Depth { get; }

            /// <summary>获取节点是否为叶节点。</summary>
            public bool IsLeaf { get { return mChildren == null; } }

            /// <summary>获取叶节点实体的只读视图。</summary>
            public IReadOnlyList<T> Entities { get { return mEntities; } }

            /// <summary>获取子节点只读视图；叶节点返回空列表。</summary>
            public IReadOnlyList<OctreeNode> Children
            {
                get { return mChildren ?? Array.Empty<OctreeNode>(); }
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

            /// <summary>把叶节点分裂成八个子节点。</summary>
            internal void Split()
            {
                YokiVector3 center = Bounds.Center;
                YokiVector3 childSize = Bounds.Size * HALF_SIZE;
                YokiVector3 offset = childSize * HALF_SIZE;
                int childDepth = Depth + 1;
                mChildren = new OctreeNode[CHILD_COUNT];
                for (int index = 0; index < CHILD_COUNT; index++)
                {
                    YokiVector3 childOffset = new YokiVector3(
                        (index & 1) == 0 ? -offset.X : offset.X,
                        (index & 2) == 0 ? -offset.Y : offset.Y,
                        (index & 4) == 0 ? -offset.Z : offset.Z);
                    mChildren[index] = new OctreeNode(new YokiBounds(center + childOffset, childSize), childDepth);
                }
            }

            /// <summary>获取三维位置所属子节点索引。</summary>
            /// <param name="position">三维位置。</param>
            /// <returns>0 到 7 的子节点索引。</returns>
            internal int GetChildIndex(YokiVector3 position)
            {
                YokiVector3 center = Bounds.Center;
                int index = 0;
                if (position.X >= center.X) index |= 1;
                if (position.Y >= center.Y) index |= 2;
                if (position.Z >= center.Z) index |= 4;
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
            internal OctreeNode[] GetChildrenArray()
            {
                return mChildren;
            }

            /// <summary>获取指定子节点。</summary>
            internal OctreeNode GetChild(int index)
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
