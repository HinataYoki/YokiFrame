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
    /// 用委托快速创建 AudioKit 资源加载器，适合项目侧接入简单自定义加载逻辑。
    /// </summary>
    public sealed class DelegateAudioResourceLoader : IAudioResourceLoader
    {
        private readonly Func<string, object> mLoad;
        private readonly Action<object> mRelease;

        public DelegateAudioResourceLoader(string loaderName, Func<string, object> load, Action<object> release)
        {
            if (load == null)
                throw new ArgumentNullException(nameof(load));

            LoaderName = string.IsNullOrEmpty(loaderName) ? "DelegateAudioResourceLoader" : loaderName;
            mLoad = load;
            mRelease = release;
        }

        public string LoaderName { get; private set; }

        public T Load<T>(string path) where T : class
        {
            return mLoad(path) as T;
        }

#if YOKIFRAME_UNITASK_SUPPORT
        public UniTask<T> LoadAsync<T>(string path, CancellationToken token = default) where T : class
        {
            return UniTask.FromResult(Load<T>(path));
        }
#else
        public Task<T> LoadAsync<T>(string path, CancellationToken token = default) where T : class
        {
            return Task.FromResult(Load<T>(path));
        }
#endif

        public void Release(object asset)
        {
            if (asset != null && mRelease != null)
                mRelease(asset);
        }
    }
}
