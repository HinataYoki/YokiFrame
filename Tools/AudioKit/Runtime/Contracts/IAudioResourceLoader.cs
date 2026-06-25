using System.Threading;
#if YOKIFRAME_UNITASK_SUPPORT
using Cysharp.Threading.Tasks;
#else
using System.Threading.Tasks;
#endif

namespace YokiFrame
{
    /// <summary>
    /// AudioKit 的资源加载后端。默认实现委托 ResKit，用户可替换为 Addressables、YooAsset 或项目自定义加载器。
    /// </summary>
    public interface IAudioResourceLoader
    {
        /// <summary>
        /// 加载器名称，用于诊断。
        /// </summary>
        string LoaderName { get; }

        /// <summary>
        /// 同步加载音频后端所需资源。
        /// </summary>
        /// <typeparam name="T">资源对象类型。</typeparam>
        /// <param name="path">资源路径。</param>
        /// <returns>加载到的资源；失败时返回空。</returns>
        T Load<T>(string path) where T : class;

#if YOKIFRAME_UNITASK_SUPPORT
        /// <summary>
        /// 异步加载音频后端所需资源。
        /// </summary>
        /// <typeparam name="T">资源对象类型。</typeparam>
        /// <param name="path">资源路径。</param>
        /// <param name="token">取消令牌。</param>
        /// <returns>加载到的资源；失败时返回空。</returns>
        UniTask<T> LoadAsync<T>(string path, CancellationToken token = default) where T : class;
#else
        /// <summary>
        /// 异步加载音频后端所需资源。
        /// </summary>
        /// <typeparam name="T">资源对象类型。</typeparam>
        /// <param name="path">资源路径。</param>
        /// <param name="token">取消令牌。</param>
        /// <returns>加载到的资源；失败时返回空。</returns>
        Task<T> LoadAsync<T>(string path, CancellationToken token = default) where T : class;
#endif

        /// <summary>
        /// 释放由该加载器加载的资源。
        /// </summary>
        /// <param name="asset">资源对象。</param>
        void Release(object asset);
    }
}
