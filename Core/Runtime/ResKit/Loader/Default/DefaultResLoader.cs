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
    public class DefaultResLoader : IResLoader
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
