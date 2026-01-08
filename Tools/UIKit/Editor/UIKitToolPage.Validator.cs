using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using YokiFrame.EditorTools;

namespace YokiFrame
{
    /// <summary>
    /// UIKitToolPage - 验证器功能
    /// </summary>
    public partial class UIKitToolPage
    {
        #region 字段 - 验证器

        private GameObject mValidatorTargetPanel;
        private UIPanelValidator.ValidationResult mValidatorCurrentResult;
        private readonly List<UIPanelValidator.ValidationResult> mValidatorSceneResults = new(16);
        private bool mValidatorShowErrors = true;
        private bool mValidatorShowWarnings = true;
        private bool mValidatorShowInfo = true;
        private UIPanelValidator.IssueCategory? mValidatorCategoryFilter;
        private bool mValidatorAutoValidate = true;
        private VisualElement mValidatorContent;

        #endregion

        #region 验证器 UI

        private void BuildValidatorUI(VisualElement container)
        {
            // 工具栏
            var toolbar = new VisualElement();
            toolbar.style.flexDirection = FlexDirection.Row;
            toolbar.style.paddingLeft = 8;
            toolbar.style.paddingRight = 8;
            toolbar.style.paddingTop = 4;
            toolbar.style.paddingBottom = 4;
            toolbar.style.backgroundColor = new StyleColor(new Color(0.15f, 0.15f, 0.15f));
            toolbar.style.borderBottomWidth = 1;
            toolbar.style.borderBottomColor = new StyleColor(new Color(0.2f, 0.2f, 0.2f));
            container.Add(toolbar);

            var validateSelectedBtn = new Button(() => SetValidatorTarget(Selection.activeGameObject)) { text = "验证选中" };
            validateSelectedBtn.style.height = 24;
            toolbar.Add(validateSelectedBtn);

            var validateSceneBtn = new Button(ValidateScene) { text = "验证场景" };
            validateSceneBtn.style.height = 24;
            validateSceneBtn.style.marginLeft = 4;
            toolbar.Add(validateSceneBtn);

            toolbar.Add(new VisualElement { style = { flexGrow = 1 } });

            var autoValidateToggle = YokiFrameUIComponents.CreateModernToggle("自动验证", mValidatorAutoValidate, v => mValidatorAutoValidate = v);
            toolbar.Add(autoValidateToggle);

            var clearBtn = new Button(() =>
            {
                mValidatorCurrentResult = null;
                mValidatorSceneResults.Clear();
                mValidatorTargetPanel = null;
                RefreshValidatorContent();
            }) { text = "清除" };
            clearBtn.style.height = 24;
            clearBtn.style.marginLeft = 8;
            toolbar.Add(clearBtn);

            // 目标选择栏
            var targetBar = new VisualElement();
            targetBar.style.flexDirection = FlexDirection.Row;
            targetBar.style.paddingLeft = 8;
            targetBar.style.paddingRight = 8;
            targetBar.style.paddingTop = 4;
            targetBar.style.paddingBottom = 4;
            targetBar.style.backgroundColor = new StyleColor(new Color(0.13f, 0.13f, 0.13f));
            container.Add(targetBar);

            targetBar.Add(new Label("验证目标:") { style = { unityTextAlign = TextAnchor.MiddleLeft, marginRight = 4 } });

            var targetField = new ObjectField();
            targetField.objectType = typeof(GameObject);
            targetField.value = mValidatorTargetPanel;
            targetField.style.width = 200;
            targetField.RegisterValueChangedCallback(evt => SetValidatorTarget(evt.newValue as GameObject));
            targetBar.Add(targetField);

            // 过滤栏
            var filterBar = new VisualElement();
            filterBar.style.flexDirection = FlexDirection.Row;
            filterBar.style.paddingLeft = 8;
            filterBar.style.paddingRight = 8;
            filterBar.style.paddingTop = 4;
            filterBar.style.paddingBottom = 4;
            filterBar.style.backgroundColor = new StyleColor(new Color(0.13f, 0.13f, 0.13f));
            filterBar.style.borderBottomWidth = 1;
            filterBar.style.borderBottomColor = new StyleColor(new Color(0.2f, 0.2f, 0.2f));
            container.Add(filterBar);

            // 严重程度过滤
            filterBar.Add(CreateValidatorFilterButton("错误", mValidatorShowErrors, new Color(1f, 0.5f, 0.5f), v => { mValidatorShowErrors = v; RefreshValidatorContent(); }));
            filterBar.Add(CreateValidatorFilterButton("警告", mValidatorShowWarnings, new Color(1f, 0.8f, 0.3f), v => { mValidatorShowWarnings = v; RefreshValidatorContent(); }));
            filterBar.Add(CreateValidatorFilterButton("信息", mValidatorShowInfo, new Color(0.5f, 0.8f, 1f), v => { mValidatorShowInfo = v; RefreshValidatorContent(); }));

            filterBar.Add(new Label("类别:") { style = { unityTextAlign = TextAnchor.MiddleLeft, marginLeft = 12, marginRight = 4 } });

            var categoryDropdown = new DropdownField();
            categoryDropdown.choices = new List<string> { "全部", "绑定", "引用", "Canvas", "动画", "焦点", "其他" };
            categoryDropdown.index = mValidatorCategoryFilter.HasValue ? (int)mValidatorCategoryFilter.Value + 1 : 0;
            categoryDropdown.style.width = 80;
            categoryDropdown.RegisterValueChangedCallback(evt =>
            {
                var idx = categoryDropdown.choices.IndexOf(evt.newValue);
                mValidatorCategoryFilter = idx == 0 ? null : (UIPanelValidator.IssueCategory?)(idx - 1);
                RefreshValidatorContent();
            });
            filterBar.Add(categoryDropdown);

            // 内容区域
            mValidatorContent = new ScrollView();
            mValidatorContent.style.flexGrow = 1;
            mValidatorContent.style.paddingLeft = 12;
            mValidatorContent.style.paddingRight = 12;
            mValidatorContent.style.paddingTop = 12;
            container.Add(mValidatorContent);

            RefreshValidatorContent();
        }

