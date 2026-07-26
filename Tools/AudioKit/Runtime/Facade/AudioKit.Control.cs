using System;
using System.Threading;
using System.Threading.Tasks;

namespace YokiFrame
{
    public static partial class AudioKit
    {
        /// <summary>立即停止仍属于当前后端代次的 voice。</summary>
        public static bool Stop(AudioVoiceHandle handle)
        {
            if (!TryGetBackend(handle, out IAudioBackend backend)) return false;
            bool stopped = backend.Stop(handle.VoiceId);
#if UNITY_EDITOR || (GODOT && TOOLS)
            if (stopped) RecordControl("play_stopped", handle, null);
#endif
            return stopped;
        }

        /// <summary>按指定秒数淡出并停止当前代次 voice。</summary>
        public static bool StopWithFade(AudioVoiceHandle handle, float fadeDuration)
        {
            float normalizedDuration = NormalizeDuration(fadeDuration, nameof(fadeDuration));
            if (!TryGetBackend(handle, out IAudioBackend backend)) return false;
            bool stopped = backend.StopWithFade(handle.VoiceId, normalizedDuration);
#if UNITY_EDITOR || (GODOT && TOOLS)
            if (stopped) RecordControl(normalizedDuration > 0f ? "play_stop_requested" : "play_stopped", handle, null);
#endif
            return stopped;
        }

        /// <summary>停止当前已创建后端的全部 voice；没有后端时保持无副作用。</summary>
        public static void StopAll()
        {
            IAudioBackend backend = GetBackend();
            if (backend == null) return;
            backend.StopAll();
#if UNITY_EDITOR || (GODOT && TOOLS)
            RecordControl("stop_all", default, null);
#endif
        }

        /// <summary>停止当前后端指定逻辑总线的全部 voice；Master 等价于 StopAll。</summary>
        public static void StopBus(string bus)
        {
            string normalized = NormalizeBus(bus, AudioBus.Sfx);
            if (string.Equals(normalized, AudioBus.Master, StringComparison.OrdinalIgnoreCase))
            {
                StopAll();
                return;
            }

            IAudioBackend backend = GetBackend();
            if (backend == null) return;
            backend.StopBus(normalized);
#if UNITY_EDITOR || (GODOT && TOOLS)
            RecordControl("stop_bus", default, normalized);
#endif
        }

        /// <summary>暂停当前后端全部 voice；不会为此创建默认后端。</summary>
        public static void PauseAll()
        {
            IAudioBackend backend = GetBackend();
            if (backend == null) return;
            backend.PauseAll();
#if UNITY_EDITOR || (GODOT && TOOLS)
            BumpDiagnosticVersion();
#endif
        }

        /// <summary>恢复当前后端全部 voice；不会为此创建默认后端。</summary>
        public static void ResumeAll()
        {
            IAudioBackend backend = GetBackend();
            if (backend == null) return;
            backend.ResumeAll();
#if UNITY_EDITOR || (GODOT && TOOLS)
            BumpDiagnosticVersion();
#endif
        }

        /// <summary>同步预加载音频资源；这是会创建默认后端的真实业务调用。</summary>
        public static bool Preload(string path)
        {
            string normalizedPath = NormalizePath(path);
            return EnsureBackend().Preload(normalizedPath);
        }

        /// <summary>异步预加载音频资源并响应当前调用取消。</summary>
        public static Task<bool> PreloadAsync(string path, CancellationToken token = default)
        {
            string normalizedPath = NormalizePath(path);
            return EnsureBackend().PreloadAsync(normalizedPath, token);
        }

        /// <summary>卸载当前后端指定缓存资源；没有后端时不创建。</summary>
        public static void Unload(string path)
        {
            IAudioBackend backend = GetBackend();
            if (backend != null) backend.Unload(NormalizePath(path));
        }

        /// <summary>卸载当前后端全部缓存资源；没有后端时不创建。</summary>
        public static void UnloadAll()
        {
            IAudioBackend backend = GetBackend();
            if (backend != null) backend.UnloadAll();
        }
    }
}
