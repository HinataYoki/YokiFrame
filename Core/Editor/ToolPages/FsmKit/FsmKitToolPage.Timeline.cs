#if UNITY_EDITOR
using UnityEngine.UIElements;
using YokiFrame.EditorTools;

namespace YokiFrame
{
    /// <summary>
    /// FsmKit 工具页面 - 转换时间轴区域
    /// 显示状态转换历史，类似 EventKit 的时间轴
    /// 使用 USS 类消除内联样式
    /// </summary>
    public partial class FsmKitToolPage
    {
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
            var section = new VisualElement { name = "timeline-section" };
            section.AddToClassList("yoki-fsm-timeline");

            // 工具栏
            var toolbar = new VisualElement();
            toolbar.AddToClassList("yoki-fsm-timeline__toolbar");
            section.Add(toolbar);

            var titleIcon = new Image { image = KitIcons.GetTexture(KitIcons.TIMELINE) };
            titleIcon.AddToClassList("yoki-fsm-timeline__icon");
            toolbar.Add(titleIcon);

            var titleLabel = new Label("转换历史");
            titleLabel.AddToClassList("yoki-fsm-timeline__title");
            toolbar.Add(titleLabel);

            var recordToggle = CreateToolbarToggle("记录", FsmDebugger.RecordTransitions,
                v => FsmDebugger.RecordTransitions = v);
            toolbar.Add(recordToggle);

            toolbar.Add(YokiFrameUIComponents.CreateFlexSpacer());

            mHistoryCountLabel = new Label("0/300");
            mHistoryCountLabel.AddToClassList("yoki-fsm-timeline__count");
            toolbar.Add(mHistoryCountLabel);

            var clearBtn = CreateToolbarButton("清空", () =>
            {
                FsmDebugger.ClearHistory();
                UpdateTimelineSection();
            });
            toolbar.Add(clearBtn);

            // 历史列表（ScrollView）
            var scrollView = new ScrollView();
            scrollView.AddToClassList("yoki-fsm-timeline__list");
            section.Add(scrollView);

            mTimelineList = new VisualElement { name = "timeline-list" };
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
                var emptyLabel = new Label("暂无转换记录");
                emptyLabel.AddToClassList("yoki-fsm-empty__text");
                mTimelineList.Add(emptyLabel);
            }
        }

        /// <summary>
        /// 创建时间轴项
        /// </summary>
        private VisualElement CreateTimelineItem(FsmDebugger.TransitionEntry entry)
        {
            var item = new VisualElement();
            item.AddToClassList("yoki-transition-item");

            // 时间戳
            var timeLabel = new Label($"[{entry.Time:F2}s]");
            timeLabel.AddToClassList("yoki-transition-item__time");
            item.Add(timeLabel);

            // 动作类型徽章
            var actionText = GetActionText(entry.Action);
            var actionBadge = new Label(actionText);
            actionBadge.AddToClassList("yoki-transition-item__action");
            actionBadge.AddToClassList(GetActionClass(entry.Action));
            item.Add(actionBadge);

            // 转换信息
            var transitionContainer = new VisualElement();
            transitionContainer.AddToClassList("yoki-transition-item__transition");
            item.Add(transitionContainer);

            if (entry.Action == "Change")
            {
                // 状态转换：FromState → ToState
                var fromLabel = new Label(entry.FromState);
                fromLabel.AddToClassList("yoki-transition-item__from-state");
                transitionContainer.Add(fromLabel);

                var arrowLabel = new Label(" → ");
                arrowLabel.AddToClassList("yoki-transition-item__arrow");
                transitionContainer.Add(arrowLabel);

                var toLabel = new Label(entry.ToState);
                toLabel.AddToClassList("yoki-transition-item__to-state");
                transitionContainer.Add(toLabel);
            }
            else
            {
                // 其他动作：显示相关状态
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
        /// 获取动作显示文本
        /// </summary>
        private static string GetActionText(string action) => action switch
        {
            "Start" => "Start",
            "Change" => "Change",
            "Stop" or "End" => "Stop",
            "Add" => "Add",
            "Remove" => "Remove",
            "Clear" => "Clear",
            "Dispose" => "Dispose",
            _ => action
        };

        /// <summary>
        /// 获取动作对应的 USS 类名
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
