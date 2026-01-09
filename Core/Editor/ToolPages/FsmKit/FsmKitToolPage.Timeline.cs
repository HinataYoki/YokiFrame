using UnityEngine;
using UnityEngine.UIElements;
using YokiFrame.EditorTools;

namespace YokiFrame
{
    /// <summary>
    /// FsmKit 工具页面 - 转换时间轴区域
    /// 显示状态转换历史，类似 EventKit 的时间轴
    /// </summary>
    public partial class FsmKitToolPage
    {
        #region 常量

        private const float TIMELINE_HEIGHT = 200f;

        #endregion

        #region 字段

        private VisualElement mTimelineList;
        private Label mHistoryCountLabel;

        #endregion

        #region 构建时间轴

        /// <summary>
        /// 构建时间轴区域
        /// </summary>
        private VisualElement BuildTimelineSection()
        {
            var section = new VisualElement();
            section.name = "timeline-section";
            section.style.height = TIMELINE_HEIGHT;
            section.style.minHeight = TIMELINE_HEIGHT;

            // 工具栏
            var toolbar = CreateToolbar();
            section.Add(toolbar);

            var titleIcon = new Image { image = EditorTools.KitIcons.GetTexture(EditorTools.KitIcons.TIMELINE) };
            titleIcon.style.width = 14;
            titleIcon.style.height = 14;
            titleIcon.style.marginRight = 4;
            toolbar.Add(titleIcon);
            
            var titleLabel = new Label("转换历史");
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.AddToClassList("toolbar-label");
            toolbar.Add(titleLabel);

            var recordToggle = CreateToolbarToggle("记录", FsmDebugger.RecordTransitions,
                v => FsmDebugger.RecordTransitions = v);
            toolbar.Add(recordToggle);

            toolbar.Add(new VisualElement { style = { flexGrow = 1 } });

            mHistoryCountLabel = new Label("0/300");
            mHistoryCountLabel.AddToClassList("toolbar-label");
            mHistoryCountLabel.style.color = new StyleColor(YokiFrameUIComponents.Colors.TextTertiary);
            toolbar.Add(mHistoryCountLabel);

            var clearBtn = CreateToolbarButton("清空", () =>
            {
                FsmDebugger.ClearHistory();
                UpdateTimelineSection();
            });
            toolbar.Add(clearBtn);

            // 历史列表（ScrollView）
            var scrollView = new ScrollView();
            scrollView.style.flexGrow = 1;
            section.Add(scrollView);

            mTimelineList = new VisualElement();
            mTimelineList.name = "timeline-list";
            scrollView.Add(mTimelineList);

            return section;
        }

        /// <summary>
        /// 更新时间轴
        /// </summary>
        private void UpdateTimelineSection()
        {
            if (mTimelineList == null) return;

            mTimelineList.Clear();

            var history = FsmDebugger.TransitionHistory;
            mHistoryCountLabel.text = $"{history.Count}/{FsmDebugger.MAX_HISTORY_COUNT}";

            // 只显示选中 FSM 的历史
            var filterName = mSelectedFsm?.Name;

            // 倒序显示（最新的在上面）
            for (var i = history.Count - 1; i >= 0; i--)
            {
                var entry = history[i];

                // 过滤
                if (filterName != null && entry.FsmName != filterName)
                    continue;

                var item = CreateTimelineItem(entry);
                mTimelineList.Add(item);
            }

            // 空状态
            if (mTimelineList.childCount == 0)
            {
                var emptyLabel = new Label("  暂无转换记录");
                emptyLabel.style.color = new StyleColor(YokiFrameUIComponents.Colors.TextTertiary);
                emptyLabel.style.fontSize = 11;
                emptyLabel.style.marginTop = YokiFrameUIComponents.Spacing.SM;
                mTimelineList.Add(emptyLabel);
            }
        }

