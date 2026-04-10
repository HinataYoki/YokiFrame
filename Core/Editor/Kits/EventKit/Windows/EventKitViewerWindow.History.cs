#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using YokiFrame.EditorTools;

namespace YokiFrame
{
    /// <summary>
    /// EventKit 旧版查看窗口的事件历史视图。
    /// </summary>
    public partial class EventKitViewerWindow
    {
        #region 历史视图构建

        /// <summary>
        /// 构建事件历史页。
        /// </summary>
        private void BuildHistoryView()
        {
            mHistoryView = new VisualElement();
            mHistoryView.style.flexGrow = 1;
            mHistoryView.style.flexDirection = FlexDirection.Column;

            var headerTrailing = new VisualElement();
            headerTrailing.style.flexDirection = FlexDirection.Row;
            headerTrailing.style.alignItems = Align.Center;

            mHistoryCountLabel = new Label($"记录: 0/{EasyEventDebugger.MAX_HISTORY_COUNT}");
            mHistoryCountLabel.style.marginRight = 8;
            mHistoryCountLabel.style.color = new StyleColor(new Color(0.6f, 0.6f, 0.6f));
            headerTrailing.Add(mHistoryCountLabel);

            var clearButton = new Button(() =>
            {
                EasyEventDebugger.ClearHistory();
                RefreshHistoryView();
            })
            {
                text = "清空"
            };
            clearButton.style.height = 22;
            clearButton.style.paddingLeft = 12;
            clearButton.style.paddingRight = 12;
            headerTrailing.Add(clearButton);

            mHistoryView.Add(CreateMonitorPanelHeader("事件历史", KitIcons.CLOCK, headerTrailing));

            BuildHistoryToolbar();
            BuildHistoryList();
        }

        /// <summary>
        /// 构建历史工具栏。
        /// </summary>
        private void BuildHistoryToolbar()
        {
            var toolbar = new VisualElement();
            toolbar.style.flexDirection = FlexDirection.Row;
            toolbar.style.minHeight = 28;
            toolbar.style.backgroundColor = new StyleColor(new Color(0.2f, 0.2f, 0.2f));
            toolbar.style.paddingLeft = 8;
            toolbar.style.paddingRight = 8;
            toolbar.style.paddingTop = 6;
            toolbar.style.paddingBottom = 6;
            toolbar.style.alignItems = Align.Center;
            toolbar.style.flexWrap = Wrap.Wrap;
            toolbar.style.borderBottomWidth = 1;
            toolbar.style.borderBottomColor = new StyleColor(new Color(0.15f, 0.15f, 0.15f));
            mHistoryView.Add(toolbar);

            var actionLabel = new Label("操作:");
            actionLabel.style.marginRight = 4;
            actionLabel.style.color = new StyleColor(new Color(0.7f, 0.7f, 0.7f));
            toolbar.Add(actionLabel);

            mHistoryActionFilter = new DropdownField();
            mHistoryActionFilter.choices = new List<string> { "All", "Register", "UnRegister", "Send" };
            mHistoryActionFilter.value = mHistoryFilterAction;
            mHistoryActionFilter.style.width = 90;
            mHistoryActionFilter.style.marginRight = 12;
            mHistoryActionFilter.RegisterValueChangedCallback(evt =>
            {
                mHistoryFilterAction = evt.newValue;
                RefreshHistoryView();
            });
            toolbar.Add(mHistoryActionFilter);

            var typeLabel = new Label("类型:");
            typeLabel.style.marginRight = 4;
            typeLabel.style.color = new StyleColor(new Color(0.7f, 0.7f, 0.7f));
            toolbar.Add(typeLabel);

            mHistoryTypeFilter = new DropdownField();
            mHistoryTypeFilter.choices = new List<string> { "All", "Enum", "Type", "String", "Listener" };
            mHistoryTypeFilter.value = mHistoryFilterType;
            mHistoryTypeFilter.style.width = 90;
            mHistoryTypeFilter.style.marginRight = 12;
            mHistoryTypeFilter.RegisterValueChangedCallback(evt =>
            {
                mHistoryFilterType = evt.newValue;
                RefreshHistoryView();
            });
            toolbar.Add(mHistoryTypeFilter);

            mRecordSendToggle = new Toggle("记录 Send");
            mRecordSendToggle.value = EasyEventDebugger.RecordSendEvents;
            mRecordSendToggle.style.marginRight = 8;
            mRecordSendToggle.RegisterValueChangedCallback(evt => EasyEventDebugger.RecordSendEvents = evt.newValue);
            toolbar.Add(mRecordSendToggle);

            mRecordStackToggle = new Toggle("记录堆栈");
            mRecordStackToggle.value = EasyEventDebugger.RecordSendStackTrace;
            mRecordStackToggle.style.marginRight = 8;
            mRecordStackToggle.RegisterValueChangedCallback(evt => EasyEventDebugger.RecordSendStackTrace = evt.newValue);
            toolbar.Add(mRecordStackToggle);

            mAutoScrollToggle = new Toggle("自动滚动");
            mAutoScrollToggle.value = mHistoryAutoScroll;
            mAutoScrollToggle.style.marginRight = 8;
            mAutoScrollToggle.RegisterValueChangedCallback(evt => mHistoryAutoScroll = evt.newValue);
            toolbar.Add(mAutoScrollToggle);

            mClearOnStopToggle = new Toggle("停止时清空");
            mClearOnStopToggle.value = mClearHistoryOnStop;
            mClearOnStopToggle.style.marginRight = 8;
            mClearOnStopToggle.RegisterValueChangedCallback(evt =>
            {
                mClearHistoryOnStop = evt.newValue;
                EditorPrefs.SetBool("EventKitViewer_ClearHistoryOnStop", mClearHistoryOnStop);
            });
            toolbar.Add(mClearOnStopToggle);
        }

