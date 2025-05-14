using System;
using System.Collections;
using UnityEngine;

namespace YokiFrame
{
    using System.Linq;

    /// <summary>
    /// UI配置
    /// </summary>
    [MonoSingletonPath("UIKit/Factory")]
    public class UIFactory : MonoBehaviour, ISingleton
    {
        public static UIFactory Instance => SingletonKit<UIFactory>.Instance;
        /// <summary>
        /// 加载池
        /// </summary>
        private ILoaderPool mLoaderPool = new DefaultPanelLoaderPool();

        public IPanel LoadPanel(PanelHandler handler)
        {
            try
            {
                var loader = mLoaderPool.AllocateLoader();
                var prefab = loader.Load(handler);
                if (prefab == null)
                {
                    LogKit.Error<UIKit>($"{handler}: 预制体加载失败");
                }
                else
                {
                    var panel = Instantiate(prefab).GetComponent<UIPanel>();

                    handler.Prefab = prefab;
                    handler.Loader = loader;
                    handler.Panel = panel;
                    panel.Handler = handler;
                }
            }
            catch (Exception e)
            {
                LogKit.Exception(e);
            }

            return handler.Panel;
        }

        public void LoadPanelAsync(PanelHandler handler, Action<IPanel> onPanelComplete)
        {
            try
            {
                var loader = mLoaderPool.AllocateLoader();
                loader.LoadAsync(handler, prefab =>
                {
                    if (prefab == null)
                    {
                        LogKit.Error<UIKit>($"{handler}: 预制体加载失败");
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
                            if (op.isDone)
                            {
                                var panel = op.Result.First().GetComponent<UIPanel>();
                                handler.Panel = panel;
                                panel.Handler = handler;
                                onPanelComplete?.Invoke(handler.Panel);
                            }
                        }
#else
                        var panel = Instantiate(prefab).GetComponent<UIPanel>();
                        handler.Panel = panel;
                        panel.Handler = handler;
                        onPanelComplete?.Invoke(handler.Panel);
#endif
                    }
                });
            }
            catch (Exception e)
            {
                LogKit.Exception(e);
            }
        }

        public void SetDefaultSizeOfPanel(IPanel panel)
        {
            var panelRectTrans = panel.Transform as RectTransform;

            panelRectTrans.offsetMin = Vector2.zero;
            panelRectTrans.offsetMax = Vector2.zero;
            panelRectTrans.anchoredPosition3D = Vector3.zero;
            panelRectTrans.anchorMin = Vector2.zero;
            panelRectTrans.anchorMax = Vector2.one;

            panelRectTrans.localScale = Vector3.one;
        }


        void ISingleton.OnSingletonInit() { }
    }
}
