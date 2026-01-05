using System;
using System.Collections.Generic;
using System.Threading;
#if YOKIFRAME_UNITASK_SUPPORT
using Cysharp.Threading.Tasks;
#endif
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

#if YOKIFRAME_UNITASK_SUPPORT
    /// <summary>
    /// 支持 UniTask 的面板加载器接口
    /// </summary>
    public interface IPanelLoaderUniTask : IPanelLoader
    {
        UniTask<GameObject> LoadUniTaskAsync(PanelHandler handler, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// 默认 UniTask 面板加载池（基于 ResKit）
    /// </summary>
    public class DefaultPanelLoaderUniTaskPool : AbstractPanelLoaderPool
    {
        protected override IPanelLoader CreatePanelLoader() => new DefaultPanelLoaderUniTask(this);

        public class DefaultPanelLoaderUniTask : IPanelLoaderUniTask
        {
            private readonly IPanelLoaderPool mLoaderPool;
            private IResLoader mResLoader;

            public DefaultPanelLoaderUniTask(IPanelLoaderPool pool) => mLoaderPool = pool;

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

            public async UniTask<GameObject> LoadUniTaskAsync(PanelHandler handler, CancellationToken cancellationToken = default)
            {
                return await ResKit.LoadUniTaskAsync<GameObject>(handler.Type.Name, cancellationToken);
            }

            public void UnLoadAndRecycle()
            {
                mResLoader?.UnloadAndRecycle();
                mResLoader = null;
                mLoaderPool.RecycleLoader(this);
            }
        }
    }
#endif
}