using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// 引擎无关音频门面。播放实现由 IAudioBackend 注入。
    /// </summary>
    public static partial class AudioKit
    {
        private const int MAX_HISTORY = 128;

        private static readonly object sLock = new();
        private static readonly Queue<AudioHistoryRecord> sHistory = new(MAX_HISTORY);
        private static readonly Dictionary<string, float> sBusVolumes = new(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> sMutedBuses = new(StringComparer.OrdinalIgnoreCase);
        private static IAudioBackend sBackend;
        private static IAudioResourceLoader sResourceLoader;
        private static Func<int, string> sPathResolver;
        private static float sGlobalVolume = 1f;
        private static bool sGlobalMuted;

        /// <summary>
        /// 当前音频后端名称；未注入后端时返回 None。
        /// </summary>
        public static string BackendName => sBackend != null ? sBackend.BackendName : "None";

        /// <summary>
        /// 当前是否已注入音频后端。
        /// </summary>
        public static bool HasBackend => sBackend != null;

        /// <summary>
        /// 设置 AudioKit 使用的引擎音频后端。
        /// </summary>
        /// <param name="backend">音频后端实例；传入空值表示清除后端。</param>
        public static void SetBackend(IAudioBackend backend)
        {
            lock (sLock)
                sBackend = backend;

            SyncBackendState(backend);
        }

        /// <summary>
        /// 获取当前音频后端。
        /// </summary>
        /// <returns>当前后端；未注入时返回空。</returns>
        public static IAudioBackend GetBackend()
        {
            lock (sLock)
                return sBackend;
        }

        /// <summary>
        /// 清除当前音频后端引用。
        /// </summary>
        public static void ClearBackend()
        {
            lock (sLock)
                sBackend = null;
        }

        /// <summary>
        /// 停止播放并重置 AudioKit 的后端、路径解析器、音量、静音和历史状态。
        /// </summary>
        public static void Reset()
        {
            var backend = GetBackend();
            if (backend != null)
                backend.StopAll();

            lock (sLock)
            {
                sBackend = null;
                sResourceLoader = null;
                sPathResolver = null;
                sGlobalVolume = 1f;
                sGlobalMuted = false;
                sBusVolumes.Clear();
                sMutedBuses.Clear();
                sHistory.Clear();
            }
        }

        /// <summary>
        /// 设置音频 ID 到资源路径的解析器。
        /// </summary>
        /// <param name="resolver">路径解析器；传入空值时回退到默认 Audio/{id} 路径。</param>
        public static void SetPathResolver(Func<int, string> resolver)
        {
            lock (sLock)
                sPathResolver = resolver;
        }
    }
}
