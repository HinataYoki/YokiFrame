#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame
{
    /// <summary>
    /// EventKit 工具页面 - 运行时监控视图
    /// 设计目标：聚焦动态变化与热度，让用户一眼识别"当前谁在活跃"
    /// 布局模式：Master-Detail（主从布局）—— 左侧列表，右侧详情
    /// </summary>
    public partial class EventKitToolPage
    {
        #region 运行时数据结构

        /// <summary>
        /// 事件信息缓存
        /// </summary>
        private class EventInfo
        {
            public string EventType;      // Enum/Type/String
            public string EventKey;       // 事件键名
            public string ParamType;      // 参数类型
            public int ListenerCount;     // 监听者数量
            public int TriggerCount;      // 累计触发次数
            public double LastTriggerTime; // 最后触发时间
            public IEasyEvent Event;      // 事件引用
        }

        #endregion

        #region 运行时私有字段

        // 事件数据
        private readonly List<EventInfo> mEventInfos = new(64);

        // 右侧详情
        private VisualElement mDetailPanel;
        private EventInfo mSelectedEvent;

        // 详情面板元素
        private Label mDetailTitle;
        private Label mDetailParamType;
        private Label mDetailTriggerCount;
        private Label mDetailListenerCount;
        private VisualElement mTimelineContainer;

        // 触发时间缓存
        private readonly Dictionary<string, double> mTriggerTimeCache = new(64);
        
        // 触发次数缓存（跨刷新保持）
        private readonly Dictionary<string, int> mTriggerCountCache = new(64);
        
        // 时间轴历史缓存（每个事件独立保存）
        private readonly Dictionary<string, List<TimelineEntry>> mTimelineHistoryCache = new(64);
        
        /// <summary>
        /// 时间轴条目数据
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
        /// 选中事件（点击泳道行时调用）
        /// </summary>
        private void SelectEvent(EventInfo info)
        {
            mSelectedEvent = info;
            UpdateDetailPanel();
            
            // 高亮选中行
            foreach (var kvp in mSwimlaneRows)
            {
                var isSelected = kvp.Key == $"{info.EventType}_{info.EventKey}";
                kvp.Value.style.backgroundColor = isSelected
                    ? new StyleColor(new Color(0.22f, 0.25f, 0.30f))
                    : new StyleColor(new Color(0.18f, 0.18f, 0.2f));
            }
        }

        #endregion

        #region 创建运行时视图

        /// <summary>
        /// 创建运行时监控视图（动态泳道布局）
        /// 左侧 70%：动态泳道可视化（发送者 -> 事件中心 -> 接收者）
        /// 右侧 30%：详情面板（时间轴 + 统计）
        /// </summary>
        private VisualElement CreateRuntimeView()
        {
            var container = new VisualElement();
            container.style.flexGrow = 1;
            container.style.flexDirection = FlexDirection.Row;

            // 左侧：动态泳道面板（约 66%）
            var swimlanePanel = CreateSwimlanePanel();
            swimlanePanel.style.flexGrow = 2;
            swimlanePanel.style.flexBasis = 0;
            swimlanePanel.style.minWidth = 400;
            container.Add(swimlanePanel);

            // 分隔线
            var divider = new VisualElement();
            divider.style.width = 1;
            divider.style.backgroundColor = new StyleColor(new Color(0.2f, 0.2f, 0.25f));
            container.Add(divider);

            // 右侧：详情面板（约 33%，2:1 比例）
            mDetailPanel = CreateDetailPanel();
            mDetailPanel.style.flexGrow = 1;
            mDetailPanel.style.flexBasis = 0;
            mDetailPanel.style.minWidth = 300;
            container.Add(mDetailPanel);

            return container;
        }

        #endregion

        #region 数据刷新

        /// <summary>
        /// 刷新运行时视图
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
                
                var countLabel = mSwimlaneContainer?.parent?.parent?.Q<Label>("swimlane-count");
                if (countLabel != null)
                    countLabel.text = "未运行";
                    
                mSwimlaneContainer?.Add(CreateEmptyState("请先运行游戏以查看运行时事件"));
                return;
            }

            // 保存当前选中事件的 Key
            var selectedKey = mSelectedEvent != null 
                ? $"{mSelectedEvent.EventType}_{mSelectedEvent.EventKey}" 
                : null;

            CollectEventInfos();
            
            // 恢复选中事件的引用
            if (selectedKey != null)
            {
                mSelectedEvent = mEventInfos.Find(info => 
                    $"{info.EventType}_{info.EventKey}" == selectedKey);
            }
            
            RebuildSwimlanes();
            UpdateDetailPanel();
        }

        #endregion
    }
}
#endif
