using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// ActionKit Flexbox 布局监控面板
    /// 使用 Flexbox 可视化区分串行(Sequence)和并行(Parallel)逻辑
    /// 采用响应式数据绑定，通过订阅 Action 事件实现增量更新
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

        private const float INTERACTION_COOLDOWN = 2.0f;    // 交互保护 2 秒
        private const float THROTTLE_INTERVAL = 0.2f;       // 节流间隔 200ms
        private const float CARD_BORDER_RADIUS = 6f;
        private const float CARD_MARGIN = 4f;
        private const float CARD_PADDING = 8f;

        // 配色常量
        private static readonly Color COLOR_SEQUENCE = new(0.6f, 0.4f, 0.8f);
        private static readonly Color COLOR_PARALLEL = new(0.9f, 0.6f, 0.3f);
        private static readonly Color COLOR_LEAF_DELAY = new(0.3f, 0.6f, 0.9f);
        private static readonly Color COLOR_LEAF_CALLBACK = new(0.4f, 0.8f, 0.5f);
        private static readonly Color COLOR_LEAF_LERP = new(0.9f, 0.5f, 0.7f);
        private static readonly Color COLOR_LEAF_REPEAT = new(0.9f, 0.4f, 0.4f);
        private static readonly Color COLOR_RUNNING = new(0.3f, 0.9f, 0.4f);
        private static readonly Color COLOR_FINISHED = new(0.5f, 0.5f, 0.5f);
        private static readonly Color CARD_BG = new(0.22f, 0.22f, 0.22f);
        private static readonly Color CARD_BG_DARK = new(0.18f, 0.18f, 0.18f);

        #endregion

        #region 字段

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

        // 响应式更新相关
        private Throttle mRefreshThrottle;

        #endregion

        #region 入口

        protected override void BuildUI(VisualElement root)
        {
            root.style.backgroundColor = new StyleColor(new Color(0.15f, 0.15f, 0.15f));

            var scrollView = new ScrollView(ScrollViewMode.VerticalAndHorizontal);
            scrollView.style.flexGrow = 1;
            scrollView.style.paddingLeft = 16;
            scrollView.style.paddingRight = 16;
            scrollView.style.paddingTop = 16;
            root.Add(scrollView);

            BuildHeader(scrollView);
            BuildStatsCard(scrollView);
            BuildStackSettings(scrollView);
            BuildFlexTreeCard(scrollView);
            BuildStackTraceCard(scrollView);

            // 初始化响应式订阅
            SetupReactiveSubscriptions();

            RefreshData();
        }

        #endregion

        #region 响应式订阅

        /// <summary>
        /// 设置响应式订阅 - 监听 Action 开始/结束事件
        /// </summary>
        private void SetupReactiveSubscriptions()
        {
            // 创建节流器，避免频繁刷新
            mRefreshThrottle = CreateThrottle(THROTTLE_INTERVAL);

            // 订阅 Action 开始事件
            SubscribeChannel<IAction>(DataChannels.ACTION_STARTED, OnActionStarted);

            // 订阅 Action 结束事件
            SubscribeChannel<IAction>(DataChannels.ACTION_FINISHED, OnActionFinished);
        }

        /// <summary>
        /// Action 开始时的回调
        /// </summary>
        private void OnActionStarted(IAction action)
        {
            if (!Application.isPlaying) return;
            if (IsInteractionCooldown()) return;

            // 使用节流器延迟刷新
            mRefreshThrottle.Execute(RefreshData);
        }

        /// <summary>
        /// Action 结束时的回调
        /// </summary>
        private void OnActionFinished(IAction action)
        {
            if (!Application.isPlaying) return;
            if (IsInteractionCooldown()) return;

            mTotalFinished++;

            // 使用节流器延迟刷新
            mRefreshThrottle.Execute(RefreshData);
        }

        /// <summary>
        /// 检查是否在交互冷却期内
        /// </summary>
        private bool IsInteractionCooldown()
            => EditorApplication.timeSinceStartup - mLastInteractionTime < INTERACTION_COOLDOWN;

        #endregion

        #region 生命周期

        public override void OnActivate()
        {
            base.OnActivate();
            
            // 重新设置响应式订阅（页面切换时需要重新绑定）
            SetupReactiveSubscriptions();
            
            // 强制刷新数据（修复页面切换后不更新的问题）
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
                if (mClearStackOnExit) ActionStackTraceService.Clear();
                mStackTraceLabel.text = "点击卡片查看堆栈";
                mStackTraceLabel.style.color = new StyleColor(new Color(0.6f, 0.6f, 0.6f));
                mStackCountLabel.text = $"已记录: {ActionStackTraceService.Count}";
            }
            RefreshData();
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
            _ => new Color(0.7f, 0.7f, 0.7f)
        };

        private string GetTypeIcon(string typeName) => typeName switch
        {
            "Sequence" => "▶",
            "Parallel" => "⊞",
            "Repeat" => "↻",
            "Delay" or "DelayFrame" => "⏱",
            "Callback" => "λ",
            "Lerp" => "~",
            _ => "•"
        };

        #endregion
    }
}
