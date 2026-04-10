using System;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// AudioKit - 播放 API（String Path）
    /// </summary>
    public static partial class AudioKit
    {
        #region 播放 - String Path（主要 API）

        /// <summary>
        /// 播放音频（简化调用，内置通道）
        /// </summary>
        public static IAudioHandle Play(string path, AudioChannel channel = AudioChannel.Sfx)
        {
            var config = AudioPlayConfig.Default.WithChannel(channel);
            return Play(path, config);
        }

        /// <summary>
        /// 播放音频（简化调用，自定义通道 ID）
        /// </summary>
        public static IAudioHandle Play(string path, int channelId)
        {
            var config = AudioPlayConfig.Default.WithChannel(channelId);
            return Play(path, config);
        }

        /// <summary>
        /// 播放音频（完整配置）
        /// </summary>
        public static IAudioHandle Play(string path, AudioPlayConfig config)
        {
            if (string.IsNullOrEmpty(path))
            {
                KitLogger.Error("[AudioKit] 播放路径为空");
                return null;
            }

            EnsureInitialized();
            return sBackend.Play(path, config);
        }

        /// <summary>
        /// 异步播放音频
        /// </summary>
        public static void PlayAsync(string path, AudioPlayConfig config, Action<IAudioHandle> onComplete)
        {
            if (string.IsNullOrEmpty(path))
            {
                KitLogger.Error("[AudioKit] 播放路径为空");
                onComplete?.Invoke(null);
                return;
            }

            EnsureInitialized();
            sBackend.PlayAsync(path, config, onComplete);
        }

        /// <summary>
        /// 播放 3D 音效（位置）
        /// </summary>
        public static IAudioHandle Play3D(string path, Vector3 position, AudioPlayConfig config = default)
        {
            if (config.Equals(default(AudioPlayConfig)))
            {
                config = AudioPlayConfig.Create3D(position);
            }
            else
            {
                config = config.With3DPosition(position);
            }

            return Play(path, config);
        }

        /// <summary>
        /// 播放 3D 音效（跟随目标）
        /// </summary>
        public static IAudioHandle Play3D(string path, Transform followTarget, AudioPlayConfig config = default)
        {
            if (followTarget == null)
            {
                KitLogger.Warning("[AudioKit] 跟随目标为空，使用原点位置");
                return Play3D(path, Vector3.zero, config);
            }

            if (config.Equals(default(AudioPlayConfig)))
            {
                config = AudioPlayConfig.Create3DFollow(followTarget);
            }
            else
            {
                config = config.With3DFollow(followTarget);
            }

            return Play(path, config);
        }

        #endregion
    }
}
