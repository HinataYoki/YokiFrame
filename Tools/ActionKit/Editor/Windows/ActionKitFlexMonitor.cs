using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using static YokiFrame.EditorTools.YokiFrameUIComponents;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// ActionKit 运行时流程监控页。
    /// 用于在编辑器中观察当前 Action 树、执行概况与堆栈追踪状态。
    /// </summary>
    [YokiToolPage(
        kit: "ActionKit",
        name: "ActionKit",
        icon: KitIcons.ACTIONKIT,
        priority: 30,
        category: YokiPageCategory.Tool)]
    public partial class ActionKitFlexMonitor : YokiToolPageBase
    {
        #region 常量

        private const float INTERACTION_COOLDOWN = 2.0f;
        private const float THROTTLE_INTERVAL = 0.2f;

        private static readonly Color COLOR_SEQUENCE = new(0.53f, 0.43f, 0.72f);
        private static readonly Color COLOR_PARALLEL = new(0.78f, 0.58f, 0.35f);
        private static readonly Color COLOR_LEAF_DELAY = new(0.34f, 0.58f, 0.84f);
        private static readonly Color COLOR_LEAF_CALLBACK = new(0.42f, 0.70f, 0.52f);
        private static readonly Color COLOR_LEAF_LERP = new(0.80f, 0.48f, 0.64f);
        private static readonly Color COLOR_LEAF_REPEAT = new(0.80f, 0.40f, 0.40f);
        private static readonly Color COLOR_RUNNING = new(0.38f, 0.79f, 0.50f);
        private static readonly Color COLOR_FINISHED = new(0.52f, 0.56f, 0.62f);

        #endregion

        #region 字段

        private VisualElement mStatusContainer;
        private VisualElement mTreeContainer;
        private Label mActiveCountLabel;
        private Label mTotalFinishedLabel;
        private Label mStackTraceLabel;
        private Label mStackCountLabel;

        private readonly List<IAction> mActiveActions = new(32);
        private readonly List<string> mExecutorNames = new(32);
        private readonly Dictionary<ulong, VisualElement> mNodeCache = new(64);

        private double mLastInteractionTime;
        private ulong mSelectedActionId;
        private int mTotalFinished;
        private bool mClearStackOnExit = true;
        private Throttle mRefreshThrottle;

        #endregion

        #region 入口

        protected override void BuildUI(VisualElement root)
        {
            var scaffold = CreateKitPageScaffold(
                "ActionKit",
                "统一查看 Action 运行时流程、根执行器分布与堆栈追踪状态，便于排查串行、并行与重复动作的执行问题。",
                KitIcons.ACTIONKIT,
                "动作工作台");
            root.Add(scaffold.Root);

            mStatusContainer = scaffold.StatusBar;
            RefreshStatusBanner();

            var scrollView = new ScrollView(ScrollViewMode.Vertical);
            scrollView.AddToClassList("yoki-action-workbench");
            scrollView.style.flexGrow = 1;
            scaffold.Content.Add(scrollView);

            BuildHeader(scrollView);
            BuildStatsCard(scrollView);
            BuildStackSettings(scrollView);
            BuildFlexTreeCard(scrollView);
            BuildStackTraceCard(scrollView);

            SetupReactiveSubscriptions();
            RefreshData();
        }

        #endregion

        #region 响应式订阅

        /// <summary>
        /// 注册 ActionKit 监控页依赖的编辑器通信通道。
        /// </summary>
        private void SetupReactiveSubscriptions()
        {
            mRefreshThrottle = CreateThrottle(THROTTLE_INTERVAL);
            SubscribeChannel<IAction>(DataChannels.ACTION_STARTED, OnActionStarted);
            SubscribeChannel<IAction>(DataChannels.ACTION_FINISHED, OnActionFinished);
        }

        /// <summary>
        /// Action 开始时触发的刷新回调。
        /// </summary>
        private void OnActionStarted(IAction action)
        {
            if (!Application.isPlaying || IsInteractionCooldown())
            {
                return;
            }

            mRefreshThrottle.Execute(RefreshData);
        }

        /// <summary>
        /// Action 结束时触发的刷新回调。
        /// </summary>
        private void OnActionFinished(IAction action)
        {
            if (!Application.isPlaying || IsInteractionCooldown())
            {
                return;
            }

            mTotalFinished++;
            mRefreshThrottle.Execute(RefreshData);
        }

        /// <summary>
        /// 判断当前是否处于交互保护期，避免点击节点时界面频繁跳动。
        /// </summary>
        private bool IsInteractionCooldown()
            => EditorApplication.timeSinceStartup - mLastInteractionTime < INTERACTION_COOLDOWN;

        #endregion

        #region 生命周期

        public override void OnActivate()
        {
            base.OnActivate();
            SetupReactiveSubscriptions();
            RefreshStatusBanner();
            RefreshData();
        }

        public override void OnDeactivate() => base.OnDeactivate();

        protected override void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            base.OnPlayModeStateChanged(state);

            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                mTotalFinished = 0;
                mSelectedActionId = 0;
                mNodeCache.Clear();

                if (mClearStackOnExit)
                {
                    ActionStackTraceService.Clear();
                }

                if (mStackTraceLabel != null)
                {
                    mStackTraceLabel.text = "点击流程节点后，可在这里查看对应的调用堆栈。";
                    mStackTraceLabel.style.color = new StyleColor(Colors.TextTertiary);
                }

                if (mStackCountLabel != null)
                {
                    mStackCountLabel.text = $"已记录: {ActionStackTraceService.Count}";
                }
            }

            RefreshStatusBanner();
            RefreshData();
        }

        #endregion

        #region 状态栏

        /// <summary>
        /// 根据运行状态刷新顶部状态说明。
        /// </summary>
        private void RefreshStatusBanner()
        {
            if (Application.isPlaying)
            {
                SetStatusBanner(
                    mStatusContainer,
                    "运行时监控",
                    ActionStackTraceService.Enabled
                        ? "当前正在监控 Action 运行时流程。堆栈追踪已开启，可点击节点查看记录。"
                        : "当前正在监控 Action 运行时流程。堆栈追踪默认关闭，如需定位调用来源请手动开启。");
                return;
            }

            SetStatusBanner(
                mStatusContainer,
                "运行时监控",
                "该页用于观察 PlayMode 下的 Action 树、执行器和堆栈追踪状态。未进入运行模式时仅显示工作台骨架与配置入口。");
        }

        #endregion

        #region 辅助方法

        private Color GetTypeColor(string typeName) => typeName switch
        {
            "Sequence" => COLOR_SEQUENCE,
            "Parallel" => COLOR_PARALLEL,
            "Repeat" => COLOR_LEAF_REPEAT,
            "Delay" or "DelayFrame" => COLOR_LEAF_DELAY,
            "Callback" => COLOR_LEAF_CALLBACK,
            "Lerp" => COLOR_LEAF_LERP,
            _ => Colors.TextSecondary
        };

        private string GetTypeIcon(string typeName) => typeName switch
        {
            "Sequence" => "顺",
            "Parallel" => "并",
            "Repeat" => "循",
            "Delay" or "DelayFrame" => "延",
            "Callback" => "回",
            "Lerp" => "插",
            _ => "动"
        };

        #endregion
    }
}
