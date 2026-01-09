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
    public partial class FsmKitToolPage : YokiFrameToolPageBase
    {
        public override string PageName => "FsmKit";
        public override string PageIcon => KitIcons.FSMKIT;
        public override int Priority => 20;

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
            item.AddToClassList("list-item");
            item.style.height = LIST_ITEM_HEIGHT;
            item.style.paddingTop = item.style.paddingBottom = 6;
            item.style.paddingLeft = item.style.paddingRight = 8;
            item.style.flexDirection = FlexDirection.Row;
            item.style.alignItems = Align.Center;
            
            // 状态指示器
            var indicator = new VisualElement { name = "indicator" };
            indicator.style.width = indicator.style.height = 8;
            indicator.style.borderTopLeftRadius = indicator.style.borderTopRightRadius = 
                indicator.style.borderBottomLeftRadius = indicator.style.borderBottomRightRadius = 4;
            indicator.style.marginRight = 8;
            item.Add(indicator);
            
            // 信息区域
            var infoArea = new VisualElement { style = { flexGrow = 1 } };
            item.Add(infoArea);
            
            // FSM 名称
            var nameLabel = new Label { name = "fsm-name" };
            nameLabel.style.fontSize = 12;
            nameLabel.style.unityFontStyleAndWeight = UnityEngine.FontStyle.Bold;
            nameLabel.style.color = new StyleColor(YokiFrameUIComponents.Colors.TextPrimary);
            infoArea.Add(nameLabel);
            
            // 当前状态 + 时间
            var stateRow = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 2 } };
            stateRow.style.alignItems = Align.Center;
            infoArea.Add(stateRow);
            
            stateRow.Add(new Label { name = "current-state", style = { fontSize = 11 } });
            stateRow.Add(new Label { name = "timer", style = { fontSize = 10, marginLeft = 8, color = new StyleColor(YokiFrameUIComponents.Colors.TextTertiary) } });
            
            // 状态数量徽章
            var countBadge = new Label { name = "state-count" };
            countBadge.style.fontSize = 10;
            countBadge.style.color = new StyleColor(YokiFrameUIComponents.Colors.TextTertiary);
            countBadge.style.paddingLeft = countBadge.style.paddingRight = 6;
            countBadge.style.paddingTop = countBadge.style.paddingBottom = 2;
            countBadge.style.backgroundColor = new StyleColor(new UnityEngine.Color(0.15f, 0.15f, 0.17f));
            countBadge.style.borderTopLeftRadius = countBadge.style.borderTopRightRadius = 
                countBadge.style.borderBottomLeftRadius = countBadge.style.borderBottomRightRadius = 4;
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
            var stateColor = isRunning ? YokiFrameUIComponents.Colors.BrandSuccess : YokiFrameUIComponents.Colors.TextTertiary;
            
            element.Q<VisualElement>("indicator").style.backgroundColor = new StyleColor(stateColor);
            element.Q<Label>("fsm-name").text = fsm.Name;
            
            var currentStateName = GetCurrentStateName(fsm);
            var stateLabel = element.Q<Label>("current-state");
            stateLabel.text = currentStateName;
            stateLabel.style.color = new StyleColor(stateColor);
            
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

        public override void OnUpdate()
        {
            if (!IsPlaying) return;
            
            var now = EditorApplication.timeSinceStartup;
            if (now - mLastRefreshTime < REFRESH_INTERVAL) return;
            mLastRefreshTime = now;
            
            // 刷新 FSM 列表
            FsmDebugger.GetActiveFsms(mCachedFsms);
            mFsmListView.itemsSource = mCachedFsms;
            mFsmListView.RefreshItems();
            
            // 刷新右侧面板
            if (mSelectedFsm != null)
            {
                // 检查状态是否变化
                var currentState = GetCurrentStateName(mSelectedFsm);
                if (currentState != mLastCurrentState)
                {
                    mLastCurrentState = currentState;
                    UpdateMatrixSection(); // 状态变化时重建矩阵
                }
                else
                {
                    UpdateHudSection(); // 只更新 HUD（计时器）
                }
                
                UpdateTimelineSection();
            }
            
            // 更新呼吸动画
            YokiFrameUIComponents.UpdateAllBreathing();
        }

        #endregion
    }
}
