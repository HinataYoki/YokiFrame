#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

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
            var btnRow = CreateHeaderButtons(channelId);
            header.Add(btnRow);
            
            return header;
        }

        private VisualElement CreateHeaderButtons(int channelId)
        {
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
            
            // 缓存元素
            if (!mChannelStrips.ContainsKey(channelId))
            {
                mChannelStrips[channelId] = new ChannelStripElements();
            }
            var elements = mChannelStrips[channelId];
            elements.MuteBtn = muteBtn;
            elements.StopBtn = stopBtn;
            elements.IsMuted = false;
            elements.Volume = 1f;
            mChannelStrips[channelId] = elements;
            
            return btnRow;
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
