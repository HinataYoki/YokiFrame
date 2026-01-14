#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame
{
    /// <summary>
    /// EventKit 查看器 - 历史视图
    /// </summary>
    public partial class EventKitViewerWindow
    {
        #region 历史视图构建

        /// <summary>
        /// 构建历史视图
        /// </summary>
        private void BuildHistoryView()
        {
            mHistoryView = new VisualElement();
            mHistoryView.style.flexGrow = 1;
            mHistoryView.style.flexDirection = FlexDirection.Column;

            BuildHistoryToolbar();
            BuildHistoryList();
        }

        /// <summary>
        /// 构建历史工具栏
        /// </summary>
        private void BuildHistoryToolbar()
        {
            var toolbar = new VisualElement();
            toolbar.style.flexDirection = FlexDirection.Row;
            toolbar.style.height = 28;
            toolbar.style.backgroundColor = new StyleColor(new Color(0.2f, 0.2f, 0.2f));
            toolbar.style.paddingLeft = 8;
            toolbar.style.paddingRight = 8;
            toolbar.style.alignItems = Align.Center;
            toolbar.style.flexWrap = Wrap.Wrap;
            mHistoryView.Add(toolbar);

            // 操作过滤
            var actionLabel = new Label("操作:");
            actionLabel.style.marginRight = 4;
            actionLabel.style.color = new StyleColor(new Color(0.7f, 0.7f, 0.7f));
            toolbar.Add(actionLabel);

            mHistoryActionFilter = new DropdownField();
            mHistoryActionFilter.choices = new List<string> { "All", "Register", "UnRegister", "Send" };
            mHistoryActionFilter.value = mHistoryFilterAction;
            mHistoryActionFilter.style.width = 80;
            mHistoryActionFilter.style.marginRight = 12;
            mHistoryActionFilter.RegisterValueChangedCallback(evt =>
            {
                mHistoryFilterAction = evt.newValue;
                RefreshHistoryView();
            });
            toolbar.Add(mHistoryActionFilter);

            // 类型过滤
            var typeLabel = new Label("类型:");
            typeLabel.style.marginRight = 4;
            typeLabel.style.color = new StyleColor(new Color(0.7f, 0.7f, 0.7f));
            toolbar.Add(typeLabel);

            mHistoryTypeFilter = new DropdownField();
            mHistoryTypeFilter.choices = new List<string> { "All", "Enum", "Type", "String", "Listener" };
            mHistoryTypeFilter.value = mHistoryFilterType;
            mHistoryTypeFilter.style.width = 80;
            mHistoryTypeFilter.style.marginRight = 12;
            mHistoryTypeFilter.RegisterValueChangedCallback(evt =>
            {
                mHistoryFilterType = evt.newValue;
                RefreshHistoryView();
            });
            toolbar.Add(mHistoryTypeFilter);

            // 记录 Send 开关
            mRecordSendToggle = new Toggle("记录Send");
            mRecordSendToggle.value = EasyEventDebugger.RecordSendEvents;
            mRecordSendToggle.style.marginRight = 8;
            mRecordSendToggle.RegisterValueChangedCallback(evt => EasyEventDebugger.RecordSendEvents = evt.newValue);
            toolbar.Add(mRecordSendToggle);

            // 记录堆栈开关
            mRecordStackToggle = new Toggle("堆栈");
            mRecordStackToggle.value = EasyEventDebugger.RecordSendStackTrace;
            mRecordStackToggle.style.marginRight = 8;
            mRecordStackToggle.RegisterValueChangedCallback(evt => EasyEventDebugger.RecordSendStackTrace = evt.newValue);
            toolbar.Add(mRecordStackToggle);

            // 自动滚动开关
            mAutoScrollToggle = new Toggle("自动滚动");
            mAutoScrollToggle.value = mHistoryAutoScroll;
            mAutoScrollToggle.style.marginRight = 8;
            mAutoScrollToggle.RegisterValueChangedCallback(evt => mHistoryAutoScroll = evt.newValue);
            toolbar.Add(mAutoScrollToggle);

            // 停止时清空开关
            mClearOnStopToggle = new Toggle("停止时清空");
            mClearOnStopToggle.value = mClearHistoryOnStop;
            mClearOnStopToggle.style.marginRight = 8;
            mClearOnStopToggle.RegisterValueChangedCallback(evt =>
            {
                mClearHistoryOnStop = evt.newValue;
                EditorPrefs.SetBool("EventKitViewer_ClearHistoryOnStop", mClearHistoryOnStop);
            });
            toolbar.Add(mClearOnStopToggle);

            // 弹性空间
            toolbar.Add(new VisualElement { style = { flexGrow = 1 } });

            // 记录数量
            mHistoryCountLabel = new Label($"记录: 0/{EasyEventDebugger.MAX_HISTORY_COUNT}");
            mHistoryCountLabel.style.marginRight = 8;
            mHistoryCountLabel.style.color = new StyleColor(new Color(0.6f, 0.6f, 0.6f));
            toolbar.Add(mHistoryCountLabel);

            // 清空按钮
            var clearBtn = new Button(() =>
            {
                EasyEventDebugger.ClearHistory();
                RefreshHistoryView();
            }) { text = "清空" };
            clearBtn.style.height = 22;
            clearBtn.style.paddingLeft = 12;
            clearBtn.style.paddingRight = 12;
            toolbar.Add(clearBtn);
        }

        /// <summary>
        /// 构建历史列表
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
        /// 创建历史列表项
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

            // 时间
            var timeLabel = new Label();
            timeLabel.name = "time";
            timeLabel.style.width = 55;
            timeLabel.style.color = new StyleColor(new Color(0.6f, 0.6f, 0.6f));
            item.Add(timeLabel);

            // 操作类型标签
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

            // 事件类型标签
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

            // 事件键
            var keyLabel = new Label();
            keyLabel.name = "key";
            keyLabel.style.width = 180;
            keyLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            keyLabel.style.overflow = Overflow.Hidden;
            keyLabel.style.textOverflow = TextOverflow.Ellipsis;
            item.Add(keyLabel);

            // 参数
            var argsLabel = new Label();
            argsLabel.name = "args";
            argsLabel.style.width = 120;
            argsLabel.style.color = new StyleColor(new Color(0.6f, 0.6f, 0.6f));
            argsLabel.style.fontSize = 11;
            argsLabel.style.overflow = Overflow.Hidden;
            argsLabel.style.textOverflow = TextOverflow.Ellipsis;
            item.Add(argsLabel);

            // 调用位置
            var callerLabel = new Label();
            callerLabel.name = "caller";
            callerLabel.style.flexGrow = 1;
            callerLabel.style.color = new StyleColor(new Color(0.5f, 0.5f, 0.5f));
            callerLabel.style.fontSize = 11;
            callerLabel.style.overflow = Overflow.Hidden;
            callerLabel.style.textOverflow = TextOverflow.Ellipsis;
            item.Add(callerLabel);

            // 跳转按钮
            var jumpBtn = new Button { text = "跳转" };
            jumpBtn.name = "jump";
            jumpBtn.style.height = 20;
            jumpBtn.style.paddingLeft = 8;
            jumpBtn.style.paddingRight = 8;
            jumpBtn.style.display = DisplayStyle.None;
            item.Add(jumpBtn);

            return item;
        }

        /// <summary>
        /// 绑定历史列表项数据
        /// </summary>
        private void BindHistoryItem(VisualElement element, int index)
        {
            var history = EasyEventDebugger.EventHistory;
            // 倒序显示，最新的在前
            var realIndex = history.Count - 1 - index;
            if (realIndex < 0 || realIndex >= history.Count) return;

            var entry = history[realIndex];

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
            argsLabel.text = string.IsNullOrEmpty(entry.Args) ? "" : $"({entry.Args})";

            var callerLabel = element.Q<Label>("caller");
            var jumpBtn = element.Q<Button>("jump");

            if (!string.IsNullOrEmpty(entry.CallerInfo))
            {
                var shortPath = entry.CallerInfo;
                if (shortPath.Length > 35)
                    shortPath = "..." + shortPath[^32..];
                callerLabel.text = shortPath;

                jumpBtn.style.display = DisplayStyle.Flex;
                jumpBtn.clickable = new Clickable(() =>
                {
                    var parts = entry.CallerInfo.Split(':');
                    if (parts.Length == 2 && int.TryParse(parts[1], out var line))
                        OpenFileAtLine(parts[0], line);
                });
            }
            else
            {
                callerLabel.text = "";
                jumpBtn.style.display = DisplayStyle.None;
            }
        }

        #endregion

        #region 历史视图刷新

        /// <summary>
        /// 刷新历史视图
        /// </summary>
        private void RefreshHistoryView()
        {
            if (mHistoryListView == null) return;

            var history = EasyEventDebugger.EventHistory;

            // 过滤数据
            var filteredList = new List<EasyEventDebugger.EventHistoryEntry>(history.Count);
            foreach (var entry in history)
            {
                if (PassHistoryFilter(entry))
                    filteredList.Add(entry);
            }

            mHistoryCountLabel.text = $"记录: {history.Count}/{EasyEventDebugger.MAX_HISTORY_COUNT}";
            mHistoryListView.itemsSource = filteredList;
            mHistoryListView.RefreshItems();

            // 自动滚动到顶部（最新记录）
            if (mHistoryAutoScroll && filteredList.Count > 0)
            {
                mHistoryListView.ScrollToItem(0);
            }
        }

        /// <summary>
        /// 检查历史条目是否通过过滤
        /// </summary>
        private bool PassHistoryFilter(EasyEventDebugger.EventHistoryEntry entry)
        {
            if (mHistoryFilterAction != "All" && entry.Action != mHistoryFilterAction) return false;
            if (mHistoryFilterType != "All" && entry.EventType != mHistoryFilterType) return false;
            return true;
        }

        #endregion
    }
}
#endif
