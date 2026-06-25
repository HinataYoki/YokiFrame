using System;
using System.Threading;
#if YOKIFRAME_UNITASK_SUPPORT
using Cysharp.Threading.Tasks;
#else
using System.Threading.Tasks;
#endif

namespace YokiFrame
{
    /// <summary>
    /// 将旧的 IResourceProvider 适配为 AudioKit 资源加载器。
    /// </summary>
    public sealed class ResourceProviderAudioResourceLoader : IAudioResourceLoader
    {
        private readonly IResourceProvider mProvider;

        public ResourceProviderAudioResourceLoader(IResourceProvider provider)
        {
            mProvider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        public string LoaderName => mProvider.ProviderName;

        public T Load<T>(string path) where T : class
        {
            return mProvider.Load<T>(path);
        }

#if YOKIFRAME_UNITASK_SUPPORT
        public UniTask<T> LoadAsync<T>(string path, CancellationToken token = default) where T : class
        {
            return mProvider.LoadAsync<T>(path, token);
        }
#else
        public Task<T> LoadAsync<T>(string path, CancellationToken token = default) where T : class
        {
            return mProvider.LoadAsync<T>(path, token);
        }
#endif

        public void Release(object asset)
        {
            if (asset != null)
                mProvider.Release(asset);
        }
    }
}
