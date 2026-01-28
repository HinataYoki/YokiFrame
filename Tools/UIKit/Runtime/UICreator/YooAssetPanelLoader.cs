#if YOKIFRAME_YOOASSET_SUPPORT
using System;
using System.Threading;
#if YOKIFRAME_UNITASK_SUPPORT
using Cysharp.Threading.Tasks;
#endif
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// YooAsset 面板加载池
    /// </summary>
    public class YooAssetPanelLoaderPool : AbstractPanelLoaderPool
    {
        protected override IPanelLoader CreatePanelLoader() => new YooAssetPanelLoader(this);
    }

    /// <summary>
    /// YooAsset 面板加载器
    /// </summary>
    public class YooAssetPanelLoader :
#if YOKIFRAME_UNITASK_SUPPORT
        IPanelLoaderUniTask
#else
        IPanelLoader
#endif
    {
        private readonly IPanelLoaderPool mLoaderPool;
        private ResHandler mHandler;

        public YooAssetPanelLoader(YooAssetPanelLoaderPool pool) => mLoaderPool = pool;

        public GameObject Load(PanelHandler handler)
        {
            mHandler = ResKit.LoadAsset<GameObject>(handler.Type.Name);
            return mHandler != default ? mHandler.Asset as GameObject : null;
        }

        public void LoadAsync(PanelHandler handler, Action<GameObject> onLoadComplete)
        {
            ResKit.LoadAssetAsync<GameObject>(handler.Type.Name, h =>
            {
                mHandler = h;
                if (onLoadComplete != default)
                {
                    onLoadComplete(h != default ? h.Asset as GameObject : null);
                }
            });
        }

#if YOKIFRAME_UNITASK_SUPPORT
        public async UniTask<GameObject> LoadUniTaskAsync(PanelHandler handler, CancellationToken cancellationToken = default)
        {
            var prefab = await ResKit.LoadUniTaskAsync<GameObject>(handler.Type.Name, cancellationToken);
            return prefab;
        }
#endif

        public void UnLoadAndRecycle()
        {
            if (mHandler != default) mHandler.Release();
            mHandler = null;
            mLoaderPool.RecycleLoader(this);
        }
    }
}
#endif
