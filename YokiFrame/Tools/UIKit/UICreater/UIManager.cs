using System;
using System.Collections.Generic;
using UnityEngine;

namespace YokiFrame
{
    [MonoSingletonPath("UIKit/Manager")]
    public class UIManager : MonoBehaviour, ISingleton
    {
        private static UIManager instance;
        public static UIManager Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = SingletonKit<UIManager>.Instance;
                }
                return instance;
            }
        }

        /// <summary>
        /// 已经存在的Panel缓存
        /// </summary>
        private readonly Dictionary<Type, IPanel> PanelCacheDic = new();
        /// <summary>
        /// UI界面堆栈
        /// </summary>
        private readonly LinkedList<IPanel> PanelStack = new();

        public IPanel GetUI(PanelHandler handler)
        {
            PanelCacheDic.TryGetValue(handler.Type, out var panel);

            handler.Recycle();

            return panel;
        }

        public IPanel OpenUI(PanelHandler handler)
        {
            if (PanelCacheDic.TryGetValue(handler.Type,out var panel))
            {
                handler.Recycle();
                panel.Open();
                panel.Show();
            }
            else
            {
                panel = CreateUI(handler);
                if (panel != null)
                {
                    panel.Open();
                    panel.Show();
                }
            }
            UIRoot.Instance.SetLevelOfPanel(handler.Level, panel);
            return panel;
        }

        public void OpenUIAsync(PanelHandler handler, Action<IPanel> callback)
        {
            if (PanelCacheDic.TryGetValue(handler.Type,out var panel))
            {
                handler.Recycle();
                panel.Open();
                panel.Show();

                UIRoot.Instance.SetLevelOfPanel(handler.Level, panel);
                callback?.Invoke(panel);
            }
            else
            {
                CreateUIAsync(handler, createPanel =>
                {
                    createPanel.Open();
                    createPanel.Show();

                    UIRoot.Instance.SetLevelOfPanel(handler.Level, createPanel);
                    callback?.Invoke(createPanel);
                });
            }
        }

        public void ShowUI(PanelHandler handler)
        {
            if (PanelCacheDic.TryGetValue(handler.Type, out var panel))
            {
                panel.Show();
            }

            handler.Recycle();
        }

        public void HideUI(PanelHandler handler)
        {
            if (PanelCacheDic.TryGetValue(handler.Type, out var panel))
            {
                panel.Hide();
            }

            handler.Recycle();
        }

        public void HideAllUI()
        {
            foreach (var panel in PanelCacheDic.Values)
            {
                panel.Hide();
            }
        }

        /// <summary>
        /// 关闭并且卸载UI
        /// </summary>
        public void CloseUI(PanelHandler handler)
        {
            if (PanelCacheDic.TryGetValue(handler.Type,out var panel))
            {
                panel.Close();
                PanelCacheDic.Remove(handler.Type);
                if (handler.OnStack != null)
                {
                    PanelStack.Remove(handler.OnStack);
                }
            }

            handler.Recycle();
        }

        public void CloseAllUI()
        {
            foreach (var panel in PanelCacheDic.Values)
            {
                panel.Close();
            }
            PanelStack.Clear();
        }

        private IPanel CreateUI(PanelHandler handler)
        {
            var panel = UIFactory.Instance.LoadPanel(handler);
            if (panel != null)
            {
                UIFactory.Instance.SetDefaultSizeOfPanel(panel);
                panel.Transform.gameObject.name = handler.Type.Name;

                PanelCacheDic.Add(handler.Type, panel);

                panel.Init(handler.Data);
            }

            return panel;
        }

        private void CreateUIAsync(PanelHandler handler, Action<IPanel> onPanelCreate)
        {
            UIFactory.Instance.LoadPanelAsync(handler, panel =>
            {
                if (panel != null)
                {
                    UIFactory.Instance.SetDefaultSizeOfPanel(panel);
                    panel.Transform.gameObject.name = handler.Type.Name;

                    PanelCacheDic.Add(handler.Type, panel);

                    panel.Init(handler.Data);
                    onPanelCreate?.Invoke(panel);
                }
            });
        }

        public void PushOpenUI(PanelHandler handler, bool HidePreLevel = true)
        {
            var panel = OpenUI(handler);
            PushUI(panel, HidePreLevel);
        }

        public void PushOpenUIAsync(PanelHandler handler, bool HidePreLevel = true)
        {
            OpenUIAsync(handler, panel =>
            {
                PushUI(panel, HidePreLevel);
            });
        }

        public void PushUI(PanelHandler handler, bool HidePreLevel = true)
        {
            var panel = GetUI(handler);
            if (panel != null) PushUI(panel, HidePreLevel);
        }

        public void PushUI(IPanel panel, bool HidePreLevel = true)
        {
            if (panel.Handler.OnStack != null)
            {
                if (HidePreLevel && PanelStack.Count > 0)
                {
                    PanelStack.Last.Value.Hide();
                }
                panel.Handler.OnStack = PanelStack.AddLast(panel);
            }
        }

        public IPanel PopUI(bool ShowPreLevel = true)
        {
            if (PanelStack.Count > 0)
            {
                var panel = PanelStack.Last.Value;
                PanelStack.RemoveLast();
                panel.Handler.OnStack = null;

                if (ShowPreLevel && PanelStack.Count > 0)
                {
                    PanelStack.Last.Value.Show();
                }

                return panel;
            }

            return null;
        }

        public void CloseAllStackUI()
        {
            var panelList = UnityEngine.Pool.ListPool<IPanel>.Get();
            foreach (var panel in PanelStack)
            {
                panelList.Add(panel);
            }
            foreach(var panel in panelList)
            {
                var hander = PanelHandler.Allocate();
                hander.Type = panel.Handler.Type;
                CloseUI(hander);
            }
            UnityEngine.Pool.ListPool<IPanel>.Release(panelList);
        }

        void ISingleton.OnSingletonInit() { }
    }
}