#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame
{
    /// <summary>
    /// AudioKitToolPage - 通道条推子和 VU 表
    /// </summary>
    public partial class AudioKitToolPage
    {
        #region 推子区域

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
            var handle = CreateFaderHandle(accentColor);
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

        private VisualElement CreateFaderHandle(Color accentColor)
        {
            var handle = new VisualElement
            {
                style =
                {
                    position = Position.Absolute,
                    left = 2,
                    right = 2,
                    height = 24,
                    bottom = Length.Percent(100 - 12),
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
            
            return handle;
        }

        #endregion

        #region 推子拖拽

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
                float trackHeight = track.resolvedStyle.height - 24;
                float volumeDelta = -deltaY / trackHeight;
                
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
                float clickY = evt.localPosition.y - 12;
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
            handle.style.top = Length.Percent(Mathf.Clamp(percent - 6, 0, 88));
            fill.style.height = Length.Percent(volume * 100f);
        }

        #endregion
    }
}
#endif
