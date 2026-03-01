#if YOKIFRAME_YOOASSET_SUPPORT && YOKIFRAME_UNITASK_SUPPORT
using System.Threading;
using Cysharp.Threading.Tasks;
using YooAsset;
using Object = UnityEngine.Object;

namespace YokiFrame
{
    /// <summary>
    /// YooAsset 资源加载池（支持 UniTask） - 继承 YooAssetResLoaderPool，仅覆写创建方法
    /// </summary>
    public class YooAssetResLoaderUniTaskPool : YooAssetResLoaderPool
    {
        public YooAssetResLoaderUniTaskPool() : base() { }
        public YooAssetResLoaderUniTaskPool(string packageName) : base(packageName) { }
        public YooAssetResLoaderUniTaskPool(ResourcePackage package) : base(package) { }

        protected override IResLoader CreateLoader() => new YooAssetResLoaderUniTask(this, mPackage);
    }

    /// <summary>
    /// YooAsset 资源加载器（支持 UniTask） - 继承 YooAssetResLoader，仅扩展 UniTask 异步方法
    /// </summary>
    public class YooAssetResLoaderUniTask : YooAssetResLoader, IResLoaderUniTask
    {
        public YooAssetResLoaderUniTask(IResLoaderPool pool, ResourcePackage package)
            : base(pool, package) { }

        public async UniTask<T> LoadUniTaskAsync<T>(string path, CancellationToken cancellationToken = default)
            where T : Object
        {
            mHandle = mPackage.LoadAssetAsync<T>(path);
            await mHandle.ToUniTask(cancellationToken: cancellationToken);
            var asset = mHandle.GetAssetObject<T>();
            ResLoadTracker.OnLoad(this, path, typeof(T), asset);
            return asset;
        }
    }
}
#endif
