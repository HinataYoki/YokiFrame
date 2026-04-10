#if UNITY_EDITOR
using UnityEngine.UIElements;
using YokiFrame.EditorTools;

namespace YokiFrame
{
    /// <summary>
    /// FsmKit 转换历史区。
    /// </summary>
    public partial class FsmKitToolPage
    {
        #region Fields

        private VisualElement mTimelineList;
        private Label mHistoryCountLabel;
        private bool mShowSelectedHistoryOnly = true;

        #endregion

        #region Timeline

        /// <summary>
        /// 构建转换历史面板。
        /// </summary>
        private VisualElement BuildTimelineSection()
        {
            var actions = new VisualElement();
            actions.style.flexDirection = FlexDirection.Row;
            actions.style.alignItems = Align.Center;

            var recordToggle = CreateToolbarToggle(
                "记录",
                FsmDebugger.RecordTransitions,
                value => FsmDebugger.RecordTransitions = value);
            recordToggle.tooltip = "控制状态机转换历史的记录开关。";
            actions.Add(recordToggle);

            var scopeToggle = CreateToolbarToggle(
                "仅当前",
                mShowSelectedHistoryOnly,
                value =>
                {
                    mShowSelectedHistoryOnly = value;
                    UpdateTimelineSection();
                });
            scopeToggle.tooltip = "开启后只显示当前选中状态机的历史；关闭后显示全部状态机的历史。";
            actions.Add(scopeToggle);

            var spacer = CreateToolbarSpacer();
            spacer.style.minWidth = 12;
            actions.Add(spacer);

            mHistoryCountLabel = new Label("0/300");
            mHistoryCountLabel.AddToClassList("yoki-fsm-timeline__count");
            actions.Add(mHistoryCountLabel);

            var clearBtn = CreateToolbarButton("清空", () =>
            {
                FsmDebugger.ClearHistory();
                UpdateTimelineSection();
            });
            clearBtn.tooltip = "清空当前保留的转换历史记录。";
            actions.Add(clearBtn);

            var (panel, body) = CreateKitSectionPanel(
                "转换历史",
                "记录状态进入、切换、停止等关键行为。",
                KitIcons.TIMELINE,
                actions);
            panel.name = "timeline-section";
            panel.AddToClassList("yoki-fsm-timeline");
            panel.AddToClassList("yoki-kit-panel--cyan");
            panel.AddToClassList("yoki-monitor-secondary-panel");
            panel.style.minHeight = 180;

            var scrollView = new ScrollView();
            scrollView.AddToClassList("yoki-fsm-timeline__list");
            body.Add(scrollView);

            mTimelineList = new VisualElement { name = "timeline-list" };
            scrollView.Add(mTimelineList);

            return panel;
        }

        /// <summary>
        /// 刷新转换历史列表。
        /// </summary>
        private void UpdateTimelineSection()
        {
            if (mTimelineList == null)
            {
                return;
            }

            mTimelineList.Clear();

            var history = FsmDebugger.TransitionHistory;
            mHistoryCountLabel.text = $"{history.Count}/{FsmDebugger.MAX_HISTORY_COUNT}";

            string filterName = mShowSelectedHistoryOnly ? mSelectedFsm?.Name : null;

            for (int i = history.Count - 1; i >= 0; i--)
            {
                var entry = history[i];
                if (filterName != null && entry.FsmName != filterName)
                {
                    continue;
                }

                mTimelineList.Add(CreateTimelineItem(entry));
            }

            if (mTimelineList.childCount == 0)
            {
                var emptyLabel = new Label(mShowSelectedHistoryOnly
                    ? "当前选中的状态机暂无可显示的转换记录。"
                    : "当前暂无可显示的状态转换记录。");
                emptyLabel.AddToClassList("yoki-fsm-empty__text");
                mTimelineList.Add(emptyLabel);
            }
        }

        /// <summary>
        /// 创建单条转换记录视图。
        /// </summary>
        private VisualElement CreateTimelineItem(FsmDebugger.TransitionEntry entry)
        {
            var item = new VisualElement();
            item.AddToClassList("yoki-transition-item");

            var timeLabel = new Label($"[{entry.Time:F2}s]");
            timeLabel.AddToClassList("yoki-transition-item__time");
            item.Add(timeLabel);

            string actionText = GetActionText(entry.Action);
            var actionBadge = new Label(actionText);
            actionBadge.AddToClassList("yoki-transition-item__action");
            actionBadge.AddToClassList(GetActionClass(entry.Action));
            item.Add(actionBadge);

            var transitionContainer = new VisualElement();
            transitionContainer.AddToClassList("yoki-transition-item__transition");
            item.Add(transitionContainer);

            if (!mShowSelectedHistoryOnly && !string.IsNullOrEmpty(entry.FsmName))
            {
                var fsmNameLabel = new Label($"{entry.FsmName}  ");
                fsmNameLabel.AddToClassList("yoki-transition-item__fsm-name");
                transitionContainer.Add(fsmNameLabel);
            }

            if (entry.Action == "Change")
            {
                var fromLabel = new Label(entry.FromState);
                fromLabel.AddToClassList("yoki-transition-item__from-state");
                transitionContainer.Add(fromLabel);

                var arrowLabel = new Label(" -> ");
                arrowLabel.AddToClassList("yoki-transition-item__arrow");
                transitionContainer.Add(arrowLabel);

                var toLabel = new Label(entry.ToState);
                toLabel.AddToClassList("yoki-transition-item__to-state");
                transitionContainer.Add(toLabel);
            }
            else
            {
                var stateText = !string.IsNullOrEmpty(entry.ToState) ? entry.ToState : entry.FromState;
                if (!string.IsNullOrEmpty(stateText))
                {
                    var stateLabel = new Label(stateText);
                    stateLabel.AddToClassList("yoki-transition-item__from-state");
                    transitionContainer.Add(stateLabel);
                }
            }

            return item;
        }

        /// <summary>
        /// 将内部动作名转换为界面文案。
        /// </summary>
        private static string GetActionText(string action) => action switch
        {
            "Start" => "启动",
            "Change" => "切换",
            "Stop" or "End" => "停止",
            "Add" => "添加",
            "Remove" => "移除",
            "Clear" => "清空",
            "Dispose" => "释放",
            _ => action
        };

        /// <summary>
        /// 获取动作对应的样式类名。
        /// </summary>
        private static string GetActionClass(string action) => action switch
        {
            "Start" => "yoki-transition-item__action--start",
            "Change" => "yoki-transition-item__action--change",
            "Stop" or "End" => "yoki-transition-item__action--stop",
            "Add" => "yoki-transition-item__action--add",
            "Remove" => "yoki-transition-item__action--remove",
            "Clear" => "yoki-transition-item__action--clear",
            "Dispose" => "yoki-transition-item__action--dispose",
            _ => "yoki-transition-item__action--change"
        };

        #endregion
    }
}
#endif