        /// <summary>
        /// 构建历史列表。
        /// </summary>
        private void BuildHistoryList()
        {
            var container = new VisualElement();
            container.style.flexGrow = 1;
            container.style.paddingTop = 8;
            container.style.paddingBottom = 8;
            container.style.paddingLeft = 8;
            container.style.paddingRight = 8;
            mHistoryView.Add(container);

            mHistoryListView = new ListView();
            mHistoryListView.fixedItemHeight = 32;
            mHistoryListView.makeItem = MakeHistoryItem;
            mHistoryListView.bindItem = BindHistoryItem;
            mHistoryListView.style.flexGrow = 1;
            mHistoryListView.style.backgroundColor = new StyleColor(new Color(0.2f, 0.2f, 0.2f));
            mHistoryListView.style.borderTopLeftRadius = 4;
            mHistoryListView.style.borderTopRightRadius = 4;
            mHistoryListView.style.borderBottomLeftRadius = 4;
            mHistoryListView.style.borderBottomRightRadius = 4;
            container.Add(mHistoryListView);
        }

        #endregion

        #region 历史列表项

        /// <summary>
        /// 创建历史列表项。
        /// </summary>
        private VisualElement MakeHistoryItem()
        {
            var item = new VisualElement();
            item.style.flexDirection = FlexDirection.Row;
            item.style.alignItems = Align.Center;
            item.style.height = 32;
            item.style.paddingLeft = 12;
            item.style.paddingRight = 12;
            item.style.borderBottomWidth = 1;
            item.style.borderBottomColor = new StyleColor(new Color(0.15f, 0.15f, 0.15f));

            var timeLabel = new Label();
            timeLabel.name = "time";
            timeLabel.style.width = 55;
            timeLabel.style.color = new StyleColor(new Color(0.6f, 0.6f, 0.6f));
            item.Add(timeLabel);

            var actionBadge = new Label();
            actionBadge.name = "action";
            actionBadge.style.width = 70;
            actionBadge.style.height = 18;
            actionBadge.style.unityTextAlign = TextAnchor.MiddleCenter;
            actionBadge.style.borderTopLeftRadius = 9;
            actionBadge.style.borderTopRightRadius = 9;
            actionBadge.style.borderBottomLeftRadius = 9;
            actionBadge.style.borderBottomRightRadius = 9;
            actionBadge.style.marginRight = 4;
            actionBadge.style.fontSize = 10;
            item.Add(actionBadge);

            var typeBadge = new Label();
            typeBadge.name = "type";
            typeBadge.style.width = 50;
            typeBadge.style.height = 18;
            typeBadge.style.unityTextAlign = TextAnchor.MiddleCenter;
            typeBadge.style.borderTopLeftRadius = 9;
            typeBadge.style.borderTopRightRadius = 9;
            typeBadge.style.borderBottomLeftRadius = 9;
            typeBadge.style.borderBottomRightRadius = 9;
            typeBadge.style.marginRight = 8;
            typeBadge.style.fontSize = 10;
            item.Add(typeBadge);

            var keyLabel = new Label();
            keyLabel.name = "key";
            keyLabel.style.width = 180;
            keyLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            keyLabel.style.overflow = Overflow.Hidden;
            keyLabel.style.textOverflow = TextOverflow.Ellipsis;
            item.Add(keyLabel);

            var argsLabel = new Label();
            argsLabel.name = "args";
            argsLabel.style.width = 120;
            argsLabel.style.color = new StyleColor(new Color(0.6f, 0.6f, 0.6f));
            argsLabel.style.fontSize = 11;
            argsLabel.style.overflow = Overflow.Hidden;
            argsLabel.style.textOverflow = TextOverflow.Ellipsis;
            item.Add(argsLabel);

            var callerLabel = new Label();
            callerLabel.name = "caller";
            callerLabel.style.flexGrow = 1;
            callerLabel.style.color = new StyleColor(new Color(0.5f, 0.5f, 0.5f));
            callerLabel.style.fontSize = 11;
            callerLabel.style.overflow = Overflow.Hidden;
            callerLabel.style.textOverflow = TextOverflow.Ellipsis;
            item.Add(callerLabel);

            var jumpButton = new Button { text = "跳转" };
            jumpButton.name = "jump";
            jumpButton.style.height = 20;
            jumpButton.style.paddingLeft = 8;
            jumpButton.style.paddingRight = 8;
            jumpButton.style.display = DisplayStyle.None;
            item.Add(jumpButton);

            return item;
        }

