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
        private static IAudioBackend sBackend;
        private static AudioKitConfig sConfig;
        private static Func<int, string> sPathResolver;
        private static readonly float[] sChannelVolumes = new float[5] { 1f, 1f, 1f, 1f, 1f };
        private static readonly bool[] sChannelMuted = new bool[5];
        private static float sGlobalVolume = 1f;
        private static bool sGlobalMuted;
        private static bool sIsInitialized;

        // 缓存容器复用
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

        #region 播放

        /// <summary>
        /// 播放音频（简化调用）
        /// </summary>
        public static IAudioHandle Play(int audioId, AudioChannel channel = AudioChannel.Sfx)
        {
            var config = AudioPlayConfig.Default.WithChannel(channel);
            return Play(audioId, config);
        }

        /// <summary>
        /// 播放音频（完整配置）
        /// </summary>
        public static IAudioHandle Play(int audioId, AudioPlayConfig config)
        {
            EnsureInitialized();

            var path = ResolvePath(audioId);
            if (string.IsNullOrEmpty(path))
            {
                KitLogger.Error($"[AudioKit] 无法解析音频路径: {audioId}");
                return null;
            }

            // 计算有效音量
            var channelVolume = GetChannelVolume(config.Channel);
            var effectiveVolume = config.Volume * channelVolume * GetEffectiveGlobalVolume();
            var adjustedConfig = config.WithVolume(effectiveVolume);

            return sBackend.Play(audioId, path, adjustedConfig);
        }

        /// <summary>
        /// 异步播放音频
        /// </summary>
        public static void PlayAsync(int audioId, AudioPlayConfig config, Action<IAudioHandle> onComplete)
        {
            EnsureInitialized();

            var path = ResolvePath(audioId);
            if (string.IsNullOrEmpty(path))
            {
                KitLogger.Error($"[AudioKit] 无法解析音频路径: {audioId}");
                onComplete?.Invoke(null);
                return;
            }

            var channelVolume = GetChannelVolume(config.Channel);
            var effectiveVolume = config.Volume * channelVolume * GetEffectiveGlobalVolume();
            var adjustedConfig = config.WithVolume(effectiveVolume);

            sBackend.PlayAsync(audioId, path, adjustedConfig, onComplete);
        }

        /// <summary>
        /// 播放 3D 音效（位置）
        /// </summary>
        public static IAudioHandle Play3D(int audioId, Vector3 position, AudioPlayConfig config = default)
        {
            if (config.Equals(default(AudioPlayConfig)))
            {
                config = AudioPlayConfig.Create3D(position);
            }
            else
            {
                config = config.With3DPosition(position);
            }

            return Play(audioId, config);
        }

        /// <summary>
        /// 播放 3D 音效（跟随目标）
        /// </summary>
        public static IAudioHandle Play3D(int audioId, Transform followTarget, AudioPlayConfig config = default)
        {
            if (followTarget == null)
            {
                KitLogger.Warning("[AudioKit] 跟随目标为空，使用原点位置");
                return Play3D(audioId, Vector3.zero, config);
            }

            if (config.Equals(default(AudioPlayConfig)))
            {
                config = AudioPlayConfig.Create3DFollow(followTarget);
            }
            else
            {
                config = config.With3DFollow(followTarget);
            }

            return Play(audioId, config);
        }

        #endregion

        #region 通道控制

        /// <summary>
        /// 设置通道音量
        /// </summary>
        public static void SetChannelVolume(AudioChannel channel, float volume)
        {
            var index = (int)channel;
            if (index < 0 || index >= sChannelVolumes.Length) return;

            sChannelVolumes[index] = Mathf.Clamp01(volume);

            // 更新后端
            if (sBackend is UnityAudioBackend unityBackend)
            {
                unityBackend.SetChannelVolume(channel, sChannelVolumes[index]);
            }
        }

        /// <summary>
        /// 获取通道音量
        /// </summary>
        public static float GetChannelVolume(AudioChannel channel)
        {
            var index = (int)channel;
            if (index < 0 || index >= sChannelVolumes.Length) return 1f;
            if (sChannelMuted[index]) return 0f;
            return sChannelVolumes[index];
        }

        /// <summary>
        /// 静音/取消静音通道
        /// </summary>
        public static void MuteChannel(AudioChannel channel, bool mute)
        {
            var index = (int)channel;
            if (index < 0 || index >= sChannelMuted.Length) return;

            sChannelMuted[index] = mute;

            if (sBackend is UnityAudioBackend unityBackend)
            {
                unityBackend.SetChannelMuted(channel, mute);
            }
        }

        /// <summary>
        /// 停止指定通道的所有音频
        /// </summary>
        public static void StopChannel(AudioChannel channel)
        {
            if (sBackend is UnityAudioBackend unityBackend)
            {
                unityBackend.StopChannel(channel);
            }
            else if (sBackend != null)
            {
                // 通用实现：获取通道音频并停止
                sBackend.GetPlayingHandles(channel, sCachedHandleList);
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

        #region 资源管理

        /// <summary>
        /// 预加载音频
        /// </summary>
        public static void Preload(int audioId)
        {
            EnsureInitialized();

            var path = ResolvePath(audioId);
            if (string.IsNullOrEmpty(path))
            {
                KitLogger.Error($"[AudioKit] 无法解析音频路径: {audioId}");
                return;
            }

            sBackend.Preload(audioId, path);
        }

        /// <summary>
        /// 异步预加载音频
        /// </summary>
        public static void PreloadAsync(int audioId, Action onComplete = null)
        {
            EnsureInitialized();

            var path = ResolvePath(audioId);
            if (string.IsNullOrEmpty(path))
            {
                KitLogger.Error($"[AudioKit] 无法解析音频路径: {audioId}");
                onComplete?.Invoke();
                return;
            }

            sBackend.PreloadAsync(audioId, path, onComplete);
        }

        /// <summary>
        /// 卸载音频
        /// </summary>
        public static void Unload(int audioId)
        {
            sBackend?.Unload(audioId);
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

            for (var i = 0; i < sChannelVolumes.Length; i++)
            {
                sChannelVolumes[i] = 1f;
                sChannelMuted[i] = false;
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
        #region UniTask 异步

        /// <summary>
        /// [UniTask] 异步播放音频
        /// </summary>
        public static async UniTask<IAudioHandle> PlayUniTaskAsync(int audioId, AudioPlayConfig config, CancellationToken cancellationToken = default)
        {
            EnsureInitialized();

            var path = ResolvePath(audioId);
            if (string.IsNullOrEmpty(path))
            {
                KitLogger.Error($"[AudioKit] 无法解析音频路径: {audioId}");
                return null;
            }

            var channelVolume = GetChannelVolume(config.Channel);
            var effectiveVolume = config.Volume * channelVolume * GetEffectiveGlobalVolume();
            var adjustedConfig = config.WithVolume(effectiveVolume);

            return await sBackend.PlayUniTaskAsync(audioId, path, adjustedConfig, cancellationToken);
        }

        /// <summary>
        /// [UniTask] 异步预加载音频
        /// </summary>
        public static async UniTask PreloadUniTaskAsync(int audioId, CancellationToken cancellationToken = default)
        {
            EnsureInitialized();

            var path = ResolvePath(audioId);
            if (string.IsNullOrEmpty(path))
            {
                KitLogger.Error($"[AudioKit] 无法解析音频路径: {audioId}");
                return;
            }

            await sBackend.PreloadUniTaskAsync(audioId, path, cancellationToken);
        }

        #endregion
#endif
    }
}
