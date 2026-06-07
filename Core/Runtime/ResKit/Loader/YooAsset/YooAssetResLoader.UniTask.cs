#if YOKIFRAME_YOOASSET_SUPPORT && YOKIFRAME_UNITASK_SUPPORT
using System.Threading;
using Cysharp.Threading.Tasks;
using Object = UnityEngine.Object;

namespace YokiFrame
{
    /// <summary>
    /// YooAsset 资源加载器 UniTask 扩展。
    /// 继承版本无关的 YooAssetResLoader，仅追加 UniTask 异步方法。
    /// 本文件零内部 #if。
    /// </summary>
    public sealed class YooAssetResLoaderUniTask : YooAssetResLoader,
        IResLoaderUniTask, IAllAssetsLoaderUniTask, ISubAssetsLoaderUniTask
    {
        private readonly IYooAssetResUniTaskProvider mUniTaskProvider;

        internal YooAssetResLoaderUniTask(IResLoaderPool pool, IYooAssetResUniTaskProvider provider)
            : base(pool, provider)
        {
            mUniTaskProvider = provider;
        }

        public async UniTask<T> LoadUniTaskAsync<T>(string path, CancellationToken cancellationToken = default)
            where T : Object
        {
            var asset = await mUniTaskProvider.LoadAssetUniTaskAsync<T>(path, cancellationToken);
            ResLoadTracker.OnLoad(this, path, typeof(T), asset);
            return asset;
        }

        public async UniTask<T[]> LoadAllUniTaskAsync<T>(string path, CancellationToken cancellationToken = default)
            where T : Object
        {
            return await mUniTaskProvider.LoadAllAssetsUniTaskAsync<T>(path, cancellationToken);
        }

        public async UniTask<SubAssetsResult<T>> LoadSubUniTaskAsync<T>(
            string path, CancellationToken cancellationToken = default) where T : Object
        {
            return await mUniTaskProvider.LoadSubAssetsUniTaskAsync<T>(path, cancellationToken);
        }
    }
}
#endif
