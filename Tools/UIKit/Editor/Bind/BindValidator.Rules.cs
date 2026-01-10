#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// BindValidator - 验证规则
    /// </summary>
    public static partial class BindValidator
    {
        #region 命名冲突检测

        /// <summary>
        /// 检查命名冲突
        /// </summary>
        /// <param name="name">要检查的名称</param>
        /// <param name="existingNames">已存在的名称集合</param>
        /// <param name="target">关联的 GameObject</param>
        /// <returns>验证结果，如果无冲突则返回 null</returns>
        public static BindValidationResult? CheckNameConflict(
            string name,
            HashSet<string> existingNames,
            GameObject target = null)
        {
            if (string.IsNullOrEmpty(name) || existingNames == null)
                return null;

            if (existingNames.Contains(name))
            {
                return BindValidationResult.Error(
                    $"字段名 '{name}' 与同层级其他绑定冲突",
                    target,
                    "请修改字段名以避免冲突",
                    RuleIds.NAME_CONFLICT);
            }

            return null;
        }

        /// <summary>
        /// 在绑定树中检测命名冲突
        /// </summary>
        /// <remarks>
        /// 命名冲突检测规则：
        /// - Member 类型的字段名在其直接父容器（Panel/Element/Component）内必须唯一
        /// - 不同父容器下的 Member 可以同名（因为会生成到不同的类中）
        /// </remarks>
        /// <param name="rootNode">绑定树根节点</param>
        /// <param name="results">验证结果列表（输出）</param>
        public static void DetectNameConflicts(BindTreeNode rootNode, List<BindValidationResult> results)
        {
            if (rootNode == null || results == null)
                return;

            // 从根节点开始，递归检测每个容器内的命名冲突
            DetectNameConflictsInContainer(rootNode, rootNode, results);
        }

        /// <summary>
        /// 在指定容器内检测命名冲突
        /// </summary>
        /// <param name="containerNode">容器节点（Panel/Element/Component）</param>
        /// <param name="currentNode">当前遍历的节点</param>
        /// <param name="results">验证结果列表</param>
        private static void DetectNameConflictsInContainer(
            BindTreeNode containerNode,
            BindTreeNode currentNode,
            List<BindValidationResult> results)
        {
            if (currentNode == null || currentNode.Children == null)
                return;

            // 收集当前容器下直接子 Member 的名称
            var nameToNodes = new Dictionary<string, List<BindTreeNode>>(8);

            foreach (var child in currentNode.Children)
            {
                CollectDirectChildMembers(child, containerNode, nameToNodes, results);
            }

            // 检测当前容器内的冲突
            foreach (var kvp in nameToNodes)
            {
                if (kvp.Value.Count > 1)
                {
                    // 获取容器名称用于错误提示
                    string containerName = GetContainerDisplayName(containerNode);
                    
                    foreach (var node in kvp.Value)
                    {
                        var result = BindValidationResult.Error(
                            $"字段名 '{kvp.Key}' 在 {containerName} 中存在 {kvp.Value.Count} 处重复定义",
                            node.GameObject,
                            "请修改字段名以避免冲突",
                            RuleIds.NAME_CONFLICT);
                        results.Add(result);
                        node.AddValidationResult(result);
                    }
                }
            }
        }

        /// <summary>
        /// 收集直接子 Member 节点（遇到新容器则递归处理该容器）
        /// </summary>
        private static void CollectDirectChildMembers(
            BindTreeNode node,
            BindTreeNode currentContainer,
            Dictionary<string, List<BindTreeNode>> nameToNodes,
            List<BindValidationResult> results)
        {
            if (node == null)
                return;

            // 如果是 Element 或 Component，它是一个新的容器，递归处理
            if (node.Type is BindType.Element or BindType.Component)
            {
                // 先将此节点作为当前容器的成员记录（Element/Component 本身也是父容器的成员）
                if (!string.IsNullOrEmpty(node.Name))
                {
                    if (!nameToNodes.TryGetValue(node.Name, out var list))
                    {
                        list = new List<BindTreeNode>(2);
                        nameToNodes[node.Name] = list;
                    }
                    list.Add(node);
                }
                
                // 然后递归处理这个新容器内部的成员
                DetectNameConflictsInContainer(node, node, results);
                return;
            }

            // Member 类型：记录到当前容器的名称集合
            if (node.Type == BindType.Member && !string.IsNullOrEmpty(node.Name))
            {
                if (!nameToNodes.TryGetValue(node.Name, out var list))
                {
                    list = new List<BindTreeNode>(2);
                    nameToNodes[node.Name] = list;
                }
                list.Add(node);
            }

            // 继续遍历子节点（可能有嵌套的 GameObject 但没有 Bind）
            if (node.Children != null)
            {
                foreach (var child in node.Children)
                {
                    CollectDirectChildMembers(child, currentContainer, nameToNodes, results);
                }
            }
        }

        /// <summary>
        /// 获取容器的显示名称
        /// </summary>
        private static string GetContainerDisplayName(BindTreeNode containerNode)
        {
            if (containerNode == null)
                return "未知容器";

            return containerNode.Type switch
            {
                BindType.Element => $"Element '{containerNode.Name}'",
                BindType.Component => $"Component '{containerNode.Name}'",
                _ => containerNode.Bind == null ? $"Panel '{containerNode.Name}'" : $"'{containerNode.Name}'"
            };
        }

        #endregion

        #region 层级规则验证

        /// <summary>
        /// 验证层级规则（Element 不能在 Component 下）
        /// </summary>
        /// <param name="node">要验证的节点</param>
        /// <returns>验证结果，如果合法则返回 null</returns>
        public static BindValidationResult? ValidateHierarchyRule(BindTreeNode node)
        {
            if (node == null)
                return null;

            // Element 不能在 Component 下
            if (node.Type == BindType.Element)
            {
                var parent = node.Parent;
                while (parent != null)
                {
                    if (parent.Type == BindType.Component)
                    {
                        return BindValidationResult.Error(
                            $"Element '{node.Name}' 不能定义在 Component '{parent.Name}' 下",
                            node.GameObject,
                            "Element 是面板内部结构，Component 是跨面板复用组件。建议将此绑定改为 Member 类型",
                            RuleIds.ELEMENT_UNDER_COMPONENT);
                    }
                    parent = parent.Parent;
                }
            }

            return null;
        }

        /// <summary>
        /// 递归验证整个绑定树的层级规则
        /// </summary>
        /// <param name="rootNode">绑定树根节点</param>
        /// <param name="results">验证结果列表（输出）</param>
        public static void ValidateHierarchyRules(BindTreeNode rootNode, List<BindValidationResult> results)
        {
            if (rootNode == null || results == null)
                return;

            // 验证当前节点
            var result = ValidateHierarchyRule(rootNode);
            if (result.HasValue)
            {
                results.Add(result.Value);
                // 将验证结果关联到节点
                rootNode.AddValidationResult(result.Value);
            }

            // 递归验证子节点
            if (rootNode.Children != null)
            {
                foreach (var child in rootNode.Children)
                {
                    ValidateHierarchyRules(child, results);
                }
            }
        }

        #endregion

        #region 完整验证

        /// <summary>
        /// 验证单个 Bind 组件
        /// </summary>
        /// <param name="bind">要验证的 Bind 组件</param>
        /// <param name="existingNames">已存在的名称集合（用于冲突检测）</param>
        /// <returns>验证结果列表</returns>
        public static List<BindValidationResult> Validate(
            AbstractBind bind,
            HashSet<string> existingNames = null)
        {
            List<BindValidationResult> results = new(4);

            if (bind == null)
                return results;

            var target = bind.gameObject;

            // 1. 验证字段名
            var identifierResult = ValidateIdentifier(bind.Name, target);
            if (identifierResult.HasValue)
            {
                results.Add(identifierResult.Value);
            }

            // 2. 检查命名冲突
            if (existingNames != null)
            {
                var conflictResult = CheckNameConflict(bind.Name, existingNames, target);
                if (conflictResult.HasValue)
                {
                    results.Add(conflictResult.Value);
                }
            }

            // 3. 检查类型是否缺失
            if (string.IsNullOrEmpty(bind.Type))
            {
                results.Add(BindValidationResult.Warning(
                    "未指定组件类型，将使用 GameObject 作为默认类型",
                    target,
                    "建议选择具体的组件类型",
                    RuleIds.MISSING_TYPE));
            }

            return results;
        }

        /// <summary>
        /// 验证绑定树
        /// </summary>
        /// <param name="rootNode">绑定树根节点</param>
        /// <returns>验证结果列表</returns>
        public static List<BindValidationResult> ValidateTree(BindTreeNode rootNode)
        {
            List<BindValidationResult> results = new(16);

            if (rootNode == null)
                return results;

            // 1. 验证所有节点的标识符
            ValidateAllIdentifiers(rootNode, results);

            // 2. 检测命名冲突
            DetectNameConflicts(rootNode, results);

            // 3. 验证层级规则
            ValidateHierarchyRules(rootNode, results);

            return results;
        }

        /// <summary>
        /// 递归验证所有节点的标识符
        /// </summary>
        private static void ValidateAllIdentifiers(BindTreeNode node, List<BindValidationResult> results)
        {
            if (node == null)
                return;

            // 验证当前节点
            var result = ValidateIdentifier(node.Name, node.GameObject);
            if (result.HasValue)
            {
                results.Add(result.Value);
                node.AddValidationResult(result.Value);
            }

            // 递归验证子节点
            if (node.Children != null)
            {
                foreach (var child in node.Children)
                {
                    ValidateAllIdentifiers(child, results);
                }
            }
        }

        #endregion
    }
}
#endif
