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
        private VisualElement mToolbarButtons;

        // 节流器
        private Throttle mRefreshThrottle;

        #endregion

        #region 生命周期

        protected override void BuildUI(VisualElement root)
        {
            // 工具栏
            var toolbar = YokiFrameUIComponents.CreateToolbar();
            root.Add(toolbar);

            mToolbarButtons = new VisualElement();
            mToolbarButtons.style.flexDirection = FlexDirection.Row;
            toolbar.Add(mToolbarButtons);

            AddViewModeButton(KitIcons.DOT, "运行时监控", ViewMode.Runtime);
            AddViewModeButton(KitIcons.TARGET, "代码扫描", ViewMode.CodeScan);

            toolbar.Add(YokiFrameUIComponents.CreateFlexSpacer());

            // 内容区域 - 需要设置 overflow 确保子视图的 ScrollView 能正确工作
            var content = new VisualElement();
            content.AddToClassList("content-area");
            content.style.flexGrow = 1;
            content.style.overflow = Overflow.Hidden;
            content.style.paddingLeft = 0;
            content.style.paddingRight = 0;
            content.style.paddingTop = 0;
            content.style.paddingBottom = 0;
            root.Add(content);

            // 创建两个视图
            mRuntimeView = CreateRuntimeView();
            mCodeScanView = CreateCodeScanView();

            content.Add(mRuntimeView);
            content.Add(mCodeScanView);

            // 初始化节流器
            mRefreshThrottle = new Throttle(THROTTLE_INTERVAL);

            // 订阅事件
            SubscribeEvents();

            SwitchView(ViewMode.Runtime);
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

        private void AddViewModeButton(string iconId, string text, ViewMode mode)
        {
            var button = YokiFrameUIComponents.CreateToolbarButtonWithIcon(iconId, text, () => SwitchView(mode));
            button.name = $"btn_{mode}";
            mToolbarButtons.Add(button);
        }

        private void SwitchView(ViewMode mode)
        {
            mViewMode = mode;

            mRuntimeView.style.display = mode == ViewMode.Runtime ? DisplayStyle.Flex : DisplayStyle.None;
            mCodeScanView.style.display = mode == ViewMode.CodeScan ? DisplayStyle.Flex : DisplayStyle.None;

            // 更新按钮状态
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

            // 切换到运行时视图时刷新
            if (mode == ViewMode.Runtime && IsPlaying)
            {
                RefreshRuntimeView();
            }
        }

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
