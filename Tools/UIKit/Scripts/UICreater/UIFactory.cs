using System;
using System.Collections;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// UI工厂
    /// </summary>
    [MonoSingletonPath("UIKit/Factory")]
    public class UIFactory : MonoBehaviour, ISingleton
    {
        public static UIFactory Instance => SingletonKit<UIFactory>.Instance;
        /// <summary>
        /// 加载池
        /// </summary>
        private IPanelLoaderPool mLoaderPool = new DefaultPanelLoaderPool();

        public IPanel LoadPanel(PanelHandler handler)
        {
            if (handler == null)
            {
                KitLogger.Error("LoadPanel: handler为null");
                return null;
            }
            
            try
            {
                var loader = mLoaderPool.AllocateLoader();
                var prefab = loader.Load(handler);
                if (prefab == null)
                {
                    KitLogger.Error($"{handler.Type?.Name ?? "Unknown"}: 预制体加载失败");
                    mLoaderPool.RecycleLoader(loader);
                    return null;
                }
                
                var instance = Instantiate(prefab);
                if (!instance.TryGetComponent<UIPanel>(out var panel))
                {
                    KitLogger.Error($"{handler.Type?.Name ?? "Unknown"}: 预制体上未找到UIPanel组件");
                    Destroy(instance);
                    mLoaderPool.RecycleLoader(loader);
                    return null;
                }

                handler.Prefab = prefab;
                handler.Loader = loader;
                handler.Panel = panel;
                panel.Handler = handler;
                UIRoot.Instance.SetLevelOfPanel(handler.Level, panel);
            }
            catch (Exception e)
            {
                KitLogger.Exception(e);
                return null;
            }

            return handler.Panel;
        }

        public void LoadPanelAsync(PanelHandler handler, Action<IPanel> onPanelComplete)
        {
            if (handler == null)
            {
                KitLogger.Error("LoadPanelAsync: handler为null");
                onPanelComplete?.Invoke(null);
                return;
            }
            
            try
            {
                var loader = mLoaderPool.AllocateLoader();
                loader.LoadAsync(handler, prefab =>
                {
                    if (prefab == null)
                    {
                        KitLogger.Error($"{handler.Type?.Name ?? "Unknown"}: 预制体加载失败");
                        mLoaderPool.RecycleLoader(loader);
                        onPanelComplete?.Invoke(null);
                    }
                    else
                    {
                        handler.Prefab = prefab;
                        handler.Loader = loader;
#if UNITY_2022_3_OR_NEWER
                        StartCoroutine(LoadAsync());
                        IEnumerator LoadAsync()
                        {
                            var op = InstantiateAsync(prefab);
                            yield return op;
                            if (op.isDone && op.Result != null && op.Result.Length > 0)
                            {
                                var panel = op.Result[0].GetComponent<UIPanel>();
                                if (panel == null)
                                {
                                    KitLogger.Error($"{handler.Type?.Name ?? "Unknown"}: 预制体上未找到UIPanel组件");
                                    Destroy(op.Result[0]);
                                    mLoaderPool.RecycleLoader(loader);
                                    onPanelComplete?.Invoke(null);
                                }
                                else
                                {
                                    handler.Panel = panel;
                                    panel.Handler = handler;
                                    UIRoot.Instance.SetLevelOfPanel(handler.Level, handler.Panel);
                                    onPanelComplete?.Invoke(handler.Panel);
                                }
                            }
                            else
                            {
                                KitLogger.Error($"{handler.Type?.Name ?? "Unknown"}: 异步实例化失败");
                                mLoaderPool.RecycleLoader(loader);
                                onPanelComplete?.Invoke(null);
                            }
                        }
#else
                        var instance = Instantiate(prefab);
                        var panel = instance.GetComponent<UIPanel>();
                        if (panel == null)
                        {
                            KitLogger.Error($"{handler.Type?.Name ?? "Unknown"}: 预制体上未找到UIPanel组件");
                            UnityEngine.Object.Destroy(instance);
                            mLoaderPool.RecycleLoader(loader);
                            onPanelComplete?.Invoke(null);
                        }
                        else
                        {
                            handler.Panel = panel;
                            panel.Handler = handler;
                            UIRoot.Instance.SetLevelOfPanel(handler.Level, handler.Panel);
                            onPanelComplete?.Invoke(handler.Panel);
                        }
#endif
                    }
                });
            }
            catch (Exception e)
            {
                KitLogger.Exception(e);
                onPanelComplete?.Invoke(null);
            }
        }

        internal void SetPanelLoader(IPanelLoaderPool loaderPool)
        {
            mLoaderPool = loaderPool;
            Debug.Log($"当前UIKit加载池为: {mLoaderPool}");
        }

        void ISingleton.OnSingletonInit() { }
    }
}
