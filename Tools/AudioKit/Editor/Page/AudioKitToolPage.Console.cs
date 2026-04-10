#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using YokiFrame.EditorTools;

namespace YokiFrame
{
    /// <summary>
    /// AudioKit 工具页中的混音台视图。
    /// 保留录音台式的纵向推子交互，但接入统一工作台的头部、指标和分区语义。
    /// </summary>
    public partial class AudioKitToolPage
    {
        #region 混音台设计常量

        private static class Console
        {
            public const float CHANNEL_WIDTH = 140f;
            public const float FADER_HEIGHT = 200f;
            public const float VU_METER_WIDTH = 8f;
            public const float HEADER_HEIGHT = 60f;

            public static readonly Color BgmColor = new(0.28f, 0.52f, 0.78f);
            public static readonly Color SfxColor = new(0.72f, 0.52f, 0.32f);
            public static readonly Color VoiceColor = new(0.46f, 0.68f, 0.46f);
            public static readonly Color AmbientColor = new(0.50f, 0.44f, 0.70f);
            public static readonly Color UiColor = new(0.72f, 0.46f, 0.58f);
            public static readonly Color CustomColor = new(0.48f, 0.50f, 0.55f);

            public static readonly Color VuGreen = new(0.35f, 0.75f, 0.42f);
            public static readonly Color VuYellow = new(0.85f, 0.72f, 0.26f);
            public static readonly Color VuRed = new(0.82f, 0.38f, 0.32f);

            public static Color GetChannelColor(int channelId) => channelId switch
            {
                0 => BgmColor,
                1 => SfxColor,
                2 => VoiceColor,
                3 => AmbientColor,
                4 => UiColor,
                _ => CustomColor
            };

            public static string GetChannelName(int channelId) => channelId switch
            {
                0 => "BGM",
                1 => "SFX",
                2 => "VOICE",
                3 => "AMBIENT",
                4 => "UI",
                _ => $"CH{channelId}"
            };
        }

        #endregion

        #region 混音台字段

        private VisualElement mConsoleContainer;
        private readonly Dictionary<int, ChannelStripElements> mChannelStrips = new(8);
        private readonly Dictionary<int, float> mVuLevels = new(8);
        private readonly Dictionary<int, float> mVuTargets = new(8);
        private Label mConsolePlayStateMetric;
        private Label mConsoleGlobalVolumeMetric;
        private Label mConsoleActiveTrackMetric;
        private Label mConsoleChannelMetric;

        /// <summary>
        /// 单个通道条的 UI 元素缓存。
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
        /// 构建混音台风格的运行时监控界面。
        /// </summary>
        private void BuildConsoleUI(VisualElement container)
        {
            CleanupConsole();

            var toolbar = CreateConsoleToolbar();
            container.Add(toolbar);

            var metricStrip = CreateKitMetricStrip();
            var (playStateCard, playStateValue) = CreateKitMetricCard("运行状态", Application.isPlaying ? "PlayMode" : "编辑模式", "当前混音台数据来源");
            var (globalVolumeCard, globalVolumeValue) = CreateKitMetricCard("全局音量", "1.00", "AudioKit 全局音量");
            var (activeTrackCard, activeTrackValue) = CreateKitMetricCard("活跃音轨", "0", "当前正在播放的音轨数量");
            var (channelCard, channelValue) = CreateKitMetricCard("监控通道", "5", "当前混音台展示的通道数");
            metricStrip.Add(playStateCard);
            metricStrip.Add(globalVolumeCard);
            metricStrip.Add(activeTrackCard);
            metricStrip.Add(channelCard);
            container.Add(metricStrip);

            mConsolePlayStateMetric = playStateValue;
            mConsoleGlobalVolumeMetric = globalVolumeValue;
            mConsoleActiveTrackMetric = activeTrackValue;
            mConsoleChannelMetric = channelValue;

            var (section, body) = CreateKitSectionPanel("实时混音台", "保留录音台式通道推子布局，用于观察播放状态、音量和通道活跃情况。", KitIcons.MUSIC);
            container.Add(section);

            mConsoleContainer = new VisualElement();
            mConsoleContainer.AddToClassList("yoki-audio-console");
            body.Add(mConsoleContainer);

            for (int i = 0; i < 5; i++)
            {
                CreateChannelStrip(i);
            }

            UpdateConsoleSummary();
            EditorApplication.update += UpdateVuMeters;
            RefreshConsoleData();
        }

