using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace YokiFrame
{
    /// <summary>
    /// 资源加载器接口
    /// </summary>
    public interface IResLoader
    {
        /// <summary>
        /// 同步加载资源
        /// </summary>
        T Load<T>(string path) where T : Object;

        /// <summary>
        /// 异步加载资源
        /// </summary>
        void LoadAsync<T>(string path, Action<T> onComplete) where T : Object;

        /// <summary>
        /// 卸载并回收加载器
        /// </summary>
        void UnloadAndRecycle();
    }

    /// <summary>
    /// 资源加载池接口
    /// </summary>
    public interface IResLoaderPool
    {
        IResLoader Allocate();
        void Recycle(IResLoader loader);
    }

    /// <summary>
    /// 抽象加载池基类
    /// </summary>
    public abstract class AbstractResLoaderPool : IResLoaderPool
    {
        private readonly Stack<IResLoader> mPool = new();

        public IResLoader Allocate() => mPool.Count > 0 ? mPool.Pop() : CreateLoader();
        public void Recycle(IResLoader loader) => mPool.Push(loader);

        protected abstract IResLoader CreateLoader();
    }

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
        private Object mAsset;

        public DefaultResLoader(IResLoaderPool pool) => mPool = pool;

        public T Load<T>(string path) where T : Object
        {
            mAsset = Resources.Load<T>(path);
            return mAsset as T;
        }

        public void LoadAsync<T>(string path, Action<T> onComplete) where T : Object
        {
            var request = Resources.LoadAsync<T>(path);
            request.completed += _ =>
            {
                mAsset = request.asset;
                onComplete?.Invoke(mAsset as T);
            };
        }

        public void UnloadAndRecycle()
        {
            if (mAsset != null)
            {
                Resources.UnloadAsset(mAsset);
                mAsset = null;
            }
            mPool.Recycle(this);
        }
    }
}
