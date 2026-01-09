#if YOKIFRAME_YOOASSET_SUPPORT && YOKIFRAME_UNITASK_SUPPORT
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using YooAsset;
using Object = UnityEngine.Object;

namespace YokiFrame
{
    /// <summary>
    /// YooAsset 资源加载池（支持 UniTask）
    /// </summary>
    public class YooAssetResLoaderUniTaskPool : IResLoaderPool
    {
        private readonly Stack<IResLoader> mPool = new();
        private readonly ResourcePackage mPackage;

        public YooAssetResLoaderUniTaskPool() : this(YooAssets.GetPackage("DefaultPackage")) { }
        public YooAssetResLoaderUniTaskPool(string packageName) : this(YooAssets.GetPackage(packageName)) { }
        public YooAssetResLoaderUniTaskPool(ResourcePackage package)
            => mPackage = package ?? throw new ArgumentNullException(nameof(package));

        public IResLoader Allocate() => mPool.Count > 0 ? mPool.Pop() : new YooAssetResLoaderUniTask(this, mPackage);
        public void Recycle(IResLoader loader) => mPool.Push(loader);
    }

    /// <summary>
    /// YooAsset 资源加载器（支持 UniTask）
    /// </summary>
    public class YooAssetResLoaderUniTask : IResLoaderUniTask
    {
        private readonly IResLoaderPool mPool;
        private readonly ResourcePackage mPackage;
        private AssetHandle mHandle;

        public YooAssetResLoaderUniTask(IResLoaderPool pool, ResourcePackage package)
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

        public async UniTask<T> LoadUniTaskAsync<T>(string path, CancellationToken cancellationToken = default) 
            where T : Object
        {
            mHandle = mPackage.LoadAssetAsync<T>(path);
            await mHandle.ToUniTask(cancellationToken: cancellationToken);
            var asset = mHandle.GetAssetObject<T>();
            ResLoadTracker.OnLoad(this, path, typeof(T), asset);
            return asset;
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
