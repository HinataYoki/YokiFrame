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
        public override int Priority => 20;

        private const float REFRESH_INTERVAL = 0.2f;

        private enum ViewMode { Runtime, History }

        private ViewMode mViewMode = ViewMode.Runtime;
        private double mLastRefreshTime;

        // UI 元素引用
        private VisualElement mRuntimeView;
        private VisualElement mHistoryView;
        private VisualElement mToolbarButtons;
        private ListView mFsmListView;
        private VisualElement mDetailPanel;
        private ListView mHistoryListView;
        private Label mHistoryCountLabel;

        // 数据缓存
        private readonly List<IFSM> mCachedFsms = new(16);
        private IFSM mSelectedFsm;

        protected override void BuildUI(VisualElement root)
        {
            // 工具栏
            var toolbar = CreateToolbar();
            root.Add(toolbar);
            
            mToolbarButtons = new VisualElement();
            mToolbarButtons.style.flexDirection = FlexDirection.Row;
            toolbar.Add(mToolbarButtons);
            
            AddViewModeButton("运行时监控", ViewMode.Runtime);
            AddViewModeButton("转换历史", ViewMode.History);
            
            toolbar.Add(new VisualElement { style = { flexGrow = 1 } });
            
            // 内容区域
            var content = new VisualElement();
            content.AddToClassList("content-area");
            root.Add(content);
            
            mRuntimeView = CreateRuntimeView();
            mHistoryView = CreateHistoryView();
            
            content.Add(mRuntimeView);
            content.Add(mHistoryView);
            
            SwitchView(ViewMode.Runtime);
        }

        private void AddViewModeButton(string text, ViewMode mode)
        {
            var button = CreateToolbarButton(text, () => SwitchView(mode));
            button.name = $"btn_{mode}";
            mToolbarButtons.Add(button);
        }

        private void SwitchView(ViewMode mode)
        {
            mViewMode = mode;
            
            mRuntimeView.style.display = mode == ViewMode.Runtime ? DisplayStyle.Flex : DisplayStyle.None;
            mHistoryView.style.display = mode == ViewMode.History ? DisplayStyle.Flex : DisplayStyle.None;
            
            foreach (var child in mToolbarButtons.Children())
            {
                if (child is Button btn)
                {
                    var isSelected = btn.name == $"btn_{mode}";
                    if (isSelected)
                        btn.AddToClassList("selected");
                    else
                        btn.RemoveFromClassList("selected");
                }
            }
        }

        #region Runtime View

        private VisualElement CreateRuntimeView()
        {
            var container = new VisualElement();
            container.style.flexGrow = 1;
            
            // 分割面板
            var splitView = CreateSplitView(250f);
            container.Add(splitView);
            
            // 左侧：FSM 列表
            var leftPanel = new VisualElement();
            leftPanel.AddToClassList("left-panel");
            splitView.Add(leftPanel);
            
            var leftHeader = CreatePanelHeader("活跃状态机");
            leftPanel.Add(leftHeader);
            
            mFsmListView = new ListView();
            mFsmListView.makeItem = () =>
            {
                var item = new VisualElement();
                item.AddToClassList("list-item");
                
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
            
            // 右侧：详情面板
            var rightPanel = new VisualElement();
            rightPanel.AddToClassList("right-panel");
            splitView.Add(rightPanel);
            
            mDetailPanel = rightPanel;
            UpdateDetailPanel();
            
            return container;
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

        #endregion

        #region History View

        private VisualElement CreateHistoryView()
        {
            var container = new VisualElement();
            container.style.flexGrow = 1;
            
            // 工具栏
            var toolbar = CreateToolbar();
            container.Add(toolbar);
            
            var recordToggle = CreateToolbarToggle("记录转换", FsmDebugger.RecordTransitions,
                v => FsmDebugger.RecordTransitions = v);
            toolbar.Add(recordToggle);
            
            toolbar.Add(new VisualElement { style = { flexGrow = 1 } });
            
            mHistoryCountLabel = new Label("记录: 0/500");
            mHistoryCountLabel.AddToClassList("toolbar-label");
            toolbar.Add(mHistoryCountLabel);
            
            var clearBtn = CreateToolbarButton("清空", () =>
            {
                FsmDebugger.ClearHistory();
                RefreshHistoryList();
            });
            toolbar.Add(clearBtn);
            
            // 历史列表
            mHistoryListView = new ListView();
            mHistoryListView.makeItem = CreateHistoryItem;
            mHistoryListView.bindItem = BindHistoryItem;
            mHistoryListView.style.flexGrow = 1;
            container.Add(mHistoryListView);
            
            return container;
        }

        private VisualElement CreateHistoryItem()
        {
            var item = new VisualElement();
            item.AddToClassList("history-item");
            
            var time = new Label();
            time.AddToClassList("history-time");
            item.Add(time);
            
            var actionBadge = new Label();
            actionBadge.AddToClassList("history-badge");
            item.Add(actionBadge);
            
            var fsmName = new Label();
            fsmName.AddToClassList("history-key");
            fsmName.style.width = 150;
            item.Add(fsmName);
            
            var transition = new Label();
            transition.style.flexGrow = 1;
            transition.style.color = new StyleColor(new UnityEngine.Color(0.8f, 0.8f, 0.8f));
            item.Add(transition);
            
            return item;
        }

        private void BindHistoryItem(VisualElement element, int index)
        {
            var history = FsmDebugger.TransitionHistory;
            var entry = history[history.Count - 1 - index];
            
            var labels = element.Query<Label>().ToList();
            if (labels.Count < 4) return;
            
            labels[0].text = $"{entry.Time:F2}s";
            
            labels[1].text = entry.Action;
            labels[1].RemoveFromClassList("start");
            labels[1].RemoveFromClassList("change");
            labels[1].AddToClassList(entry.Action.ToLower());
            
            labels[2].text = entry.FsmName;
            
            if (entry.Action == "Change")
                labels[3].text = $"{entry.FromState} → {entry.ToState}";
            else if (!string.IsNullOrEmpty(entry.ToState))
                labels[3].text = entry.ToState;
            else if (!string.IsNullOrEmpty(entry.FromState))
                labels[3].text = entry.FromState;
            else
                labels[3].text = "";
        }

        private void RefreshHistoryList()
        {
            var history = FsmDebugger.TransitionHistory;
            mHistoryListView.itemsSource = new int[history.Count];
            mHistoryListView.RefreshItems();
            mHistoryCountLabel.text = $"记录: {history.Count}/{FsmDebugger.MAX_HISTORY_COUNT}";
        }

        #endregion

        #region Update

        public override void OnUpdate()
        {
            if (!IsPlaying) return;
            
            if (EditorApplication.timeSinceStartup - mLastRefreshTime > REFRESH_INTERVAL)
            {
                if (mViewMode == ViewMode.Runtime)
                {
                    FsmDebugger.GetActiveFsms(mCachedFsms);
                    mFsmListView.itemsSource = mCachedFsms;
                    mFsmListView.RefreshItems();
                    
                    if (mSelectedFsm != null)
                        UpdateDetailPanel();
                }
                else if (mViewMode == ViewMode.History)
                {
                    RefreshHistoryList();
                }
                
                mLastRefreshTime = EditorApplication.timeSinceStartup;
            }
        }

        #endregion
    }
}
