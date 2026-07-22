using System.Threading;
using System.Threading.Tasks;

namespace YokiFrame
{
    /// <summary>定义原生音频后端使用的可替换资源加载边界。</summary>
    public interface IAudioResourceLoader
    {
        /// <summary>获取诊断展示名称。</summary>
        string LoaderName { get; }

        /// <summary>同步加载指定类型资源。</summary>
        T Load<T>(string path) where T : class;

        /// <summary>异步加载指定类型资源，并响应当前调用的取消令牌。</summary>
        Task<T> LoadAsync<T>(string path, CancellationToken token) where T : class;

        /// <summary>释放由当前加载器返回的一次资源租约。</summary>
        void Release(object asset);
    }
}