        private Button CreateValidatorFilterButton(string text, bool initialValue, Color activeColor, Action<bool> onChanged)
        {
            var btn = new Button();
            btn.text = text;
            btn.style.height = 24;
            btn.style.marginRight = 4;

            void UpdateStyle(bool isActive)
            {
                btn.style.backgroundColor = new StyleColor(isActive ? activeColor : new Color(0.2f, 0.2f, 0.2f));
                btn.style.color = new StyleColor(isActive ? Color.white : new Color(0.7f, 0.7f, 0.7f));
            }

            UpdateStyle(initialValue);
            bool currentValue = initialValue;

            btn.clicked += () =>
            {
                currentValue = !currentValue;
                UpdateStyle(currentValue);
                onChanged?.Invoke(currentValue);
            };

            return btn;
        }

        #endregion

        #region 验证器逻辑

        private void SetValidatorTarget(GameObject target)
        {
            mValidatorTargetPanel = target;
            if (target != null)
            {
                mValidatorCurrentResult = UIPanelValidator.ValidatePanel(target);
            }
            else
            {
                mValidatorCurrentResult = null;
            }
            RefreshValidatorContent();
        }

        private void ValidateScene()
        {
            mValidatorCurrentResult = null;
            mValidatorTargetPanel = null;
            mValidatorSceneResults.Clear();

            var results = UIPanelValidator.ValidateAllPanelsInScene();
            mValidatorSceneResults.AddRange(results);

            RefreshValidatorContent();
        }

        private void RefreshValidatorContent()
        {
            if (mValidatorContent == null) return;
            mValidatorContent.Clear();

            if (mValidatorCurrentResult != null)
            {
                DrawValidationResult(mValidatorCurrentResult);
            }
            else if (mValidatorSceneResults.Count > 0)
            {
                DrawSceneResults();
            }
            else
            {
                var helpBox = YokiFrameUIComponents.CreateHelpBox("选择一个 UIPanel 进行验证，或点击\"验证场景\"检查所有面板");
                mValidatorContent.Add(helpBox);
            }
        }

        private void DrawValidationResult(UIPanelValidator.ValidationResult result)
        {
            DrawResultSummary(result);

            if (result.Issues.Count == 0)
            {
                var successBox = YokiFrameUIComponents.CreateHelpBox("未发现问题 ✓");
                mValidatorContent.Add(successBox);
                return;
            }

            for (int i = 0; i < result.Issues.Count; i++)
            {
                var issue = result.Issues[i];
                if (PassValidatorFilter(issue))
                {
                    DrawIssueItem(issue);
                }
            }
        }

