#if !GODOT
using System;
using System.Collections.Generic;
using System.Threading;
#if YOKIFRAME_UNITASK_SUPPORT
using Cysharp.Threading.Tasks;
#else
using System.Threading.Tasks;
#endif
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 面板加载器接口
    /// </summary>
    public interface IPanelLoader
    {
        /// <summary>
        /// 同步加载面板实例。
        /// </summary>
        /// <param name="handler">面板处理句柄。</param>
        /// <returns>加载得到的面板 GameObject。</returns>
        GameObject Load(PanelHandler handler);

        /// <summary>
        /// 异步加载面板实例。
        /// </summary>
        /// <param name="handler">面板处理句柄。</param>
        /// <param name="onLoadComplete">加载完成回调。</param>
        void LoadAsync(PanelHandler handler, Action<GameObject> onLoadComplete);

#if YOKIFRAME_UNITASK_SUPPORT
        /// <summary>
        /// 异步加载面板实例。安装 UniTask 后返回 UniTask，否则返回 Task。
        /// </summary>
        UniTask<GameObject> LoadAsync(PanelHandler handler, CancellationToken cancellationToken = default);
#else
        /// <summary>
        /// 异步加载面板实例。安装 UniTask 后返回 UniTask，否则返回 Task。
        /// </summary>
        Task<GameObject> LoadAsync(PanelHandler handler, CancellationToken cancellationToken = default);
#endif

        /// <summary>
        /// 卸载当前面板资源并回收到加载池。
        /// </summary>
        void UnLoadAndRecycle();
    }

    /// <summary>
    /// 面板加载池接口
    /// </summary>
    public interface IPanelLoaderPool
    {
        /// <summary>
        /// 是否使用可寻址 location 加载面板。启用后默认加载器直接使用面板类型名。
        /// </summary>
        bool UseAddressableLocation { get; set; }

        /// <summary>
        /// 分配一个面板加载器。
        /// </summary>
        /// <returns>面板加载器实例。</returns>
        IPanelLoader AllocateLoader();

        /// <summary>
        /// 回收面板加载器。
        /// </summary>
        /// <param name="panelLoader">需要回收的加载器。</param>
        void RecycleLoader(IPanelLoader panelLoader);
    }

    /// <summary>
    /// 抽象面板加载池
    /// </summary>
    public abstract class AbstractPanelLoaderPool : IPanelLoaderPool
    {
        private readonly Stack<IPanelLoader> mLoaderPool = new();

        /// <inheritdoc />
        public virtual bool UseAddressableLocation { get; set; }

        /// <inheritdoc />
        public IPanelLoader AllocateLoader() => mLoaderPool.Count > 0 ? mLoaderPool.Pop() : CreatePanelLoader();

        /// <inheritdoc />
        public void RecycleLoader(IPanelLoader panelLoader) => mLoaderPool.Push(panelLoader);

        /// <summary>
        /// 创建新的面板加载器实例。
        /// </summary>
        /// <returns>面板加载器实例。</returns>
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

        /// <summary>
        /// 默认加载池创建时是否使用可寻址 location。启用后默认加载器直接使用面板类型名，例如 LoginPanel。
        /// </summary>
        public static bool DefaultUseAddressableLocation { get; set; }

        /// <summary>
        /// 创建默认面板加载池。
        /// </summary>
        public DefaultPanelLoaderPool()
        {
            UseAddressableLocation = DefaultUseAddressableLocation;
        }
        
        /// <inheritdoc />
        protected override IPanelLoader CreatePanelLoader() => new DefaultPanelLoader(this);

        /// <summary>
        /// 默认面板加载器。
        /// </summary>
        public class DefaultPanelLoader : IPanelLoader
        {
            protected readonly IPanelLoaderPool mLoaderPool;
            protected ResHandle<GameObject> mHandle;

            /// <summary>
            /// 创建默认面板加载器。
            /// </summary>
            /// <param name="pool">所属加载器池。</param>
            public DefaultPanelLoader(IPanelLoaderPool pool) => mLoaderPool = pool;

            /// <inheritdoc />
            public GameObject Load(PanelHandler handler)
            {
                var path = BuildPath(handler);
                mHandle = ResKit.LoadAsset<GameObject>(path);
                return mHandle != null ? mHandle.Asset : null;
            }

            /// <inheritdoc />
            public async void LoadAsync(PanelHandler handler, Action<GameObject> onLoadComplete)
            {
                try
                {
#if YOKIFRAME_UNITASK_SUPPORT
                    var prefab = await LoadAsync(handler);
#else
                    var prefab = await LoadAsync(handler).ConfigureAwait(false);
#endif
                    if (onLoadComplete != null)
                        onLoadComplete(prefab);
                }
                catch (Exception exception)
                {
                    LogKit.Exception(exception);
                    if (onLoadComplete != null)
                        onLoadComplete(null);
                }
            }

#if YOKIFRAME_UNITASK_SUPPORT
            /// <inheritdoc />
            public async UniTask<GameObject> LoadAsync(PanelHandler handler, CancellationToken cancellationToken = default)
#else
            /// <inheritdoc />
            public async Task<GameObject> LoadAsync(PanelHandler handler, CancellationToken cancellationToken = default)
#endif
            {
                var path = BuildPath(handler);
#if YOKIFRAME_UNITASK_SUPPORT
                mHandle = await ResKit.LoadAssetAsync<GameObject>(path, cancellationToken);
#else
                mHandle = await ResKit.LoadAssetAsync<GameObject>(path, cancellationToken).ConfigureAwait(false);
#endif
                return mHandle != null ? mHandle.Asset : null;
            }

            /// <inheritdoc />
            public void UnLoadAndRecycle()
            {
                if (mHandle != null)
                    mHandle.Release();
                mHandle = null;
                mLoaderPool.RecycleLoader(this);
            }

            private string BuildPath(PanelHandler handler)
            {
                if (mLoaderPool.UseAddressableLocation)
                    return handler.Type.Name;

#if YOKIFRAME_ZSTRING_SUPPORT
                return Cysharp.Text.ZString.Concat(PathPrefix, "/", handler.Type.Name);
#else
                return PathPrefix + "/" + handler.Type.Name;
#endif
            }
        }
    }
}
#endif
