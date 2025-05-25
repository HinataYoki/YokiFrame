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
                throw;
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
                            if (op.isDone && op.Result.Length > 0)
                            {
                                var panel = op.Result[0].GetComponent<UIPanel>();
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
                throw;
            }
        }

        public void SetDefaultSizeOfPanel(IPanel panel)
        {
            var rect = panel.Transform as RectTransform;

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.anchoredPosition3D = Vector3.zero;
            rect.localEulerAngles = Vector3.zero;
            rect.localScale = Vector3.one;

            rect.sizeDelta = Vector2.zero;
        }


        void ISingleton.OnSingletonInit() { }
    }
}
