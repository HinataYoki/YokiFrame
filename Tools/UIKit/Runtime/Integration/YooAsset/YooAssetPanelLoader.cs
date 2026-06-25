#if !GODOT
#if YOKIFRAME_YOOASSET_SUPPORT
using System;
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
    /// YooAsset 面板加载池。
    /// </summary>
    public class YooAssetPanelLoaderPool : AbstractPanelLoaderPool
    {
        protected override IPanelLoader CreatePanelLoader()
        {
            return new YooAssetPanelLoader(this);
        }
    }

    /// <summary>
    /// YooAsset 面板加载器。底层复用 2.0 ResKit provider，路径保持 1.0 的面板名约定。
    /// </summary>
    public class YooAssetPanelLoader : IPanelLoader
    {
        private readonly IPanelLoaderPool mLoaderPool;
        private ResHandle<GameObject> mHandle;

        public YooAssetPanelLoader(YooAssetPanelLoaderPool pool)
        {
            mLoaderPool = pool;
        }

        public GameObject Load(PanelHandler handler)
        {
            mHandle = ResKit.LoadAsset<GameObject>(BuildPath(handler));
            return mHandle != default ? mHandle.Asset : null;
        }

        public async void LoadAsync(PanelHandler handler, Action<GameObject> onLoadComplete)
        {
            try
            {
#if YOKIFRAME_UNITASK_SUPPORT
                var prefab = await LoadAsync(handler);
#else
                var prefab = await LoadAsync(handler).ConfigureAwait(false);
#endif
                if (onLoadComplete != default)
                    onLoadComplete(prefab);
            }
            catch (Exception exception)
            {
                LogKit.Exception(exception);
                if (onLoadComplete != default)
                    onLoadComplete(null);
            }
        }

#if YOKIFRAME_UNITASK_SUPPORT
        public async UniTask<GameObject> LoadAsync(
            PanelHandler handler,
            CancellationToken cancellationToken = default)
#else
        public async Task<GameObject> LoadAsync(
            PanelHandler handler,
            CancellationToken cancellationToken = default)
#endif
        {
#if YOKIFRAME_UNITASK_SUPPORT
            mHandle = await ResKit.LoadAssetAsync<GameObject>(BuildPath(handler), cancellationToken);
#else
            mHandle = await ResKit.LoadAssetAsync<GameObject>(BuildPath(handler), cancellationToken).ConfigureAwait(false);
#endif
            return mHandle != default ? mHandle.Asset : null;
        }

        public void UnLoadAndRecycle()
        {
            if (mHandle != default)
            {
                mHandle.Release();
                mHandle = null;
            }

            mLoaderPool.RecycleLoader(this);
        }

        private static string BuildPath(PanelHandler handler)
        {
            return handler != default && handler.Type != default ? handler.Type.Name : string.Empty;
        }
    }
}
#endif
#endif
