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
            
            var tabBar = new VisualElement();
            tabBar.style.flexDirection = FlexDirection.Row;
            tabBar.style.borderBottomWidth = 1;
            tabBar.style.borderBottomColor = new StyleColor(new Color(0.2f, 0.2f, 0.2f));
            tabBar.style.backgroundColor = new StyleColor(new Color(0.12f, 0.12f, 0.12f));
            root.Add(tabBar);
            
            mRuntimeMonitorTabBtn = CreateTabButton("运行时监控", TabType.RuntimeMonitor);
            mCodeGeneratorTabBtn = CreateTabButton("代码生成器", TabType.CodeGenerator);
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

        private Button CreateTabButton(string text, TabType tabType)
        {
            var btn = new Button(() => SwitchTab(tabType)) { text = text };
            btn.style.paddingLeft = 20;
            btn.style.paddingRight = 20;
            btn.style.paddingTop = 10;
            btn.style.paddingBottom = 10;
            btn.style.borderLeftWidth = btn.style.borderRightWidth = btn.style.borderTopWidth = 0;
            btn.style.borderBottomWidth = 2;
            btn.style.borderBottomColor = new StyleColor(Color.clear);
            btn.style.backgroundColor = StyleKeyword.Null;
            btn.style.color = new StyleColor(new Color(0.7f, 0.7f, 0.7f));
            return btn;
        }

        private void UpdateTabButtonStyles()
        {
            // 运行时监控 Tab
            bool isRuntimeActive = mCurrentTab == TabType.RuntimeMonitor;
            mRuntimeMonitorTabBtn.style.borderBottomColor = new StyleColor(isRuntimeActive ? new Color(0.25f, 0.55f, 0.90f) : Color.clear);
            mRuntimeMonitorTabBtn.style.color = new StyleColor(isRuntimeActive ? new Color(0.95f, 0.95f, 0.97f) : new Color(0.55f, 0.55f, 0.57f));
            mRuntimeMonitorTabBtn.style.backgroundColor = new StyleColor(isRuntimeActive ? new Color(0.18f, 0.18f, 0.20f) : Color.clear);
            
            // 代码生成器 Tab
            bool isCodeGenActive = mCurrentTab == TabType.CodeGenerator;
            mCodeGeneratorTabBtn.style.borderBottomColor = new StyleColor(isCodeGenActive ? new Color(0.25f, 0.55f, 0.90f) : Color.clear);
            mCodeGeneratorTabBtn.style.color = new StyleColor(isCodeGenActive ? new Color(0.95f, 0.95f, 0.97f) : new Color(0.55f, 0.55f, 0.57f));
            mCodeGeneratorTabBtn.style.backgroundColor = new StyleColor(isCodeGenActive ? new Color(0.18f, 0.18f, 0.20f) : Color.clear);
        }

        private void SwitchTab(TabType tabType)
        {
            mCurrentTab = tabType;
            mTabContent.Clear();
            mChannelPanels.Clear();
            
            UpdateTabButtonStyles();
            
            if (tabType == TabType.CodeGenerator)
                BuildCodeGeneratorUI(mTabContent);
            else
                BuildRuntimeMonitorUI(mTabContent);
        }

        #endregion

        #region 生命周期

        public override void OnUpdate()
        {
            // 响应式模式下不再需要轮询
            // 保留此方法以便将来扩展
        }

        public override void OnDeactivate()
        {
            SavePrefs();
            base.OnDeactivate();
        }

        #endregion
    }
}
