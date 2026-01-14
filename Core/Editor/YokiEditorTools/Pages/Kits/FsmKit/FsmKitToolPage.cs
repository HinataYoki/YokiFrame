using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine.UIElements;
using YokiFrame.EditorTools;

namespace YokiFrame
{
    /// <summary>
    /// FsmKit 工具页面 - 现代化 FSM 仪表盘
    /// 采用 HUD + 状态矩阵 + 时间轴 的三段式布局
    /// </summary>
    [YokiToolPage(
        kit: "FsmKit",
        name: "FsmKit",
        icon: KitIcons.FSMKIT,
        priority: 20,
        category: YokiPageCategory.Tool)]
    public partial class FsmKitToolPage : YokiToolPageBase
    {

        #region 常量

        private const float REFRESH_INTERVAL = 0.1f;    // 刷新间隔（秒）
        private const float LIST_ITEM_HEIGHT = 48f;     // 列表项高度

        #endregion

        #region 字段

        private double mLastRefreshTime;

        // UI 元素引用
        private ListView mFsmListView;
        private VisualElement mRightPanel;
        private VisualElement mHudSection;
        private VisualElement mMatrixSection;
        private VisualElement mTimelineSection;

        // HUD 元素
        private Label mCurrentStateLabel;
        private Label mDurationLabel;
        private Label mPrevStateLabel;

        // 数据缓存
        private readonly List<IFSM> mCachedFsms = new(16);
        private IFSM mSelectedFsm;
        private string mLastCurrentState;

        // 响应式订阅
        private IDisposable mFsmListSubscription;
        private IDisposable mFsmStateSubscription;
        private IDisposable mHistorySubscription;

        #endregion

        #region BuildUI

        protected override void BuildUI(VisualElement root)
        {
            // 工具栏
            var toolbar = YokiFrameUIComponents.CreateToolbar();
            root.Add(toolbar);

            var helpLabel = new Label("运行时状态机监控（需要运行游戏）");
            helpLabel.AddToClassList("toolbar-label");
            toolbar.Add(helpLabel);

            toolbar.Add(YokiFrameUIComponents.CreateFlexSpacer());

            // 内容区域
            var content = new VisualElement();
            content.AddToClassList("content-area");
            root.Add(content);

            // 分割面板
            var splitView = CreateSplitView(280f);
            content.Add(splitView);

            // 左侧：FSM 实例列表（带摘要信息）
            var leftPanel = BuildLeftPanel();
            splitView.Add(leftPanel);

            // 右侧：详情面板（HUD + 状态矩阵 + 时间轴）
            mRightPanel = BuildRightPanel();
            splitView.Add(mRightPanel);

            // 初始状态
            UpdateRightPanel();
        }

        /// <summary>
        /// 构建左侧面板（FSM 实例列表）
        /// </summary>
        private VisualElement BuildLeftPanel()
        {
            var leftPanel = new VisualElement();
            leftPanel.AddToClassList("left-panel");

            var leftHeader = YokiFrameUIComponents.CreateSectionHeader("活跃状态机");
            leftPanel.Add(leftHeader);

            mFsmListView = new ListView();
            mFsmListView.fixedItemHeight = LIST_ITEM_HEIGHT;
            mFsmListView.makeItem = MakeListItem;
            mFsmListView.bindItem = BindListItem;
#if UNITY_2022_1_OR_NEWER
            mFsmListView.selectionChanged += OnFsmSelected;
#else
            mFsmListView.onSelectionChange += OnFsmSelected;
#endif
            mFsmListView.style.flexGrow = 1;
            leftPanel.Add(mFsmListView);

            return leftPanel;
        }

        /// <summary>
        /// 构建右侧面板（三段式布局）
        /// </summary>
        private VisualElement BuildRightPanel()
        {
            var rightPanel = new VisualElement();
            rightPanel.AddToClassList("right-panel");
            rightPanel.style.flexDirection = FlexDirection.Column;

            // 区域 A: 当前状态 HUD（顶部，固定高度）
            mHudSection = BuildHudSection();
            rightPanel.Add(mHudSection);

            // 区域 B: 状态矩阵（中部，弹性高度）
            mMatrixSection = BuildMatrixSection();
            rightPanel.Add(mMatrixSection);

            // 区域 C: 转换时间轴（底部，固定高度）
            mTimelineSection = BuildTimelineSection();
            rightPanel.Add(mTimelineSection);

            return rightPanel;
        }

        #endregion

        #region 列表项

