using System;
using System.Collections.Generic;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 加载器
    /// </summary>
    public interface IPanelLoader
    {
        GameObject Load(PanelHandler handler);
        void LoadAsync(PanelHandler handler, Action<GameObject> onLoadComplete);
        void UnLoadAndRecycle();
    }

    /// <summary>
    /// 加载池
    /// </summary>
    public interface ILoaderPool
    {
        IPanelLoader AllocateLoader();
        void RecycleLoader(IPanelLoader panelLoader);
    }

    /// <summary>
    /// 抽象加载池
    /// </summary>
    public abstract class AbsPanelLoderPool : ILoaderPool
    {
        private readonly Stack<IPanelLoader> loaderPool = new();
        public IPanelLoader AllocateLoader() => loaderPool.Count > 0 ? loaderPool.Pop() : CreatePanelLoader();
        public void RecycleLoader(IPanelLoader panelLoader) => loaderPool.Push(panelLoader);
        protected abstract IPanelLoader CreatePanelLoader();
    }

    /// <summary>
    /// 默认加载池
    /// </summary>
    public class DefaultPanelLoaderPool : AbsPanelLoderPool
    {
        protected override IPanelLoader CreatePanelLoader() => new DefaultPanelLoader(this);

        public class DefaultPanelLoader : IPanelLoader
        {
            private ILoaderPool mLoaderPool;
            private GameObject mPanelPrefab;

            public DefaultPanelLoader(ILoaderPool pool) => mLoaderPool = pool;

            public GameObject Load(PanelHandler handler)
            {
                mPanelPrefab = Resources.Load<GameObject>(handler.Type.Name);
                return mPanelPrefab;
            }

            public void LoadAsync(PanelHandler handler, Action<GameObject> onLoadComplete)
            {
                var request = Resources.LoadAsync<GameObject>(handler.Type.Name);
                request.completed += operation =>
                {
                    mPanelPrefab = request.asset as GameObject;
                    onLoadComplete?.Invoke(mPanelPrefab);
                };
            }

            public void UnLoadAndRecycle()
            {
                if (mPanelPrefab != null)
                {
                    Resources.UnloadAsset(mPanelPrefab);
                    mPanelPrefab = null;
                }
                mLoaderPool.RecycleLoader(this);
            }
        }
    }
}