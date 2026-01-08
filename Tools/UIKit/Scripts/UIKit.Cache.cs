using System;
using System.Collections.Generic;
#if YOKIFRAME_UNITASK_SUPPORT
using System.Threading;
using Cysharp.Threading.Tasks;
#endif

namespace YokiFrame
{
    /// <summary>
    /// UIKit 缓存管理扩展
    /// </summary>
    public partial class UIKit
    {
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
            var cachedTypes = UICacheManager.GetCachedPanelTypes();
            var result = new List<Type>(cachedTypes.Count + 8);
            
            // 添加主缓存中的类型（通过 partial class 访问 PanelCacheDic）
            foreach (var kvp in PanelCacheDic)
            {
                result.Add(kvp.Key);
            }
            
            // 添加预加载缓存中的类型（去重）
            foreach (var type in cachedTypes)
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
            var result = new List<IPanel>(PanelCacheDic.Count);
            foreach (var handler in PanelCacheDic.Values)
            {
                if (handler?.Panel != null)
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

#if YOKIFRAME_UNITASK_SUPPORT
        #region UniTask 缓存

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
