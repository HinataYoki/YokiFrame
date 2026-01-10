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
    /// EventKit 工具页面 - 响应式版本
    /// 页面一：运行时监控（心电图）- 热度和时间轴
    /// 页面二：静态代码扫描（电路图）- 连线和流向
    /// </summary>
    public partial class EventKitToolPage : YokiFrameToolPageBase
    {
        public override string PageName => "EventKit";
        public override string PageIcon => KitIcons.EVENTKIT;
        public override int Priority => 10;

        #region 常量

        private const float THROTTLE_INTERVAL = 0.1f;
        private const float ACTIVITY_FLASH_DURATION = 0.1f;
        private const float ACTIVITY_RECENT_DURATION = 1.0f;

        #endregion

        #region 枚举

        private enum ViewMode { Runtime, CodeScan }
        private enum EventTypeFilter { All, Enum, Type, String }

        #endregion

        #region 私有字段

        private ViewMode mViewMode = ViewMode.Runtime;
        // TODO: 后续实现类型过滤功能时启用
        // private EventTypeFilter mTypeFilter = EventTypeFilter.All;

        // UI 元素引用
        private VisualElement mRuntimeView;
        private VisualElement mCodeScanView;
        private YokiFrameUIComponents.TabView mTabView;

        // 节流器
        private Throttle mRefreshThrottle;

        #endregion

        #region 生命周期

        protected override void BuildUI(VisualElement root)
        {
            // 创建两个视图
            mRuntimeView = CreateRuntimeView();
            mCodeScanView = CreateCodeScanView();

            // 使用统一的标签页视图组件
            mTabView = YokiFrameUIComponents.CreateTabView(
                ("运行时监控", mRuntimeView, KitIcons.DOT),
                ("代码扫描", mCodeScanView, KitIcons.TARGET)
            );
            mTabView.OnTabChanged += OnTabChanged;
            root.Add(mTabView.Root);

            // 初始化节流器
            mRefreshThrottle = new Throttle(THROTTLE_INTERVAL);

            // 订阅事件
            SubscribeEvents();
        }

        /// <summary>
        /// 标签页切换回调
        /// </summary>
        private void OnTabChanged(int index)
        {
            mViewMode = index == 0 ? ViewMode.Runtime : ViewMode.CodeScan;
            
            // 切换到运行时视图时刷新
            if (mViewMode == ViewMode.Runtime && IsPlaying)
            {
                RefreshRuntimeView();
            }
        }

        public override void OnActivate()
        {
            base.OnActivate();

            if (mViewMode == ViewMode.Runtime && IsPlaying)
            {
                RefreshRuntimeView();
            }
        }

        /// <summary>
        /// PlayMode 状态变化时重新订阅事件
        /// </summary>
        protected override void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            base.OnPlayModeStateChanged(state);
            
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                // 进入 PlayMode 时重新订阅（基类在 ExitingPlayMode 时会清理订阅）
                SubscribeEvents();
                RefreshRuntimeView();
            }
        }

        public override void OnUpdate()
        {
            // 更新活跃状态动画
            if (IsPlaying && mViewMode == ViewMode.Runtime)
            {
                UpdateActivityStates();
                
                // 更新飞行脉冲和溶解动画
                YokiFrameUIComponents.UpdateAllFlightPulses();
                YokiFrameUIComponents.UpdateAllDissolves();
            }
        }

        #endregion

        #region 事件订阅

        private void SubscribeEvents()
        {
            // 先清理旧订阅，避免重复订阅
            Subscriptions.Clear();
            
            // 订阅事件触发通知
            Subscriptions.Add(EditorDataBridge.Subscribe<(string eventType, string eventKey, string args)>(
                DataChannels.EVENT_TRIGGERED,
                OnEventTriggered));

            // 订阅事件注册/注销通知
            Subscriptions.Add(EditorDataBridge.Subscribe<object>(
                DataChannels.EVENT_REGISTERED,
                _ => RequestRefresh()));

            Subscriptions.Add(EditorDataBridge.Subscribe<object>(
                DataChannels.EVENT_UNREGISTERED,
                _ => RequestRefresh()));
        }

        private void RequestRefresh()
        {
            if (!IsPlaying || mViewMode != ViewMode.Runtime) return;
            mRefreshThrottle.Execute(RefreshRuntimeView);
        }

        #endregion

        #region 视图切换

        /// <summary>
        /// 切换到运行时监控标签页
        /// </summary>
        public void SwitchToRuntimeView() => mTabView?.SwitchTo(0);

        /// <summary>
        /// 切换到代码扫描标签页
        /// </summary>
        public void SwitchToCodeScanView() => mTabView?.SwitchTo(1);

        #endregion

        #region 工具方法

        private static void OpenFileAtLine(string filePath, int line)
        {
            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(filePath);
            if (asset != null)
                AssetDatabase.OpenAsset(asset, line);
        }

        /// <summary>
        /// 获取事件类型对应的颜色
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

        #endregion
    }
}
#endif
