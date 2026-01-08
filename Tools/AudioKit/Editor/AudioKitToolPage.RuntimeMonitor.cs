using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using YokiFrame.EditorTools;

namespace YokiFrame
{
    /// <summary>
    /// AudioKitToolPage - 运行时监控部分
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
                0 => BgmColor, 1 => SfxColor, 2 => VoiceColor,
                3 => AmbientColor, 4 => UiColor, _ => CustomColor
            };
            
            public static string GetChannelName(int channelId) => channelId switch
            {
                0 => "BGM", 1 => "SFX", 2 => "Voice",
                3 => "Ambient", 4 => "UI", _ => $"Custom{channelId}"
            };
        }

        #endregion

        #region 运行时监控字段

        private VisualElement mStickyHeader;
        private VisualElement mChannelContainer;
        private Label mGlobalVolumeLabel;
        private Slider mGlobalVolumeSlider;
        
        // 通道面板缓存
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

        private void BuildRuntimeMonitorUI(VisualElement container)
        {
            mStickyHeader = CreateStickyHeader();
            container.Add(mStickyHeader);
            
            var scrollView = new ScrollView();
            scrollView.style.flexGrow = 1;
            scrollView.style.paddingLeft = 12;
            scrollView.style.paddingRight = 12;
            scrollView.style.paddingTop = 12;
            scrollView.style.paddingBottom = 12;
            container.Add(scrollView);
            
            mChannelContainer = new VisualElement();
            scrollView.Add(mChannelContainer);
            
            // 创建内置通道面板
            for (int i = 0; i < 5; i++)
                CreateChannelPanel(i);
            
            RefreshMonitorData();
        }

        private VisualElement CreateStickyHeader()
        {
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.height = Design.STICKY_HEADER_HEIGHT;
            header.style.paddingLeft = 12;
            header.style.paddingRight = 12;
            header.style.backgroundColor = new StyleColor(new Color(0.14f, 0.14f, 0.16f));
            header.style.borderBottomWidth = 1;
            header.style.borderBottomColor = new StyleColor(new Color(0.22f, 0.22f, 0.24f));
            
            // 标题和响应式提示
            var titleLabel = new Label("🎵 运行时监控");
            titleLabel.style.fontSize = 13;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.Add(titleLabel);
            
            var reactiveHint = new Label("🔄") { style = { fontSize = 10, color = new StyleColor(new Color(0.3f, 0.9f, 0.4f)), marginLeft = 8 }, tooltip = "响应式更新" };
            header.Add(reactiveHint);
            
            header.Add(new VisualElement { style = { flexGrow = 1 } });
            
            // 全局音量
            header.Add(new Label("🔊") { style = { marginRight = 4 } });
            
            mGlobalVolumeSlider = new Slider(0f, 1f) { style = { width = 100 } };
            mGlobalVolumeSlider.value = Application.isPlaying ? AudioKit.GetGlobalVolume() : 1f;
            mGlobalVolumeSlider.RegisterValueChangedCallback(evt =>
            {
                if (Application.isPlaying) AudioKit.SetGlobalVolume(evt.newValue);
                mGlobalVolumeLabel.text = $"{evt.newValue:F2}";
            });
            header.Add(mGlobalVolumeSlider);
            
            mGlobalVolumeLabel = new Label("1.00") { style = { width = 36, marginLeft = 4, fontSize = 11 } };
            header.Add(mGlobalVolumeLabel);
            
            // 分隔线
            header.Add(CreateVerticalDivider());
            
            // 控制按钮
            header.Add(CreateHeaderButton("⏸", "暂停全部", () => { if (Application.isPlaying) AudioKit.PauseAll(); }));
            header.Add(CreateHeaderButton("▶", "恢复全部", () => { if (Application.isPlaying) AudioKit.ResumeAll(); }));
            
            var stopBtn = CreateHeaderButton("⏹", "停止全部", () => { if (Application.isPlaying) AudioKit.StopAll(); });
            stopBtn.style.backgroundColor = new StyleColor(new Color(0.55f, 0.22f, 0.22f));
            header.Add(stopBtn);
            
            // 分隔线
            header.Add(CreateVerticalDivider());
            
            // 全部展开按钮
            var expandAllBtn = CreateHeaderButton("⬇", "全部展开", ExpandAllChannels);
            expandAllBtn.style.width = 60;
            expandAllBtn.tooltip = "全部展开/折叠";
            header.Add(expandAllBtn);
            
            // 分隔线
            header.Add(CreateVerticalDivider());
            
            // 自动刷新开关
            var autoRefreshToggle = YokiFrameUIComponents.CreateModernToggle("自动刷新", mAutoRefresh, v => mAutoRefresh = v);
            autoRefreshToggle.style.marginLeft = 4;
            header.Add(autoRefreshToggle);
            
            // 手动刷新按钮
            var refreshBtn = CreateHeaderButton("🔄", "手动刷新", RefreshMonitorData);
            refreshBtn.style.marginLeft = 8;
            header.Add(refreshBtn);
            
            return header;
        }

        private VisualElement CreateVerticalDivider()
        {
            var divider = new VisualElement();
            divider.style.width = 1;
            divider.style.height = 24;
            divider.style.marginLeft = divider.style.marginRight = 8;
            divider.style.backgroundColor = new StyleColor(new Color(0.28f, 0.28f, 0.30f));
            return divider;
        }

        private Button CreateHeaderButton(string text, string tooltip, System.Action onClick)
        {
            var btn = new Button(onClick) { text = text, tooltip = tooltip };
            btn.style.width = 32;
            btn.style.height = 28;
            btn.style.marginLeft = 4;
            return btn;
        }

        private void ExpandAllChannels()
        {
            // 检查是否全部已展开
            bool allExpanded = true;
            foreach (var kvp in mChannelPanels)
            {
                if (!kvp.Value.IsExpanded)
                {
                    allExpanded = false;
                    break;
                }
            }
            
            // 切换状态
            bool targetState = !allExpanded;
            foreach (var kvp in mChannelPanels)
            {
                var panel = kvp.Value;
                panel.IsExpanded = targetState;
                panel.ExpandBtn.text = targetState ? "▼" : "▶";
                panel.TracksContainer.style.display = targetState ? DisplayStyle.Flex : DisplayStyle.None;
                mChannelPanels[kvp.Key] = panel;
            }
        }

        #endregion

        #region 通道面板

        private void CreateChannelPanel(int channelId)
        {
            var accentColor = Design.GetChannelColor(channelId);
            var channelName = Design.GetChannelName(channelId);
            
            // 主容器
            var panel = new VisualElement();
            panel.name = $"channel-panel-{channelId}";
            panel.style.marginBottom = 8;
            panel.style.backgroundColor = new StyleColor(new Color(0.16f, 0.16f, 0.18f));
            panel.style.borderTopLeftRadius = panel.style.borderTopRightRadius = 6;
            panel.style.borderBottomLeftRadius = panel.style.borderBottomRightRadius = 6;
            panel.style.borderLeftWidth = 4;
            panel.style.borderLeftColor = new StyleColor(accentColor);
            
            // 头部
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.height = Design.CHANNEL_HEADER_HEIGHT;
            header.style.paddingLeft = 12;
            header.style.paddingRight = 12;
            panel.Add(header);
            
            // 展开按钮
            var expandBtn = new Button(() => ToggleExpand(channelId)) { text = "▶" };
            expandBtn.style.width = 24;
            expandBtn.style.height = 24;
            expandBtn.style.fontSize = 10;
            expandBtn.style.backgroundColor = StyleKeyword.Null;
            expandBtn.style.borderLeftWidth = expandBtn.style.borderRightWidth = 0;
            expandBtn.style.borderTopWidth = expandBtn.style.borderBottomWidth = 0;
            header.Add(expandBtn);
            
            // 状态灯
            var statusLight = new VisualElement();
            statusLight.style.width = 8;
            statusLight.style.height = 8;
            statusLight.style.borderTopLeftRadius = statusLight.style.borderTopRightRadius = 4;
            statusLight.style.borderBottomLeftRadius = statusLight.style.borderBottomRightRadius = 4;
            statusLight.style.backgroundColor = new StyleColor(new Color(0.3f, 0.3f, 0.32f));
            statusLight.style.marginLeft = 8;
            statusLight.style.marginRight = 8;
            header.Add(statusLight);
            
            // 名称
            var nameLabel = new Label(channelName);
            nameLabel.style.fontSize = 14;
            nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            nameLabel.style.color = new StyleColor(new Color(0.90f, 0.90f, 0.92f));
            nameLabel.style.width = 70;
            header.Add(nameLabel);
            
            // 播放数量
            var countLabel = new Label("0 playing");
            countLabel.style.fontSize = 11;
            countLabel.style.color = new StyleColor(new Color(0.55f, 0.57f, 0.60f));
            countLabel.style.width = 70;
            header.Add(countLabel);
            
            // 音量滑块
            var volumeSlider = new Slider(0f, 1f) { style = { flexGrow = 1, marginLeft = 12, marginRight = 8 } };
            volumeSlider.value = Application.isPlaying ? AudioKit.GetChannelVolume(channelId) : 1f;
            volumeSlider.RegisterValueChangedCallback(evt =>
            {
                if (Application.isPlaying) AudioKit.SetChannelVolume(channelId, evt.newValue);
                if (mChannelPanels.TryGetValue(channelId, out var p))
                    p.VolumeValueLabel.text = $"{evt.newValue:F2}";
            });
            header.Add(volumeSlider);
            
            var volumeValueLabel = new Label("1.00");
            volumeValueLabel.style.width = 36;
            volumeValueLabel.style.fontSize = 11;
            volumeValueLabel.style.unityTextAlign = TextAnchor.MiddleRight;
            header.Add(volumeValueLabel);

            // 静音按钮
            var muteBtn = new Button(() => ToggleMute(channelId)) { text = "🔊" };
            muteBtn.style.width = 32;
            muteBtn.style.height = 28;
            muteBtn.style.marginLeft = 8;
            header.Add(muteBtn);
            
            // 停止按钮
            var stopBtn = new Button(() => { if (Application.isPlaying) AudioKit.StopChannel(channelId); }) { text = "⏹" };
            stopBtn.style.width = 32;
            stopBtn.style.height = 28;
            stopBtn.style.marginLeft = 4;
            stopBtn.style.backgroundColor = new StyleColor(new Color(0.45f, 0.20f, 0.20f));
            header.Add(stopBtn);
            
            // 子轨道容器（默认折叠）
            var tracksContainer = new VisualElement();
            tracksContainer.style.display = DisplayStyle.None;
            tracksContainer.style.paddingLeft = 44;
            tracksContainer.style.paddingRight = 12;
            tracksContainer.style.paddingBottom = 8;
            tracksContainer.style.borderTopWidth = 1;
            tracksContainer.style.borderTopColor = new StyleColor(new Color(0.22f, 0.22f, 0.24f));
            panel.Add(tracksContainer);
            
            mChannelContainer.Add(panel);
            
            mChannelPanels[channelId] = new ChannelPanelElements
            {
                Root = panel,
                Header = header,
                NameLabel = nameLabel,
                CountLabel = countLabel,
                StatusLight = statusLight,
                VolumeSlider = volumeSlider,
                VolumeValueLabel = volumeValueLabel,
                MuteBtn = muteBtn,
                StopBtn = stopBtn,
                ExpandBtn = expandBtn,
                TracksContainer = tracksContainer,
                IsExpanded = false,
                IsMuted = false
            };
        }

        private void ToggleExpand(int channelId)
        {
            if (!mChannelPanels.TryGetValue(channelId, out var panel)) return;
            
            panel.IsExpanded = !panel.IsExpanded;
            panel.ExpandBtn.text = panel.IsExpanded ? "▼" : "▶";
            panel.TracksContainer.style.display = panel.IsExpanded ? DisplayStyle.Flex : DisplayStyle.None;
            mChannelPanels[channelId] = panel;
        }

        private void ToggleMute(int channelId)
        {
            if (!Application.isPlaying) return;
            if (!mChannelPanels.TryGetValue(channelId, out var panel)) return;
            
            panel.IsMuted = !panel.IsMuted;
            AudioKit.MuteChannel(channelId, panel.IsMuted);
            panel.MuteBtn.text = panel.IsMuted ? "🔇" : "🔊";
            panel.MuteBtn.style.backgroundColor = new StyleColor(panel.IsMuted ? new Color(0.75f, 0.30f, 0.25f) : new Color(0.25f, 0.25f, 0.28f));
            mChannelPanels[channelId] = panel;
        }

        #endregion

        #region 轨道项

        private VisualElement CreateTrackItem(AudioDebugger.AudioPlayRecord record, Color accentColor, bool isFinished = false, int index = -1)
        {
            var item = new VisualElement();
            item.style.flexDirection = FlexDirection.Row;
            item.style.alignItems = Align.Center;
            item.style.height = Design.TRACK_ITEM_HEIGHT;
            item.style.marginTop = 4;
            item.style.paddingLeft = 8;
            item.style.paddingRight = 8;
            item.style.backgroundColor = new StyleColor(isFinished ? new Color(0.10f, 0.10f, 0.11f) : new Color(0.12f, 0.12f, 0.14f));
            item.style.borderTopLeftRadius = item.style.borderTopRightRadius = 4;
            item.style.borderBottomLeftRadius = item.style.borderBottomRightRadius = 4;
            if (isFinished) item.style.opacity = 0.6f;
            
            // 序号
            if (index >= 0)
            {
                var indexLabel = new Label($"#{index}");
                indexLabel.style.width = 24;
                indexLabel.style.fontSize = 9;
                indexLabel.style.color = new StyleColor(new Color(0.45f, 0.45f, 0.48f));
                item.Add(indexLabel);
            }
            
            // 播放状态图标
            string statusIconText = isFinished ? "✓" : (record.IsPaused ? "⏸" : "▶");
            Color statusColor = isFinished ? new Color(0.5f, 0.5f, 0.52f) : (record.IsPaused ? new Color(0.8f, 0.6f, 0.2f) : new Color(0.3f, 0.8f, 0.4f));
            var statusIcon = new Label(statusIconText);
            statusIcon.style.width = 20;
            statusIcon.style.fontSize = 10;
            statusIcon.style.color = new StyleColor(statusColor);
            item.Add(statusIcon);
            
            // 文件名
            var fileName = Path.GetFileName(record.Path);
            var nameLabel = new Label(fileName);
            nameLabel.style.width = 140;
            nameLabel.style.fontSize = 11;
            nameLabel.style.color = new StyleColor(isFinished ? new Color(0.55f, 0.55f, 0.57f) : new Color(0.85f, 0.85f, 0.87f));
            nameLabel.style.overflow = Overflow.Hidden;
            nameLabel.style.textOverflow = TextOverflow.Ellipsis;
            item.Add(nameLabel);
            
            // 进度条容器
            var progressContainer = new VisualElement();
            progressContainer.style.flexGrow = 1;
            progressContainer.style.height = 6;
            progressContainer.style.marginLeft = 8;
            progressContainer.style.marginRight = 8;
            progressContainer.style.backgroundColor = new StyleColor(new Color(0.08f, 0.08f, 0.10f));
            progressContainer.style.borderTopLeftRadius = progressContainer.style.borderTopRightRadius = 3;
            progressContainer.style.borderBottomLeftRadius = progressContainer.style.borderBottomRightRadius = 3;
            progressContainer.style.overflow = Overflow.Hidden;
            item.Add(progressContainer);
            
            // 进度条填充
            var progressFill = new VisualElement();
            progressFill.style.height = Length.Percent(100);
            progressFill.style.width = Length.Percent(isFinished ? 100f : record.Progress * 100f);
            progressFill.style.backgroundColor = new StyleColor(isFinished ? new Color(0.35f, 0.35f, 0.38f) : accentColor);
            progressFill.style.borderTopLeftRadius = progressFill.style.borderTopRightRadius = 3;
            progressFill.style.borderBottomLeftRadius = progressFill.style.borderBottomRightRadius = 3;
            progressContainer.Add(progressFill);
            
            // 时间显示
            var timeLabel = new Label(isFinished ? $"{record.Duration:F1}s" : $"{record.CurrentTime:F1}s / {record.Duration:F1}s");
            timeLabel.style.width = 80;
            timeLabel.style.fontSize = 10;
            timeLabel.style.color = new StyleColor(new Color(0.55f, 0.57f, 0.60f));
            timeLabel.style.unityTextAlign = TextAnchor.MiddleRight;
            item.Add(timeLabel);
            
            // 音量显示
            var volLabel = new Label($"Vol: {record.Volume:F2}");
            volLabel.style.width = 60;
            volLabel.style.fontSize = 10;
            volLabel.style.color = new StyleColor(new Color(0.55f, 0.57f, 0.60f));
            volLabel.style.unityTextAlign = TextAnchor.MiddleRight;
            item.Add(volLabel);
            
            return item;
        }

        #endregion

        #region 数据刷新

        private void RefreshMonitorData()
        {
            if (!Application.isPlaying)
            {
                mGlobalVolumeLabel.text = "N/A";
                foreach (var kvp in mChannelPanels)
                {
                    kvp.Value.StatusLight.style.backgroundColor = new StyleColor(new Color(0.3f, 0.3f, 0.32f));
                    kvp.Value.CountLabel.text = "0 playing";
                    kvp.Value.TracksContainer.Clear();
                }
                return;
            }

            // 更新全局音量
            var globalVolume = AudioKit.GetGlobalVolume();
            mGlobalVolumeSlider.SetValueWithoutNotify(globalVolume);
            mGlobalVolumeLabel.text = $"{globalVolume:F2}";
            
            // 获取当前播放状态
            var currentPlaying = AudioDebugger.GetCurrentPlaying();
            var playingByChannel = new Dictionary<int, List<AudioDebugger.AudioPlayRecord>>();
            
            foreach (var record in currentPlaying)
            {
                if (!playingByChannel.TryGetValue(record.ChannelId, out var list))
                {
                    list = new List<AudioDebugger.AudioPlayRecord>(8);
                    playingByChannel[record.ChannelId] = list;
                }
                list.Add(record);
            }
            
            // 对每个通道的播放列表排序
            foreach (var kvp in playingByChannel)
            {
                kvp.Value.Sort((a, b) =>
                {
                    if (!a.IsPaused && b.IsPaused) return -1;
                    if (a.IsPaused && !b.IsPaused) return 1;
                    return a.CurrentTime.CompareTo(b.CurrentTime);
                });
            }

            // 更新每个通道面板
            foreach (var kvp in mChannelPanels)
            {
                var channelId = kvp.Key;
                var panel = kvp.Value;
                var accentColor = Design.GetChannelColor(channelId);
                
                // 更新音量滑块
                panel.VolumeSlider.SetValueWithoutNotify(AudioKit.GetChannelVolume(channelId));
                panel.VolumeValueLabel.text = $"{AudioKit.GetChannelVolume(channelId):F2}";
                
                // 获取该通道的播放列表
                bool hasPlaying = playingByChannel.TryGetValue(channelId, out var records) && records.Count > 0;
                int playingCount = hasPlaying ? records.Count : 0;
                
                // 更新状态灯
                panel.StatusLight.style.backgroundColor = new StyleColor(
                    hasPlaying ? new Color(0.3f, 0.9f, 0.4f) : new Color(0.3f, 0.3f, 0.32f));
                
                // 更新播放数量
                panel.CountLabel.text = playingCount > 0 ? $"{playingCount} playing" : "0 playing";
                
                // 更新子轨道列表
                panel.TracksContainer.Clear();
                
                if (hasPlaying && panel.IsExpanded)
                {
                    int trackIndex = 0;
                    foreach (var record in records)
                    {
                        var trackItem = CreateTrackItem(record, accentColor, false, trackIndex++);
                        panel.TracksContainer.Add(trackItem);
                    }
                }
                else if (!hasPlaying && panel.IsExpanded)
                {
                    var emptyLabel = new Label("没有正在播放的音频");
                    emptyLabel.style.color = new StyleColor(new Color(0.45f, 0.47f, 0.50f));
                    emptyLabel.style.fontSize = 11;
                    emptyLabel.style.paddingTop = 8;
                    emptyLabel.style.paddingBottom = 8;
                    panel.TracksContainer.Add(emptyLabel);
                }
            }
            
            // 检查是否有自定义通道需要动态添加
            foreach (var channelId in playingByChannel.Keys)
            {
                if (channelId >= 5 && !mChannelPanels.ContainsKey(channelId))
                {
                    CreateChannelPanel(channelId);
                }
            }
        }

        #endregion
    }
}
