using System;
using System.Threading;
using System.Threading.Tasks;

namespace YokiFrame
{
    /// <summary>使用项目委托快速接入自定义音频资源系统。</summary>
    public sealed class DelegateAudioResourceLoader : IAudioResourceLoader
    {
        private readonly Func<string, object> mLoad;
        private readonly Func<string, CancellationToken, Task<object>> mLoadAsync;
        private readonly Action<object> mRelease;

        /// <summary>创建同步或异步委托资源加载器。</summary>
        public DelegateAudioResourceLoader(
            string loaderName,
            Func<string, object> load,
            Action<object> release,
            Func<string, CancellationToken, Task<object>> loadAsync = null)
        {
            LoaderName = string.IsNullOrWhiteSpace(loaderName) ? nameof(DelegateAudioResourceLoader) : loaderName.Trim();
            mLoad = load ?? throw new ArgumentNullException(nameof(load));
            mRelease = release;
            mLoadAsync = loadAsync;
        }

        /// <summary>获取项目提供的加载器名称。</summary>
        public string LoaderName { get; }

        /// <summary>同步调用项目加载委托并转换为目标类型。</summary>
        public T Load<T>(string path) where T : class => mLoad(path) as T;

        /// <summary>优先调用项目异步委托；未提供时返回同步结果。</summary>
        public async Task<T> LoadAsync<T>(string path, CancellationToken token) where T : class
        {
            token.ThrowIfCancellationRequested();
            if (mLoadAsync == null)
            {
                return Load<T>(path);
            }

            object asset = await mLoadAsync(path, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            return asset as T;
        }

        /// <summary>把非空资源交还项目释放委托。</summary>
        public void Release(object asset)
        {
            if (asset != null && mRelease != null)
            {
                mRelease(asset);
            }
        }
    }
}
