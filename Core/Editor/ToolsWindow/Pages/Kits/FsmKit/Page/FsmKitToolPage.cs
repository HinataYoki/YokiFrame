using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine.UIElements;
using YokiFrame.EditorTools;

namespace YokiFrame
{
    /// <summary>
    /// FsmKit 运行时监视页。
    /// 使用统一的监视台骨架展示状态机列表、状态摘要、状态矩阵与转换历史。
    /// </summary>
    [YokiToolPage(
        kit: "FsmKit",
        name: "FsmKit",
        icon: KitIcons.FSMKIT,
        priority: 20,
        category: YokiPageCategory.Tool)]
    public partial class FsmKitToolPage : YokiToolPageBase
    {
        #region Constants

        private const float REFRESH_INTERVAL = 0.1f;
        private const float LIST_ITEM_HEIGHT = 48f;

        #endregion

        #region Fields

        private double mLastRefreshTime;

        private ListView mFsmListView;
        private VisualElement mRightPanel;
        private VisualElement mHudSection;
        private VisualElement mMatrixSection;
        private VisualElement mTimelineSection;
        private VisualElement mRuntimeStatusBanner;

        private Label mCurrentStateLabel;
        private Label mDurationLabel;
        private Label mPrevStateLabel;

        private readonly List<IFSM> mCachedFsms = new(16);
        private IFSM mSelectedFsm;
        private string mLastCurrentState;

        private IDisposable mFsmListSubscription;
        private IDisposable mFsmStateSubscription;
        private IDisposable mHistorySubscription;

        #endregion

        #region BuildUI

        /// <summary>
        /// 构建 FsmKit 页面骨架。
        /// </summary>
        protected override void BuildUI(VisualElement root)
        {
            var scaffold = CreateKitPageScaffold(
                "FsmKit",
                "面向运行时排查的状态机监视台，聚焦当前状态、持续时间、状态访问情况与转换历史。",
                KitIcons.FSMKIT,
                "KIT 运行时监视");
            root.Add(scaffold.Root);

            scaffold.Toolbar.style.display = DisplayStyle.None;

            mRuntimeStatusBanner = CreateKitStatusBanner(
                "运行模式说明",
                "当前未进入 PlayMode。进入 PlayMode 后将自动订阅 FSM 调试数据并实时刷新状态信息。",
                YokiFrameUIComponents.HelpBoxType.Info);
            scaffold.StatusBar.Add(mRuntimeStatusBanner);

            var splitView = CreateSplitView(300f, "YokiFrame.FsmKit.MainSplitWidth.V3");
            splitView.AddToClassList("yoki-monitor-layout");
            scaffold.Content.Add(splitView);

            var leftPanel = BuildLeftPanel();
            splitView.Add(leftPanel);

            mRightPanel = BuildRightPanel();
            mRightPanel.AddToClassList("yoki-monitor-layout__detail");
            splitView.Add(mRightPanel);

            RefreshStatusBanner();
            UpdateRightPanel();
        }

        /// <summary>
        /// 构建左侧状态机列表面板。
        /// </summary>
        private VisualElement BuildLeftPanel()
        {
            var (leftPanel, body) = CreateKitSectionPanel(
                "活动状态机",
                "展示当前已注册并参与运行的状态机实例。",
                KitIcons.FSMKIT);
            leftPanel.AddToClassList("left-panel");
            leftPanel.AddToClassList("yoki-kit-panel--blue");

            mFsmListView = new ListView
            {
                fixedItemHeight = LIST_ITEM_HEIGHT,
                makeItem = MakeListItem,
                bindItem = BindListItem
            };
#if UNITY_2022_1_OR_NEWER
            mFsmListView.selectionChanged += OnFsmSelected;
#else
            mFsmListView.onSelectionChange += OnFsmSelected;
#endif
            mFsmListView.style.flexGrow = 1;
            body.Add(mFsmListView);

            return leftPanel;
        }

        /// <summary>
        /// 构建右侧监视台区域。
        /// </summary>
        private VisualElement BuildRightPanel()
        {
            var rightPanel = new VisualElement();
            rightPanel.AddToClassList("right-panel");
            rightPanel.AddToClassList("yoki-monitor-dashboard");
            rightPanel.style.flexDirection = FlexDirection.Column;

            mHudSection = BuildHudSection();
            rightPanel.Add(mHudSection);

            mMatrixSection = BuildMatrixSection();
            rightPanel.Add(mMatrixSection);

            mTimelineSection = BuildTimelineSection();
            rightPanel.Add(mTimelineSection);

            return rightPanel;
        }

