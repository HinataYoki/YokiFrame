#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 绑定服务 - 提供绑定相关的核心功能
    /// </summary>
    public static partial class BindService
    {
        #region 绑定树收集

        /// <summary>
        /// 获取 Bind 组件的绑定路径（从 Panel 根节点开始）
        /// </summary>
        /// <param name="bind">目标 Bind 组件</param>
        /// <returns>绑定路径字符串</returns>
        public static string GetBindPath(AbstractBind bind)
        {
            if (bind == null)
                return string.Empty;

            // 查找 Panel 根节点
            var panelRoot = FindPanelRoot(bind.transform);
            if (panelRoot == null)
            {
                // 没有 UIPanel，使用 transform 根节点
                panelRoot = bind.transform.root;
            }

            return CalculatePath(bind.gameObject, panelRoot.gameObject);
        }

        /// <summary>
        /// 查找 Panel 根节点
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
        /// 收集 Prefab 的绑定树结构
        /// </summary>
        /// <param name="root">UI Prefab 根节点</param>
        /// <returns>绑定树根节点（虚拟节点，不对应实际 Bind）</returns>
        public static BindTreeNode CollectBindTree(GameObject root)
        {
            if (root == null)
                return null;

            // 创建虚拟根节点
            BindTreeNode rootNode = new()
            {
                Name = root.name,
                Path = root.name,
                GameObject = root,
                Type = BindType.Member,
                Depth = 0
            };

            // 递归收集子节点
            CollectBindTreeRecursive(root.transform, rootNode);

            return rootNode;
        }

        /// <summary>
        /// 递归收集绑定树
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
                    // 创建绑定节点
                    var node = new BindTreeNode(bind, parentNode);
                    parentNode.AddChild(node);

                    // 递归处理子节点
                    CollectBindTreeRecursive(child, node);
                }
                else
                {
                    // 没有 Bind 组件，继续向下搜索
                    CollectBindTreeRecursive(child, parentNode);
                }
            }
        }

        /// <summary>
        /// 计算 GameObject 相对于根节点的路径
        /// </summary>
        /// <param name="target">目标 GameObject</param>
        /// <param name="root">根节点</param>
        /// <returns>相对路径</returns>
        public static string CalculatePath(GameObject target, GameObject root)
        {
            if (target == null || root == null)
                return string.Empty;

            if (target == root)
                return root.name;

            // 收集路径
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
                // target 不是 root 的子节点
                return target.name;
            }

            // 添加根节点名称
            pathParts.Add(root.name);

            // 反转并拼接
            pathParts.Reverse();
            return string.Join("/", pathParts);
        }

        #endregion

        #region 验证

        /// <summary>
        /// 验证 Prefab 的所有绑定配置
        /// </summary>
        /// <param name="root">UI Prefab 根节点</param>
        /// <returns>验证结果列表</returns>
        public static List<BindValidationResult> ValidateBindings(GameObject root)
        {
            if (root == null)
                return new List<BindValidationResult>();

            // 收集绑定树
            var tree = CollectBindTree(root);
            if (tree == null)
                return new List<BindValidationResult>();

            // 验证绑定树
            return BindValidator.ValidateTree(tree);
        }

        /// <summary>
        /// 快速检查是否有验证错误
        /// </summary>
        /// <param name="root">UI Prefab 根节点</param>
        /// <returns>是否有错误</returns>
        public static bool HasValidationErrors(GameObject root)
        {
            var results = ValidateBindings(root);
            return BindValidator.HasErrors(results);
        }

        #endregion

        #region 统计

        /// <summary>
        /// 统计绑定数量
        /// </summary>
        /// <param name="root">UI Prefab 根节点</param>
        /// <returns>各类型绑定数量</returns>
        public static BindStatistics GetBindStatistics(GameObject root)
        {
            BindStatistics stats = new();

            if (root == null)
                return stats;

            // 收集所有 Bind 组件
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
    /// 绑定统计信息
    /// </summary>
    public struct BindStatistics
    {
        /// <summary>
        /// 总绑定数量
        /// </summary>
        public int Total;

        /// <summary>
        /// Member 类型数量
        /// </summary>
        public int MemberCount;

        /// <summary>
        /// Element 类型数量
        /// </summary>
        public int ElementCount;

        /// <summary>
        /// Component 类型数量
        /// </summary>
        public int ComponentCount;

        /// <summary>
        /// Leaf 类型数量
        /// </summary>
        public int LeafCount;

        public override readonly string ToString()
            => $"共 {Total} 个绑定 ({MemberCount} Member, {ElementCount} Element, {ComponentCount} Component, {LeafCount} Leaf)";
    }
}
#endif
