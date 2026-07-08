#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 编译后为 UIKit 生成类型挂载组件并回填 Prefab 引用。
    /// </summary>
    internal static class UIKitPrefabBindingProcessor
    {
        private const string LOG_PREFIX = "[UIKitPrefabBindingProcessor] ";
        private const string DEFAULT_ASSEMBLY_NAME = "Assembly-CSharp";

        private enum GeneratedTypeResolveStatus
        {
            Resolved,
            Pending,
            Invalid,
        }

        private enum GeneratedComponentAssignStatus
        {
            Assigned,
            Pending,
            Invalid,
        }

        /// <summary>
        /// 尝试为已生成并完成编译的 UIPanel Prefab 回填绑定引用。
        /// </summary>
        public static bool TryBindGeneratedPanel(
            string panelName,
            string scriptNamespace,
            string prefabPath,
            string scriptFolder,
            string assemblyName)
        {
            if (string.IsNullOrEmpty(panelName) ||
                string.IsNullOrEmpty(prefabPath) ||
                string.IsNullOrEmpty(scriptFolder))
            {
                Debug.LogError(LOG_PREFIX + "待绑定面板参数不完整: " + prefabPath);
                return true;
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogWarning(LOG_PREFIX + "待绑定 Prefab 已不存在，移出队列: " + prefabPath);
                return true;
            }

            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var noPendingGeneratedTypes = TryBindGeneratedPanelContents(
                    panelName,
                    scriptNamespace,
                    root,
                    scriptFolder,
                    assemblyName);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                AssetDatabase.ImportAsset(prefabPath);
                return noPendingGeneratedTypes;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>
        /// 在已加载的 Prefab 内容根节点上直接挂载生成组件并回填引用。
        /// </summary>
        public static bool TryBindGeneratedPanelContents(
            string panelName,
            string scriptNamespace,
            GameObject root,
            string scriptFolder,
            string assemblyName)
        {
            if (string.IsNullOrEmpty(panelName) ||
                root == null ||
                string.IsNullOrEmpty(scriptFolder))
            {
                Debug.LogError(LOG_PREFIX + "待绑定面板内容参数不完整: " + panelName);
                return true;
            }

            var normalizedAssemblyName = string.IsNullOrEmpty(assemblyName)
                ? DEFAULT_ASSEMBLY_NAME
                : assemblyName;
            var panelFullName = BuildQualifiedName(scriptNamespace, panelName);
            var panelScriptPath = GetPanelScriptPath(panelName, scriptFolder);
            var panelResolveStatus = ResolveGeneratedType(
                    panelScriptPath,
                    panelFullName,
                    normalizedAssemblyName,
                    typeof(UIPanel),
                    out var panelType);
            if (panelResolveStatus == GeneratedTypeResolveStatus.Pending)
            {
                Debug.LogWarning(
                    LOG_PREFIX + "等待面板类型完成编译: " + panelFullName +
                    " script=" + panelScriptPath);
                return false;
            }

            if (panelResolveStatus == GeneratedTypeResolveStatus.Invalid)
                return true;

            if (panelType.IsAbstract)
            {
                Debug.LogError(LOG_PREFIX + "面板类型不能是抽象类: " + panelType.FullName);
                return true;
            }

            var panel = root.GetComponent(panelType);
            if (panel == null)
                panel = root.AddComponent(panelType);

            var context = new UIKitPanelCodeGenContext(panelName, scriptFolder, scriptNamespace);
            var bindInfo = CollectBindInfo(root, panelName);
            var noPendingGeneratedTypes = AssignBindReferences(
                panel,
                bindInfo,
                context,
                normalizedAssemblyName);
            EditorUtility.SetDirty(root);
            EditorUtility.SetDirty(panel);
            return noPendingGeneratedTypes;
        }

        private static BindCodeInfo CollectBindInfo(GameObject root, string panelName)
        {
            var bindInfo = new BindCodeInfo
            {
                Type = panelName,
                Name = panelName,
                Self = root,
                Bind = BindType.Member
            };
            BindCollector.SearchBinds(root.transform, root.name, bindInfo);
            return bindInfo;
        }

        private static bool AssignBindReferences(
            Component owner,
            BindCodeInfo bindInfo,
            UIKitPanelCodeGenContext context,
            string assemblyName)
        {
            if (owner == null)
                return false;

            var hasPendingGeneratedTypes = false;
            var serialized = new SerializedObject(owner);
            serialized.Update();
            AssignBindReferencesRecursive(
                serialized,
                bindInfo,
                context,
                assemblyName,
                ref hasPendingGeneratedTypes);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return !hasPendingGeneratedTypes;
        }

        private static void AssignBindReferencesRecursive(
            SerializedObject serialized,
            BindCodeInfo bindInfo,
            UIKitPanelCodeGenContext context,
            string assemblyName,
            ref bool hasPendingGeneratedTypes)
        {
            var children = GetSortedChildren(bindInfo);
            for (var i = 0; i < children.Count; i++)
            {
                var child = children[i];
                var fieldName = GetBindFieldName(child);
                if (string.IsNullOrEmpty(fieldName))
                    continue;

                var objectReference = ResolveBindObjectReference(child, context, assemblyName, ref hasPendingGeneratedTypes);
                var property = serialized.FindProperty(fieldName);
                if (property != null && property.propertyType == SerializedPropertyType.ObjectReference)
                {
                    property.objectReferenceValue = objectReference;
                }
                else if (property == null)
                {
                    Debug.LogError(
                        LOG_PREFIX + "未找到序列化字段: " + fieldName +
                        " owner=" + serialized.targetObject.GetType().FullName,
                        child.Self);
                }
                else
                {
                    Debug.LogError(
                        LOG_PREFIX + "字段不是对象引用，无法回填: " + fieldName +
                        " owner=" + serialized.targetObject.GetType().FullName,
                        child.Self);
                }

                var childComponent = objectReference as Component;
                var strategy = BindStrategyRegistry.Get(child.Bind);
                if (childComponent == null || strategy == null || !strategy.RequiresClassFile)
                    continue;

                var childSerialized = new SerializedObject(childComponent);
                childSerialized.Update();
                AssignBindReferencesRecursive(childSerialized, child, context, assemblyName, ref hasPendingGeneratedTypes);
                childSerialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static UnityEngine.Object ResolveBindObjectReference(
            BindCodeInfo bindInfo,
            UIKitPanelCodeGenContext context,
            string assemblyName,
            ref bool hasPendingGeneratedTypes)
        {
            if (bindInfo == null || bindInfo.Self == null)
                return null;

            if (bindInfo.Bind == BindType.Member)
                return ResolveMemberReference(bindInfo);

            var assignStatus = TryAssignGeneratedBindComponent(bindInfo, context, assemblyName, out var generatedComponent);
            if (assignStatus == GeneratedComponentAssignStatus.Pending)
                hasPendingGeneratedTypes = true;

            return generatedComponent;
        }

        private static UnityEngine.Object ResolveMemberReference(BindCodeInfo bindInfo)
        {
            if (IsGameObjectTypeName(bindInfo.Type))
                return bindInfo.Self;

            var matchedComponent = FindComponentByTypeName(bindInfo.Self, bindInfo.Type);
            if (matchedComponent != null)
                return matchedComponent;

            var memberType = ResolveType(bindInfo.Type, null);
            if (memberType == typeof(GameObject))
                return bindInfo.Self;

            if (memberType != null && typeof(Component).IsAssignableFrom(memberType))
            {
                var component = bindInfo.Self.GetComponent(memberType);
                if (component == null)
                {
                    Debug.LogError(
                        LOG_PREFIX + "绑定节点缺少成员组件: " + bindInfo.Type +
                        " path=" + bindInfo.PathToRoot,
                        bindInfo.Self);
                }
                return component;
            }

            Debug.LogError(
                LOG_PREFIX + "无法解析成员绑定类型: " + bindInfo.Type +
                " path=" + bindInfo.PathToRoot,
                bindInfo.Self);
            return null;
        }

        private static GeneratedComponentAssignStatus TryAssignGeneratedBindComponent(
            BindCodeInfo bindInfo,
            UIKitPanelCodeGenContext context,
            string assemblyName,
            out Component component)
        {
            component = null;
            var strategy = BindStrategyRegistry.Get(bindInfo.Bind);
            if (strategy == null || !strategy.RequiresClassFile)
                return GeneratedComponentAssignStatus.Invalid;

            var scriptPath = strategy.GetScriptPath(bindInfo, context, false);
            var fullTypeName = BuildGeneratedTypeName(bindInfo, context, strategy);
            var resolveStatus = ResolveGeneratedType(
                    scriptPath,
                    fullTypeName,
                    assemblyName,
                    typeof(Component),
                    out var componentType);
            if (resolveStatus == GeneratedTypeResolveStatus.Pending)
            {
                Debug.LogWarning(
                    LOG_PREFIX + "等待生成绑定类型完成编译: " + fullTypeName +
                    " script=" + scriptPath,
                    bindInfo.Self);
                return GeneratedComponentAssignStatus.Pending;
            }

            if (resolveStatus == GeneratedTypeResolveStatus.Invalid)
                return GeneratedComponentAssignStatus.Invalid;

            if (componentType.IsAbstract)
            {
                Debug.LogError(
                    LOG_PREFIX + "生成绑定类型不能是抽象类: " + componentType.FullName,
                    bindInfo.Self);
                return GeneratedComponentAssignStatus.Invalid;
            }

            component = bindInfo.Self.GetComponent(componentType);
            if (component == null)
                component = bindInfo.Self.AddComponent(componentType);

            return GeneratedComponentAssignStatus.Assigned;
        }

        private static GeneratedTypeResolveStatus ResolveGeneratedType(
            string scriptPath,
            string fullTypeName,
            string assemblyName,
            Type requiredBaseType,
            out Type type)
        {
            var scriptStatus = ResolveTypeFromScript(
                scriptPath,
                requiredBaseType,
                out type,
                out var scriptInvalidReason,
                out var scriptInvalidContext);
            if (scriptStatus == GeneratedTypeResolveStatus.Resolved)
                return scriptStatus;

            type = ResolveType(fullTypeName, assemblyName);
            if (type != null && requiredBaseType.IsAssignableFrom(type))
            {
                if (scriptStatus == GeneratedTypeResolveStatus.Invalid)
                {
                    Debug.LogWarning(
                        LOG_PREFIX + scriptInvalidReason +
                        "；已通过完整类型名解析到: " + type.FullName,
                        scriptInvalidContext);
                }
                return GeneratedTypeResolveStatus.Resolved;
            }

            if (type != null)
            {
                Debug.LogError(
                    LOG_PREFIX + "生成类型继承关系不匹配: " + fullTypeName +
                    " type=" + type.FullName +
                    " required=" + requiredBaseType.FullName);
                type = null;
                return GeneratedTypeResolveStatus.Invalid;
            }

            if (scriptStatus == GeneratedTypeResolveStatus.Invalid)
            {
                Debug.LogError(LOG_PREFIX + scriptInvalidReason, scriptInvalidContext);
                return GeneratedTypeResolveStatus.Invalid;
            }

            type = null;
            return GeneratedTypeResolveStatus.Pending;
        }

        private static GeneratedTypeResolveStatus ResolveTypeFromScript(
            string scriptPath,
            Type requiredBaseType,
            out Type type,
            out string invalidReason,
            out UnityEngine.Object invalidContext)
        {
            type = null;
            invalidReason = null;
            invalidContext = null;
            if (string.IsNullOrEmpty(scriptPath))
                return GeneratedTypeResolveStatus.Pending;

            var script = AssetDatabase.LoadAssetAtPath<MonoScript>(scriptPath);
            if (script == null)
                return GeneratedTypeResolveStatus.Pending;

            type = script.GetClass();
            if (type == null)
                return GeneratedTypeResolveStatus.Pending;

            if (requiredBaseType.IsAssignableFrom(type))
                return GeneratedTypeResolveStatus.Resolved;

            invalidReason =
                "脚本类型不匹配: " + scriptPath +
                " type=" + type.FullName +
                " required=" + requiredBaseType.FullName;
            invalidContext = script;
            type = null;
            return GeneratedTypeResolveStatus.Invalid;
        }

        private static bool IsGameObjectTypeName(string typeName)
        {
            return string.Equals(typeName, nameof(GameObject), StringComparison.Ordinal) ||
                   string.Equals(typeName, typeof(GameObject).FullName, StringComparison.Ordinal);
        }

        private static Component FindComponentByTypeName(GameObject gameObject, string typeName)
        {
            if (gameObject == null || string.IsNullOrEmpty(typeName))
                return null;

            var components = gameObject.GetComponents<Component>();
            for (var i = 0; i < components.Length; i++)
            {
                var component = components[i];
                if (component == null)
                    continue;

                var componentType = component.GetType();
                if (string.Equals(componentType.FullName, typeName, StringComparison.Ordinal) ||
                    string.Equals(componentType.Name, typeName, StringComparison.Ordinal))
                    return component;
            }

            return null;
        }

        private static Type ResolveType(string typeName, string assemblyName)
        {
            if (string.IsNullOrEmpty(typeName))
                return null;

            if (!string.IsNullOrEmpty(assemblyName))
            {
                var assemblyQualifiedType = Type.GetType(typeName + ", " + assemblyName, false);
                if (assemblyQualifiedType != null)
                    return assemblyQualifiedType;

                try
                {
                    var assembly = Assembly.Load(assemblyName);
                    if (assembly != null)
                    {
                        var typeInAssembly = assembly.GetType(typeName, false);
                        if (typeInAssembly != null)
                            return typeInAssembly;
                    }
                }
                catch
                {
                    // 程序集名可能来自旧配置；继续回退到全部已加载程序集扫描。
                }
            }

            var type = Type.GetType(typeName, false);
            if (type != null)
                return type;

            var assemblies = LoadedAssemblyProvider.GetLoadedAssemblies();
            for (var i = 0; i < assemblies.Length; i++)
            {
                type = assemblies[i].GetType(typeName, false);
                if (type != null)
                    return type;
            }

            return null;
        }

        private static string BuildGeneratedTypeName(
            BindCodeInfo bindInfo,
            UIKitPanelCodeGenContext context,
            IBindTypeStrategy strategy)
        {
            var typeName = bindInfo != null ? bindInfo.Type : null;
            if (string.IsNullOrEmpty(typeName))
                return string.Empty;

            if (typeName.IndexOf('.') >= 0)
                return typeName;

            return BuildQualifiedName(strategy.GetNamespace(context), typeName);
        }

        private static string BuildQualifiedName(string typeNamespace, string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
                return string.Empty;

            if (string.IsNullOrEmpty(typeNamespace) || typeName.IndexOf('.') >= 0)
                return typeName;

            return typeNamespace + "." + typeName;
        }

        private static string GetPanelScriptPath(string panelName, string scriptFolder)
        {
            return scriptFolder.TrimEnd('/') + "/" + panelName + "/" + panelName + ".cs";
        }

        private static string GetBindFieldName(BindCodeInfo bindInfo)
        {
            if (bindInfo == null || bindInfo.RepeatElement || string.IsNullOrEmpty(bindInfo.Name))
                return string.Empty;

            return bindInfo.Name;
        }

        private static List<BindCodeInfo> GetSortedChildren(BindCodeInfo bindInfo)
        {
            var children = new List<BindCodeInfo>();
            if (bindInfo == null || bindInfo.MemberDic == null)
                return children;

            foreach (var pair in bindInfo.MemberDic)
            {
                if (pair.Value != null)
                    children.Add(pair.Value);
            }

            children.Sort(static (left, right) => left.Order.CompareTo(right.Order));
            return children;
        }
    }
}
#endif
