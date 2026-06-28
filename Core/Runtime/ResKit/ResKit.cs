using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
#if YOKIFRAME_UNITASK_SUPPORT
using Cysharp.Threading.Tasks;
#else
using System.Threading.Tasks;
#endif
namespace YokiFrame
{
    /// <summary>
    /// 引擎无关资源门面。引擎差异由 IResourceProvider 实现承载。
    /// </summary>
    public static partial class ResKit
    {
        private const int MAX_UNLOAD_HISTORY = 100;

        private static readonly object sLock = new();
        private static readonly Dictionary<ResCacheKey, object> sAssetCache = new();
        private static readonly Queue<ResUnloadRecord> sUnloadHistory = new(MAX_UNLOAD_HISTORY);
        private static IResourceProvider sProvider;
        private static long sDiagnosticVersion;
        private static bool sEnableLoadLocationTracking;

        /// <summary>
        /// 是否记录资源加载调用位置。默认关闭，避免普通运行时加载路径付出堆栈采集成本。
        /// </summary>
        public static bool EnableLoadLocationTracking
        {
            get { return sEnableLoadLocationTracking; }
            set
            {
                if (sEnableLoadLocationTracking == value)
                    return;

                sEnableLoadLocationTracking = value;
                BumpDiagnosticVersion();
            }
        }

        /// <summary>
        /// ResKit 诊断状态版本。
        /// </summary>
        public static long DiagnosticVersion
        {
            get { return Interlocked.Read(ref sDiagnosticVersion); }
        }

        /// <summary>
        /// 当前资源 Provider 名称；未配置时返回 None。
        /// </summary>
        public static string ProviderName => sProvider != null ? sProvider.ProviderName : "None";

        /// <summary>
        /// 当前缓存中的资源数量。
        /// </summary>
        public static int LoadedCount
        {
            get
            {
                lock (sLock)
                    return sAssetCache.Count;
            }
        }

        /// <summary>
        /// 当前所有已缓存资源的总引用计数。
        /// </summary>
        public static int TotalRefCount
        {
            get
            {
                var total = 0;
                lock (sLock)
                {
                    foreach (var kvp in sAssetCache)
                    {
                        if (kvp.Value is IResHandleDebugView handle)
                            total += handle.RefCount;
                    }
                }

                return total;
            }
        }

        /// <summary>
        /// 当前保留的资源卸载历史数量。
        /// </summary>
        public static int UnloadHistoryCount
        {
            get
            {
                lock (sLock)
                    return sUnloadHistory.Count;
            }
        }

        /// <summary>
        /// 设置 ResKit 使用的资源 Provider。
        /// </summary>
        /// <param name="provider">引擎或宿主提供的资源 Provider。</param>
        /// <exception cref="ArgumentNullException">provider 为空时抛出。</exception>
        public static void SetProvider(IResourceProvider provider)
        {
            if (provider == null)
                throw new ArgumentNullException(nameof(provider));

            ClearAll();
            sProvider = provider;
            ClearSceneBackend();
            if (provider is IResSceneBackend sceneBackend)
                SetSceneBackend(sceneBackend);
            BumpDiagnosticVersion();
        }

        /// <summary>
        /// 获取当前资源 Provider。
        /// </summary>
        /// <returns>当前资源 Provider；未配置时返回空。</returns>
        public static IResourceProvider GetProvider()
        {
            return sProvider;
        }

        /// <summary>
        /// 同步加载资源对象。
        /// </summary>
        /// <typeparam name="T">资源对象类型。</typeparam>
        /// <param name="path">资源路径。</param>
        /// <returns>资源对象；加载失败时返回空。</returns>
        public static T Load<T>(string path) where T : class
        {
            var handler = LoadAsset<T>(path);
            return handler != null ? handler.Asset : null;
        }

        /// <summary>
        /// 同步加载资源并返回可释放句柄。
        /// </summary>
        /// <typeparam name="T">资源对象类型。</typeparam>
        /// <param name="path">资源路径。</param>
        /// <returns>资源句柄；加载失败时返回空。</returns>
        public static ResHandle<T> LoadAsset<T>(string path) where T : class
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("path is empty", nameof(path));

            var provider = EnsureProvider();
            var key = new ResCacheKey(typeof(T), path);

