using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using YokiFrame.EditorTools;

namespace YokiFrame
{
    /// <summary>
    /// AudioKit 主工具页。
    /// 负责在运行时音频监控与音频 ID 代码生成两个工作区之间切换，并接入统一工作台外壳。
    /// </summary>
    [YokiToolPage(
        kit: "AudioKit",
        name: "AudioKit",
        icon: KitIcons.AUDIOKIT,
        priority: 40,
        category: YokiPageCategory.Tool)]
    public partial class AudioKitToolPage : YokiToolPageBase
    {
        private enum TabType
        {
            CodeGenerator,
            RuntimeMonitor
        }

        private const string ASSETS_PREFIX = "Assets";
        private const float THROTTLE_INTERVAL = 0.15f;
        private const double REFRESH_INTERVAL = 0.1;

        private static readonly string[] AUDIO_EXTENSIONS = { ".wav", ".mp3", ".ogg", ".aiff", ".aif", ".flac" };

        private TabType mCurrentTab = TabType.RuntimeMonitor;
        private VisualElement mTabContent;
        private VisualElement mStatusContainer;

        private Button mRuntimeMonitorTabBtn;
        private Button mCodeGeneratorTabBtn;

        private Throttle mRefreshThrottle;
        private bool mAutoRefresh = true;
        private double mLastRefreshTime;

        /// <summary>
        /// 构建 AudioKit 主页面。
        /// </summary>
        protected override void BuildUI(VisualElement root)
        {
            LoadPrefs();

            var scaffold = CreateKitPageScaffold(
                "AudioKit",
                "在统一工作台中切换运行时音频监控与音频 ID 代码生成，保留原有混音台与生成流程。",
                KitIcons.AUDIOKIT,
                "音频工作台");
            root.Add(scaffold.Root);

            mStatusContainer = scaffold.StatusBar;

            var tabBar = YokiFrameUIComponents.CreateTabBar();
            scaffold.Toolbar.Add(tabBar);

            mRuntimeMonitorTabBtn = YokiFrameUIComponents.CreateTabButton(
                "运行时监控",
                mCurrentTab == TabType.RuntimeMonitor,
                () => SwitchTab(TabType.RuntimeMonitor));

            mCodeGeneratorTabBtn = YokiFrameUIComponents.CreateTabButton(
                "代码生成器",
                mCurrentTab == TabType.CodeGenerator,
                () => SwitchTab(TabType.CodeGenerator));

            tabBar.Add(mRuntimeMonitorTabBtn);
            tabBar.Add(mCodeGeneratorTabBtn);

            mTabContent = new VisualElement();
            mTabContent.style.flexGrow = 1;
            scaffold.Content.Add(mTabContent);

            SetupReactiveSubscriptions();
            SwitchTab(mCurrentTab);
        }

        /// <summary>
        /// 建立运行时音频监控订阅。
        /// </summary>
        private void SetupReactiveSubscriptions()
        {
            mRefreshThrottle = CreateThrottle(THROTTLE_INTERVAL);

            SubscribeChannel<(string path, int channelId, float volume, float pitch, float duration)>(
                DataChannels.AUDIO_PLAY_STARTED,
                OnAudioPlayStarted);

            SubscribeChannel<(string path, int channelId)>(
                DataChannels.AUDIO_PLAY_STOPPED,
                OnAudioPlayStopped);
        }

        /// <summary>
        /// 响应音频开始播放事件。
        /// </summary>
        private void OnAudioPlayStarted((string path, int channelId, float volume, float pitch, float duration) data)
        {
            if (!Application.isPlaying || mCurrentTab != TabType.RuntimeMonitor || !mAutoRefresh)
            {
                return;
            }

            mRefreshThrottle.Execute(RefreshActiveRuntimeView);
        }

        /// <summary>
        /// 响应音频停止播放事件。
        /// </summary>
        private void OnAudioPlayStopped((string path, int channelId) data)
        {
            if (!Application.isPlaying || mCurrentTab != TabType.RuntimeMonitor || !mAutoRefresh)
            {
                return;
            }

            mRefreshThrottle.Execute(RefreshActiveRuntimeView);
        }

