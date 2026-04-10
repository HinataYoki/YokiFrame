using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;
using YokiFrame.EditorTools;

namespace YokiFrame
{
    /// <summary>
    /// AudioKitToolPage 运行时监控的通道 UI 子模块。
    /// 负责通道面板、轨道项以及局部交互控件的构建。
    /// </summary>
    public partial class AudioKitToolPage
    {
        #region 通道面板

        /// <summary>
        /// 为指定通道创建一组监控面板元素。
        /// </summary>
        private void CreateChannelPanel(int channelId)
        {
            if (mChannelContainer == null)
            {
                return;
            }

            var accentColor = Design.GetChannelColor(channelId);
            var channelName = Design.GetChannelName(channelId);

            var panel = new VisualElement();
            panel.name = $"channel-panel-{channelId}";
            panel.style.marginBottom = YokiFrameUIComponents.Spacing.SM;
            panel.style.backgroundColor = new StyleColor(YokiFrameUIComponents.Colors.LayerCard);
            panel.style.borderTopLeftRadius = panel.style.borderTopRightRadius = YokiFrameUIComponents.Radius.LG;
            panel.style.borderBottomLeftRadius = panel.style.borderBottomRightRadius = YokiFrameUIComponents.Radius.LG;
            panel.style.borderLeftWidth = 4;
            panel.style.borderLeftColor = new StyleColor(accentColor);

            var header = CreateChannelHeader(channelId, channelName, out var elements);
            panel.Add(header);

            var tracksContainer = new VisualElement();
            tracksContainer.style.display = DisplayStyle.None;
            tracksContainer.style.paddingLeft = 44;
            tracksContainer.style.paddingRight = YokiFrameUIComponents.Spacing.MD;
            tracksContainer.style.paddingBottom = YokiFrameUIComponents.Spacing.SM;
            tracksContainer.style.borderTopWidth = 1;
            tracksContainer.style.borderTopColor = new StyleColor(YokiFrameUIComponents.Colors.BorderDefault);
            panel.Add(tracksContainer);

            mChannelContainer.Add(panel);

            elements.Root = panel;
            elements.TracksContainer = tracksContainer;
            mChannelPanels[channelId] = elements;
        }

        /// <summary>
        /// 创建通道头部区域，并返回对应元素引用缓存。
        /// </summary>
        private VisualElement CreateChannelHeader(int channelId, string channelName, out ChannelPanelElements elements)
        {
            elements = new ChannelPanelElements { IsExpanded = false, IsMuted = false };

            var header = YokiFrameUIComponents.CreateRow();
            header.style.height = Design.CHANNEL_HEADER_HEIGHT;
            header.style.paddingLeft = YokiFrameUIComponents.Spacing.MD;
            header.style.paddingRight = YokiFrameUIComponents.Spacing.MD;
            elements.Header = header;

            var expandBtn = new Button(() => ToggleExpand(channelId));
            expandBtn.style.width = 24;
            expandBtn.style.height = 24;
            expandBtn.style.fontSize = 10;
            expandBtn.style.backgroundColor = StyleKeyword.Null;
            expandBtn.style.borderLeftWidth = expandBtn.style.borderRightWidth = 0;
            expandBtn.style.borderTopWidth = expandBtn.style.borderBottomWidth = 0;
            expandBtn.style.alignItems = Align.Center;
            expandBtn.style.justifyContent = Justify.Center;
            var expandIcon = new Image { image = KitIcons.GetTexture(KitIcons.ARROW_RIGHT) };
            expandIcon.style.width = 12;
            expandIcon.style.height = 12;
            expandIcon.name = "expand-icon";
            expandBtn.Add(expandIcon);
            header.Add(expandBtn);
            elements.ExpandBtn = expandBtn;

            var statusLight = new VisualElement();
            statusLight.style.width = 8;
            statusLight.style.height = 8;
            statusLight.style.borderTopLeftRadius = statusLight.style.borderTopRightRadius = 4;
            statusLight.style.borderBottomLeftRadius = statusLight.style.borderBottomRightRadius = 4;
            statusLight.style.backgroundColor = new StyleColor(YokiFrameUIComponents.Colors.TextTertiary);
            statusLight.style.marginLeft = YokiFrameUIComponents.Spacing.SM;
            statusLight.style.marginRight = YokiFrameUIComponents.Spacing.SM;
            header.Add(statusLight);
            elements.StatusLight = statusLight;

            var nameLabel = new Label(channelName);
            nameLabel.style.fontSize = 14;
            nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            nameLabel.style.color = new StyleColor(YokiFrameUIComponents.Colors.TextPrimary);
            nameLabel.style.width = 70;
            header.Add(nameLabel);
            elements.NameLabel = nameLabel;

            var countLabel = new Label("0 playing");
            countLabel.style.fontSize = 11;
            countLabel.style.color = new StyleColor(YokiFrameUIComponents.Colors.TextTertiary);
            countLabel.style.width = 70;
            header.Add(countLabel);
            elements.CountLabel = countLabel;

            CreateChannelVolumeControls(header, channelId, ref elements);
            CreateChannelActionButtons(header, channelId, ref elements);

            return header;
        }

