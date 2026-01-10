using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using YokiFrame.EditorTools;
using static YokiFrame.EditorTools.YokiFrameUIComponents;

namespace YokiFrame
{
    /// <summary>
    /// UIKit 工具页面 - 集成创建面板、调试、绑定检查、验证器功能
    /// 采用响应式数据绑定，通过订阅面板事件实现自动更新
    /// </summary>
    public partial class UIKitToolPage : YokiFrameToolPageBase
    {
        public override string PageName => "UIKit";
        public override string PageIcon => KitIcons.UIKIT;
        public override int Priority => 30;

        #region 常量

        private const float THROTTLE_INTERVAL = 0.2f;  // 节流间隔 200ms

        #endregion

        #region 标签页枚举

        private enum TabType
        {
            CreatePanel,    // 创建面板
            Debug,          // 调试
            BindInspector,  // 绑定检查
            Validator       // 验证器
        }

        #endregion

        #region 字段 - 通用

        private TabType mCurrentTab = TabType.CreatePanel;
        private VisualElement mTabContent;
        private Button mCreatePanelTabBtn;
        private Button mDebugTabBtn;
        private Button mBindInspectorTabBtn;
        private Button mValidatorTabBtn;
        
        // 响应式更新
        private Throttle mRefreshThrottle;

        #endregion

        #region 构建 UI

        protected override void BuildUI(VisualElement root)
        {
            // 标签栏
            var tabBar = new VisualElement();
            tabBar.style.flexDirection = FlexDirection.Row;
            tabBar.style.borderBottomWidth = 1;
            tabBar.style.borderBottomColor = new StyleColor(Colors.BorderLight);
            tabBar.style.backgroundColor = new StyleColor(Colors.LayerTabBar);
            root.Add(tabBar);

            mCreatePanelTabBtn = CreateTabButton("创建面板", TabType.CreatePanel);
            mDebugTabBtn = CreateTabButton("调试", TabType.Debug);
            mBindInspectorTabBtn = CreateTabButton("绑定检查", TabType.BindInspector);
            mValidatorTabBtn = CreateTabButton("验证器", TabType.Validator);

            tabBar.Add(mCreatePanelTabBtn);
            tabBar.Add(mDebugTabBtn);
            tabBar.Add(mBindInspectorTabBtn);
            tabBar.Add(mValidatorTabBtn);

            // 内容区域
            mTabContent = new VisualElement { style = { flexGrow = 1 } };
            root.Add(mTabContent);

            // 初始化响应式订阅
            SetupReactiveSubscriptions();

            SwitchTab(TabType.CreatePanel);
        }

        #endregion

        #region 响应式订阅

        /// <summary>
        /// 设置响应式订阅 - 监听面板状态变化事件
        /// </summary>
        private void SetupReactiveSubscriptions()
        {
            // 创建节流器
            mRefreshThrottle = CreateThrottle(THROTTLE_INTERVAL);
            
            // 订阅面板打开事件
            SubscribeChannel<IPanel>(DataChannels.PANEL_OPENED, OnPanelOpened);
            
            // 订阅面板关闭事件
            SubscribeChannel<IPanel>(DataChannels.PANEL_CLOSED, OnPanelClosed);
            
            // 订阅焦点变化事件
            SubscribeChannel<GameObject>(DataChannels.FOCUS_CHANGED, OnFocusChanged);
        }
        
        private void OnPanelOpened(IPanel panel)
        {
            if (mCurrentTab != TabType.Debug) return;
            if (!mDebugAutoRefresh) return;
            mRefreshThrottle.Execute(RefreshDebugContent);
        }
        
        private void OnPanelClosed(IPanel panel)
        {
            if (mCurrentTab != TabType.Debug) return;
            if (!mDebugAutoRefresh) return;
            mRefreshThrottle.Execute(RefreshDebugContent);
        }
        
        private void OnFocusChanged(GameObject focusObj)
        {
            if (mCurrentTab != TabType.Debug) return;
            if (!mDebugAutoRefresh || !mShowFocusInfo) return;
            mRefreshThrottle.Execute(RefreshDebugContent);
        }

        #endregion

        #region 标签页切换

        private Button CreateTabButton(string text, TabType tabType)
        {
            var btn = new Button(() => SwitchTab(tabType)) { text = text };
            btn.style.paddingLeft = Spacing.LG;
            btn.style.paddingRight = Spacing.LG;
            btn.style.paddingTop = Spacing.SM + 2;
            btn.style.paddingBottom = Spacing.SM + 2;
            btn.style.borderLeftWidth = btn.style.borderRightWidth = btn.style.borderTopWidth = 0;
            btn.style.borderBottomWidth = 2;
            btn.style.borderBottomColor = new StyleColor(Color.clear);
            btn.style.backgroundColor = StyleKeyword.Null;
            btn.style.color = new StyleColor(Colors.TextSecondary);
            return btn;
        }

        private void UpdateTabButtonStyles()
        {
            UpdateSingleTabStyle(mCreatePanelTabBtn, mCurrentTab == TabType.CreatePanel);
            UpdateSingleTabStyle(mDebugTabBtn, mCurrentTab == TabType.Debug);
            UpdateSingleTabStyle(mBindInspectorTabBtn, mCurrentTab == TabType.BindInspector);
            UpdateSingleTabStyle(mValidatorTabBtn, mCurrentTab == TabType.Validator);
        }

        private void UpdateSingleTabStyle(Button btn, bool isActive)
        {
            btn.style.borderBottomColor = new StyleColor(isActive ? Colors.BrandPrimary : Color.clear);
            btn.style.color = new StyleColor(isActive ? Colors.TextPrimary : Colors.TextTertiary);
            btn.style.backgroundColor = new StyleColor(isActive ? Colors.LayerSection : Color.clear);
        }

        private void SwitchTab(TabType tabType)
        {
            mCurrentTab = tabType;
            mTabContent.Clear();
            UpdateTabButtonStyles();

            switch (tabType)
            {
                case TabType.CreatePanel:
                    BuildCreatePanelUI(mTabContent);
                    break;
                case TabType.Debug:
                    BuildDebugUI(mTabContent);
                    break;
                case TabType.BindInspector:
                    BuildBindInspectorUI(mTabContent);
                    break;
                case TabType.Validator:
                    BuildValidatorUI(mTabContent);
                    break;
            }
        }

        #endregion

        #region 通用辅助方法

        private Button CreateSmallButton(string text, Action onClick)
        {
            var btn = new Button(onClick) { text = text };
            btn.style.height = 20;
            btn.style.paddingLeft = Spacing.SM;
            btn.style.paddingRight = Spacing.SM;
            btn.style.marginLeft = Spacing.XS;
            btn.style.fontSize = 11;
            return btn;
        }

        #endregion

        #region 更新

        [Obsolete("保留用于绑定检查标签页轮询刷新")]
        public override void OnUpdate()
        {
            // 响应式模式下，调试标签页不再需要轮询
            // 绑定检查标签页仍使用轮询（因为绑定数据没有事件通知）
            if (mCurrentTab == TabType.BindInspector && mBindAutoRefresh && mBindTargetRoot != null)
            {
                if (EditorApplication.timeSinceStartup - mBindLastRefreshTime > BIND_REFRESH_INTERVAL)
                {
                    mBindLastRefreshTime = EditorApplication.timeSinceStartup;
                    RefreshBindings();
                    RefreshBindContent();
                }
            }
        }

        #endregion
    }
}
