using System;
using System.Collections.Generic;

namespace YokiFrame
{
    public class UIKit
    {
        static UIKit() => _ = UIRoot.Instance;

        /// <summary>
        /// 创建界面时赋予的热度值
        /// </summary>
        public static int OpenHot = 3;
        /// <summary>
        /// 获取界面时赋予的热度值
        /// </summary>
        public static int GetHot = 2;
        /// <summary>
        /// 每次行为造成的衰减热度值
        /// </summary>
        public static int Weaken = 1;

        /// <summary>
        /// 已经存在的Panel缓存
        /// </summary>
        private static readonly Dictionary<Type, PanelHandler> PanelCacheDic = new();
        /// <summary>
        /// UI界面堆栈
        /// </summary>
        private static readonly PooledLinkedList<IPanel> PanelStack = new();

        #region Handler
        
        /// <summary>
        /// 尝试获取已缓存的 Handler（不创建）
        /// </summary>
        private static bool TryGetHandler(Type type, out PanelHandler handler)
        {
            return PanelCacheDic.TryGetValue(type, out handler);
        }
        
        /// <summary>
        /// 获取或创建 Handler（同步，会触发 CreateUI）
        /// </summary>
        private static PanelHandler GetOrCreateHandler(Type type, UILevel level, IUIData data, out bool isNewCreated)
        {
            if (TryGetHandler(type, out var handler))
            {
                isNewCreated = false;
                handler.Data = data;
                return handler;
            }
            
            isNewCreated = true;
            handler = PanelHandler.Allocate();
            handler.Type = type;
            handler.Level = level;
            handler.Data = data;
            CreateUI(handler);
            return handler;
        }
        
        /// <summary>
        /// 获取或创建 Handler（异步，会触发 CreateUIAsync）
        /// </summary>
        private static void GetOrCreateHandlerAsync(Type type, UILevel level, IUIData data, Action<PanelHandler, bool> onComplete)
        {
            if (TryGetHandler(type, out var handler))
            {
                handler.Data = data;
                onComplete?.Invoke(handler, false);
                return;
            }
            
            handler = PanelHandler.Allocate();
            handler.Type = type;
            handler.Level = level;
            handler.Data = data;
            CreateUIAsync(handler, panel =>
            {
                onComplete?.Invoke(panel != null ? handler : null, true);
            });
        }
        
        #endregion

        /// <summary>
        /// 获取指定类型的Panel,如果不存在则返回null
        /// </summary>
        public static T GetPanel<T>() where T : UIPanel
        {
            WeakenHot();
            if (TryGetHandler(typeof(T), out var handler))
            {
                handler.Hot += GetHot;
                return handler.Panel as T;
            }
            return null;
        }
        
        /// <summary>
        /// 创建指定类型的Panel
        /// </summary>
        public static T OpenPanel<T>(UILevel level = UILevel.Common, IUIData data = null) where T : UIPanel
        {
            WeakenHot();
            var handler = GetOrCreateHandler(typeof(T), level, data, out _);
            
            if (handler?.Panel == null)
            {
                KitLogger.Error($"OpenPanel失败: {typeof(T).Name} 创建失败");
                return null;
            }
            
            OpenAndShowPanel(handler.Panel, data);
            return handler.Panel as T;
        }
        
        /// <summary>
        /// 异步创建指定类型的Panel
        /// </summary>
        public static void OpenPanelAsync<T>(Action<IPanel> callback = null,
            UILevel level = UILevel.Common, IUIData data = null) where T : UIPanel
        {
            var type = typeof(T);
            GetOrCreateHandlerAsync(type, level, data, (handler, isNew) =>
            {
                if (handler?.Panel != null)
                {
                    if (!isNew)
                    {
                        // 已存在的面板，直接打开显示
                        OpenAndShowPanel(handler.Panel, data);
                    }
                    // 新创建的面板在 CreateUIAsync 回调中已经调用了 OpenAndShowPanel
                    callback?.Invoke(handler.Panel);
                }
                else
                {
                    KitLogger.Error($"OpenPanelAsync: {type.Name} 创建失败");
                    callback?.Invoke(null);
                }
            });
        }
        
        /// <summary>
        /// 显示一个指定类型的Panel
        /// </summary>
        public static void ShowPanel<T>() where T : UIPanel
        {
            var panel = GetPanel<T>();
            panel?.Show();
        }
        
        /// <summary>
        /// 隐藏一个指定类型的Panel
        /// </summary>
        public static void HidePanel<T>() where T : UIPanel
        {
            var panel = GetPanel<T>();
            panel?.Hide();
        }
        
        /// <summary>
        /// 隐藏所有Panel
        /// </summary>
        public static void HideAllPanel()
        {
            foreach (var handler in PanelCacheDic.Values)
            {
                handler?.Panel?.Hide();
            }
        }
        