        private void DrawResultSummary(UIPanelValidator.ValidationResult result)
        {
            var errorCount = result.GetErrorCount();
            var warningCount = result.GetWarningCount();
            var infoCount = result.Issues.Count - errorCount - warningCount;

            var summaryRow = new VisualElement();
            summaryRow.style.flexDirection = FlexDirection.Row;
            summaryRow.style.alignItems = Align.Center;
            summaryRow.style.paddingTop = 8;
            summaryRow.style.paddingBottom = 8;
            summaryRow.style.paddingLeft = 12;
            summaryRow.style.paddingRight = 12;
            summaryRow.style.backgroundColor = new StyleColor(new Color(0.15f, 0.15f, 0.15f));
            summaryRow.style.borderTopLeftRadius = summaryRow.style.borderTopRightRadius = 4;
            summaryRow.style.borderBottomLeftRadius = summaryRow.style.borderBottomRightRadius = 4;
            summaryRow.style.marginBottom = 12;

            var targetName = result.Target != null ? result.Target.name : "未知";
            summaryRow.Add(new Label($"目标: {targetName}") { style = { unityFontStyleAndWeight = FontStyle.Bold, flexGrow = 1 } });

            if (errorCount > 0)
            {
                summaryRow.Add(new Label($"✗ {errorCount}") { style = { marginRight = 12, color = new StyleColor(new Color(1f, 0.5f, 0.5f)) } });
            }

            if (warningCount > 0)
            {
                summaryRow.Add(new Label($"⚠ {warningCount}") { style = { marginRight = 12, color = new StyleColor(new Color(1f, 0.8f, 0.3f)) } });
            }

            if (infoCount > 0)
            {
                summaryRow.Add(new Label($"ℹ {infoCount}") { style = { marginRight = 12, color = new StyleColor(new Color(0.5f, 0.8f, 1f)) } });
            }

            if (errorCount == 0 && warningCount == 0 && infoCount == 0)
            {
                summaryRow.Add(new Label("✓ 通过") { style = { color = new StyleColor(new Color(0.5f, 1f, 0.5f)) } });
            }

            mValidatorContent.Add(summaryRow);
        }

        private void DrawIssueItem(UIPanelValidator.ValidationIssue issue)
        {
            var bgColor = GetIssueSeverityBgColor(issue.Severity);

            var card = new VisualElement();
            card.style.backgroundColor = new StyleColor(bgColor);
            card.style.paddingTop = 8;
            card.style.paddingBottom = 8;
            card.style.paddingLeft = 12;
            card.style.paddingRight = 12;
            card.style.marginBottom = 8;
            card.style.borderTopLeftRadius = card.style.borderTopRightRadius = 4;
            card.style.borderBottomLeftRadius = card.style.borderBottomRightRadius = 4;

            // 第一行：图标 + 类别 + 消息
            var headerRow = new VisualElement();
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.alignItems = Align.FlexStart;

            var icon = GetIssueSeverityIcon(issue.Severity);
            var iconColor = GetIssueSeverityTextColor(issue.Severity);
            headerRow.Add(new Label(icon) { style = { width = 20, color = new StyleColor(iconColor) } });

            var categoryLabel = GetIssueCategoryLabel(issue.Category);
            headerRow.Add(new Label($"[{categoryLabel}]") { style = { width = 50, fontSize = 11, color = new StyleColor(new Color(0.6f, 0.6f, 0.6f)) } });

            headerRow.Add(new Label(issue.Message) { style = { flexGrow = 1, whiteSpace = WhiteSpace.Normal } });

            card.Add(headerRow);

            // 修复建议
            if (!string.IsNullOrEmpty(issue.FixSuggestion))
            {
                var suggestionRow = new VisualElement();
                suggestionRow.style.paddingLeft = 20;
                suggestionRow.style.marginTop = 4;
                suggestionRow.Add(new Label($"→ {issue.FixSuggestion}") { style = { fontSize = 11, color = new StyleColor(new Color(0.6f, 0.6f, 0.6f)) } });
                card.Add(suggestionRow);
            }

            // 定位按钮
            if (issue.Context != null)
            {
                var btnRow = new VisualElement();
                btnRow.style.flexDirection = FlexDirection.Row;
                btnRow.style.justifyContent = Justify.FlexEnd;
                btnRow.style.marginTop = 4;

                btnRow.Add(CreateSmallButton("定位", () =>
                {
                    if (issue.Context is Component comp)
                    {
                        Selection.activeGameObject = comp.gameObject;
                        EditorGUIUtility.PingObject(comp.gameObject);
                    }
                    else if (issue.Context is GameObject go)
                    {
                        Selection.activeGameObject = go;
                        EditorGUIUtility.PingObject(go);
                    }
                    else
                    {
                        EditorGUIUtility.PingObject(issue.Context);
                    }
                }));

                card.Add(btnRow);
            }

            mValidatorContent.Add(card);
        }

