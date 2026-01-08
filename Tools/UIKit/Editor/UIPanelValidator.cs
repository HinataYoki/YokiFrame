using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace YokiFrame
{
    /// <summary>
    /// UI 面板配置验证器 - 检测缺失绑定、断开引用、Canvas 配置问题
    /// </summary>
    public static class UIPanelValidator
    {
        #region 数据结构

        /// <summary>
        /// 验证结果
        /// </summary>
        public class ValidationResult
        {
            public GameObject Target;
            public readonly List<ValidationIssue> Issues = new(8);
            public bool HasErrors => GetErrorCount() > 0;
            public bool HasWarnings => GetWarningCount() > 0;
            
            public int GetErrorCount()
            {
                var count = 0;
                for (int i = 0; i < Issues.Count; i++)
                {
                    if (Issues[i].Severity == IssueSeverity.Error) count++;
                }
                return count;
            }
            
            public int GetWarningCount()
            {
                var count = 0;
                for (int i = 0; i < Issues.Count; i++)
                {
                    if (Issues[i].Severity == IssueSeverity.Warning) count++;
                }
                return count;
            }
        }

        /// <summary>
        /// 验证问题
        /// </summary>
        public class ValidationIssue
        {
            public IssueSeverity Severity;
            public IssueCategory Category;
            public string Message;
            public Object Context;
            public string FixSuggestion;
        }

        /// <summary>
        /// 问题严重程度
        /// </summary>
        public enum IssueSeverity
        {
            Info,
            Warning,
            Error
        }

        /// <summary>
        /// 问题类别
        /// </summary>
        public enum IssueCategory
        {
            Binding,
            Reference,
            Canvas,
            Animation,
            Focus,
            Other
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 验证面板配置
        /// </summary>
        public static ValidationResult ValidatePanel(GameObject panelRoot)
        {
            var result = new ValidationResult { Target = panelRoot };
            
            if (panelRoot == null)
            {
                result.Issues.Add(new ValidationIssue
                {
                    Severity = IssueSeverity.Error,
                    Category = IssueCategory.Other,
                    Message = "面板根对象为空"
                });
                return result;
            }
            
            // 执行各项验证
            ValidateBindings(panelRoot, result);
            ValidateReferences(panelRoot, result);
            ValidateCanvasConfiguration(panelRoot, result);
            ValidateAnimationConfiguration(panelRoot, result);
            ValidateFocusConfiguration(panelRoot, result);
            
            return result;
        }

        /// <summary>
        /// 验证场景中所有面板
        /// </summary>
        public static List<ValidationResult> ValidateAllPanelsInScene()
        {
            var results = new List<ValidationResult>();
            var panels = Object.FindObjectsByType<UIPanel>(FindObjectsSortMode.None);
            
            for (int i = 0; i < panels.Length; i++)
            {
                var result = ValidatePanel(panels[i].gameObject);
                if (result.Issues.Count > 0)
                {
                    results.Add(result);
                }
            }
            
            return results;
        }

        /// <summary>
        /// 验证预制体
        /// </summary>
        public static ValidationResult ValidatePrefab(string prefabPath)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                var result = new ValidationResult();
                result.Issues.Add(new ValidationIssue
                {
                    Severity = IssueSeverity.Error,
                    Category = IssueCategory.Other,
                    Message = $"无法加载预制体: {prefabPath}"
                });
                return result;
            }
            
            return ValidatePanel(prefab);
        }

        #endregion

        #region 绑定验证

        private static void ValidateBindings(GameObject root, ValidationResult result)
        {
            var binds = root.GetComponentsInChildren<AbstractBind>(true);
            var nameSet = new HashSet<string>();
            
            for (int i = 0; i < binds.Length; i++)
            {
                var bind = binds[i];
                
                // 跳过 Leaf 类型
                if (bind.Bind == BindType.Leaf) continue;
                
                // 检查字段名称为空
                if (string.IsNullOrEmpty(bind.Name))
                {
                    result.Issues.Add(new ValidationIssue
                    {
                        Severity = IssueSeverity.Error,
                        Category = IssueCategory.Binding,
                        Message = $"绑定字段名称为空",
                        Context = bind,
                        FixSuggestion = "设置绑定的字段名称"
                    });
                    continue;
                }
                
                // 检查重复名称
                if (!nameSet.Add(bind.Name))
                {
                    result.Issues.Add(new ValidationIssue
                    {
                        Severity = IssueSeverity.Error,
                        Category = IssueCategory.Binding,
                        Message = $"绑定字段名称重复: {bind.Name}",
                        Context = bind,
                        FixSuggestion = "修改为唯一的字段名称"
                    });
                }
                
                // 检查类型为空
                if (string.IsNullOrEmpty(bind.Type))
                {
                    result.Issues.Add(new ValidationIssue
                    {
                        Severity = IssueSeverity.Error,
                        Category = IssueCategory.Binding,
                        Message = $"绑定类型未设置: {bind.Name}",
                        Context = bind,
                        FixSuggestion = "选择绑定的组件类型"
                    });
                    continue;
                }
                
                // 检查 Member 类型的组件是否存在
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
                    FixSuggestion = "添加对应的组件或修改绑定类型"
                });
            }
        }

        #endregion

        #region 引用验证

        private static void ValidateReferences(GameObject root, ValidationResult result)
        {
            // 检查 Image 组件的 Sprite 引用
            var images = root.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                var image = images[i];
                if (image.sprite == null && image.color.a > 0)
                {
                    // 只有当 Image 可见时才警告
                    var raycastTarget = image.raycastTarget;
                    if (!raycastTarget) // 如果不是 raycast target，可能是装饰性的
                    {
                        result.Issues.Add(new ValidationIssue
                        {
                            Severity = IssueSeverity.Warning,
                            Category = IssueCategory.Reference,
                            Message = $"Image 组件缺少 Sprite: {GetPath(image.transform, root.transform)}",
                            Context = image,
                            FixSuggestion = "设置 Sprite 或将 Alpha 设为 0"
                        });
                    }
                }
            }
            
            // 检查 Button 组件的 OnClick 引用
            var buttons = root.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                var button = buttons[i];
                if (button.onClick.GetPersistentEventCount() == 0)
                {
                    // 检查是否有 Bind 组件（可能通过代码绑定）
                    var hasBind = button.GetComponent<AbstractBind>() != null;
                    if (!hasBind)
                    {
                        result.Issues.Add(new ValidationIssue
                        {
                            Severity = IssueSeverity.Info,
                            Category = IssueCategory.Reference,
                            Message = $"Button 没有绑定点击事件: {GetPath(button.transform, root.transform)}",
                            Context = button,
                            FixSuggestion = "添加 OnClick 事件或通过代码绑定"
                        });
                    }
                }
            }
            
            // 检查 Text/TMP 组件
            ValidateTextReferences(root, result);
        }

        private static void ValidateTextReferences(GameObject root, ValidationResult result)
        {
            // 检查 Unity Text
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
                        Message = $"Text 组件缺少字体: {GetPath(text.transform, root.transform)}",
                        Context = text,
                        FixSuggestion = "设置字体引用"
                    });
                }
            }
            
            // 检查 TMP Text（如果存在）
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
                                Message = $"TMP_Text 组件缺少字体: {GetPath((tmp as Component).transform, root.transform)}",
                                Context = tmp as Object,
                                FixSuggestion = "设置 TMP 字体引用"
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
            // 检查动态元素是否在静态 Canvas 下
            var dynamicElements = root.GetComponentsInChildren<UIDynamicElement>(true);
            for (int i = 0; i < dynamicElements.Length; i++)
            {
                var element = dynamicElements[i];
                var parentCanvas = element.GetComponentInParent<Canvas>();
                
                if (parentCanvas != null)
                {
                    // 检查是否有 UIStaticElement 标记的父级
                    var staticParent = element.GetComponentInParent<UIStaticElement>();
                    if (staticParent != null)
                    {
                        result.Issues.Add(new ValidationIssue
                        {
                            Severity = IssueSeverity.Warning,
                            Category = IssueCategory.Canvas,
                            Message = $"动态元素位于静态区域下: {GetPath(element.transform, root.transform)}",
                            Context = element,
                            FixSuggestion = "将动态元素移到动态 Canvas 下，或移除 UIDynamicElement 标记"
                        });
                    }
                }
            }
            
            // 检查 Canvas 嵌套深度
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
                        FixSuggestion = "减少 Canvas 嵌套层级以优化性能"
                    });
                }
            }
            
            // 检查 Raycast Target 优化
            ValidateRaycastTargets(root, result);
        }

        private static void ValidateRaycastTargets(GameObject root, ValidationResult result)
        {
            var graphics = root.GetComponentsInChildren<Graphic>(true);
            var unnecessaryRaycastCount = 0;
            
            for (int i = 0; i < graphics.Length; i++)
            {
                var graphic = graphics[i];
                
                // 检查是否是交互元素
                var selectable = graphic.GetComponent<Selectable>();
                var hasClickHandler = graphic.GetComponent<Button>() != null ||
                                     graphic.GetComponent<Toggle>() != null ||
                                     graphic.GetComponent<Slider>() != null ||
                                     graphic.GetComponent<Scrollbar>() != null ||
                                     graphic.GetComponent<InputField>() != null;
                
                // 如果不是交互元素但开启了 raycastTarget
                if (graphic.raycastTarget && selectable == null && !hasClickHandler)
                {
                    // 检查是否是 ScrollRect 的内容
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
                    FixSuggestion = "关闭非交互元素的 Raycast Target 以优化性能"
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
            if (panel == null) return;
            
            // 检查动画配置
            var showAnimField = panel.GetType().GetField("mShowAnimation", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var hideAnimField = panel.GetType().GetField("mHideAnimation",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (showAnimField != null && hideAnimField != null)
            {
                var showAnim = showAnimField.GetValue(panel);
                var hideAnim = hideAnimField.GetValue(panel);
                
                // 如果只配置了一个动画，给出提示
                if ((showAnim != null && hideAnim == null) || (showAnim == null && hideAnim != null))
                {
                    result.Issues.Add(new ValidationIssue
                    {
                        Severity = IssueSeverity.Info,
                        Category = IssueCategory.Animation,
                        Message = "面板只配置了单向动画",
                        Context = panel,
                        FixSuggestion = "建议同时配置显示和隐藏动画以保持一致性"
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
            
            // 检查是否有可选中元素
            var selectables = root.GetComponentsInChildren<Selectable>(true);
            if (selectables.Length == 0)
            {
                return; // 没有可选中元素，不需要焦点配置
            }
            
            // 检查 SelectableGroup 配置
            var groups = root.GetComponentsInChildren<SelectableGroup>(true);
            
            // 检查导航配置
            var hasExplicitNavigation = false;
            for (int i = 0; i < selectables.Length; i++)
            {
                var selectable = selectables[i];
                if (selectable.navigation.mode == Navigation.Mode.Explicit)
                {
                    hasExplicitNavigation = true;
                    
                    // 检查显式导航是否完整
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
                            FixSuggestion = "配置至少一个导航方向或改用自动导航"
                        });
                    }
                }
            }
            
            // 如果有多个可选中元素但没有导航配置
            if (selectables.Length > 1 && !hasExplicitNavigation && groups.Length == 0)
            {
                result.Issues.Add(new ValidationIssue
                {
                    Severity = IssueSeverity.Info,
                    Category = IssueCategory.Focus,
                    Message = $"面板有 {selectables.Length} 个可选中元素但未配置导航",
                    Context = panel,
                    FixSuggestion = "考虑添加 SelectableGroup 或配置显式导航以支持手柄/键盘操作"
                });
            }
        }

        #endregion

        #region 辅助方法

        private static string GetPath(Transform target, Transform root)
        {
            if (target == null || root == null) return "";
            if (target == root) return target.name;
            
            var path = target.name;
            var current = target.parent;
            
            while (current != null && current != root)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }
            
            return path;
        }

        private static string GetShortTypeName(string fullName)
        {
            if (string.IsNullOrEmpty(fullName)) return "";
            
            var lastDot = fullName.LastIndexOf('.');
            return lastDot >= 0 ? fullName.Substring(lastDot + 1) : fullName;
        }

        #endregion
    }
}