        /// <summary>
        /// 刷新页签按钮样式。
        /// </summary>
        private void UpdateTabButtonStyles()
        {
            YokiFrameUIComponents.UpdateTabButtonStyle(mRuntimeMonitorTabBtn, mCurrentTab == TabType.RuntimeMonitor);
            YokiFrameUIComponents.UpdateTabButtonStyle(mCodeGeneratorTabBtn, mCurrentTab == TabType.CodeGenerator);
        }

        /// <summary>
        /// 切换 AudioKit 工作区。
        /// </summary>
        private void SwitchTab(TabType tabType)
        {
            mCurrentTab = tabType;
            mTabContent.Clear();
            mChannelPanels.Clear();

            if (tabType != TabType.RuntimeMonitor)
            {
                CleanupConsole();
            }

            UpdateTabButtonStyles();
            RefreshStatusBanner();

            if (tabType == TabType.CodeGenerator)
            {
                BuildCodeGeneratorUI(mTabContent);
                return;
            }

            BuildConsoleUI(mTabContent);
        }

        /// <summary>
        /// 根据当前页签刷新顶部状态说明。
        /// Core 负责状态栏唯一内容约束，Kit 只声明当前应显示的横幅。
        /// </summary>
        private void RefreshStatusBanner()
        {
            if (mCurrentTab == TabType.RuntimeMonitor)
            {
                string message = Application.isPlaying
                    ? "运行时监控已连接到 PlayMode 数据，混音台会根据音频播放事件自动刷新。"
                    : "运行时监控需要在 PlayMode 下查看实时音频状态，当前可先调整布局与观察静态控件。";

                SetStatusBanner(mStatusContainer, "运行时监控", message);
                return;
            }

            SetStatusBanner(
                mStatusContainer,
                "代码生成器",
                "代码生成页用于扫描音频资源并生成 ID 常量，不依赖 PlayMode。");
        }

        /// <summary>
        /// 保留的运行时轮询刷新入口。
        /// </summary>
        [Obsolete("Retained for runtime audio monitor refresh.")]
        public override void OnUpdate()
        {
            if (!Application.isPlaying || mCurrentTab != TabType.RuntimeMonitor || !mAutoRefresh)
            {
                return;
            }

            double currentTime = EditorApplication.timeSinceStartup;
            if (currentTime - mLastRefreshTime < REFRESH_INTERVAL)
            {
                return;
            }

            mLastRefreshTime = currentTime;
            RefreshConsoleData();
        }

        /// <summary>
        /// 页面停用时保存配置并清理运行时资源。
        /// </summary>
        public override void OnDeactivate()
        {
            SavePrefs();
            CleanupConsole();
            base.OnDeactivate();
        }

        /// <summary>
        /// 页面激活时刷新当前页签状态与监控内容。
        /// </summary>
        public override void OnActivate()
        {
            base.OnActivate();
            RefreshStatusBanner();

            if (mCurrentTab == TabType.RuntimeMonitor && Application.isPlaying)
            {
                RefreshActiveRuntimeView();
            }
        }

        /// <summary>
        /// 根据当前监控视图刷新运行时内容。
        /// </summary>
        private void RefreshActiveRuntimeView()
        {
            if (mChannelStrips.Count > 0)
            {
                RefreshConsoleData();
                return;
            }

            if (mChannelPanels.Count > 0)
            {
                RefreshMonitorData();
            }
        }

        /// <summary>
        /// PlayMode 切换后重建当前页签内容。
        /// </summary>
        protected override void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            base.OnPlayModeStateChanged(state);

            if (state != PlayModeStateChange.EnteredPlayMode &&
                state != PlayModeStateChange.EnteredEditMode)
            {
                return;
            }

            EditorApplication.delayCall += () =>
            {
                if (mTabContent == null)
                {
                    return;
                }

                SwitchTab(mCurrentTab);
            };
        }
    }
}
