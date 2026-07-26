#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>表示编译后 Prefab 引用回填状态。</summary>
    internal enum UIKitPrefabBindingStatus
    {
        /// <summary>生成类型尚未完成编译，稍后重试。</summary>
        Pending,

        /// <summary>所有组件和序列化引用已经提交。</summary>
        Success,

        /// <summary>契约错误阻止回填，不应继续重试。</summary>
        Failed,
    }

    /// <summary>
    /// 在生成类型完成编译后挂载 Panel/Element/Component 并回填序列化字段。
    /// </summary>
    internal static class UIKitPrefabBindingProcessor
    {
        /// <summary>
        /// 打开目标 Prefab 内容、执行全量回填并原子保存 Unity Prefab 资产。
        /// </summary>
        internal static UIKitPrefabBindingStatus Bind(
            UIKitPanelCodeLayout layout,
            out string error)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(layout.PrefabPath);
            if (root == default)
            {
                error = "无法加载 Prefab: " + layout.PrefabPath;
                return UIKitPrefabBindingStatus.Failed;
            }

            try
            {
                UIKitPrefabBindingStatus status = BindContents(layout, root, out error);
                if (status == UIKitPrefabBindingStatus.Success)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, layout.PrefabPath);
                    AssetDatabase.ImportAsset(layout.PrefabPath);
                }

                return status;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>加载 Prefab，并按具体 UIElement/UIComponent 类型执行独立 owner 回填。</summary>
        internal static UIKitPrefabBindingStatus BindOwner(
            UIKitPanelCodeLayout layout,
            string ownerTypeName,
            string ownerAssemblyName,
            UIKitGeneratedOwnerKind ownerKind,
            string ownerPath,
            out string error)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(layout.PrefabPath);
            if (root == default)
            {
                error = "无法加载 Prefab: " + layout.PrefabPath;
                return UIKitPrefabBindingStatus.Failed;
            }
            try
            {
                Type ownerType = ResolveType(ownerTypeName, ownerAssemblyName);
                error = string.Empty;
                UIKitPrefabBindingStatus status = ownerType == null
                    ? UIKitPrefabBindingStatus.Pending
                    : BindOwnerContents(layout, root, ownerPath, ownerType, ownerKind, out error);
                if (ownerType == null)
                    error = "等待绑定 owner 类型完成编译: " + ownerTypeName;
                if (status == UIKitPrefabBindingStatus.Success)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, layout.PrefabPath);
                    AssetDatabase.ImportAsset(layout.PrefabPath);
                }
                return status;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>兼容无路径的旧内部调用，把 owner 视为 Prefab 根节点。</summary>
        internal static UIKitPrefabBindingStatus BindOwner(
            UIKitPanelCodeLayout layout,
            string ownerTypeName,
            string ownerAssemblyName,
            UIKitGeneratedOwnerKind ownerKind,
            out string error)
        {
            return BindOwner(
                layout,
                ownerTypeName,
                ownerAssemblyName,
                ownerKind,
                string.Empty,
                out error);
        }

        /// <summary>在已加载 Prefab 中按相对路径扫描并回填具体 Element/Component owner。</summary>
        internal static UIKitPrefabBindingStatus BindOwnerContents(
            UIKitPanelCodeLayout layout,
            GameObject root,
            string ownerPath,
            Type ownerType,
            UIKitGeneratedOwnerKind ownerKind,
            out string error)
        {
            if (!IsGeneratedOwnerType(ownerType, ownerKind))
            {
                error = "绑定 owner 类型与 kind 不匹配: "
                    + (ownerType == null ? string.Empty : ownerType.FullName);
                return UIKitPrefabBindingStatus.Failed;
            }
            Component owner = ResolveOwner(root, ownerPath, ownerType, out error);
            if (owner == default)
                return UIKitPrefabBindingStatus.Failed;
            UIKitBindScanResult scan = UIKitBindScanner.ScanOwner(owner.gameObject, ownerKind);
            if (scan.HasErrors)
            {
                error = BuildDiagnostics(scan);
                return UIKitPrefabBindingStatus.Failed;
            }
            return AssignOwner(layout, owner, scan.Nodes, out error);
        }

        /// <summary>兼容无路径的旧内部调用，把 owner 视为 Prefab 根节点。</summary>
        internal static UIKitPrefabBindingStatus BindOwnerContents(
            UIKitPanelCodeLayout layout,
            GameObject root,
            Type ownerType,
            UIKitGeneratedOwnerKind ownerKind,
            out string error)
        {
            return BindOwnerContents(layout, root, string.Empty, ownerType, ownerKind, out error);
        }

        /// <summary>按 sibling-index 路径定位 Prefab 中的具体 owner 组件。</summary>
        private static Component ResolveOwner(
            GameObject root,
            string ownerPath,
            Type ownerType,
            out string error)
        {
            if (root == default)
            {
                error = "Prefab 根为空。";
                return default;
            }
            Transform current = root.transform;
            string normalizedPath = ownerPath ?? string.Empty;
            if (normalizedPath.Length > 0)
            {
                string[] segments = normalizedPath.Split('/');
                for (var index = 0; index < segments.Length; index++)
                {
                    if (!int.TryParse(segments[index], out int siblingIndex)
                        || siblingIndex < 0
                        || siblingIndex >= current.childCount)
                    {
                        error = "Prefab owner 层级路径不存在: " + normalizedPath;
                        return default;
                    }
                    current = current.GetChild(siblingIndex);
                }
            }

            Component owner = current.GetComponent(ownerType);
            if (owner == default)
            {
                error = "Prefab 层级路径缺少绑定 owner: " + normalizedPath;
                return default;
            }
            error = string.Empty;
            return owner;
        }

        /// <summary>判断具体类型是否匹配独立生成 owner kind。</summary>
        private static bool IsGeneratedOwnerType(Type ownerType, UIKitGeneratedOwnerKind ownerKind)
        {
            if (ownerType == null || ownerType.IsAbstract)
                return false;
            return ownerKind == UIKitGeneratedOwnerKind.Element
                ? typeof(UIElement).IsAssignableFrom(ownerType)
                    && !typeof(UIComponent).IsAssignableFrom(ownerType)
                : ownerKind == UIKitGeneratedOwnerKind.Component
                    && typeof(UIComponent).IsAssignableFrom(ownerType);
        }

        /// <summary>在已经加载的 Prefab 根上执行类型挂载和递归字段回填。</summary>
        internal static UIKitPrefabBindingStatus BindContents(
            UIKitPanelCodeLayout layout,
            GameObject root,
            out string error)
        {
            Type panelType = ResolveType(
                layout.ScriptNamespace + "." + layout.PanelName,
                layout.AssemblyName);
            if (panelType == null)
            {
                error = "等待 Panel 类型完成编译: " + layout.PanelName;
                return UIKitPrefabBindingStatus.Pending;
            }

            if (!typeof(UIPanel).IsAssignableFrom(panelType) || panelType.IsAbstract)
            {
                error = "生成 Panel 类型必须是非抽象 UIPanel: " + panelType.FullName;
                return UIKitPrefabBindingStatus.Failed;
            }

            Component panel = root.GetComponent(panelType);
            if (panel == default) panel = root.AddComponent(panelType);
            UIKitBindScanResult scan = UIKitBindScanner.Scan(root);
            if (scan.HasErrors)
            {
                error = BuildDiagnostics(scan);
                return UIKitPrefabBindingStatus.Failed;
            }

            return AssignOwner(layout, panel, scan.Nodes, out error);
        }

        /// <summary>递归回填一个 owner 的直接字段，并为生成节点挂载对应组件。</summary>
        private static UIKitPrefabBindingStatus AssignOwner(
            UIKitPanelCodeLayout layout,
            Component owner,
            List<UIKitBindNode> nodes,
            out string error)
        {
            SerializedObject serialized = new(owner);
            serialized.Update();
            for (var index = 0; index < nodes.Count; index++)
            {
                UIKitBindNode node = nodes[index];
                UIKitPrefabBindingStatus status = ResolveReference(layout, node, out UnityEngine.Object target, out error);
                if (status != UIKitPrefabBindingStatus.Success) return status;
                if (!node.IsRepeated && !AssignProperty(serialized, node, target, out error))
                    return UIKitPrefabBindingStatus.Failed;
                if (target is Component childOwner && node.Strategy.CanContainChildren)
                {
                    status = AssignOwner(layout, childOwner, node.Children, out error);
                    if (status != UIKitPrefabBindingStatus.Success) return status;
                }
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(owner);
            error = string.Empty;
            return UIKitPrefabBindingStatus.Success;
        }

        /// <summary>解析直接引用或等待编译后的生成组件。</summary>
        private static UIKitPrefabBindingStatus ResolveReference(
            UIKitPanelCodeLayout layout,
            UIKitBindNode node,
            out UnityEngine.Object target,
            out string error)
        {
            if (node.Strategy.OutputKind == UIKitBindOutputKind.Member)
            {
                target = node.Target;
                error = target == default ? "Member 引用为空: " + node.Path : string.Empty;
                return target == default ? UIKitPrefabBindingStatus.Failed : UIKitPrefabBindingStatus.Success;
            }

            string namespaceName = node.Strategy.OutputKind == UIKitBindOutputKind.Element
                ? layout.GetElementNamespace()
                : layout.ScriptNamespace;
            Type generatedType = ResolveType(namespaceName + "." + node.TypeName, layout.AssemblyName);
            if (generatedType == null)
            {
                target = default;
                error = "等待 Bind 生成类型完成编译: " + namespaceName + "." + node.TypeName;
                return UIKitPrefabBindingStatus.Pending;
            }

            Type expectedBase = node.Strategy.OutputKind == UIKitBindOutputKind.Element
                ? typeof(UIElement)
                : typeof(UIComponent);
            if (!expectedBase.IsAssignableFrom(generatedType) || generatedType.IsAbstract)
            {
                target = default;
                error = "生成类型基类不匹配: " + generatedType.FullName;
                return UIKitPrefabBindingStatus.Failed;
            }

            Component component = node.Bind.GetComponent(generatedType);
            if (component == default) component = node.Bind.gameObject.AddComponent(generatedType);
            target = component;
            error = string.Empty;
            return UIKitPrefabBindingStatus.Success;
        }

        /// <summary>把一个对象引用写入 owner 的同名 Unity 序列化字段。</summary>
        private static bool AssignProperty(
            SerializedObject serialized,
            UIKitBindNode node,
            UnityEngine.Object target,
            out string error)
        {
            SerializedProperty property = serialized.FindProperty(node.FieldName);
            if (property == null)
            {
                error = "生成字段不存在: " + serialized.targetObject.GetType().FullName + "." + node.FieldName;
                return false;
            }

            if (property.propertyType != SerializedPropertyType.ObjectReference)
            {
                error = "生成字段不是 Unity 对象引用: " + node.FieldName;
                return false;
            }

            property.objectReferenceValue = target;
            error = string.Empty;
            return true;
        }

        /// <summary>按程序集优先、全域兜底解析生成类型。</summary>
        private static Type ResolveType(string fullName, string assemblyName)
        {
            Type resolved = Type.GetType(fullName + ", " + assemblyName, false);
            if (resolved != null) return resolved;
            var types = TypeCache.GetTypesDerivedFrom<MonoBehaviour>();
            for (var index = 0; index < types.Count; index++)
            {
                Type candidate = types[index];
                if (!string.Equals(candidate.Assembly.GetName().Name, assemblyName, StringComparison.Ordinal))
                    continue;
                if (string.Equals(candidate.FullName, fullName, StringComparison.Ordinal))
                    return candidate;
            }

            return null;
        }

        /// <summary>把阻断扫描诊断压缩为可定位错误文本。</summary>
        private static string BuildDiagnostics(UIKitBindScanResult scan)
        {
            List<string> errors = new();
            for (var index = 0; index < scan.Diagnostics.Count; index++)
            {
                UIKitBindDiagnostic diagnostic = scan.Diagnostics[index];
                if (diagnostic.Severity == UIKitBindDiagnosticSeverity.Error)
                    errors.Add(diagnostic.Path + ": " + diagnostic.Message);
            }

            return string.Join(" | ", errors);
        }
    }
}
#endif
