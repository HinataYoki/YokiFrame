using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using YokiFrame.EditorTools;

namespace YokiFrame
{
    /// <summary>
    /// AudioKit 工具页面 - 现代化机架式设计（带子轨道显示）
    /// 采用响应式数据绑定，通过订阅音频事件实现自动更新
    /// </summary>
    public partial class AudioKitToolPage : YokiFrameToolPageBase
    {
        public override string PageName => "AudioKit";
        public override string PageIcon => KitIcons.AUDIOKIT;
        public override int Priority => 40;

        private enum TabType { CodeGenerator, RuntimeMonitor }

        #region 常量

        private const string ASSETS_PREFIX = "Assets";
        private const float THROTTLE_INTERVAL = 0.15f;  // 节流间隔 150ms
        
        private static readonly string[] AUDIO_EXTENSIONS = { ".wav", ".mp3", ".ogg", ".aiff", ".aif", ".flac" };

        #endregion

        #region 字段

        private TabType mCurrentTab = TabType.RuntimeMonitor;
        private VisualElement mTabContent;
        
        // Tab 按钮引用
        private Button mRuntimeMonitorTabBtn;
        private Button mCodeGeneratorTabBtn;
        
        // 响应式更新
        private Throttle mRefreshThrottle;
        private bool mAutoRefresh = true;

        #endregion

        #region 入口

        protected override void BuildUI(VisualElement root)
        {
            LoadPrefs();
            
            // 使用公共组件创建标签栏
            var tabBar = YokiFrameUIComponents.CreateTabBar();
            root.Add(tabBar);
            
            mRuntimeMonitorTabBtn = YokiFrameUIComponents.CreateTabButton("运行时监控", mCurrentTab == TabType.RuntimeMonitor, () => SwitchTab(TabType.RuntimeMonitor));
            mCodeGeneratorTabBtn = YokiFrameUIComponents.CreateTabButton("代码生成器", mCurrentTab == TabType.CodeGenerator, () => SwitchTab(TabType.CodeGenerator));
            tabBar.Add(mRuntimeMonitorTabBtn);
            tabBar.Add(mCodeGeneratorTabBtn);
            
            mTabContent = new VisualElement { style = { flexGrow = 1 } };
            root.Add(mTabContent);
            
            // 初始化响应式订阅
            SetupReactiveSubscriptions();
            
            SwitchTab(TabType.RuntimeMonitor);
        }

        #endregion

        #region 响应式订阅

        /// <summary>
        /// 设置响应式订阅 - 监听音频播放/停止事件
        /// </summary>
        private void SetupReactiveSubscriptions()
        {
            // 创建节流器，避免频繁刷新
            mRefreshThrottle = CreateThrottle(THROTTLE_INTERVAL);
            
            // 订阅音频播放开始事件
            SubscribeChannel<(string path, int channelId, float volume, float pitch, float duration)>(
                DataChannels.AUDIO_PLAY_STARTED, OnAudioPlayStarted);
            
            // 订阅音频播放停止事件
            SubscribeChannel<(string path, int channelId)>(
                DataChannels.AUDIO_PLAY_STOPPED, OnAudioPlayStopped);
        }
        
        /// <summary>
        /// 音频播放开始时的回调
        /// </summary>
        private void OnAudioPlayStarted((string path, int channelId, float volume, float pitch, float duration) data)
        {
            if (!Application.isPlaying) return;
            if (mCurrentTab != TabType.RuntimeMonitor) return;
            if (!mAutoRefresh) return;
            
            // 使用节流器延迟刷新
            mRefreshThrottle.Execute(RefreshMonitorData);
        }
        
        /// <summary>
        /// 音频播放停止时的回调
        /// </summary>
        private void OnAudioPlayStopped((string path, int channelId) data)
        {
            if (!Application.isPlaying) return;
            if (mCurrentTab != TabType.RuntimeMonitor) return;
            if (!mAutoRefresh) return;
            
            // 使用节流器延迟刷新
            mRefreshThrottle.Execute(RefreshMonitorData);
        }

        #endregion

        #region Tab 切换

        private void UpdateTabButtonStyles()
        {
            // 使用公共组件更新标签按钮样式
            YokiFrameUIComponents.UpdateTabButtonStyle(mRuntimeMonitorTabBtn, mCurrentTab == TabType.RuntimeMonitor);
            YokiFrameUIComponents.UpdateTabButtonStyle(mCodeGeneratorTabBtn, mCurrentTab == TabType.CodeGenerator);
        }

        private void SwitchTab(TabType tabType)
        {
            mCurrentTab = tabType;
            mTabContent.Clear();
            mChannelPanels.Clear();
            
            // 清理混音台资源
            if (tabType != TabType.RuntimeMonitor)
                CleanupConsole();
            
            UpdateTabButtonStyles();
            
            if (tabType == TabType.CodeGenerator)
                BuildCodeGeneratorUI(mTabContent);
            else
                BuildConsoleUI(mTabContent); // 使用新的混音台布局
        }

        #endregion

        #region 生命周期

        /// <summary>
        /// 上次刷新时间
        /// </summary>
        private double mLastRefreshTime;
        
        /// <summary>
        /// 刷新间隔（秒）
        /// </summary>
        private const double REFRESH_INTERVAL = 0.1;

        [Obsolete("保留用于运行时音频监控刷新")]
        public override void OnUpdate()
        {
            // 运行时监控模式下定期刷新活跃音频数据
            if (!Application.isPlaying) return;
            if (mCurrentTab != TabType.RuntimeMonitor) return;
            if (!mAutoRefresh) return;
            
            // 节流：每 100ms 刷新一次
            double currentTime = EditorApplication.timeSinceStartup;
            if (currentTime - mLastRefreshTime < REFRESH_INTERVAL) return;
            mLastRefreshTime = currentTime;
            
            RefreshConsoleData();
        }

        public override void OnDeactivate()
        {
            SavePrefs();
            CleanupConsole();
            base.OnDeactivate();
        }

        /// <summary>
        /// PlayMode 状态变化时重建 UI
        /// </summary>
        protected override void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            base.OnPlayModeStateChanged(state);
            
            // 进入或退出 PlayMode 后重建当前 Tab 的 UI
            if (state == PlayModeStateChange.EnteredPlayMode || 
                state == PlayModeStateChange.EnteredEditMode)
            {
                // 延迟一帧执行，确保 Unity 状态已完全切换
                EditorApplication.delayCall += () =>
                {
                    if (mTabContent == null) return;
                    
                    // 重建当前 Tab
                    SwitchTab(mCurrentTab);
                };
            }
        }

        #endregion
    }
}
