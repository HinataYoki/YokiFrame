using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine.UIElements;
using YokiFrame.EditorTools;

namespace YokiFrame
{
    /// <summary>
    /// FsmKit 工具页面 - UI Toolkit 版本
    /// </summary>
    public class FsmKitToolPage : YokiFrameToolPageBase
    {
        public override string PageName => "FsmKit";
        public override string PageIcon => KitIcons.FSMKIT;
        public override int Priority => 20;

        private const float REFRESH_INTERVAL = 0.2f;

        private double mLastRefreshTime;

        // UI 元素引用
        private ListView mFsmListView;
        private VisualElement mDetailPanel;
        private VisualElement mHistoryPanel;
        private Label mHistoryCountLabel;

        // 数据缓存
        private readonly List<IFSM> mCachedFsms = new(16);
        private IFSM mSelectedFsm;

        protected override void BuildUI(VisualElement root)
        {
            // 工具栏
            var toolbar = CreateToolbar();
            root.Add(toolbar);
            
            var helpLabel = new Label("运行时状态机监控（需要运行游戏）");
            helpLabel.AddToClassList("toolbar-label");
            toolbar.Add(helpLabel);
            
            toolbar.Add(new VisualElement { style = { flexGrow = 1 } });
            
            // 内容区域
            var content = new VisualElement();
            content.AddToClassList("content-area");
            root.Add(content);
            
            // 分割面板
            var splitView = CreateSplitView(250f);
            content.Add(splitView);
            
            // 左侧：FSM 列表
            var leftPanel = new VisualElement();
            leftPanel.AddToClassList("left-panel");
            splitView.Add(leftPanel);
            
            var leftHeader = CreatePanelHeader("活跃状态机");
            leftPanel.Add(leftHeader);
            
            mFsmListView = new ListView();
            mFsmListView.fixedItemHeight = 32;
            mFsmListView.makeItem = () =>
            {
                var item = new VisualElement();
                item.AddToClassList("list-item");
                item.style.height = 32;
                item.style.paddingTop = 4;
                item.style.paddingBottom = 4;
                
                var indicator = new VisualElement();
                indicator.AddToClassList("list-item-indicator");
                item.Add(indicator);
                
                var label = new Label();
                label.AddToClassList("list-item-label");
                item.Add(label);
                
                var count = new Label();
                count.AddToClassList("list-item-count");
                item.Add(count);
                
                return item;
            };
            mFsmListView.bindItem = (element, index) =>
            {
                var fsm = mCachedFsms[index];
                var indicator = element.Q<VisualElement>(className: "list-item-indicator");
                var label = element.Q<Label>(className: "list-item-label");
                var count = element.Q<Label>(className: "list-item-count");
                
                indicator.RemoveFromClassList("active");
                indicator.RemoveFromClassList("inactive");
                indicator.AddToClassList(fsm.MachineState == MachineState.Running ? "active" : "inactive");
                
                label.text = fsm.Name;
                count.text = $"[{fsm.GetAllStates().Count}]";
            };
            mFsmListView.selectionChanged += OnFsmSelected;
            mFsmListView.style.flexGrow = 1;
            leftPanel.Add(mFsmListView);
            
            // 右侧：详情面板 + 历史面板
            var rightPanel = new VisualElement();
            rightPanel.AddToClassList("right-panel");
            rightPanel.style.flexDirection = FlexDirection.Column;
            splitView.Add(rightPanel);
            
            // 上半部分：状态机详情
            mDetailPanel = new VisualElement();
            mDetailPanel.style.flexGrow = 1;
            mDetailPanel.style.minHeight = 200;
            rightPanel.Add(mDetailPanel);
            
            // 下半部分：转换历史
            mHistoryPanel = CreateHistoryPanel();
            rightPanel.Add(mHistoryPanel);
            
            UpdateDetailPanel();
        }

        private void OnFsmSelected(IEnumerable<object> selection)
        {
            foreach (var item in selection)
            {
                if (item is IFSM fsm)
                {
                    mSelectedFsm = fsm;
                    UpdateDetailPanel();
                    return;
                }
            }
        }

