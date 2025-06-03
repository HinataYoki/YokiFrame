using System;
using System.Collections.Generic;

namespace YokiFrame
{
    public class UIKit
    {
        static UIKit() => _ = UIRoot.Instance;

        // 创建界面时赋予的热度值
        public static int OpenHot = 3;
        // 获取界面时赋予的热度值
        public static int GetHot = 2;
        // 每次行为造成的衰减热度值
        public static int Weaken = 1;

        /// <summary>
        /// 已经存在的Panel缓存
        /// </summary>
        private static readonly Dictionary<Type, PanelHandler> PanelCacheDic = new();
        /// <summary>
        /// UI界面堆栈
        /// </summary>
        private static readonly PooledLinkedList<IPanel> PanelStack = new();

        /// <summary>
        /// 获取指定类型的Panel,如果不存在则返回null
        /// </summary>
        public static T GetPanel<T>() where T : UIPanel
        {
            WeakenHot();
            if (PanelCacheDic.TryGetValue(typeof(T), out var handler))
            {
                handler.Hot += GetHot;
                return handler.Panel as T;
            }
            else return null;
        }
        /// <summary>
        /// 创建指定类型的Panel
        /// </summary>
        public static T OpenPanel<T>(UILevel level = UILevel.Common, IUIData data = null) where T : UIPanel
        {
            WeakenHot();
            var type = typeof(T);
            if (!PanelCacheDic.TryGetValue(type, out var handler))
            {
                handler = PanelHandler.Allocate();
                handler.Type = type;
                handler.Level = level;
                handler.Data = data;
                CreateUI(handler);
            }
            handler.Panel.Init(data);
            handler.Panel.Open();
            handler.Panel.Show();

            return handler.Panel as T;
        }
        /// <summary>
        /// 异步创建指定类型的Panel
        /// </summary>
        public static void OpenPanelAsync<T>(Action<IPanel> callbaack = null,
            UILevel level = UILevel.Common, IUIData data = null) where T : UIPanel
        {
            var type = typeof(T);
            if (!PanelCacheDic.TryGetValue(type, out var handler))
            {
                handler = PanelHandler.Allocate();
                handler.Type = type;
                handler.Level = level;
                handler.Data = data;
                CreateUIAsync(handler, callbaack);
            }
            else
            {
                callbaack?.Invoke(handler.Panel);
            }
        }
        /// <summary>
        /// 显示一个指定类型的Panel
        /// </summary>
        public static void ShowPanel<T>() where T : UIPanel
        {
            var panel = GetPanel<T>();
            if (panel == null)
            {
                panel.Show();
            }
        }
        /// <summary>
        /// 隐藏一个指定类型的Panel
        /// </summary>
        public static void HidePanel<T>() where T : UIPanel
        {
            var panel = GetPanel<T>();
            if (panel == null)
            {
                panel.Hide();
            }
        }
        /// <summary>
        /// 隐藏所有Panel
        /// </summary>
        public static void HideAllPanel()
        {
            foreach (var handler in PanelCacheDic.Values)
            {
                handler.Panel.Hide();
            }
        }
        /// <summary>
        /// 关闭指定类型的Panel
        /// </summary>
        public static void ClosePanel<T>() where T : UIPanel
        {
            if (PanelCacheDic.TryGetValue(typeof(T), out var handler))
            {
                ClosePanel(handler.Panel);
            }
        }
        /// <summary>
        /// 关闭传入的Panel实例
        /// </summary>
        /// <param name="panel"></param>
        public static void ClosePanel(IPanel panel)
        {
            panel.Close();
            if (panel.Handler.Hot <= 0)
            {
                UnityEngine.Object.Destroy(panel.Transform.gameObject);
                panel.Handler.Recycle();
                PanelCacheDic.Remove(panel.Handler.Type);
            }
            if (panel.Handler.OnStack != null)
            {
                PanelStack.Remove(panel.Handler.OnStack);
            }
        }
        /// <summary>
        /// 关闭所有面板
        /// </summary>
        public static void CloseAllPanel()
        {
            foreach (var handler in PanelCacheDic.Values)
            {
                ClosePanel(handler.Panel);
            }
            PanelStack.Clear();
        }
        /// <summary>
        /// 压入一个Panel到栈中
        /// </summary>
        /// <param name="hidePreLevel">隐藏栈中上一层UI</param>
        public static void PushPanel<T>(bool hidePreLevel = true) where T : UIPanel
        {
            var panel = GetPanel<T>();
            if (panel != null) PushPanel(panel, hidePreLevel);
        }
        /// <summary>
        /// 压入一个Panel到栈中
        /// </summary>
        /// <param name="hidePreLevel">隐藏栈中上一层UI</param>
        public static void PushPanel(IPanel panel, bool hidePreLevel = true)
        {
            if (panel.Handler.OnStack != null)
            {
                if (hidePreLevel && PanelStack.Count > 0)
                {
                    PanelStack.Last.Value.Hide();
                }
                panel.Handler.OnStack = PanelStack.AddLast(panel);
            }
        }
        /// <summary>
        /// 打开并且压入指定类型的Panel到栈中
        /// </summary>
        public static void PushOpenPanel<T>(UILevel level = UILevel.Common, 
            IUIData data = null, bool hidePreLevel = true) where T : UIPanel
        {
            var panel = OpenPanel<T>(level, data);
            PushPanel(panel, hidePreLevel);
        }
        /// <summary>
        /// 异步打开并且压入指定类型的Panel到栈中
        /// </summary>
        public static void PushOpenPanelAysnc<T>(Action<IPanel> callbaack = null, 
            UILevel level = UILevel.Common, IUIData data = null, bool hidePreLevel = true) where T : UIPanel
        {
            callbaack += panel =>
            {
                PushPanel(panel, hidePreLevel);
            };
            OpenPanelAsync<T>(callbaack, level, data);
        }
        /// <summary>
        /// 弹出一个面板
        /// </summary>
        /// <param name="showPreLevel">自动显示上一层面板</param>
        /// <param name="autoClose">自动关闭弹出面板</param>
        /// <returns></returns>
        public static IPanel PopPanel(bool showPreLevel = true, bool autoClose = true)
        {
            if (PanelStack.Count > 0)
            {
                var panel = PanelStack.Last.Value;
                PanelStack.RemoveLast();
                panel.Handler.OnStack = null;

                if (showPreLevel && PanelStack.Count > 0)
                {
                    PanelStack.Last.Value.Show();
                }
                if (autoClose)
                {
                    panel.Close();
                }
                return panel;
            }

            return null;
        }
        /// <summary>
        /// 关闭所有栈上面板
        /// </summary>
        public static void CloseAllStackPanel()
        {
            Pool.List<IPanel>(list =>
            {
                foreach (var panel in PanelStack)
                {
                    list.Add(panel);
                }
                foreach (var panel in list)
                {
                    ClosePanel(panel);
                }
            });
        }
        /// <summary>
        /// 设置自定义的Panel加载器池
        /// </summary>
        /// <param name="loaderPool"></param>
        public static void SetPanelLoader(IPanelLoaderPool loaderPool) => UIFactory.Instance.SetPanelLoader(loaderPool);

