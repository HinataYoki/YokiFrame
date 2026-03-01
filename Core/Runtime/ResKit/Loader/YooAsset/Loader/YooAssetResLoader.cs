#if YOKIFRAME_YOOASSET_SUPPORT
using System;
using YooAsset;
using Object = UnityEngine.Object;

namespace YokiFrame
{
    /// <summary>
    /// YooAsset 资源加载池
    /// </summary>
    public class YooAssetResLoaderPool : AbstractResLoaderPool
    {
        protected readonly ResourcePackage mPackage;

        public YooAssetResLoaderPool() : this(YooAssets.GetPackage("DefaultPackage")) { }
        public YooAssetResLoaderPool(string packageName) : this(YooAssets.GetPackage(packageName)) { }
        public YooAssetResLoaderPool(ResourcePackage package)
            => mPackage = package ?? throw new ArgumentNullException(nameof(package));

        protected override IResLoader CreateLoader() => new YooAssetResLoader(this, mPackage);
    }

    /// <summary>
    /// YooAsset 资源加载器
    /// </summary>
    public class YooAssetResLoader : IResLoader
    {
        private readonly IResLoaderPool mPool;
        protected readonly ResourcePackage mPackage;
        protected AssetHandle mHandle;

        public YooAssetResLoader(IResLoaderPool pool, ResourcePackage package)
        {
            mPool = pool;
            mPackage = package;
        }

        public T Load<T>(string path) where T : Object
        {
            mHandle = mPackage.LoadAssetSync<T>(path);
            var asset = mHandle.GetAssetObject<T>();
            ResLoadTracker.OnLoad(this, path, typeof(T), asset);
            return asset;
        }

        public void LoadAsync<T>(string path, Action<T> onComplete) where T : Object
        {
            mHandle = mPackage.LoadAssetAsync<T>(path);
            mHandle.Completed += handle =>
            {
                var asset = handle.GetAssetObject<T>();
                ResLoadTracker.OnLoad(this, path, typeof(T), asset);
                onComplete?.Invoke(asset);
            };
        }

        public void UnloadAndRecycle()
        {
            ResLoadTracker.OnUnload(this);
            mHandle?.Release();
            mHandle = null;
            mPool.Recycle(this);
        }
    }
}
#endif