        private void UpdateDetailPanel()
        {
            mDetailPanel.Clear();
            
            if (mSelectedFsm == null)
            {
                var header = CreatePanelHeader("状态机详情");
                mDetailPanel.Add(header);
                mDetailPanel.Add(CreateHelpBox("选择左侧状态机查看详情"));
                return;
            }
            
            var fsm = mSelectedFsm;
            
            var headerWithName = CreatePanelHeader($"状态机: {fsm.Name}");
            mDetailPanel.Add(headerWithName);
            
            // 基本信息
            var infoBox = new VisualElement();
            infoBox.AddToClassList("info-box");
            mDetailPanel.Add(infoBox);
            
            AddInfoRow(infoBox, "枚举类型:", fsm.EnumType.Name);
            AddInfoRow(infoBox, "机器状态:", fsm.MachineState.ToString());
            
            var currentStateName = fsm.CurrentStateId >= 0 
                ? Enum.GetName(fsm.EnumType, fsm.CurrentStateId) ?? fsm.CurrentStateId.ToString()
                : "None";
            AddInfoRow(infoBox, "当前状态:", currentStateName, true);
            
            // 状态列表
            var statesHeader = CreatePanelHeader("注册状态");
            mDetailPanel.Add(statesHeader);
            
            var scrollView = new ScrollView();
            scrollView.style.flexGrow = 1;
            mDetailPanel.Add(scrollView);
            
            var states = fsm.GetAllStates();
            var currentId = fsm.CurrentStateId;
            
            foreach (var kvp in states)
            {
                var isCurrent = kvp.Key == currentId;
                var stateItem = CreateStateItem(fsm.EnumType, kvp.Key, kvp.Value, isCurrent);
                scrollView.Add(stateItem);
            }
        }

        private void AddInfoRow(VisualElement parent, string label, string value, bool highlight = false)
        {
            var row = new VisualElement();
            row.AddToClassList("info-row");
            
            var labelElement = new Label(label);
            labelElement.AddToClassList("info-label");
            row.Add(labelElement);
            
            var valueElement = new Label(value);
            valueElement.AddToClassList("info-value");
            if (highlight)
                valueElement.AddToClassList("highlight");
            row.Add(valueElement);
            
            parent.Add(row);
        }

        private VisualElement CreateStateItem(Type enumType, int stateId, IState state, bool isCurrent)
        {
            var item = new VisualElement();
            item.AddToClassList("state-item");
            if (isCurrent)
                item.AddToClassList("current");
            
            var indicator = new Label(isCurrent ? "▶" : "");
            indicator.AddToClassList("state-indicator");
            item.Add(indicator);
            
            var stateName = Enum.GetName(enumType, stateId) ?? stateId.ToString();
            var nameLabel = new Label(stateName);
            nameLabel.AddToClassList("state-name");
            item.Add(nameLabel);
            
            var typeLabel = new Label(state.GetType().Name);
            typeLabel.AddToClassList("state-type");
            item.Add(typeLabel);
            
            return item;
        }

        #region History Panel

        private VisualElement CreateHistoryPanel()
        {
            var container = new VisualElement();
            container.style.minHeight = 280;
            container.style.borderTopWidth = 1;
            container.style.borderTopColor = new StyleColor(new UnityEngine.Color(0.3f, 0.3f, 0.3f));
            
            // 工具栏
            var toolbar = CreateToolbar();
            container.Add(toolbar);
            
            var titleLabel = new Label("📜 转换历史");
            titleLabel.style.unityFontStyleAndWeight = UnityEngine.FontStyle.Bold;
            titleLabel.AddToClassList("toolbar-label");
            toolbar.Add(titleLabel);
            
            var recordToggle = CreateToolbarToggle("记录", FsmDebugger.RecordTransitions,
                v => FsmDebugger.RecordTransitions = v);
            toolbar.Add(recordToggle);
            
            toolbar.Add(new VisualElement { style = { flexGrow = 1 } });
            
            mHistoryCountLabel = new Label("0/500");
            mHistoryCountLabel.AddToClassList("toolbar-label");
            toolbar.Add(mHistoryCountLabel);
            
            var clearBtn = CreateToolbarButton("清空", () =>
            {
                FsmDebugger.ClearHistory();
                RefreshHistoryList();
            });
            toolbar.Add(clearBtn);
            
            // 历史列表
            var scrollView = new ScrollView();
            scrollView.style.flexGrow = 1;
            container.Add(scrollView);
            
            var historyList = new VisualElement();
            historyList.name = "history-list";
            scrollView.Add(historyList);
            
            return container;
        }