        /// <summary>
        /// 构建通道音量控制区。
        /// </summary>
        private void CreateChannelVolumeControls(VisualElement header, int channelId, ref ChannelPanelElements elements)
        {
            var volumeSlider = new Slider(0f, 1f)
            {
                style =
                {
                    flexGrow = 1,
                    marginLeft = YokiFrameUIComponents.Spacing.MD,
                    marginRight = YokiFrameUIComponents.Spacing.SM
                }
            };
            volumeSlider.value = Application.isPlaying ? AudioKit.GetChannelVolume(channelId) : 1f;
            volumeSlider.RegisterValueChangedCallback(evt =>
            {
                if (Application.isPlaying)
                {
                    AudioKit.SetChannelVolume(channelId, evt.newValue);
                }

                if (mChannelPanels.TryGetValue(channelId, out var p))
                {
                    p.VolumeValueLabel.text = $"{evt.newValue:F2}";
                }
            });
            header.Add(volumeSlider);
            elements.VolumeSlider = volumeSlider;

            var volumeValueLabel = new Label("1.00");
            volumeValueLabel.style.width = 36;
            volumeValueLabel.style.fontSize = 11;
            volumeValueLabel.style.unityTextAlign = TextAnchor.MiddleRight;
            header.Add(volumeValueLabel);
            elements.VolumeValueLabel = volumeValueLabel;
        }

        /// <summary>
        /// 构建通道静音与停止操作按钮。
        /// </summary>
        private void CreateChannelActionButtons(VisualElement header, int channelId, ref ChannelPanelElements elements)
        {
            var muteBtn = new Button(() => ToggleMute(channelId));
            muteBtn.style.width = 32;
            muteBtn.style.height = 28;
            muteBtn.style.marginLeft = YokiFrameUIComponents.Spacing.SM;
            muteBtn.style.alignItems = Align.Center;
            muteBtn.style.justifyContent = Justify.Center;
            var muteIcon = new Image { image = KitIcons.GetTexture(KitIcons.VOLUME) };
            muteIcon.style.width = 14;
            muteIcon.style.height = 14;
            muteIcon.name = "mute-icon";
            muteBtn.Add(muteIcon);
            header.Add(muteBtn);
            elements.MuteBtn = muteBtn;

            var stopBtn = new Button(() =>
            {
                if (Application.isPlaying) AudioKit.StopChannel(channelId);
            });
            stopBtn.style.width = 32;
            stopBtn.style.height = 28;
            stopBtn.style.marginLeft = YokiFrameUIComponents.Spacing.XS;
            stopBtn.style.backgroundColor = new StyleColor(YokiFrameUIComponents.Colors.BadgeError);
            stopBtn.style.alignItems = Align.Center;
            stopBtn.style.justifyContent = Justify.Center;
            var stopIcon = new Image { image = KitIcons.GetTexture(KitIcons.STOP) };
            stopIcon.style.width = 14;
            stopIcon.style.height = 14;
            stopBtn.Add(stopIcon);
            header.Add(stopBtn);
            elements.StopBtn = stopBtn;
        }

