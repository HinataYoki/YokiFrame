#if YOKIFRAME_YOOASSET_SUPPORT && YOOASSET_2_3_OR_NEWER && YOKIFRAME_UNITASK_SUPPORT
using System.Threading;
using Cysharp.Threading.Tasks;
using YooAsset;
using Object = UnityEngine.Object;

namespace YokiFrame
{
    /// <summary>
    /// YooAsset 2.x 资源加载池（支持 UniTask）
    /// </summary>
    public class YooAssetResLoaderUniTaskPool : YooAssetResLoaderPool
    {
        public YooAssetResLoaderUniTaskPool() : base() { }
        public YooAssetResLoaderUniTaskPool(string packageName) : base(packageName) { }

        protected override IResLoader CreateLoader() => new YooAssetResLoaderUniTask(this);
    }

    /// <summary>
    /// YooAsset 2.x 资源加载器（支持 UniTask）
    /// </summary>
    public class YooAssetResLoaderUniTask : YooAssetResLoader, IResLoaderUniTask, IAllAssetsLoaderUniTask, ISubAssetsLoaderUniTask
    {
        public YooAssetResLoaderUniTask(IResLoaderPool pool)
            : base(pool) { }

        public async UniTask<T> LoadUniTaskAsync<T>(string path, CancellationToken cancellationToken = default)
            where T : Object
        {
            mHandle = YooAssets.LoadAssetAsync<T>(path);
            await mHandle.ToUniTask(cancellationToken: cancellationToken);
            var asset = mHandle.AssetObject as T;
            ResLoadTracker.OnLoad(this, path, typeof(T), asset);
            return asset;
        }

        public async UniTask<T[]> LoadAllUniTaskAsync<T>(string path, CancellationToken cancellationToken = default)
            where T : Object
        {
            mAllAssetsHandle = YooAssets.LoadAllAssetsAsync<T>(path);
            await mAllAssetsHandle.ToUniTask(cancellationToken: cancellationToken);

            if (mAllAssetsHandle.Status != EOperationStatus.Succeed)
                return System.Array.Empty<T>();

            var objects = mAllAssetsHandle.AllAssetObjects;
            var result = new T[objects.Count];
            for (int i = 0; i < objects.Count; i++)
                result[i] = objects[i] as T;
            return result;
        }

        public async UniTask<SubAssetsResult<T>> LoadSubUniTaskAsync<T>(string path, CancellationToken cancellationToken = default)
            where T : Object
        {
            mSubAssetsHandle = YooAssets.LoadSubAssetsAsync<T>(path);
            await mSubAssetsHandle.ToUniTask(cancellationToken: cancellationToken);

            if (mSubAssetsHandle.Status != EOperationStatus.Succeed)
                return default;

            var objects = mSubAssetsHandle.SubAssetObjects;
            T mainAsset = null;
            var subList = new System.Collections.Generic.List<T>(objects.Count);
            foreach (var obj in objects)
            {
                if (obj is T typed)
                {
                    mainAsset ??= typed;
                    subList.Add(typed);
                }
            }
            return new SubAssetsResult<T>(mainAsset, subList.ToArray());
        }
    }
}
#endif
