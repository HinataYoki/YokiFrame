using System;
using System.Collections.Generic;
#if YOKIFRAME_UNITASK_SUPPORT
using System.Threading;
using Cysharp.Threading.Tasks;
#endif

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

    public class UIKit
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
        private static bool TryGetHandler(Type type, out PanelHandler handler)
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
                // 将预加载的面板移入主缓存
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
                // 将预加载的面板移入主缓存
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
            
            // 检查面板是否已被销毁（Unity 对象需要用 == null 检查）
            var unityObj = panel as UnityEngine.Object;
            if (unityObj == null)
            {
                // 面板已销毁，只清理缓存
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
                    
                    // 强制设置 Hot 为 0，确保面板被销毁
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
            UIStackManager.Push(panel, UIStackManager.DEFAULT_STACK, hidePreLevel);
        }

        /// <summary>
        /// 压入一个Panel到指定命名栈中
        /// </summary>
        /// <param name="stackName">栈名称</param>
        /// <param name="hidePreLevel">隐藏栈中上一层UI</param>
        public static void PushPanel(IPanel panel, string stackName, bool hidePreLevel = true)
        {
            UIStackManager.Push(panel, stackName, hidePreLevel);
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
            return UIStackManager.Pop(UIStackManager.DEFAULT_STACK, showPreLevel, autoClose);
        }

        /// <summary>
        /// 从指定命名栈弹出一个面板
        /// </summary>
        /// <param name="stackName">栈名称</param>
        /// <param name="showPreLevel">自动显示上一层面板</param>
        /// <param name="autoClose">自动关闭弹出面板</param>
        public static IPanel PopPanel(string stackName, bool showPreLevel = true, bool autoClose = true)
        {
            return UIStackManager.Pop(stackName, showPreLevel, autoClose);
        }

        /// <summary>
        /// 查看栈顶面板（不移除）
        /// </summary>
        public static IPanel PeekPanel(string stackName = UIStackManager.DEFAULT_STACK)
        {
            return UIStackManager.Peek(stackName);
        }

        /// <summary>
        /// 获取指定栈的深度
        /// </summary>
        public static int GetStackDepth(string stackName = UIStackManager.DEFAULT_STACK)
        {
            return UIStackManager.GetDepth(stackName);
        }

        /// <summary>
        /// 获取所有栈名称
        /// </summary>
        public static IReadOnlyCollection<string> GetAllStackNames()
        {
            return UIStackManager.GetStackNames();
        }

        /// <summary>
        /// 清空指定栈
        /// </summary>
        public static void ClearStack(string stackName = UIStackManager.DEFAULT_STACK, bool closeAll = true)
        {
            UIStackManager.Clear(stackName, closeAll);
        }
        
        /// <summary>
        /// 关闭所有栈上面板
        /// </summary>
        public static void CloseAllStackPanel()
        {
            UIStackManager.Clear(UIStackManager.DEFAULT_STACK, true);
        }
        
        /// <summary>
        /// 设置自定义的Panel加载器池
        /// </summary>
        public static void SetPanelLoader(IPanelLoaderPool loaderPool) => UIRoot.Instance.SetPanelLoader(loaderPool);

        #region 对话框 API

        /// <summary>
        /// 设置默认对话框类型
        /// </summary>
        public static void SetDefaultDialogType<T>() where T : UIDialogPanel
        {
            UIDialogManager.SetDefaultDialogType<T>();
        }

        /// <summary>
        /// 设置默认输入对话框类型
        /// </summary>
        public static void SetDefaultPromptType<T>() where T : UIDialogPanel
        {
            UIDialogManager.SetDefaultPromptType<T>();
        }

        /// <summary>
        /// 显示对话框
        /// </summary>
        public static void ShowDialog(DialogConfig config, Action<DialogResultData> onResult = null)
        {
            UIDialogManager.ShowDialog(config, onResult);
        }

        /// <summary>
        /// 显示指定类型的对话框
        /// </summary>
        public static void ShowDialog<T>(DialogConfig config, Action<DialogResultData> onResult = null) where T : UIDialogPanel
        {
            UIDialogManager.ShowDialog<T>(config, onResult);
        }

        /// <summary>
        /// 显示 Alert 对话框
        /// </summary>
        public static void Alert(string message, string title = null, Action onClose = null)
        {
            UIDialogManager.Alert(message, title, onClose);
        }

        /// <summary>
        /// 显示 Confirm 对话框
        /// </summary>
        public static void Confirm(string message, string title = null, Action<bool> onResult = null)
        {
            UIDialogManager.Confirm(message, title, onResult);
        }

        /// <summary>
        /// 显示 Prompt 对话框
        /// </summary>
        public static void Prompt(string message, string title = null, string defaultValue = null, Action<bool, string> onResult = null)
        {
            UIDialogManager.Prompt(message, title, defaultValue, onResult);
        }

        /// <summary>
        /// 是否有对话框正在显示
        /// </summary>
        public static bool HasActiveDialog => UIDialogManager.HasActiveDialog;

        /// <summary>
        /// 清空对话框队列
        /// </summary>
        public static void ClearDialogQueue()
        {
            UIDialogManager.ClearQueue();
        }

