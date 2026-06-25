using System.Collections.Generic;
using YokiFrame;

namespace YokiFrame
{
    /// <summary>
    /// Octree 节点定义。
    /// </summary>
    public partial class Octree<T>
    {
        /// <summary>
        /// 表示八叉树节点。
        /// </summary>
        public class OctreeNode
        {
            /// <summary>
            /// 节点三维边界。
            /// </summary>
            public YokiBounds Bounds;

            /// <summary>
            /// 节点深度。
            /// </summary>
            public int Depth;

            /// <summary>
            /// 叶节点实体列表。
            /// </summary>
            public List<T> Entities;

            /// <summary>
            /// 子节点数组。
            /// </summary>
            public OctreeNode[] Children;

            /// <summary>
            /// 创建八叉树节点。
            /// </summary>
            /// <param name="bounds">节点边界。</param>
            /// <param name="depth">节点深度。</param>
            public OctreeNode(YokiBounds bounds, int depth)
            {
                Bounds = bounds;
                Depth = depth;
                Entities = new List<T>(INITIAL_NODE_CAPACITY);
            }

            /// <summary>
            /// 获取当前节点是否为叶节点。
            /// </summary>
            public bool IsLeaf => Children == null;

            /// <summary>
            /// 将当前叶节点分裂为八个子节点。
            /// </summary>
            public void Split()
            {
                var center = Bounds.Center;
                var childSize = Bounds.Size * HALF_SIZE_MULTIPLIER;
                var offset = childSize * HALF_SIZE_MULTIPLIER;
                int depth = Depth + 1;
                Children = new OctreeNode[CHILD_COUNT];
                for (int i = 0; i < CHILD_COUNT; i++)
                {
                    var childOffset = new YokiVector3(
                        (i & 1) == 0 ? -offset.X : offset.X,
                        (i & 2) == 0 ? -offset.Y : offset.Y,
                        (i & 4) == 0 ? -offset.Z : offset.Z);
                    Children[i] = new OctreeNode(new YokiBounds(center + childOffset, childSize), depth);
                }
            }

            /// <summary>
            /// 获取三维位置所属的子节点索引。
            /// </summary>
            /// <param name="position">三维位置。</param>
            /// <returns>子节点索引。</returns>
            public int GetChildIndex(YokiVector3 position)
            {
                var center = Bounds.Center;
                int index = 0;
                if (position.X >= center.X)
                {
                    index |= 1;
                }

                if (position.Y >= center.Y)
                {
                    index |= 2;
                }

                if (position.Z >= center.Z)
                {
                    index |= 4;
                }

                return index;
            }
        }
    }
}
