using UnityEngine;
using UnityEngine.UIElements;
using static YokiFrame.EditorTools.YokiFrameUIComponents;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// ActionKit 监控页的 UI 构建部分。
    /// </summary>
    public partial class ActionKitFlexMonitor
    {
        #region 头部与统计

        private void BuildHeader(VisualElement parent)
        {
            var toolbar = CreateToolbar();
            toolbar.AddToClassList("yoki-action-toolbar");
            parent.Add(toolbar);

            var titleBlock = new VisualElement();
            titleBlock.AddToClassList("yoki-action-toolbar__title-group");
            toolbar.Add(titleBlock);

            var title = new Label("Action 流程可视化");
            title.AddToClassList("yoki-action-toolbar__title");
            titleBlock.Add(title);

            var summary = new Label("查看当前根 Action、组合结构与堆栈追踪状态。");
            summary.AddToClassList("yoki-action-toolbar__summary");
            titleBlock.Add(summary);

            toolbar.Add(CreateToolbarSpacer());

            var runtimeChip = new VisualElement();
            runtimeChip.AddToClassList("yoki-action-toolbar__chip");
            runtimeChip.AddToClassList(Application.isPlaying
                ? "yoki-action-toolbar__chip--active"
                : "yoki-action-toolbar__chip--inactive");
            toolbar.Add(runtimeChip);

            var chipIcon = new Image { image = KitIcons.GetTexture(KitIcons.REFRESH) };
            chipIcon.AddToClassList("yoki-action-toolbar__chip-icon");
            chipIcon.tintColor = Colors.BrandPrimary;
            runtimeChip.Add(chipIcon);

            var chipLabel = new Label(Application.isPlaying ? "响应式监控中" : "等待运行模式");
            chipLabel.AddToClassList("yoki-action-toolbar__chip-label");
            runtimeChip.Add(chipLabel);

            var refreshBtn = CreateToolbarButton("刷新", RefreshData);
            refreshBtn.AddToClassList("yoki-action-toolbar__button");
            toolbar.Add(refreshBtn);
        }

        private void BuildStatsCard(VisualElement parent)
        {
            var (panel, body) = CreateKitSectionPanel("统计信息", "概览当前活跃 Action、累计完成数量和动作类型分布。", KitIcons.CHART);
            panel.AddToClassList("yoki-action-section");
            parent.Add(panel);

            var metrics = CreateKitMetricStrip();
            var (activeCard, activeValue) = CreateKitMetricCard("活跃", "0", "当前仍在执行中的 Action 数量", COLOR_RUNNING);
            var (finishedCard, finishedValue) = CreateKitMetricCard("完成", "0", "监控期间累计完成的 Action 数量", COLOR_LEAF_DELAY);
            metrics.Add(activeCard);
            metrics.Add(finishedCard);
            body.Add(metrics);

            mActiveCountLabel = activeValue;
            mTotalFinishedLabel = finishedValue;

            body.Add(CreateLegend());
        }

        private VisualElement CreateLegend()
        {
            var box = new VisualElement();
            box.AddToClassList("yoki-action-legend");
            AddLegendItem(box, "顺序", COLOR_SEQUENCE);
            AddLegendItem(box, "并行", COLOR_PARALLEL);
            AddLegendItem(box, "重复", COLOR_LEAF_REPEAT);
            AddLegendItem(box, "延时", COLOR_LEAF_DELAY);
            AddLegendItem(box, "回调", COLOR_LEAF_CALLBACK);
            AddLegendItem(box, "插值", COLOR_LEAF_LERP);
            return box;
        }

        private void AddLegendItem(VisualElement parent, string label, Color color)
        {
            var item = new VisualElement();
            item.AddToClassList("yoki-action-legend__item");

            var dot = new VisualElement();
            dot.AddToClassList("yoki-action-legend__dot");
            dot.style.backgroundColor = new StyleColor(color);
            item.Add(dot);

            var text = new Label(label);
            text.AddToClassList("yoki-action-legend__label");
            item.Add(text);

            parent.Add(item);
        }

        #endregion

        #region 堆栈设置

        private void BuildStackSettings(VisualElement parent)
        {
            var (panel, body) = CreateKitSectionPanel("堆栈追踪", "用于记录 Action 的创建调用来源，便于从运行时流程回溯到代码入口。", KitIcons.TARGET);
            panel.AddToClassList("yoki-action-section");
            parent.Add(panel);

            var hint = CreateHelpBox("启用堆栈追踪后，需要重新进入 PlayMode 才能记录新创建 Action 的调用堆栈。默认建议关闭，仅在定位问题时手动开启。", YokiFrameUIComponents.HelpBoxType.Warning);
            body.Add(hint);

            var row1 = new VisualElement();
            row1.AddToClassList("yoki-action-settings-row");
            body.Add(row1);

            var enabledToggle = CreateModernToggle("启用堆栈追踪", ActionStackTraceService.Enabled, value =>
            {
                ActionStackTraceService.Enabled = value;
                RefreshStatusBanner();
            });
            enabledToggle.AddToClassList("yoki-action-settings-row__toggle");
            row1.Add(enabledToggle);

            row1.Add(new VisualElement { style = { flexGrow = 1 } });

            mStackCountLabel = new Label($"已记录: {ActionStackTraceService.Count}");
            mStackCountLabel.AddToClassList("yoki-action-settings-row__meta");
            row1.Add(mStackCountLabel);

            var row2 = new VisualElement();
            row2.AddToClassList("yoki-action-settings-row");
            body.Add(row2);

            var clearToggle = CreateModernToggle("退出时清空", mClearStackOnExit, value => mClearStackOnExit = value);
            clearToggle.AddToClassList("yoki-action-settings-row__toggle");
            row2.Add(clearToggle);

            row2.Add(new VisualElement { style = { flexGrow = 1 } });

            var clearBtn = CreateToolbarButton("清空记录", () =>
            {
                ActionStackTraceService.Clear();
                mStackCountLabel.text = "已记录: 0";
                mStackTraceLabel.text = "点击流程节点后，可在这里查看对应的调用堆栈。";
                mStackTraceLabel.style.color = new StyleColor(Colors.TextTertiary);
            });
            clearBtn.AddToClassList("yoki-action-settings-row__button");
            row2.Add(clearBtn);
        }

        #endregion

        #region 流程图与堆栈卡片

        private void BuildFlexTreeCard(VisualElement parent)
        {
            var (panel, body) = CreateKitSectionPanel("Action 流程图", "展示当前根 Action 及其子节点结构。点击节点可查看对应堆栈。", KitIcons.STACK);
            panel.AddToClassList("yoki-action-section");
            panel.AddToClassList("yoki-action-section--tree");
            parent.Add(panel);

            mTreeContainer = new VisualElement();
            mTreeContainer.AddToClassList("yoki-action-tree");
            body.Add(mTreeContainer);
        }

        private void BuildStackTraceCard(VisualElement parent)
        {
            var (panel, body) = CreateKitSectionPanel("调用堆栈", "显示当前选中 Action 的调用来源，便于快速定位代码入口。", KitIcons.CODE);
            panel.AddToClassList("yoki-action-section");
            parent.Add(panel);

            mStackTraceLabel = new Label("点击流程节点后，可在这里查看对应的调用堆栈。");
            mStackTraceLabel.AddToClassList("yoki-action-stack__content");
            body.Add(mStackTraceLabel);
        }

        #endregion
    }
}
