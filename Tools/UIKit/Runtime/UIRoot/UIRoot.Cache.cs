using System;
using System.Collections.Generic;
#if YOKIFRAME_UNITASK_SUPPORT
using System.Threading;
using Cysharp.Threading.Tasks;
#endif

namespace YokiFrame
{
    /// <summary>
    /// UIRoot - 缓存子系统
    /// </summary>
    public partial class UIRoot
    {
        #region 缓存数据

        /// <summary>
        /// 已打开/已用过的面板缓存
        /// </summary>
        private readonly Dictionary<Type, PanelHandler> mOpenedCache = new();

        /// <summary>
        /// 预加载面板缓存（独立管理，LRU 淘汰）
        /// </summary>
        private readonly Dictionary<Type, PanelCacheData> mPreloadedCache = new();

        /// <summary>
        /// 热度衰减计时器
        /// </summary>
        private float mWeakenTimer;

        /// <summary>
        /// 热度衰减间隔（秒）
        /// </summary>
        private const float WEAKEN_INTERVAL = 1f;

        #endregion

        #region 缓存配置

        public int CacheCapacity
        {
            get => mConfig.CacheCapacity;
            set => mConfig.CacheCapacity = Math.Max(1, value);
        }

        public int OpenHot
        {
            get => mConfig.OpenHot;
            set => mConfig.OpenHot = value;
        }

        public int GetHot
        {
            get => mConfig.GetHot;
            set => mConfig.GetHot = value;
        }

        public int Weaken
        {
            get => mConfig.Weaken;
            set => mConfig.Weaken = value;
        }

        #endregion

        #region 缓存查询

        /// <summary>
        /// 尝试获取已缓存的 Handler（已打开或预加载）
        /// </summary>
        internal bool TryGetCachedHandler(Type type, out PanelHandler handler)
        {
            // 优先从已打开缓存查找
            if (mOpenedCache.TryGetValue(type, out handler))
            {
                return true;
            }

            // 从预加载缓存查找并转正
            if (mPreloadedCache.TryGetValue(type, out var preloadData))
            {
                handler = preloadData.Handler;
                // 预加载 -> 已打开（状态迁移）
                mPreloadedCache.Remove(type);
                mOpenedCache[type] = handler;
                return true;
            }

            handler = null;
            return false;
        }

        public bool IsPanelCached(Type panelType)
        {
            return mOpenedCache.ContainsKey(panelType) || mPreloadedCache.ContainsKey(panelType);
        }

        public bool IsPanelCached<T>() where T : UIPanel => IsPanelCached(typeof(T));

        public bool IsPanelPreloaded(Type panelType) => mPreloadedCache.ContainsKey(panelType);
        public bool IsPanelPreloaded<T>() where T : UIPanel => IsPanelPreloaded(typeof(T));

        public IReadOnlyCollection<Type> GetCachedPanelTypes()
        {
            var result = new List<Type>(mOpenedCache.Count + mPreloadedCache.Count);
            result.AddRange(mOpenedCache.Keys);
            result.AddRange(mPreloadedCache.Keys);
            return result;
        }

        public IReadOnlyList<IPanel> GetCachedPanels()
        {
            var result = new List<IPanel>(mOpenedCache.Count + mPreloadedCache.Count);
            foreach (var handler in mOpenedCache.Values)
            {
                if (handler != default && handler.Panel != default)
                {
                    result.Add(handler.Panel);
                }
            }
            foreach (var data in mPreloadedCache.Values)
            {
                if (data.Handler != default && data.Handler.Panel != default)
                {
                    result.Add(data.Handler.Panel);
                }
            }
            return result;
        }

        #endregion

        #region 缓存操作

        /// <summary>
        /// 添加到已打开缓存
        /// </summary>
        internal void AddToOpenedCache(Type type, PanelHandler handler)
        {
            if (mOpenedCache.ContainsKey(type)) return;
            mOpenedCache[type] = handler;
        }

        /// <summary>
        /// 从已打开缓存移除
        /// </summary>
        internal bool RemoveFromOpenedCache(Type type) => mOpenedCache.Remove(type);

