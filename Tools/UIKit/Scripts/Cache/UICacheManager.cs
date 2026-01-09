using System;
using System.Collections.Generic;
#if YOKIFRAME_UNITASK_SUPPORT
using System.Threading;
using Cysharp.Threading.Tasks;
#endif

namespace YokiFrame
{
    /// <summary>
    /// 面板缓存模式
    /// </summary>
    public enum PanelCacheMode
    {
        /// <summary>
        /// 不缓存，关闭后立即销毁
        /// </summary>
        None,
        
        /// <summary>
        /// 使用 Hot 系统自动管理（默认）
        /// </summary>
        Hot,
        
        /// <summary>
        /// 永久缓存，直到手动清理
        /// </summary>
        Permanent
    }

    /// <summary>
    /// UI 缓存管理器
    /// 支持预加载、LRU 淘汰策略
    /// </summary>
    internal static partial class UICacheManager
    {
        /// <summary>
        /// 默认缓存容量
        /// </summary>
        private const int DEFAULT_CAPACITY = 10;
        
        /// <summary>
        /// 当前缓存容量
        /// </summary>
        private static int sCapacity = DEFAULT_CAPACITY;
        
        /// <summary>
        /// 预加载的面板缓存（尚未显示过的面板）
        /// Key: Type, Value: PanelHandler
        /// </summary>
        private static readonly Dictionary<Type, PanelHandler> sPreloadedPanels = new();
        
        /// <summary>
        /// 访问时间记录（用于 LRU）
        /// Key: Type, Value: 最后访问时间戳
        /// </summary>
        private static readonly Dictionary<Type, long> sAccessTimestamps = new();

        #region Configuration

        /// <summary>
        /// 设置缓存容量
        /// </summary>
        /// <param name="capacity">最大缓存数量</param>
        public static void SetCapacity(int capacity)
        {
            sCapacity = Math.Max(1, capacity);
            TryEvict();
        }

        /// <summary>
        /// 获取当前缓存容量
        /// </summary>
        public static int GetCapacity() => sCapacity;

        #endregion

        #region Preload Operations

        /// <summary>
        /// 预加载面板（异步，回调版本）
        /// </summary>
        /// <typeparam name="T">面板类型</typeparam>
        /// <param name="level">UI 层级</param>
        /// <param name="onComplete">完成回调</param>
        public static void PreloadPanelAsync<T>(UILevel level = UILevel.Common, Action<bool> onComplete = null) where T : UIPanel
        {
            PreloadPanelAsync(typeof(T), level, onComplete);
        }

        /// <summary>
        /// 预加载面板（异步，回调版本）
        /// </summary>
        /// <param name="panelType">面板类型</param>
        /// <param name="level">UI 层级</param>
        /// <param name="onComplete">完成回调</param>
        public static void PreloadPanelAsync(Type panelType, UILevel level = UILevel.Common, Action<bool> onComplete = null)
        {
            if (panelType == null)
            {
                onComplete?.Invoke(false);
                return;
            }

            // 检查是否已预加载
            if (sPreloadedPanels.ContainsKey(panelType))
            {
                UpdateAccessTime(panelType);
                onComplete?.Invoke(true);
                return;
            }

            // 检查是否已在主缓存中
            if (IsPanelCached(panelType))
            {
                UpdateAccessTime(panelType);
                onComplete?.Invoke(true);
                return;
            }

            // 检查容量，必要时淘汰
            TryEvict();

            // 创建 Handler 并加载
            var handler = PanelHandler.Allocate();
            handler.Type = panelType;
            handler.Level = level;
            handler.CacheMode = PanelCacheMode.Hot;

            UIRoot.Instance.LoadPanelAsync(handler, panel =>
            {
                if (panel != null && panel.Transform != null)
                {
                    // 设置面板但不显示
                    panel.Transform.gameObject.name = panelType.Name;
                    panel.Transform.gameObject.SetActive(false);
                    panel.Init(null);
                    
                    sPreloadedPanels[panelType] = handler;
                    UpdateAccessTime(panelType);
                    
                    KitLogger.Log($"[UIKit] 预加载面板成功: {panelType.Name}");
                    onComplete?.Invoke(true);
                }
                else
                {
                    handler.Recycle();
                    KitLogger.Warning($"[UIKit] 预加载面板失败: {panelType.Name}");
                    onComplete?.Invoke(false);
                }
            });
        }

        /// <summary>
        /// 尝试从预加载缓存获取面板
        /// </summary>
        /// <param name="panelType">面板类型</param>
        /// <param name="handler">输出的 Handler</param>
        /// <returns>是否找到</returns>
        internal static bool TryGetPreloaded(Type panelType, out PanelHandler handler)
        {
            if (sPreloadedPanels.TryGetValue(panelType, out handler))
            {
                sPreloadedPanels.Remove(panelType);
                UpdateAccessTime(panelType);
                return true;
            }
            handler = null;
            return false;
        }

        #endregion

        #region Cache Query

        /// <summary>
        /// 检查面板是否已缓存（包括预加载和主缓存）
        /// </summary>
        public static bool IsPanelCached(Type panelType)
        {
            return sPreloadedPanels.ContainsKey(panelType) || IsPanelInMainCache(panelType);
        }

        /// <summary>
        /// 检查面板是否已缓存
        /// </summary>
        public static bool IsPanelCached<T>() where T : UIPanel
        {
            return IsPanelCached(typeof(T));
        }

        /// <summary>
        /// 获取所有已缓存的面板类型
        /// </summary>
        public static IReadOnlyCollection<Type> GetCachedPanelTypes()
        {
            var result = new List<Type>(sPreloadedPanels.Keys);
            // 主缓存中的面板类型由 UIKit 管理，这里只返回预加载的
            return result;
        }

        /// <summary>
        /// 获取预加载缓存数量
        /// </summary>
        public static int GetPreloadedCount() => sPreloadedPanels.Count;

        #endregion

        #region Cache Clear

        /// <summary>
        /// 清理指定面板的预加载缓存
        /// </summary>
        public static void ClearPreloaded<T>() where T : UIPanel
        {
            ClearPreloaded(typeof(T));
        }

        /// <summary>
        /// 清理指定面板的预加载缓存
        /// </summary>
        public static void ClearPreloaded(Type panelType)
        {
            if (sPreloadedPanels.TryGetValue(panelType, out var handler))
            {
                sPreloadedPanels.Remove(panelType);
                sAccessTimestamps.Remove(panelType);
                
                if (handler.Panel != null && handler.Panel.Transform != null)
                {
                    UnityEngine.Object.Destroy(handler.Panel.Transform.gameObject);
                }
                handler.Recycle();
            }
        }

        /// <summary>
        /// 清理所有预加载缓存
        /// </summary>
        public static void ClearAllPreloaded()
        {
            foreach (var handler in sPreloadedPanels.Values)
            {
                if (handler?.Panel != null && handler.Panel.Transform != null)
                {
                    UnityEngine.Object.Destroy(handler.Panel.Transform.gameObject);
                }
                handler?.Recycle();
            }
            sPreloadedPanels.Clear();
            sAccessTimestamps.Clear();
        }

        #endregion

    }
}