        /// <summary>
        /// 切换单个通道面板的展开状态。
        /// </summary>
        private void ToggleExpand(int channelId)
        {
            if (!mChannelPanels.TryGetValue(channelId, out var panel))
            {
                return;
            }

            panel.IsExpanded = !panel.IsExpanded;
            var expandIcon = panel.ExpandBtn.Q<Image>("expand-icon");
            if (expandIcon != null)
            {
                expandIcon.image = KitIcons.GetTexture(panel.IsExpanded ? KitIcons.ARROW_DOWN : KitIcons.ARROW_RIGHT);
            }

            panel.TracksContainer.style.display = panel.IsExpanded ? DisplayStyle.Flex : DisplayStyle.None;
            mChannelPanels[channelId] = panel;
        }

        /// <summary>
        /// 切换单个通道的静音状态。
        /// </summary>
        private void ToggleMute(int channelId)
        {
            if (!Application.isPlaying || !mChannelPanels.TryGetValue(channelId, out var panel))
            {
                return;
            }

            panel.IsMuted = !panel.IsMuted;
            AudioKit.MuteChannel(channelId, panel.IsMuted);

            var muteIcon = panel.MuteBtn.Q<Image>("mute-icon");
            if (muteIcon != null)
            {
                muteIcon.image = KitIcons.GetTexture(panel.IsMuted ? KitIcons.WARNING : KitIcons.VOLUME);
            }

            panel.MuteBtn.style.backgroundColor = new StyleColor(
                panel.IsMuted ? YokiFrameUIComponents.Colors.StatusError : YokiFrameUIComponents.Colors.BadgeDefault);
            mChannelPanels[channelId] = panel;
        }

        #endregion

        #region 轨道项

        /// <summary>
        /// 创建轨道项视图。
        /// 用于展示单条音频播放记录的状态、进度和摘要信息。
        /// </summary>
        private VisualElement CreateTrackItem(AudioDebugger.AudioPlayRecord record, Color accentColor, bool isFinished = false, int index = -1)
        {
            var item = YokiFrameUIComponents.CreateRow();
            item.style.height = Design.TRACK_ITEM_HEIGHT;
            item.style.marginTop = YokiFrameUIComponents.Spacing.XS;
            item.style.paddingLeft = YokiFrameUIComponents.Spacing.SM;
            item.style.paddingRight = YokiFrameUIComponents.Spacing.SM;
            item.style.backgroundColor = new StyleColor(isFinished
                ? new Color(0.10f, 0.10f, 0.11f)
                : YokiFrameUIComponents.Colors.LayerFilterBar);
            item.style.borderTopLeftRadius = item.style.borderTopRightRadius = YokiFrameUIComponents.Radius.MD;
            item.style.borderBottomLeftRadius = item.style.borderBottomRightRadius = YokiFrameUIComponents.Radius.MD;
            if (isFinished)
            {
                item.style.opacity = 0.6f;
            }

            if (index >= 0)
            {
                var indexLabel = new Label($"#{index}");
                indexLabel.style.width = 24;
                indexLabel.style.fontSize = 9;
                indexLabel.style.color = new StyleColor(YokiFrameUIComponents.Colors.TextTertiary);
                item.Add(indexLabel);
            }

            string statusIconId = isFinished ? KitIcons.SUCCESS : (record.IsPaused ? KitIcons.PAUSE : KitIcons.PLAY);
            Color statusColor = isFinished
                ? YokiFrameUIComponents.Colors.TextTertiary
                : (record.IsPaused ? YokiFrameUIComponents.Colors.StatusWarning : YokiFrameUIComponents.Colors.StatusSuccess);
            var statusIcon = new Image { image = KitIcons.GetTexture(statusIconId) };
            statusIcon.style.width = 14;
            statusIcon.style.height = 14;
            statusIcon.style.marginRight = YokiFrameUIComponents.Spacing.XS;
            statusIcon.tintColor = statusColor;
            item.Add(statusIcon);

            BuildTrackItemContent(item, record, accentColor, isFinished);
            return item;
        }