        /// <summary>
        /// 定时衰减热度（在 Update 中调用）
        /// </summary>
        internal void UpdateHotWeaken(float deltaTime)
        {
            mWeakenTimer += deltaTime;
            if (mWeakenTimer < WEAKEN_INTERVAL) return;
            mWeakenTimer = 0f;
            WeakenAllHot();
        }

        /// <summary>
        /// 衰减所有面板热度
        /// </summary>
        internal void WeakenAllHot()
        {
            Pool.List<Type>(toRemove =>
            {
                foreach (var kvp in mOpenedCache)
                {
                    var handler = kvp.Value;
                    if (handler == default) continue;

                    handler.Hot -= Weaken;

                    // 热度归零且已关闭的面板，根据 CacheMode 决策
                    if (handler.Hot <= 0 &&
                        handler.Panel != default &&
                        handler.Panel.State is PanelState.Close)
                    {
                        // Persistent 模式不淘汰
                        if (handler.CacheMode == PanelCacheMode.Persistent) continue;
                        toRemove.Add(kvp.Key);
                    }
                }

                for (int i = 0; i < toRemove.Count; i++)
                {
                    var type = toRemove[i];
                    if (mOpenedCache.TryGetValue(type, out var handler))
                    {
                        DestroyPanelInternal(handler.Panel);
                        handler.Recycle();
                        mOpenedCache.Remove(type);
                    }
                }
            });
        }

        /// <summary>
        /// 根据 CacheMode 处理面板关闭
        /// </summary>
        internal bool ShouldDestroyOnClose(PanelHandler handler)
        {
            if (handler == default) return true;

            return handler.CacheMode switch
            {
                PanelCacheMode.Temporary => true,  // 立即销毁
                PanelCacheMode.Persistent => false, // 永不销毁
                PanelCacheMode.Hot => handler.Hot <= 0, // 热度决定
                _ => handler.Hot <= 0
            };
        }

        #endregion

        #region 预加载

        public void PreloadPanelAsync<T>(UILevel level = UILevel.Common, Action<bool> onComplete = null)
            where T : UIPanel
        {
            PreloadPanelAsync(typeof(T), level, onComplete);
        }

        /// <summary>
        /// 预加载面板（异步）
        /// </summary>
        /// <param name="panelType">面板类型</param>
        /// <param name="level">UI 层级</param>
        /// <param name="onComplete">完成回调</param>
        public void PreloadPanelAsync(Type panelType, UILevel level, Action<bool> onComplete)
        {
            if (panelType == default)
            {
                onComplete?.Invoke(false);
                return;
            }

            // 已在已打开缓存
            if (mOpenedCache.ContainsKey(panelType))
            {
                onComplete?.Invoke(true);
                return;
            }

            // 已在预加载缓存
            if (mPreloadedCache.TryGetValue(panelType, out var existingData))
            {
                existingData.AccessTimestamp = DateTime.UtcNow.Ticks;
                onComplete?.Invoke(true);
                return;
            }

            TryEvictPreloaded();

            var handler = PanelHandler.Allocate();
            handler.Type = panelType;
            handler.Level = level;
            handler.CacheMode = PanelCacheMode.Hot;

            LoadPanelAsync(handler, panel =>
            {
                onComplete?.Invoke(SetupPreloadedPanel(panelType, handler, panel));
            });
        }

        /// <summary>
        /// 预加载面板的结果处理（回调版和 UniTask 版共用）
        /// </summary>
        private bool SetupPreloadedPanel(Type panelType, PanelHandler handler, IPanel panel)
        {
            if (panel != default && panel.Transform != default)
            {
                panel.Transform.gameObject.name = panelType.Name;
                panel.Transform.gameObject.SetActive(false);
                panel.Init(null);
                AddToPreloadedCache(panelType, handler);
#if YOKIFRAME_ZSTRING_SUPPORT
                using (var sb = Cysharp.Text.ZString.CreateStringBuilder())
                {
                    sb.Append("[UIRoot] 预加载面板成功: ");
                    sb.Append(panelType.Name);
                    KitLogger.Log(sb.ToString());
                }
#else
                KitLogger.Log("[UIRoot] 预加载面板成功: " + panelType.Name);
#endif
                return true;
            }

            handler.Recycle();
#if YOKIFRAME_ZSTRING_SUPPORT
            using (var sb = Cysharp.Text.ZString.CreateStringBuilder())
            {
                sb.Append("[UIRoot] 预加载面板失败: ");
                sb.Append(panelType.Name);
                KitLogger.Warning(sb.ToString());
            }
#else
            KitLogger.Warning("[UIRoot] 预加载面板失败: " + panelType.Name);
#endif
            return false;
        }

