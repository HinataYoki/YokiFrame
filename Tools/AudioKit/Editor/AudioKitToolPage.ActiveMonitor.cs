#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using YokiFrame.EditorTools;

namespace YokiFrame
{
    /// <summary>
    /// AudioKitToolPage - 活跃监视器（Active Monitor）
    /// C区：显示正在播放的音频卡片
    /// </summary>
    public partial class AudioKitToolPage
    {
        #region 创建活跃监视器

        /// <summary>
        /// C区：创建活跃监视器
        /// </summary>
        private VisualElement CreateActiveMonitor(int channelId, Color accentColor)
        {
            var monitor = new VisualElement
            {
                style =
                {
                    minHeight = 60,
                    paddingLeft = 8,
                    paddingRight = 8,
                    paddingTop = 8,
                    paddingBottom = 8,
                    borderTopWidth = 1,
                    borderTopColor = new StyleColor(new Color(0.2f, 0.2f, 0.22f))
                }
            };
            
            // 缓存元素
            var elements = mChannelStrips[channelId];
            elements.ActiveMonitor = monitor;
            elements.Root = monitor.parent; // 会在后面设置
            mChannelStrips[channelId] = elements;
            
            return monitor;
        }

        /// <summary>
        /// 创建活跃音频卡片
        /// </summary>
        private VisualElement CreateActiveAudioCard(AudioDebugger.AudioPlayRecord record, Color accentColor)
        {
            var card = new VisualElement
            {
                style =
                {
                    height = 44,
                    marginTop = 4,
                    paddingLeft = 6,
                    paddingRight = 6,
                    paddingTop = 4,
                    paddingBottom = 4,
                    backgroundColor = new StyleColor(new Color(0.1f, 0.1f, 0.12f)),
                    borderTopLeftRadius = 4,
                    borderTopRightRadius = 4,
                    borderBottomLeftRadius = 4,
                    borderBottomRightRadius = 4
                }
            };
            
            // 第一行：状态图标 + 文件名
            var row1 = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    marginBottom = 4
                }
            };
            
            // 播放状态图标
            string statusIconId = record.IsPaused ? KitIcons.PAUSE : KitIcons.PLAY;
            Color statusColor = record.IsPaused ? new Color(0.8f, 0.6f, 0.2f) : new Color(0.3f, 0.9f, 0.4f);
            var statusIcon = new Image { image = KitIcons.GetTexture(statusIconId) };
            statusIcon.style.width = 10;
            statusIcon.style.height = 10;
            statusIcon.style.marginRight = 4;
            statusIcon.tintColor = statusColor;
            row1.Add(statusIcon);
            
            // 文件名
            var fileName = Path.GetFileNameWithoutExtension(record.Path);
            if (fileName.Length > 12) fileName = fileName[..10] + "..";
            var nameLabel = new Label(fileName)
            {
                style =
                {
                    fontSize = 10,
                    color = new StyleColor(new Color(0.8f, 0.8f, 0.82f)),
                    overflow = Overflow.Hidden,
                    textOverflow = TextOverflow.Ellipsis
                }
            };
            nameLabel.tooltip = record.Path;
            row1.Add(nameLabel);
            
            card.Add(row1);
            
            // 第二行：进度条
            var progressBg = new VisualElement
            {
                style =
                {
                    height = 6,
                    backgroundColor = new StyleColor(new Color(0.05f, 0.05f, 0.07f)),
                    borderTopLeftRadius = 3,
                    borderTopRightRadius = 3,
                    borderBottomLeftRadius = 3,
                    borderBottomRightRadius = 3,
                    overflow = Overflow.Hidden
                }
            };
            
            var progressFill = new VisualElement
            {
                style =
                {
                    height = Length.Percent(100),
                    width = Length.Percent(record.Progress * 100f),
                    backgroundColor = new StyleColor(accentColor),
                    borderTopLeftRadius = 3,
                    borderTopRightRadius = 3,
                    borderBottomLeftRadius = 3,
                    borderBottomRightRadius = 3
                }
            };
            progressBg.Add(progressFill);
            card.Add(progressBg);
            
            // 第三行：时间
            var timeLabel = new Label($"{record.CurrentTime:F1}s / {record.Duration:F1}s")
            {
                style =
                {
                    fontSize = 9,
                    color = new StyleColor(new Color(0.5f, 0.5f, 0.52f)),
                    marginTop = 2,
                    unityTextAlign = TextAnchor.MiddleRight
                }
            };
            card.Add(timeLabel);
            