#if YOKIFRAME_UNITASK_SUPPORT
        /// <summary>
        /// [UniTask] 显示对话框
        /// </summary>
        public static UniTask<DialogResultData> ShowDialogUniTaskAsync(DialogConfig config, CancellationToken ct = default)
        {
            return UIDialogManager.ShowDialogUniTaskAsync(config, ct);
        }

        /// <summary>
        /// [UniTask] 显示指定类型的对话框
        /// </summary>
        public static UniTask<DialogResultData> ShowDialogUniTaskAsync<T>(DialogConfig config, CancellationToken ct = default) where T : UIDialogPanel
        {
            return UIDialogManager.ShowDialogUniTaskAsync<T>(config, ct);
        }

        /// <summary>
        /// [UniTask] Alert 对话框
        /// </summary>
        public static UniTask AlertUniTaskAsync(string message, string title = null, CancellationToken ct = default)
        {
            return UIDialogManager.AlertUniTaskAsync(message, title, ct);
        }

        /// <summary>
        /// [UniTask] Confirm 对话框
        /// </summary>
        public static UniTask<bool> ConfirmUniTaskAsync(string message, string title = null, CancellationToken ct = default)
        {
            return UIDialogManager.ConfirmUniTaskAsync(message, title, ct);
        }

        /// <summary>
        /// [UniTask] Prompt 对话框
        /// </summary>
        public static UniTask<(bool confirmed, string value)> PromptUniTaskAsync(string message, string title = null, string defaultValue = null, CancellationToken ct = default)
        {
            return UIDialogManager.PromptUniTaskAsync(message, title, defaultValue, ct);
        }
