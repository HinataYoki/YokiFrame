#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine.UIElements;
using YokiFrame;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// PoolKit 监控工具页面
    /// 采用 Master-Detail 布局（左侧池列表 + 右侧详情面板）
    /// 右侧面板上下分屏：活跃对象（快照）+ 事件日志（流）
    /// 使用响应式订阅模式，通过 EditorDataBridge 接收运行时数据变化
    /// </summary>
    [YokiToolPage(
        kit: "PoolKit",
        name: "PoolKit",
        icon: KitIcons.POOLKIT,
        priority: 25,
        category: YokiPageCategory.Tool)]
    public partial class PoolKitToolPage : YokiToolPageBase
    {

        #region 常量

        private const float THROTTLE_INTERVAL = 0.1f;   // 节流间隔（秒）
        private const float LIST_ITEM_HEIGHT = 56f;     // 列表项高度
        private const float RIGHT_SPLIT_RATIO = 0.6f;   // 右侧上下分屏比例

        #endregion

        #region 字段

        // ViewModel
        private PoolKitViewModel mViewModel;

        // UI 元素引用
        private ListView mPoolListView;
        private VisualElement mRightPanel;
        private VisualElement mHudSection;
        private TwoPaneSplitView mRightSplitView;       // 右侧上下分屏

        // HUD 卡片标签
        private Label mTotalLabel;
        private Label mActiveLabel;
        private Label mInactiveLabel;
        private Label mPeakLabel;

        // 数据缓存（用于 ListView 绑定）
        private readonly List<PoolDebugInfo> mCachedPools = new(16);
        private PoolDebugInfo mSelectedPool;

        // 分屏高度记忆（EditorPrefs 键）
        private const string SPLIT_HEIGHT_PREF_KEY = "YokiFrame.PoolKit.SplitHeight";
        private const float DEFAULT_SPLIT_HEIGHT = 200f;

        // 节流器
        private Throttle mRefreshThrottle;

        #endregion

        #region BuildUI

        protected override void BuildUI(VisualElement root)
        {
            // 工具栏
            var toolbar = YokiFrameUIComponents.CreateToolbar();
            root.Add(toolbar);

            var helpLabel = new Label("对象池运行时监控");
            helpLabel.AddToClassList("toolbar-label");
            toolbar.Add(helpLabel);

            toolbar.Add(YokiFrameUIComponents.CreateFlexSpacer());

            // 追踪开关
            var trackingToggle = CreateToolbarToggle("追踪", PoolDebugger.EnableTracking, value =>
            {
                PoolDebugger.EnableTracking = value;
            });
            toolbar.Add(trackingToggle);

            // 堆栈开关
            var stackToggle = CreateToolbarToggle("堆栈", PoolDebugger.EnableStackTrace, value =>
            {
                PoolDebugger.EnableStackTrace = value;
            });
            toolbar.Add(stackToggle);

            // 内容区域（填充剩余空间）
            var content = new VisualElement();
            content.AddToClassList("content-area");
            content.style.flexGrow = 1;
            root.Add(content);

            // 左右分割面板
            var splitView = CreateSplitView(280f);
            content.Add(splitView);

            // 左侧：池列表
            var leftPanel = BuildLeftPanel();
            splitView.Add(leftPanel);

            // 右侧：详情面板（HUD + 上下分屏）
            mRightPanel = BuildRightPanel();
            splitView.Add(mRightPanel);

            // 初始状态
            UpdateRightPanel();
        }

        /// <summary>
        /// 构建右侧面板（HUD + 上下分屏）
        /// </summary>
        private VisualElement BuildRightPanel()
        {
            var panel = new VisualElement();
            panel.style.flexGrow = 1;
            panel.style.flexDirection = FlexDirection.Column;

            // HUD 区域（固定高度）
            mHudSection = BuildHudSection();
            panel.Add(mHudSection);

            // 上下分屏区域（活跃对象 + 事件日志）
            // 从 EditorPrefs 读取上次保存的分屏高度
            var savedHeight = EditorPrefs.GetFloat(SPLIT_HEIGHT_PREF_KEY, DEFAULT_SPLIT_HEIGHT);
            mRightSplitView = new TwoPaneSplitView(0, savedHeight, TwoPaneSplitViewOrientation.Vertical);
            mRightSplitView.style.flexGrow = 1;

            // 监听分屏高度变化，保存到 EditorPrefs
            mRightSplitView.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                if (mRightSplitView.fixedPane != default)
                {
                    var currentHeight = mRightSplitView.fixedPane.resolvedStyle.height;
                    if (currentHeight > 0)
                    {
                        EditorPrefs.SetFloat(SPLIT_HEIGHT_PREF_KEY, currentHeight);
                    }
                }
            });
            panel.Add(mRightSplitView);

            // 上半部分：活跃对象列表
            var activeSection = BuildActiveObjectsSection();
            mRightSplitView.Add(activeSection);

            // 下半部分：事件日志
            var eventLogSection = BuildEventLogSection();
            mRightSplitView.Add(eventLogSection);

            return panel;
        }

        #endregion

        #region 生命周期

        public override void OnActivate()
        {
            base.OnActivate();

            // 创建 ViewModel
            mViewModel = new PoolKitViewModel();

            // 创建节流器
            mRefreshThrottle = CreateThrottle(THROTTLE_INTERVAL);

            // 订阅运行时数据变化通道（带节流）
            SubscribeChannelThrottled<PoolDebugInfo>(
                PoolDebugger.CHANNEL_POOL_LIST_CHANGED,
                OnPoolListChanged,
                THROTTLE_INTERVAL);

            SubscribeChannelThrottled<PoolDebugInfo>(
                PoolDebugger.CHANNEL_POOL_ACTIVE_CHANGED,
                OnPoolActiveChanged,
                THROTTLE_INTERVAL);

            SubscribeChannelThrottled<PoolEvent>(
                PoolDebugger.CHANNEL_POOL_EVENT_LOGGED,
                OnPoolEventLogged,
                THROTTLE_INTERVAL);

            // 初始加载数据（页面激活时立即刷新）
            if (IsPlaying)
            {
                RefreshPoolList();

                // 如果有选中的池，刷新详情
                if (mSelectedPool != default)
                {
                    UpdateRightPanel();
                }
            }
        }

        public override void OnDeactivate()
        {
            // 停止时间更新调度器
            StopDurationUpdateScheduler();

            // 清理 ViewModel
            mViewModel?.Dispose();
            mViewModel = null;

            base.OnDeactivate();
        }

        protected override void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            base.OnPlayModeStateChanged(state);

            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                // 进入 PlayMode 时刷新数据
                RefreshPoolList();
            }
            else if (state == PlayModeStateChange.ExitingPlayMode)
            {
                // 退出 PlayMode 时清空数据
                mCachedPools.Clear();
                mSelectedPool = null;
                mPoolListView?.RefreshItems();
                ShowEmptyState();
            }
        }

        #endregion

        #region 响应式事件处理

        /// <summary>
        /// 池列表变化事件处理
        /// </summary>
        private void OnPoolListChanged(PoolDebugInfo info)
        {
            mRefreshThrottle.Execute(RefreshPoolList);
        }

        /// <summary>
        /// 活跃对象变化事件处理
        /// </summary>
        private void OnPoolActiveChanged(PoolDebugInfo info)
        {
            // 仅当变化的池是当前选中的池时才刷新
            if (mSelectedPool != null && info != null && mSelectedPool.Name == info.Name)
            {
                mRefreshThrottle.Execute(() =>
                {
                    UpdateHudSection();
                    UpdateActiveObjectsList();
                });
            }
        }

        /// <summary>
        /// 事件日志事件处理
        /// </summary>
        private void OnPoolEventLogged(PoolEvent evt)
        {
            // 仅当事件属于当前选中的池时才刷新
            if (mSelectedPool != null && evt.PoolName == mSelectedPool.Name)
            {
                mRefreshThrottle.Execute(UpdateEventLogList);
            }
        }

        /// <summary>
        /// 刷新池列表
        /// </summary>
        private void RefreshPoolList()
        {
            if (!IsPlaying) return;

            PoolDebugger.GetAllPools(mCachedPools);
            mPoolListView.itemsSource = mCachedPools;
            mPoolListView.RefreshItems();

            // 如果有选中的池，通过名称匹配更新引用
            if (mSelectedPool != default)
            {
                var selectedName = mSelectedPool.Name;
                mSelectedPool = default;

                for (int i = 0; i < mCachedPools.Count; i++)
                {
                    if (mCachedPools[i].Name == selectedName)
                    {
                        mSelectedPool = mCachedPools[i];
                        break;
                    }
                }

                // 如果选中的池已不存在，清除选择并显示空状态
                if (mSelectedPool == default)
                {
                    mPoolListView.ClearSelection();
                    ShowEmptyState();
                }
                else
                {
                    // 更新右侧面板数据
                    UpdateRightPanel();
                }
            }
        }

        #endregion

        /// <summary>
        /// 更新右侧面板
        /// </summary>
        private void UpdateRightPanel()
        {
            if (mSelectedPool == default)
            {
                ShowEmptyState();
                return;
            }

            UpdateHudSection();
            UpdateActiveObjectsList();
            UpdateEventLogList();
        }

        /// <summary>
        /// 显示空状态
        /// </summary>
        private void ShowEmptyState()
        {
            // 更新 HUD 显示状态（切换到空状态）
            UpdateHudSection();

            // 清空活跃对象列表
            mActiveObjectsScrollView?.Clear();

            // 清空事件日志列表
            mFilteredEvents.Clear();
            mEventLogListView?.RefreshItems();
        }
    }
}
#endif
