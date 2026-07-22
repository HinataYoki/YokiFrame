using System.Threading;
using System.Threading.Tasks;

namespace YokiFrame
{
    /// <summary>通过 ResKit 默认或显式 Provider 加载原生音频资源。</summary>
    public sealed class ResKitAudioResourceLoader : IAudioResourceLoader
    {
        internal static readonly ResKitAudioResourceLoader Shared = new();

        /// <summary>限制外部创建重复无状态实例。</summary>
        private ResKitAudioResourceLoader() { }

        /// <summary>获取稳定加载器名称。</summary>
        public string LoaderName => "ResKit";

        /// <summary>通过 ResKit 同步加载并登记匿名 lease。</summary>
        public T Load<T>(string path) where T : class => ResKit.Load<T>(path);

        /// <summary>通过 ResKit 的 Task 桥接异步加载，保持 AudioKit 不依赖可选 UniTask 程序集。</summary>
        public Task<T> LoadAsync<T>(string path, CancellationToken token) where T : class
        {
            return ResKit.LoadTaskAsync<T>(path, token);
        }

        /// <summary>消费 ResKit 为该资源登记的一次匿名 lease。</summary>
        public void Release(object asset)
        {
            if (asset != null)
            {
                ResKit.Release(asset);
            }
        }
    }
}
