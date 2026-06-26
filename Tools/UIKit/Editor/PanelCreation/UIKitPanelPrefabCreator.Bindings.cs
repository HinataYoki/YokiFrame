#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace YokiFrame
{
    internal static partial class UIKitPanelPrefabCreator
    {
        private static void AppendBindingFields(ICodeScope scope, BindCodeInfo bindInfo, UIKitPanelCodeGenContext context)
        {
            var children = GetSortedChildren(bindInfo);
            for (var i = 0; i < children.Count; i++)
            {
                var child = children[i];
                var typeName = GetBindFieldType(child, context);
                var fieldName = GetBindFieldName(child);
                if (string.IsNullOrEmpty(typeName) || string.IsNullOrEmpty(fieldName))
                    continue;

                scope.Field(typeName, fieldName, field => field
                    .WithAccess(AccessModifier.Public));
            }

            if (children.Count > 0)
                scope.EmptyLine();
        }

        private static void AppendClearBindingFields(ICodeScope scope, BindCodeInfo bindInfo)
        {
            var children = GetSortedChildren(bindInfo);
            for (var i = 0; i < children.Count; i++)
            {
                var child = children[i];
                var fieldName = GetBindFieldName(child);
                if (string.IsNullOrEmpty(fieldName))
                    continue;

                scope.Custom(fieldName + " = default;");
            }
        }

        private static string GetBindFieldName(BindCodeInfo bindInfo)
        {
            if (bindInfo == default || bindInfo.RepeatElement || string.IsNullOrEmpty(bindInfo.Name))
                return string.Empty;

            return bindInfo.Name;
        }

        private static string GetBindFieldType(BindCodeInfo bindInfo, UIKitPanelCodeGenContext context)
        {
            var strategy = BindStrategyRegistry.Get(bindInfo.Bind);
            return strategy != default ? strategy.GetFullTypeName(bindInfo, context) : bindInfo.Type;
        }

        private static List<BindCodeInfo> GetSortedChildren(BindCodeInfo bindInfo)
        {
            var children = new List<BindCodeInfo>();
            if (bindInfo == default || bindInfo.MemberDic == default)
                return children;

            foreach (var pair in bindInfo.MemberDic)
            {
                if (pair.Value != default)
                    children.Add(pair.Value);
            }

            children.Sort(static (left, right) => left.Order.CompareTo(right.Order));
            return children;
        }

        private static bool TryBindGeneratedPanel(string panelName, string scriptNamespace, string prefabPath, string scriptFolder, string assemblyName)
        {
            var panelType = ResolveType(scriptNamespace + "." + panelName, assemblyName);
            if (panelType == default || !typeof(UIPanel).IsAssignableFrom(panelType))
                return false;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == default)
                return true;

            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                if (root.GetComponent(panelType) == default)
                    root.AddComponent(panelType);

                var request = new UIKitPanelCreateRequest
                {
                    PanelName = panelName,
                    ScriptNamespace = scriptNamespace,
                    ScriptFolder = scriptFolder,
                    PrefabFolder = Path.GetDirectoryName(prefabPath),
                    AssemblyName = string.IsNullOrEmpty(assemblyName) ? DEFAULT_ASSEMBLY_NAME : assemblyName
                };
                var context = new UIKitPanelCodeGenContext(panelName, scriptFolder, scriptNamespace);
                var bindInfo = CollectBindInfo(root, panelName);
                AssignBindReferences(root, panelType, bindInfo, context);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                AssetDatabase.ImportAsset(prefabPath);
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void AssignBindReferences(GameObject root, Type panelType, BindCodeInfo bindInfo, UIKitPanelCodeGenContext context)
        {
            var panel = root.GetComponent(panelType);
            if (panel == default)
                return;

            var serialized = new SerializedObject(panel);
            AssignBindReferencesRecursive(serialized, bindInfo, context);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssignBindReferencesRecursive(SerializedObject serialized, BindCodeInfo bindInfo, UIKitPanelCodeGenContext context)
        {
            var children = GetSortedChildren(bindInfo);
            for (var i = 0; i < children.Count; i++)
            {
                var child = children[i];
                var fieldName = GetBindFieldName(child);
                if (string.IsNullOrEmpty(fieldName))
                    continue;

                var objectReference = ResolveBindObjectReference(child, context);
                var property = serialized.FindProperty(fieldName);
                if (property != default && property.propertyType == SerializedPropertyType.ObjectReference)
                    property.objectReferenceValue = objectReference;

                var childComponent = objectReference as Component;
                var strategy = BindStrategyRegistry.Get(child.Bind);
                if (childComponent == default || strategy == default || !strategy.RequiresClassFile)
                    continue;

                var childSerialized = new SerializedObject(childComponent);
                AssignBindReferencesRecursive(childSerialized, child, context);
                childSerialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static UnityEngine.Object ResolveBindObjectReference(BindCodeInfo bindInfo, UIKitPanelCodeGenContext context)
        {
            if (bindInfo == default || bindInfo.Self == default)
                return default;

            if (bindInfo.Bind == BindType.Member)
            {
                var memberType = ResolveType(bindInfo.Type);
                if (memberType != default && typeof(Component).IsAssignableFrom(memberType))
                    return bindInfo.Self.GetComponent(memberType);

                if (memberType == typeof(GameObject))
                    return bindInfo.Self;
            }

            return AssignGeneratedBindComponent(bindInfo, context);
        }

        private static Component AssignGeneratedBindComponent(BindCodeInfo bindInfo, UIKitPanelCodeGenContext context)
        {
            var typeName = GetBindFieldType(bindInfo, context);
            var type = ResolveType(typeName);
            if (type == default || !typeof(Component).IsAssignableFrom(type) || bindInfo.Self == default)
                return default;

            var component = bindInfo.Self.GetComponent(type);
            if (component == default)
                component = bindInfo.Self.AddComponent(type);

            return component;
        }
    }
}
#endif
