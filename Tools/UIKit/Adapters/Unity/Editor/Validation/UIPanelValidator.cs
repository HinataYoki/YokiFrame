#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace YokiFrame
{
    /// <summary>提供 UIKit 面板、Prefab 和场景的只读配置校验。</summary>
    public static partial class UIPanelValidator
    {
        /// <summary>校验一个 Panel 根节点及其绑定、引用和布局配置。</summary>
        /// <param name="panelRoot">Panel Prefab 或场景根节点。</param>
        /// <returns>完整校验结果；不会修改目标。</returns>
        public static UIPanelValidationResult ValidatePanel(GameObject panelRoot)
        {
            UIPanelValidationResult result = new(panelRoot);
            if (panelRoot == default)
            {
                AddIssue(
                    result,
                    UIPanelValidationSeverity.Error,
                    UIPanelValidationCategory.Other,
                    "面板根对象为空。",
                    fixSuggestion: "选择有效的 UIPanel 根节点。");
                return result;
            }

            if (panelRoot.GetComponent<UIPanel>() == default)
            {
                AddIssue(
                    result,
                    UIPanelValidationSeverity.Error,
                    UIPanelValidationCategory.Other,
                    "面板根节点缺少 UIPanel 组件。",
                    panelRoot,
                    fixSuggestion: "在 Prefab 根节点挂载具体 UIPanel 类型。");
            }

            ValidateBindings(panelRoot, result);
            ValidateReferences(panelRoot, result);
            ValidateCanvasConfiguration(panelRoot, result);
            ValidatePanelAnimation(panelRoot, result);
            ValidatePanelFocus(panelRoot, result);
            return result;
        }

        /// <summary>校验 Assets 路径下的 Panel Prefab。</summary>
        /// <param name="prefabPath">项目相对 Prefab 路径。</param>
        /// <returns>Prefab 校验结果。</returns>
        public static UIPanelValidationResult ValidatePrefab(string prefabPath)
        {
            GameObject prefab = string.IsNullOrWhiteSpace(prefabPath)
                ? default
                : AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == default)
            {
                UIPanelValidationResult result = new(default);
                AddIssue(
                    result,
                    UIPanelValidationSeverity.Error,
                    UIPanelValidationCategory.Other,
                    "无法加载面板预制体: " + prefabPath,
                    fixSuggestion: "确认路径位于 Assets 内且资源已导入。");
                return result;
            }

            return ValidatePanel(prefab);
        }

        /// <summary>校验当前场景中全部具体 UIPanel，并只返回包含问题的结果。</summary>
        /// <returns>有问题的面板结果列表。</returns>
        public static List<UIPanelValidationResult> ValidateAllPanelsInScene()
        {
            List<UIPanelValidationResult> results = new();
            UIPanel[] panels = Object.FindObjectsByType<UIPanel>(FindObjectsInactive.Exclude);
            for (var index = 0; index < panels.Length; index++)
            {
                UIPanelValidationResult result = ValidatePanel(panels[index].gameObject);
                if (result.Issues.Count > 0) results.Add(result);
            }

            return results;
        }

        /// <summary>把一条问题追加到结果并统一处理可选路径。</summary>
        /// <param name="result">目标结果。</param>
        /// <param name="severity">严重度。</param>
        /// <param name="category">类别。</param>
        /// <param name="message">说明。</param>
        /// <param name="context">可选上下文对象。</param>
        /// <param name="path">层级路径；为空时从对象计算。</param>
        /// <param name="fixSuggestion">修复建议。</param>
        internal static void AddIssue(
            UIPanelValidationResult result,
            UIPanelValidationSeverity severity,
            UIPanelValidationCategory category,
            string message,
            Object context = null,
            string path = "",
            string fixSuggestion = "")
        {
            result.Issues.Add(new UIPanelValidationIssue(
                severity,
                category,
                message,
                context,
                string.IsNullOrEmpty(path) ? GetPath(context, result.Target) : path,
                fixSuggestion));
        }

        /// <summary>生成从面板根到 Unity 对象的层级路径。</summary>
        /// <param name="context">上下文对象。</param>
        /// <param name="root">面板根。</param>
        /// <returns>稳定层级路径。</returns>
        private static string GetPath(Object context, GameObject root)
        {
            Transform target = null;
            if (context is GameObject gameObject)
            {
                target = gameObject.transform;
            }
            else
            {
                Component component = context as Component;
                if (component != default) target = component.transform;
            }
            if (target == default || root == default) return string.Empty;
            List<string> names = new();
            Transform current = target;
            while (current != default)
            {
                names.Add(current.name);
                if (current == root.transform) break;
                current = current.parent;
            }

            names.Reverse();
            return string.Join("/", names);
        }
    }
}
#endif