        /// <summary>
        /// 创建列表项模板
        /// </summary>
        private VisualElement MakeListItem()
        {
            var item = new VisualElement();
            item.AddToClassList("yoki-fsm-list-item");

            // 状态指示器
            var indicator = new VisualElement { name = "indicator" };
            indicator.AddToClassList("yoki-fsm-list-item__indicator");
            item.Add(indicator);

            // 信息区域
            var infoArea = new VisualElement();
            infoArea.AddToClassList("yoki-fsm-list-item__info");
            item.Add(infoArea);

            // FSM 名称
            var nameLabel = new Label { name = "fsm-name" };
            nameLabel.AddToClassList("yoki-fsm-list-item__name");
            infoArea.Add(nameLabel);

            // 当前状态 + 时间
            var stateRow = new VisualElement();
            stateRow.AddToClassList("yoki-fsm-list-item__state-row");
            infoArea.Add(stateRow);

            var currentStateLabel = new Label { name = "current-state" };
            currentStateLabel.AddToClassList("yoki-fsm-list-item__current-state");
            stateRow.Add(currentStateLabel);

            var timerLabel = new Label { name = "timer" };
            timerLabel.AddToClassList("yoki-fsm-list-item__timer");
            stateRow.Add(timerLabel);

            // 状态数量徽章
            var countBadge = new Label { name = "state-count" };
            countBadge.AddToClassList("yoki-fsm-list-item__count-badge");
            item.Add(countBadge);

            return item;
        }

        /// <summary>
        /// 绑定列表项数据
        /// </summary>
        private void BindListItem(VisualElement element, int index)
        {
            if (index >= mCachedFsms.Count) return;

            var fsm = mCachedFsms[index];
            var isRunning = fsm.MachineState == MachineState.Running;

            // 更新指示器样式
            var indicator = element.Q<VisualElement>("indicator");
            indicator.RemoveFromClassList("yoki-fsm-list-item__indicator--running");
            indicator.RemoveFromClassList("yoki-fsm-list-item__indicator--suspended");
            indicator.RemoveFromClassList("yoki-fsm-list-item__indicator--stopped");

            switch (fsm.MachineState)
            {
                case MachineState.Running:
                    indicator.AddToClassList("yoki-fsm-list-item__indicator--running");
                    break;
                case MachineState.Suspend:
                    indicator.AddToClassList("yoki-fsm-list-item__indicator--suspended");
                    break;
                default:
                    indicator.AddToClassList("yoki-fsm-list-item__indicator--stopped");
                    break;
            }

            element.Q<Label>("fsm-name").text = fsm.Name;

            var currentStateName = GetCurrentStateName(fsm);
            var stateLabel = element.Q<Label>("current-state");
            stateLabel.text = currentStateName;

            // 更新状态标签样式
            stateLabel.RemoveFromClassList("yoki-fsm-list-item__current-state--running");
            stateLabel.RemoveFromClassList("yoki-fsm-list-item__current-state--inactive");
            stateLabel.AddToClassList(isRunning
                ? "yoki-fsm-list-item__current-state--running"
                : "yoki-fsm-list-item__current-state--inactive");

            element.Q<Label>("timer").text = isRunning ? $"{FsmDebugger.GetStateDuration(fsm.Name):F1}s" : "—";
            element.Q<Label>("state-count").text = $"{fsm.GetAllStates().Count}";
        }

        /// <summary>
        /// 获取当前状态名称
        /// </summary>
        private string GetCurrentStateName(IFSM fsm) =>
            fsm.CurrentStateId < 0 ? "None" : Enum.GetName(fsm.EnumType, fsm.CurrentStateId) ?? fsm.CurrentStateId.ToString();

        #endregion

        #region 选择与更新

        private void OnFsmSelected(IEnumerable<object> selection)
        {
            foreach (var item in selection)
            {
                if (item is IFSM fsm)
                {
                    mSelectedFsm = fsm;
                    mLastCurrentState = null; // 强制刷新
                    UpdateRightPanel();
                    return;
                }
            }
        }

        /// <summary>
        /// 更新右侧面板
        /// </summary>
        private void UpdateRightPanel()
        {
            if (mSelectedFsm == null)
            {
                ShowEmptyState();
                return;
            }

            // 确保 HUD 结构存在（可能被 ShowEmptyState 清空）
            if (mCurrentStateLabel == null || mCurrentStateLabel.parent == null)
            {
                RebuildHudSection();
            }

            UpdateHudSection();
            UpdateMatrixSection();
            UpdateTimelineSection();
        }

        /// <summary>
        /// 重建 HUD 区域结构
        /// </summary>
        private void RebuildHudSection()
        {
            mHudSection.Clear();

            // 重新构建 HUD 内容
            var hudContent = BuildHudContent();
            mHudSection.Add(hudContent);
        }

