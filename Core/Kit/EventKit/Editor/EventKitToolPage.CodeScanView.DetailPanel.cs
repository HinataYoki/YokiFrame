#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame
{
    /// <summary>
    /// EventKit 代码扫描视图 - 事件详情面板（可展开）
    /// </summary>
    public partial class EventKitToolPage
    {
        #region 事件枢纽列（带详情面板）

        /// <summary>
        /// 创建事件枢纽列（带可展开详情面板）
        /// </summary>
        private (VisualElement column, VisualElement detailPanel) CreateEventHubColumnWithDetail(EventFlowData flow, HealthStatus health)
        {
            var column = new VisualElement();
            column.style.width = 240;
            column.style.flexDirection = FlexDirection.Row;
            column.style.justifyContent = Justify.SpaceBetween;
            column.style.alignItems = Align.Center;

            // 左箭头（红色系 - 发送方向）
            var leftArrow = CreateArrowIcon(flow.Senders.Count > 0, true);
            column.Add(leftArrow);

            // 创建详情面板（初始隐藏）
            var (detailPanel, detailContent) = EditorTools.YokiFrameUIComponents.CreateFoldoutPanel();
            BuildDetailPanelContent(detailContent, flow);

            // 事件卡片（可点击展开详情）
            var eventCard = CreateClickableEventHubCard(flow, health, detailPanel);
            column.Add(eventCard);

            // 右箭头（绿色系 - 接收方向）
            var rightArrow = CreateArrowIcon(flow.Receivers.Count > 0, false);
            column.Add(rightArrow);

            return (column, detailPanel);
        }

        /// <summary>
        /// 创建可点击的事件卡片（带统计信息）
        /// </summary>
        private VisualElement CreateClickableEventHubCard(EventFlowData flow, HealthStatus health, VisualElement detailPanel)
        {
            var card = CreateEventCardBase(flow, health);
            // 设置鼠标指针样式为链接
            card.AddToClassList("clickable");

            // 统计徽章行（方案 B：显示 R/U 统计）
            var statsRow = new VisualElement();
            statsRow.style.flexDirection = FlexDirection.Row;
            statsRow.style.justifyContent = Justify.Center;
            statsRow.style.marginTop = 6;
            card.Add(statsRow);

            // Register 统计（绿色）
            var regBadge = EditorTools.YokiFrameUIComponents.CreateStatsBadge(
                "R", flow.Receivers.Count, new Color(0.5f, 1f, 0.6f));
            statsRow.Add(regBadge);

            // UnRegister 统计（蓝色）
            var unregBadge = EditorTools.YokiFrameUIComponents.CreateStatsBadge(
                "U", flow.Unregisters.Count, new Color(0.5f, 0.7f, 1f));
            statsRow.Add(unregBadge);

            // 展开提示
            var expandHint = new Label("▼ 点击展开");
            expandHint.style.fontSize = 9;
            expandHint.style.color = new StyleColor(new Color(0.4f, 0.4f, 0.4f));
            expandHint.style.marginTop = 4;
            card.Add(expandHint);

            // 悬停效果
            card.RegisterCallback<MouseEnterEvent>(_ =>
            {
                card.style.backgroundColor = new StyleColor(new Color(0.28f, 0.28f, 0.32f));
                expandHint.style.color = new StyleColor(new Color(0.6f, 0.6f, 0.6f));
            });
            card.RegisterCallback<MouseLeaveEvent>(_ =>
            {
                card.style.backgroundColor = new StyleColor(new Color(0.22f, 0.22f, 0.25f));
                expandHint.style.color = new StyleColor(new Color(0.4f, 0.4f, 0.4f));
            });

            // 点击展开/折叠详情（方案 C）
            card.RegisterCallback<ClickEvent>(evt =>
            {
                evt.StopPropagation();
                var isExpanded = detailPanel.style.display == DisplayStyle.Flex;
                detailPanel.style.display = isExpanded ? DisplayStyle.None : DisplayStyle.Flex;
                expandHint.text = isExpanded ? "▼ 点击展开" : "▲ 点击收起";
            });

            return card;
        }

        /// <summary>
        /// 创建事件卡片基础结构
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

            // 健康状态标签
            var statusLabel = CreateHealthStatusLabel(health);
            statusLabel.style.marginBottom = 4;
            card.Add(statusLabel);

            // 事件名
            var nameLabel = new Label(flow.EventKey);
            nameLabel.style.fontSize = 12;
            nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            nameLabel.style.color = new StyleColor(new Color(0.93f, 0.93f, 0.93f));
            nameLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            card.Add(nameLabel);

            // 参数类型
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
        /// 构建详情面板内容（双列居中布局，数量多时向两边扩展）
        /// </summary>
        private void BuildDetailPanelContent(VisualElement content, EventFlowData flow)
        {
            // 主容器：水平布局，居中对齐
            var mainRow = new VisualElement();
            mainRow.style.flexDirection = FlexDirection.Row;
            mainRow.style.justifyContent = Justify.Center;
            mainRow.style.alignItems = Align.FlexStart;
            content.Add(mainRow);

            // Register 列（绿色）- 左侧
            if (flow.Receivers.Count > 0)
            {
                var regColumn = CreateDetailColumn(
                    $"📥 Register ({flow.Receivers.Count})",
                    new Color(0.5f, 1f, 0.6f),
                    flow.Receivers,
                    new Color(0.6f, 0.9f, 0.7f));
                mainRow.Add(regColumn);
            }

            // UnRegister 列（蓝色）- 右侧
            if (flow.Unregisters.Count > 0)
            {
                var unregColumn = CreateDetailColumn(
                    $"🔓 UnRegister ({flow.Unregisters.Count})",
                    new Color(0.5f, 0.7f, 1f),
                    flow.Unregisters,
                    new Color(0.6f, 0.8f, 1f));
                mainRow.Add(unregColumn);
            }

            // 泄漏风险提示（居中显示）
            BuildLeakWarning(content, flow);
        }

        /// <summary>
        /// 创建详情列（单列布局）
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

            // 标题
            var titleLabel = new Label(title);
            titleLabel.style.fontSize = 11;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.color = new StyleColor(titleColor);
            titleLabel.style.marginBottom = 4;
            titleLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            column.Add(titleLabel);

            // 列表项
            foreach (var item in items)
            {
                var label = EditorTools.YokiFrameUIComponents.CreateClickableLocationLabel(
                    item.FilePath, item.LineNumber,
                    itemColor,
                    () => OpenFileAtLine(item.FilePath, item.LineNumber));
                label.style.unityTextAlign = TextAnchor.MiddleCenter;
                column.Add(label);
            }

            return column;
        }

        /// <summary>
        /// 构建泄漏风险提示（居中显示）
        /// </summary>
        private void BuildLeakWarning(VisualElement content, EventFlowData flow)
        {
            Label warningLabel = null;
            
            if (flow.Receivers.Count > flow.Unregisters.Count && flow.Unregisters.Count > 0)
            {
                var diff = flow.Receivers.Count - flow.Unregisters.Count;
                warningLabel = new Label($"⚠️ 可能存在 {diff} 处未注销的监听器");
                ApplyWarningStyle(warningLabel, new Color(1f, 0.8f, 0.4f), true);
            }
            else if (flow.Receivers.Count > 0 && flow.Unregisters.Count == 0)
            {
                warningLabel = new Label("⚠️ 未找到任何 UnRegister 调用");
                ApplyWarningStyle(warningLabel, new Color(1f, 0.7f, 0.4f), false);
            }

            if (warningLabel != null)
            {
                // 居中容器
                var warningContainer = new VisualElement();
                warningContainer.style.flexDirection = FlexDirection.Row;
                warningContainer.style.justifyContent = Justify.Center;
                warningContainer.style.marginTop = 8;
                warningContainer.Add(warningLabel);
                content.Add(warningContainer);
            }
        }

        /// <summary>
        /// 应用警告样式
        /// </summary>
        private void ApplyWarningStyle(Label label, Color color, bool withBackground)
        {
            label.style.fontSize = 10;
            label.style.color = new StyleColor(color);
            
            if (withBackground)
            {
                label.style.paddingLeft = 8;
                label.style.paddingRight = 8;
                label.style.paddingTop = 4;
                label.style.paddingBottom = 4;
                label.style.backgroundColor = new StyleColor(new Color(0.4f, 0.3f, 0.1f, 0.3f));
                label.style.borderTopLeftRadius = 4;
                label.style.borderTopRightRadius = 4;
                label.style.borderBottomLeftRadius = 4;
                label.style.borderBottomRightRadius = 4;
            }
        }

        #endregion
    }
}
#endif
