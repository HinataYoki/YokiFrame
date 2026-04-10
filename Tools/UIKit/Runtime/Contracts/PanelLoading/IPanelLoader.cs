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
        private readonly Stack<IPanelLoader> mLoaderPool = new();
        public IPanelLoader AllocateLoader() => mLoaderPool.Count > 0 ? mLoaderPool.Pop() : CreatePanelLoader();
        public void RecycleLoader(IPanelLoader panelLoader) => mLoaderPool.Push(panelLoader);
        protected abstract IPanelLoader CreatePanelLoader();
    }

    /// <summary>
    /// 默认面板加载池（基于 ResKit）
    /// <para>默认从 Resources/Art/UIPrefab/ 加载面板预制体</para>
    /// </summary>
    public class DefaultPanelLoaderPool : AbstractPanelLoaderPool
    {
        /// <summary>
        /// 默认路径前缀（相对于 Resources 文件夹）
        /// </summary>
        public const string DEFAULT_PATH_PREFIX = "Art/UIPrefab";
        
        /// <summary>
        /// 路径前缀，可在运行时修改
        /// </summary>
        public static string PathPrefix { get; set; } = DEFAULT_PATH_PREFIX;
        
        protected override IPanelLoader CreatePanelLoader() => new DefaultPanelLoader(this);

        public class DefaultPanelLoader : IPanelLoader
        {
            protected readonly IPanelLoaderPool mLoaderPool;
            protected IResLoader mResLoader;

            public DefaultPanelLoader(IPanelLoaderPool pool) => mLoaderPool = pool;

            public GameObject Load(PanelHandler handler)
            {
                mResLoader = ResKit.GetLoaderPool().Allocate();
#if YOKIFRAME_ZSTRING_SUPPORT
                var path = Cysharp.Text.ZString.Concat(PathPrefix, "/", handler.Type.Name);
#else
                var path = PathPrefix + "/" + handler.Type.Name;
#endif
                return mResLoader.Load<GameObject>(path);
            }

            public void LoadAsync(PanelHandler handler, Action<GameObject> onLoadComplete)
            {
                mResLoader = ResKit.GetLoaderPool().Allocate();
#if YOKIFRAME_ZSTRING_SUPPORT
                var path = Cysharp.Text.ZString.Concat(PathPrefix, "/", handler.Type.Name);
#else
                var path = PathPrefix + "/" + handler.Type.Name;
#endif
                mResLoader.LoadAsync<GameObject>(path, onLoadComplete);
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
    /// <para>默认从 Resources/Art/UIPrefab/ 加载面板预制体</para>
    /// </summary>
    public class DefaultPanelLoaderUniTaskPool : AbstractPanelLoaderPool
    {
        protected override IPanelLoader CreatePanelLoader() => new DefaultPanelLoaderUniTask(this);

        /// <summary>
        /// 默认 UniTask 面板加载器 - 继承 DefaultPanelLoader，仅扩展 UniTask 异步方法
        /// </summary>
        public class DefaultPanelLoaderUniTask : DefaultPanelLoaderPool.DefaultPanelLoader, IPanelLoaderUniTask
        {
            public DefaultPanelLoaderUniTask(IPanelLoaderPool pool) : base(pool) { }

            public async UniTask<GameObject> LoadUniTaskAsync(PanelHandler handler, CancellationToken cancellationToken = default)
            {
                mResLoader ??= ResKit.GetLoaderPool().Allocate();
#if YOKIFRAME_ZSTRING_SUPPORT
                var path = Cysharp.Text.ZString.Concat(DefaultPanelLoaderPool.PathPrefix, "/", handler.Type.Name);
#else
                var path = DefaultPanelLoaderPool.PathPrefix + "/" + handler.Type.Name;
#endif
                if (mResLoader is IResLoaderUniTask uniTaskLoader)
                    return await uniTaskLoader.LoadUniTaskAsync<GameObject>(path, cancellationToken);

                // 回退：TCS 包装回调
                var tcs = new UniTaskCompletionSource<GameObject>();
                mResLoader.LoadAsync<GameObject>(path, p => tcs.TrySetResult(p));
                return await tcs.Task.AttachExternalCancellation(cancellationToken);
            }
        }
    }
#endif
}