        /// <summary>
        /// 创建时间轴项
        /// </summary>
        private VisualElement CreateTimelineItem(FsmDebugger.TransitionEntry entry)
        {
            var item = new VisualElement();
            item.style.flexDirection = FlexDirection.Row;
            item.style.alignItems = Align.Center;
            item.style.paddingLeft = YokiFrameUIComponents.Spacing.SM;
            item.style.paddingRight = YokiFrameUIComponents.Spacing.SM;
            item.style.paddingTop = 4;
            item.style.paddingBottom = 4;
            item.style.borderBottomWidth = 1;
            item.style.borderBottomColor = new StyleColor(new Color(0.15f, 0.15f, 0.17f));

            // 时间戳
            var timeLabel = new Label($"[{entry.Time:F2}s]");
            timeLabel.style.width = 60;
            timeLabel.style.fontSize = 10;
            timeLabel.style.color = new StyleColor(YokiFrameUIComponents.Colors.TextTertiary);
            item.Add(timeLabel);

            // 动作类型徽章
            var (actionText, actionColor) = GetActionStyle(entry.Action);
            var actionBadge = new Label(actionText);
            actionBadge.style.width = 60;
            actionBadge.style.fontSize = 10;
            actionBadge.style.unityFontStyleAndWeight = FontStyle.Bold;
            actionBadge.style.color = new StyleColor(actionColor);
            item.Add(actionBadge);

            // 转换信息
            var transitionContainer = new VisualElement();
            transitionContainer.style.flexDirection = FlexDirection.Row;
            transitionContainer.style.alignItems = Align.Center;
            transitionContainer.style.flexGrow = 1;
            item.Add(transitionContainer);

            if (entry.Action == "Change")
            {
                // 状态转换：FromState → ToState
                var fromLabel = new Label(entry.FromState);
                fromLabel.style.fontSize = 11;
                fromLabel.style.color = new StyleColor(YokiFrameUIComponents.Colors.TextSecondary);
                transitionContainer.Add(fromLabel);

                var arrowLabel = new Label(" ➜ ");
                arrowLabel.style.fontSize = 11;
                arrowLabel.style.color = new StyleColor(YokiFrameUIComponents.Colors.BrandPrimary);
                transitionContainer.Add(arrowLabel);

                var toLabel = new Label(entry.ToState);
                toLabel.style.fontSize = 11;
                toLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                toLabel.style.color = new StyleColor(YokiFrameUIComponents.Colors.TextPrimary);
                transitionContainer.Add(toLabel);
            }
            else
            {
                // 其他动作：显示相关状态
                var stateText = !string.IsNullOrEmpty(entry.ToState) ? entry.ToState : entry.FromState;
                if (!string.IsNullOrEmpty(stateText))
                {
                    var stateLabel = new Label(stateText);
                    stateLabel.style.fontSize = 11;
                    stateLabel.style.color = new StyleColor(YokiFrameUIComponents.Colors.TextSecondary);
                    transitionContainer.Add(stateLabel);
                }
            }

            return item;
        }

        /// <summary>
        /// 获取动作样式
        /// </summary>
        private (string text, Color color) GetActionStyle(string action)
        {
            return action switch
            {
                "Start" => ("▶ Start", YokiFrameUIComponents.Colors.BrandSuccess),
                "Change" => ("↔ Change", YokiFrameUIComponents.Colors.BrandPrimary),
                "Stop" or "End" => ("■ Stop", YokiFrameUIComponents.Colors.BrandDanger),
                "Add" => ("+ Add", new Color(0.6f, 0.8f, 0.6f)),
                "Remove" => ("- Remove", new Color(0.8f, 0.6f, 0.6f)),
                "Clear" => ("✕ Clear", YokiFrameUIComponents.Colors.BrandWarning),
                "Dispose" => ("✕ Dispose", YokiFrameUIComponents.Colors.TextTertiary),
                _ => (action, YokiFrameUIComponents.Colors.TextSecondary)
            };
        }

        #endregion
    }
}
