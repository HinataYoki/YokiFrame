using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// AudioKit 全局配置
    /// </summary>
    public sealed class AudioKitConfig
    {
        /// <summary>
        /// 最大同时播放数量
        /// </summary>
        public int MaxConcurrentSounds { get; set; } = 32;

        /// <summary>
        /// 音频源对象池初始大小
        /// </summary>
        public int PoolInitialSize { get; set; } = 8;

        /// <summary>
        /// 音频源对象池最大大小
        /// </summary>
        public int PoolMaxSize { get; set; } = 32;

        /// <summary>
        /// 通道最大并发数配置（key: 通道 ID，value: 最大并发数，0 表示无限制）
        /// </summary>
        private readonly Dictionary<int, int> mChannelMaxConcurrent = new()
        {
            { (int)AudioChannel.Bgm, 1 },      // BGM 默认单曲模式
            { (int)AudioChannel.Voice, 1 },    // Voice 默认单曲模式
            { (int)AudioChannel.Sfx, 0 },      // SFX 无限制
            { (int)AudioChannel.Ambient, 0 },  // Ambient 无限制
            { (int)AudioChannel.UI, 0 }        // UI 无限制
        };

        /// <summary>
        /// 全局音量
        /// </summary>
        public float GlobalVolume { get; set; } = 1f;

        /// <summary>
        /// BGM 音量
        /// </summary>
        public float BgmVolume { get; set; } = 1f;

        /// <summary>
        /// 音效音量
        /// </summary>
        public float SfxVolume { get; set; } = 1f;

        /// <summary>
        /// 语音音量
        /// </summary>
        public float VoiceVolume { get; set; } = 1f;

        /// <summary>
        /// 环境音音量
        /// </summary>
        public float AmbientVolume { get; set; } = 1f;

        /// <summary>
        /// UI 音效音量
        /// </summary>
        public float UIVolume { get; set; } = 1f;

        /// <summary>
        /// 默认配置
        /// </summary>
        public static AudioKitConfig Default => new();

        /// <summary>
        /// 获取指定通道的默认音量
        /// </summary>
        public float GetChannelVolume(AudioChannel channel)
        {
            return channel switch
            {
                AudioChannel.Bgm => BgmVolume,
                AudioChannel.Sfx => SfxVolume,
                AudioChannel.Voice => VoiceVolume,
                AudioChannel.Ambient => AmbientVolume,
                AudioChannel.UI => UIVolume,
                _ => 1f
            };
        }

        /// <summary>
        /// 设置指定通道的默认音量
        /// </summary>
        public void SetChannelVolume(AudioChannel channel, float volume)
        {
            switch (channel)
            {
                case AudioChannel.Bgm:
                    BgmVolume = volume;
                    break;
                case AudioChannel.Sfx:
                    SfxVolume = volume;
                    break;
                case AudioChannel.Voice:
                    VoiceVolume = volume;
                    break;
                case AudioChannel.Ambient:
                    AmbientVolume = volume;
                    break;
                case AudioChannel.UI:
                    UIVolume = volume;
                    break;
            }
        }

        /// <summary>
        /// 获取指定通道的最大并发数（0 表示无限制）
        /// </summary>
        public int GetChannelMaxConcurrent(AudioChannel channel)
        {
            return channel switch
            {
                AudioChannel.Bgm => mChannelMaxConcurrent[(int)AudioChannel.Bgm],
                AudioChannel.Voice => mChannelMaxConcurrent[(int)AudioChannel.Voice],
                AudioChannel.Sfx => mChannelMaxConcurrent[(int)AudioChannel.Sfx],
                AudioChannel.Ambient => mChannelMaxConcurrent[(int)AudioChannel.Ambient],
                AudioChannel.UI => mChannelMaxConcurrent[(int)AudioChannel.UI],
                _ => 0
            };
        }

        /// <summary>
        /// 获取指定通道 ID 的最大并发数（支持自定义通道）
        /// </summary>
        public int GetChannelMaxConcurrent(int channelId)
        {
            if (channelId >= 0 && channelId <= 4)
            {
                return GetChannelMaxConcurrent((AudioChannel)channelId);
            }
            return mChannelMaxConcurrent.TryGetValue(channelId, out var max) ? max : 0;
        }

        /// <summary>
        /// 设置指定通道的最大并发数（0 表示无限制）
        /// </summary>
        public void SetChannelMaxConcurrent(AudioChannel channel, int maxConcurrent)
        {
            mChannelMaxConcurrent[(int)channel] = maxConcurrent;
        }

        /// <summary>
        /// 设置指定通道 ID 的最大并发数（支持自定义通道）
        /// </summary>
        public void SetChannelMaxConcurrent(int channelId, int maxConcurrent)
        {
            mChannelMaxConcurrent[channelId] = maxConcurrent;
        }
    }
}
