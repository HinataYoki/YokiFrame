#if !GODOT
using System;
using System.Collections.Generic;
using System.Threading;
#if YOKIFRAME_UNITASK_SUPPORT
using Cysharp.Threading.Tasks;
#else
using System.Threading.Tasks;
#endif

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
        public static bool IsPanelCached<T>() where T : UIPanel
        {
            var root = Root;
            return root != default ? root.IsPanelCached<T>() : false;
        }

        /// <summary>
        /// 检查面板是否已缓存
        /// </summary>
        public static bool IsPanelCached(Type panelType)
        {
            var root = Root;
            return root != default ? root.IsPanelCached(panelType) : false;
        }

        /// <summary>
        /// 检查面板是否已预加载。
        /// </summary>
        public static bool IsPanelPreloaded<T>() where T : UIPanel
        {
            var root = Root;
            return root != default && root.IsPanelPreloaded<T>();
        }

        /// <summary>
        /// 检查面板是否已预加载。
        /// </summary>
        public static bool IsPanelPreloaded(Type panelType)
        {
            var root = Root;
            return root != default && root.IsPanelPreloaded(panelType);
        }

        /// <summary>
        /// 获取所有已缓存的面板类型
        /// </summary>
        public static IReadOnlyCollection<Type> GetCachedPanelTypes()
        {
            var root = Root;
            return root != default ? root.GetCachedPanelTypes() : Array.Empty<Type>();
        }

        /// <summary>
        /// 获取所有已缓存的面板实例
        /// </summary>
        public static IReadOnlyList<IPanel> GetCachedPanels()
        {
            var root = Root;
            return root != default ? root.GetCachedPanels() : Array.Empty<IPanel>();
        }

        /// <summary>
        /// 获取缓存容量
        /// </summary>
        public static int GetCacheCapacity()
        {
            var root = Root;
            return root != default ? root.CacheCapacity : 0;
        }

        /// <summary>
        /// 设置缓存容量
        /// </summary>
        public static void SetCacheCapacity(int capacity)
        {
            var root = Root;
            if (root != default) root.CacheCapacity = capacity;
        }

        /// <summary>
        /// 预加载面板
        /// </summary>
#if YOKIFRAME_UNITASK_SUPPORT
        public static UniTask<bool> PreloadPanelAsync<T>(UILevel level = default,
            CancellationToken ct = default) where T : UIPanel
#else
        public static Task<bool> PreloadPanelAsync<T>(UILevel level = default,
            CancellationToken ct = default) where T : UIPanel
#endif
        {
            return PreloadPanelAsync(typeof(T), level, ct);
        }

        /// <summary>
        /// 预加载面板
        /// </summary>
#if YOKIFRAME_UNITASK_SUPPORT
        public static UniTask<bool> PreloadPanelAsync(Type panelType, UILevel level = default,
            CancellationToken ct = default)
#else
        public static Task<bool> PreloadPanelAsync(Type panelType, UILevel level = default,
            CancellationToken ct = default)
#endif
        {
            var root = Root;
            if (root != default)
                return root.PreloadPanelAsync(panelType, level, ct);

#if YOKIFRAME_UNITASK_SUPPORT
            return UniTask.FromResult(false);
#else
            return Task.FromResult(false);
#endif
        }

        /// <summary>
        /// 清理指定预加载面板
        /// </summary>
        public static void ClearPreloadedCache<T>() where T : UIPanel
        {
            var root = Root;
            if (root == default) return;
            root.ClearPreloadedPanel<T>();
        }

        /// <summary>
        /// 清理所有预加载面板
        /// </summary>
        public static void ClearAllPreloadedCache()
        {
            var root = Root;
            if (root == default) return;
            root.ClearAllPreloadedPanels();
        }

        #endregion
    }
}
#endif
