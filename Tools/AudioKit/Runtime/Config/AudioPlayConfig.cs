using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 音频播放配置
    /// </summary>
    public struct AudioPlayConfig
    {
        /// <summary>
        /// 音频通道 ID（0-4 为内置通道，5+ 为用户自定义通道）
        /// </summary>
        public int ChannelId;

        /// <summary>
        /// 音频通道（内置通道的便捷属性）
        /// </summary>
        public AudioChannel Channel => ChannelId < 5 ? (AudioChannel)ChannelId : AudioChannel.Sfx;

        /// <summary>
        /// 音量 (0-1)
        /// </summary>
        public float Volume;

        /// <summary>
        /// 音调 (0.01-3)
        /// </summary>
        public float Pitch;

        /// <summary>
        /// 是否循环
        /// </summary>
        public bool Loop;

        /// <summary>
        /// 淡入时长（秒）
        /// </summary>
        public float FadeInDuration;

        /// <summary>
        /// 淡出时长（秒）
        /// </summary>
        public float FadeOutDuration;

        /// <summary>
        /// 是否为 3D 音效
        /// </summary>
        public bool Is3D;

        /// <summary>
        /// 3D 音效位置
        /// </summary>
        public Vector3 Position;

        /// <summary>
        /// 3D 音效跟随目标
        /// </summary>
        public Transform FollowTarget;

        /// <summary>
        /// 3D 音效最小距离
        /// </summary>
        public float MinDistance;

        /// <summary>
        /// 3D 音效最大距离
        /// </summary>
        public float MaxDistance;

        /// <summary>
        /// 3D 音效衰减模式
        /// </summary>
        public AudioRolloffMode RolloffMode;

        /// <summary>
        /// 默认配置
        /// </summary>
        public static AudioPlayConfig Default => new()
        {
            ChannelId = (int)AudioChannel.Sfx,
            Volume = 1f,
            Pitch = 1f,
            Loop = false,
            FadeInDuration = 0f,
            FadeOutDuration = 0f,
            Is3D = false,
            Position = Vector3.zero,
            FollowTarget = null,
            MinDistance = 1f,
            MaxDistance = 500f,
            RolloffMode = AudioRolloffMode.Logarithmic
        };

        /// <summary>
        /// 创建 3D 音效配置（固定位置）
        /// </summary>
        public static AudioPlayConfig Create3D(Vector3 position, float minDistance = 1f, float maxDistance = 500f)
        {
            var config = Default;
            config.Is3D = true;
            config.Position = position;
            config.MinDistance = minDistance;
            config.MaxDistance = maxDistance;
            return config;
        }

        /// <summary>
        /// 创建 3D 音效配置（跟随目标）
        /// </summary>
        public static AudioPlayConfig Create3DFollow(Transform target, float minDistance = 1f, float maxDistance = 500f)
        {
            var config = Default;
            config.Is3D = true;
            config.FollowTarget = target;
            config.MinDistance = minDistance;
            config.MaxDistance = maxDistance;
            return config;
        }

        /// <summary>
        /// 设置通道（使用内置通道枚举）
        /// </summary>
        public AudioPlayConfig WithChannel(AudioChannel channel)
        {
            ChannelId = (int)channel;
            return this;
        }

        /// <summary>
        /// 设置通道（使用自定义通道 ID，5+ 为用户自定义）
        /// </summary>
        public AudioPlayConfig WithChannel(int channelId)
        {
            ChannelId = channelId;
            return this;
        }

        /// <summary>
        /// 设置音量
        /// </summary>
        public AudioPlayConfig WithVolume(float volume)
        {
            Volume = Mathf.Clamp01(volume);
            return this;
        }

        /// <summary>
        /// 设置音调
        /// </summary>
        public AudioPlayConfig WithPitch(float pitch)
        {
            Pitch = Mathf.Clamp(pitch, 0.01f, 3f);
            return this;
        }

        /// <summary>
        /// 设置循环
        /// </summary>
        public AudioPlayConfig WithLoop(bool loop)
        {
            Loop = loop;
            return this;
        }

        /// <summary>
        /// 设置淡入时长
        /// </summary>
        public AudioPlayConfig WithFadeIn(float duration)
        {
            FadeInDuration = Mathf.Max(0f, duration);
            return this;
        }

        /// <summary>
        /// 设置淡出时长
        /// </summary>
        public AudioPlayConfig WithFadeOut(float duration)
        {
            FadeOutDuration = Mathf.Max(0f, duration);
            return this;
        }

        /// <summary>
        /// 设置 3D 位置
        /// </summary>
        public AudioPlayConfig With3DPosition(Vector3 position, float minDistance = 1f, float maxDistance = 500f)
        {
            Is3D = true;
            Position = position;
            MinDistance = minDistance;
            MaxDistance = maxDistance;
            FollowTarget = null;
            return this;
        }

        /// <summary>
        /// 设置 3D 跟随目标
        /// </summary>
        public AudioPlayConfig With3DFollow(Transform target, float minDistance = 1f, float maxDistance = 500f)
        {
            Is3D = true;
            FollowTarget = target;
            MinDistance = minDistance;
            MaxDistance = maxDistance;
            return this;
        }

        /// <summary>
        /// 设置 3D 衰减模式
        /// </summary>
        public AudioPlayConfig WithRolloffMode(AudioRolloffMode mode)
        {
            RolloffMode = mode;
            return this;
        }

        /// <summary>
        /// 设置 3D 距离参数
        /// </summary>
        public AudioPlayConfig WithDistance(float minDistance, float maxDistance)
        {
            MinDistance = Mathf.Max(0f, minDistance);
            MaxDistance = Mathf.Max(minDistance, maxDistance);
            return this;
        }
    }
}
