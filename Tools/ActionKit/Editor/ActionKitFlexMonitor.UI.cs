using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// ActionKitFlexMonitor - UI 构建部分
    /// </summary>
    public partial class ActionKitFlexMonitor
    {
        #region 头部与统计

        private void BuildHeader(VisualElement parent)
        {
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginBottom = 16 } };
            parent.Add(row);

            var title = new Label("Action 流程可视化") { style = { fontSize = 16, unityFontStyleAndWeight = FontStyle.Bold, flexGrow = 1 } };
            row.Add(title);

            // 响应式模式提示
            var hint = new Label("🔄 响应式") { style = { fontSize = 10, color = new StyleColor(COLOR_RUNNING), marginRight = 8 }, tooltip = "自动响应 Action 状态变化" };
            row.Add(hint);
            
            var refreshBtn = new Button(RefreshData) { text = "刷新", style = { marginLeft = 8 } };
            row.Add(refreshBtn);
        }

        private void BuildStatsCard(VisualElement parent)
        {
            var card = CreateLocalCard("统计信息");
            parent.Add(card);

            var content = new VisualElement { style = { paddingLeft = 12, paddingRight = 12, paddingBottom = 12, flexDirection = FlexDirection.Row } };
            card.Add(content);

            var activeBox = CreateStatBox("活跃", "0", COLOR_RUNNING);
            mActiveCountLabel = activeBox.Q<Label>("value");
            content.Add(activeBox);

            var totalBox = CreateStatBox("完成", "0", COLOR_LEAF_DELAY);
            mTotalFinishedLabel = totalBox.Q<Label>("value");
            totalBox.style.marginLeft = 16;
            content.Add(totalBox);

            content.Add(CreateLegend());
        }

        private VisualElement CreateLegend()
        {
            var box = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, flexWrap = Wrap.Wrap, marginLeft = 32 } };
            AddLegendItem(box, "Seq", COLOR_SEQUENCE);
            AddLegendItem(box, "Par", COLOR_PARALLEL);
            AddLegendItem(box, "Delay", COLOR_LEAF_DELAY);
            AddLegendItem(box, "Callback", COLOR_LEAF_CALLBACK);
            return box;
        }

        private void AddLegendItem(VisualElement parent, string label, Color color)
        {
            var item = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginRight = 12 } };
            var dot = new VisualElement { style = { width = 10, height = 10, borderTopLeftRadius = 2, borderTopRightRadius = 2, borderBottomLeftRadius = 2, borderBottomRightRadius = 2, backgroundColor = new StyleColor(color), marginRight = 4 } };
            item.Add(dot);
            item.Add(new Label(label) { style = { fontSize = 10, color = new StyleColor(new Color(0.7f, 0.7f, 0.7f)) } });
            parent.Add(item);
        }

        private VisualElement CreateStatBox(string label, string value, Color color)
        {
            var box = new VisualElement { style = { backgroundColor = new StyleColor(CARD_BG_DARK), borderTopLeftRadius = 4, borderTopRightRadius = 4, borderBottomLeftRadius = 4, borderBottomRightRadius = 4, paddingLeft = 16, paddingRight = 16, paddingTop = 8, paddingBottom = 8, borderLeftWidth = 3, borderLeftColor = new StyleColor(color) } };
            box.Add(new Label(label) { style = { fontSize = 11, color = new StyleColor(new Color(0.6f, 0.6f, 0.6f)) } });
            var val = new Label(value) { name = "value", style = { fontSize = 20, unityFontStyleAndWeight = FontStyle.Bold, color = new StyleColor(color) } };
            box.Add(val);
            return box;
        }

        #endregion

        #region 堆栈设置

        private void BuildStackSettings(VisualElement parent)
        {
            var card = CreateLocalCard("堆栈追踪");
            card.style.marginTop = 12;
            parent.Add(card);

            var content = new VisualElement { style = { paddingLeft = 12, paddingRight = 12, paddingBottom = 12 } };
            card.Add(content);

            // 提示信息
            var hint = new Label("💡 启用后需重新运行游戏才能记录新 Action 的堆栈") { style = { fontSize = 10, color = new StyleColor(new Color(0.6f, 0.6f, 0.6f)), marginBottom = 8 } };
            content.Add(hint);

            var row1 = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginBottom = 8 } };
            content.Add(row1);
            row1.Add(YokiFrameUIComponents.CreateModernToggle("启用堆栈追踪", ActionStackTraceService.Enabled, v => ActionStackTraceService.Enabled = v));
            row1.Add(new VisualElement { style = { flexGrow = 1 } });
            mStackCountLabel = new Label($"已记录: {ActionStackTraceService.Count}") { style = { fontSize = 11, color = new StyleColor(new Color(0.6f, 0.6f, 0.6f)) } };
            row1.Add(mStackCountLabel);

            var row2 = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
            content.Add(row2);
            row2.Add(YokiFrameUIComponents.CreateModernToggle("退出时清空", mClearStackOnExit, v => mClearStackOnExit = v));
            row2.Add(new VisualElement { style = { flexGrow = 1 } });
            row2.Add(new Button(() => { ActionStackTraceService.Clear(); mStackCountLabel.text = "已记录: 0"; mStackTraceLabel.text = "点击卡片查看堆栈"; }) { text = "清空" });
        }

        #endregion

        #region 流程图与堆栈卡片

        private void BuildFlexTreeCard(VisualElement parent)
        {
            var card = CreateLocalCard("Action 流程图");
            card.style.marginTop = 12;
            card.style.minHeight = 200;
            parent.Add(card);

            mTreeContainer = new VisualElement { style = { paddingLeft = 12, paddingRight = 12, paddingTop = 8, paddingBottom = 12, flexDirection = FlexDirection.Column } };
            card.Add(mTreeContainer);
        }

        private void BuildStackTraceCard(VisualElement parent)
        {
            var card = CreateLocalCard("调用堆栈");
            card.style.marginTop = 12;
            parent.Add(card);

            mStackTraceLabel = new Label("点击卡片查看堆栈") { style = { paddingLeft = 12, paddingRight = 12, paddingTop = 8, paddingBottom = 12, fontSize = 11, color = new StyleColor(new Color(0.6f, 0.6f, 0.6f)), whiteSpace = WhiteSpace.Normal } };
            card.Add(mStackTraceLabel);
        }

        /// <summary>
        /// 创建本地卡片样式（避免与基类 CreateCard 冲突）
        /// </summary>
        private VisualElement CreateLocalCard(string title)
        {
            var card = new VisualElement { style = { backgroundColor = new StyleColor(CARD_BG_DARK), borderTopLeftRadius = CARD_BORDER_RADIUS, borderTopRightRadius = CARD_BORDER_RADIUS, borderBottomLeftRadius = CARD_BORDER_RADIUS, borderBottomRightRadius = CARD_BORDER_RADIUS } };
            var header = new Label(title) { style = { fontSize = 13, unityFontStyleAndWeight = FontStyle.Bold, paddingLeft = 12, paddingTop = 10, paddingBottom = 8, borderBottomWidth = 1, borderBottomColor = new StyleColor(new Color(0.25f, 0.25f, 0.25f)) } };
            card.Add(header);
            return card;
        }

        #endregion
    }
}
