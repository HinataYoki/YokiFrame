#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame
{
    /// <summary>
    /// EventKit 的运行时监控视图。
    /// 采用 Master-Detail 布局，左侧显示事件泳道，右侧显示详情与时间轴。
    /// </summary>
    public partial class EventKitToolPage
    {
        #region 运行时数据结构

        /// <summary>
        /// 运行时事件快照。
        /// </summary>
        private class EventInfo
        {
            public string EventType;
            public string EventKey;
            public string ParamType;
            public int ListenerCount;
            public int TriggerCount;
            public double LastTriggerTime;
            public IEasyEvent Event;
        }

        #endregion

        #region 运行时字段

        private readonly List<EventInfo> mEventInfos = new(64);

        private VisualElement mDetailPanel;
        private EventInfo mSelectedEvent;

        private Label mDetailTitle;
        private Label mDetailParamType;
        private Label mDetailTriggerCount;
        private Label mDetailListenerCount;
        private VisualElement mTimelineContainer;

        private readonly Dictionary<string, double> mTriggerTimeCache = new(64);
        private readonly Dictionary<string, int> mTriggerCountCache = new(64);
        private readonly Dictionary<string, List<TimelineEntry>> mTimelineHistoryCache = new(64);

        /// <summary>
        /// 时间轴记录项。
        /// </summary>
        private struct TimelineEntry
        {
            public float Time;
            public string Args;
        }

        private const int MAX_TIMELINE_ENTRIES = 50;

        #endregion

        #region 事件选择

        /// <summary>
        /// 从泳道列表中选中一个事件，并刷新右侧详情。
        /// </summary>
        private void SelectEvent(EventInfo info)
        {
            mSelectedEvent = info;
            UpdateDetailPanel();
            RefreshSwimlaneSelectionState();
        }

        /// <summary>
        /// 刷新泳道选中高亮状态。
        /// </summary>
        private void RefreshSwimlaneSelectionState()
        {
            string selectedKey = mSelectedEvent != null
                ? $"{mSelectedEvent.EventType}_{mSelectedEvent.EventKey}"
                : null;

            foreach (var kvp in mSwimlaneRows)
            {
                bool isSelected = selectedKey != null && kvp.Key == selectedKey;
                kvp.Value.style.backgroundColor = isSelected
                    ? new StyleColor(new Color(0.22f, 0.25f, 0.30f))
                    : new StyleColor(new Color(0.18f, 0.18f, 0.2f));
            }
        }

        #endregion

        #region 构建运行时视图

        /// <summary>
        /// 创建运行时监控主视图。
        /// </summary>
        private VisualElement CreateRuntimeView()
        {
            var container = new VisualElement();
            container.style.flexGrow = 1;
            container.style.flexDirection = FlexDirection.Column;

            if (mRuntimeStatusBanner != null)
            {
                mRuntimeStatusBanner.style.marginBottom = 10;
                container.Add(mRuntimeStatusBanner);
            }

            var workspace = new VisualElement();
            workspace.style.flexGrow = 1;
            workspace.style.flexDirection = FlexDirection.Row;
            container.Add(workspace);

            var swimlanePanel = CreateSwimlanePanel();
            swimlanePanel.style.flexGrow = 2;
            swimlanePanel.style.flexBasis = 0;
            swimlanePanel.style.minWidth = 400;
            workspace.Add(swimlanePanel);

            var divider = new VisualElement();
            divider.style.width = 1;
            divider.style.backgroundColor = new StyleColor(new Color(0.2f, 0.2f, 0.25f));
            workspace.Add(divider);

            mDetailPanel = CreateDetailPanel();
            mDetailPanel.style.flexGrow = 1;
            mDetailPanel.style.flexBasis = 0;
            mDetailPanel.style.minWidth = 300;
            workspace.Add(mDetailPanel);

            return container;
        }

        #endregion

        #region 数据刷新

        /// <summary>
        /// 刷新运行时视图，并尽量保留当前选中事件。
        /// </summary>
        private void RefreshRuntimeView()
        {
            if (!IsPlaying)
            {
                mSwimlaneContainer?.Clear();
                mSwimlaneRows.Clear();
                mEventHubs.Clear();
                mReceiverContainers.Clear();
                mEventInfos.Clear();
                mSelectedEvent = null;

                var countLabel = mSwimlaneContainer?.parent?.parent?.Q<Label>("swimlane-count");
                if (countLabel != null)
                {
                    countLabel.text = "未运行";
                }

                mSwimlaneContainer?.Add(CreateEmptyState("请先运行游戏以查看运行时事件"));
                UpdateDetailPanel();
                return;
            }

            string selectedKey = mSelectedEvent != null
                ? $"{mSelectedEvent.EventType}_{mSelectedEvent.EventKey}"
                : null;

            CollectEventInfos();

            if (selectedKey != null)
            {
                mSelectedEvent = mEventInfos.Find(info =>
                    $"{info.EventType}_{info.EventKey}" == selectedKey);
            }
            else
            {
                mSelectedEvent = null;
            }

            RebuildSwimlanes();
            UpdateDetailPanel();
        }

        #endregion
    }
}
#endif