        /// <summary>
        /// 绑定历史列表项。
        /// </summary>
        private void BindHistoryItem(VisualElement element, int index)
        {
            if (mHistoryListView?.itemsSource is not List<EasyEventDebugger.EventHistoryEntry> filteredHistory)
            {
                return;
            }

            int realIndex = filteredHistory.Count - 1 - index;
            if (realIndex < 0 || realIndex >= filteredHistory.Count)
            {
                return;
            }

            var entry = filteredHistory[realIndex];
            element.Q<Label>("time").text = $"{entry.Time:F2}s";

            var actionBadge = element.Q<Label>("action");
            actionBadge.text = entry.Action;
            actionBadge.style.backgroundColor = new StyleColor(GetActionColor(entry.Action));
            actionBadge.style.color = new StyleColor(Color.white);

            var typeBadge = element.Q<Label>("type");
            typeBadge.text = entry.EventType;
            typeBadge.style.backgroundColor = new StyleColor(GetEventTypeColor(entry.EventType));
            typeBadge.style.color = new StyleColor(Color.white);

            element.Q<Label>("key").text = entry.EventKey;

            var argsLabel = element.Q<Label>("args");
            argsLabel.text = string.IsNullOrEmpty(entry.Args) ? string.Empty : $"({entry.Args})";

            var callerLabel = element.Q<Label>("caller");
            var jumpButton = element.Q<Button>("jump");

            if (!string.IsNullOrEmpty(entry.CallerInfo))
            {
                var shortPath = entry.CallerInfo;
                if (shortPath.Length > 35)
                {
                    shortPath = "..." + shortPath[^32..];
                }

                callerLabel.text = shortPath;
                jumpButton.style.display = DisplayStyle.Flex;
                jumpButton.clickable = new Clickable(() =>
                {
                    var parts = entry.CallerInfo.Split(':');
                    if (parts.Length == 2 && int.TryParse(parts[1], out var line))
                    {
                        OpenFileAtLine(parts[0], line);
                    }
                });
            }
            else
            {
                callerLabel.text = string.Empty;
                jumpButton.style.display = DisplayStyle.None;
            }
        }

        #endregion

        #region 历史刷新

        /// <summary>
        /// 刷新事件历史视图。
        /// </summary>
        private void RefreshHistoryView()
        {
            if (mHistoryListView == null)
            {
                return;
            }

            var history = EasyEventDebugger.EventHistory;
            var filteredList = new List<EasyEventDebugger.EventHistoryEntry>(history.Count);
            foreach (var entry in history)
            {
                if (PassHistoryFilter(entry))
                {
                    filteredList.Add(entry);
                }
            }

            mHistoryCountLabel.text = $"记录: {history.Count}/{EasyEventDebugger.MAX_HISTORY_COUNT}";
            mHistoryListView.itemsSource = filteredList;
            mHistoryListView.RefreshItems();

            if (mHistoryAutoScroll && filteredList.Count > 0)
            {
                mHistoryListView.ScrollToItem(0);
            }
        }

        /// <summary>
        /// 判断事件历史条目是否通过筛选。
        /// </summary>
        private bool PassHistoryFilter(EasyEventDebugger.EventHistoryEntry entry)
        {
            if (mHistoryFilterAction != "All" && entry.Action != mHistoryFilterAction)
            {
                return false;
            }

            if (mHistoryFilterType != "All" && entry.EventType != mHistoryFilterType)
            {
                return false;
            }

            return true;
        }

        #endregion
    }
}
#endif
