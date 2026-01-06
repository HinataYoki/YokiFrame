#if YOKIFRAME_YOOASSET_SUPPORT && YOKIFRAME_UNITASK_SUPPORT
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using YooAsset;
using Object = UnityEngine.Object;

namespace YokiFrame
{
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
