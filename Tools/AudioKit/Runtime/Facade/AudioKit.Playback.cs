using System;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

namespace YokiFrame
{
    public static partial class AudioKit
    {
        /// <summary>使用默认参数同步播放指定音频路径。</summary>
        public static AudioVoiceHandle Play(string path) => Play(path, AudioPlayOptions.Default);

        /// <summary>使用指定跨宿主参数同步播放音频。</summary>
        public static AudioVoiceHandle Play(string path, AudioPlayOptions options)
        {
            string normalizedPath = NormalizePath(path);
            AudioPlayOptions normalizedOptions = NormalizeOptions(options);
            IAudioBackend backend = EnsureBackend();
            AudioVoiceHandle handle = CreateHandleForBackend(
                backend,
                backend.Play(normalizedPath, normalizedOptions));
#if UNITY_EDITOR || (GODOT && TOOLS)
            RecordPlayback("play_started", handle, normalizedPath, normalizedOptions);
#endif
            return handle;
        }

        /// <summary>在 Music 总线播放音乐。</summary>
        public static AudioVoiceHandle PlayMusic(string path, bool loop = true, float volume = 1f)
        {
            AudioPlayOptions options = AudioPlayOptions.Default;
            options.Bus = AudioBus.Music;
            options.Loop = loop;
            options.Volume = volume;
            return Play(path, options);
        }

        /// <summary>在 Sfx 总线播放一次音效。</summary>
        public static AudioVoiceHandle PlaySfx(string path, float volume = 1f, float pitch = 1f)
        {
            AudioPlayOptions options = AudioPlayOptions.Default;
            options.Bus = AudioBus.Sfx;
            options.Volume = volume;
            options.Pitch = pitch;
            return Play(path, options);
        }

        /// <summary>异步加载并播放指定音频，取消只影响当前启动请求。
        /// await 前后校验后端身份，避免替换后端后用新代次包装旧 voiceId。</summary>
        public static async Task<AudioVoiceHandle> PlayAsync(
            string path,
            AudioPlayOptions options,
            CancellationToken token = default)
        {
            string normalizedPath = NormalizePath(path);
            AudioPlayOptions normalizedOptions = NormalizeOptions(options);
            IAudioBackend backend = EnsureBackend();
            int voiceId = 0;
            try
            {
                voiceId = await backend.PlayAsync(normalizedPath, normalizedOptions, token).ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
            }
            catch (OperationCanceledException)
            {
                // 后端可能已经在取消信号到达前创建 voice；调用方拿不到 handle 时必须回收该 voice。
                await TryStopVoiceAsync(backend, voiceId).ConfigureAwait(false);
                throw;
            }

            AudioVoiceHandle handle = CreateHandleForBackend(backend, voiceId);
            if (!handle.IsValid && voiceId > 0)
            {
                // 异步期间后端已替换或释放：尽量停止旧后端上的孤儿 voice，绝不绑定新代次。
                await TryStopVoiceAsync(backend, voiceId).ConfigureAwait(false);

                return default;
            }

#if UNITY_EDITOR || (GODOT && TOOLS)
            RecordPlayback("play_started", handle, normalizedPath, normalizedOptions);
#endif
            return handle;
        }

        /// <summary>尽力停止异步启动失败或代次失效的 voice；旧后端已释放时忽略清理异常。</summary>
        private static async Task TryStopVoiceAsync(IAudioBackend backend, int voiceId)
        {
            if (backend == null || voiceId <= 0)
            {
                return;
            }

            try
            {
                if (backend is IAudioBackendAsyncCleanup asyncCleanup)
                {
                    await asyncCleanup.StopVoiceAsync(voiceId).ConfigureAwait(false);
                }
                else
                {
                    backend.Stop(voiceId);
                }
            }
            catch (Exception)
            {
                // 后端替换或宿主退出可能已完成清理，不能用二次清理异常覆盖原始取消结果。
            }
        }

        /// <summary>在固定世界位置播放 3D 音频。</summary>
        public static AudioVoiceHandle Play3D(string path, Vector3 position) =>
            Play3D(path, position, AudioPlayOptions.Default);

        /// <summary>在固定世界位置播放 3D 音频。</summary>
        public static AudioVoiceHandle Play3D(string path, Vector3 position, AudioPlayOptions options)
        {
            options.Is3D = true;
            options.Position = position;
            options.FollowTarget = null;
            return Play(path, options);
        }

        /// <summary>播放跟随窄位置目标的 3D 音频。</summary>
        public static AudioVoiceHandle Play3D(string path, IAudioFollowTarget target) =>
            Play3D(path, target, AudioPlayOptions.Default);

        /// <summary>播放跟随窄位置目标的 3D 音频。</summary>
        public static AudioVoiceHandle Play3D(string path, IAudioFollowTarget target, AudioPlayOptions options)
        {
            options.Is3D = true;
            options.FollowTarget = target;
            if (target != null && target.IsAlive)
            {
                options.Position = target.Position;
            }

            return Play(path, options);
        }
    }
}
