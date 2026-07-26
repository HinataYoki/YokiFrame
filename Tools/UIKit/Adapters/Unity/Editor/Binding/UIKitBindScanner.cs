#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 把 Prefab 层级中的兼容 Bind 组件转换为 Editor-only 确定性生成树。
    /// </summary>
    internal static class UIKitBindScanner
    {
        /// <summary>
        /// 扫描指定 Prefab 根并收集全部诊断；该方法不修改场景、Prefab 或文件。
        /// </summary>
        /// <param name="root">待扫描 Prefab 内容根。</param>
        /// <returns>完整生成树与诊断。</returns>
        internal static UIKitBindScanResult Scan(GameObject root)
        {
            return ScanCore(root, default);
        }

        /// <summary>按独立 Element/Component 根语义扫描 Prefab 绑定。</summary>
        internal static UIKitBindScanResult ScanOwner(
            GameObject root,
            UIKitGeneratedOwnerKind ownerKind)
        {
            BindType bindType = ownerKind == UIKitGeneratedOwnerKind.Element
                ? BindType.Element
                : BindType.Component;
            if (!UIKitBindStrategyRegistry.TryGetBuiltIn(bindType, out IUIKitBindStrategy strategy))
                throw new InvalidOperationException("缺少内置 owner Bind 策略: " + bindType);
            return ScanCore(root, strategy);
        }

        /// <summary>使用指定根 owner 策略执行统一确定性扫描。</summary>
        private static UIKitBindScanResult ScanCore(
            GameObject root,
            IUIKitBindStrategy ownerStrategy)
        {
            if (root == default) throw new ArgumentNullException(nameof(root));
            UIKitBindScanResult result = new(root.name);
            Dictionary<string, UIKitBindNode> rootNames = new(StringComparer.Ordinal);
            ScanChildren(root.transform, result.Nodes, rootNames, ownerStrategy, result);
            return result;
        }

        /// <summary>按 Transform sibling 顺序递归扫描，并维护当前 owner 的字段命名空间。</summary>
        private static void ScanChildren(
            Transform parent,
            List<UIKitBindNode> ownerNodes,
            Dictionary<string, UIKitBindNode> ownerNames,
            IUIKitBindStrategy ownerStrategy,
            UIKitBindScanResult result)
        {
            for (var childIndex = 0; childIndex < parent.childCount; childIndex++)
            {
                Transform child = parent.GetChild(childIndex);
                AbstractBind bind = child.GetComponent<AbstractBind>();
                if (bind == default)
                {
                    ScanChildren(child, ownerNodes, ownerNames, ownerStrategy, result);
                    continue;
                }

                string path = BuildPath(child);
                if (!TryCreateNodes(
                        bind,
                        path,
                        ownerNodes.Count,
                        ownerStrategy,
                        result,
                        out List<UIKitBindNode> nodes))
                {
                    ScanChildren(child, ownerNodes, ownerNames, ownerStrategy, result);
                    continue;
                }

                if (nodes[0].Strategy.OutputKind == UIKitBindOutputKind.Marker) continue;
                for (var nodeIndex = 0; nodeIndex < nodes.Count; nodeIndex++)
                    RegisterNode(nodes[nodeIndex], ownerNodes, ownerNames, result);
                UIKitBindNode scopeNode = nodes[0];
                if (scopeNode.Strategy.CanContainChildren)
                {
                    Dictionary<string, UIKitBindNode> childNames = new(StringComparer.Ordinal);
                    ScanChildren(child, scopeNode.Children, childNames, scopeNode.Strategy, result);
                }
                else
                {
                    ScanChildren(child, ownerNodes, ownerNames, ownerStrategy, result);
                }
            }
        }

        /// <summary>解析策略和层级规则，并按配置创建一个或多个生成节点。</summary>
        private static bool TryCreateNodes(
            AbstractBind bind,
            string path,
            int order,
            IUIKitBindStrategy ownerStrategy,
            UIKitBindScanResult result,
            out List<UIKitBindNode> nodes)
        {
            if (!UIKitBindStrategyRegistry.TryGet(bind, out IUIKitBindStrategy strategy, out string error))
                return Fail(path, error, result, out nodes);

            if (ownerStrategy != default && !ownerStrategy.TryValidateChild(bind.Bind, out error))
                return Fail(path, error, result, out nodes);

            if (CanExpandMemberTargets(bind, strategy))
                return TryCreateMemberNodes(bind, strategy, path, order, result, out nodes);

            if (!strategy.TryResolve(bind, out string typeName, out UnityEngine.Object target, out error))
                return Fail(path, error, result, out nodes);

            string fieldName = string.IsNullOrWhiteSpace(bind.Name)
                ? bind.gameObject.name
                : bind.Name.Trim();
            if (!TryValidateIdentifiers(strategy.OutputKind, fieldName, typeName, out error))
                return Fail(path, error, result, out nodes);

            nodes = new List<UIKitBindNode>(1)
            {
                new(bind, strategy, path, fieldName, typeName, target, order)
            };
            return true;
        }

        /// <summary>判断当前 Bind 是否使用内置 Member 的显式多目标列表。</summary>
        private static bool CanExpandMemberTargets(AbstractBind bind, IUIKitBindStrategy strategy)
        {
            return bind.MemberTargets.Count > 0
                && strategy.OutputKind == UIKitBindOutputKind.Member;
        }

        /// <summary>校验多目标列表，并按序创建独立 Member 节点。</summary>
        private static bool TryCreateMemberNodes(
            AbstractBind bind,
            IUIKitBindStrategy strategy,
            string path,
            int order,
            UIKitBindScanResult result,
            out List<UIKitBindNode> nodes)
        {
            nodes = new List<UIKitBindNode>(bind.MemberTargets.Count);
            HashSet<Component> targets = new();
            for (var index = 0; index < bind.MemberTargets.Count; index++)
            {
                BindMemberTarget item = bind.MemberTargets[index];
                Component target = item == null ? default : item.Target;
                string itemPath = path + "[" + index + "]";
                if (!TryValidateMemberTarget(bind, target, targets, out string error))
                    return Fail(itemPath, error, result, out nodes);

                string fieldName = string.IsNullOrWhiteSpace(item.Name)
                    ? UIKitBindMemberNaming.CreateDefaultName(bind, target, bind.MemberTargets, index)
                    : item.Name.Trim();
                string typeName = target.GetType().FullName;
                if (!TryValidateIdentifiers(strategy.OutputKind, fieldName, typeName, out error))
                    return Fail(itemPath, error, result, out nodes);
                nodes.Add(new UIKitBindNode(
                    bind,
                    strategy,
                    itemPath,
                    fieldName,
                    typeName,
                    target,
                    order + index));
            }
            return true;
        }

        /// <summary>验证多目标组件属于当前节点且没有重复选择。</summary>
        private static bool TryValidateMemberTarget(
            AbstractBind bind,
            Component target,
            HashSet<Component> targets,
            out string error)
        {
            if (target == default)
                error = "Member 目标不能为空。";
            else if (target is AbstractBind)
                error = "Member 目标不能是 Bind 组件。";
            else if (target.gameObject != bind.gameObject)
                error = "Member 目标必须位于 Bind 所在 GameObject。";
            else if (!targets.Add(target))
                error = "同一组件不能重复绑定。";
            else
                error = string.Empty;
            return error.Length == 0;
        }

        /// <summary>校验字段名以及需要生成类型时的类名。</summary>
        private static bool TryValidateIdentifiers(
            UIKitBindOutputKind outputKind,
            string fieldName,
            string typeName,
            out string error)
        {
            try
            {
                CodeGenKit.RequireIdentifier(fieldName, nameof(fieldName));
                if (outputKind == UIKitBindOutputKind.Element
                    || outputKind == UIKitBindOutputKind.Component)
                    CodeGenKit.RequireIdentifier(typeName, nameof(typeName));
                error = string.Empty;
                return true;
            }
            catch (ArgumentException exception)
            {
                error = exception.Message;
                return false;
            }
        }

        /// <summary>提交节点并把重复字段转换为明确错误或兼容重复类型提示。</summary>
        private static void RegisterNode(
            UIKitBindNode node,
            List<UIKitBindNode> ownerNodes,
            Dictionary<string, UIKitBindNode> ownerNames,
            UIKitBindScanResult result)
        {
            if (ownerNames.TryGetValue(node.FieldName, out UIKitBindNode existing))
            {
                if (node.Strategy.OutputKind == UIKitBindOutputKind.Member)
                {
                    AddError(result, node.Path,
                        "字段名重复: " + node.FieldName + "，首次出现于 " + existing.Path);
                    return;
                }

                node.IsRepeated = true;
                AddWarning(result, node.Path,
                    "重复生成类型不会创建第二个 owner 字段: " + node.FieldName);
            }
            else
            {
                ownerNames.Add(node.FieldName, node);
            }

            ownerNodes.Add(node);
        }

        /// <summary>记录阻断诊断并返回统一失败结果。</summary>
        private static bool Fail(
            string path,
            string error,
            UIKitBindScanResult result,
            out List<UIKitBindNode> nodes)
        {
            AddError(result, path, error);
            nodes = default;
            return false;
        }

        /// <summary>追加阻断错误。</summary>
        private static void AddError(UIKitBindScanResult result, string path, string message)
        {
            result.Diagnostics.Add(new UIKitBindDiagnostic(
                UIKitBindDiagnosticSeverity.Error,
                path,
                message));
        }

        /// <summary>追加非阻断提示。</summary>
        private static void AddWarning(UIKitBindScanResult result, string path, string message)
        {
            result.Diagnostics.Add(new UIKitBindDiagnostic(
                UIKitBindDiagnosticSeverity.Warning,
                path,
                message));
        }

        /// <summary>构造从 Prefab 根到当前节点的稳定层级路径。</summary>
        private static string BuildPath(Transform target)
        {
            var segments = new Stack<string>();
            Transform current = target;
            while (current != default)
            {
                segments.Push(current.name);
                current = current.parent;
            }

            return string.Join("/", segments);
        }
    }
}
#endif
