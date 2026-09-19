#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>连接 Bind Inspector 与统一生成布局，支持尚未生成具体脚本的 Element/Component。</summary>
    internal static class UIKitBindCodeService
    {
        /// <summary>从当前绑定创建代码并登记编译后挂载；不依赖当前选择或已存在的生成组件。</summary>
        internal static void Generate(AbstractBind bind)
        {
            RequireGeneratedBind(bind);
            UIKitPanelCodeLayout layout = ResolveLayout(bind);
            Dictionary<string, string> sources = UIKitPanelCodeGenerator.BuildBindSources(layout, bind);
            bool changed = UIKitPanelCodeGenerator.CommitSources(sources);
            SaveAndQueue(bind, layout);
            if (changed) AssetDatabase.Refresh();
            else UIKitPendingBindingService.Process();
        }

        /// <summary>保存当前 Prefab 编辑并登记精确 owner；新类型可在编译后按路径首次挂载。</summary>
        internal static void SaveAndQueue(AbstractBind bind, UIKitPanelCodeLayout layout)
        {
            UIKitGeneratedOwnerCodeService.ResolvePrefab(bind, out _, out _, out string ownerPath);
            var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && bind.gameObject.scene == stage.prefabContentsRoot.scene)
                PrefabUtility.SaveAsPrefabAsset(stage.prefabContentsRoot, stage.assetPath);
            else if (PrefabUtility.IsPartOfPrefabInstance(bind))
                PrefabUtility.ApplyObjectOverride(bind, layout.PrefabPath, InteractionMode.AutomatedAction);
            AssetDatabase.SaveAssets();
            UIKitBindOutputKind kind = GetOutputKind(bind.Bind);
            UIKitPendingBindingService.QueueCore(layout, layout.GetFullTypeName(kind, GetTypeName(bind)),
                layout.AssemblyName, (int)(bind.Bind == BindType.Element
                    ? UIKitGeneratedOwnerKind.Element : UIKitGeneratedOwnerKind.Component), ownerPath);
        }

        /// <summary>优先恢复已编译 owner 的真实脚本布局，否则沿祖先绑定确定作用域和项目输出位置。</summary>
        internal static UIKitPanelCodeLayout ResolveLayout(AbstractBind bind, bool includeSelf = true)
        {
            UIKitGeneratedOwnerCodeService.ResolvePrefab(bind, out _, out string prefabPath, out _);
            Transform current = includeSelf ? bind.transform : bind.transform.parent;
            string componentName = null;
            while (current != default)
            {
                UIElement owner = current.GetComponent<UIElement>();
                if (owner != default)
                {
                    UIKitPanelCodeLayout layout = UIKitGeneratedOwnerCodeService.CreateLayout(owner.GetType(),
                        owner is UIComponent ? UIKitGeneratedOwnerKind.Component : UIKitGeneratedOwnerKind.Element,
                        UIKitGeneratedOwnerCodeService.GetScriptPath(owner), prefabPath);
                    return componentName == null ? layout : layout.ForComponent(componentName);
                }
                AbstractBind ancestor = current == bind.transform ? default : current.GetComponent<AbstractBind>();
                if (componentName == null && ancestor != default && ancestor.Bind == BindType.Component)
                    componentName = GetTypeName(ancestor);
                UIPanel panel = current.GetComponent<UIPanel>();
                if (panel != default) return CreatePanelLayout(panel, prefabPath, componentName);
                current = current.parent;
            }
            UIKitPanelGenerationRequest request = UIKitPanelGenerationRequest.CreateDefault(Path.GetFileNameWithoutExtension(prefabPath));
            request.prefabPath = prefabPath;
            return new UIKitPanelCodeLayout(request, componentName);
        }

        /// <summary>从 Panel 实际脚本恢复自定义输出根，避免 Inspector 回退到其它项目默认目录。</summary>
        private static UIKitPanelCodeLayout CreatePanelLayout(UIPanel panel, string prefabPath, string componentName)
        {
            Type type = panel.GetType();
            string script = AssetDatabase.GetAssetPath(MonoScript.FromMonoBehaviour(panel));
            UIKitPanelGenerationRequest request = UIKitPanelGenerationRequest.CreateDefault(type.Name);
            request.scriptFolder = Path.GetDirectoryName(Path.GetDirectoryName(script)).Replace('\\', '/');
            request.scriptNamespace = type.Namespace;
            request.assemblyName = type.Assembly.GetName().Name;
            request.prefabPath = prefabPath;
            return new UIKitPanelCodeLayout(request, componentName);
        }

        /// <summary>返回已有或待生成绑定类型的 Designer 路径，供跳转和转换共用。</summary>
        internal static string GetScriptPath(UIKitPanelCodeLayout layout, BindType kind, string name, bool designer)
        {
            return kind == BindType.Element ? layout.GetElementPath(name, designer) : layout.GetComponentPath(name, designer);
        }

        /// <summary>按照绑定策略读取生成类型名，避免 Inspector 与生成器使用不同的回退顺序。</summary>
        internal static string GetTypeName(AbstractBind bind)
        {
            if (!UIKitBindStrategyRegistry.TryGet(bind, out IUIKitBindStrategy strategy, out string error)
                || !strategy.TryResolve(bind, out string name, out _, out error))
                throw new InvalidOperationException(error);
            return CodeGenKit.RequireIdentifier(name, nameof(name));
        }

        /// <summary>限定此入口只生成 Element/Component，不把任意绑定误生成为 Panel。</summary>
        private static void RequireGeneratedBind(AbstractBind bind)
        {
            if (bind == default || (bind.Bind != BindType.Element && bind.Bind != BindType.Component))
                throw new InvalidOperationException("请选择 Element 或 Component 绑定。");
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("请等待脚本编译和资源导入完成。");
        }

        /// <summary>将已有绑定枚举映射为生成输出类型，调用方须先确认属于生成绑定。</summary>
        internal static UIKitBindOutputKind GetOutputKind(BindType kind)
        {
            return kind == BindType.Element ? UIKitBindOutputKind.Element : UIKitBindOutputKind.Component;
        }
    }
}
#endif
