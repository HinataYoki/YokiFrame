#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>编排 UIElement/UIComponent Inspector 的独立代码生成与延迟回填。</summary>
    internal static class UIKitGeneratedOwnerCodeService
    {
        private const string ELEMENT_FOLDER = "UIElement";
        private const string COMPONENT_FOLDER = "UIComponent";
        private const string ELEMENT_NAMESPACE_SUFFIX = "UIElement";

        /// <summary>为当前具体 owner 生成 Designer 与内部绑定类型。</summary>
        internal static void Generate(Component owner, UIKitGeneratedOwnerKind ownerKind)
        {
            UIKitGeneratedOwnerContext context = ResolveContext(owner, ownerKind);
            UIKitBindScanResult scan = UIKitBindScanner.ScanOwner(context.ScanRoot, ownerKind);
            Dictionary<string, string> sources = UIKitPanelCodeGenerator.BuildOwnerSources(
                context.Layout,
                scan,
                ownerKind,
                context.OwnerType,
                context.DesignerPath);
            bool scriptsChanged = UIKitPanelCodeGenerator.CommitSources(sources);
            AssetDatabase.SaveAssets();
            UIKitPendingBindingService.QueueOwner(
                context.Layout,
                context.OwnerType,
                ownerKind,
                context.OwnerPath);
            if (scriptsChanged)
                AssetDatabase.Refresh();
            else
                UIKitPendingBindingService.Process();
        }

        /// <summary>打开当前 owner 的用户脚本。</summary>
        internal static void OpenScript(Component owner)
        {
            MonoScript script = ResolveMonoScript(owner);
            if (!AssetDatabase.OpenAsset(script))
                throw new InvalidOperationException("无法打开 owner 脚本: " + script.name);
        }

        /// <summary>返回当前 owner 的 MonoScript Asset 路径，供 Inspector 只读展示。</summary>
        internal static string GetScriptPath(Component owner)
        {
            MonoScript script = ResolveMonoScript(owner);
            return AssetDatabase.GetAssetPath(script);
        }

        /// <summary>解析 Prefab、脚本、类型和现有生成目录组成的安全上下文。</summary>
        private static UIKitGeneratedOwnerContext ResolveContext(
            Component owner,
            UIKitGeneratedOwnerKind ownerKind)
        {
            ValidateOwner(owner, ownerKind);
            ResolvePrefab(
                owner,
                out GameObject scanRoot,
                out string prefabPath,
                out string ownerPath);
            string scriptPath = GetScriptPath(owner);
            UIKitPanelCodeLayout layout = CreateLayout(
                owner.GetType(),
                ownerKind,
                scriptPath,
                prefabPath);
            string designerPath = ReplaceFileName(
                scriptPath,
                owner.GetType().Name + ".Designer.cs");
            return new UIKitGeneratedOwnerContext(
                scanRoot,
                owner.GetType(),
                designerPath,
                layout,
                ownerPath);
        }

        /// <summary>验证 owner 是非抽象、kind 匹配的具体 Unity 组件。</summary>
        private static void ValidateOwner(Component owner, UIKitGeneratedOwnerKind ownerKind)
        {
            if (owner == default)
                throw new ArgumentNullException(nameof(owner));
            Type ownerType = owner.GetType();
            bool valid = ownerKind == UIKitGeneratedOwnerKind.Element
                ? typeof(UIElement).IsAssignableFrom(ownerType)
                    && !typeof(UIComponent).IsAssignableFrom(ownerType)
                : ownerKind == UIKitGeneratedOwnerKind.Component
                    && typeof(UIComponent).IsAssignableFrom(ownerType);
            if (!valid || ownerType.IsAbstract)
                throw new InvalidOperationException("Inspector owner 类型与生成 kind 不匹配: " + ownerType.FullName);
        }

        /// <summary>解析 owner 所属 Prefab，并记录相对根的确定 sibling-index 路径。</summary>
        private static void ResolvePrefab(
            Component owner,
            out GameObject scanRoot,
            out string prefabPath,
            out string ownerPath)
        {
            PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (IsInPrefabStage(owner, stage))
            {
                ResolvePrefabContext(
                    owner,
                    stage.prefabContentsRoot,
                    stage.assetPath,
                    out scanRoot,
                    out prefabPath,
                    out ownerPath);
                return;
            }
            string directPath = AssetDatabase.GetAssetPath(owner.gameObject);
            GameObject hierarchyRoot = string.IsNullOrEmpty(directPath)
                ? PrefabUtility.GetNearestPrefabInstanceRoot(owner.gameObject)
                : AssetDatabase.LoadAssetAtPath<GameObject>(directPath);
            if (string.IsNullOrEmpty(directPath) && hierarchyRoot != default)
            {
                directPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(hierarchyRoot);
            }
            if (hierarchyRoot == default || string.IsNullOrEmpty(directPath))
                throw new InvalidOperationException("请先把当前绑定 owner 保存为 Prefab。");
            ResolvePrefabContext(
                owner,
                hierarchyRoot,
                directPath,
                out scanRoot,
                out prefabPath,
                out ownerPath);
        }

        /// <summary>判断 owner 是否属于当前 Prefab Stage。</summary>
        private static bool IsInPrefabStage(Component owner, PrefabStage stage)
        {
            return stage != null
                && stage.prefabContentsRoot != default
                && owner.gameObject.scene == stage.prefabContentsRoot.scene;
        }

        /// <summary>完成 Prefab 上下文校验，并把扫描范围限制到当前 owner 子树。</summary>
        internal static void ResolvePrefabContext(
            Component owner,
            GameObject prefabRoot,
            string assetPath,
            out GameObject scanRoot,
            out string prefabPath,
            out string ownerPath)
        {
            if (prefabRoot == default || string.IsNullOrEmpty(assetPath))
                throw new InvalidOperationException("无法解析当前绑定 owner 所属的 Prefab。");
            ownerPath = BuildOwnerPath(prefabRoot.transform, owner.transform);
            scanRoot = owner.gameObject;
            prefabPath = assetPath;
        }

        /// <summary>使用 sibling index 构造从 Prefab 根到 owner 的无歧义相对路径。</summary>
        private static string BuildOwnerPath(Transform prefabRoot, Transform owner)
        {
            List<string> segments = new();
            Transform current = owner;
            while (current != default && current != prefabRoot)
            {
                segments.Add(current.GetSiblingIndex().ToString(CultureInfo.InvariantCulture));
                current = current.parent;
            }
            if (current != prefabRoot)
                throw new InvalidOperationException("当前绑定 owner 不属于已解析的 Prefab 层级。");
            segments.Reverse();
            return string.Join("/", segments);
        }

        /// <summary>从具体 owner 解析唯一 MonoScript。</summary>
        private static MonoScript ResolveMonoScript(Component owner)
        {
            MonoBehaviour behaviour = owner as MonoBehaviour;
            if (behaviour == default)
                throw new InvalidOperationException("绑定 owner 必须是 MonoBehaviour。");
            MonoScript script = MonoScript.FromMonoBehaviour(behaviour);
            if (script == default)
                throw new InvalidOperationException("无法解析绑定 owner 的 MonoScript。");
            string path = AssetDatabase.GetAssetPath(script);
            if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/", StringComparison.Ordinal))
                throw new InvalidOperationException("绑定 owner 脚本必须位于当前项目 Assets。");
            return script;
        }

        /// <summary>根据 owner kind 从标准脚本路径恢复现有 Panel 布局。</summary>
        private static UIKitPanelCodeLayout CreateLayout(
            Type ownerType,
            UIKitGeneratedOwnerKind ownerKind,
            string scriptPath,
            string prefabPath)
        {
            string scriptDirectory = GetAssetDirectory(scriptPath);
            UIKitPanelGenerationRequest request = UIKitPanelGenerationRequest.CreateDefault();
            request.prefabFolder = GetAssetDirectory(prefabPath);
            request.prefabPath = prefabPath;
            request.assemblyName = ownerType.Assembly.GetName().Name;
            if (ownerKind == UIKitGeneratedOwnerKind.Element)
                ConfigureElementLayout(request, ownerType, scriptDirectory);
            else
                ConfigureComponentLayout(request, ownerType, scriptDirectory);
            return new UIKitPanelCodeLayout(request);
        }

        /// <summary>从 `<Panel>/UIElement` 目录和命名空间后缀恢复 Element 布局。</summary>
        private static void ConfigureElementLayout(
            UIKitPanelGenerationRequest request,
            Type ownerType,
            string scriptDirectory)
        {
            string namespaceName = ownerType.Namespace ?? string.Empty;
            int separator = namespaceName.LastIndexOf('.');
            string segment = separator < 0 ? namespaceName : namespaceName.Substring(separator + 1);
            if (!segment.EndsWith(ELEMENT_NAMESPACE_SUFFIX, StringComparison.Ordinal)
                || segment.Length == ELEMENT_NAMESPACE_SUFFIX.Length
                || separator <= 0
                || !scriptDirectory.EndsWith("/" + ELEMENT_FOLDER, StringComparison.Ordinal))
                throw new InvalidOperationException("UIElement 脚本不在标准 Panel/UIElement 生成布局中。");
            string panelName = segment.Substring(0, segment.Length - ELEMENT_NAMESPACE_SUFFIX.Length);
            string panelFolder = GetAssetDirectory(scriptDirectory);
            if (!panelFolder.EndsWith("/" + panelName, StringComparison.Ordinal))
                throw new InvalidOperationException("UIElement 目录与命名空间中的 Panel 名不一致。");
            request.panelName = panelName;
            request.scriptFolder = GetAssetDirectory(panelFolder);
            request.scriptNamespace = namespaceName.Substring(0, separator);
        }

        /// <summary>从 `UIComponent` 目录和具体类型命名空间恢复 Component 布局。</summary>
        private static void ConfigureComponentLayout(
            UIKitPanelGenerationRequest request,
            Type ownerType,
            string scriptDirectory)
        {
            if (!scriptDirectory.EndsWith("/" + COMPONENT_FOLDER, StringComparison.Ordinal))
                throw new InvalidOperationException("UIComponent 脚本不在标准 UIComponent 生成目录中。");
            request.panelName = ownerType.Name;
            request.scriptFolder = GetAssetDirectory(scriptDirectory);
            request.scriptNamespace = ownerType.Namespace ?? string.Empty;
        }

        /// <summary>返回 Assets 文件或目录的父目录。</summary>
        private static string GetAssetDirectory(string assetPath)
        {
            string normalized = assetPath.Replace('\\', '/').TrimEnd('/');
            int separator = normalized.LastIndexOf('/');
            if (separator <= 0)
                throw new InvalidOperationException("无法解析 Asset 父目录: " + assetPath);
            return normalized.Substring(0, separator);
        }

        /// <summary>在保持目录不变的情况下替换 Asset 文件名。</summary>
        private static string ReplaceFileName(string assetPath, string fileName)
        {
            return UIKitPanelCodeLayout.CombineAssetPath(GetAssetDirectory(assetPath), fileName);
        }

        /// <summary>保存一次独立 owner 生成需要的不可变上下文。</summary>
        private sealed class UIKitGeneratedOwnerContext
        {
            /// <summary>创建已经完成路径和类型校验的生成上下文。</summary>
            internal UIKitGeneratedOwnerContext(
                GameObject scanRoot,
                Type ownerType,
                string designerPath,
                UIKitPanelCodeLayout layout,
                string ownerPath)
            {
                ScanRoot = scanRoot;
                OwnerType = ownerType;
                DesignerPath = designerPath;
                Layout = layout;
                OwnerPath = ownerPath;
            }

            internal GameObject ScanRoot { get; }
            internal Type OwnerType { get; }
            internal string DesignerPath { get; }
            internal UIKitPanelCodeLayout Layout { get; }
            internal string OwnerPath { get; }
        }
    }
}
#endif
