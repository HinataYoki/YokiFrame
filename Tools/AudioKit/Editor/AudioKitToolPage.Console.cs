#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using YokiFrame.EditorTools;

namespace YokiFrame
{
    /// <summary>
    /// AudioKitToolPage - 垂直混音台布局（Channel Strip Layout）
    /// 设计灵感：录音室混音台，每个通道一个竖向推子
    /// </summary>
    public partial class AudioKitToolPage
    {
        #region 混音台设计常量

        private static class Console
        {
            // 通道条尺寸
            public const float CHANNEL_WIDTH = 140f;
            public const float FADER_HEIGHT = 200f;
            public const float VU_METER_WIDTH = 8f;
            public const float HEADER_HEIGHT = 60f;
            
            // 颜色定义
            public static readonly Color BgmColor = new(0.25f, 0.55f, 0.90f);
            public static readonly Color SfxColor = new(0.95f, 0.55f, 0.25f);
            public static readonly Color VoiceColor = new(0.55f, 0.85f, 0.35f);
            public static readonly Color AmbientColor = new(0.60f, 0.35f, 0.85f);
            public static readonly Color UiColor = new(0.90f, 0.35f, 0.60f);
            public static readonly Color CustomColor = new(0.50f, 0.50f, 0.55f);
            
            // VU 表颜色
            public static readonly Color VuGreen = new(0.3f, 0.9f, 0.4f);
            public static readonly Color VuYellow = new(0.95f, 0.85f, 0.2f);
            public static readonly Color VuRed = new(0.95f, 0.3f, 0.25f);
            
            public static Color GetChannelColor(int channelId) => channelId switch
            {
                0 => BgmColor, 1 => SfxColor, 2 => VoiceColor,
                3 => AmbientColor, 4 => UiColor, _ => CustomColor
            };
            
            public static string GetChannelName(int channelId) => channelId switch
            {
                0 => "BGM", 1 => "SFX", 2 => "VOICE",
                3 => "AMBIENT", 4 => "UI", _ => $"CH{channelId}"
            };
        }

        #endregion

        #region 混音台字段

        private VisualElement mConsoleContainer;
        private readonly Dictionary<int, ChannelStripElements> mChannelStrips = new(8);
        
        // VU 表动画
        private readonly Dictionary<int, float> mVuLevels = new(8);
        private readonly Dictionary<int, float> mVuTargets = new(8);
        
        /// <summary>
        /// 通道条 UI 元素缓存
        /// </summary>
        private struct ChannelStripElements
        {
            public VisualElement Root;
            public VisualElement Header;
            public Label NameLabel;
            public Button MuteBtn;
            public Button StopBtn;
            public VisualElement FaderTrack;
            public VisualElement FaderHandle;
            public VisualElement VuMeter;
            public List<VisualElement> VuBlocks;
            public Label VolumeLabel;
            public VisualElement ActiveMonitor;
            public float Volume;
            public bool IsMuted;
            public bool IsDragging;
        }

        #endregion

        #region 构建混音台 UI

        /// <summary>
        /// 构建混音台风格的运行时监控 UI
        /// </summary>
        private void BuildConsoleUI(VisualElement container)
        {
            // 先清理之前的资源，防止重复订阅
            CleanupConsole();
            
            // 顶部工具栏
            var toolbar = CreateConsoleToolbar();
            container.Add(toolbar);
            
            // 混音台主容器（横向排列通道条，铺满宽度）
            mConsoleContainer = new VisualElement();
            mConsoleContainer.AddToClassList("yoki-audio-console");
            container.Add(mConsoleContainer);
            
            // 创建内置通道条（每个通道条使用 flexGrow 平分宽度）
            for (int i = 0; i < 5; i++)
                CreateChannelStrip(i);
            
            // 初始化 VU 表动画
            EditorApplication.update += UpdateVuMeters;
            
            RefreshConsoleData();
        }

        /// <summary>
        /// 创建混音台工具栏
        /// </summary>
        private VisualElement CreateConsoleToolbar()
        {
            var toolbar = YokiFrameUIComponents.CreateToolbar();
            toolbar.AddToClassList("yoki-audio-toolbar");
            
            // 标题
            var titleIcon = new Image { image = KitIcons.GetTexture(KitIcons.MUSIC) };
            titleIcon.style.width = 18;
            titleIcon.style.height = 18;
            titleIcon.style.marginRight = YokiFrameUIComponents.Spacing.SM;
            toolbar.Add(titleIcon);
            
            var title = new Label("AUDIO CONSOLE");
            title.AddToClassList("yoki-audio-toolbar__title");
            toolbar.Add(title);
            
            // 响应式指示器
            var reactiveIndicator = new VisualElement { tooltip = "响应式更新已启用" };
            reactiveIndicator.AddToClassList("yoki-audio-toolbar__indicator");
            toolbar.Add(reactiveIndicator);
            
            toolbar.Add(YokiFrameUIComponents.CreateFlexSpacer());
            
            // 全局音量
            var masterLabel = new Label("MASTER");
            masterLabel.AddToClassList("yoki-audio-toolbar__master-label");
            toolbar.Add(masterLabel);
            
            mGlobalVolumeSlider = new Slider(0f, 1f);
            mGlobalVolumeSlider.AddToClassList("yoki-audio-toolbar__volume-slider");
            mGlobalVolumeSlider.value = Application.isPlaying ? AudioKit.GetGlobalVolume() : 1f;
            mGlobalVolumeSlider.RegisterValueChangedCallback(evt =>
            {
                if (Application.isPlaying) AudioKit.SetGlobalVolume(evt.newValue);
                mGlobalVolumeLabel.text = $"{evt.newValue:F2}";
            });
            toolbar.Add(mGlobalVolumeSlider);
            
            mGlobalVolumeLabel = new Label("1.00");
            mGlobalVolumeLabel.AddToClassList("yoki-audio-toolbar__volume-label");
            toolbar.Add(mGlobalVolumeLabel);
            
            // 分隔线
            toolbar.Add(CreateConsoleDivider());
            
            // 控制按钮
            toolbar.Add(CreateConsoleButton(KitIcons.PAUSE, "暂停全部", () => { if (Application.isPlaying) AudioKit.PauseAll(); }));
            toolbar.Add(CreateConsoleButton(KitIcons.PLAY, "恢复全部", () => { if (Application.isPlaying) AudioKit.ResumeAll(); }));
            
            var stopAllBtn = CreateConsoleButton(KitIcons.STOP, "停止全部", () => { if (Application.isPlaying) AudioKit.StopAll(); });
            stopAllBtn.AddToClassList("yoki-audio-toolbar__button--stop");
            toolbar.Add(stopAllBtn);
            
            // 分隔线
            toolbar.Add(CreateConsoleDivider());
            
            // 刷新按钮
            toolbar.Add(CreateConsoleButton(KitIcons.REFRESH, "刷新", RefreshConsoleData));
            
            return toolbar;
        }

        private VisualElement CreateConsoleDivider()
        {
            var divider = new VisualElement();
            divider.AddToClassList("yoki-audio-toolbar__divider");
            return divider;
        }

        private Button CreateConsoleButton(string iconId, string tooltip, System.Action onClick)
        {
            var btn = new Button(onClick) { tooltip = tooltip };
            btn.AddToClassList("yoki-audio-toolbar__button");
            
            var icon = new Image { image = KitIcons.GetTexture(iconId) };
            icon.style.width = 16;
            icon.style.height = 16;
            btn.Add(icon);
            
            return btn;
        }

        #endregion
    }
}
#endif