        #endregion

        #region List Items

        /// <summary>
        /// 创建状态机列表项模板。
        /// </summary>
        private VisualElement MakeListItem()
        {
            var item = new VisualElement();
            item.AddToClassList("yoki-fsm-list-item");

            var indicator = new VisualElement { name = "indicator" };
            indicator.AddToClassList("yoki-fsm-list-item__indicator");
            item.Add(indicator);

            var infoArea = new VisualElement();
            infoArea.AddToClassList("yoki-fsm-list-item__info");
            item.Add(infoArea);

            var nameLabel = new Label { name = "fsm-name" };
            nameLabel.AddToClassList("yoki-fsm-list-item__name");
            infoArea.Add(nameLabel);

            var stateRow = new VisualElement();
            stateRow.AddToClassList("yoki-fsm-list-item__state-row");
            infoArea.Add(stateRow);

            var currentStateLabel = new Label { name = "current-state" };
            currentStateLabel.AddToClassList("yoki-fsm-list-item__current-state");
            stateRow.Add(currentStateLabel);

            var timerLabel = new Label { name = "timer" };
            timerLabel.AddToClassList("yoki-fsm-list-item__timer");
            stateRow.Add(timerLabel);

            var countBadge = new Label { name = "state-count" };
            countBadge.AddToClassList("yoki-fsm-list-item__count-badge");
            item.Add(countBadge);

            return item;
        }

        /// <summary>
        /// 绑定状态机列表项数据。
        /// </summary>
        private void BindListItem(VisualElement element, int index)
        {
            if (index < 0 || index >= mCachedFsms.Count)
            {
                return;
            }

            var fsm = mCachedFsms[index];
            bool isRunning = fsm.MachineState == MachineState.Running;

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

            string currentStateName = GetCurrentStateName(fsm);
            var stateLabel = element.Q<Label>("current-state");
            stateLabel.text = currentStateName;

            stateLabel.RemoveFromClassList("yoki-fsm-list-item__current-state--running");
            stateLabel.RemoveFromClassList("yoki-fsm-list-item__current-state--inactive");
            stateLabel.AddToClassList(isRunning
                ? "yoki-fsm-list-item__current-state--running"
                : "yoki-fsm-list-item__current-state--inactive");

            element.Q<Label>("timer").text = isRunning ? $"{FsmDebugger.GetStateDuration(fsm.Name):F1}s" : "--";
            element.Q<Label>("state-count").text = $"{fsm.GetAllStates().Count}";
        }

        /// <summary>
        /// 获取当前状态名称。
        /// </summary>
        private string GetCurrentStateName(IFSM fsm) =>
            fsm.CurrentStateId < 0 ? "无" : Enum.GetName(fsm.EnumType, fsm.CurrentStateId) ?? fsm.CurrentStateId.ToString();

        #endregion

        #region Selection And Refresh

        /// <summary>
        /// 处理状态机选中变化。
        /// </summary>
        private void OnFsmSelected(IEnumerable<object> selection)
        {
            foreach (var item in selection)
            {
                if (item is IFSM fsm)
                {
                    mSelectedFsm = fsm;
                    mLastCurrentState = null;
                    UpdateRightPanel();
                    return;
                }
            }
        }

        /// <summary>
        /// 刷新右侧监视区。
        /// </summary>
        private void UpdateRightPanel()
        {
            if (mSelectedFsm == null)
            {
                ShowEmptyState();
                return;
            }

            if (mCurrentStateLabel == null || mCurrentStateLabel.parent == null)
            {
                RebuildHudSection();
            }

            UpdateHudSection();
            UpdateMatrixSection();
            UpdateTimelineSection();
        }

        /// <summary>
        /// HUD 内容被清空后重建摘要正文。
        /// </summary>
        private void RebuildHudSection()
        {
            mHudSection.Clear();
            mHudSection.Add(BuildHudContent());
        }

        /// <summary>
        /// 未选中状态机时显示空状态。
        /// </summary>
        private void ShowEmptyState()
        {
            mCurrentStateLabel = null;
            mDurationLabel = null;
            mPrevStateLabel = null;

            mHudSection.Clear();
            mHudSection.Add(CreateEmptyState(
                KitIcons.FSMKIT,
                "未选择状态机",
                "先在左侧选择一个状态机，再查看状态摘要、状态矩阵和转换历史。"));

            mMatrixContainer?.Clear();
            mTimelineList?.Clear();
        }

