#if YOKIFRAME_UNITASK_SUPPORT
using System.Threading;
using Cysharp.Threading.Tasks;
using Object = UnityEngine.Object;

namespace YokiFrame
{
    /// <summary>
    /// 支持 UniTask 的批量资源加载器接口扩展
    /// </summary>
    public interface IAllAssetsLoaderUniTask : IAllAssetsLoader
    {
        /// <summary>
        /// [UniTask] 异步加载资源包内所有指定类型的资源
        /// </summary>
        UniTask<T[]> LoadAllUniTaskAsync<T>(string path, CancellationToken cancellationToken = default)
            where T : Object;
    }

    /// <summary>
    /// 支持 UniTask 的子资源加载器接口扩展
    /// </summary>
    public interface ISubAssetsLoaderUniTask : ISubAssetsLoader
    {
        /// <summary>
        /// [UniTask] 异步加载子资源
        /// </summary>
        UniTask<SubAssetsResult<T>> LoadSubUniTaskAsync<T>(string path, CancellationToken cancellationToken = default)
            where T : Object;
    }
}
#endif
