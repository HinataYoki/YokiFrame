#if !GODOT
using System;
using System.Collections;
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
    /// UIRoot - 面板加载
    /// </summary>
    public partial class UIRoot
    {
        #region 面板加载器

        private IPanelLoaderPool mLoaderPool = new DefaultPanelLoaderPool();

        /// <summary>
        /// 获取当前面板加载器池。
        /// </summary>
        /// <returns>当前面板加载器池。</returns>
        public IPanelLoaderPool GetPanelLoader()
        {
            return mLoaderPool;
        }

        /// <summary>
        /// 设置面板加载器
        /// </summary>
        /// <param name="loaderPool">加载器池</param>
        public void SetPanelLoader(IPanelLoaderPool loaderPool)
        {
            mLoaderPool = loaderPool;
#if YOKIFRAME_ZSTRING_SUPPORT
            using (var sb = Cysharp.Text.ZString.CreateStringBuilder())
            {
                sb.Append("[UIRoot] 加载池已切换为: ");
                sb.Append(loaderPool.GetType().Name);
                KitLogger.Log(sb.ToString());
            }
#else
            KitLogger.Log("[UIRoot] 加载池已切换为: " + loaderPool.GetType().Name);
#endif
        }

        #endregion

        #region 面板加载

        public IPanel LoadPanel(PanelHandler handler)
        {
            var loader = mLoaderPool.AllocateLoader();
            var prefab = loader.Load(handler);

            if (prefab == default)
            {
#if YOKIFRAME_ZSTRING_SUPPORT
                using (var sb = Cysharp.Text.ZString.CreateStringBuilder())
                {
                    sb.Append("[UIRoot] 面板加载失败: ");
                    sb.Append(handler.Type.Name);
                    KitLogger.Error(sb.ToString());
                }
#else
                KitLogger.Error("[UIRoot] 面板加载失败: " + handler.Type.Name);
#endif
                return null;
            }

            bool isSceneObject = prefab.scene.IsValid();
            var panel = Instantiate(prefab).GetComponent<UIPanel>();
            if (isSceneObject) Destroy(prefab);

            SetupPanelHandler(handler, loader, prefab, panel);
            SetLevelOfPanel(handler.Level, panel);
            return panel;
        }

        public void LoadPanelAsync(PanelHandler handler, Action<IPanel> onComplete)
        {
            var loader = mLoaderPool.AllocateLoader();
            loader.LoadAsync(handler, prefab =>
            {
                if (prefab == default)
                {
#if YOKIFRAME_ZSTRING_SUPPORT
                    using (var sb = Cysharp.Text.ZString.CreateStringBuilder())
                    {
                        sb.Append("[UIRoot] 面板加载失败: ");
                        sb.Append(handler.Type.Name);
                        KitLogger.Error(sb.ToString());
                    }
#else
                    KitLogger.Error("[UIRoot] 面板加载失败: " + handler.Type.Name);
#endif
                    onComplete?.Invoke(null);
                    return;
                }

                handler.Prefab = prefab;
                handler.Loader = loader;

#if UNITY_6000_0_OR_NEWER
                StartCoroutine(InstantiatePanelAsync(handler, onComplete));
#else
                var panel = Instantiate(prefab).GetComponent<UIPanel>();
                SetupPanelHandler(handler, loader, prefab, panel);
                SetLevelOfPanel(handler.Level, panel);
                onComplete?.Invoke(panel);
#endif
            });
        }

#if UNITY_6000_0_OR_NEWER
        private IEnumerator InstantiatePanelAsync(PanelHandler handler, Action<IPanel> onComplete)
        {
            var op = InstantiateAsync(handler.Prefab);
            yield return op;

            if (op.isDone && op.Result.Length > 0)
            {
                var panel = op.Result[0].GetComponent<UIPanel>();
                handler.Panel = panel;
                panel.Handler = handler;
                SetLevelOfPanel(handler.Level, panel);
                onComplete?.Invoke(panel);
            }
        }
#endif

#if YOKIFRAME_UNITASK_SUPPORT
        public async UniTask<IPanel> LoadPanelAsync(PanelHandler handler, CancellationToken ct = default)
#else
        public async Task<IPanel> LoadPanelAsync(PanelHandler handler, CancellationToken ct = default)
#endif
        {
            var loader = mLoaderPool.AllocateLoader();
#if YOKIFRAME_UNITASK_SUPPORT
            var prefab = await loader.LoadAsync(handler, ct);
#else
            var prefab = await loader.LoadAsync(handler, ct).ConfigureAwait(false);
#endif

            if (prefab == default)
            {
#if YOKIFRAME_ZSTRING_SUPPORT
                using (var sb = Cysharp.Text.ZString.CreateStringBuilder())
                {
                    sb.Append("[UIRoot] 面板加载失败: ");
                    sb.Append(handler.Type.Name);
                    KitLogger.Error(sb.ToString());
                }
#else
                KitLogger.Error("[UIRoot] 面板加载失败: " + handler.Type.Name);
#endif
                return null;
            }

            handler.Prefab = prefab;
            handler.Loader = loader;

#if UNITY_6000_0_OR_NEWER
#if YOKIFRAME_UNITASK_SUPPORT
            var op = InstantiateAsync(prefab);
            await op.ToUniTask(cancellationToken: ct);
            if (op.isDone && op.Result.Length > 0)
            {
                var panel = op.Result[0].GetComponent<UIPanel>();
                handler.Panel = panel;
                panel.Handler = handler;
                SetLevelOfPanel(handler.Level, panel);
                return panel;
            }
            return null;
#else
            var panel = Instantiate(prefab).GetComponent<UIPanel>();
            SetupPanelHandler(handler, loader, prefab, panel);
            SetLevelOfPanel(handler.Level, panel);
            return panel;
#endif
#else
            var panel = Instantiate(prefab).GetComponent<UIPanel>();
            SetupPanelHandler(handler, loader, prefab, panel);
            SetLevelOfPanel(handler.Level, panel);
            return panel;
#endif
        }

        private static void SetupPanelHandler(PanelHandler handler, IPanelLoader loader,
            GameObject prefab, UIPanel panel)
        {
            handler.Prefab = prefab;
            handler.Loader = loader;
            handler.Panel = panel;
            panel.Handler = handler;
        }

        #endregion
    }
}
#endif