        /// <summary>
        /// 填充轨道项内容区域。
        /// </summary>
        private void BuildTrackItemContent(VisualElement item, AudioDebugger.AudioPlayRecord record, Color accentColor, bool isFinished)
        {
            var fileName = Path.GetFileName(record.Path);
            var nameLabel = new Label(fileName);
            nameLabel.style.width = 140;
            nameLabel.style.fontSize = 11;
            nameLabel.style.color = new StyleColor(isFinished
                ? YokiFrameUIComponents.Colors.TextTertiary
                : YokiFrameUIComponents.Colors.TextPrimary);
            nameLabel.style.overflow = Overflow.Hidden;
            nameLabel.style.textOverflow = TextOverflow.Ellipsis;
            item.Add(nameLabel);

            var progressContainer = new VisualElement();
            progressContainer.style.flexGrow = 1;
            progressContainer.style.height = 6;
            progressContainer.style.marginLeft = YokiFrameUIComponents.Spacing.SM;
            progressContainer.style.marginRight = YokiFrameUIComponents.Spacing.SM;
            progressContainer.style.backgroundColor = new StyleColor(new Color(0.08f, 0.08f, 0.10f));
            progressContainer.style.borderTopLeftRadius = progressContainer.style.borderTopRightRadius = YokiFrameUIComponents.Radius.SM;
            progressContainer.style.borderBottomLeftRadius = progressContainer.style.borderBottomRightRadius = YokiFrameUIComponents.Radius.SM;
            progressContainer.style.overflow = Overflow.Hidden;
            item.Add(progressContainer);

            var progressFill = new VisualElement();
            progressFill.style.height = Length.Percent(100);
            progressFill.style.width = Length.Percent(isFinished ? 100f : record.Progress * 100f);
            progressFill.style.backgroundColor = new StyleColor(isFinished ? YokiFrameUIComponents.Colors.TextTertiary : accentColor);
            progressFill.style.borderTopLeftRadius = progressFill.style.borderTopRightRadius = YokiFrameUIComponents.Radius.SM;
            progressFill.style.borderBottomLeftRadius = progressFill.style.borderBottomRightRadius = YokiFrameUIComponents.Radius.SM;
            progressContainer.Add(progressFill);

            var timeLabel = new Label(isFinished
                ? $"{record.Duration:F1}s"
                : $"{record.CurrentTime:F1}s / {record.Duration:F1}s");
            timeLabel.style.width = 80;
            timeLabel.style.fontSize = 10;
            timeLabel.style.color = new StyleColor(YokiFrameUIComponents.Colors.TextTertiary);
            timeLabel.style.unityTextAlign = TextAnchor.MiddleRight;
            item.Add(timeLabel);

            var volLabel = new Label($"Vol: {record.Volume:F2}");
            volLabel.style.width = 60;
            volLabel.style.fontSize = 10;
            volLabel.style.color = new StyleColor(YokiFrameUIComponents.Colors.TextTertiary);
            volLabel.style.unityTextAlign = TextAnchor.MiddleRight;
            item.Add(volLabel);
        }

        #endregion

        #region 数据刷新

