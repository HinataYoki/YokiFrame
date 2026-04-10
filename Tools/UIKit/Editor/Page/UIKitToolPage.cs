using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using YokiFrame.EditorTools;
using static YokiFrame.EditorTools.YokiFrameUIComponents;

namespace YokiFrame
{
    /// <summary>
    /// UIKit 工具页总入口。
    /// 聚合创建面板、全局运行时监控、批量验证中心和设置页，并接入统一工作台外壳。
    /// </summary>
    [YokiToolPage(
        kit: "UIKit",
        name: "UIKit",
        icon: KitIcons.UIKIT,
        priority: 30,
        category: YokiPageCategory.Tool)]
    public partial class UIKitToolPage : YokiToolPageBase
    {
        /// <summary>
        /// 运行时监控页收到连续事件时的刷新节流间隔。
        /// </summary>
        private const float THROTTLE_INTERVAL = 0.2f;

        /// <summary>
        /// UIKit 工具页内部功能页签。
        /// </summary>
        private enum TabType
        {
            CreatePanel,
            Debug,
            Validator,
            Settings
        }

        private TabType mCurrentTab = TabType.CreatePanel;
        private VisualElement mTabContent;
        private VisualElement mStatusContainer;
        private Button mCreatePanelTabBtn;
        private Button mDebugTabBtn;
        private Button mValidatorTabBtn;
        private Button mSettingsTabBtn;

        /// <summary>
        /// 用于合并短时间内的连续刷新请求。
        /// </summary>
        private Throttle mRefreshThrottle;

        /// <summary>
        /// 构建 UIKit 页面骨架与页签入口。
        /// </summary>
        protected override void BuildUI(VisualElement root)
        {
            var scaffold = CreateKitPageScaffold(
                "UIKit",
                "统一进入面板创建、全局运行时监控、批量验证中心与设置页，单面板绑定和校验能力下放到 UIPanel Inspector。",
                KitIcons.UIKIT,
                "界面工作台");
            root.Add(scaffold.Root);

            mStatusContainer = scaffold.StatusBar;

            var tabBar = new VisualElement();
            tabBar.style.flexDirection = FlexDirection.Row;
            tabBar.style.borderBottomWidth = 1;
            tabBar.style.borderBottomColor = new StyleColor(Colors.BorderLight);
            tabBar.style.backgroundColor = new StyleColor(Colors.LayerTabBar);
            scaffold.Toolbar.Add(tabBar);

            mCreatePanelTabBtn = CreateTabButton("创建面板", TabType.CreatePanel);
            mDebugTabBtn = CreateTabButton("运行时监控", TabType.Debug);
            mValidatorTabBtn = CreateTabButton("批量验证", TabType.Validator);
            mSettingsTabBtn = CreateTabButton("设置", TabType.Settings);

            tabBar.Add(mCreatePanelTabBtn);
            tabBar.Add(mDebugTabBtn);
            tabBar.Add(mValidatorTabBtn);
            tabBar.Add(mSettingsTabBtn);

            mTabContent = new VisualElement { style = { flexGrow = 1 } };
            scaffold.Content.Add(mTabContent);

            SetupReactiveSubscriptions();
            SwitchTab(mCurrentTab);
        }

        /// <summary>
        /// 注册 UIKit 工具页依赖的运行时事件订阅。
        /// </summary>
        private void SetupReactiveSubscriptions()
        {
            mRefreshThrottle = CreateThrottle(THROTTLE_INTERVAL);

            SubscribeChannel<IPanel>(DataChannels.PANEL_OPENED, OnPanelOpened);
            SubscribeChannel<IPanel>(DataChannels.PANEL_CLOSED, OnPanelClosed);
            SubscribeChannel<GameObject>(DataChannels.FOCUS_CHANGED, OnFocusChanged);
        }

        private void OnPanelOpened(IPanel panel)
        {
            if (mCurrentTab != TabType.Debug || !mDebugAutoRefresh)
            {
                return;
            }

            mRefreshThrottle.Execute(RefreshDebugContent);
        }

        private void OnPanelClosed(IPanel panel)
        {
            if (mCurrentTab != TabType.Debug || !mDebugAutoRefresh)
            {
                return;
            }

            mRefreshThrottle.Execute(RefreshDebugContent);
        }

        private void OnFocusChanged(GameObject focusObj)
        {
            if (mCurrentTab != TabType.Debug || !mDebugAutoRefresh || !mShowFocusInfo)
            {
                return;
            }

            mRefreshThrottle.Execute(RefreshDebugContent);
        }

        private Button CreateTabButton(string text, TabType tabType)
        {
            var btn = new Button(() => SwitchTab(tabType)) { text = text };
            btn.style.paddingLeft = Spacing.LG;
            btn.style.paddingRight = Spacing.LG;
            btn.style.paddingTop = Spacing.SM + 2;
            btn.style.paddingBottom = Spacing.SM + 2;
            btn.style.borderLeftWidth = 0;
            btn.style.borderRightWidth = 0;
            btn.style.borderTopWidth = 0;
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
            UpdateSingleTabStyle(mValidatorTabBtn, mCurrentTab == TabType.Validator);
            UpdateSingleTabStyle(mSettingsTabBtn, mCurrentTab == TabType.Settings);
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
            RefreshStatusBanner();

            switch (tabType)
            {
                case TabType.CreatePanel:
                    BuildCreatePanelUI(mTabContent);
                    break;
                case TabType.Debug:
                    BuildDebugUI(mTabContent);
                    break;
                case TabType.Validator:
                    BuildValidatorUI(mTabContent);
                    break;
                case TabType.Settings:
                    BuildSettingsUI(mTabContent);
                    break;
            }
        }

        private void RefreshStatusBanner()
        {
            switch (mCurrentTab)
            {
                case TabType.CreatePanel:
                    SetStatusBanner(mStatusContainer, "创建面板", "用于快速生成 UIKit 面板脚本与目录结构，不依赖 PlayMode。");
                    break;
                case TabType.Debug:
                    SetStatusBanner(
                        mStatusContainer,
                        "全局运行时监控",
                        Application.isPlaying
                            ? "用于观察当前 UIKit 的全局面板栈、焦点和缓存状态，不替代单面板 Inspector 调试。"
                            : "运行时监控建议在 PlayMode 下查看，当前仍可预览监控布局。");
                    break;
                case TabType.Validator:
                    SetStatusBanner(
                        mStatusContainer,
                        "批量验证中心",
                        "该页仅负责场景级批量扫描与问题导航；单个 UIPanel 的绑定树和验证结果请在 Inspector 中查看。");
                    break;
                case TabType.Settings:
                    SetStatusBanner(mStatusContainer, "设置", "集中管理 UIKit 编辑器工具的默认行为与偏好配置。");
                    break;
            }
        }

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
    }
}