        /// <summary>
        /// 关闭指定类型的Panel
        /// </summary>
        public static void ClosePanel<T>() where T : UIPanel
        {
            if (TryGetHandler(typeof(T), out var handler))
            {
                ClosePanel(handler.Panel);
            }
        }
        
        /// <summary>
        /// 关闭传入的Panel实例
        /// </summary>
        public static void ClosePanel(IPanel panel)
        {
            if (panel == null) return;
            
            panel.Close();
            
            if (panel.Handler == null) return;
            
            if (panel.Handler.OnStack != null)
            {
                PanelStack.Remove(panel.Handler.OnStack);
                panel.Handler.OnStack = null;
            }
            
            if (panel.Handler.Hot <= 0)
            {
                DestroyPanel(panel);
                PanelCacheDic.Remove(panel.Handler.Type);
                panel.Handler.Recycle();
            }
        }
        
        /// <summary>
        /// 关闭所有面板
        /// </summary>
        public static void CloseAllPanel()
        {
            Pool.List<IPanel>(panelsToClose =>
            {
                foreach (var handler in PanelCacheDic.Values)
                {
                    if (handler?.Panel != null)
                    {
                        panelsToClose.Add(handler.Panel);
                    }
                }
                for (int i = 0; i < panelsToClose.Count; i++)
                {
                    ClosePanel(panelsToClose[i]);
                }
            });
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
            if (panel?.Handler == null) return;
            
            if (panel.Handler.OnStack != null)
            {
                PanelStack.Remove(panel.Handler.OnStack);
            }
            
            if (hidePreLevel && PanelStack.Count > 0)
            {
                PanelStack.Last.Value.Hide();
            }
            panel.Handler.OnStack = PanelStack.AddLast(panel);
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
        public static void PushOpenPanelAsync<T>(Action<IPanel> callback = null, 
            UILevel level = UILevel.Common, IUIData data = null, bool hidePreLevel = true) where T : UIPanel
        {
            OpenPanelAsync<T>(panel =>
            {
                PushPanel(panel, hidePreLevel);
                callback?.Invoke(panel);
            }, level, data);
        }
        
        /// <summary>
        /// 弹出一个面板
        /// </summary>
        /// <param name="showPreLevel">自动显示上一层面板</param>
        /// <param name="autoClose">自动关闭弹出面板</param>
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
            while (PanelStack.Count > 0)
            {
                var panel = PanelStack.Last.Value;
                ClosePanel(panel);
            }
        }
        
        /// <summary>
        /// 设置自定义的Panel加载器池
        /// </summary>
        public static void SetPanelLoader(IPanelLoaderPool loaderPool) => UIFactory.Instance.SetPanelLoader(loaderPool);

        #region 内部方法
        
        private static IPanel CreateUI(PanelHandler handler)
        {
            if (handler == null) return null;
            
            var panel = UIFactory.Instance.LoadPanel(handler);
            if (panel != null && panel.Transform != null)
            {
                SetupPanel(handler, panel);
            }
            else
            {
                handler.Recycle();
            }

            return panel;
        }

        private static void CreateUIAsync(PanelHandler handler, Action<IPanel> onPanelCreate)
        {
            if (handler == null)
            {
                onPanelCreate?.Invoke(null);
                return;
            }
            
            UIFactory.Instance.LoadPanelAsync(handler, panel =>
            {
                if (panel != null && panel.Transform != null)
                {
                    SetupPanel(handler, panel);
                    OpenAndShowPanel(panel, handler.Data);
                    onPanelCreate?.Invoke(panel);
                }
                else
                {
                    handler.Recycle();
                    onPanelCreate?.Invoke(null);
                }
            });
        }
        
        /// <summary>
        /// 打开并显示Panel
        /// </summary>
        private static void OpenAndShowPanel(IPanel panel, IUIData data = null)
        {
            if (panel == null) return;
            panel.Open(data);
            panel.Show();
        }
        
        /// <summary>
        /// 设置Panel的基本属性
        /// </summary>
        private static void SetupPanel(PanelHandler handler, IPanel panel)
        {
            panel.Transform.gameObject.name = handler.Type.Name;
            PanelCacheDic.TryAdd(handler.Type, handler);
            handler.Hot += OpenHot;
            panel.Init(handler.Data);
        }
        
        /// <summary>
        /// 安全销毁Panel的GameObject
        /// </summary>
        private static void DestroyPanel(IPanel panel)
        {
            if (panel != null && panel.Transform != null && panel.Transform.gameObject != null)
            {
                UnityEngine.Object.Destroy(panel.Transform.gameObject);
            }
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
                    if (handler == null) continue;
                    
                    handler.Hot -= Weaken;
                    if (handler.Hot <= 0 && handler.Panel != null && handler.Panel.State is PanelState.Close)
                    {
                        DestroyPanel(handler.Panel);
                        list.Add(handler.Type);
                        handler.Recycle();
                    }
                }
                for (int i = 0; i < list.Count; i++)
                {
                    PanelCacheDic.Remove(list[i]);
                }
            });
        }
        
        #endregion
    }
}