#if YOKIFRAME_YOOASSET_SUPPORT
using System;
using Object = UnityEngine.Object;

namespace YokiFrame
{
    /// <summary>
    /// YooAsset 资源加载器。
    /// 版本无关 — V2/V3 差异通过 IYooAssetResProvider 隔离。
    /// 本文件零内部 #if。
    /// </summary>
    public class YooAssetResLoader : IResLoader, IAllAssetsLoader, ISubAssetsLoader
    {
        private readonly IResLoaderPool mPool;
        internal readonly IYooAssetResProvider Provider;

        internal YooAssetResLoader(IResLoaderPool pool, IYooAssetResProvider provider)
        {
            mPool = pool;
            Provider = provider;
        }

        public T Load<T>(string path) where T : Object
        {
            var asset = Provider.LoadAsset<T>(path);
            ResLoadTracker.OnLoad(this, path, typeof(T), asset);
            return asset;
        }

        public void LoadAsync<T>(string path, Action<T> onComplete) where T : Object
        {
            Provider.LoadAssetAsync<T>(path, asset =>
            {
                ResLoadTracker.OnLoad(this, path, typeof(T), asset);
                onComplete?.Invoke(asset);
            });
        }

        public T[] LoadAll<T>(string path) where T : Object
        {
            var result = Provider.LoadAllAssets<T>(path);
            return result;
        }

        public void LoadAllAsync<T>(string path, Action<T[]> onComplete) where T : Object
        {
            Provider.LoadAllAssetsAsync<T>(path, result => onComplete?.Invoke(result));
        }

        public SubAssetsResult<T> LoadSub<T>(string path) where T : Object
        {
            return Provider.LoadSubAssets<T>(path);
        }

        public void LoadSubAsync<T>(string path, Action<SubAssetsResult<T>> onComplete) where T : Object
        {
            Provider.LoadSubAssetsAsync<T>(path, result => onComplete?.Invoke(result));
        }

        public void UnloadAndRecycle()
        {
            ResLoadTracker.OnUnload(this);
            Provider.ReleaseHandles();
            mPool.Recycle(this);
        }
    }
}
#endif
