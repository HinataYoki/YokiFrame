using System;
using System.Collections.Generic;
using System.Threading;
#if YOKIFRAME_UNITASK_SUPPORT
using Cysharp.Threading.Tasks;
#endif
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 音频管理工具 - 静态入口类
    /// </summary>
    public static class AudioKit
    {
        /// <summary>
        /// 当前音频后端实例，负责实际的音频播放逻辑
        /// </summary>
        private static IAudioBackend sBackend;

        /// <summary>
        /// 全局配置，包含最大并发数、对象池大小、各通道默认音量等
        /// </summary>
        private static AudioKitConfig sConfig;

        /// <summary>
        /// 路径解析器，将 audioId 转换为资源路径
        /// </summary>
        private static Func<int, string> sPathResolver;

        /// <summary>
        /// 各通道音量缓存，key 为通道 ID（0-4 内置，5+ 自定义）
        /// </summary>
        private static readonly Dictionary<int, float> sChannelVolumes = new()
        {
            { 0, 1f }, { 1, 1f }, { 2, 1f }, { 3, 1f }, { 4, 1f }
        };

        /// <summary>
        /// 各通道静音状态缓存，key 为通道 ID
        /// </summary>
        private static readonly Dictionary<int, bool> sChannelMuted = new();

        /// <summary>
        /// 全局音量，影响所有通道的最终音量
        /// </summary>
        private static float sGlobalVolume = 1f;

        /// <summary>
        /// 全局静音状态
        /// </summary>
        private static bool sGlobalMuted;

        /// <summary>
        /// 是否已初始化后端
        /// </summary>
        private static bool sIsInitialized;

        /// <summary>
        /// 缓存的句柄列表，用于通道操作时避免 GC
        /// </summary>
        private static readonly List<IAudioHandle> sCachedHandleList = new(32);

        #region 配置

        /// <summary>
        /// 设置音频后端
        /// </summary>
        public static void SetBackend(IAudioBackend backend)
        {
            if (backend == null)
            {
                throw new ArgumentNullException(nameof(backend));
            }

            // 停止当前所有音频并释放资源
            if (sBackend != null)
            {
                sBackend.StopAll();
                sBackend.Dispose();
            }

            sBackend = backend;
            sBackend.Initialize(sConfig ?? AudioKitConfig.Default);
            sIsInitialized = true;

            KitLogger.Log($"[AudioKit] 后端已切换为: {backend.GetType().Name}");
        }

        /// <summary>
        /// 设置全局配置
        /// </summary>
        public static void SetConfig(AudioKitConfig config)
        {
            sConfig = config ?? AudioKitConfig.Default;

            // 同步通道音量
            sChannelVolumes[(int)AudioChannel.Bgm] = sConfig.BgmVolume;
            sChannelVolumes[(int)AudioChannel.Sfx] = sConfig.SfxVolume;
            sChannelVolumes[(int)AudioChannel.Voice] = sConfig.VoiceVolume;
            sChannelVolumes[(int)AudioChannel.Ambient] = sConfig.AmbientVolume;
            sChannelVolumes[(int)AudioChannel.UI] = sConfig.UIVolume;
            sGlobalVolume = sConfig.GlobalVolume;

            // 如果后端已初始化，重新初始化
            if (sBackend != null)
            {
                sBackend.Initialize(sConfig);
            }
        }

        /// <summary>
        /// 设置路径解析器
        /// </summary>
        public static void SetPathResolver(Func<int, string> resolver)
        {
            sPathResolver = resolver;
        }

        #endregion

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

        #region 通道控制

        /// <summary>
        /// 设置通道音量（内置通道）
        /// </summary>
        public static void SetChannelVolume(AudioChannel channel, float volume)
        {
            SetChannelVolume((int)channel, volume);
        }

        /// <summary>
        /// 设置通道音量（支持自定义通道 ID，5+ 为用户自定义）
        /// </summary>
        public static void SetChannelVolume(int channelId, float volume)
        {
            sChannelVolumes[channelId] = Mathf.Clamp01(volume);

            // 更新后端
            if (sBackend is UnityAudioBackend unityBackend)
            {
                unityBackend.SetChannelVolume(channelId, sChannelVolumes[channelId]);
            }
        }

        /// <summary>
        /// 获取通道音量（内置通道）
        /// </summary>
        public static float GetChannelVolume(AudioChannel channel)
        {
            return GetChannelVolume((int)channel);
        }

        /// <summary>
        /// 获取通道音量（支持自定义通道 ID）
        /// </summary>
        public static float GetChannelVolume(int channelId)
        {
            if (sChannelMuted.TryGetValue(channelId, out var muted) && muted) return 0f;
            return sChannelVolumes.TryGetValue(channelId, out var volume) ? volume : 1f;
        }

        /// <summary>
        /// 静音/取消静音通道（内置通道）
        /// </summary>
        public static void MuteChannel(AudioChannel channel, bool mute)
        {
            MuteChannel((int)channel, mute);
        }

        /// <summary>
        /// 静音/取消静音通道（支持自定义通道 ID）
        /// </summary>
        public static void MuteChannel(int channelId, bool mute)
        {
            sChannelMuted[channelId] = mute;

            if (sBackend is UnityAudioBackend unityBackend)
            {
                unityBackend.SetChannelMuted(channelId, mute);
            }
        }

        /// <summary>
        /// 停止指定通道的所有音频（内置通道）
        /// </summary>
        public static void StopChannel(AudioChannel channel)
        {
            StopChannel((int)channel);
        }

        /// <summary>
        /// 停止指定通道的所有音频（支持自定义通道 ID）
        /// </summary>
        public static void StopChannel(int channelId)
        {
            if (sBackend is UnityAudioBackend unityBackend)
            {
                unityBackend.StopChannel(channelId);
            }
            else if (sBackend != null)
            {
                // 通用实现：获取通道音频并停止
                sBackend.GetPlayingHandles(channelId, sCachedHandleList);
                foreach (var handle in sCachedHandleList)
                {
                    handle.Stop();
                }
            }
        }

        #endregion

        #region 全局控制

        /// <summary>
        /// 设置全局音量
        /// </summary>
        public static void SetGlobalVolume(float volume)
        {
            sGlobalVolume = Mathf.Clamp01(volume);
            sBackend?.SetGlobalVolume(GetEffectiveGlobalVolume());
        }

        /// <summary>
        /// 获取全局音量
        /// </summary>
        public static float GetGlobalVolume() => sGlobalVolume;

        /// <summary>
        /// 暂停所有音频
        /// </summary>
        public static void PauseAll()
        {
            sBackend?.PauseAll();
        }

        /// <summary>
        /// 恢复所有音频
        /// </summary>
        public static void ResumeAll()
        {
            sBackend?.ResumeAll();
        }

        /// <summary>
        /// 停止所有音频
        /// </summary>
        public static void StopAll()
        {
            sBackend?.StopAll();
        }

        /// <summary>
        /// 全局静音/取消静音
        /// </summary>
        public static void MuteAll(bool mute)
        {
            sGlobalMuted = mute;
            sBackend?.SetGlobalVolume(GetEffectiveGlobalVolume());
        }

        /// <summary>
        /// 获取全局静音状态
        /// </summary>
        public static bool IsMuted() => sGlobalMuted;

        #endregion


        #region 资源管理 - String Path

        /// <summary>
        /// 预加载音频
        /// </summary>
        public static void Preload(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                KitLogger.Error("[AudioKit] 预加载路径为空");
                return;
            }

            EnsureInitialized();
            sBackend.Preload(path);
        }

        /// <summary>
        /// 异步预加载音频
        /// </summary>
        public static void PreloadAsync(string path, Action onComplete = null)
        {
            if (string.IsNullOrEmpty(path))
            {
                KitLogger.Error("[AudioKit] 预加载路径为空");
                onComplete?.Invoke();
                return;
            }

            EnsureInitialized();
            sBackend.PreloadAsync(path, onComplete);
        }

        /// <summary>
        /// 卸载音频
        /// </summary>
        public static void Unload(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            sBackend?.Unload(path);
        }

        #endregion

        #region 资源管理 - Int AudioId（向后兼容）

        /// <summary>
        /// 预加载音频 - 通过 PathResolver 解析路径
        /// </summary>
        public static void Preload(int audioId)
        {
            var path = ResolvePath(audioId);
            if (string.IsNullOrEmpty(path))
            {
                KitLogger.Error($"[AudioKit] 无法解析音频路径: {audioId}");
                return;
            }

            Preload(path);
        }

        /// <summary>
        /// 异步预加载音频 - 通过 PathResolver 解析路径
        /// </summary>
        public static void PreloadAsync(int audioId, Action onComplete = null)
        {
            var path = ResolvePath(audioId);
            if (string.IsNullOrEmpty(path))
            {
                KitLogger.Error($"[AudioKit] 无法解析音频路径: {audioId}");
                onComplete?.Invoke();
                return;
            }

            PreloadAsync(path, onComplete);
        }

        /// <summary>
        /// 卸载音频 - 通过 PathResolver 解析路径
        /// </summary>
        public static void Unload(int audioId)
        {
            var path = ResolvePath(audioId);
            if (!string.IsNullOrEmpty(path))
            {
                Unload(path);
            }
        }

        /// <summary>
        /// 卸载所有音频
        /// </summary>
        public static void UnloadAll()
        {
            sBackend?.UnloadAll();
        }

        #endregion

        #region 更新

        /// <summary>
        /// 更新（驱动淡入淡出和 3D 跟随）
        /// </summary>
        public static void Update(float deltaTime)
        {
            sBackend?.Update(deltaTime);
        }

        #endregion

        #region 重置

        /// <summary>
        /// 重置（测试用）
        /// </summary>
        public static void Reset()
        {
            if (sBackend != null)
            {
                sBackend.StopAll();
                sBackend.UnloadAll();
                sBackend.Dispose();
                sBackend = null;
            }

            sConfig = null;
            sPathResolver = null;
            sGlobalVolume = 1f;
            sGlobalMuted = false;
            sIsInitialized = false;

            // 重置通道状态
            sChannelVolumes.Clear();
            sChannelMuted.Clear();
            for (var i = 0; i < 5; i++)
            {
                sChannelVolumes[i] = 1f;
            }
        }

        #endregion

        #region 辅助方法

        private static void EnsureInitialized()
        {
            if (sIsInitialized) return;

            // 自动初始化默认后端
            SetBackend(new UnityAudioBackend());
        }

        private static string ResolvePath(int audioId)
        {
            if (sPathResolver != null)
            {
                return sPathResolver(audioId);
            }

            // 默认路径解析：假设 audioId 对应 Resources 下的路径
            // 实际项目中应该通过配置表获取路径
            return $"Audio/{audioId}";
        }

        private static float GetEffectiveGlobalVolume()
        {
            return sGlobalMuted ? 0f : sGlobalVolume;
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