        /// <summary>
        /// 创建混音台工具栏。
        /// </summary>
        private VisualElement CreateConsoleToolbar()
        {
            var toolbar = YokiFrameUIComponents.CreateToolbar();
            toolbar.AddToClassList("yoki-audio-toolbar");

            var titleIcon = new Image { image = KitIcons.GetTexture(KitIcons.MUSIC) };
            titleIcon.style.width = 18;
            titleIcon.style.height = 18;
            titleIcon.style.marginRight = YokiFrameUIComponents.Spacing.SM;
            toolbar.Add(titleIcon);

            var title = new Label("音频混音台");
            title.AddToClassList("yoki-audio-toolbar__title");
            toolbar.Add(title);

            var reactiveIndicator = new VisualElement { tooltip = "已启用运行时实时刷新" };
            reactiveIndicator.AddToClassList("yoki-audio-toolbar__indicator");
            toolbar.Add(reactiveIndicator);

            toolbar.Add(YokiFrameUIComponents.CreateFlexSpacer());

            var masterLabel = new Label("主音量");
            masterLabel.AddToClassList("yoki-audio-toolbar__master-label");
            toolbar.Add(masterLabel);

            mGlobalVolumeSlider = new Slider(0f, 1f);
            mGlobalVolumeSlider.AddToClassList("yoki-audio-toolbar__volume-slider");
            mGlobalVolumeSlider.value = Application.isPlaying ? AudioKit.GetGlobalVolume() : 1f;
            mGlobalVolumeSlider.RegisterValueChangedCallback(evt =>
            {
                if (Application.isPlaying)
                {
                    AudioKit.SetGlobalVolume(evt.newValue);
                }

                mGlobalVolumeLabel.text = $"{evt.newValue:F2}";
                if (mConsoleGlobalVolumeMetric != null)
                {
                    mConsoleGlobalVolumeMetric.text = $"{evt.newValue:F2}";
                }
            });
            toolbar.Add(mGlobalVolumeSlider);

            mGlobalVolumeLabel = new Label("1.00");
            mGlobalVolumeLabel.AddToClassList("yoki-audio-toolbar__volume-label");
            toolbar.Add(mGlobalVolumeLabel);

            toolbar.Add(CreateConsoleDivider());

            toolbar.Add(CreateConsoleButton(KitIcons.PAUSE, "暂停全部", () =>
            {
                if (Application.isPlaying)
                {
                    AudioKit.PauseAll();
                }
            }));

            toolbar.Add(CreateConsoleButton(KitIcons.PLAY, "恢复全部", () =>
            {
                if (Application.isPlaying)
                {
                    AudioKit.ResumeAll();
                }
            }));

            var stopAllBtn = CreateConsoleButton(KitIcons.STOP, "停止全部", () =>
            {
                if (Application.isPlaying)
                {
                    AudioKit.StopAll();
                }
            });
            stopAllBtn.AddToClassList("yoki-audio-toolbar__button--stop");
            toolbar.Add(stopAllBtn);

            toolbar.Add(CreateConsoleDivider());
            toolbar.Add(CreateConsoleButton(KitIcons.REFRESH, "刷新", RefreshConsoleData));

            return toolbar;
        }

        /// <summary>
        /// 刷新混音台顶部概要指标。
        /// </summary>
        private void UpdateConsoleSummary()
        {
            if (mConsolePlayStateMetric == null)
            {
                return;
            }

            mConsolePlayStateMetric.text = Application.isPlaying ? "PlayMode" : "编辑模式";
            mConsoleChannelMetric.text = mChannelStrips.Count.ToString();

            if (!Application.isPlaying)
            {
                mConsoleGlobalVolumeMetric.text = "N/A";
                mConsoleActiveTrackMetric.text = "0";
                return;
            }

            var globalVolume = AudioKit.GetGlobalVolume();
            var currentPlaying = AudioDebugger.GetCurrentPlaying();
            mConsoleGlobalVolumeMetric.text = globalVolume.ToString("F2");
            mConsoleActiveTrackMetric.text = currentPlaying.Count.ToString();
        }

        /// <summary>
        /// 创建工具栏分隔线。
        /// </summary>
        private VisualElement CreateConsoleDivider()
        {
            var divider = new VisualElement();
            divider.AddToClassList("yoki-audio-toolbar__divider");
            return divider;
        }

        /// <summary>
        /// 创建工具栏图标按钮。
        /// </summary>
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
