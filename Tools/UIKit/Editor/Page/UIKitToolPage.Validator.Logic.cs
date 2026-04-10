#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using YokiFrame.EditorTools;
using static YokiFrame.EditorTools.YokiFrameUIComponents;

namespace YokiFrame
{
    /// <summary>
    /// 批量验证中心的数据请求、结果呈现和筛选辅助逻辑。
    /// </summary>
    public partial class UIKitToolPage
    {
        #region 验证器逻辑

        private void ValidateScene()
        {
            mValidatorSceneResults.Clear();
            var results = UIPanelValidator.ValidateAllPanelsInScene();
            mValidatorSceneResults.AddRange(results);
            RefreshValidatorContent();
        }

        private void RefreshValidatorContent()
        {
            if (mValidatorContent == null)
            {
                return;
            }

            mValidatorContent.Clear();

            if (mValidatorSceneResults.Count == 0)
            {
                mValidatorContent.Add(CreateHelpBox("点击“扫描当前场景”以批量检查所有 UIPanel。单个面板的绑定树和校验结果请在 Inspector 中查看。"));
                return;
            }

            DrawSceneResults();
        }

        private void DrawSceneResults()
        {
            int panelCount = mValidatorSceneResults.Count;
            int issueCount = 0;
            int errorCount = 0;
            int warningCount = 0;
            int infoCount = 0;

            for (int i = 0; i < mValidatorSceneResults.Count; i++)
            {
                var result = mValidatorSceneResults[i];
                issueCount += result.Issues.Count;
                errorCount += result.GetErrorCount();
                warningCount += result.GetWarningCount();
                infoCount += result.Issues.Count - result.GetErrorCount() - result.GetWarningCount();
            }

            var metricStrip = CreateKitMetricStrip();
            var (panelCard, _) = CreateKitMetricCard("面板数量", panelCount.ToString(), "本次扫描覆盖的 UIPanel 数量");
            var (issueCard, _) = CreateKitMetricCard("问题总数", issueCount.ToString(), "所有面板累计问题数量", Colors.StatusWarning);
            var (errorCard, _) = CreateKitMetricCard("错误", errorCount.ToString(), "优先处理的问题数量", Colors.StatusError);
            var (warningCard, _) = CreateKitMetricCard("警告", warningCount.ToString(), "建议继续排查的风险", Colors.StatusWarning);
            metricStrip.Add(panelCard);
            metricStrip.Add(issueCard);
            metricStrip.Add(errorCard);
            metricStrip.Add(warningCard);

            if (infoCount > 0)
            {
                var (infoCard, _) = CreateKitMetricCard("信息", infoCount.ToString(), "补充说明与提示", Colors.StatusInfo);
                metricStrip.Add(infoCard);
            }

            mValidatorContent.Add(metricStrip);

            for (int i = 0; i < mValidatorSceneResults.Count; i++)
            {
                var result = mValidatorSceneResults[i];
                DrawSceneResultCard(result);
            }
        }

        private void DrawSceneResultCard(UIPanelValidator.ValidationResult result)
        {
            var card = new VisualElement();
            card.AddToClassList("yoki-validator-scene-card");

            var headerRow = new VisualElement();
            headerRow.AddToClassList("yoki-validator-scene-header");

            var targetName = result.Target != null ? result.Target.name : "未知";
            var nameLabel = new Label(targetName);
            nameLabel.AddToClassList("yoki-validator-scene-name");
            headerRow.Add(nameLabel);

            if (result.Target != null)
            {
                headerRow.Add(CreateSmallButton("选择", () =>
                {
                    Selection.activeGameObject = result.Target;
                    EditorGUIUtility.PingObject(result.Target);
                }));

                headerRow.Add(CreateSmallButton("Inspector", () =>
                {
                    Selection.activeGameObject = result.Target;
                    EditorGUIUtility.PingObject(result.Target);
                    EditorApplication.ExecuteMenuItem("Window/General/Inspector");
                }));
            }

            card.Add(headerRow);

            var visibleIssues = 0;
            for (int i = 0; i < result.Issues.Count; i++)
            {
                if (PassValidatorFilter(result.Issues[i]))
                {
                    visibleIssues++;
                }
            }

            var statsLabel = new Label($"错误: {result.GetErrorCount()}，警告: {result.GetWarningCount()}，可见问题: {visibleIssues}");
            statsLabel.AddToClassList("yoki-validator-scene-stats");
            statsLabel.style.color = new StyleColor(Colors.TextTertiary);
            card.Add(statsLabel);

            if (visibleIssues == 0)
            {
                card.Add(CreateHelpBox("当前筛选条件下没有需要显示的问题。", HelpBoxType.Success));
                mValidatorContent.Add(card);
                return;
            }

            for (int i = 0; i < result.Issues.Count; i++)
            {
                var issue = result.Issues[i];
                if (!PassValidatorFilter(issue))
                {
                    continue;
                }

                DrawIssueItem(card, issue);
            }

            mValidatorContent.Add(card);
        }

        private void DrawIssueItem(VisualElement parent, UIPanelValidator.ValidationIssue issue)
        {
            var card = new VisualElement();
            card.AddToClassList("yoki-validator-issue-card");

            switch (issue.Severity)
            {
                case UIPanelValidator.IssueSeverity.Error:
                    card.AddToClassList("yoki-validator-issue-card--error");
                    break;
                case UIPanelValidator.IssueSeverity.Warning:
                    card.AddToClassList("yoki-validator-issue-card--warning");
                    break;
                case UIPanelValidator.IssueSeverity.Info:
                    card.AddToClassList("yoki-validator-issue-card--info");
                    break;
            }

            var headerRow = new VisualElement();
            headerRow.AddToClassList("yoki-validator-issue-header");

            var iconId = GetIssueSeverityIconId(issue.Severity);
            var iconColor = GetIssueSeverityTextColor(issue.Severity);
            var iconImage = new Image { image = KitIcons.GetTexture(iconId) };
            iconImage.AddToClassList("yoki-validator-issue-icon");
            iconImage.tintColor = iconColor;
            headerRow.Add(iconImage);

            var categoryLabel = GetIssueCategoryLabel(issue.Category);
            var categoryElement = new Label($"[{categoryLabel}]");
            categoryElement.AddToClassList("yoki-validator-issue-category");
            categoryElement.style.color = new StyleColor(Colors.TextTertiary);
            headerRow.Add(categoryElement);

            var messageElement = new Label(issue.Message);
            messageElement.AddToClassList("yoki-validator-issue-message");
            headerRow.Add(messageElement);
            card.Add(headerRow);

            if (!string.IsNullOrEmpty(issue.FixSuggestion))
            {
                var suggestionRow = new VisualElement();
                suggestionRow.AddToClassList("yoki-validator-suggestion-row");

                var arrowIcon = new Image { image = KitIcons.GetTexture(KitIcons.ARROW_RIGHT) };
                arrowIcon.AddToClassList("yoki-validator-suggestion-icon");
                arrowIcon.tintColor = Colors.TextTertiary;
                suggestionRow.Add(arrowIcon);

                var suggestionText = new Label(issue.FixSuggestion);
                suggestionText.AddToClassList("yoki-validator-suggestion-text");
                suggestionText.style.color = new StyleColor(Colors.TextTertiary);
                suggestionRow.Add(suggestionText);

                card.Add(suggestionRow);
            }

            if (issue.Context != null)
            {
                var btnRow = new VisualElement();
                btnRow.AddToClassList("yoki-validator-button-row");

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

            parent.Add(card);
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
