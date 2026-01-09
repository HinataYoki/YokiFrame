using System;
using System.Collections.Generic;
#if YOKIFRAME_UNITASK_SUPPORT
using System.Threading;
using Cysharp.Threading.Tasks;
#endif

namespace YokiFrame
{
    /// <summary>
    /// UI 缓存管理器 - LRU 淘汰、内部辅助、UniTask 异步方法
    /// </summary>
    internal static partial class UICacheManager
    {
        #region LRU Eviction

        /// <summary>
        /// 更新访问时间
        /// </summary>
        internal static void UpdateAccessTime(Type panelType)
        {
            sAccessTimestamps[panelType] = DateTime.UtcNow.Ticks;
        }

        /// <summary>
        /// 尝试淘汰面板以腾出空间
        /// </summary>
        private static void TryEvict()
        {
            while (sPreloadedPanels.Count >= sCapacity)
            {
                EvictLeastRecentlyUsed();
            }
        }

        /// <summary>
        /// 淘汰最近最少使用的面板
        /// 优先淘汰 Hot 值最低的，Hot 值相同时淘汰访问时间最早的
        /// </summary>
        private static void EvictLeastRecentlyUsed()
        {
            if (sPreloadedPanels.Count == 0) return;

            Type lruType = null;
            int lowestHot = int.MaxValue;
            long oldestAccess = long.MaxValue;

            foreach (var kvp in sPreloadedPanels)
            {
                var handler = kvp.Value;
                var hot = handler?.Hot ?? 0;
                sAccessTimestamps.TryGetValue(kvp.Key, out var accessTime);

                // 优先淘汰 Hot 值低的，Hot 相同时淘汰访问时间早的
                if (hot < lowestHot || (hot == lowestHot && accessTime < oldestAccess))
                {
                    lowestHot = hot;
                    oldestAccess = accessTime;
                    lruType = kvp.Key;
                }
            }

            if (lruType != null)
            {
                KitLogger.Log($"[UIKit] LRU 淘汰预加载面板: {lruType.Name} (Hot={lowestHot})");
                ClearPreloaded(lruType);
            }
        }

        #endregion

        #region Internal Helpers

        /// <summary>
        /// 检查面板是否在主缓存中（由 UIKit.PanelCacheDic 管理）
        /// </summary>
        private static bool IsPanelInMainCache(Type panelType)
        {
            // 通过 UIKit.GetPanel 间接检查，但这会影响 Hot 值
            // 由于 PanelCacheDic 是 private，暂时返回 false，后续通过 UIKit 暴露接口
            return false;
        }

        /// <summary>
        /// 清理所有缓存（用于测试）
        /// </summary>
        internal static void ClearAll()
        {
            ClearAllPreloaded();
        }

        #endregion

#if YOKIFRAME_UNITASK_SUPPORT
        #region UniTask Async Methods

        /// <summary>
        /// [UniTask] 预加载面板
        /// </summary>
        public static async UniTask<bool> PreloadPanelUniTaskAsync<T>(UILevel level = UILevel.Common, CancellationToken ct = default) where T : UIPanel
        {
            return await PreloadPanelUniTaskAsync(typeof(T), level, ct);
        }

        /// <summary>
        /// [UniTask] 预加载面板
        /// </summary>
        public static async UniTask<bool> PreloadPanelUniTaskAsync(Type panelType, UILevel level = UILevel.Common, CancellationToken ct = default)
        {
            if (panelType == null) return false;

            // 检查是否已预加载
            if (sPreloadedPanels.ContainsKey(panelType))
            {
                UpdateAccessTime(panelType);
                return true;
            }

            // 检查是否已在主缓存中
            if (IsPanelCached(panelType))
            {
                UpdateAccessTime(panelType);
                return true;
            }

            // 检查容量，必要时淘汰
            TryEvict();

            // 创建 Handler 并加载
            var handler = PanelHandler.Allocate();
            handler.Type = panelType;
            handler.Level = level;
            handler.CacheMode = PanelCacheMode.Hot;

            var panel = await UIRoot.Instance.LoadPanelUniTaskAsync(handler, ct);

            if (panel != null && panel.Transform != null)
            {
                // 设置面板但不显示
                panel.Transform.gameObject.name = panelType.Name;
                panel.Transform.gameObject.SetActive(false);
                panel.Init(null);
                
                sPreloadedPanels[panelType] = handler;
                UpdateAccessTime(panelType);
                
                KitLogger.Log($"[UIKit] 预加载面板成功: {panelType.Name}");
                return true;
            }

            handler.Recycle();
            KitLogger.Warning($"[UIKit] 预加载面板失败: {panelType.Name}");
            return false;
        }

        #endregion
#endif
    }
}
