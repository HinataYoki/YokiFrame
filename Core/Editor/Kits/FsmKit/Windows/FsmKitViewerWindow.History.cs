#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using YokiFrame.EditorTools;

namespace YokiFrame
{
    /// <summary>
    /// FsmKit 查看器 - 历史视图。
    /// </summary>
    public partial class FsmKitViewerWindow
    {
        #region 历史视图构建

        /// <summary>
        /// 构建历史视图。
        /// </summary>
        private void BuildHistoryView()
        {
            mHistoryView = new VisualElement();
            mHistoryView.style.flexGrow = 1;
            mHistoryView.style.flexDirection = FlexDirection.Column;

            var headerTrailing = new VisualElement();
            headerTrailing.style.flexDirection = FlexDirection.Row;
            headerTrailing.style.alignItems = Align.Center;

            mHistoryCountLabel = new Label($"记录：0/{FsmDebugger.MAX_HISTORY_COUNT}");
            mHistoryCountLabel.style.marginRight = 8;
            mHistoryCountLabel.style.color = new StyleColor(new Color(0.6f, 0.6f, 0.6f));
            headerTrailing.Add(mHistoryCountLabel);

            var clearButton = new Button(() =>
            {
                FsmDebugger.ClearHistory();
                RefreshHistoryView();
            })
            {
                text = "清空"
            };
            clearButton.style.height = 22;
            clearButton.style.paddingLeft = 12;
            clearButton.style.paddingRight = 12;
            headerTrailing.Add(clearButton);

            mHistoryView.Add(CreateMonitorPanelHeader("状态切换历史", KitIcons.CLOCK, headerTrailing));

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

            var actionLabel = new Label("操作：");
            actionLabel.style.marginRight = 4;
            actionLabel.style.color = new StyleColor(new Color(0.7f, 0.7f, 0.7f));
            toolbar.Add(actionLabel);

            mHistoryActionFilter = new DropdownField();
            mHistoryActionFilter.choices = new List<string> { "All", "Start", "Change", "Add", "Remove", "Clear", "Dispose" };
            mHistoryActionFilter.value = mHistoryFilterAction;
            mHistoryActionFilter.style.width = 90;
            mHistoryActionFilter.style.marginRight = 12;
            mHistoryActionFilter.RegisterValueChangedCallback(evt =>
            {
                mHistoryFilterAction = evt.newValue;
                RefreshHistoryView();
            });
            toolbar.Add(mHistoryActionFilter);

            mRecordTransitionsToggle = new Toggle("记录切换");
            mRecordTransitionsToggle.value = FsmDebugger.RecordTransitions;
            mRecordTransitionsToggle.style.marginRight = 8;
            mRecordTransitionsToggle.RegisterValueChangedCallback(evt => FsmDebugger.RecordTransitions = evt.newValue);
            toolbar.Add(mRecordTransitionsToggle);

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
                EditorPrefs.SetBool("FsmKitViewer_ClearHistoryOnStop", mClearHistoryOnStop);
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
            actionBadge.style.width = 55;
            actionBadge.style.height = 18;
            actionBadge.style.unityTextAlign = TextAnchor.MiddleCenter;
            actionBadge.style.borderTopLeftRadius = 9;
            actionBadge.style.borderTopRightRadius = 9;
            actionBadge.style.borderBottomLeftRadius = 9;
            actionBadge.style.borderBottomRightRadius = 9;
            actionBadge.style.marginRight = 8;
            actionBadge.style.fontSize = 10;
            item.Add(actionBadge);

            var nameLabel = new Label();
            nameLabel.name = "name";
            nameLabel.style.width = 150;
            nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            nameLabel.style.overflow = Overflow.Hidden;
            nameLabel.style.textOverflow = TextOverflow.Ellipsis;
            item.Add(nameLabel);

            var transitionLabel = new Label();
            transitionLabel.name = "transition";
            transitionLabel.style.flexGrow = 1;
            transitionLabel.style.color = new StyleColor(new Color(0.7f, 0.7f, 0.7f));
            item.Add(transitionLabel);

            return item;
        }

        /// <summary>
        /// 绑定历史列表项数据。
        /// </summary>
        private void BindHistoryItem(VisualElement element, int index)
        {
            var filteredHistory = GetFilteredHistory();
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

            element.Q<Label>("name").text = entry.FsmName;

            var transitionLabel = element.Q<Label>("transition");
            if (entry.Action == "Change")
            {
                transitionLabel.text = $"{entry.FromState} -> {entry.ToState}";
            }
            else if (!string.IsNullOrEmpty(entry.ToState))
            {
                transitionLabel.text = entry.ToState;
            }
            else if (!string.IsNullOrEmpty(entry.FromState))
            {
                transitionLabel.text = entry.FromState;
            }
            else
            {
                transitionLabel.text = string.Empty;
            }
        }

        /// <summary>
        /// 获取操作类型颜色。
        /// </summary>
        private static Color GetActionColor(string action) => action switch
        {
            "Start" => new Color(0.4f, 0.8f, 0.4f),
            "Change" => new Color(0.4f, 0.7f, 0.9f),
            "Add" => new Color(0.6f, 0.9f, 0.6f),
            "Remove" => new Color(0.9f, 0.5f, 0.5f),
            "Clear" => new Color(0.9f, 0.7f, 0.3f),
            "Dispose" => new Color(0.6f, 0.6f, 0.6f),
            _ => new Color(0.5f, 0.5f, 0.5f)
        };

        #endregion

        #region 历史视图刷新

        /// <summary>
        /// 刷新历史视图。
        /// </summary>
        private void RefreshHistoryView()
        {
            if (mHistoryListView == null)
            {
                return;
            }

            var history = FsmDebugger.TransitionHistory;
            var filteredHistory = GetFilteredHistory();

            mHistoryCountLabel.text = $"记录：{history.Count}/{FsmDebugger.MAX_HISTORY_COUNT}";
            mHistoryListView.itemsSource = filteredHistory;
            mHistoryListView.RefreshItems();

            if (mHistoryAutoScroll && filteredHistory.Count > 0)
            {
                mHistoryListView.ScrollToItem(0);
            }
        }

        /// <summary>
        /// 获取过滤后的历史记录。
        /// </summary>
        private List<FsmDebugger.TransitionEntry> GetFilteredHistory()
        {
            var history = FsmDebugger.TransitionHistory;
            var filtered = new List<FsmDebugger.TransitionEntry>(history.Count);

            foreach (var entry in history)
            {
                if (mHistoryFilterAction == "All" || entry.Action == mHistoryFilterAction)
                {
                    filtered.Add(entry);
                }
            }

            return filtered;
        }

        #endregion
    }
}
#endif