        #endregion

        #region Reactive Subscription

        /// <summary>
        /// 页面激活时挂接 FSM 调试通道。
        /// </summary>
        public override void OnActivate()
        {
            base.OnActivate();

            mFsmListSubscription = Disposable.Empty;
            mFsmStateSubscription = Disposable.Empty;
            mHistorySubscription = Disposable.Empty;

            SubscribeChannelThrottled<IFSM>(
                DataChannels.FSM_LIST_CHANGED,
                OnFsmListChanged,
                REFRESH_INTERVAL);

            SubscribeChannelThrottled<IFSM>(
                DataChannels.FSM_STATE_CHANGED,
                OnFsmStateChanged,
                REFRESH_INTERVAL);

            SubscribeChannel<FsmDebugger.TransitionEntry>(
                DataChannels.FSM_HISTORY_LOGGED,
                OnHistoryLogged);

            RefreshFsmList();
            RefreshStatusBanner();
        }

        /// <summary>
        /// 页面停用时释放订阅。
        /// </summary>
        public override void OnDeactivate()
        {
            base.OnDeactivate();
        }

        /// <summary>
        /// 处理状态机列表变化。
        /// </summary>
        private void OnFsmListChanged(IFSM _)
        {
            RefreshFsmList();
        }

        /// <summary>
        /// 处理状态机状态变化。
        /// </summary>
        private void OnFsmStateChanged(IFSM fsm)
        {
            mFsmListView?.RefreshItems();

            if (mSelectedFsm != null && fsm != null && mSelectedFsm.Name == fsm.Name)
            {
                string currentState = GetCurrentStateName(mSelectedFsm);
                if (currentState != mLastCurrentState)
                {
                    mLastCurrentState = currentState;
                    UpdateMatrixSection();
                }

                UpdateHudSection();
            }
        }

        /// <summary>
        /// 处理选中状态机的新增转换记录。
        /// </summary>
        private void OnHistoryLogged(FsmDebugger.TransitionEntry entry)
        {
            if (!mShowSelectedHistoryOnly)
            {
                UpdateTimelineSection();
                return;
            }

            if (mSelectedFsm != null && entry.FsmName == mSelectedFsm.Name)
            {
                UpdateTimelineSection();
            }
        }

        /// <summary>
        /// 刷新状态机列表，并尽量保留选中项。
        /// </summary>
        private void RefreshFsmList()
        {
            FsmDebugger.GetActiveFsms(mCachedFsms);
            mFsmListView.itemsSource = mCachedFsms;
            mFsmListView.RefreshItems();

            if (mSelectedFsm != null && !mCachedFsms.Contains(mSelectedFsm))
            {
                mSelectedFsm = null;
                mLastCurrentState = null;
                UpdateRightPanel();
            }
        }

        /// <summary>
        /// PlayMode 变化时同步状态条与订阅状态。
        /// </summary>
        protected override void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            base.OnPlayModeStateChanged(state);
            RefreshStatusBanner();
        }

        /// <summary>
        /// 少量轮询逻辑只用于时间显示与动画刷新。
        /// </summary>
        [Obsolete("Main data sync has moved to reactive subscriptions. This update loop only refreshes timers and animations.")]
        public override void OnUpdate()
        {
            if (!IsPlaying)
            {
                return;
            }

            var now = EditorApplication.timeSinceStartup;
            if (now - mLastRefreshTime < REFRESH_INTERVAL)
            {
                return;
            }

            mLastRefreshTime = now;
            mFsmListView?.RefreshItems();

            if (mSelectedFsm != null && mDurationLabel != null)
            {
                mDurationLabel.text = $"{FsmDebugger.GetStateDuration(mSelectedFsm.Name):F1}s";
            }

            YokiFrameUIComponents.UpdateAllBreathing();
        }

        /// <summary>
        /// 根据运行状态显示或隐藏顶部状态条。
        /// </summary>
        private void RefreshStatusBanner()
        {
            if (mRuntimeStatusBanner == default)
            {
                return;
            }

            mRuntimeStatusBanner.style.display = IsPlaying ? DisplayStyle.None : DisplayStyle.Flex;
        }

        #endregion
    }
}