        /// <summary>
        /// 刷新运行时监控数据。
        /// </summary>
        private void RefreshMonitorData()
        {
            if (mGlobalVolumeLabel == null || mGlobalVolumeSlider == null || mChannelContainer == null)
            {
                return;
            }

            if (!Application.isPlaying)
            {
                SyncDynamicChannelPanels(new HashSet<int>());
                mGlobalVolumeLabel.text = "N/A";

                foreach (var kvp in mChannelPanels)
                {
                    kvp.Value.StatusLight.style.backgroundColor = new StyleColor(YokiFrameUIComponents.Colors.TextTertiary);
                    kvp.Value.CountLabel.text = "0 playing";
                    kvp.Value.TracksContainer.Clear();
                }

                return;
            }

            var globalVolume = AudioKit.GetGlobalVolume();
            mGlobalVolumeSlider.SetValueWithoutNotify(globalVolume);
            mGlobalVolumeLabel.text = $"{globalVolume:F2}";

            var currentPlaying = AudioDebugger.GetCurrentPlaying();
            var playingByChannel = new Dictionary<int, List<AudioDebugger.AudioPlayRecord>>();

            foreach (var record in currentPlaying)
            {
                if (!playingByChannel.TryGetValue(record.ChannelId, out var list))
                {
                    list = new List<AudioDebugger.AudioPlayRecord>();
                    playingByChannel[record.ChannelId] = list;
                }

                list.Add(record);
            }

            foreach (var kvp in playingByChannel)
            {
                kvp.Value.Sort((a, b) =>
                {
                    if (!a.IsPaused && b.IsPaused) return -1;
                    if (a.IsPaused && !b.IsPaused) return 1;
                    return a.CurrentTime.CompareTo(b.CurrentTime);
                });
            }

            UpdateChannelPanels(playingByChannel);
        }

        /// <summary>
        /// 用最新数据更新所有通道面板。
        /// </summary>
        private void UpdateChannelPanels(Dictionary<int, List<AudioDebugger.AudioPlayRecord>> playingByChannel)
        {
            SyncDynamicChannelPanels(new HashSet<int>(playingByChannel.Keys));

            foreach (var kvp in mChannelPanels)
            {
                var channelId = kvp.Key;
                var panel = kvp.Value;
                var accentColor = Design.GetChannelColor(channelId);

                panel.VolumeSlider.SetValueWithoutNotify(AudioKit.GetChannelVolume(channelId));
                panel.VolumeValueLabel.text = $"{AudioKit.GetChannelVolume(channelId):F2}";

                bool hasPlaying = playingByChannel.TryGetValue(channelId, out var records) && records.Count > 0;
                int playingCount = hasPlaying ? records.Count : 0;

                panel.StatusLight.style.backgroundColor = new StyleColor(
                    hasPlaying ? YokiFrameUIComponents.Colors.StatusSuccess : YokiFrameUIComponents.Colors.TextTertiary);
                panel.CountLabel.text = playingCount > 0 ? $"{playingCount} playing" : "0 playing";
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
                    emptyLabel.style.color = new StyleColor(YokiFrameUIComponents.Colors.TextTertiary);
                    emptyLabel.style.fontSize = 11;
                    emptyLabel.style.paddingTop = YokiFrameUIComponents.Spacing.SM;
                    emptyLabel.style.paddingBottom = YokiFrameUIComponents.Spacing.SM;
                    panel.TracksContainer.Add(emptyLabel);
                }
            }

            foreach (var channelId in playingByChannel.Keys)
            {
                if (channelId >= 5 && !mChannelPanels.ContainsKey(channelId))
                {
                    CreateChannelPanel(channelId);
                }
            }
        }

        /// <summary>
        /// 同步运行时动态通道面板，避免停止播放后残留旧的自定义通道。
        /// </summary>
        private void SyncDynamicChannelPanels(HashSet<int> activeChannelIds)
        {
            var staleChannelIds = new List<int>();

            foreach (var kvp in mChannelPanels)
            {
                if (kvp.Key < 5)
                {
                    continue;
                }

                if (activeChannelIds.Contains(kvp.Key))
                {
                    continue;
                }

                kvp.Value.Root?.RemoveFromHierarchy();
                staleChannelIds.Add(kvp.Key);
            }

            foreach (var channelId in staleChannelIds)
            {
                mChannelPanels.Remove(channelId);
            }
        }

        #endregion
    }
}
