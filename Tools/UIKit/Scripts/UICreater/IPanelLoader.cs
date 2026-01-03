using System;
using System.Collections.Generic;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 面板加载器接口
    /// </summary>
    public interface IPanelLoader
    {
        GameObject Load(PanelHandler handler);
        void LoadAsync(PanelHandler handler, Action<GameObject> onLoadComplete);
        void UnLoadAndRecycle();
    }

    /// <summary>
    /// 面板加载池接口
    /// </summary>
    public interface IPanelLoaderPool
    {
        IPanelLoader AllocateLoader();
        void RecycleLoader(IPanelLoader panelLoader);
    }

    /// <summary>
    /// 抽象面板加载池
    /// </summary>
    public abstract class AbstractPanelLoaderPool : IPanelLoaderPool
    {
        private readonly Stack<IPanelLoader> loaderPool = new();
        public IPanelLoader AllocateLoader() => loaderPool.Count > 0 ? loaderPool.Pop() : CreatePanelLoader();
        public void RecycleLoader(IPanelLoader panelLoader) => loaderPool.Push(panelLoader);
        protected abstract IPanelLoader CreatePanelLoader();
    }

    /// <summary>
    /// 默认面板加载池（基于 ResKit）
    /// </summary>
    public class DefaultPanelLoaderPool : AbstractPanelLoaderPool
    {
        protected override IPanelLoader CreatePanelLoader() => new DefaultPanelLoader(this);

        public class DefaultPanelLoader : IPanelLoader
        {
            private readonly IPanelLoaderPool mLoaderPool;
            private IResLoader mResLoader;

            public DefaultPanelLoader(IPanelLoaderPool pool) => mLoaderPool = pool;

            public GameObject Load(PanelHandler handler)
            {
                mResLoader = ResKit.GetLoaderPool().Allocate();
                return mResLoader.Load<GameObject>(handler.Type.Name);
            }

            public void LoadAsync(PanelHandler handler, Action<GameObject> onLoadComplete)
            {
                mResLoader = ResKit.GetLoaderPool().Allocate();
                mResLoader.LoadAsync<GameObject>(handler.Type.Name, onLoadComplete);
            }

            public void UnLoadAndRecycle()
            {
                mResLoader?.UnloadAndRecycle();
                mResLoader = null;
                mLoaderPool.RecycleLoader(this);
            }
        }
    }
}