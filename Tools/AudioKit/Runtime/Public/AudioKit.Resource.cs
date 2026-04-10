using System;

namespace YokiFrame
{
    /// <summary>
    /// AudioKit - 资源管理
    /// </summary>
    public static partial class AudioKit
    {
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
    }
}
