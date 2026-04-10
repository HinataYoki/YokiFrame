using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using YokiFrame.EditorTools;

namespace YokiFrame
{
    /// <summary>
    /// AudioKitToolPage 的运行时监控子模块。
    /// 负责构建通道监控视图、全局控制栏和通道级交互入口。
    /// </summary>
    public partial class AudioKitToolPage
    {
        #region 设计常量

        private static class Design
        {
            public const float CHANNEL_HEADER_HEIGHT = 48f;
            public const float TRACK_ITEM_HEIGHT = 36f;
            public const float STICKY_HEADER_HEIGHT = 52f;

            public static readonly Color BgmColor = new(0.25f, 0.55f, 0.90f);
            public static readonly Color SfxColor = new(0.95f, 0.55f, 0.25f);
            public static readonly Color VoiceColor = new(0.55f, 0.85f, 0.35f);
            public static readonly Color AmbientColor = new(0.60f, 0.35f, 0.85f);
            public static readonly Color UiColor = new(0.90f, 0.35f, 0.60f);
            public static readonly Color CustomColor = new(0.50f, 0.50f, 0.55f);

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
                2 => "Voice",
                3 => "Ambient",
                4 => "UI",
                _ => $"Custom{channelId}"
            };
        }

        #endregion

        #region 运行时监控字段

        private VisualElement mStickyHeader;
        private VisualElement mChannelContainer;
        private Label mGlobalVolumeLabel;
        private Slider mGlobalVolumeSlider;

        private readonly Dictionary<int, ChannelPanelElements> mChannelPanels = new();

        private struct ChannelPanelElements
        {
            public VisualElement Root;
            public VisualElement Header;
            public Label NameLabel;
            public Label CountLabel;
            public VisualElement StatusLight;
            public Slider VolumeSlider;
            public Label VolumeValueLabel;
            public Button MuteBtn;
            public Button StopBtn;
            public Button ExpandBtn;
            public VisualElement TracksContainer;
            public bool IsExpanded;
            public bool IsMuted;
        }

        #endregion

        #region 运行时监控 UI 构建

        /// <summary>
        /// 构建运行时监控标签页。
        /// </summary>
        private void BuildRuntimeMonitorUI(VisualElement container)
        {
            mStickyHeader = CreateStickyHeader();
            container.Add(mStickyHeader);

            var scrollView = new ScrollView();
            scrollView.style.flexGrow = 1;
            scrollView.style.paddingLeft = YokiFrameUIComponents.Spacing.MD;
            scrollView.style.paddingRight = YokiFrameUIComponents.Spacing.MD;
            scrollView.style.paddingTop = YokiFrameUIComponents.Spacing.MD;
            scrollView.style.paddingBottom = YokiFrameUIComponents.Spacing.MD;
            container.Add(scrollView);

            mChannelContainer = new VisualElement();
            scrollView.Add(mChannelContainer);

            for (int i = 0; i < 5; i++)
            {
                CreateChannelPanel(i);
            }

            RefreshMonitorData();
        }

        /// <summary>
        /// 创建运行时监控页的顶部粘性工具栏。
        /// </summary>
        private VisualElement CreateStickyHeader()
        {
            var header = YokiFrameUIComponents.CreateToolbar();
            header.style.height = Design.STICKY_HEADER_HEIGHT;
            header.style.paddingLeft = YokiFrameUIComponents.Spacing.MD;
            header.style.paddingRight = YokiFrameUIComponents.Spacing.MD;
            header.style.alignItems = Align.Center;

            var titleRow = YokiFrameUIComponents.CreateRow();

            var titleIcon = new Image { image = KitIcons.GetTexture(KitIcons.MUSIC) };
            titleIcon.style.width = 16;
            titleIcon.style.height = 16;
            titleIcon.style.marginRight = YokiFrameUIComponents.Spacing.XS;
            titleRow.Add(titleIcon);

            var titleLabel = new Label("运行时监控");
            titleLabel.style.fontSize = 13;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleRow.Add(titleLabel);
            header.Add(titleRow);

            var reactiveIcon = new Image { image = KitIcons.GetTexture(KitIcons.REFRESH) };
            reactiveIcon.style.width = 12;
            reactiveIcon.style.height = 12;
            reactiveIcon.style.marginLeft = YokiFrameUIComponents.Spacing.SM;
            reactiveIcon.tintColor = YokiFrameUIComponents.Colors.StatusSuccess;
            reactiveIcon.tooltip = "响应式更新";
            header.Add(reactiveIcon);

            header.Add(YokiFrameUIComponents.CreateFlexSpacer());

            BuildGlobalVolumeControls(header);
            header.Add(CreateVerticalDivider());

            header.Add(CreateHeaderButtonWithIcon(KitIcons.PAUSE, "暂停全部", () =>
            {
                if (Application.isPlaying) AudioKit.PauseAll();
            }));

            header.Add(CreateHeaderButtonWithIcon(KitIcons.PLAY, "恢复全部", () =>
            {
                if (Application.isPlaying) AudioKit.ResumeAll();
            }));

            var stopBtn = CreateHeaderButtonWithIcon(KitIcons.STOP, "停止全部", () =>
            {
                if (Application.isPlaying) AudioKit.StopAll();
            });
            stopBtn.style.backgroundColor = new StyleColor(YokiFrameUIComponents.Colors.BadgeError);
            header.Add(stopBtn);

            header.Add(CreateVerticalDivider());

            var expandAllBtn = CreateHeaderButtonWithIcon(KitIcons.EXPAND, "全部展开", ExpandAllChannels);
            expandAllBtn.style.width = 60;
            expandAllBtn.tooltip = "全部展开/折叠";
            header.Add(expandAllBtn);

            header.Add(CreateVerticalDivider());

            var autoRefreshToggle = YokiFrameUIComponents.CreateModernToggle("自动刷新", mAutoRefresh, v => mAutoRefresh = v);
            autoRefreshToggle.style.marginLeft = YokiFrameUIComponents.Spacing.XS;
            header.Add(autoRefreshToggle);

            var refreshBtn = CreateHeaderButtonWithIcon(KitIcons.REFRESH, "手动刷新", RefreshMonitorData);
            refreshBtn.style.marginLeft = YokiFrameUIComponents.Spacing.SM;
            header.Add(refreshBtn);

            return header;
        }

