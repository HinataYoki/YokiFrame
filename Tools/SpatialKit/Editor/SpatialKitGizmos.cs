using System.Collections;
using System.Reflection;
using UnityEngine;

namespace YokiFrame.Editor
{
    /// <summary>
    /// 空间索引 Gizmos 绘制工具
    /// </summary>
    public static class SpatialKitGizmos
    {
        /// <summary>
        /// 绘制空间哈希网格
        /// </summary>
        /// <typeparam name="T">实体类型</typeparam>
        /// <param name="grid">空间哈希网格实例</param>
        /// <param name="color">绘制颜色</param>
        /// <param name="onlyNonEmpty">是否只绘制非空格子</param>
        public static void DrawHashGrid<T>(SpatialHashGrid<T> grid, Color color, bool onlyNonEmpty = true) where T : ISpatialEntity
        {
            Gizmos.color = color;
            if (grid == null) return;

            var gridType = grid.GetType();
            var cellSizeField = gridType.GetField("mCellSize", BindingFlags.Instance | BindingFlags.NonPublic);
            var cellsField = gridType.GetField("mCells", BindingFlags.Instance | BindingFlags.NonPublic);

            if (cellSizeField == null || cellsField == null) return;

            var cellSizeObj = cellSizeField.GetValue(grid);
            if (cellSizeObj is not float cellSize || cellSize <= 0f) return;

            var cellsObj = cellsField.GetValue(grid);
            if (cellsObj is not IDictionary cells) return;

            var plane = grid.Plane;

            foreach (DictionaryEntry entry in cells)
            {
                if (entry.Key is not long hash) continue;

                if (onlyNonEmpty)
                {
                    if (entry.Value is not ICollection list || list.Count == 0) continue;
                }

                int cellA = (int)(hash >> 32);
                int cellB = (int)(hash & 0xffffffff);

                Vector3 center, size;
                if (plane == SpatialPlane.XZ)
                {
                    center = new Vector3((cellA + 0.5f) * cellSize, 0f, (cellB + 0.5f) * cellSize);
                    size = new Vector3(cellSize, 0.05f, cellSize);
                }
                else
                {
                    center = new Vector3((cellA + 0.5f) * cellSize, (cellB + 0.5f) * cellSize, 0f);
                    size = new Vector3(cellSize, cellSize, 0.05f);
                }
                Gizmos.DrawWireCube(center, size);
            }
        }

        /// <summary>
        /// 绘制四叉树
        /// </summary>
        /// <typeparam name="T">实体类型</typeparam>
        /// <param name="quadtree">四叉树实例</param>
        /// <param name="color">绘制颜色</param>
        /// <param name="onlyNonEmpty">是否只绘制非空节点</param>
        public static void DrawQuadtree<T>(Quadtree<T> quadtree, Color color, bool onlyNonEmpty = true) where T : ISpatialEntity
        {
            Gizmos.color = color;
            DrawQuadtreeNode(quadtree.Root, quadtree.Plane, onlyNonEmpty);
        }

        /// <summary>
        /// 绘制八叉树
        /// </summary>
        /// <typeparam name="T">实体类型</typeparam>
        /// <param name="octree">八叉树实例</param>
        /// <param name="color">绘制颜色</param>
        /// <param name="onlyNonEmpty">是否只绘制非空节点</param>
        public static void DrawOctree<T>(Octree<T> octree, Color color, bool onlyNonEmpty = true) where T : ISpatialEntity
        {
            Gizmos.color = color;
            DrawOctreeNode(octree.Root, onlyNonEmpty);
        }

        private static void DrawQuadtreeNode<T>(Quadtree<T>.QuadtreeNode node, SpatialPlane plane, bool onlyNonEmpty) where T : ISpatialEntity
        {
            if (node == null) return;

            bool shouldDraw = !onlyNonEmpty || (node.IsLeaf && node.Entities.Count > 0);

            if (shouldDraw && node.IsLeaf)
            {
                var rect = node.Bounds;
                Vector3 center, size;
                if (plane == SpatialPlane.XZ)
                {
                    center = new Vector3(rect.center.x, 0, rect.center.y);
                    size = new Vector3(rect.width, 0.1f, rect.height);
                }
                else
                {
                    center = new Vector3(rect.center.x, rect.center.y, 0);
                    size = new Vector3(rect.width, rect.height, 0.1f);
                }
                Gizmos.DrawWireCube(center, size);
            }

            if (!node.IsLeaf)
            {
                for (int i = 0; i < 4; i++)
                    DrawQuadtreeNode(node.Children[i], plane, onlyNonEmpty);
            }
        }

        private static void DrawOctreeNode<T>(Octree<T>.OctreeNode node, bool onlyNonEmpty) where T : ISpatialEntity
        {
            if (node == null) return;

            bool shouldDraw = !onlyNonEmpty || (node.IsLeaf && node.Entities.Count > 0);

            if (shouldDraw && node.IsLeaf)
            {
                Gizmos.DrawWireCube(node.Bounds.center, node.Bounds.size);
            }

            if (!node.IsLeaf)
            {
                for (int i = 0; i < 8; i++)
                    DrawOctreeNode(node.Children[i], onlyNonEmpty);
            }
        }
    }
}
