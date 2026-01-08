#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame
{
    /// <summary>
    /// EventKit 代码扫描视图 - 事件流渲染（泳道布局）
    /// </summary>
    public partial class EventKitToolPage
    {
        #region 渲染事件流

        private void RenderEventFlowsByType(System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, EventFlowData>> flows)
        {
            // 添加列标题说明
            var headerRow = CreateColumnHeaderRow();
            mScanResultsScrollView.Add(headerRow);
            
            var typeOrder = new[] { "Enum", "Type", "String" };

            foreach (var eventType in typeOrder)
            {
                if (!flows.TryGetValue(eventType, out var typeDict) || typeDict.Count == 0)
                    continue;

                var groupHeader = CreateTypeGroupHeader(eventType, typeDict.Count);
                mScanResultsScrollView.Add(groupHeader);

                foreach (var kvp in typeDict)
                {
                    var flowRow = CreateEventFlowRow(kvp.Value);
                    mScanResultsScrollView.Add(flowRow);
                }

                var spacer = new VisualElement();
                spacer.style.height = 24;
                mScanResultsScrollView.Add(spacer);
            }
        }
        
        /// <summary>
        /// 创建列标题行（发送方 / 事件 / 接收方）
        /// </summary>
        private VisualElement CreateColumnHeaderRow()
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.paddingTop = 8;
            row.style.paddingBottom = 12;
            row.style.marginBottom = 8;
            row.style.borderBottomWidth = 1;
            row.style.borderBottomColor = new StyleColor(new Color(0.3f, 0.3f, 0.3f));

            // 左栏标题：发送方（红色系）
            var senderHeader = new VisualElement();
            senderHeader.style.flexGrow = 1;
            senderHeader.style.flexBasis = 0;
            senderHeader.style.alignItems = Align.FlexEnd;
            senderHeader.style.paddingRight = 10;
            
            var senderLabel = new Label("📤 发送方 (Send)");
            senderLabel.style.fontSize = 12;
            senderLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            senderLabel.style.color = new StyleColor(new Color(1f, 0.6f, 0.5f)); // 红色系
            senderHeader.Add(senderLabel);
            row.Add(senderHeader);

            // 中栏标题：事件
            var hubHeader = new VisualElement();
            hubHeader.style.width = 240;
            hubHeader.style.alignItems = Align.Center;
            
            var hubLabel = new Label("⚡ 事件");
            hubLabel.style.fontSize = 12;
            hubLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            hubLabel.style.color = new StyleColor(new Color(0.8f, 0.8f, 0.8f));
            hubHeader.Add(hubLabel);
            row.Add(hubHeader);

            // 右栏标题：接收方（绿色系）
            var receiverHeader = new VisualElement();
            receiverHeader.style.flexGrow = 1;
            receiverHeader.style.flexBasis = 0;
            receiverHeader.style.alignItems = Align.FlexStart;
            receiverHeader.style.paddingLeft = 10;
            
            var receiverLabel = new Label("📥 接收方 (Register)");
            receiverLabel.style.fontSize = 12;
            receiverLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            receiverLabel.style.color = new StyleColor(new Color(0.5f, 1f, 0.6f)); // 绿色系
            receiverHeader.Add(receiverLabel);
            row.Add(receiverHeader);

            return row;
        }

        private VisualElement CreateTypeGroupHeader(string eventType, int count)
        {
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.paddingTop = 12;
            header.style.paddingBottom = 8;
            header.style.marginBottom = 8;
            header.style.borderBottomWidth = 2;

            var (_, borderColor, textColor) = GetEventTypeColors(eventType);
            header.style.borderBottomColor = new StyleColor(borderColor);

            var icon = eventType switch
            {
                "Enum" => "🟢",
                "Type" => "🔵",
                "String" => "🟠",
                _ => "⚪"
            };

            var label = new Label($"{icon} {eventType} 事件 ({count})");
            label.style.fontSize = 16;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.color = new StyleColor(textColor);
            header.Add(label);

            return header;
        }

        /// <summary>
        /// 创建事件流行（泳道布局：发送方 -> 事件枢纽 -> 接收方）
        /// 关键：左右栏用 flex-grow:1 + flex-basis:0 均分，中栏固定宽度
        /// </summary>
        private VisualElement CreateEventFlowRow(EventFlowData flow)
        {
            // 外层容器（包含主行和可展开详情）
            var container = new VisualElement();
            container.style.marginBottom = 4;
            
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center; // 垂直居中整行
            row.style.paddingTop = 8;
            row.style.paddingBottom = 8;
            row.style.borderBottomWidth = 1;
            row.style.borderBottomColor = new StyleColor(new Color(0.17f, 0.17f, 0.17f));

            var health = flow.GetHealthStatus();

            // 根据健康状态设置背景
            if (health == HealthStatus.Orphan)
                row.style.backgroundColor = new StyleColor(new Color(0.4f, 0.35f, 0.2f, 0.1f));
            else if (health == HealthStatus.LeakRisk)
                row.style.backgroundColor = new StyleColor(new Color(0.4f, 0.2f, 0.2f, 0.1f));

            // 左栏：发送方（flex-grow:1, flex-basis:0, align-items:flex-end）
            var senderColumn = CreateSenderColumn(flow);
            row.Add(senderColumn);

            // 中栏：事件枢纽（固定宽度 240px）- 可点击展开详情
            var (hubColumn, detailPanel) = CreateEventHubColumnWithDetail(flow, health);
            row.Add(hubColumn);

            // 右栏：接收方（flex-grow:1, flex-basis:0, align-items:flex-start）
            var receiverColumn = CreateReceiverColumn(flow);
            row.Add(receiverColumn);

            container.Add(row);
            container.Add(detailPanel);

            return container;
        }

        #endregion

        #region 发送方列（左栏）- 内容靠右对齐

        private VisualElement CreateSenderColumn(EventFlowData flow)
        {
            var column = new VisualElement();
            // 关键样式：flex-grow:1 + flex-basis:0 让左右栏均分剩余空间
            column.style.flexGrow = 1;
            column.style.flexBasis = 0;
            // 关键：align-items:flex-end 把内容推到右边，紧贴中间箭头
            column.style.alignItems = Align.FlexEnd;
            column.style.justifyContent = Justify.Center;
            column.style.paddingRight = 10; // 与箭头的间距

            if (flow.Senders.Count == 0)
            {
                column.Add(CreateWarningBadge("⚠️ 无发送源"));
            }
            else
            {
                foreach (var sender in flow.Senders)
                    column.Add(CreateSenderLocationLabel(sender));
            }

            return column;
        }

        /// <summary>
        /// 创建发送方位置标签（红色系，右对齐样式）
        /// </summary>
        private VisualElement CreateSenderLocationLabel(EventCodeScanner.ScanResult result)
        {
            var fileName = System.IO.Path.GetFileName(result.FilePath);
            var label = new Label($"{fileName}:{result.LineNumber}");
            label.style.fontSize = 11;
            label.style.color = new StyleColor(new Color(1f, 0.7f, 0.6f)); // 红色系
            label.style.marginBottom = 2;
            label.style.unityTextAlign = TextAnchor.MiddleRight;

            // 点击跳转
            label.RegisterCallback<ClickEvent>(_ => OpenFileAtLine(result.FilePath, result.LineNumber));
            label.RegisterCallback<MouseEnterEvent>(_ => label.style.color = new StyleColor(new Color(1f, 0.85f, 0.8f)));
            label.RegisterCallback<MouseLeaveEvent>(_ => label.style.color = new StyleColor(new Color(1f, 0.7f, 0.6f)));

            return label;
        }

        #endregion

        #region 事件枢纽列（中栏）- 固定宽度

        /// <summary>
        /// 创建箭头图标（连接器样式，带颜色区分）
        /// </summary>
        private Label CreateArrowIcon(bool hasConnection, bool isSender)
        {
            var arrow = new Label("→");
            arrow.style.fontSize = 14;
            arrow.style.unityFontStyleAndWeight = FontStyle.Bold;
            
            if (hasConnection)
            {
                arrow.style.color = new StyleColor(isSender 
                    ? new Color(1f, 0.6f, 0.5f)   // 红色系
                    : new Color(0.5f, 1f, 0.6f)); // 绿色系
            }
            else
            {
                arrow.style.color = new StyleColor(new Color(0.25f, 0.25f, 0.25f));
            }
            
            return arrow;
        }

        private VisualElement CreateHealthStatusLabel(HealthStatus health)
        {
            var (text, bgColor, textColor) = health switch
            {
                HealthStatus.Healthy => ("✅ 完美闭环", new Color(0.2f, 0.4f, 0.2f), new Color(0.6f, 1f, 0.6f)),
                HealthStatus.Orphan => ("⚠️ 孤儿事件", new Color(0.4f, 0.35f, 0.2f), new Color(1f, 0.9f, 0.5f)),
                HealthStatus.LeakRisk => ("🛑 潜在泄露", new Color(0.4f, 0.2f, 0.2f), new Color(1f, 0.6f, 0.6f)),
                HealthStatus.NoSender => ("⚠️ 无发送源", new Color(0.35f, 0.35f, 0.2f), new Color(0.9f, 0.9f, 0.5f)),
                _ => ("", Color.clear, Color.white)
            };
            return EditorTools.YokiFrameUIComponents.CreateStatusBadge(text, bgColor, textColor);
        }

        #endregion

        #region 接收方列（右栏）- 内容靠左对齐

        private VisualElement CreateReceiverColumn(EventFlowData flow)
        {
            var column = new VisualElement();
            // 关键样式：flex-grow:1 + flex-basis:0 让左右栏均分剩余空间
            column.style.flexGrow = 1;
            column.style.flexBasis = 0;
            // 关键：align-items:flex-start 把内容推到左边，紧贴中间箭头
            column.style.alignItems = Align.FlexStart;
            column.style.justifyContent = Justify.Center;
            column.style.paddingLeft = 10; // 与箭头的间距

            if (flow.Receivers.Count == 0)
            {
                column.Add(CreateWarningBadge("⚠️ 无监听者"));
            }
            else
            {
                foreach (var receiver in flow.Receivers)
                    column.Add(CreateReceiverLocationLabel(receiver));
            }

            return column;
        }

        /// <summary>
        /// 创建接收方位置标签（绿色系，左对齐样式）
        /// </summary>
        private VisualElement CreateReceiverLocationLabel(EventCodeScanner.ScanResult result)
        {
            var fileName = System.IO.Path.GetFileName(result.FilePath);
            var label = new Label($"{fileName}:{result.LineNumber}");
            label.style.fontSize = 11;
            label.style.color = new StyleColor(new Color(0.6f, 1f, 0.7f)); // 绿色系
            label.style.marginBottom = 2;
            label.style.unityTextAlign = TextAnchor.MiddleLeft;

            // 点击跳转
            label.RegisterCallback<ClickEvent>(_ => OpenFileAtLine(result.FilePath, result.LineNumber));
            label.RegisterCallback<MouseEnterEvent>(_ => label.style.color = new StyleColor(new Color(0.8f, 1f, 0.85f)));
            label.RegisterCallback<MouseLeaveEvent>(_ => label.style.color = new StyleColor(new Color(0.6f, 1f, 0.7f)));

            return label;
        }

        #endregion

        #region 通用组件

        /// <summary>
        /// 创建警告徽章
        /// </summary>
        private VisualElement CreateWarningBadge(string text)
        {
            var badge = new VisualElement();
            badge.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0.2f));
            badge.style.borderTopLeftRadius = 4;
            badge.style.borderTopRightRadius = 4;
            badge.style.borderBottomLeftRadius = 4;
            badge.style.borderBottomRightRadius = 4;
            badge.style.paddingLeft = 8;
            badge.style.paddingRight = 8;
            badge.style.paddingTop = 4;
            badge.style.paddingBottom = 4;

            var label = new Label(text);
            label.style.fontSize = 10;
            label.style.color = new StyleColor(new Color(0.67f, 0.67f, 0.67f)); // #AAAAAA
            badge.Add(label);

            return badge;
        }

        #endregion
    }
}
#endif