        private static IPanel CreateUI(PanelHandler handler)
        {
            var panel = UIFactory.Instance.LoadPanel(handler);
            if (panel != null)
            {
                panel.Transform.gameObject.name = handler.Type.Name;

                PanelCacheDic.Add(handler.Type, handler);
                handler.Hot += OpenHot;
            }

            return panel;
        }

        private static void CreateUIAsync(PanelHandler handler, Action<IPanel> onPanelCreate)
        {
            UIFactory.Instance.LoadPanelAsync(handler, panel =>
            {
                if (panel != null)
                {
                    panel.Transform.gameObject.name = handler.Type.Name;

                    PanelCacheDic.Add(handler.Type, handler);
                    handler.Hot += OpenHot;

                    panel.Init(handler.Data);
                    onPanelCreate?.Invoke(panel);
                }
            });
        }
        /// <summary>
        /// 衰减UI热度
        /// </summary>
        private static void WeakenHot()
        {
            Pool.List<Type>(list =>
            {
                foreach (var handler in PanelCacheDic.Values)
                {
                    handler.Hot -= Weaken;
                    if (handler.Hot <= 0 && handler.Panel.State is PanelState.Close)
                    {
                        UnityEngine.Object.Destroy(handler.Panel.Transform.gameObject);
                        list.Add(handler.Type);
                        handler.Recycle();
                    }
                }
                foreach (var type in list)
                {
                    PanelCacheDic.Remove(type);
                }
            });
        }
    }
}