            lock (sLock)
            {
                if (sAssetCache.TryGetValue(key, out var cached))
                {
                    var cachedHandle = cached as ResHandle<T>;
                    if (cachedHandle != null)
                    {
                        cachedHandle.Retain();
                        return cachedHandle;
                    }
                }
            }

            var asset = provider.Load<T>(path);
            if (asset == null)
                return null;

            var source = CaptureLoadSource();
            var handle = new ResHandle<T>(path, asset, provider.ProviderName, source.Display, source.FilePath, source.Line);
            lock (sLock)
                sAssetCache[key] = handle;
            BumpDiagnosticVersion();

            return handle;
        }

#if YOKIFRAME_UNITASK_SUPPORT
        /// <summary>
        /// 异步加载资源对象。
        /// </summary>
        /// <typeparam name="T">资源对象类型。</typeparam>
        /// <param name="path">资源路径。</param>
        /// <param name="token">取消令牌。</param>
        /// <returns>资源对象；加载失败时返回空。</returns>
        public static async UniTask<T> LoadAsync<T>(string path, CancellationToken token = default) where T : class
#else
        /// <summary>
        /// 异步加载资源对象。
        /// </summary>
        /// <typeparam name="T">资源对象类型。</typeparam>
        /// <param name="path">资源路径。</param>
        /// <param name="token">取消令牌。</param>
        /// <returns>资源对象；加载失败时返回空。</returns>
        public static async Task<T> LoadAsync<T>(string path, CancellationToken token = default) where T : class
#endif
        {
#if YOKIFRAME_UNITASK_SUPPORT
            var handler = await LoadAssetAsync<T>(path, token);
#else
            var handler = await LoadAssetAsync<T>(path, token).ConfigureAwait(false);
#endif
            return handler != null ? handler.Asset : null;
        }

#if YOKIFRAME_UNITASK_SUPPORT
        /// <summary>
        /// 异步加载资源并返回可释放句柄。
        /// </summary>
        /// <typeparam name="T">资源对象类型。</typeparam>
        /// <param name="path">资源路径。</param>
        /// <param name="token">取消令牌。</param>
        /// <returns>资源句柄；加载失败时返回空。</returns>
        public static async UniTask<ResHandle<T>> LoadAssetAsync<T>(string path, CancellationToken token = default) where T : class
#else
        /// <summary>
        /// 异步加载资源并返回可释放句柄。
        /// </summary>
        /// <typeparam name="T">资源对象类型。</typeparam>
        /// <param name="path">资源路径。</param>
        /// <param name="token">取消令牌。</param>
        /// <returns>资源句柄；加载失败时返回空。</returns>
        public static async Task<ResHandle<T>> LoadAssetAsync<T>(string path, CancellationToken token = default) where T : class
#endif
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("path is empty", nameof(path));

            var provider = EnsureProvider();
            var key = new ResCacheKey(typeof(T), path);

            lock (sLock)
            {
                if (sAssetCache.TryGetValue(key, out var cached))
                {
                    var cachedHandle = cached as ResHandle<T>;
                    if (cachedHandle != null)
                    {
                        cachedHandle.Retain();
                        return cachedHandle;
                    }
                }
            }

#if YOKIFRAME_UNITASK_SUPPORT
            var asset = await provider.LoadAsync<T>(path, token);
#else
            var asset = await provider.LoadAsync<T>(path, token).ConfigureAwait(false);
#endif
            if (asset == null)
                return null;

            var source = CaptureLoadSource();
            var handle = new ResHandle<T>(path, asset, provider.ProviderName, source.Display, source.FilePath, source.Line);
            lock (sLock)
            {
                sAssetCache[key] = handle;
                BumpDiagnosticVersion();
            }

            return handle;
        }

        internal static void BumpDiagnosticVersion()
        {
            Interlocked.Increment(ref sDiagnosticVersion);
        }

        /// <summary>
        /// 实例化指定路径的资源对象。
        /// </summary>
        /// <param name="path">资源路径。</param>
        /// <returns>引擎对象抽象。</returns>
        public static IEngineObject Instantiate(string path)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("path is empty", nameof(path));

            return EnsureProvider().Instantiate(path);
        }
    }
}
