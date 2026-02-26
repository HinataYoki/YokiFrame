#if YOKIFRAME_UNITASK_SUPPORT
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;

namespace YokiFrame
{
    /// <summary>
    /// 默认 UniTask 加载池
    /// </summary>
    public class DefaultResLoaderUniTaskPool : AbstractResLoaderPool
    {
        protected override IResLoader CreateLoader() => new DefaultResLoaderUniTask(this);
    }

    /// <summary>
    /// 默认 UniTask 加载器（Resources）
    /// </summary>
    public class DefaultResLoaderUniTask : IResLoaderUniTask
    {
        private readonly IResLoaderPool mPool;
        private Object mAsset;

        public DefaultResLoaderUniTask(IResLoaderPool pool) => mPool = pool;

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

        public async UniTask<T> LoadUniTaskAsync<T>(string path, CancellationToken cancellationToken = default) where T : Object
        {
            var request = Resources.LoadAsync<T>(path);
            await request.ToUniTask(cancellationToken: cancellationToken);
            mAsset = request.asset;
            ResLoadTracker.OnLoad(this, path, typeof(T), mAsset);
            return mAsset as T;
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
#endif
