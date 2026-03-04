using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace YokiFrame
{
    /// <summary>
    /// 默认加载池（Resources）
    /// </summary>
    public class DefaultResLoaderPool : AbstractResLoaderPool
    {
        protected override IResLoader CreateLoader() => new DefaultResLoader(this);
    }

    /// <summary>
    /// 默认加载器（Resources）
    /// </summary>
    public class DefaultResLoader : IResLoader, IAllAssetsLoader, ISubAssetsLoader
    {
        private readonly IResLoaderPool mPool;
        protected Object mAsset;

        public DefaultResLoader(IResLoaderPool pool) => mPool = pool;

        public T Load<T>(string path) where T : Object
        {
            mAsset = Resources.Load<T>(path);
            ResLoadTracker.OnLoad(this, path, typeof(T), mAsset);
            return mAsset as T;
        }

        public void LoadAsync<T>(string path, Action<T> onComplete) where T : Object
        {
            var request = Resources.LoadAsync<T>(path);
            request.completed += _ =>
            {
                mAsset = request.asset;
                ResLoadTracker.OnLoad(this, path, typeof(T), mAsset);
                onComplete?.Invoke(mAsset as T);
            };
        }

        #region IAllAssetsLoader

        /// <summary>
        /// Resources.LoadAll: 加载 Resources 文件夹中指定路径下的所有资源。
        /// 若 path 指向文件夹，加载该文件夹内所有资源；若指向文件，加载该文件及其子对象。
        /// 注意：与 YooAsset 的 LoadAllAssets（按 Bundle 加载）含义不同。
        /// </summary>
        public T[] LoadAll<T>(string path) where T : Object
            => Resources.LoadAll<T>(path);

        public void LoadAllAsync<T>(string path, Action<T[]> onComplete) where T : Object
        {
            // Resources 无原生异步 LoadAll，同步回退
            var result = Resources.LoadAll<T>(path);
            onComplete?.Invoke(result);
        }

        #endregion

        #region ISubAssetsLoader

        public SubAssetsResult<T> LoadSub<T>(string path) where T : Object
        {
            // Resources 无真正子资源概念，回退到 LoadAll
            var all = Resources.LoadAll<T>(path);
            var main = all is { Length: > 0 } ? all[0] : null;
            return new SubAssetsResult<T>(main, all);
        }

        public void LoadSubAsync<T>(string path, Action<SubAssetsResult<T>> onComplete) where T : Object
        {
            var result = LoadSub<T>(path);
            onComplete?.Invoke(result);
        }

        #endregion

        public void UnloadAndRecycle()
        {
            ResLoadTracker.OnUnload(this);
            if (mAsset != default)
            {
                // GameObject/Component 无法通过 UnloadAsset 释放，置空引用等待 UnloadUnusedAssets
                if (mAsset is not (GameObject or Component))
                {
                    Resources.UnloadAsset(mAsset);
                }
                mAsset = null;
            }
            mPool.Recycle(this);
        }
    }
}