        private void RefreshHistoryList()
        {
            var historyList = mHistoryPanel.Q<VisualElement>("history-list");
            if (historyList == null) return;
            
            historyList.Clear();
            
            var history = FsmDebugger.TransitionHistory;
            mHistoryCountLabel.text = $"{history.Count}/{FsmDebugger.MAX_HISTORY_COUNT}";
            
            // 只显示选中 FSM 的历史，或者全部（如果没有选中）
            var filterName = mSelectedFsm?.Name;
            
            // 倒序显示最新的在上面
            for (var i = history.Count - 1; i >= 0; i--)
            {
                var entry = history[i];
                
                // 如果选中了 FSM，只显示该 FSM 的历史
                if (filterName != null && entry.FsmName != filterName)
                    continue;
                
                var item = CreateHistoryItem(entry);
                historyList.Add(item);
            }
            
            if (historyList.childCount == 0)
            {
                var empty = new Label("  暂无转换记录");
                empty.style.color = new StyleColor(new UnityEngine.Color(0.5f, 0.5f, 0.5f));
                empty.style.fontSize = 11;
                empty.style.marginTop = 8;
                historyList.Add(empty);
            }
        }

        private VisualElement CreateHistoryItem(FsmDebugger.TransitionEntry entry)
        {
            var item = new VisualElement();
            item.style.flexDirection = FlexDirection.Row;
            item.style.alignItems = Align.Center;
            item.style.paddingLeft = 4;
            item.style.paddingTop = 2;
            item.style.paddingBottom = 2;
            item.style.borderBottomWidth = 1;
            item.style.borderBottomColor = new StyleColor(new UnityEngine.Color(0.2f, 0.2f, 0.2f));
            
            // 时间
            var time = new Label($"{entry.Time:F2}s");
            time.style.width = 50;
            time.style.fontSize = 10;
            time.style.color = new StyleColor(new UnityEngine.Color(0.6f, 0.6f, 0.6f));
            item.Add(time);
            
            // 动作类型
            var actionBadge = new Label(entry.Action);
            actionBadge.style.width = 50;
            actionBadge.style.fontSize = 10;
            actionBadge.style.unityFontStyleAndWeight = UnityEngine.FontStyle.Bold;
            
            var actionColor = entry.Action switch
            {
                "Start" => new UnityEngine.Color(0.3f, 0.8f, 0.3f),
                "Change" => new UnityEngine.Color(0.3f, 0.6f, 0.9f),
                "Stop" => new UnityEngine.Color(0.9f, 0.4f, 0.4f),
                _ => new UnityEngine.Color(0.7f, 0.7f, 0.7f)
            };
            actionBadge.style.color = new StyleColor(actionColor);
            item.Add(actionBadge);
            
            // 转换信息
            var transition = new Label();
            transition.style.flexGrow = 1;
            transition.style.fontSize = 11;
            transition.style.color = new StyleColor(new UnityEngine.Color(0.8f, 0.8f, 0.8f));
            
            if (entry.Action == "Change")
                transition.text = $"{entry.FromState} → {entry.ToState}";
            else if (!string.IsNullOrEmpty(entry.ToState))
                transition.text = entry.ToState;
            else if (!string.IsNullOrEmpty(entry.FromState))
                transition.text = entry.FromState;
            
            item.Add(transition);
            
            return item;
        }

        #endregion

        #region Update

        public override void OnUpdate()
        {
            if (!IsPlaying) return;
            
            if (EditorApplication.timeSinceStartup - mLastRefreshTime > REFRESH_INTERVAL)
            {
                FsmDebugger.GetActiveFsms(mCachedFsms);
                mFsmListView.itemsSource = mCachedFsms;
                mFsmListView.RefreshItems();
                
                if (mSelectedFsm != null)
                    UpdateDetailPanel();
                
                RefreshHistoryList();
                
                mLastRefreshTime = EditorApplication.timeSinceStartup;
            }
        }

        #endregion
    }
}
