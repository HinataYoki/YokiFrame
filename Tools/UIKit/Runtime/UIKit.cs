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
