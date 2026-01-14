using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// UI 管理工具 - 静态门面
    /// 所有调用转发到 UIRoot 实例
    /// </summary>
    public partial class UIKit
    {
        #region 初始化

        static UIKit() => _ = UIRoot.Instance;

        #endregion

        #region 配置

        /// <summary>
        /// 创建界面时赋予的热度值
        /// </summary>
        public static int OpenHot
        {
            get => UIRoot.Instance.OpenHot;
            set => UIRoot.Instance.OpenHot = value;
        }

        /// <summary>
        /// 获取界面时赋予的热度值
        /// </summary>
        public static int GetHot
        {
            get => UIRoot.Instance.GetHot;
            set => UIRoot.Instance.GetHot = value;
        }

        /// <summary>
        /// 每次行为造成的衰减热度值
        /// </summary>
        public static int Weaken
        {
            get => UIRoot.Instance.Weaken;
            set => UIRoot.Instance.Weaken = value;
        }

        #endregion

        #region 面板操作

        /// <summary>
        /// 获取指定类型的 Panel（纯查询，不触发热度衰减）
        /// </summary>
        public static T GetPanel<T>() where T : UIPanel
        {
            if (UIRoot.Instance.TryGetCachedHandler(typeof(T), out var handler))
            {
                handler.Hot += UIRoot.Instance.GetHot;
                return handler.Panel as T;
            }
            return null;
        }

        /// <summary>
        /// 打开指定类型的 Panel
        /// </summary>
        public static T OpenPanel<T>(UILevel level = UILevel.Common, IUIData data = null) where T : UIPanel
        {
            return UIRoot.Instance.OpenPanelInternal(typeof(T), level, data) as T;
        }

        /// <summary>
        /// 异步打开指定类型的 Panel
        /// </summary>
        public static void OpenPanelAsync<T>(Action<IPanel> callback = null,
            UILevel level = UILevel.Common, IUIData data = null) where T : UIPanel
        {
            UIRoot.Instance.OpenPanelAsyncInternal(typeof(T), level, data, callback);
        }

        /// <summary>
        /// 异步打开指定类型的 Panel（通过 Type）
        /// </summary>
        public static void OpenPanelAsync(Type type, UILevel level, IUIData data, Action<IPanel> callback)
        {
            UIRoot.Instance.OpenPanelAsyncInternal(type, level, data, callback);
        }

        /// <summary>
        /// 显示指定类型的 Panel
        /// </summary>
        public static void ShowPanel<T>() where T : UIPanel
        {
            var panel = GetPanel<T>();
            if (panel != default) panel.Show();
        }

        /// <summary>
        /// 隐藏指定类型的 Panel
        /// </summary>
        public static void HidePanel<T>() where T : UIPanel
        {
            var panel = GetPanel<T>();
            if (panel != default) panel.Hide();
        }

        /// <summary>
        /// 隐藏所有 Panel
        /// </summary>
        public static void HideAllPanel()
        {
            foreach (var panel in UIRoot.Instance.GetCachedPanels())
            {
                panel?.Hide();
            }
        }

        /// <summary>
        /// 关闭指定类型的 Panel
        /// </summary>
        public static void ClosePanel<T>() where T : UIPanel
        {
            if (UIRoot.Instance.TryGetCachedHandler(typeof(T), out var handler))
            {
                UIRoot.Instance.ClosePanelInternal(handler.Panel);
            }
        }

        /// <summary>
        /// 关闭传入的 Panel 实例
        /// </summary>
        public static void ClosePanel(IPanel panel)
        {
            UIRoot.Instance.ClosePanelInternal(panel);
        }

        /// <summary>
        /// 关闭所有面板
        /// </summary>
        public static void CloseAllPanel()
        {
            Pool.List<IPanel>(panelsToClose =>
            {
                foreach (var panel in UIRoot.Instance.GetCachedPanels())
                {
                    panelsToClose.Add(panel);
                }
                for (int i = 0; i < panelsToClose.Count; i++)
                {
                    UIRoot.Instance.ClosePanelInternal(panelsToClose[i]);
                }
            });
            UIRoot.Instance.ClearAllStacks();
            UIRoot.Instance.ClearAllLevels();
        }

        /// <summary>
        /// 设置自定义的 Panel 加载器池
        /// </summary>
        public static void SetPanelLoader(IPanelLoaderPool loaderPool)
        {
            UIRoot.Instance.SetPanelLoader(loaderPool);
        }

        #endregion

        #region 缓存

        /// <summary>
        /// 检查面板是否已缓存
        /// </summary>
        public static bool IsPanelCached<T>() where T : UIPanel => UIRoot.Instance.IsPanelCached<T>();

        /// <summary>
        /// 检查面板是否已缓存
        /// </summary>
        public static bool IsPanelCached(Type panelType) => UIRoot.Instance.IsPanelCached(panelType);

        /// <summary>
        /// 获取所有已缓存的面板类型
        /// </summary>
        public static IReadOnlyCollection<Type> GetCachedPanelTypes() => UIRoot.Instance.GetCachedPanelTypes();

        /// <summary>
        /// 获取所有已缓存的面板实例
        /// </summary>
        public static IReadOnlyList<IPanel> GetCachedPanels() => UIRoot.Instance.GetCachedPanels();

        /// <summary>
        /// 获取缓存容量
        /// </summary>
        public static int GetCacheCapacity() => UIRoot.Instance.CacheCapacity;

        /// <summary>
        /// 设置缓存容量
        /// </summary>
        public static void SetCacheCapacity(int capacity) => UIRoot.Instance.CacheCapacity = capacity;

        /// <summary>
        /// 预加载面板
        /// </summary>
        public static void PreloadPanelAsync<T>(UILevel level = UILevel.Common, Action<bool> onComplete = null)
            where T : UIPanel
        {
            UIRoot.Instance.PreloadPanelAsync<T>(level, onComplete);
        }

        /// <summary>
        /// 预加载面板
        /// </summary>
        public static void PreloadPanelAsync(Type panelType, UILevel level = UILevel.Common,
            Action<bool> onComplete = null)
        {
            UIRoot.Instance.PreloadPanelAsync(panelType, level, onComplete);
        }

        /// <summary>
        /// 清理指定预加载面板
        /// </summary>
        public static void ClearPreloadedCache<T>() where T : UIPanel
        {
            UIRoot.Instance.ClearPreloadedPanel<T>();
        }

        /// <summary>
        /// 清理所有预加载面板
        /// </summary>
        public static void ClearAllPreloadedCache()
        {
            UIRoot.Instance.ClearAllPreloadedPanels();
        }

        #endregion

        #region 堆栈

        /// <summary>
        /// 压入 Panel 到栈中
        /// </summary>
        public static void PushPanel<T>(bool hidePreLevel = true) where T : UIPanel
        {
            var panel = GetPanel<T>();
            if (panel != default) UIRoot.Instance.PushToStack(panel, UIRoot.DEFAULT_STACK, hidePreLevel);
        }

        /// <summary>
        /// 压入 Panel 到栈中
        /// </summary>
        public static void PushPanel(IPanel panel, bool hidePreLevel = true)
        {
            UIRoot.Instance.PushToStack(panel, UIRoot.DEFAULT_STACK, hidePreLevel);
        }

        /// <summary>
        /// 压入 Panel 到指定命名栈
        /// </summary>
        public static void PushPanel(IPanel panel, string stackName, bool hidePreLevel = true)
        {
            UIRoot.Instance.PushToStack(panel, stackName, hidePreLevel);
        }

        /// <summary>
        /// 打开并压入 Panel 到栈中
        /// </summary>
        public static void PushOpenPanel<T>(UILevel level = UILevel.Common,
            IUIData data = null, bool hidePreLevel = true) where T : UIPanel
        {
            var panel = OpenPanel<T>(level, data);
            UIRoot.Instance.PushToStack(panel, UIRoot.DEFAULT_STACK, hidePreLevel);
        }

        /// <summary>
        /// 异步打开并压入 Panel 到栈中
        /// </summary>
        public static void PushOpenPanelAsync<T>(Action<IPanel> callback = null,
            UILevel level = UILevel.Common, IUIData data = null, bool hidePreLevel = true) where T : UIPanel
        {
            OpenPanelAsync<T>(panel =>
            {
                UIRoot.Instance.PushToStack(panel, UIRoot.DEFAULT_STACK, hidePreLevel);
                callback?.Invoke(panel);
            }, level, data);
        }

        /// <summary>
        /// 弹出面板
        /// </summary>
        public static IPanel PopPanel(bool showPreLevel = true, bool autoClose = true)
        {
            return UIRoot.Instance.PopFromStack(UIRoot.DEFAULT_STACK, showPreLevel, autoClose);
        }

        /// <summary>
        /// 从指定命名栈弹出面板
        /// /// </summary>
        public static IPanel PopPanel(string stackName, bool showPreLevel = true, bool autoClose = true)
        {
            return UIRoot.Instance.PopFromStack(stackName, showPreLevel, autoClose);
        }

        /// <summary>
        /// 查看栈顶面板
        /// </summary>
        public static IPanel PeekPanel(string stackName = UIRoot.DEFAULT_STACK)
        {
            return UIRoot.Instance.PeekStack(stackName);
        }

        /// <summary>
        /// 获取栈深度
        /// </summary>
        public static int GetStackDepth(string stackName = UIRoot.DEFAULT_STACK)
        {
            return UIRoot.Instance.GetStackDepth(stackName);
        }

        /// <summary>
        /// 获取所有栈名称
        /// </summary>
        public static IReadOnlyCollection<string> GetAllStackNames()
        {
            return UIRoot.Instance.GetAllStackNames();
        }

        /// <summary>
        /// 清空指定栈
        /// </summary>
        public static void ClearStack(string stackName = UIRoot.DEFAULT_STACK, bool closeAll = true)
        {
            UIRoot.Instance.ClearStack(stackName, closeAll);
        }

        #endregion

        #region 层级

        /// <summary>
        /// 设置面板层级
        /// </summary>
        public static void SetPanelLevel(IPanel panel, UILevel level, int subLevel = 0)
        {
            UIRoot.Instance.SetPanelLevel(panel, level, subLevel);
        }

        /// <summary>
        /// 设置面板子层级
        /// </summary>
        public static void SetPanelSubLevel(IPanel panel, int subLevel)
        {
            UIRoot.Instance.SetPanelSubLevel(panel, subLevel);
        }

        /// <summary>
        /// 获取指定层级的顶部面板
        /// </summary>
        public static IPanel GetTopPanelAtLevel(UILevel level)
        {
            return UIRoot.Instance.GetTopPanelAtLevel(level);
        }

        /// <summary>
        /// 获取全局顶部面板
        /// </summary>
        public static IPanel GetGlobalTopPanel()
        {
            return UIRoot.Instance.GetGlobalTopPanel();
        }

        /// <summary>
        /// 获取指定层级的所有面板
        /// </summary>
        public static IReadOnlyList<IPanel> GetPanelsAtLevel(UILevel level)
        {
            return UIRoot.Instance.GetPanelsAtLevel(level);
        }

        /// <summary>
        /// 设置面板为模态
        /// </summary>
        public static void SetPanelModal(IPanel panel, bool isModal)
        {
            UIRoot.Instance.SetPanelModal(panel, isModal);
        }

        /// <summary>
        /// 检查是否有模态面板
        /// </summary>
        public static bool HasModalBlocker()
        {
            return UIRoot.Instance.HasModalBlocker();
        }

        #endregion

        #region 焦点

        /// <summary>
        /// 焦点系统是否启用
        /// </summary>
        public static bool FocusSystemEnabled
        {
            get => UIRoot.Instance.FocusSystemEnabled;
            set => UIRoot.Instance.FocusSystemEnabled = value;
        }

        /// <summary>
        /// 当前输入模式
        /// </summary>
        public static UIInputMode GetInputMode() => UIRoot.Instance.CurrentInputMode;

        /// <summary>
        /// 设置焦点
        /// </summary>
        public static void SetFocus(UnityEngine.GameObject target) => UIRoot.Instance.SetFocus(target);

        /// <summary>
        /// 设置焦点
        /// </summary>
        public static void SetFocus(UnityEngine.UI.Selectable selectable) => UIRoot.Instance.SetFocus(selectable);

        /// <summary>
        /// 清除焦点
        /// </summary>
        public static void ClearFocus() => UIRoot.Instance.ClearFocus();

        /// <summary>
        /// 获取当前焦点
        /// </summary>
        public static UnityEngine.GameObject GetCurrentFocus() => UIRoot.Instance.CurrentFocus;

        #endregion

        #region 对话框

        /// <summary>
        /// 设置默认对话框类型
        /// </summary>
        public static void SetDefaultDialogType<T>() where T : UIDialogPanel
        {
            UIRoot.Instance.SetDefaultDialogType<T>();
        }

        /// <summary>
        /// 设置默认输入对话框类型
        /// </summary>
        public static void SetDefaultPromptType<T>() where T : UIDialogPanel
        {
            UIRoot.Instance.SetDefaultPromptType<T>();
        }

        /// <summary>
        /// 显示对话框
        /// </summary>
        public static void ShowDialog(DialogConfig config, Action<DialogResultData> onResult = null)
        {
            UIRoot.Instance.ShowDialog(config, onResult);
        }

        /// <summary>
        /// 显示指定类型的对话框
        /// </summary>
        public static void ShowDialog<T>(DialogConfig config, Action<DialogResultData> onResult = null)
            where T : UIDialogPanel
        {
            UIRoot.Instance.ShowDialog<T>(config, onResult);
        }

        /// <summary>
        /// Alert 对话框
        /// </summary>
        public static void Alert(string message, string title = null, Action onClose = null)
        {
            UIRoot.Instance.Alert(message, title, onClose);
        }

        /// <summary>
        /// Confirm 对话框
        /// </summary>
        public static void Confirm(string message, string title = null, Action<bool> onResult = null)
        {
            UIRoot.Instance.Confirm(message, title, onResult);
        }

        /// <summary>
        /// Prompt 对话框
        /// </summary>
        public static void Prompt(string message, string title = null, string defaultValue = null,
            Action<bool, string> onResult = null)
        {
            UIRoot.Instance.Prompt(message, title, defaultValue, onResult);
        }

        /// <summary>
        /// 是否有对话框正在显示
        /// </summary>
        public static bool HasActiveDialog => UIRoot.Instance.HasActiveDialog;

        /// <summary>
        /// 清空对话框队列
        /// </summary>
        public static void ClearDialogQueue() => UIRoot.Instance.ClearDialogQueue();

        #endregion
    }
}
