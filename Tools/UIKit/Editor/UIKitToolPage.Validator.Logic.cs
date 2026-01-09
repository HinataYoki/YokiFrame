#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using YokiFrame.EditorTools;
using static YokiFrame.EditorTools.YokiFrameUIComponents;

namespace YokiFrame
{
    /// <summary>
    /// UIKitToolPage - 验证器功能 - 验证逻辑
    /// </summary>
    public partial class UIKitToolPage
    {
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
                var helpBox = CreateHelpBox("选择一个 UIPanel 进行验证，或点击\"验证场景\"检查所有面板");
                mValidatorContent.Add(helpBox);
            }
        }

        private void DrawValidationResult(UIPanelValidator.ValidationResult result)
        {
            DrawResultSummary(result);

            if (result.Issues.Count == 0)
            {
                var successBox = CreateHelpBox("未发现问题", HelpBoxType.Success);
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
            summaryRow.style.paddingTop = Spacing.SM;
            summaryRow.style.paddingBottom = Spacing.SM;
            summaryRow.style.paddingLeft = Spacing.MD;
            summaryRow.style.paddingRight = Spacing.MD;
            summaryRow.style.backgroundColor = new StyleColor(Colors.LayerToolbar);
            summaryRow.style.borderTopLeftRadius = summaryRow.style.borderTopRightRadius = Radius.MD;
            summaryRow.style.borderBottomLeftRadius = summaryRow.style.borderBottomRightRadius = Radius.MD;
            summaryRow.style.marginBottom = Spacing.MD;

            var targetName = result.Target != null ? result.Target.name : "未知";
            summaryRow.Add(new Label($"目标: {targetName}") { style = { unityFontStyleAndWeight = FontStyle.Bold, flexGrow = 1 } });

            if (errorCount > 0)
            {
                var errorBadge = CreateBadge(errorCount.ToString(), Colors.BadgeError);
                errorBadge.style.marginRight = Spacing.MD;
                summaryRow.Add(errorBadge);
            }

            if (warningCount > 0)
            {
                var warningBadge = CreateBadge(warningCount.ToString(), Colors.BadgeWarning);
                warningBadge.style.marginRight = Spacing.MD;
                summaryRow.Add(warningBadge);
            }

            if (infoCount > 0)
            {
                var infoBadge = CreateBadge(infoCount.ToString(), Colors.BadgeInfo);
                infoBadge.style.marginRight = Spacing.MD;
                summaryRow.Add(infoBadge);
            }

            if (errorCount == 0 && warningCount == 0 && infoCount == 0)
            {
                var successBadge = CreateBadge("通过", Colors.BadgeSuccess);
                summaryRow.Add(successBadge);
            }

            mValidatorContent.Add(summaryRow);
        }

        private void DrawIssueItem(UIPanelValidator.ValidationIssue issue)
        {
            var bgColor = GetIssueSeverityBgColor(issue.Severity);

            var card = new VisualElement();
            card.style.backgroundColor = new StyleColor(bgColor);
            card.style.paddingTop = Spacing.SM;
            card.style.paddingBottom = Spacing.SM;
            card.style.paddingLeft = Spacing.MD;
            card.style.paddingRight = Spacing.MD;
            card.style.marginBottom = Spacing.SM;
            card.style.borderTopLeftRadius = card.style.borderTopRightRadius = Radius.MD;
            card.style.borderBottomLeftRadius = card.style.borderBottomRightRadius = Radius.MD;

            // 第一行：图标 + 类别 + 消息
            var headerRow = new VisualElement();
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.alignItems = Align.FlexStart;

            var iconId = GetIssueSeverityIconId(issue.Severity);
            var iconColor = GetIssueSeverityTextColor(issue.Severity);
            var iconImage = new Image { image = KitIcons.GetTexture(iconId) };
            iconImage.style.width = 16;
            iconImage.style.height = 16;
            iconImage.style.marginRight = Spacing.XS;
            iconImage.tintColor = iconColor;
            headerRow.Add(iconImage);

            var categoryLabel = GetIssueCategoryLabel(issue.Category);
            headerRow.Add(new Label($"[{categoryLabel}]") { style = { width = 50, fontSize = 11, color = new StyleColor(Colors.TextTertiary) } });

            headerRow.Add(new Label(issue.Message) { style = { flexGrow = 1, whiteSpace = WhiteSpace.Normal } });

            card.Add(headerRow);

            // 修复建议
            if (!string.IsNullOrEmpty(issue.FixSuggestion))
            {
                var suggestionRow = new VisualElement();
                suggestionRow.style.flexDirection = FlexDirection.Row;
                suggestionRow.style.alignItems = Align.Center;
                suggestionRow.style.paddingLeft = Spacing.XL;
                suggestionRow.style.marginTop = Spacing.XS;
                
                var arrowIcon = new Image { image = KitIcons.GetTexture(KitIcons.ARROW_RIGHT) };
                arrowIcon.style.width = 12;
                arrowIcon.style.height = 12;
                arrowIcon.style.marginRight = Spacing.XS;
                arrowIcon.tintColor = Colors.TextTertiary;
                suggestionRow.Add(arrowIcon);
                
                suggestionRow.Add(new Label(issue.FixSuggestion) { style = { fontSize = 11, color = new StyleColor(Colors.TextTertiary) } });
                card.Add(suggestionRow);
            }

            // 定位按钮
            if (issue.Context != null)
            {
                var btnRow = new VisualElement();
                btnRow.style.flexDirection = FlexDirection.Row;
                btnRow.style.justifyContent = Justify.FlexEnd;
                btnRow.style.marginTop = Spacing.XS;

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
            titleLabel.style.marginBottom = Spacing.MD;
            mValidatorContent.Add(titleLabel);

            for (int i = 0; i < mValidatorSceneResults.Count; i++)
            {
                var result = mValidatorSceneResults[i];

                var card = new VisualElement();
                card.style.backgroundColor = new StyleColor(Colors.LayerToolbar);
                card.style.paddingTop = Spacing.SM;
                card.style.paddingBottom = Spacing.SM;
                card.style.paddingLeft = Spacing.MD;
                card.style.paddingRight = Spacing.MD;
                card.style.marginBottom = Spacing.SM;
                card.style.borderTopLeftRadius = card.style.borderTopRightRadius = Radius.MD;
                card.style.borderBottomLeftRadius = card.style.borderBottomRightRadius = Radius.MD;

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
                card.Add(new Label($"  错误: {errorCount}, 警告: {warningCount}") { style = { fontSize = 11, color = new StyleColor(Colors.TextTertiary), marginTop = Spacing.XS } });

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
            UIPanelValidator.IssueSeverity.Error => new Color(Colors.BadgeError.r, Colors.BadgeError.g, Colors.BadgeError.b, 0.3f),
            UIPanelValidator.IssueSeverity.Warning => new Color(Colors.BadgeWarning.r, Colors.BadgeWarning.g, Colors.BadgeWarning.b, 0.3f),
            UIPanelValidator.IssueSeverity.Info => new Color(Colors.BadgeInfo.r, Colors.BadgeInfo.g, Colors.BadgeInfo.b, 0.3f),
            _ => Colors.LayerToolbar
        };

        private static Color GetIssueSeverityTextColor(UIPanelValidator.IssueSeverity severity) => severity switch
        {
            UIPanelValidator.IssueSeverity.Error => Colors.StatusError,
            UIPanelValidator.IssueSeverity.Warning => Colors.StatusWarning,
            UIPanelValidator.IssueSeverity.Info => Colors.StatusInfo,
            _ => Color.white
        };

        private static string GetIssueSeverityIconId(UIPanelValidator.IssueSeverity severity) => severity switch
        {
            UIPanelValidator.IssueSeverity.Error => KitIcons.ERROR,
            UIPanelValidator.IssueSeverity.Warning => KitIcons.WARNING,
            UIPanelValidator.IssueSeverity.Info => KitIcons.INFO,
            _ => KitIcons.INFO
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
#endif
