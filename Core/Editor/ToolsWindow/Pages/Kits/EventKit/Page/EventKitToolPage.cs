#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using YokiFrame.EditorTools;

namespace YokiFrame
{
    /// <summary>
    /// EventKit 工具页。
    /// 提供运行时监视与代码扫描两种工作模式。
    /// </summary>
    [YokiToolPage(
        kit: "EventKit",
        name: "EventKit",
        icon: KitIcons.EVENTKIT,
        priority: 10,
        category: YokiPageCategory.Tool)]
    public partial class EventKitToolPage : YokiToolPageBase
    {
        #region Constants

        private const float THROTTLE_INTERVAL = 0.1f;
        private const float ACTIVITY_FLASH_DURATION = 0.1f;
        private const float ACTIVITY_RECENT_DURATION = 1.0f;

        #endregion

        #region Enums

        private enum ViewMode
        {
            Runtime,
            CodeScan
        }

        private enum EventTypeFilter
        {
            All,
            Enum,
            Type,
            String
        }

        #endregion

        #region Fields

        private ViewMode mViewMode = ViewMode.Runtime;
        // If event-type filtering is re-enabled later, restore this field.
        // private EventTypeFilter mTypeFilter = EventTypeFilter.All;

        private VisualElement mRuntimeView;
        private VisualElement mCodeScanView;
        private YokiFrameUIComponents.TabView mTabView;
        private VisualElement mRuntimeStatusBanner;

        private Throttle mRefreshThrottle;

        #endregion

        #region Lifecycle

        /// <summary>
        /// 构建 EventKit 页面外壳并挂载两种工作模式视图。
        /// </summary>
        protected override void BuildUI(VisualElement root)
        {
            var scaffold = CreateKitPageScaffold(
                "EventKit",
                "统一查看运行时事件流、订阅关系与代码扫描结果。",
                KitIcons.EVENTKIT,
                "KIT 事件中心");
            root.Add(scaffold.Root);
            scaffold.Toolbar.style.display = DisplayStyle.None;
            scaffold.StatusBar.style.display = DisplayStyle.None;

            mRuntimeStatusBanner = CreateKitStatusBanner(
                "运行模式说明",
                "当前未进入 PlayMode。进入 PlayMode 后可查看运行时事件流与触发明细；代码扫描页不受影响。",
                YokiFrameUIComponents.HelpBoxType.Info);

            mRuntimeView = CreateRuntimeView();
            mCodeScanView = CreateCodeScanView();

            mTabView = YokiFrameUIComponents.CreateTabView(
                ("运行时监视", mRuntimeView, KitIcons.DOT),
                ("代码扫描", mCodeScanView, KitIcons.TARGET)
            );
            mTabView.OnTabChanged += OnTabChanged;
            scaffold.Content.Add(mTabView.Root);

            mRefreshThrottle = new Throttle(THROTTLE_INTERVAL);
            SubscribeEvents();
            RefreshStatusBanner();
        }

        /// <summary>
        /// 切换顶层模式。
        /// </summary>
        private void OnTabChanged(int index)
        {
            mViewMode = index == 0 ? ViewMode.Runtime : ViewMode.CodeScan;
            RefreshStatusBanner();

            if (mViewMode == ViewMode.Runtime && IsPlaying)
            {
                RefreshRuntimeView();
            }
        }

        /// <summary>
        /// 页面激活时在可用情况下刷新运行时视图。
        /// </summary>
        public override void OnActivate()
        {
            base.OnActivate();
            RefreshStatusBanner();

            if (mViewMode == ViewMode.Runtime && IsPlaying)
            {
                RefreshRuntimeView();
            }
        }

        /// <summary>
        /// PlayMode 切换后刷新运行时订阅与提示条。
        /// </summary>
        protected override void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            base.OnPlayModeStateChanged(state);
            RefreshStatusBanner();

            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                SubscribeEvents();
                RefreshRuntimeView();
            }
        }

        /// <summary>
        /// 仅保留轻量动画刷新。
        /// </summary>
        [Obsolete("Retained only for UI animation refresh.")]
        public override void OnUpdate()
        {
            if (IsPlaying && mViewMode == ViewMode.Runtime)
            {
                UpdateActivityStates();
                YokiFrameUIComponents.UpdateAllFlightPulses();
                YokiFrameUIComponents.UpdateAllDissolves();
            }
        }

        #endregion

        #region Event Subscription

        /// <summary>
        /// 重建页面级事件订阅。
        /// </summary>
        private void SubscribeEvents()
        {
            Subscriptions.Clear();

            SubscribeChannel<(string eventType, string eventKey, string args)>(
                DataChannels.EVENT_TRIGGERED,
                OnEventTriggered);

            SubscribeChannel<object>(
                DataChannels.EVENT_REGISTERED,
                _ => RequestRefresh());

            SubscribeChannel<object>(
                DataChannels.EVENT_UNREGISTERED,
                _ => RequestRefresh());
        }

        /// <summary>
        /// 仅在运行时监视模式下请求节流刷新。
        /// </summary>
        private void RequestRefresh()
        {
            if (!IsPlaying || mViewMode != ViewMode.Runtime)
            {
                return;
            }

            mRefreshThrottle.Execute(RefreshRuntimeView);
        }

        #endregion

        #region View Switching

        /// <summary>
        /// 切换到运行时监视模式。
        /// </summary>
        public void SwitchToRuntimeView() => mTabView?.SwitchTo(0);

        /// <summary>
        /// 切换到代码扫描模式。
        /// </summary>
        public void SwitchToCodeScanView() => mTabView?.SwitchTo(1);

        #endregion

        #region Helpers

        /// <summary>
        /// 打开指定代码文件与行号。
        /// </summary>
        private static void OpenFileAtLine(string filePath, int line)
        {
            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(filePath);
            if (asset != null)
            {
                AssetDatabase.OpenAsset(asset, line);
            }
        }

        /// <summary>
        /// 返回事件类型徽章颜色。
        /// </summary>
        private static (Color bg, Color border, Color text) GetEventTypeColors(string eventType)
        {
            return eventType switch
            {
                "Enum" => (
                    new Color(0.15f, 0.25f, 0.15f, 0.5f),
                    new Color(0.3f, 0.7f, 0.3f),
                    new Color(0.6f, 0.9f, 0.6f)
                ),
                "Type" => (
                    new Color(0.15f, 0.2f, 0.3f, 0.5f),
                    new Color(0.3f, 0.5f, 0.9f),
                    new Color(0.6f, 0.7f, 1f)
                ),
                "String" => (
                    new Color(0.3f, 0.2f, 0.1f, 0.5f),
                    new Color(0.9f, 0.6f, 0.2f),
                    new Color(1f, 0.8f, 0.4f)
                ),
                _ => (
                    new Color(0.2f, 0.2f, 0.2f, 0.5f),
                    new Color(0.5f, 0.5f, 0.5f),
                    new Color(0.8f, 0.8f, 0.8f)
                )
            };
        }

        /// <summary>
        /// 根据当前模式与运行状态更新运行时提示条。
        /// </summary>
        private void RefreshStatusBanner()
        {
            if (mRuntimeStatusBanner == null)
            {
                return;
            }

            mRuntimeStatusBanner.style.display =
                mViewMode == ViewMode.Runtime && !IsPlaying
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
        }

        #endregion
    }
}
#endif
