#if YOKIFRAME_YOOASSET_SUPPORT
using System;
using System.Collections.Generic;
using YooAsset;
using Object = UnityEngine.Object;

namespace YokiFrame
{
    /// <summary>
    /// YooAsset 资源加载池
    /// </summary>
    public class YooAssetResLoaderPool : IResLoaderPool
    {
        private readonly Stack<IResLoader> mPool = new();
        private readonly ResourcePackage mPackage;

        public YooAssetResLoaderPool() : this(YooAssets.GetPackage("DefaultPackage")) { }
        public YooAssetResLoaderPool(string packageName) : this(YooAssets.GetPackage(packageName)) { }
        public YooAssetResLoaderPool(ResourcePackage package)
            => mPackage = package ?? throw new ArgumentNullException(nameof(package));

        public IResLoader Allocate() => mPool.Count > 0 ? mPool.Pop() : new YooAssetResLoader(this, mPackage);
        public void Recycle(IResLoader loader) => mPool.Push(loader);
    }

    /// <summary>
    /// YooAsset 资源加载器
    /// </summary>
    public class YooAssetResLoader : IResLoader
    {
        private readonly IResLoaderPool mPool;
        private readonly ResourcePackage mPackage;
        private AssetHandle mHandle;

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
