#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using YokiFrame;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// PoolKit 事件日志区。
    /// </summary>
    public partial class PoolKitToolPage
    {
        #region Constants

        private const float EVENT_ITEM_HEIGHT = 24f;
        private const int EVENT_BADGE_WIDTH = 65;

        #endregion

        #region Fields

        private ListView mEventLogListView;
        private PoolEventType? mEventFilter;
        private readonly List<PoolEvent> mFilteredEvents = new(128);

        private Button mFilterAllBtn;
        private Button mFilterSpawnBtn;
        private Button mFilterReturnBtn;

        #endregion

        /// <summary>
        /// 构建事件日志面板。
        /// </summary>
        private VisualElement BuildEventLogSection()
        {
            var (panel, body) = CreateKitSectionPanel(
                "事件日志",
                "作为辅助证据，查看借出、归还和强制回收记录。",
                KitIcons.SCROLL,
                BuildEventLogActions());
            panel.AddToClassList("yoki-monitor-secondary-panel");
            panel.AddToClassList("yoki-kit-panel--cyan");
            panel.style.minHeight = 220;
            panel.style.marginTop = 10;
            body.style.minHeight = 160;

            mEventLogListView = new ListView
            {
                fixedItemHeight = EVENT_ITEM_HEIGHT,
                makeItem = MakeEventLogItem,
                bindItem = BindEventLogItem
            };
            mEventLogListView.style.flexGrow = 1;
            mEventLogListView.style.minHeight = 160;
            body.Add(mEventLogListView);

            return panel;
        }

        /// <summary>
        /// 构建事件日志操作区。
        /// </summary>
        private VisualElement BuildEventLogActions()
        {
            var filterGroup = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    marginRight = 8
                }
            };

            mFilterAllBtn = YokiFrameUIComponents.CreateFilterButton("全部", true, () => SetEventFilter(null));
            mFilterAllBtn.style.fontSize = 12;
            mFilterAllBtn.style.height = 22;
            filterGroup.Add(mFilterAllBtn);

            mFilterSpawnBtn = YokiFrameUIComponents.CreateFilterButton("借出", false, () => SetEventFilter(PoolEventType.Spawn));
            mFilterSpawnBtn.style.fontSize = 12;
            mFilterSpawnBtn.style.height = 22;
            filterGroup.Add(mFilterSpawnBtn);

            mFilterReturnBtn = YokiFrameUIComponents.CreateFilterButton("归还", false, () => SetEventFilter(PoolEventType.Return));
            mFilterReturnBtn.style.fontSize = 12;
            mFilterReturnBtn.style.height = 22;
            filterGroup.Add(mFilterReturnBtn);

            var clearBtn = new Button(OnClearEventLog)
            {
                text = "清空",
                style =
                {
                    fontSize = 12,
                    height = 22,
                    paddingLeft = 10,
                    paddingRight = 10,
                    paddingTop = 2,
                    paddingBottom = 2,
                    backgroundColor = new StyleColor(YokiFrameUIComponents.Colors.LayerCard),
                    borderTopLeftRadius = 3,
                    borderTopRightRadius = 3,
                    borderBottomLeftRadius = 3,
                    borderBottomRightRadius = 3
                }
            };

            var actions = new VisualElement();
            actions.style.flexDirection = FlexDirection.Row;
            actions.style.alignItems = Align.Center;
            actions.Add(filterGroup);
            actions.Add(clearBtn);

            return actions;
        }

        /// <summary>
        /// 设置当前事件过滤器。
        /// </summary>
        private void SetEventFilter(PoolEventType? filterType)
        {
            mEventFilter = filterType;

            YokiFrameUIComponents.SetFilterButtonActive(mFilterAllBtn, filterType == null);
            YokiFrameUIComponents.SetFilterButtonActive(mFilterSpawnBtn, filterType == PoolEventType.Spawn);
            YokiFrameUIComponents.SetFilterButtonActive(mFilterReturnBtn, filterType == PoolEventType.Return);

            UpdateEventLogList();
        }

        /// <summary>
        /// 创建事件日志列表项模板。
        /// </summary>
        private VisualElement MakeEventLogItem()
        {
            var item = new VisualElement { name = "event-row" };
            item.style.height = EVENT_ITEM_HEIGHT;
            item.style.flexDirection = FlexDirection.Row;
            item.style.alignItems = Align.Center;
            item.style.paddingLeft = item.style.paddingRight = 8;
            item.style.borderBottomWidth = 1;
            item.style.borderBottomColor = new StyleColor(new Color(0.15f, 0.15f, 0.17f));

            var timeLabel = new Label { name = "time" };
            timeLabel.style.fontSize = 9;
            timeLabel.style.width = 65;
            timeLabel.style.color = new StyleColor(YokiFrameUIComponents.Colors.TextTertiary);
            timeLabel.style.unityFontStyleAndWeight = FontStyle.Normal;
            item.Add(timeLabel);

            var badge = new Label { name = "badge" };
            badge.style.fontSize = 9;
            badge.style.width = EVENT_BADGE_WIDTH;
            badge.style.height = 16;
            badge.style.unityTextAlign = TextAnchor.MiddleCenter;
            badge.style.borderTopLeftRadius = badge.style.borderTopRightRadius =
                badge.style.borderBottomLeftRadius = badge.style.borderBottomRightRadius = 3;
            badge.style.marginRight = 8;
            item.Add(badge);

            var objLabel = new Label { name = "obj-name" };
            objLabel.style.fontSize = 10;
            objLabel.style.color = new StyleColor(YokiFrameUIComponents.Colors.TextPrimary);
            objLabel.style.flexGrow = 0.3f;
            objLabel.style.overflow = Overflow.Hidden;
            objLabel.style.textOverflow = TextOverflow.Ellipsis;
            item.Add(objLabel);

            var arrow = new Label { name = "arrow", text = "<-" };
            arrow.style.fontSize = 10;
            arrow.style.color = new StyleColor(YokiFrameUIComponents.Colors.TextTertiary);
            arrow.style.marginLeft = arrow.style.marginRight = 6;
            item.Add(arrow);

            var sourceLabel = new Label { name = "source" };
            sourceLabel.style.fontSize = 10;
            sourceLabel.style.color = new StyleColor(new Color(1f, 0.76f, 0.03f));
            sourceLabel.style.flexGrow = 0.7f;
            sourceLabel.style.overflow = Overflow.Hidden;
            sourceLabel.style.textOverflow = TextOverflow.Ellipsis;
            sourceLabel.pickingMode = PickingMode.Position;
            item.Add(sourceLabel);

            sourceLabel.RegisterCallback<ClickEvent>(_ =>
            {
                if (sourceLabel.userData is PoolEvent evt)
                {
                    CopyStackTrace(evt);
                }
            });

            return item;
        }

        /// <summary>
        /// 绑定事件日志列表项数据。
        /// </summary>
        private void BindEventLogItem(VisualElement element, int index)
        {
            if (index < 0 || index >= mFilteredEvents.Count)
            {
                return;
            }

            var evt = mFilteredEvents[index];

            var timeLabel = element.Q<Label>("time");
            timeLabel.text = evt.Timestamp.ToString("F1");

            var badge = element.Q<Label>("badge");
            if (evt.EventType == PoolEventType.Spawn)
            {
                badge.text = "借出";
                badge.style.backgroundColor = new StyleColor(new Color(0.22f, 0.37f, 0.24f));
                badge.style.color = new StyleColor(new Color(0.63f, 0.92f, 0.67f));
            }
            else if (evt.EventType == PoolEventType.Return)
            {
                badge.text = "归还";
                badge.style.backgroundColor = new StyleColor(new Color(0.24f, 0.28f, 0.42f));
                badge.style.color = new StyleColor(new Color(0.67f, 0.75f, 1f));
            }
            else
            {
                badge.text = "强制";
                badge.style.backgroundColor = new StyleColor(new Color(0.42f, 0.23f, 0.21f));
                badge.style.color = new StyleColor(new Color(1f, 0.74f, 0.68f));
            }

            element.Q<Label>("obj-name").text = evt.ObjectName;
            element.Q<Label>("source").text = evt.Source;
            element.Q<Label>("source").userData = evt;
        }

        /// <summary>
        /// 刷新事件日志列表。
        /// </summary>
        private void UpdateEventLogList()
        {
            if (mEventLogListView == null)
            {
                return;
            }

            mFilteredEvents.Clear();

            if (mSelectedPool != default)
            {
                var events = new List<PoolEvent>(128);
                PoolDebugger.GetEventHistory(events, mEventFilter, mSelectedPool.Name);
                for (int i = 0; i < events.Count; i++)
                {
                    mFilteredEvents.Add(events[i]);
                }
            }

            mEventLogListView.itemsSource = mFilteredEvents;
            mEventLogListView.RefreshItems();
        }

        /// <summary>
        /// 清空当前事件日志。
        /// </summary>
        private void OnClearEventLog()
        {
            if (mSelectedPool == default)
            {
                return;
            }

            PoolDebugger.ClearEventHistory();
            UpdateEventLogList();
        }

        /// <summary>
        /// 将事件堆栈复制到剪贴板。
        /// </summary>
        private static void CopyStackTrace(PoolEvent evt)
        {
            if (string.IsNullOrEmpty(evt.StackTrace))
            {
                EditorGUIUtility.systemCopyBuffer = "无堆栈信息";
                return;
            }

            EditorGUIUtility.systemCopyBuffer = evt.StackTrace;
        }
    }
}
#endif
