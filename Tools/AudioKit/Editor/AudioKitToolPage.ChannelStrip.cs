#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using YokiFrame.EditorTools;

namespace YokiFrame
{
    /// <summary>
    /// AudioKitToolPage - 通道条组件（Channel Strip）
    /// </summary>
    public partial class AudioKitToolPage
    {
        #region 创建通道条

        /// <summary>
        /// 创建单个通道条
        /// </summary>
        private void CreateChannelStrip(int channelId)
        {
            var accentColor = Console.GetChannelColor(channelId);
            var channelName = Console.GetChannelName(channelId);
            
            // 通道条主容器（使用 flexGrow 平分宽度）
            var strip = new VisualElement
            {
                name = $"channel-strip-{channelId}",
                style =
                {
                    flexGrow = 1,
                    flexBasis = 0,
                    marginLeft = 4,
                    marginRight = 4,
                    backgroundColor = new StyleColor(new Color(0.14f, 0.14f, 0.16f)),
                    borderTopLeftRadius = 8,
                    borderTopRightRadius = 8,
                    borderBottomLeftRadius = 8,
                    borderBottomRightRadius = 8
                }
            };
            
            // A区：表头
            var header = CreateChannelHeader(channelId, channelName, accentColor);
            strip.Add(header);
            
            // B区：推子区域
            var faderSection = CreateFaderSection(channelId, accentColor);
            strip.Add(faderSection);
            
            // C区：活跃监视器
            var activeMonitor = CreateActiveMonitor(channelId, accentColor);
            strip.Add(activeMonitor);
            
            mConsoleContainer.Add(strip);
            
            // 初始化 VU 表数据
            mVuLevels[channelId] = 0f;
            mVuTargets[channelId] = 0f;
        }

        /// <summary>
        /// A区：创建通道表头
        /// </summary>
        private VisualElement CreateChannelHeader(int channelId, string channelName, Color accentColor)
        {
            var header = new VisualElement
            {
                style =
                {
                    height = Console.HEADER_HEIGHT,
                    borderTopWidth = 4,
                    borderTopColor = new StyleColor(accentColor),
                    borderTopLeftRadius = 8,
                    borderTopRightRadius = 8,
                    paddingTop = 8,
                    paddingBottom = 8,
                    paddingLeft = 8,
                    paddingRight = 8,
                    backgroundColor = new StyleColor(new Color(0.12f, 0.12f, 0.14f))
                }
            };
            
            // 通道名称
            var nameLabel = new Label(channelName)
            {
                style =
                {
                    fontSize = 13,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    unityTextAlign = TextAnchor.MiddleCenter,
                    color = new StyleColor(accentColor),
                    letterSpacing = 1f
                }
            };
            header.Add(nameLabel);
            
            // 按钮行（Mute + Stop）
            var btnRow = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    justifyContent = Justify.Center,
                    marginTop = 6
                }
            };
            
            // Mute 按钮
            var muteBtn = new Button(() => ToggleChannelMute(channelId))
            {
                text = "M",
                tooltip = "静音",
                style =
                {
                    width = 32,
                    height = 24,
                    fontSize = 11,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    marginRight = 4,
                    backgroundColor = new StyleColor(new Color(0.25f, 0.25f, 0.28f)),
                    borderTopLeftRadius = 4,
                    borderTopRightRadius = 4,
                    borderBottomLeftRadius = 4,
                    borderBottomRightRadius = 4
                }
            };
            btnRow.Add(muteBtn);
            
            // Stop 按钮
            var stopBtn = new Button(() => { if (Application.isPlaying) AudioKit.StopChannel(channelId); })
            {
                text = "S",
                tooltip = "停止",
                style =
                {
                    width = 32,
                    height = 24,
                    fontSize = 11,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    backgroundColor = new StyleColor(new Color(0.5f, 0.2f, 0.2f)),
                    borderTopLeftRadius = 4,
                    borderTopRightRadius = 4,
                    borderBottomLeftRadius = 4,
                    borderBottomRightRadius = 4
                }
            };
            btnRow.Add(stopBtn);
            
            header.Add(btnRow);
            
            // 缓存元素
            if (!mChannelStrips.ContainsKey(channelId))
            {
                mChannelStrips[channelId] = new ChannelStripElements();
            }
            var elements = mChannelStrips[channelId];
            elements.Header = header;
            elements.NameLabel = nameLabel;
            elements.MuteBtn = muteBtn;
            elements.StopBtn = stopBtn;
            elements.IsMuted = false;
            elements.Volume = 1f;
            mChannelStrips[channelId] = elements;
            
