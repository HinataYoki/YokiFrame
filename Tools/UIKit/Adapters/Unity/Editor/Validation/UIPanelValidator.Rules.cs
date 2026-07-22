#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace YokiFrame
{
    /// <summary>实现 UIKit 面板校验器的绑定、引用、Canvas、动画和焦点规则。</summary>
    public static partial class UIPanelValidator
    {
        /// <summary>复用确定性 Bind 扫描器，把错误和警告转为公共校验问题。</summary>
        private static void ValidateBindings(GameObject root, UIPanelValidationResult result)
        {
            UIKitBindScanResult scan = UIKitBindScanner.Scan(root);
            for (var index = 0; index < scan.Diagnostics.Count; index++)
            {
                UIKitBindDiagnostic diagnostic = scan.Diagnostics[index];
                AddIssue(
                    result,
                    diagnostic.Severity == UIKitBindDiagnosticSeverity.Error
                        ? UIPanelValidationSeverity.Error
                        : UIPanelValidationSeverity.Warning,
                    UIPanelValidationCategory.Binding,
                    diagnostic.Message,
                    path: diagnostic.Path,
                    fixSuggestion: "在 Bind Inspector 中修复该绑定后重新扫描。");
            }
        }

        /// <summary>检查 Image、Button、Text 和可选 TMP 字体引用。</summary>
        private static void ValidateReferences(GameObject root, UIPanelValidationResult result)
        {
            Image[] images = root.GetComponentsInChildren<Image>(true);
            for (var index = 0; index < images.Length; index++)
            {
                Image image = images[index];
                if (image.sprite == null && image.color.a > 0f && !image.raycastTarget)
                {
                    AddIssue(
                        result,
                        UIPanelValidationSeverity.Warning,
                        UIPanelValidationCategory.Reference,
                        "Image 缺少 Sprite 且仍有可见颜色。",
                        image,
                        fixSuggestion: "设置 Sprite，或将透明度设为 0。");
                }
            }

            Button[] buttons = root.GetComponentsInChildren<Button>(true);
            for (var index = 0; index < buttons.Length; index++)
            {
                Button button = buttons[index];
                if (button.onClick.GetPersistentEventCount() == 0
                    && button.GetComponent<AbstractBind>() == default)
                {
                    AddIssue(
                        result,
                        UIPanelValidationSeverity.Info,
                        UIPanelValidationCategory.Reference,
                        "Button 没有序列化 OnClick 事件。",
                        button,
                        fixSuggestion: "确认该按钮由代码绑定，或添加 OnClick 事件。");
                }
            }

            Text[] texts = root.GetComponentsInChildren<Text>(true);
            for (var index = 0; index < texts.Length; index++)
            {
                if (texts[index].font == null)
                {
                    AddIssue(
                        result,
                        UIPanelValidationSeverity.Error,
                        UIPanelValidationCategory.Reference,
                        "Text 缺少字体引用。",
                        texts[index],
                        fixSuggestion: "设置 Unity UI Text 的 Font。");
                }
            }
        }

        /// <summary>检查 Canvas 嵌套深度和明显多余的 Raycast Target。</summary>
        private static void ValidateCanvasConfiguration(GameObject root, UIPanelValidationResult result)
        {
            Canvas[] canvases = root.GetComponentsInChildren<Canvas>(true);
            for (var index = 0; index < canvases.Length; index++)
            {
                int depth = GetCanvasDepth(canvases[index].transform, root.transform);
                if (depth > 3)
                {
                    AddIssue(
                        result,
                        UIPanelValidationSeverity.Warning,
                        UIPanelValidationCategory.Canvas,
                        "Canvas 嵌套超过 3 层（当前 " + depth + " 层）。",
                        canvases[index],
                        fixSuggestion: "减少嵌套 Canvas，或确认额外 Canvas 确实用于隔离重建。");
                }
            }

            Graphic[] graphics = root.GetComponentsInChildren<Graphic>(true);
            int unnecessaryRaycastCount = 0;
            for (var index = 0; index < graphics.Length; index++)
            {
                Graphic graphic = graphics[index];
                if (!graphic.raycastTarget || graphic.GetComponent<Selectable>() != default)
                    continue;
                if (graphic.GetComponentInParent<ScrollRect>() == default)
                    unnecessaryRaycastCount++;
            }

            if (unnecessaryRaycastCount > 5)
            {
                AddIssue(
                    result,
                    UIPanelValidationSeverity.Info,
                    UIPanelValidationCategory.Canvas,
                    "发现 " + unnecessaryRaycastCount + " 个可能不需要 Raycast Target 的 Graphic。",
                    root,
                    fixSuggestion: "仅为需要接收指针事件的 Graphic 保留 Raycast Target。");
            }
        }

        /// <summary>检查显示/隐藏动画配置的时长、曲线和组合子项。</summary>
        private static void ValidatePanelAnimation(GameObject root, UIPanelValidationResult result)
        {
            UIPanel panel = root.GetComponent<UIPanel>();
            if (panel == default) return;
            ValidateAnimationConfig(panel.ShowAnimationConfig, "显示动画", panel, result);
            ValidateAnimationConfig(panel.HideAnimationConfig, "隐藏动画", panel, result);
        }

        /// <summary>递归检查一个 SerializeReference 动画配置。</summary>
        private static void ValidateAnimationConfig(
            UIAnimationConfig config,
            string label,
            UIPanel panel,
            UIPanelValidationResult result)
        {
            if (config == null) return;
            if (config.Duration < 0f)
            {
                AddIssue(
                    result,
                    UIPanelValidationSeverity.Error,
                    UIPanelValidationCategory.Animation,
                    label + "时长不能为负数。",
                    panel,
                    fixSuggestion: "将动画时长设置为 0 或更大的值。");
            }

            if (config.Curve == null || config.Curve.length == 0)
            {
                AddIssue(
                    result,
                    UIPanelValidationSeverity.Warning,
                    UIPanelValidationCategory.Animation,
                    label + "缺少有效曲线。",
                    panel,
                    fixSuggestion: "重新创建或编辑动画曲线。");
            }

            if (!(config is CompositeAnimationConfig composite)) return;
            if (composite.Animations == null || composite.Animations.Count == 0)
            {
                AddIssue(
                    result,
                    UIPanelValidationSeverity.Warning,
                    UIPanelValidationCategory.Animation,
                    label + "组合没有子动画。",
                    panel,
                    fixSuggestion: "添加至少一个子动画，或清除组合配置。");
                return;
            }

            for (var index = 0; index < composite.Animations.Count; index++)
            {
                if (composite.Animations[index] == null)
                {
                    AddIssue(
                        result,
                        UIPanelValidationSeverity.Warning,
                        UIPanelValidationCategory.Animation,
                        label + "包含空的子动画项。",
                        panel,
                        fixSuggestion: "移除空项或选择具体动画类型。");
                    continue;
                }

                ValidateAnimationConfig(
                    composite.Animations[index],
                    label + "/" + index,
                    panel,
                    result);
            }
        }

        /// <summary>检查默认焦点是否属于面板且可交互。</summary>
        private static void ValidatePanelFocus(GameObject root, UIPanelValidationResult result)
        {
            UIPanel panel = root.GetComponent<UIPanel>();
            if (panel == default || !panel.AutoFocusOnShow) return;
            Selectable selectable = panel.GetDefaultSelectable();
            if (selectable == default)
            {
                AddIssue(
                    result,
                    UIPanelValidationSeverity.Info,
                    UIPanelValidationCategory.Focus,
                    "面板启用了自动聚焦但没有默认 Selectable，将回退到首个可交互控件。",
                    panel,
                    fixSuggestion: "设置默认 Selectable，或确认首个可交互控件顺序。");
                return;
            }

            if (!selectable.transform.IsChildOf(root.transform) && selectable.transform != root.transform)
            {
                AddIssue(
                    result,
                    UIPanelValidationSeverity.Error,
                    UIPanelValidationCategory.Focus,
                    "默认 Selectable 不属于当前面板层级。",
                    selectable,
                    fixSuggestion: "将默认 Selectable 移入面板 Prefab，或清空该引用。");
                return;
            }

            if (!selectable.interactable || !selectable.gameObject.activeSelf)
            {
                AddIssue(
                    result,
                    UIPanelValidationSeverity.Warning,
                    UIPanelValidationCategory.Focus,
                    "默认 Selectable 当前不可交互或未激活。",
                    selectable,
                    fixSuggestion: "确认控件在显示时会被激活并允许交互。");
            }
        }

        /// <summary>计算目标 Canvas 相对于面板根的嵌套深度。</summary>
        private static int GetCanvasDepth(Transform target, Transform root)
        {
            int depth = 0;
            Transform current = target.parent;
            while (current != default && current != root)
            {
                if (current.GetComponent<Canvas>() != default) depth++;
                current = current.parent;
            }

            return depth + 1;
        }
    }
}
#endif
