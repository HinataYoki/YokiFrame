#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using YokiFrame;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// PoolKit 运行时监视页。
    /// 使用标准 Kit 页面骨架承载对象池列表、摘要信息、活跃对象与事件日志。
    /// </summary>
    [YokiToolPage(
        kit: "PoolKit",
        name: "PoolKit",
        icon: KitIcons.POOLKIT,
        priority: 25,
        category: YokiPageCategory.Tool)]
    public partial class PoolKitToolPage : YokiToolPageBase
    {
        #region Constants

        private const float THROTTLE_INTERVAL = 0.1f;
        private const float LIST_ITEM_HEIGHT = 56f;
        private const float RIGHT_SPLIT_RATIO = 0.6f;

        #endregion

        #region Fields

        private PoolKitViewModel mViewModel;

        private ListView mPoolListView;
        private VisualElement mRightPanel;
        private VisualElement mHudSection;
        private VisualElement mRuntimeStatusBanner;
        private TwoPaneSplitView mRightSplitView;

        private Label mTotalLabel;
        private Label mActiveLabel;
        private Label mInactiveLabel;
        private Label mPeakLabel;

        private readonly List<PoolDebugInfo> mCachedPools = new(16);
        private PoolDebugInfo mSelectedPool;

        private const string SPLIT_HEIGHT_PREF_KEY = "YokiFrame.PoolKit.SplitHeight";
        private const float DEFAULT_SPLIT_HEIGHT = 200f;

        private Throttle mRefreshThrottle;

        private double mLastAutoRefreshTime;
        private const double AUTO_REFRESH_INTERVAL = 1.0;

        #endregion

        #region BuildUI

        /// <summary>
        /// 构建 PoolKit 监视页。
        /// 页面布局为标准 Kit 骨架，运行时数据在后续刷新流程中注入。
        /// </summary>
        protected override void BuildUI(VisualElement root)
        {
            var heroActions = CreateHeroActions();
            var scaffold = CreateKitPageScaffold(
                "PoolKit",
                "面向运行时排查的对象池监视台，聚焦容量、借出对象、调用来源和回收事件。",
                KitIcons.POOLKIT,
                "KIT 运行时监视",
                heroActions);
            root.Add(scaffold.Root);

            scaffold.Toolbar.style.display = DisplayStyle.None;

            mRuntimeStatusBanner = CreateKitStatusBanner(
                "运行模式说明",
                "当前未进入 PlayMode。进入 PlayMode 后将同步对象池快照并实时刷新借出对象时长。",
                YokiFrameUIComponents.HelpBoxType.Info);
            scaffold.StatusBar.Add(mRuntimeStatusBanner);

            var splitView = CreateSplitView(300f, "YokiFrame.PoolKit.MainSplitWidth.V3");
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
        /// 构建右侧详情列，并恢复垂直分栏状态。
        /// 活跃对象与事件历史共享同一列，保证选中池上下文一致。
        /// </summary>
        private VisualElement BuildRightPanel()
        {
            var panel = new VisualElement();
            panel.style.flexGrow = 1;
            panel.style.flexDirection = FlexDirection.Column;
            panel.AddToClassList("yoki-monitor-dashboard");

            mHudSection = BuildHudSection();
            panel.Add(mHudSection);

            mRightSplitView = CreateVerticalSplitView(DEFAULT_SPLIT_HEIGHT, SPLIT_HEIGHT_PREF_KEY);
            mRightSplitView.AddToClassList("yoki-monitor-dashboard__split");
            panel.Add(mRightSplitView);

            var activeSection = BuildActiveObjectsSection();
            mRightSplitView.Add(activeSection);

            var eventLogSection = BuildEventLogSection();
            mRightSplitView.Add(eventLogSection);

            return panel;
        }

        #endregion

        #region Lifecycle

        /// <summary>
        /// 页面激活时创建视图模型、挂接监控通道，并开启活跃对象定时刷新。
        /// </summary>
        public override void OnActivate()
        {
            base.OnActivate();

            mViewModel = new PoolKitViewModel();
            mRefreshThrottle = CreateThrottle(THROTTLE_INTERVAL);

            SubscribeChannel<PoolDebugInfo>(
                DataChannels.POOL_LIST_CHANGED,
                OnPoolListChanged);

            SubscribeChannel<PoolDebugInfo>(
                DataChannels.POOL_ACTIVE_CHANGED,
                OnPoolActiveChanged);

            SubscribeChannel<PoolEvent>(
                DataChannels.POOL_EVENT_LOGGED,
                OnPoolEventLogged);

            if (IsPlaying)
            {
                RefreshPoolList();

                if (mSelectedPool != default)
                {
                    UpdateRightPanel();
                }
            }

            if (mPoolListView != default)
            {
                mPoolListView.itemsSource = mCachedPools;
                mPoolListView.Rebuild();
            }

            EditorApplication.update += OnEditorUpdate;
            mLastAutoRefreshTime = EditorApplication.timeSinceStartup;
            RefreshStatusBanner();
        }

        /// <summary>
        /// 页面停用时停止定时刷新并释放视图模型。
        /// 订阅清理由共享页面基类负责。
        /// </summary>
        public override void OnDeactivate()
        {
            EditorApplication.update -= OnEditorUpdate;

            mViewModel?.Dispose();
            mViewModel = null;

            base.OnDeactivate();
        }

        /// <summary>
        /// 在 PlayMode 切换时同步缓存状态。
        /// 进入 PlayMode 时拉取最新快照，退出时清理选中态与详情区。
        /// </summary>
        protected override void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            base.OnPlayModeStateChanged(state);
            RefreshStatusBanner();

            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                RefreshPoolList();
            }
            else if (state == PlayModeStateChange.ExitingPlayMode)
            {
                mCachedPools.Clear();
                mSelectedPool = null;
                mPoolListView?.RefreshItems();
                ShowEmptyState();
            }
        }

        #endregion

        #region Timed Refresh

        /// <summary>
        /// 定时刷新活跃对象使用时长。
        /// 该部分保留轮询，因为持续时间会自然增长。
        /// </summary>
        private void OnEditorUpdate()
        {
            if (!IsPlaying || mSelectedPool == default)
            {
                return;
            }

            var currentTime = EditorApplication.timeSinceStartup;
            if (currentTime - mLastAutoRefreshTime < AUTO_REFRESH_INTERVAL)
            {
                return;
            }

            mLastAutoRefreshTime = currentTime;
            UpdateActiveObjectsList();
        }

        #endregion

        #region Reactive Handlers

        /// <summary>
        /// 处理对象池列表变化。
        /// </summary>
        private void OnPoolListChanged(PoolDebugInfo info)
        {
            mRefreshThrottle.Execute(() =>
            {
                RefreshPoolList();

                if (mSelectedPool != default)
                {
                    UpdateRightPanel();
                }
            });
        }

        /// <summary>
        /// 处理活跃对象变化。
        /// 左侧列表的数量和健康状态需要保持及时反馈，因此直接刷新列表。
        /// </summary>
        private void OnPoolActiveChanged(PoolDebugInfo info)
        {
            RefreshPoolList();
        }

        /// <summary>
        /// 处理对象池事件日志变化。
        /// 仅刷新当前选中池的日志，避免无关区域重建。
        /// </summary>
        private void OnPoolEventLogged(PoolEvent evt)
        {
            if (mSelectedPool != null && evt.PoolName == mSelectedPool.Name)
            {
                mRefreshThrottle.Execute(UpdateEventLogList);
            }
        }

        /// <summary>
        /// 刷新对象池列表，并尽量保留当前选中项。
        /// 若选中池已经不存在，则清理选中态和右侧详情区。
        /// </summary>
        private void RefreshPoolList()
        {
            if (!IsPlaying)
            {
                return;
            }

            PoolDebugger.GetAllPools(mCachedPools);
            mPoolListView.itemsSource = mCachedPools;
            mPoolListView.RefreshItems();

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

                if (mSelectedPool == default)
                {
                    mPoolListView.ClearSelection();
                    ShowEmptyState();
                }
                else
                {
                    UpdateRightPanel();
                }
            }
        }

        #endregion

        /// <summary>
        /// 刷新右侧详情内容。
        /// 右侧视图始终基于当前选中的对象池快照，而不是旧缓存行。
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
        /// 显示空状态，并清理活跃对象与事件日志列表。
        /// 当选中态失效或运行时数据不可用时走这条路径。
        /// </summary>
        private void ShowEmptyState()
        {
            UpdateHudSection();

            mFilteredActiveObjects.Clear();
            if (mActiveObjectsListView != default)
            {
                mActiveObjectsListView.Rebuild();
            }

            mFilteredEvents.Clear();
            if (mEventLogListView != default)
            {
                mEventLogListView.RefreshItems();
            }
        }

        #region Helpers

        /// <summary>
        /// 创建工具栏中的健康度图例。
        /// 图例颜色与对象池卡片保持一致，方便快速理解列表状态。
        /// </summary>
        private VisualElement CreateColorLegend()
        {
            var legend = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    marginRight = 8
                }
            };
            legend.AddToClassList("yoki-kit-hero-tools");

            legend.Add(CreateLegendItem("健康", new Color(0.13f, 0.59f, 0.95f), "使用率低于 50%"));
            legend.Add(CreateLegendItem("正常", new Color(0.71f, 0.73f, 0.76f), "使用率位于 50% 到 80%"));
            legend.Add(CreateLegendItem("繁忙", new Color(1f, 0.60f, 0f), "使用率高于 80%"));

            return legend;
        }

        /// <summary>
        /// 创建单条图例项。
        /// </summary>
        private VisualElement CreateLegendItem(string label, Color color, string tooltip)
        {
            var item = new VisualElement
            {
                tooltip = tooltip,
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    marginLeft = 8
                }
            };

            var indicator = new VisualElement
            {
                style =
                {
                    width = 8,
                    height = 8,
                    backgroundColor = new StyleColor(color),
                    borderTopLeftRadius = 4,
                    borderTopRightRadius = 4,
                    borderBottomLeftRadius = 4,
                    borderBottomRightRadius = 4,
                    marginRight = 4
                }
            };
            item.Add(indicator);

            var text = new Label(label)
            {
                style =
                {
                    fontSize = 11,
                    color = new StyleColor(YokiFrameUIComponents.Colors.TextSecondary)
                }
            };
            item.Add(text);

            return item;
        }

        /// <summary>
        /// 创建头部右侧操作区。
        /// </summary>
        private VisualElement CreateHeroActions()
        {
            var container = new VisualElement();
            container.AddToClassList("yoki-kit-hero-tools");

            container.Add(CreateColorLegend());

            var trackingToggle = CreateToolbarToggle("追踪", PoolDebugger.EnableTracking, value =>
            {
                PoolDebugger.EnableTracking = value;
                RefreshStatusBanner();
            });
            trackingToggle.tooltip = "控制对象池监视数据的采集总开关。关闭后将停止记录池列表、活跃对象和事件历史。";
            container.Add(trackingToggle);

            var stackToggle = CreateToolbarToggle("堆栈", PoolDebugger.EnableStackTrace, value =>
            {
                PoolDebugger.EnableStackTrace = value;
            });
            stackToggle.tooltip = "默认关闭。开启后会为借出和归还记录调用堆栈，便于代码定位，但会增加运行时开销。";
            container.Add(stackToggle);

            return container;
        }

        /// <summary>
        /// 根据当前运行状态更新顶部状态条。
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
#endif