        /// <summary>
        /// 显示空状态
        /// </summary>
        private void ShowEmptyState()
        {
            // 清除引用
            mCurrentStateLabel = null;
            mDurationLabel = null;
            mPrevStateLabel = null;

            // 重建空状态 HUD
            mHudSection.Clear();
            var emptyHint = CreateHelpBox("选择左侧状态机查看详情");
            mHudSection.Add(emptyHint);

            // 清空矩阵
            if (mMatrixContainer != null)
                mMatrixContainer.Clear();
        }

        #endregion

        #region 响应式订阅

        /// <summary>
        /// 页面激活时订阅事件
        /// </summary>
        public override void OnActivate()
        {
            base.OnActivate();

            // 订阅 FSM 列表变化
            mFsmListSubscription = EditorDataBridge.SubscribeThrottled<IFSM>(
                FsmDebugger.CHANNEL_FSM_LIST_CHANGED,
                OnFsmListChanged,
                REFRESH_INTERVAL);

            // 订阅 FSM 状态变化
            mFsmStateSubscription = EditorDataBridge.SubscribeThrottled<IFSM>(
                FsmDebugger.CHANNEL_FSM_STATE_CHANGED,
                OnFsmStateChanged,
                REFRESH_INTERVAL);

            // 订阅转换历史
            mHistorySubscription = EditorDataBridge.Subscribe<FsmDebugger.TransitionEntry>(
                FsmDebugger.CHANNEL_FSM_HISTORY_LOGGED,
                OnHistoryLogged);

            // 初始刷新
            RefreshFsmList();
        }

        /// <summary>
        /// 页面停用时取消订阅
        /// </summary>
        public override void OnDeactivate()
        {
            base.OnDeactivate();

            mFsmListSubscription?.Dispose();
            mFsmStateSubscription?.Dispose();
            mHistorySubscription?.Dispose();

            mFsmListSubscription = null;
            mFsmStateSubscription = null;
            mHistorySubscription = null;
        }

        /// <summary>
        /// FSM 列表变化回调
        /// </summary>
        private void OnFsmListChanged(IFSM _)
        {
            RefreshFsmList();
        }

        /// <summary>
        /// FSM 状态变化回调
        /// </summary>
        private void OnFsmStateChanged(IFSM fsm)
        {
            // 刷新列表项显示
            mFsmListView?.RefreshItems();

            // 如果是当前选中的 FSM，更新详情
            if (mSelectedFsm != null && fsm != null && mSelectedFsm.Name == fsm.Name)
            {
                var currentState = GetCurrentStateName(mSelectedFsm);
                if (currentState != mLastCurrentState)
                {
                    mLastCurrentState = currentState;
                    UpdateMatrixSection();
                }
                UpdateHudSection();
            }
        }

        /// <summary>
        /// 转换历史记录回调
        /// </summary>
        private void OnHistoryLogged(FsmDebugger.TransitionEntry entry)
        {
            // 如果是当前选中的 FSM，更新时间轴
            if (mSelectedFsm != null && entry.FsmName == mSelectedFsm.Name)
            {
                UpdateTimelineSection();
            }
        }

        /// <summary>
        /// 刷新 FSM 列表
        /// </summary>
        private void RefreshFsmList()
        {
            FsmDebugger.GetActiveFsms(mCachedFsms);
            mFsmListView.itemsSource = mCachedFsms;
            mFsmListView.RefreshItems();

            // 如果选中的 FSM 已不存在，清除选择
            if (mSelectedFsm != null && !mCachedFsms.Contains(mSelectedFsm))
            {
                mSelectedFsm = null;
                mLastCurrentState = null;
                UpdateRightPanel();
            }
        }

        /// <summary>
        /// 轮询更新 - 仅用于计时器刷新和呼吸动画
        /// 主要数据更新已迁移到响应式订阅
        /// </summary>
        [Obsolete("主要逻辑已迁移到响应式订阅，仅保留计时器和动画刷新")]
        public override void OnUpdate()
        {
            if (!IsPlaying) return;

            var now = EditorApplication.timeSinceStartup;
            if (now - mLastRefreshTime < REFRESH_INTERVAL) return;
            mLastRefreshTime = now;

            // 仅刷新计时器显示（列表项中的时间）
            mFsmListView?.RefreshItems();

            // 刷新 HUD 计时器
            if (mSelectedFsm != null && mDurationLabel != null)
            {
                mDurationLabel.text = $"{FsmDebugger.GetStateDuration(mSelectedFsm.Name):F1}s";
            }

            // 更新呼吸动画
            YokiFrameUIComponents.UpdateAllBreathing();
        }

        #endregion
    }
}
