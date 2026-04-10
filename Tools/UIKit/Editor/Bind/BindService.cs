#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// UIKit 绑定服务。
    /// 负责收集绑定树、执行绑定验证、计算绑定路径以及输出统计结果，
    /// 供批量绑定窗口、Inspector 绑定树和验证器复用。
    /// </summary>
    public static partial class BindService
    {
        #region 绑定树收集

        /// <summary>
        /// 获取指定 Bind 组件在所属面板根节点下的绑定路径。
        /// </summary>
        public static string GetBindPath(AbstractBind bind)
        {
            if (bind == null)
                return string.Empty;

            var panelRoot = FindPanelRoot(bind.transform);
            if (panelRoot == null)
            {
                panelRoot = bind.transform.root;
            }

            return CalculatePath(bind.gameObject, panelRoot.gameObject);
        }

        /// <summary>
        /// 向上查找最近的 UIPanel 根节点。
        /// </summary>
        private static Transform FindPanelRoot(Transform current)
        {
            while (current != null)
            {
                if (current.GetComponent<UIPanel>() != null)
                    return current;
                current = current.parent;
            }

            return null;
        }

        /// <summary>
        /// 收集指定根节点下的绑定树结构。
        /// </summary>
        public static BindTreeNode CollectBindTree(GameObject root)
        {
            if (root == null)
                return null;

            BindTreeNode rootNode = new()
            {
                Name = root.name,
                Path = root.name,
                GameObject = root,
                Type = BindType.Member,
                Depth = 0
            };

            CollectBindTreeRecursive(root.transform, rootNode);
            return rootNode;
        }

        /// <summary>
        /// 递归收集绑定树节点。
        /// </summary>
        private static void CollectBindTreeRecursive(Transform parent, BindTreeNode parentNode)
        {
            if (parent == null || parentNode == null)
                return;

            int childCount = parent.childCount;
            for (int i = 0; i < childCount; i++)
            {
                var child = parent.GetChild(i);
                var bind = child.GetComponent<AbstractBind>();

                if (bind != null)
                {
                    var node = new BindTreeNode(bind, parentNode);
                    parentNode.AddChild(node);

                    CollectBindTreeRecursive(child, node);
                }
                else
                {
                    CollectBindTreeRecursive(child, parentNode);
                }
            }
        }

        /// <summary>
        /// 计算目标对象相对指定根节点的层级路径。
        /// </summary>
        public static string CalculatePath(GameObject target, GameObject root)
        {
            if (target == null || root == null)
                return string.Empty;

            if (target == root)
                return root.name;

            var pathParts = new List<string>(8);
            var current = target.transform;
            var rootTransform = root.transform;

            while (current != null && current != rootTransform)
            {
                pathParts.Add(current.name);
                current = current.parent;
            }

            if (current == null)
            {
                return target.name;
            }

            pathParts.Add(root.name);
            pathParts.Reverse();
            return string.Join("/", pathParts);
        }

        #endregion

        #region 验证

        /// <summary>
        /// 验证指定根节点下的全部绑定配置。
        /// </summary>
        public static List<BindValidationResult> ValidateBindings(GameObject root)
        {
            if (root == null)
                return new List<BindValidationResult>();

            var tree = CollectBindTree(root);
            if (tree == null)
                return new List<BindValidationResult>();

            return BindValidator.ValidateTree(tree);
        }

        /// <summary>
        /// 快速判断指定根节点下是否存在绑定验证错误。
        /// </summary>
        public static bool HasValidationErrors(GameObject root)
        {
            var results = ValidateBindings(root);
            return BindValidator.HasErrors(results);
        }

        #endregion

        #region 统计

        /// <summary>
        /// 统计指定根节点下的绑定数量分布。
        /// </summary>
        public static BindStatistics GetBindStatistics(GameObject root)
        {
            BindStatistics stats = new();

            if (root == null)
                return stats;

            var binds = root.GetComponentsInChildren<AbstractBind>(true);
            stats.Total = binds.Length;

            foreach (var bind in binds)
            {
                switch (bind.Bind)
                {
                    case BindType.Member:
                        stats.MemberCount++;
                        break;
                    case BindType.Element:
                        stats.ElementCount++;
                        break;
                    case BindType.Component:
                        stats.ComponentCount++;
                        break;
                    case BindType.Leaf:
                        stats.LeafCount++;
                        break;
                }
            }

            return stats;
        }

        #endregion
    }

    /// <summary>
    /// 绑定统计信息。
    /// </summary>
    public struct BindStatistics
    {
        /// <summary>
        /// 总绑定数量。
        /// </summary>
        public int Total;

        /// <summary>
        /// Member 类型数量。
        /// </summary>
        public int MemberCount;

        /// <summary>
        /// Element 类型数量。
        /// </summary>
        public int ElementCount;

        /// <summary>
        /// Component 类型数量。
        /// </summary>
        public int ComponentCount;

        /// <summary>
        /// Leaf 类型数量。
        /// </summary>
        public int LeafCount;

        public override readonly string ToString()
            => $"共 {Total} 个绑定 ({MemberCount} Member, {ElementCount} Element, {ComponentCount} Component, {LeafCount} Leaf)";
    }
}
#endif
