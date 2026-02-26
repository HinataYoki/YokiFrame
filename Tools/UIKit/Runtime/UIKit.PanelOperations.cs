using System;

namespace YokiFrame
{
    /// <summary>
    /// UI 管理工具 - 面板操作
    /// </summary>
    public partial class UIKit
    {
        #region 面板操作

        /// <summary>
        /// 获取指定类型的 Panel（纯查询，不触发热度衰减）
        /// </summary>
        public static T GetPanel<T>() where T : UIPanel
        {
            var root = Root;
            if (root == default) return null;
            
            if (root.TryGetCachedHandler(typeof(T), out var handler))
            {
                handler.Hot += root.GetHot;
                return handler.Panel as T;
            }
            return null;
        }

        /// <summary>
        /// 打开指定类型的 Panel
        /// </summary>
        public static T OpenPanel<T>(UILevel level = UILevel.Common, IUIData data = null) where T : UIPanel
        {
            return Root?.OpenPanelInternal(typeof(T), level, data) as T;
        }

        /// <summary>
        /// 异步打开指定类型的 Panel
        /// </summary>
        public static void OpenPanelAsync<T>(Action<IPanel> callback = null,
            UILevel level = UILevel.Common, IUIData data = null) where T : UIPanel
        {
            Root?.OpenPanelAsyncInternal(typeof(T), level, data, callback);
        }

        /// <summary>
        /// 异步打开指定类型的 Panel（通过 Type）
        /// </summary>
        public static void OpenPanelAsync(Type type, UILevel level, IUIData data, Action<IPanel> callback)
        {
            Root?.OpenPanelAsyncInternal(type, level, data, callback);
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
            var root = Root;
            if (root == default) return;
            
            foreach (var panel in root.GetCachedPanels())
            {
                if (panel != default) panel.Hide();
            }
        }

        /// <summary>
        /// 关闭指定类型的 Panel
        /// </summary>
        public static void ClosePanel<T>() where T : UIPanel
        {
            var root = Root;
            if (root == default) return;
            
            if (root.TryGetCachedHandler(typeof(T), out var handler))
            {
                root.ClosePanelInternal(handler.Panel);
            }
        }

        /// <summary>
        /// 关闭传入的 Panel 实例
        /// </summary>
        public static void ClosePanel(IPanel panel)
        {
            Root?.ClosePanelInternal(panel);
        }

        /// <summary>
        /// 关闭所有面板
        /// </summary>
        public static void CloseAllPanel()
        {
            var root = Root;
            if (root == default) return;
            
            Pool.List<IPanel>(panelsToClose =>
            {
                foreach (var panel in root.GetCachedPanels())
                {
                    panelsToClose.Add(panel);
                }
                for (int i = 0; i < panelsToClose.Count; i++)
                {
                    root.ClosePanelInternal(panelsToClose[i]);
                }
            });
            root.ClearAllStacks();
            root.ClearAllLevels();
        }

        /// <summary>
        /// 设置自定义的 Panel 加载器池
        /// </summary>
        public static void SetPanelLoader(IPanelLoaderPool loaderPool)
        {
            Root?.SetPanelLoader(loaderPool);
        }

        #endregion
    }
}
