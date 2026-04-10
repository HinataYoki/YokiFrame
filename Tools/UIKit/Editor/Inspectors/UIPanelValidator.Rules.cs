#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace YokiFrame
{
    /// <summary>
    /// UIPanelValidator 的具体规则实现。
    /// </summary>
    public static partial class UIPanelValidator
    {
        #region 绑定验证

        private static void ValidateBindings(GameObject root, ValidationResult result)
        {
            var binds = root.GetComponentsInChildren<AbstractBind>(true);
            var nameSet = new HashSet<string>();

            for (int i = 0; i < binds.Length; i++)
            {
                var bind = binds[i];

                if (bind.Bind == BindType.Leaf) continue;

                if (string.IsNullOrEmpty(bind.Name))
                {
                    result.Issues.Add(new ValidationIssue
                    {
                        Severity = IssueSeverity.Error,
                        Category = IssueCategory.Binding,
                        Message = "绑定字段名称为空。",
                        Context = bind,
                        FixSuggestion = "设置绑定的字段名称。"
                    });
                    continue;
                }

                if (!nameSet.Add(bind.Name))
                {
                    result.Issues.Add(new ValidationIssue
                    {
                        Severity = IssueSeverity.Error,
                        Category = IssueCategory.Binding,
                        Message = $"绑定字段名称重复: {bind.Name}",
                        Context = bind,
                        FixSuggestion = "修改为唯一的字段名称。"
                    });
                }

                if (string.IsNullOrEmpty(bind.Type))
                {
                    result.Issues.Add(new ValidationIssue
                    {
                        Severity = IssueSeverity.Error,
                        Category = IssueCategory.Binding,
                        Message = $"绑定类型未设置: {bind.Name}",
                        Context = bind,
                        FixSuggestion = "选择绑定的组件类型。"
                    });
                    continue;
                }

                if (bind.Bind == BindType.Member)
                {
                    ValidateMemberBinding(bind, result);
                }
            }
        }

        private static void ValidateMemberBinding(AbstractBind bind, ValidationResult result)
        {
            var components = bind.GetComponents<Component>();
            var found = false;

            for (int i = 0; i < components.Length; i++)
            {
                var comp = components[i];
                if (comp != null && comp.GetType().FullName == bind.Type)
                {
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                result.Issues.Add(new ValidationIssue
                {
                    Severity = IssueSeverity.Error,
                    Category = IssueCategory.Binding,
                    Message = $"绑定的组件不存在: {bind.Name} -> {GetShortTypeName(bind.Type)}",
                    Context = bind,
                    FixSuggestion = "添加对应组件或修改绑定类型。"
                });
            }
        }

        #endregion

        #region 引用验证

        private static void ValidateReferences(GameObject root, ValidationResult result)
        {
            var images = root.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                var image = images[i];
                if (image.sprite == null && image.color.a > 0)
                {
                    var raycastTarget = image.raycastTarget;
                    if (!raycastTarget)
                    {
                        result.Issues.Add(new ValidationIssue
                        {
                            Severity = IssueSeverity.Warning,
                            Category = IssueCategory.Reference,
                            Message = $"Image 缺少 Sprite: {GetPath(image.transform, root.transform)}",
                            Context = image,
                            FixSuggestion = "设置 Sprite 或将 Alpha 设为 0。"
                        });
                    }
                }
            }

            var buttons = root.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                var button = buttons[i];
                if (button.onClick.GetPersistentEventCount() == 0)
                {
                    var hasBind = button.GetComponent<AbstractBind>() != null;
                    if (!hasBind)
                    {
                        result.Issues.Add(new ValidationIssue
                        {
                            Severity = IssueSeverity.Info,
                            Category = IssueCategory.Reference,
                            Message = $"Button 没有绑定点击事件: {GetPath(button.transform, root.transform)}",
                            Context = button,
                            FixSuggestion = "添加 OnClick 事件或通过代码绑定。"
                        });
                    }
                }
            }

            ValidateTextReferences(root, result);
        }

        private static void ValidateTextReferences(GameObject root, ValidationResult result)
        {
            var texts = root.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                var text = texts[i];
                if (text.font == null)
                {
                    result.Issues.Add(new ValidationIssue
                    {
                        Severity = IssueSeverity.Error,
                        Category = IssueCategory.Reference,
                        Message = $"Text 缺少字体: {GetPath(text.transform, root.transform)}",
                        Context = text,
                        FixSuggestion = "设置字体引用。"
                    });
                }
            }

            var tmpType = System.Type.GetType("TMPro.TMP_Text, Unity.TextMeshPro");
            if (tmpType != null)
            {
                var tmpTexts = root.GetComponentsInChildren(tmpType, true);
                for (int i = 0; i < tmpTexts.Length; i++)
                {
                    var tmp = tmpTexts[i];
                    var fontProp = tmpType.GetProperty("font");
                    if (fontProp != null)
                    {
                        var font = fontProp.GetValue(tmp);
                        if (font == null)
                        {
                            result.Issues.Add(new ValidationIssue
                            {
                                Severity = IssueSeverity.Error,
                                Category = IssueCategory.Reference,
                                Message = $"TMP_Text 缺少字体: {GetPath((tmp as Component).transform, root.transform)}",
                                Context = tmp as Object,
                                FixSuggestion = "设置 TMP 字体引用。"
                            });
                        }
                    }
                }
            }
        }

        #endregion

        #region Canvas 配置验证

        private static void ValidateCanvasConfiguration(GameObject root, ValidationResult result)
        {
            var canvases = root.GetComponentsInChildren<Canvas>(true);
            for (int i = 0; i < canvases.Length; i++)
            {
                var canvas = canvases[i];
                var depth = GetCanvasNestingDepth(canvas.transform, root.transform);

                if (depth > 3)
                {
                    result.Issues.Add(new ValidationIssue
                    {
                        Severity = IssueSeverity.Warning,
                        Category = IssueCategory.Canvas,
                        Message = $"Canvas 嵌套过深 ({depth} 层): {GetPath(canvas.transform, root.transform)}",
                        Context = canvas,
                        FixSuggestion = "减少 Canvas 嵌套层级以优化性能。"
                    });
                }
            }

            ValidateRaycastTargets(root, result);
        }

        private static void ValidateRaycastTargets(GameObject root, ValidationResult result)
        {
            var graphics = root.GetComponentsInChildren<Graphic>(true);
            var unnecessaryRaycastCount = 0;

            for (int i = 0; i < graphics.Length; i++)
            {
                var graphic = graphics[i];

                var selectable = graphic.GetComponent<Selectable>();
                var hasClickHandler = graphic.GetComponent<Button>() != null ||
                                     graphic.GetComponent<Toggle>() != null ||
                                     graphic.GetComponent<Slider>() != null ||
                                     graphic.GetComponent<Scrollbar>() != null ||
                                     graphic.GetComponent<InputField>() != null;

                if (graphic.raycastTarget && selectable == null && !hasClickHandler)
                {
                    var scrollRect = graphic.GetComponentInParent<ScrollRect>();
                    if (scrollRect == null)
                    {
                        unnecessaryRaycastCount++;
                    }
                }
            }

            if (unnecessaryRaycastCount > 5)
            {
                result.Issues.Add(new ValidationIssue
                {
                    Severity = IssueSeverity.Info,
                    Category = IssueCategory.Canvas,
                    Message = $"发现 {unnecessaryRaycastCount} 个非交互元素开启了 Raycast Target",
                    Context = root,
                    FixSuggestion = "关闭非交互元素的 Raycast Target 以优化性能。"
                });
            }
        }

        private static int GetCanvasNestingDepth(Transform canvas, Transform root)
        {
            var depth = 0;
            var current = canvas.parent;

            while (current != null && current != root)
            {
                if (current.GetComponent<Canvas>() != null)
                {
                    depth++;
                }
                current = current.parent;
            }

            return depth;
        }

        #endregion

        #region 动画配置验证

        private static void ValidateAnimationConfiguration(GameObject root, ValidationResult result)
        {
            var panel = root.GetComponent<UIPanel>();
            if (panel == default) return;

            var showAnimConfigField = panel.GetType().GetField("mShowAnimationConfig",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var hideAnimConfigField = panel.GetType().GetField("mHideAnimationConfig",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (showAnimConfigField == default || hideAnimConfigField == default) return;

            var showAnimConfig = showAnimConfigField.GetValue(panel) as UIAnimationConfig;
            var hideAnimConfig = hideAnimConfigField.GetValue(panel) as UIAnimationConfig;

            if ((showAnimConfig != default && hideAnimConfig == default) ||
                (showAnimConfig == default && hideAnimConfig != default))
            {
                result.Issues.Add(new ValidationIssue
                {
                    Severity = IssueSeverity.Info,
                    Category = IssueCategory.Animation,
                    Message = "面板只配置了单向动画。",
                    Context = panel,
                    FixSuggestion = "建议同时配置显示和隐藏动画以保持一致性。"
                });
            }

            bool usesFadeAnimation = showAnimConfig is FadeAnimationConfig || hideAnimConfig is FadeAnimationConfig;
            if (usesFadeAnimation)
            {
                var canvasGroup = root.GetComponent<CanvasGroup>();
                if (canvasGroup == default)
                {
                    result.Issues.Add(new ValidationIssue
                    {
                        Severity = IssueSeverity.Warning,
                        Category = IssueCategory.Animation,
                        Message = "使用 Fade 动画但缺少 CanvasGroup 组件。",
                        Context = panel,
                        FixSuggestion = "在面板根节点添加 CanvasGroup，避免首次播放动画时卡顿。"
                    });
                }
            }
        }

        #endregion

        #region 焦点配置验证

        private static void ValidateFocusConfiguration(GameObject root, ValidationResult result)
        {
            var panel = root.GetComponent<UIPanel>();
            if (panel == null) return;

            var selectables = root.GetComponentsInChildren<Selectable>(true);
            if (selectables.Length == 0)
            {
                return;
            }

            var groups = root.GetComponentsInChildren<SelectableGroup>(true);

            var hasExplicitNavigation = false;
            for (int i = 0; i < selectables.Length; i++)
            {
                var selectable = selectables[i];
                if (selectable.navigation.mode == Navigation.Mode.Explicit)
                {
                    hasExplicitNavigation = true;

                    var nav = selectable.navigation;
                    if (nav.selectOnUp == null && nav.selectOnDown == null &&
                        nav.selectOnLeft == null && nav.selectOnRight == null)
                    {
                        result.Issues.Add(new ValidationIssue
                        {
                            Severity = IssueSeverity.Warning,
                            Category = IssueCategory.Focus,
                            Message = $"显式导航未配置任何方向: {GetPath(selectable.transform, root.transform)}",
                            Context = selectable,
                            FixSuggestion = "至少配置一个导航方向，或改用自动导航。"
                        });
                    }
                }
            }

            if (selectables.Length > 1 && !hasExplicitNavigation && groups.Length == 0)
            {
                result.Issues.Add(new ValidationIssue
                {
                    Severity = IssueSeverity.Info,
                    Category = IssueCategory.Focus,
                    Message = $"面板有 {selectables.Length} 个可选中元素，但未配置导航。",
                    Context = panel,
                    FixSuggestion = "考虑添加 SelectableGroup 或显式导航，以支持手柄和键盘操作。"
                });
            }
        }

        #endregion
    }
}
#endif
