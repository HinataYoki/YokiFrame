using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// UI 热度配置常量
    /// </summary>
    public static class UIKitConfig
    {
        /// <summary>
        /// 创建界面时赋予的热度值
        /// </summary>
        public const int OPEN_HOT = 3;
        
        /// <summary>
        /// 获取界面时赋予的热度值
        /// </summary>
        public const int GET_HOT = 2;
        
        /// <summary>
        /// 每次行为造成的衰减热度值
        /// </summary>
        public const int WEAKEN = 1;
    }

    /// <summary>
    /// UI 管理工具 - 核心 API
    /// </summary>
    public partial class UIKit
    {
        static UIKit() => _ = UIRoot.Instance;

        /// <summary>
        /// 创建界面时赋予的热度值
        /// </summary>
        public static int OpenHot = UIKitConfig.OPEN_HOT;
        
        /// <summary>
        /// 获取界面时赋予的热度值
        /// </summary>
        public static int GetHot = UIKitConfig.GET_HOT;
        
        /// <summary>
        /// 每次行为造成的衰减热度值
        /// </summary>
        public static int Weaken = UIKitConfig.WEAKEN;

        /// <summary>
        /// 已经存在的Panel缓存
        /// </summary>
        private static readonly Dictionary<Type, PanelHandler> PanelCacheDic = new();

        #region Handler
        
        /// <summary>
        /// 尝试获取已缓存的 Handler（不创建）
        /// </summary>
        internal static bool TryGetHandler(Type type, out PanelHandler handler)
        {
            return PanelCacheDic.TryGetValue(type, out handler);
        }
        
        /// <summary>
        /// 获取或创建 Handler（同步，会触发 CreateUI）
        /// </summary>
        private static PanelHandler GetOrCreateHandler(Type type, UILevel level, IUIData data, out bool isNewCreated)
        {
            // 1. 检查主缓存
            if (TryGetHandler(type, out var handler))
            {
                isNewCreated = false;
                handler.Data = data;
                return handler;
            }
            
            // 2. 检查预加载缓存
            if (UICacheManager.TryGetPreloaded(type, out handler))
            {
                isNewCreated = false;
                handler.Data = data;
                PanelCacheDic.TryAdd(type, handler);
                handler.Hot += OpenHot;
                return handler;
            }
            
            // 3. 创建新面板
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
            // 1. 检查主缓存
            if (TryGetHandler(type, out var handler))
            {
                handler.Data = data;
                onComplete?.Invoke(handler, false);
                return;
            }
            
            // 2. 检查预加载缓存
            if (UICacheManager.TryGetPreloaded(type, out handler))
            {
                handler.Data = data;
                PanelCacheDic.TryAdd(type, handler);
                handler.Hot += OpenHot;
                onComplete?.Invoke(handler, false);
                return;
            }
            
            // 3. 创建新面板
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

        #region 核心 API

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
            OpenPanelAsync(type, level, data, callback);
        }

        /// <summary>
        /// 异步创建指定类型的Panel（通过 Type）
        /// </summary>
        public static void OpenPanelAsync(Type type, UILevel level, IUIData data, Action<IPanel> callback)
        {
            GetOrCreateHandlerAsync(type, level, data, (handler, isNew) =>
            {
                if (handler?.Panel != null)
                {
                    if (!isNew)
                    {
                        OpenAndShowPanel(handler.Panel, data);
                    }
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
            
            // 检查面板是否已被销毁（Unity 对象需要用 == null 检查）
            var unityObj = panel as UnityEngine.Object;
            if (unityObj == null)
            {
                if (panel.Handler != null)
                {
                    UIStackManager.RemoveFromStack(panel);
                    UILevelManager.Unregister(panel);
                    PanelCacheDic.Remove(panel.Handler.Type);
                    panel.Handler.Recycle();
                }
                return;
            }
            
            panel.Close();
            
            if (panel.Handler == null) return;
            
            UIStackManager.RemoveFromStack(panel);
            UILevelManager.Unregister(panel);
            
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
            UIStackManager.ClearAll();
            UILevelManager.ClearAll();
        }
        
        /// <summary>
        /// 强制清理所有面板（用于测试或场景切换）
        /// </summary>
        public static void ForceCloseAllPanel()
        {
            Pool.List<PanelHandler>(handlersToRemove =>
            {
                foreach (var handler in PanelCacheDic.Values)
                {
                    if (handler == null) continue;
                    
                    handler.Hot = 0;
                    
                    if (handler.Panel != null)
                    {
                        var unityObj = handler.Panel as UnityEngine.Object;
                        if (unityObj != null)
                        {
                            handler.Panel.Close();
                            DestroyPanel(handler.Panel);
                        }
                    }
                    
                    handlersToRemove.Add(handler);
                }
                
                for (int i = 0; i < handlersToRemove.Count; i++)
                {
                    PanelCacheDic.Remove(handlersToRemove[i].Type);
                    handlersToRemove[i].Recycle();
                }
            });
            UIStackManager.ClearAll();
            UILevelManager.ClearAll();
        }

        /// <summary>
        /// 设置自定义的Panel加载器池
        /// </summary>
        public static void SetPanelLoader(IPanelLoaderPool loaderPool) => UIRoot.Instance.SetPanelLoader(loaderPool);

        #endregion

        #region 内部方法
        
        private static IPanel CreateUI(PanelHandler handler)
        {
            if (handler == null) return null;
            
            var panel = UIRoot.Instance.LoadPanel(handler);
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
            
            UIRoot.Instance.LoadPanelAsync(handler, panel =>
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
        internal static void OpenAndShowPanel(IPanel panel, IUIData data = null)
        {
            if (panel == null) return;
            panel.Open(data);
            panel.Show();
        }
        
        /// <summary>
        /// 设置Panel的基本属性
        /// </summary>
        internal static void SetupPanel(PanelHandler handler, IPanel panel)
        {
            panel.Transform.gameObject.name = handler.Type.Name;
            PanelCacheDic.TryAdd(handler.Type, handler);
            handler.Hot += OpenHot;
            panel.Init(handler.Data);
            UILevelManager.Register(panel);
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
