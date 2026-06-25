using System;
using System.Threading;
#if YOKIFRAME_UNITASK_SUPPORT
using Cysharp.Threading.Tasks;
#else
using System.Threading.Tasks;
#endif

namespace YokiFrame
{
    public static partial class AudioKit
    {
        /// <summary>
        /// 当前资源加载器名称；未自定义时为 ResKit。
        /// </summary>
        public static string ResourceLoaderName => EnsureResourceLoader().LoaderName;

        /// <summary>
        /// 设置 AudioKit 使用的资源加载器。传入空值时回退到默认 ResKit 加载器。
        /// </summary>
        /// <param name="loader">资源加载器。</param>
        public static void SetResourceLoader(IAudioResourceLoader loader)
        {
            lock (sLock)
                sResourceLoader = loader;
        }

        /// <summary>
        /// 获取当前有效资源加载器。
        /// </summary>
        /// <returns>当前自定义加载器，或默认 ResKit 加载器。</returns>
        public static IAudioResourceLoader GetResourceLoader()
        {
            return EnsureResourceLoader();
        }

        /// <summary>
        /// 通过当前 AudioKit 资源加载器同步加载资源。
        /// </summary>
        /// <typeparam name="T">资源对象类型。</typeparam>
        /// <param name="path">资源路径。</param>
        /// <returns>加载到的资源；失败时返回空。</returns>
        public static T LoadResource<T>(string path) where T : class
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("path is empty", nameof(path));

            return EnsureResourceLoader().Load<T>(path);
        }

#if YOKIFRAME_UNITASK_SUPPORT
        /// <summary>
        /// 通过当前 AudioKit 资源加载器异步加载资源。
        /// </summary>
        /// <typeparam name="T">资源对象类型。</typeparam>
        /// <param name="path">资源路径。</param>
        /// <param name="token">取消令牌。</param>
        /// <returns>加载到的资源；失败时返回空。</returns>
        public static UniTask<T> LoadResourceAsync<T>(string path, CancellationToken token = default) where T : class
#else
        /// <summary>
        /// 通过当前 AudioKit 资源加载器异步加载资源。
        /// </summary>
        /// <typeparam name="T">资源对象类型。</typeparam>
        /// <param name="path">资源路径。</param>
        /// <param name="token">取消令牌。</param>
        /// <returns>加载到的资源；失败时返回空。</returns>
        public static Task<T> LoadResourceAsync<T>(string path, CancellationToken token = default) where T : class
#endif
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("path is empty", nameof(path));

            return EnsureResourceLoader().LoadAsync<T>(path, token);
        }

        /// <summary>
        /// 释放由当前 AudioKit 资源加载器加载的资源。
        /// </summary>
        /// <param name="asset">资源对象。</param>
        public static void ReleaseResource(object asset)
        {
            if (asset == null)
                return;

            EnsureResourceLoader().Release(asset);
        }

        private static IAudioResourceLoader EnsureResourceLoader()
        {
            lock (sLock)
                return sResourceLoader ?? ResKitAudioResourceLoader.Shared;
        }
    }
}
