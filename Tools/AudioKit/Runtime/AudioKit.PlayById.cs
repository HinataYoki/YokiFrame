using System;
using System.Threading;
#if YOKIFRAME_UNITASK_SUPPORT
using Cysharp.Threading.Tasks;
#endif
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// AudioKit - 播放 API（Int AudioId，向后兼容）
    /// </summary>
    public static partial class AudioKit
    {
        #region 播放 - Int AudioId（向后兼容）

        /// <summary>
        /// 播放音频（简化调用，内置通道）- 通过 PathResolver 解析路径
        /// </summary>
        public static IAudioHandle Play(int audioId, AudioChannel channel = AudioChannel.Sfx)
        {
            var config = AudioPlayConfig.Default.WithChannel(channel);
            return Play(audioId, config);
        }

        /// <summary>
        /// 播放音频（简化调用，自定义通道 ID）- 通过 PathResolver 解析路径
        /// </summary>
        public static IAudioHandle Play(int audioId, int channelId)
        {
            var config = AudioPlayConfig.Default.WithChannel(channelId);
            return Play(audioId, config);
        }

        /// <summary>
        /// 播放音频（完整配置）- 通过 PathResolver 解析路径
        /// </summary>
        public static IAudioHandle Play(int audioId, AudioPlayConfig config)
        {
            var path = ResolvePath(audioId);
            if (string.IsNullOrEmpty(path))
            {
                KitLogger.Error($"[AudioKit] 无法解析音频路径: {audioId}");
                return null;
            }

            return Play(path, config);
        }

        /// <summary>
        /// 异步播放音频 - 通过 PathResolver 解析路径
        /// </summary>
        public static void PlayAsync(int audioId, AudioPlayConfig config, Action<IAudioHandle> onComplete)
        {
            var path = ResolvePath(audioId);
            if (string.IsNullOrEmpty(path))
            {
                KitLogger.Error($"[AudioKit] 无法解析音频路径: {audioId}");
                onComplete?.Invoke(null);
                return;
            }

            PlayAsync(path, config, onComplete);
        }

        /// <summary>
        /// 播放 3D 音效（位置）- 通过 PathResolver 解析路径
        /// </summary>
        public static IAudioHandle Play3D(int audioId, Vector3 position, AudioPlayConfig config = default)
        {
            var path = ResolvePath(audioId);
            if (string.IsNullOrEmpty(path))
            {
                KitLogger.Error($"[AudioKit] 无法解析音频路径: {audioId}");
                return null;
            }

            return Play3D(path, position, config);
        }

        /// <summary>
        /// 播放 3D 音效（跟随目标）- 通过 PathResolver 解析路径
        /// </summary>
        public static IAudioHandle Play3D(int audioId, Transform followTarget, AudioPlayConfig config = default)
        {
            var path = ResolvePath(audioId);
            if (string.IsNullOrEmpty(path))
            {
                KitLogger.Error($"[AudioKit] 无法解析音频路径: {audioId}");
                return null;
            }

            return Play3D(path, followTarget, config);
        }

        #endregion

#if YOKIFRAME_UNITASK_SUPPORT
        #region UniTask 异步 - String Path

        /// <summary>
        /// [UniTask] 异步播放音频
        /// </summary>
        public static UniTask<IAudioHandle> PlayUniTaskAsync(string path, AudioPlayConfig config, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(path))
            {
                KitLogger.Error("[AudioKit] 播放路径为空");
                return UniTask.FromResult<IAudioHandle>(null);
            }

            EnsureInitialized();
            return sBackend.PlayUniTaskAsync(path, config, cancellationToken);
        }

        /// <summary>
        /// [UniTask] 异步预加载音频
        /// </summary>
        public static UniTask PreloadUniTaskAsync(string path, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(path))
            {
                KitLogger.Error("[AudioKit] 预加载路径为空");
                return UniTask.CompletedTask;
            }

            EnsureInitialized();
            return sBackend.PreloadUniTaskAsync(path, cancellationToken);
        }

        #endregion

        #region UniTask 异步 - Int AudioId（向后兼容）

        /// <summary>
        /// [UniTask] 异步播放音频 - 通过 PathResolver 解析路径
        /// </summary>
        public static UniTask<IAudioHandle> PlayUniTaskAsync(int audioId, AudioPlayConfig config, CancellationToken cancellationToken = default)
        {
            var path = ResolvePath(audioId);
            if (string.IsNullOrEmpty(path))
            {
                KitLogger.Error($"[AudioKit] 无法解析音频路径: {audioId}");
                return UniTask.FromResult<IAudioHandle>(null);
            }

            return PlayUniTaskAsync(path, config, cancellationToken);
        }

        /// <summary>
        /// [UniTask] 异步预加载音频 - 通过 PathResolver 解析路径
        /// </summary>
        public static UniTask PreloadUniTaskAsync(int audioId, CancellationToken cancellationToken = default)
        {
            var path = ResolvePath(audioId);
            if (string.IsNullOrEmpty(path))
            {
                KitLogger.Error($"[AudioKit] 无法解析音频路径: {audioId}");
                return UniTask.CompletedTask;
            }

            return PreloadUniTaskAsync(path, cancellationToken);
        }

        #endregion
#endif
    }
}