        /// <summary>
        /// 构建全局音量控制区域。
        /// </summary>
        private void BuildGlobalVolumeControls(VisualElement header)
        {
            var volumeIcon = new Image { image = KitIcons.GetTexture(KitIcons.VOLUME) };
            volumeIcon.style.width = 14;
            volumeIcon.style.height = 14;
            volumeIcon.style.marginRight = YokiFrameUIComponents.Spacing.XS;
            header.Add(volumeIcon);

            mGlobalVolumeSlider = new Slider(0f, 1f) { style = { width = 100 } };
            mGlobalVolumeSlider.value = Application.isPlaying ? AudioKit.GetGlobalVolume() : 1f;
            mGlobalVolumeSlider.RegisterValueChangedCallback(evt =>
            {
                if (Application.isPlaying)
                {
                    AudioKit.SetGlobalVolume(evt.newValue);
                }

                mGlobalVolumeLabel.text = $"{evt.newValue:F2}";
            });
            header.Add(mGlobalVolumeSlider);

            mGlobalVolumeLabel = new Label("1.00")
            {
                style =
                {
                    width = 36,
                    marginLeft = YokiFrameUIComponents.Spacing.XS,
                    fontSize = 11
                }
            };
            header.Add(mGlobalVolumeLabel);
        }

        /// <summary>
        /// 创建工具栏纵向分隔线。
        /// </summary>
        private VisualElement CreateVerticalDivider()
        {
            var divider = new VisualElement();
            divider.style.width = 1;
            divider.style.height = 24;
            divider.style.marginLeft = divider.style.marginRight = YokiFrameUIComponents.Spacing.SM;
            divider.style.backgroundColor = new StyleColor(YokiFrameUIComponents.Colors.BorderDefault);
            return divider;
        }

        /// <summary>
        /// 创建带图标的工具栏按钮。
        /// </summary>
        private Button CreateHeaderButtonWithIcon(string iconId, string tooltip, System.Action onClick)
        {
            var btn = new Button(onClick) { tooltip = tooltip };
            btn.style.width = 32;
            btn.style.height = 28;
            btn.style.marginLeft = YokiFrameUIComponents.Spacing.XS;
            btn.style.alignItems = Align.Center;
            btn.style.justifyContent = Justify.Center;

            var icon = new Image { image = KitIcons.GetTexture(iconId) };
            icon.style.width = 14;
            icon.style.height = 14;
            btn.Add(icon);

            return btn;
        }

        /// <summary>
        /// 切换全部通道的展开状态。
        /// </summary>
        private void ExpandAllChannels()
        {
            bool allExpanded = true;
            foreach (var kvp in mChannelPanels)
            {
                if (!kvp.Value.IsExpanded)
                {
                    allExpanded = false;
                    break;
                }
            }

            bool targetState = !allExpanded;
            foreach (var kvp in mChannelPanels)
            {
                var panel = kvp.Value;
                panel.IsExpanded = targetState;

                var expandIcon = panel.ExpandBtn.Q<Image>("expand-icon");
                if (expandIcon != null)
                {
                    expandIcon.image = KitIcons.GetTexture(targetState ? KitIcons.ARROW_DOWN : KitIcons.ARROW_RIGHT);
                }

                panel.TracksContainer.style.display = targetState ? DisplayStyle.Flex : DisplayStyle.None;
                mChannelPanels[kvp.Key] = panel;
            }
        }

        #endregion
    }
}
