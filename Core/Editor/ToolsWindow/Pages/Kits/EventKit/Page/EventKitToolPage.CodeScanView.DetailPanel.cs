#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame
{
    /// <summary>
    /// EventKit 代码扫描视图中的事件详情面板。
    /// </summary>
    public partial class EventKitToolPage
    {
        #region 事件枢纽列

        /// <summary>
        /// 创建包含详情折叠区的事件中枢列。
        /// </summary>
        private (VisualElement column, VisualElement detailPanel) CreateEventHubColumnWithDetail(EventFlowData flow, HealthStatus health)
        {
            var column = new VisualElement();
            column.style.width = 240;
            column.style.flexDirection = FlexDirection.Row;
            column.style.justifyContent = Justify.SpaceBetween;
            column.style.alignItems = Align.Center;

            var leftArrow = CreateArrowIcon(flow.Senders.Count > 0, true);
            column.Add(leftArrow);

            var (detailPanel, detailContent) = EditorTools.YokiFrameUIComponents.CreateFoldoutPanel();
            BuildDetailPanelContent(detailContent, flow);

            var eventCard = CreateClickableEventHubCard(flow, health, detailPanel);
            column.Add(eventCard);

            var rightArrow = CreateArrowIcon(flow.Receivers.Count > 0, false);
            column.Add(rightArrow);

            return (column, detailPanel);
        }

        /// <summary>
        /// 创建可点击展开详情的事件卡片。
        /// </summary>
        private VisualElement CreateClickableEventHubCard(EventFlowData flow, HealthStatus health, VisualElement detailPanel)
        {
            var card = CreateEventCardBase(flow, health);
            card.AddToClassList("clickable");

            var statsRow = new VisualElement();
            statsRow.style.flexDirection = FlexDirection.Row;
            statsRow.style.justifyContent = Justify.Center;
            statsRow.style.marginTop = 6;
            card.Add(statsRow);

            var regBadge = EditorTools.YokiFrameUIComponents.CreateStatsBadge(
                "R", flow.Receivers.Count, new Color(0.5f, 1f, 0.6f));
            statsRow.Add(regBadge);

            var unregBadge = EditorTools.YokiFrameUIComponents.CreateStatsBadge(
                "U", flow.Unregisters.Count, new Color(0.5f, 0.7f, 1f));
            statsRow.Add(unregBadge);

            var expandHintRow = new VisualElement();
            expandHintRow.style.flexDirection = FlexDirection.Row;
            expandHintRow.style.alignItems = Align.Center;
            expandHintRow.style.marginTop = 4;
            card.Add(expandHintRow);

            var expandIcon = new Image { image = EditorTools.KitIcons.GetTexture(EditorTools.KitIcons.CHEVRON_DOWN) };
            expandIcon.style.width = 10;
            expandIcon.style.height = 10;
            expandIcon.style.marginRight = 4;
            expandIcon.tintColor = new Color(0.4f, 0.4f, 0.4f);
            expandHintRow.Add(expandIcon);

            var expandHint = new Label("点击展开");
            expandHint.style.fontSize = 9;
            expandHint.style.color = new StyleColor(new Color(0.4f, 0.4f, 0.4f));
            expandHintRow.Add(expandHint);

            card.RegisterCallback<MouseEnterEvent>(_ =>
            {
                card.style.backgroundColor = new StyleColor(new Color(0.28f, 0.28f, 0.32f));
                expandHint.style.color = new StyleColor(new Color(0.6f, 0.6f, 0.6f));
                expandIcon.tintColor = new Color(0.6f, 0.6f, 0.6f);
            });
            card.RegisterCallback<MouseLeaveEvent>(_ =>
            {
                card.style.backgroundColor = new StyleColor(new Color(0.22f, 0.22f, 0.25f));
                expandHint.style.color = new StyleColor(new Color(0.4f, 0.4f, 0.4f));
                expandIcon.tintColor = new Color(0.4f, 0.4f, 0.4f);
            });

            card.RegisterCallback<ClickEvent>(evt =>
            {
                evt.StopPropagation();
                bool isExpanded = detailPanel.style.display == DisplayStyle.Flex;
                detailPanel.style.display = isExpanded ? DisplayStyle.None : DisplayStyle.Flex;
                expandIcon.image = EditorTools.KitIcons.GetTexture(
                    isExpanded ? EditorTools.KitIcons.CHEVRON_DOWN : EditorTools.KitIcons.EXPAND);
                expandHint.text = isExpanded ? "点击展开" : "点击收起";
            });

            return card;
        }

        /// <summary>
        /// 创建事件卡片的基础结构。
        /// </summary>
        private VisualElement CreateEventCardBase(EventFlowData flow, HealthStatus health)
        {
            var card = new VisualElement();
            card.style.backgroundColor = new StyleColor(new Color(0.22f, 0.22f, 0.25f));
            card.style.paddingLeft = 12;
            card.style.paddingRight = 12;
            card.style.paddingTop = 8;
            card.style.paddingBottom = 8;
            card.style.borderTopLeftRadius = 6;
            card.style.borderTopRightRadius = 6;
            card.style.borderBottomLeftRadius = 6;
            card.style.borderBottomRightRadius = 6;
            card.style.alignItems = Align.Center;

            var statusLabel = CreateHealthStatusLabel(health);
            statusLabel.style.marginBottom = 4;
            card.Add(statusLabel);

            var nameLabel = new Label(flow.EventKey);
            nameLabel.style.fontSize = 12;
            nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            nameLabel.style.color = new StyleColor(new Color(0.93f, 0.93f, 0.93f));
            nameLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            card.Add(nameLabel);

            if (!string.IsNullOrEmpty(flow.ParamType))
            {
                var paramLabel = new Label($"<{flow.ParamType}>");
                paramLabel.style.fontSize = 10;
                paramLabel.style.color = new StyleColor(new Color(0.5f, 0.5f, 0.5f));
                paramLabel.style.marginTop = 2;
                card.Add(paramLabel);
            }

            return card;
        }

        #endregion

        #region 详情面板内容

        /// <summary>
        /// 构建详情面板，左右分别展示注册和注销位置。
        /// </summary>
        private void BuildDetailPanelContent(VisualElement content, EventFlowData flow)
        {
            var mainRow = new VisualElement();
            mainRow.style.flexDirection = FlexDirection.Row;
            mainRow.style.justifyContent = Justify.Center;
            mainRow.style.alignItems = Align.FlexStart;
            content.Add(mainRow);

            if (flow.Receivers.Count > 0)
            {
                var regColumn = CreateDetailColumn(
                    $"Register ({flow.Receivers.Count})",
                    new Color(0.5f, 1f, 0.6f),
                    flow.Receivers,
                    new Color(0.6f, 0.9f, 0.7f));
                mainRow.Add(regColumn);
            }

            if (flow.Unregisters.Count > 0)
            {
                var unregColumn = CreateDetailColumn(
                    $"UnRegister ({flow.Unregisters.Count})",
                    new Color(0.5f, 0.7f, 1f),
                    flow.Unregisters,
                    new Color(0.6f, 0.8f, 1f));
                mainRow.Add(unregColumn);
            }

            BuildLeakWarning(content, flow);
        }

        /// <summary>
        /// 创建详情列。
        /// </summary>
        private VisualElement CreateDetailColumn(
            string title,
            Color titleColor,
            System.Collections.Generic.List<EventCodeScanner.ScanResult> items,
            Color itemColor)
        {
            var column = new VisualElement();
            column.style.minWidth = 140;
            column.style.maxWidth = 220;
            column.style.marginLeft = 8;
            column.style.marginRight = 8;
            column.style.paddingLeft = 8;
            column.style.paddingRight = 8;
            column.style.paddingTop = 4;
            column.style.paddingBottom = 4;
            column.style.backgroundColor = new StyleColor(new Color(0.12f, 0.12f, 0.14f));
            column.style.borderTopLeftRadius = 4;
            column.style.borderTopRightRadius = 4;
            column.style.borderBottomLeftRadius = 4;
            column.style.borderBottomRightRadius = 4;

            var titleLabel = new Label(title);
            titleLabel.style.fontSize = 11;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.color = new StyleColor(titleColor);
            titleLabel.style.marginBottom = 4;
            titleLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            column.Add(titleLabel);

            foreach (var item in items)
            {
                var label = EditorTools.YokiFrameUIComponents.CreateClickableLocationLabel(
                    item.FilePath, item.LineNumber, itemColor,
                    () => OpenFileAtLine(item.FilePath, item.LineNumber));
                label.style.unityTextAlign = TextAnchor.MiddleCenter;
                column.Add(label);
            }

            return column;
        }

        /// <summary>
        /// 构建泄漏风险提示。
        /// </summary>
        private void BuildLeakWarning(VisualElement content, EventFlowData flow)
        {
            string warningText = null;
            Color warningColor = default;
            bool withBackground = false;

            if (flow.Receivers.Count > flow.Unregisters.Count && flow.Unregisters.Count > 0)
            {
                int diff = flow.Receivers.Count - flow.Unregisters.Count;
                warningText = $"可能存在 {diff} 处未注销的监听器";
                warningColor = new Color(1f, 0.8f, 0.4f);
                withBackground = true;
            }
            else if (flow.Receivers.Count > 0 && flow.Unregisters.Count == 0)
            {
                warningText = "未找到任何 UnRegister 调用";
                warningColor = new Color(1f, 0.7f, 0.4f);
            }

            if (warningText == null)
            {
                return;
            }

            var warningContainer = new VisualElement();
            warningContainer.style.flexDirection = FlexDirection.Row;
            warningContainer.style.justifyContent = Justify.Center;
            warningContainer.style.marginTop = 8;

            var warningRow = new VisualElement();
            warningRow.style.flexDirection = FlexDirection.Row;
            warningRow.style.alignItems = Align.Center;

            var warningIcon = new Image { image = EditorTools.KitIcons.GetTexture(EditorTools.KitIcons.WARNING) };
            warningIcon.style.width = 14;
            warningIcon.style.height = 14;
            warningIcon.style.marginRight = 4;
            warningIcon.tintColor = warningColor;
            warningRow.Add(warningIcon);

            var warningLabel = new Label(warningText);
            ApplyWarningStyle(warningLabel, warningColor, withBackground);
            warningRow.Add(warningLabel);

            if (withBackground)
            {
                warningRow.style.paddingLeft = 8;
                warningRow.style.paddingRight = 8;
                warningRow.style.paddingTop = 4;
                warningRow.style.paddingBottom = 4;
                warningRow.style.backgroundColor = new StyleColor(new Color(0.4f, 0.3f, 0.1f, 0.3f));
                warningRow.style.borderTopLeftRadius = 4;
                warningRow.style.borderTopRightRadius = 4;
                warningRow.style.borderBottomLeftRadius = 4;
                warningRow.style.borderBottomRightRadius = 4;
            }

            warningContainer.Add(warningRow);
            content.Add(warningContainer);
        }

        /// <summary>
        /// 应用警告文本样式。
        /// </summary>
        private void ApplyWarningStyle(Label label, Color color, bool withBackground)
        {
            label.style.fontSize = 10;
            label.style.color = new StyleColor(color);
        }

        #endregion
    }
}
#endif
