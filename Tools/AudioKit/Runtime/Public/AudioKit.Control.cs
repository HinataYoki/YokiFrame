using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// AudioKit 播放控制、预加载和卸载 API。
    /// </summary>
    public static partial class AudioKit
    {
        /// <summary>
        /// 停止指定播放 voice。
        /// </summary>
        /// <param name="voiceId">播放 voice 标识。</param>
        /// <returns>成功停止时返回 true。</returns>
        public static bool Stop(int voiceId)
        {
            if (voiceId <= 0)
                return false;

            var backend = EnsureBackend();
            var active = ListPool<AudioVoiceDebugInfo>.Get();
            bool stopped;
            try
            {
                backend.GetActiveVoices(active);
                var beforeStop = FindVoice(active, voiceId);
                stopped = backend.Stop(voiceId);
                if (stopped)
                    Record("play_stopped", beforeStop ?? new AudioVoiceDebugInfo { VoiceId = voiceId, BackendName = backend.BackendName });
            }
            finally
            {
                ListPool<AudioVoiceDebugInfo>.Release(active);
            }

            return stopped;
        }

        /// <summary>
        /// 按 voice 自身记录的淡出时长停止播放。
        /// </summary>
        /// <param name="voiceId">播放 voice 标识。</param>
        /// <returns>成功发起停止时返回 true。</returns>
        public static bool StopWithFade(int voiceId)
        {
            if (voiceId <= 0)
                return false;

            var backend = EnsureBackend();
            var active = ListPool<AudioVoiceDebugInfo>.Get();
            try
            {
                backend.GetActiveVoices(active);
                var beforeStop = FindVoice(active, voiceId);
                var fadeDuration = beforeStop != null ? beforeStop.FadeOutDuration : 0f;
                return StopWithFadeCore(backend, beforeStop, voiceId, fadeDuration);
            }
            finally
            {
                ListPool<AudioVoiceDebugInfo>.Release(active);
            }
        }

        /// <summary>
        /// 使用指定淡出时长停止播放。
        /// </summary>
        /// <param name="voiceId">播放 voice 标识。</param>
        /// <param name="fadeDuration">淡出时长，单位秒。</param>
        /// <returns>成功发起停止时返回 true。</returns>
        public static bool StopWithFade(int voiceId, float fadeDuration)
        {
            if (voiceId <= 0)
                return false;

            var backend = EnsureBackend();
            var active = ListPool<AudioVoiceDebugInfo>.Get();
            try
            {
                backend.GetActiveVoices(active);
                var beforeStop = FindVoice(active, voiceId);
                return StopWithFadeCore(backend, beforeStop, voiceId, fadeDuration);
            }
            finally
            {
                ListPool<AudioVoiceDebugInfo>.Release(active);
            }
        }

        /// <summary>
        /// 停止所有正在播放的 voice。
        /// </summary>
        public static void StopAll()
        {
            var backend = EnsureBackend();
            var active = ListPool<AudioVoiceDebugInfo>.Get();
            try
            {
                backend.GetActiveVoices(active);
                backend.StopAll();

                for (var i = 0; i < active.Count; i++)
                    Record("play_stopped", active[i]);
            }
            finally
            {
                ListPool<AudioVoiceDebugInfo>.Release(active);
            }
        }

        /// <summary>
        /// 停止指定内置音频通道上的所有 voice。
        /// </summary>
        /// <param name="channel">音频通道。</param>
        public static void StopChannel(AudioChannel channel)
        {
            StopBus(ToBus(channel));
        }

        /// <summary>
        /// 使用旧版整数通道停止所有 voice。
        /// </summary>
        /// <param name="channelId">旧版通道编号，0-4 对应内置通道，5+ 对应自定义通道。</param>
        public static void StopChannel(int channelId)
        {
            StopBus(ToBus(channelId));
        }

        /// <summary>
        /// 停止指定总线上的所有 voice。
        /// </summary>
        /// <param name="bus">音频总线名称。</param>
        public static void StopChannel(string bus)
        {
            StopBus(bus);
        }

        /// <summary>
        /// 停止指定总线上的所有 voice。
        /// </summary>
        /// <param name="bus">音频总线名称。</param>
        public static void StopBus(string bus)
        {
            var backend = EnsureBackend();
            var normalizedBus = NormalizeBus(bus);
            var active = ListPool<AudioVoiceDebugInfo>.Get();
            try
            {
                backend.GetActiveVoices(active);
                backend.StopBus(normalizedBus);

                for (var i = 0; i < active.Count; i++)
                {
                    if (string.Equals(active[i].Bus, normalizedBus, StringComparison.OrdinalIgnoreCase))
                        Record("play_stopped", active[i]);
                }
            }
            finally
            {
                ListPool<AudioVoiceDebugInfo>.Release(active);
            }
        }

        /// <summary>
        /// 暂停所有播放。
        /// </summary>
        public static void PauseAll()
        {
            EnsureBackend().PauseAll();
        }

        /// <summary>
        /// 恢复所有暂停的播放。
        /// </summary>
        public static void ResumeAll()
        {
            EnsureBackend().ResumeAll();
        }

        /// <summary>
        /// 预加载指定路径的音频资源。
        /// </summary>
        /// <param name="path">音频资源路径。</param>
        public static void Preload(string path)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("path is empty", nameof(path));

            EnsureBackend().Preload(path);
        }

        /// <summary>
        /// 设置音频后端使用的资源提供器。
        /// </summary>
        /// <param name="provider">资源提供器实例。</param>
        public static void SetResourceProvider(IResourceProvider provider)
        {
            SetResourceLoader(provider != null ? new ResourceProviderAudioResourceLoader(provider) : null);

            var backend = GetBackend();
            if (backend != null)
                backend.SetResourceProvider(provider);
        }

        /// <summary>
        /// 通过音频 ID 预加载资源。
        /// </summary>
        /// <param name="audioId">音频资源 ID。</param>
        public static void Preload(int audioId)
        {
            Preload(ResolvePath(audioId));
        }

        /// <summary>
        /// 异步预加载指定路径的音频资源。
        /// </summary>
        /// <param name="path">音频资源路径。</param>
        /// <param name="onComplete">预加载完成回调。</param>
        public static void PreloadAsync(string path, Action onComplete = null)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("path is empty", nameof(path));

            EnsureBackend().PreloadAsync(path, onComplete);
        }

        /// <summary>
        /// 通过音频 ID 异步预加载资源。
        /// </summary>
        /// <param name="audioId">音频资源 ID。</param>
        /// <param name="onComplete">预加载完成回调。</param>
        public static void PreloadAsync(int audioId, Action onComplete = null)
        {
            PreloadAsync(ResolvePath(audioId), onComplete);
        }

        /// <summary>
        /// 卸载指定路径的音频资源。
        /// </summary>
        /// <param name="path">音频资源路径。</param>
        public static void Unload(string path)
        {
            if (string.IsNullOrEmpty(path))
                return;

            var backend = GetBackend();
            if (backend == null)
                return;

            backend.Unload(path);
        }

        /// <summary>
        /// 通过音频 ID 卸载资源。
        /// </summary>
        /// <param name="audioId">音频资源 ID。</param>
        public static void Unload(int audioId)
        {
            var path = ResolvePath(audioId);
            if (!string.IsNullOrEmpty(path))
                Unload(path);
        }

        /// <summary>
        /// 卸载音频后端持有的所有资源。
        /// </summary>
        public static void UnloadAll()
        {
            var backend = GetBackend();
            if (backend == null)
                return;

            backend.UnloadAll();
        }
    }
}