            return header;
        }

        /// <summary>
        /// B区：创建推子区域（竖向滑块 + VU 表）
        /// </summary>
        private VisualElement CreateFaderSection(int channelId, Color accentColor)
        {
            var section = new VisualElement
            {
                style =
                {
                    height = Console.FADER_HEIGHT,
                    flexDirection = FlexDirection.Row,
                    justifyContent = Justify.Center,
                    alignItems = Align.Stretch,
                    paddingLeft = 16,
                    paddingRight = 16,
                    paddingTop = 12,
                    paddingBottom = 12
                }
            };
            
            // VU 表（左侧）
            var vuMeter = CreateVuMeter(channelId);
            section.Add(vuMeter);
            
            // 推子轨道（中间）
            var faderContainer = CreateFaderTrack(channelId, accentColor);
            section.Add(faderContainer);
            
            // 音量数值（底部会在推子下方显示）
            
            return section;
        }

        /// <summary>
        /// 创建 VU 表
        /// </summary>
        private VisualElement CreateVuMeter(int channelId)
        {
            var vuContainer = new VisualElement
            {
                style =
                {
                    width = Console.VU_METER_WIDTH,
                    marginRight = 8,
                    flexDirection = FlexDirection.ColumnReverse,
                    backgroundColor = new StyleColor(new Color(0.08f, 0.08f, 0.10f)),
                    borderTopLeftRadius = 2,
                    borderTopRightRadius = 2,
                    borderBottomLeftRadius = 2,
                    borderBottomRightRadius = 2,
                    overflow = Overflow.Hidden
                }
            };
            
            // 创建 VU 表色块（从下到上：绿 -> 黄 -> 红）
            var vuBlocks = new List<VisualElement>(20);
            for (int i = 0; i < 20; i++)
            {
                Color blockColor;
                if (i < 12) blockColor = Console.VuGreen;
                else if (i < 16) blockColor = Console.VuYellow;
                else blockColor = Console.VuRed;
                
                var block = new VisualElement
                {
                    style =
                    {
                        height = 6,
                        marginTop = 2,
                        backgroundColor = new StyleColor(blockColor),
                        opacity = 0.15f,
                        borderTopLeftRadius = 1,
                        borderTopRightRadius = 1,
                        borderBottomLeftRadius = 1,
                        borderBottomRightRadius = 1
                    }
                };
                vuContainer.Add(block);
                vuBlocks.Add(block);
            }
            
            // 缓存 VU 表元素
            var elements = mChannelStrips[channelId];
            elements.VuMeter = vuContainer;
            elements.VuBlocks = vuBlocks;
            mChannelStrips[channelId] = elements;
            
            return vuContainer;
        }

        /// <summary>
        /// 创建推子轨道
        /// </summary>
        private VisualElement CreateFaderTrack(int channelId, Color accentColor)
        {
            var container = new VisualElement
            {
                style =
                {
                    flexGrow = 1,
                    alignItems = Align.Center
                }
            };
            
            // 推子轨道背景
            var track = new VisualElement
            {
                style =
                {
                    width = 36,
                    flexGrow = 1,
                    backgroundColor = new StyleColor(new Color(0.1f, 0.1f, 0.12f)),
                    borderTopLeftRadius = 18,
                    borderTopRightRadius = 18,
                    borderBottomLeftRadius = 18,
                    borderBottomRightRadius = 18,
                    position = Position.Relative
                }
            };
            
            // 推子填充（从底部向上）
            var fill = new VisualElement
            {
                style =
                {
                    position = Position.Absolute,
                    left = 4,
                    right = 4,
                    bottom = 4,
                    height = Length.Percent(100),
                    backgroundColor = new StyleColor(new Color(accentColor.r * 0.4f, accentColor.g * 0.4f, accentColor.b * 0.4f, 0.5f)),
                    borderTopLeftRadius = 14,
                    borderTopRightRadius = 14,
                    borderBottomLeftRadius = 14,
                    borderBottomRightRadius = 14
                }
            };
            track.Add(fill);
            
            // 推子手柄
            var handle = new VisualElement
            {
                style =
                {
                    position = Position.Absolute,
                    left = 2,
                    right = 2,
                    height = 24,
                    bottom = Length.Percent(100 - 12), // 初始位置在顶部（音量 1.0）
                    backgroundColor = new StyleColor(accentColor),
                    borderTopLeftRadius = 12,
                    borderTopRightRadius = 12,
                    borderBottomLeftRadius = 12,
                    borderBottomRightRadius = 12,
                    borderTopWidth = 2,
                    borderBottomWidth = 2,
                    borderLeftWidth = 2,
                    borderRightWidth = 2,
                    borderTopColor = new StyleColor(new Color(1f, 1f, 1f, 0.3f)),
                    borderBottomColor = new StyleColor(new Color(0f, 0f, 0f, 0.3f)),
                    borderLeftColor = new StyleColor(new Color(1f, 1f, 1f, 0.2f)),
                    borderRightColor = new StyleColor(new Color(0f, 0f, 0f, 0.2f))
                }
            };
            
            // 手柄中间的凹槽
            var groove = new VisualElement
            {
                style =
                {
                    position = Position.Absolute,
                    left = 8,
                    right = 8,
                    top = 10,
                    height = 4,
                    backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0.4f)),
                    borderTopLeftRadius = 2,
                    borderTopRightRadius = 2,
                    borderBottomLeftRadius = 2,
                    borderBottomRightRadius = 2
                }
            };
            handle.Add(groove);
            
            track.Add(handle);
            container.Add(track);
            
            // 音量数值标签
            var volumeLabel = new Label("1.00")
            {
                style =
                {
                    marginTop = 8,
                    fontSize = 12,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    unityTextAlign = TextAnchor.MiddleCenter,
                    color = new StyleColor(new Color(0.7f, 0.7f, 0.72f))
                }
            };
            container.Add(volumeLabel);
            
            // 注册拖拽事件
            RegisterFaderDrag(track, handle, fill, channelId, volumeLabel);
            
            // 缓存元素
            var elements = mChannelStrips[channelId];
            elements.FaderTrack = track;
            elements.FaderHandle = handle;
            elements.VolumeLabel = volumeLabel;
            mChannelStrips[channelId] = elements;
            
            return container;
        }

        /// <summary>
        /// 注册推子拖拽事件
        /// </summary>
        private void RegisterFaderDrag(VisualElement track, VisualElement handle, VisualElement fill, int channelId, Label volumeLabel)
        {
            float startY = 0f;
            float startVolume = 1f;
            
            handle.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (!Application.isPlaying) return;
                
                var elements = mChannelStrips[channelId];
                elements.IsDragging = true;
                mChannelStrips[channelId] = elements;
                
                startY = evt.position.y;
                startVolume = elements.Volume;
                handle.CapturePointer(evt.pointerId);
                evt.StopPropagation();
            });
            
            handle.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (!mChannelStrips.TryGetValue(channelId, out var elements) || !elements.IsDragging) return;
                
                float deltaY = evt.position.y - startY;
                float trackHeight = track.resolvedStyle.height - 24; // 减去手柄高度
                float volumeDelta = -deltaY / trackHeight; // 向上拖动增加音量
                
                float newVolume = Mathf.Clamp01(startVolume + volumeDelta);
                elements.Volume = newVolume;
                mChannelStrips[channelId] = elements;
                
                // 更新 UI
                UpdateFaderPosition(handle, fill, newVolume);
                volumeLabel.text = $"{newVolume:F2}";
                
                // 应用到 AudioKit
                AudioKit.SetChannelVolume(channelId, newVolume);
                
                evt.StopPropagation();
            });
            
            handle.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (!mChannelStrips.TryGetValue(channelId, out var elements)) return;
                
                elements.IsDragging = false;
                mChannelStrips[channelId] = elements;
                handle.ReleasePointer(evt.pointerId);
                evt.StopPropagation();
            });
            
            // 点击轨道直接跳转
            track.RegisterCallback<ClickEvent>(evt =>
            {
                if (!Application.isPlaying) return;
                if (evt.target == handle) return;
                
                float trackHeight = track.resolvedStyle.height - 24;
                float clickY = evt.localPosition.y - 12; // 偏移手柄半高
                float newVolume = 1f - Mathf.Clamp01(clickY / trackHeight);
                
                var elements = mChannelStrips[channelId];
                elements.Volume = newVolume;
                mChannelStrips[channelId] = elements;
                
                UpdateFaderPosition(handle, fill, newVolume);
                volumeLabel.text = $"{newVolume:F2}";
                AudioKit.SetChannelVolume(channelId, newVolume);
            });
        }

        /// <summary>
        /// 更新推子位置
        /// </summary>
        private void UpdateFaderPosition(VisualElement handle, VisualElement fill, float volume)
        {
            float percent = (1f - volume) * 100f;
            handle.style.top = Length.Percent(Mathf.Clamp(percent - 6, 0, 88)); // 6% 是手柄高度的一半
            fill.style.height = Length.Percent(volume * 100f);
        }

        /// <summary>
        /// 切换通道静音
        /// </summary>
        private void ToggleChannelMute(int channelId)
        {
            if (!Application.isPlaying) return;
            if (!mChannelStrips.TryGetValue(channelId, out var elements)) return;
            
            elements.IsMuted = !elements.IsMuted;
            AudioKit.MuteChannel(channelId, elements.IsMuted);
            
            // 更新按钮样式
            elements.MuteBtn.style.backgroundColor = new StyleColor(
                elements.IsMuted ? new Color(0.9f, 0.3f, 0.25f) : new Color(0.25f, 0.25f, 0.28f));
            elements.MuteBtn.style.color = new StyleColor(
                elements.IsMuted ? Color.white : new Color(0.7f, 0.7f, 0.72f));
            
            mChannelStrips[channelId] = elements;
        }

        #endregion
    }
}
#endif
