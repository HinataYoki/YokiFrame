#if YOKIFRAME_YOOASSET_SUPPORT && !YOOASSET_3_0_OR_NEWER
using System;
using YooAsset;
using Object = UnityEngine.Object;

namespace YokiFrame
{
    /// <summary>
    /// YooAsset 2.x 资源加载池
    /// 2.x 使用 YooAssets 静态 API，无 ResourcePackage
    /// </summary>
    public class YooAssetResLoaderPool : AbstractResLoaderPool
    {
        public YooAssetResLoaderPool() { }
        public YooAssetResLoaderPool(string packageName) { }

        protected override IResLoader CreateLoader() => new YooAssetResLoader(this);
    }

    /// <summary>
    /// YooAsset 2.x 资源加载器
    /// </summary>
    public class YooAssetResLoader : IResLoader, IAllAssetsLoader, ISubAssetsLoader
    {
        private readonly IResLoaderPool mPool;
        protected AssetHandle mHandle;
        protected AllAssetsHandle mAllAssetsHandle;
        protected SubAssetsHandle mSubAssetsHandle;

        public YooAssetResLoader(IResLoaderPool pool)
        {
            mPool = pool;
        }

        public T Load<T>(string path) where T : Object
        {
            mHandle = YooAssets.LoadAssetSync<T>(path);
            var asset = mHandle.AssetObject as T;
            ResLoadTracker.OnLoad(this, path, typeof(T), asset);
            return asset;
        }

        public void LoadAsync<T>(string path, Action<T> onComplete) where T : Object
        {
            mHandle = YooAssets.LoadAssetAsync<T>(path);
            mHandle.Completed += handle =>
            {
                var asset = handle.AssetObject as T;
                ResLoadTracker.OnLoad(this, path, typeof(T), asset);
                onComplete?.Invoke(asset);
            };
        }

        #region IAllAssetsLoader

        public T[] LoadAll<T>(string path) where T : Object
        {
            mAllAssetsHandle = YooAssets.LoadAllAssetsSync<T>(path);
            return ConvertAllAssets<T>(mAllAssetsHandle);
        }

        public void LoadAllAsync<T>(string path, Action<T[]> onComplete) where T : Object
        {
            mAllAssetsHandle = YooAssets.LoadAllAssetsAsync<T>(path);
            mAllAssetsHandle.Completed += handle =>
            {
                var result = ConvertAllAssets<T>(handle as AllAssetsHandle);
                onComplete?.Invoke(result);
            };
        }

        private static T[] ConvertAllAssets<T>(AllAssetsHandle handle) where T : Object
        {
            if (handle == default || handle.Status != EOperationStatus.Succeed)
                return System.Array.Empty<T>();

            var objects = handle.AllAssetObjects;
            var result = new T[objects.Count];
            for (int i = 0; i < objects.Count; i++)
                result[i] = objects[i] as T;
            return result;
        }

        #endregion

        #region ISubAssetsLoader

        public SubAssetsResult<T> LoadSub<T>(string path) where T : Object
        {
            mSubAssetsHandle = YooAssets.LoadSubAssetsSync<T>(path);
            return ConvertSubAssets<T>(mSubAssetsHandle);
        }

        public void LoadSubAsync<T>(string path, Action<SubAssetsResult<T>> onComplete) where T : Object
        {
            mSubAssetsHandle = YooAssets.LoadSubAssetsAsync<T>(path);
            mSubAssetsHandle.Completed += handle =>
            {
                var result = ConvertSubAssets<T>(handle as SubAssetsHandle);
                onComplete?.Invoke(result);
            };
        }

        private static SubAssetsResult<T> ConvertSubAssets<T>(SubAssetsHandle handle) where T : Object
        {
            if (handle == default || handle.Status != EOperationStatus.Succeed)
                return default;

            var objects = handle.SubAssetObjects;
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

        #endregion

        public void UnloadAndRecycle()
        {
            ResLoadTracker.OnUnload(this);
            mHandle?.Release();
            mHandle = null;
            mAllAssetsHandle?.Release();
            mAllAssetsHandle = null;
            mSubAssetsHandle?.Release();
            mSubAssetsHandle = null;
            mPool.Recycle(this);
        }
    }
}
#endif