#endif

        #endregion

        #region 焦点系统 API

        /// <summary>
        /// 获取焦点系统实例
        /// </summary>
        public static UIFocusSystem FocusSystem => UIFocusSystem.Instance;

        /// <summary>
        /// 设置焦点到指定对象
        /// </summary>
        public static void SetFocus(UnityEngine.GameObject target)
        {
            UIFocusSystem.Instance?.SetFocus(target);
        }

        /// <summary>
        /// 设置焦点到指定 Selectable
        /// </summary>
        public static void SetFocus(UnityEngine.UI.Selectable selectable)
        {
            UIFocusSystem.Instance?.SetFocus(selectable);
        }

        /// <summary>
        /// 清除当前焦点
        /// </summary>
        public static void ClearFocus()
        {
            UIFocusSystem.Instance?.ClearFocus();
        }

        /// <summary>
        /// 获取当前焦点对象
        /// </summary>
        public static UnityEngine.GameObject GetCurrentFocus()
        {
            return UIFocusSystem.Instance?.CurrentFocus;
        }

        /// <summary>
        /// 获取当前输入模式
        /// </summary>
        public static UIInputMode GetInputMode()
        {
            return UIFocusSystem.Instance?.CurrentInputMode ?? UIInputMode.Pointer;
        }

        #endregion

        #region 层级管理 API

        /// <summary>
        /// 设置面板层级
        /// </summary>
        public static void SetPanelLevel(IPanel panel, UILevel level, int subLevel = 0)
        {
            UILevelManager.SetPanelLevel(panel, level, subLevel);
        }

        /// <summary>
        /// 设置面板子层级
        /// </summary>
        public static void SetPanelSubLevel(IPanel panel, int subLevel)
        {
            UILevelManager.SetPanelSubLevel(panel, subLevel);
        }

        /// <summary>
        /// 获取指定层级的顶部面板
        /// </summary>
        public static IPanel GetTopPanelAtLevel(UILevel level)
        {
            return UILevelManager.GetTopPanelAtLevel(level);
        }

        /// <summary>
        /// 获取全局顶部面板
        /// </summary>
        public static IPanel GetGlobalTopPanel()
        {
            return UILevelManager.GetGlobalTopPanel();
        }

        /// <summary>
        /// 获取指定层级的所有面板
        /// </summary>
        public static IReadOnlyList<IPanel> GetPanelsAtLevel(UILevel level)
        {
            return UILevelManager.GetPanelsAtLevel(level);
        }

        /// <summary>
        /// 设置面板为模态
        /// </summary>
        public static void SetPanelModal(IPanel panel, bool isModal)
        {
            UILevelManager.SetModal(panel, isModal);
        }

        /// <summary>
        /// 检查是否有模态面板阻断
        /// </summary>
        public static bool HasModalBlocker()
        {
            return UILevelManager.HasModalBlocker();
        }

        #endregion

        #region 缓存管理 API

        /// <summary>
        /// 预加载面板（异步，回调版本）
        /// </summary>
        /// <typeparam name="T">面板类型</typeparam>
        /// <param name="level">UI 层级</param>
        /// <param name="onComplete">完成回调</param>
        public static void PreloadPanelAsync<T>(UILevel level = UILevel.Common, Action<bool> onComplete = null) where T : UIPanel
        {
            UICacheManager.PreloadPanelAsync<T>(level, onComplete);
        }

        /// <summary>
        /// 预加载面板（异步，回调版本）
        /// </summary>
        /// <param name="panelType">面板类型</param>
        /// <param name="level">UI 层级</param>
        /// <param name="onComplete">完成回调</param>
        public static void PreloadPanelAsync(Type panelType, UILevel level = UILevel.Common, Action<bool> onComplete = null)
        {
            UICacheManager.PreloadPanelAsync(panelType, level, onComplete);
        }

        /// <summary>
        /// 检查面板是否已缓存
        /// </summary>
        public static bool IsPanelCached<T>() where T : UIPanel
        {
            return UICacheManager.IsPanelCached<T>() || TryGetHandler(typeof(T), out _);
        }

        /// <summary>
        /// 检查面板是否已缓存
        /// </summary>
        public static bool IsPanelCached(Type panelType)
        {
            return UICacheManager.IsPanelCached(panelType) || TryGetHandler(panelType, out _);
        }

        /// <summary>
        /// 获取所有已缓存的面板类型
        /// </summary>
        public static IReadOnlyCollection<Type> GetCachedPanelTypes()
        {
            var result = new List<Type>(PanelCacheDic.Keys);
            foreach (var type in UICacheManager.GetCachedPanelTypes())
            {
                if (!result.Contains(type))
                {
                    result.Add(type);
                }
            }
            return result;
        }

        /// <summary>
        /// 获取所有已缓存的面板实例
        /// </summary>
        public static IReadOnlyList<IPanel> GetCachedPanels()
        {
            var result = new List<IPanel>();
            foreach (var handler in PanelCacheDic.Values)
            {
                if (handler.Panel != null)
                {
                    result.Add(handler.Panel);
                }
            }
            return result;
        }

        /// <summary>
        /// 获取缓存容量
        /// </summary>
        public static int GetCacheCapacity()
        {
            return UICacheManager.GetCapacity();
        }

        /// <summary>
        /// 清理指定面板的预加载缓存
        /// </summary>
        public static void ClearPreloadedCache<T>() where T : UIPanel
        {
            UICacheManager.ClearPreloaded<T>();
        }

        /// <summary>
        /// 清理所有预加载缓存
        /// </summary>
        public static void ClearAllPreloadedCache()
        {
            UICacheManager.ClearAllPreloaded();
        }

        /// <summary>
        /// 设置缓存容量
        /// </summary>
        public static void SetCacheCapacity(int capacity)
        {
            UICacheManager.SetCapacity(capacity);
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
            
            // 注册到层级管理器
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

#if YOKIFRAME_UNITASK_SUPPORT
        #region UniTask 异步方法

        /// <summary>
        /// [UniTask] 异步打开指定类型的Panel
        /// </summary>
        public static async UniTask<T> OpenPanelUniTaskAsync<T>(UILevel level = UILevel.Common, IUIData data = null, CancellationToken cancellationToken = default) where T : UIPanel
        {
            WeakenHot();
            var type = typeof(T);
            
            // 1. 检查主缓存
            if (TryGetHandler(type, out var handler))
            {
                handler.Data = data;
                OpenAndShowPanel(handler.Panel, data);
                return handler.Panel as T;
            }
            
            // 2. 检查预加载缓存
            if (UICacheManager.TryGetPreloaded(type, out handler))
            {
                handler.Data = data;
                PanelCacheDic.TryAdd(type, handler);
                handler.Hot += OpenHot;
                OpenAndShowPanel(handler.Panel, data);
                return handler.Panel as T;
            }
            
            // 3. 创建新 Handler
            handler = PanelHandler.Allocate();
            handler.Type = type;
            handler.Level = level;
            handler.Data = data;
            
            // 异步创建 UI
            var panel = await CreateUIUniTaskAsync(handler, cancellationToken);
            
            if (panel != null && panel.Transform != null)
            {
                return panel as T;
            }
            
            KitLogger.Error($"[UIKit] OpenPanelUniTaskAsync: {type.Name} 创建失败");
            return null;
        }

        /// <summary>
        /// [UniTask] 异步打开并压入Panel到栈中
        /// </summary>
        public static async UniTask<T> PushOpenPanelUniTaskAsync<T>(UILevel level = UILevel.Common, IUIData data = null, bool hidePreLevel = true, CancellationToken cancellationToken = default) where T : UIPanel
        {
            var panel = await OpenPanelUniTaskAsync<T>(level, data, cancellationToken);
            if (panel != null)
            {
                PushPanel(panel, hidePreLevel);
            }
            return panel;
        }

        /// <summary>
        /// [UniTask] 异步弹出面板（等待动画完成）
        /// </summary>
        public static UniTask<IPanel> PopPanelUniTaskAsync(bool showPreLevel = true, bool autoClose = true, CancellationToken cancellationToken = default)
        {
            return UIStackManager.PopUniTaskAsync(UIStackManager.DEFAULT_STACK, showPreLevel, autoClose, cancellationToken);
        }

        /// <summary>
        /// [UniTask] 从指定命名栈异步弹出面板（等待动画完成）
        /// </summary>
        public static UniTask<IPanel> PopPanelUniTaskAsync(string stackName, bool showPreLevel = true, bool autoClose = true, CancellationToken cancellationToken = default)
        {
            return UIStackManager.PopUniTaskAsync(stackName, showPreLevel, autoClose, cancellationToken);
        }

        /// <summary>
        /// [UniTask] 异步创建 UI
        /// </summary>
        private static async UniTask<IPanel> CreateUIUniTaskAsync(PanelHandler handler, CancellationToken cancellationToken)
        {
            if (handler == null) return null;
            
            var panel = await UIRoot.Instance.LoadPanelUniTaskAsync(handler, cancellationToken);
            
            if (panel != null && panel.Transform != null)
            {
                SetupPanel(handler, panel);
                OpenAndShowPanel(panel, handler.Data);
                return panel;
            }
            
            handler.Recycle();
            return null;
        }

        /// <summary>
        /// [UniTask] 预加载面板
        /// </summary>
        public static UniTask<bool> PreloadPanelUniTaskAsync<T>(UILevel level = UILevel.Common, CancellationToken cancellationToken = default) where T : UIPanel
        {
            return UICacheManager.PreloadPanelUniTaskAsync<T>(level, cancellationToken);
        }

        /// <summary>
        /// [UniTask] 预加载面板
        /// </summary>
        public static UniTask<bool> PreloadPanelUniTaskAsync(Type panelType, UILevel level = UILevel.Common, CancellationToken cancellationToken = default)
        {
            return UICacheManager.PreloadPanelUniTaskAsync(panelType, level, cancellationToken);
        }

        #endregion
#endif
    }
}