            return card;
        }

        #endregion

        #region VU 表动画

        /// <summary>
        /// 更新 VU 表动画
        /// </summary>
        private void UpdateVuMeters()
        {
            // 防止在非运行时或通道条未初始化时执行
            if (mChannelStrips == null || mChannelStrips.Count == 0) return;
            if (!Application.isPlaying) return;
            
            // 获取当前播放状态
            var currentPlaying = AudioDebugger.GetCurrentPlaying();
            var playingByChannel = new Dictionary<int, int>(8);
            
            foreach (var record in currentPlaying)
            {
                if (!playingByChannel.ContainsKey(record.ChannelId))
                    playingByChannel[record.ChannelId] = 0;
                playingByChannel[record.ChannelId]++;
            }
            
            // 使用 Keys 的快照避免遍历时修改
            var keys = new List<int>(mChannelStrips.Keys);
            foreach (var channelId in keys)
            {
                if (!mChannelStrips.TryGetValue(channelId, out var elements)) continue;
                if (elements.VuBlocks == null || elements.VuBlocks.Count == 0) continue;
                
                // 计算目标电平（基于播放数量和音量）
                bool hasPlaying = playingByChannel.TryGetValue(channelId, out int count) && count > 0;
                float targetLevel = hasPlaying ? Mathf.Clamp01(elements.Volume * (0.5f + Random.Range(0f, 0.5f))) : 0f;
                
                // 平滑过渡
                if (!mVuTargets.ContainsKey(channelId)) mVuTargets[channelId] = 0f;
                if (!mVuLevels.ContainsKey(channelId)) mVuLevels[channelId] = 0f;
                
                mVuTargets[channelId] = targetLevel;
                mVuLevels[channelId] = Mathf.Lerp(mVuLevels[channelId], mVuTargets[channelId], 0.3f);
                
                // 更新 VU 表色块
                int activeBlocks = Mathf.RoundToInt(mVuLevels[channelId] * elements.VuBlocks.Count);
                for (int i = 0; i < elements.VuBlocks.Count; i++)
                {
                    elements.VuBlocks[i].style.opacity = i < activeBlocks ? 1f : 0.15f;
                }
            }
        }

        #endregion

        #region 数据刷新

        /// <summary>
        /// 刷新混音台数据
        /// </summary>
        private void RefreshConsoleData()
        {
            if (!Application.isPlaying)
            {
                if (mGlobalVolumeLabel != null)
                    mGlobalVolumeLabel.text = "N/A";
                    
                // 使用 Keys 的快照避免遍历时修改
                var channelIds = new List<int>(mChannelStrips.Keys);
                foreach (var channelId in channelIds)
                {
                    if (!mChannelStrips.TryGetValue(channelId, out var strip)) continue;
                    strip.ActiveMonitor?.Clear();
                    var emptyLabel = new Label("未运行")
                    {
                        style =
                        {
                            fontSize = 10,
                            color = new StyleColor(new Color(0.4f, 0.4f, 0.42f)),
                            unityTextAlign = TextAnchor.MiddleCenter,
                            marginTop = 8
                        }
                    };
                    strip.ActiveMonitor?.Add(emptyLabel);
                }
                return;
            }
            
            // 更新全局音量
            var globalVolume = AudioKit.GetGlobalVolume();
            if (mGlobalVolumeSlider != null)
                mGlobalVolumeSlider.SetValueWithoutNotify(globalVolume);
            if (mGlobalVolumeLabel != null)
                mGlobalVolumeLabel.text = $"{globalVolume:F2}";
            
            // 获取当前播放状态（只保留正在播放的）
            var currentPlaying = AudioDebugger.GetCurrentPlaying();
            var playingByChannel = new Dictionary<int, List<AudioDebugger.AudioPlayRecord>>(8);
            
            foreach (var record in currentPlaying)
            {
                // 只显示正在播放的音频，排除已停止的
                if (!record.IsPlaying) continue;
                
                if (!playingByChannel.TryGetValue(record.ChannelId, out var list))
                {
                    list = new List<AudioDebugger.AudioPlayRecord>(8);
                    playingByChannel[record.ChannelId] = list;
                }
                list.Add(record);
            }
            
            // 使用 Keys 的快照避免遍历时修改
            var keys = new List<int>(mChannelStrips.Keys);
            foreach (var channelId in keys)
            {
                if (!mChannelStrips.TryGetValue(channelId, out var elements)) continue;
                
                var accentColor = Console.GetChannelColor(channelId);
                
                // 更新推子位置（从 AudioKit 读取实际音量）
                float volume = AudioKit.GetChannelVolume(channelId);
                elements.Volume = volume;
                mChannelStrips[channelId] = elements; // 现在安全了，因为不在遍历字典本身
                
                if (elements.FaderHandle != null && elements.FaderTrack != null)
                {
                    var fill = elements.FaderTrack.Q<VisualElement>();
                    if (fill != null)
                    {
                        UpdateFaderPosition(elements.FaderHandle, fill, volume);
                    }
                }
                if (elements.VolumeLabel != null)
                    elements.VolumeLabel.text = $"{volume:F2}";
                
                // 更新活跃监视器
                elements.ActiveMonitor?.Clear();
                
                if (playingByChannel.TryGetValue(channelId, out var records) && records.Count > 0)
                {
                    foreach (var record in records)
                    {
                        var card = CreateActiveAudioCard(record, accentColor);
                        elements.ActiveMonitor?.Add(card);
                    }
                }
                else
                {
                    var emptyLabel = new Label("无活跃音频")
                    {
                        style =
                        {
                            fontSize = 10,
                            color = new StyleColor(new Color(0.4f, 0.4f, 0.42f)),
                            unityTextAlign = TextAnchor.MiddleCenter,
                            marginTop = 8
                        }
                    };
                    elements.ActiveMonitor?.Add(emptyLabel);
                }
            }
        }

        /// <summary>
        /// 清理混音台资源
        /// </summary>
        private void CleanupConsole()
        {
            EditorApplication.update -= UpdateVuMeters;
            mChannelStrips.Clear();
            mVuLevels.Clear();
            mVuTargets.Clear();
        }

        #endregion
    }
}
#endif
