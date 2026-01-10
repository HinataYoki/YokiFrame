#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using YokiFrame;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// PoolKitToolPage - 事件日志面板
    /// 显示借出/归还的历史记录流
    /// </summary>
    public partial class PoolKitToolPage
    {
        #region 常量

        private const float EVENT_ITEM_HEIGHT = 24f;        // 事件项高度
        private const int EVENT_BADGE_WIDTH = 65;           // 徽章宽度

        #endregion

        #region 字段

        private ListView mEventLogListView;
        private PoolEventType? mEventFilter;                // 事件过滤器（null = 全部）
        private readonly List<PoolEvent> mFilteredEvents = new(128);
        
        // 过滤按钮引用
        private Button mFilterAllBtn;
        private Button mFilterSpawnBtn;
        private Button mFilterReturnBtn;

        #endregion

        /// <summary>
        /// 构建事件日志区域
        /// </summary>
        private VisualElement BuildEventLogSection()
        {
            var section = new VisualElement();
            section.style.flexGrow = 1;
            section.style.flexDirection = FlexDirection.Column;
            section.style.minHeight = 120;

            // 工具栏
            var toolbar = BuildEventLogToolbar();
            section.Add(toolbar);

            // 列表
            mEventLogListView = new ListView
            {
                fixedItemHeight = EVENT_ITEM_HEIGHT,
                makeItem = MakeEventLogItem,
                bindItem = BindEventLogItem
            };
            mEventLogListView.style.flexGrow = 1;
            section.Add(mEventLogListView);

            return section;
        }

        /// <summary>
        /// 构建事件日志工具栏
        /// </summary>
        private VisualElement BuildEventLogToolbar()
        {
            var toolbar = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    height = 34,
                    paddingLeft = 8,
                    paddingRight = 8,
                    backgroundColor = new StyleColor(new Color(0.13f, 0.13f, 0.15f)),
                    borderTopWidth = 1,
                    borderTopColor = new StyleColor(YokiFrameUIComponents.Colors.BorderLight)
                }
            };

            // 标题
            var title = new Label("事件日志")
            {
                style =
                {
                    fontSize = 13,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    color = new StyleColor(YokiFrameUIComponents.Colors.TextPrimary),
                    flexGrow = 1
                }
            };
            toolbar.Add(title);

            // 过滤按钮组
            var filterGroup = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    marginRight = 8
                }
            };
            toolbar.Add(filterGroup);

            // 全部
            mFilterAllBtn = YokiFrameUIComponents.CreateFilterButton("全部", true, () => SetEventFilter(null));
            mFilterAllBtn.style.fontSize = 12;
            mFilterAllBtn.style.height = 22;
            filterGroup.Add(mFilterAllBtn);

            // 借出
            mFilterSpawnBtn = YokiFrameUIComponents.CreateFilterButton("借出", false, () => SetEventFilter(PoolEventType.Spawn));
            mFilterSpawnBtn.style.fontSize = 12;
            mFilterSpawnBtn.style.height = 22;
            filterGroup.Add(mFilterSpawnBtn);

            // 归还
            mFilterReturnBtn = YokiFrameUIComponents.CreateFilterButton("归还", false, () => SetEventFilter(PoolEventType.Return));
            mFilterReturnBtn.style.fontSize = 12;
            mFilterReturnBtn.style.height = 22;
            filterGroup.Add(mFilterReturnBtn);

            // 清空按钮
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
            toolbar.Add(clearBtn);

            return toolbar;
        }

        /// <summary>
        /// 设置事件过滤器
        /// </summary>
        private void SetEventFilter(PoolEventType? filterType)
        {
            mEventFilter = filterType;
            
            // 更新按钮样式
            YokiFrameUIComponents.SetFilterButtonActive(mFilterAllBtn, filterType == null);
            YokiFrameUIComponents.SetFilterButtonActive(mFilterSpawnBtn, filterType == PoolEventType.Spawn);
            YokiFrameUIComponents.SetFilterButtonActive(mFilterReturnBtn, filterType == PoolEventType.Return);
            
            UpdateEventLogList();
        }

        /// <summary>
        /// 创建事件日志项模板
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

            // 时间戳列
            var timeLabel = new Label { name = "time" };
            timeLabel.style.fontSize = 9;
            timeLabel.style.width = 65;
            timeLabel.style.color = new StyleColor(YokiFrameUIComponents.Colors.TextTertiary);
            timeLabel.style.unityFontStyleAndWeight = FontStyle.Normal;
            item.Add(timeLabel);

            // 事件类型徽章
            var badge = new Label { name = "badge" };
            badge.style.fontSize = 9;
            badge.style.width = EVENT_BADGE_WIDTH;
            badge.style.height = 16;
            badge.style.unityTextAlign = TextAnchor.MiddleCenter;
            badge.style.borderTopLeftRadius = badge.style.borderTopRightRadius =
                badge.style.borderBottomLeftRadius = badge.style.borderBottomRightRadius = 3;
            badge.style.marginRight = 8;
            item.Add(badge);

            // 对象名列
            var objLabel = new Label { name = "obj-name" };
            objLabel.style.fontSize = 10;
            objLabel.style.color = new StyleColor(YokiFrameUIComponents.Colors.TextPrimary);
            objLabel.style.flexGrow = 0.3f;
            objLabel.style.overflow = Overflow.Hidden;
            objLabel.style.textOverflow = TextOverflow.Ellipsis;
            item.Add(objLabel);

            // 箭头
            var arrow = new Label { name = "arrow", text = "<-" };
            arrow.style.fontSize = 10;
            arrow.style.color = new StyleColor(YokiFrameUIComponents.Colors.TextTertiary);
            arrow.style.marginLeft = arrow.style.marginRight = 6;
            item.Add(arrow);

            // 来源列（可点击查看详细堆栈）
            var sourceLabel = new Label { name = "source" };
            sourceLabel.style.fontSize = 10;
            sourceLabel.style.color = new StyleColor(new Color(1f, 0.76f, 0.03f)); // 琥珀色
            sourceLabel.style.flexGrow = 0.7f;
            sourceLabel.style.overflow = Overflow.Hidden;
            sourceLabel.style.textOverflow = TextOverflow.Ellipsis;
            sourceLabel.pickingMode = PickingMode.Position; // 启用点击
            item.Add(sourceLabel);

            return item;
        }

        /// <summary>
        /// 绑定事件日志项数据
        /// </summary>
        private void BindEventLogItem(VisualElement element, int index)
        {
            if (index >= mFilteredEvents.Count) return;

            var evt = mFilteredEvents[index];

            // 斑马纹背景
            element.style.backgroundColor = new StyleColor(YokiFrameUIComponents.GetZebraRowColor(index));

            // 时间戳
            var timeLabel = element.Q<Label>("time");
            var timeSpan = System.TimeSpan.FromSeconds(evt.Timestamp);
            timeLabel.text = $"[{timeSpan:mm\\:ss\\.f}]";

            // 事件类型徽章
            var badge = element.Q<Label>("badge");
            switch (evt.EventType)
            {
                case PoolEventType.Spawn:
                    badge.text = "SPAWN  >";
                    badge.style.backgroundColor = new StyleColor(new Color(0.20f, 0.45f, 0.22f));
                    badge.style.color = new StyleColor(new Color(0.6f, 0.9f, 0.6f));
                    break;
                case PoolEventType.Return:
                    badge.text = "< RETURN";
                    badge.style.backgroundColor = new StyleColor(new Color(0.25f, 0.25f, 0.28f));
                    badge.style.color = new StyleColor(YokiFrameUIComponents.Colors.TextSecondary);
                    break;
                case PoolEventType.Forced:
                    badge.text = "! FORCED";
                    badge.style.backgroundColor = new StyleColor(new Color(0.50f, 0.35f, 0.10f));
                    badge.style.color = new StyleColor(YokiFrameUIComponents.Colors.BrandWarning);
                    break;
            }

            // 对象名
            element.Q<Label>("obj-name").text = evt.ObjectName;

            // 来源（点击复制堆栈）
            var sourceLabel = element.Q<Label>("source");
            sourceLabel.text = evt.Source;
            sourceLabel.tooltip = "点击复制完整堆栈到剪贴板";
            sourceLabel.userData = evt;
            sourceLabel.UnregisterCallback<ClickEvent>(OnEventSourceClicked);
            sourceLabel.RegisterCallback<ClickEvent>(OnEventSourceClicked);
        }

        /// <summary>
        /// 事件来源点击事件 - 复制堆栈到剪贴板
        /// </summary>
        private void OnEventSourceClicked(ClickEvent clickEvt)
        {
            if (clickEvt.target is not Label label) return;
            if (label.userData is not PoolEvent evt) return;

            if (string.IsNullOrEmpty(evt.StackTrace)) return;

            // 复制堆栈到剪贴板
            EditorGUIUtility.systemCopyBuffer = evt.StackTrace;
            Debug.Log($"[PoolKit] 已复制 {evt.ObjectName} 的堆栈追踪到剪贴板");
        }

        /// <summary>
        /// 更新事件日志列表（仅显示当前选中池的事件）
        /// </summary>
        private void UpdateEventLogList()
        {
            if (mEventLogListView == default) return;

            // 仅在运行时获取事件历史
            if (IsPlaying && mSelectedPool != default)
            {
                PoolDebugger.GetEventHistory(mFilteredEvents, mEventFilter, mSelectedPool.Name);
            }
            else
            {
                mFilteredEvents.Clear();
            }

            mEventLogListView.itemsSource = mFilteredEvents;
            mEventLogListView.RefreshItems();
        }

        /// <summary>
        /// 清空事件日志
        /// </summary>
        private void OnClearEventLog()
        {
            PoolDebugger.ClearEventHistory();
            UpdateEventLogList();
        }
    }
}
#endif
