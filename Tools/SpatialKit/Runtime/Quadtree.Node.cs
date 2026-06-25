using System.Collections.Generic;
using YokiFrame;

namespace YokiFrame
{
    /// <summary>
    /// Quadtree 节点定义。
    /// </summary>
    public partial class Quadtree<T>
    {
        /// <summary>
        /// 表示四叉树节点。
        /// </summary>
        public class QuadtreeNode
        {
            /// <summary>
            /// 节点二维边界。
            /// </summary>
            public YokiRect Bounds;

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
            public QuadtreeNode[] Children;

            /// <summary>
            /// 创建四叉树节点。
            /// </summary>
            /// <param name="bounds">节点边界。</param>
            /// <param name="depth">节点深度。</param>
            public QuadtreeNode(YokiRect bounds, int depth)
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
            /// 将当前叶节点分裂为四个子节点。
            /// </summary>
            public void Split()
            {
                float halfWidth = Bounds.Width * HALF_SIZE_MULTIPLIER;
                float halfHeight = Bounds.Height * HALF_SIZE_MULTIPLIER;
                int depth = Depth + 1;
                Children = new QuadtreeNode[CHILD_COUNT];
                Children[0] = new QuadtreeNode(new YokiRect(Bounds.X, Bounds.Y, halfWidth, halfHeight), depth);
                Children[1] = new QuadtreeNode(new YokiRect(Bounds.X + halfWidth, Bounds.Y, halfWidth, halfHeight), depth);
                Children[2] = new QuadtreeNode(new YokiRect(Bounds.X, Bounds.Y + halfHeight, halfWidth, halfHeight), depth);
                Children[3] = new QuadtreeNode(new YokiRect(Bounds.X + halfWidth, Bounds.Y + halfHeight, halfWidth, halfHeight), depth);
            }

            /// <summary>
            /// 获取二维位置所属的子节点索引。
            /// </summary>
            /// <param name="posX">X 坐标。</param>
            /// <param name="posY">Y 坐标。</param>
            /// <returns>子节点索引。</returns>
            public int GetChildIndex(float posX, float posY)
            {
                float midX = Bounds.X + Bounds.Width * HALF_SIZE_MULTIPLIER;
                float midY = Bounds.Y + Bounds.Height * HALF_SIZE_MULTIPLIER;
                int index = 0;
                if (posX >= midX)
                {
                    index |= 1;
                }

                if (posY >= midY)
                {
                    index |= 2;
                }

                return index;
            }
        }
    }
}
