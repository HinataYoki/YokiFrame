using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// UI 管理工具 - 缓存管理
    /// </summary>
    public partial class UIKit
    {
        #region 缓存

        /// <summary>
        /// 检查面板是否已缓存
        /// </summary>
        public static bool IsPanelCached<T>() where T : UIPanel => Root?.IsPanelCached<T>() ?? false;

        /// <summary>
        /// 检查面板是否已缓存
        /// </summary>
        public static bool IsPanelCached(Type panelType) => Root?.IsPanelCached(panelType) ?? false;

        /// <summary>
        /// 获取所有已缓存的面板类型
        /// </summary>
        public static IReadOnlyCollection<Type> GetCachedPanelTypes() => Root?.GetCachedPanelTypes() ?? Array.Empty<Type>();

        /// <summary>
        /// 获取所有已缓存的面板实例
        /// </summary>
        public static IReadOnlyList<IPanel> GetCachedPanels() => Root?.GetCachedPanels() ?? Array.Empty<IPanel>();

        /// <summary>
        /// 获取缓存容量
        /// </summary>
        public static int GetCacheCapacity() => Root?.CacheCapacity ?? 0;

        /// <summary>
        /// 设置缓存容量
        /// </summary>
        public static void SetCacheCapacity(int capacity) { if (Root != default) Root.CacheCapacity = capacity; }

        /// <summary>
        /// 预加载面板
        /// </summary>
        public static void PreloadPanelAsync<T>(UILevel level = UILevel.Common, Action<bool> onComplete = null)
            where T : UIPanel
        {
            Root?.PreloadPanelAsync<T>(level, onComplete);
        }

        /// <summary>
        /// 预加载面板
        /// </summary>
        public static void PreloadPanelAsync(Type panelType, UILevel level = UILevel.Common,
            Action<bool> onComplete = null)
        {
            Root?.PreloadPanelAsync(panelType, level, onComplete);
        }

        /// <summary>
        /// 清理指定预加载面板
        /// </summary>
        public static void ClearPreloadedCache<T>() where T : UIPanel
        {
            Root?.ClearPreloadedPanel<T>();
        }

        /// <summary>
        /// 清理所有预加载面板
        /// </summary>
        public static void ClearAllPreloadedCache()
        {
            Root?.ClearAllPreloadedPanels();
        }

        #endregion
    }
}
