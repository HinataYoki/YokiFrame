using System.Threading;
#if YOKIFRAME_UNITASK_SUPPORT
using Cysharp.Threading.Tasks;
#else
using System.Threading.Tasks;
#endif

namespace YokiFrame
{
    /// <summary>
    /// 资源提供者抽象接口，替代 Unity Resources.Load / YooAsset
    /// 安装 UniTask 并启用宏后，异步签名会直接切换为 UniTask。
    /// </summary>
    public interface IResourceProvider
    {
        /// <summary>
        /// 获取资源提供者名称。
        /// </summary>
        string ProviderName { get; }

        /// <summary>
        /// 同步加载指定类型的资源。
        /// </summary>
        /// <param name="path">资源路径。</param>
        /// <typeparam name="T">资源类型。</typeparam>
        /// <returns>加载到的资源；失败时返回 null。</returns>
        T Load<T>(string path) where T : class;
#if YOKIFRAME_UNITASK_SUPPORT
        /// <summary>
        /// 异步加载指定类型的资源。
        /// </summary>
        /// <param name="path">资源路径。</param>
        /// <param name="token">取消令牌。</param>
        /// <typeparam name="T">资源类型。</typeparam>
        /// <returns>加载到的资源；失败时返回 null。</returns>
        UniTask<T> LoadAsync<T>(string path, CancellationToken token = default) where T : class;
#else
        /// <summary>
        /// 异步加载指定类型的资源。
        /// </summary>
        /// <param name="path">资源路径。</param>
        /// <param name="token">取消令牌。</param>
        /// <typeparam name="T">资源类型。</typeparam>
        /// <returns>加载到的资源；失败时返回 null。</returns>
        Task<T> LoadAsync<T>(string path, CancellationToken token = default) where T : class;
#endif
        /// <summary>
        /// 从资源路径实例化引擎对象。
        /// </summary>
        /// <param name="path">资源路径。</param>
        /// <returns>实例化后的引擎对象；失败时返回 null。</returns>
        IEngineObject Instantiate(string path);

        /// <summary>
        /// 释放由资源提供者加载或实例化的资源。
        /// </summary>
        /// <param name="asset">要释放的资源对象。</param>
        void Release(object asset);
    }
}