        private void DrawSceneResults()
        {
            var titleLabel = new Label($"场景验证结果 ({mValidatorSceneResults.Count} 个面板有问题)");
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.marginBottom = 12;
            mValidatorContent.Add(titleLabel);

            for (int i = 0; i < mValidatorSceneResults.Count; i++)
            {
                var result = mValidatorSceneResults[i];

                var card = new VisualElement();
                card.style.backgroundColor = new StyleColor(new Color(0.15f, 0.15f, 0.15f));
                card.style.paddingTop = 8;
                card.style.paddingBottom = 8;
                card.style.paddingLeft = 12;
                card.style.paddingRight = 12;
                card.style.marginBottom = 8;
                card.style.borderTopLeftRadius = card.style.borderTopRightRadius = 4;
                card.style.borderBottomLeftRadius = card.style.borderBottomRightRadius = 4;

                var headerRow = new VisualElement();
                headerRow.style.flexDirection = FlexDirection.Row;
                headerRow.style.alignItems = Align.Center;

                var targetName = result.Target != null ? result.Target.name : "未知";
                headerRow.Add(new Label(targetName) { style = { unityFontStyleAndWeight = FontStyle.Bold, flexGrow = 1 } });

                headerRow.Add(CreateSmallButton("详情", () => SetValidatorTarget(result.Target)));

                if (result.Target != null)
                {
                    headerRow.Add(CreateSmallButton("选择", () => Selection.activeGameObject = result.Target));
                }

                card.Add(headerRow);

                var errorCount = result.GetErrorCount();
                var warningCount = result.GetWarningCount();
                card.Add(new Label($"  错误: {errorCount}, 警告: {warningCount}") { style = { fontSize = 11, color = new StyleColor(new Color(0.6f, 0.6f, 0.6f)), marginTop = 4 } });

                mValidatorContent.Add(card);
            }
        }

        #endregion

        #region 验证器辅助方法

        private bool PassValidatorFilter(UIPanelValidator.ValidationIssue issue)
        {
            switch (issue.Severity)
            {
                case UIPanelValidator.IssueSeverity.Error when !mValidatorShowErrors:
                case UIPanelValidator.IssueSeverity.Warning when !mValidatorShowWarnings:
                case UIPanelValidator.IssueSeverity.Info when !mValidatorShowInfo:
                    return false;
            }

            if (mValidatorCategoryFilter.HasValue && issue.Category != mValidatorCategoryFilter.Value)
            {
                return false;
            }

            return true;
        }

        private static Color GetIssueSeverityBgColor(UIPanelValidator.IssueSeverity severity) => severity switch
        {
            UIPanelValidator.IssueSeverity.Error => new Color(0.25f, 0.15f, 0.15f),
            UIPanelValidator.IssueSeverity.Warning => new Color(0.25f, 0.22f, 0.12f),
            UIPanelValidator.IssueSeverity.Info => new Color(0.15f, 0.18f, 0.22f),
            _ => new Color(0.15f, 0.15f, 0.15f)
        };

        private static Color GetIssueSeverityTextColor(UIPanelValidator.IssueSeverity severity) => severity switch
        {
            UIPanelValidator.IssueSeverity.Error => new Color(1f, 0.5f, 0.5f),
            UIPanelValidator.IssueSeverity.Warning => new Color(1f, 0.8f, 0.3f),
            UIPanelValidator.IssueSeverity.Info => new Color(0.5f, 0.8f, 1f),
            _ => Color.white
        };

        private static string GetIssueSeverityIcon(UIPanelValidator.IssueSeverity severity) => severity switch
        {
            UIPanelValidator.IssueSeverity.Error => "✗",
            UIPanelValidator.IssueSeverity.Warning => "⚠",
            UIPanelValidator.IssueSeverity.Info => "ℹ",
            _ => "?"
        };

        private static string GetIssueCategoryLabel(UIPanelValidator.IssueCategory category) => category switch
        {
            UIPanelValidator.IssueCategory.Binding => "绑定",
            UIPanelValidator.IssueCategory.Reference => "引用",
            UIPanelValidator.IssueCategory.Canvas => "Canvas",
            UIPanelValidator.IssueCategory.Animation => "动画",
            UIPanelValidator.IssueCategory.Focus => "焦点",
            UIPanelValidator.IssueCategory.Other => "其他",
            _ => "未知"
        };

        #endregion
    }
}