        private void AddToPreloadedCache(Type type, PanelHandler handler)
        {
            var data = new PanelCacheData
            {
                Handler = handler,
                AccessTimestamp = DateTime.UtcNow.Ticks,
                IsPreloaded = true
            };
            mPreloadedCache[type] = data;
        }

        private void TryEvictPreloaded()
        {
            while (mPreloadedCache.Count >= CacheCapacity)
            {
                if (!EvictLeastRecentlyUsed()) break;
            }
        }

        private bool EvictLeastRecentlyUsed()
        {
            Type lruType = null;
            long oldestAccess = long.MaxValue;

            foreach (var kvp in mPreloadedCache)
            {
                if (kvp.Value.AccessTimestamp < oldestAccess)
                {
                    oldestAccess = kvp.Value.AccessTimestamp;
                    lruType = kvp.Key;
                }
            }

            if (lruType == default) return false;

#if YOKIFRAME_ZSTRING_SUPPORT
            using (var sb = Cysharp.Text.ZString.CreateStringBuilder())
            {
                sb.Append("[UIRoot] LRU 淘汰预加载面板: ");
                sb.Append(lruType.Name);
                KitLogger.Log(sb.ToString());
            }
#else
            KitLogger.Log("[UIRoot] LRU 淘汰预加载面板: " + lruType.Name);
#endif
            ClearPreloadedPanel(lruType);
            return true;
        }

        public void ClearPreloadedPanel<T>() where T : UIPanel => ClearPreloadedPanel(typeof(T));

        public void ClearPreloadedPanel(Type panelType)
        {
            if (!mPreloadedCache.TryGetValue(panelType, out var data)) return;

            if (data.Handler != default && data.Handler.Panel != default &&
                data.Handler.Panel.Transform != default)
            {
                UnityEngine.Object.Destroy(data.Handler.Panel.Transform.gameObject);
            }
            if (data.Handler != default) data.Handler.Recycle();
            mPreloadedCache.Remove(panelType);
        }

        public void ClearAllPreloadedPanels()
        {
            Pool.List<Type>(toRemove =>
            {
                foreach (var type in mPreloadedCache.Keys)
                {
                    toRemove.Add(type);
                }
                for (int i = 0; i < toRemove.Count; i++)
                {
                    ClearPreloadedPanel(toRemove[i]);
                }
            });
        }

        #endregion


#if YOKIFRAME_UNITASK_SUPPORT
        public async UniTask<bool> PreloadPanelUniTaskAsync<T>(UILevel level = UILevel.Common,
            CancellationToken ct = default) where T : UIPanel
        {
            return await PreloadPanelUniTaskAsync(typeof(T), level, ct);
        }

        public async UniTask<bool> PreloadPanelUniTaskAsync(Type panelType, UILevel level,
            CancellationToken ct)
        {
            if (panelType == default) return false;

            if (mOpenedCache.ContainsKey(panelType)) return true;

            if (mPreloadedCache.TryGetValue(panelType, out var existingData))
            {
                existingData.AccessTimestamp = DateTime.UtcNow.Ticks;
                return true;
            }

            TryEvictPreloaded();

            var handler = PanelHandler.Allocate();
            handler.Type = panelType;
            handler.Level = level;
            handler.CacheMode = PanelCacheMode.Hot;

            var panel = await LoadPanelUniTaskAsync(handler, ct);
            return SetupPreloadedPanel(panelType, handler, panel);
        }
#endif
    }
}
