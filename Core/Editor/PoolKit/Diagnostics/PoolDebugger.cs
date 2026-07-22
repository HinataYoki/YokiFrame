#if UNITY_EDITOR || (GODOT && TOOLS)
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading;

namespace YokiFrame
{
    /// <summary>
    /// PoolKit 运行时诊断注册表；默认只登记对象池，详细跟踪需显式开启。
    /// </summary>
    public static partial class PoolDebugger
    {
        /// <summary>
        /// 事件历史最大保留条数。
        /// </summary>
        public const int MAX_EVENT_HISTORY = 200;

        private static readonly object sLock = new();
        private static readonly Dictionary<object, PoolDebugInfo> sPools = new(PoolObjectReferenceComparer.Instance);
        private static readonly Dictionary<object, object> sObjectToPool = new(PoolObjectReferenceComparer.Instance);
        private static readonly Queue<PoolEvent> sEventHistory = new(MAX_EVENT_HISTORY);
        private static readonly List<object> sInactiveObjectBuffer = new();
        private static readonly long sStartTimestamp = Stopwatch.GetTimestamp();
        private static long sDiagnosticVersion;
        private static long sNextPoolId;
        private static bool sEnableTracking;
        private static bool sEnableStackTrace;
        private static bool sEnableEventHistory;

        /// <summary>
        /// 初始化 Editor/Tools 诊断桥接，并补齐调试器晚于 Runtime 对象池创建时的登记状态。
        /// </summary>
        static PoolDebugger()
        {
            PoolEditorHook.PoolRegistered += OnPoolRegistered;
            PoolEditorHook.PoolUnregistered += OnPoolUnregistered;
            PoolEditorHook.PoolAllocated += TrackAllocate;
            PoolEditorHook.PoolRecycled += TrackRecycle;
            PoolEditorHook.PoolCountsUpdated += UpdatePoolCounts;
            SynchronizeRegisteredPools();
        }

        /// <summary>
        /// 获取或设置是否记录活跃对象和非活跃对象。
        /// </summary>
        public static bool EnableTracking
        {
            get { return sEnableTracking; }
            set
            {
                Configure(
                    value,
                    value && sEnableEventHistory,
                    value && sEnableStackTrace);
            }
        }

        /// <summary>
        /// 获取或设置是否记录借出或归还调用堆栈；该开关成本最高。
        /// </summary>
        public static bool EnableStackTrace
        {
            get { return sEnableStackTrace; }
            set { Configure(value || sEnableTracking, value || sEnableEventHistory, value); }
        }

        /// <summary>
        /// 获取或设置是否记录对象池事件历史。
        /// </summary>
        public static bool EnableEventHistory
        {
            get { return sEnableEventHistory; }
            set { Configure(value || sEnableTracking, value, value && sEnableStackTrace); }
        }

        /// <summary>
        /// 获取诊断版本号；对象池登记、跟踪和历史变化时递增。
        /// </summary>
        public static long DiagnosticVersion
        {
            get { return Interlocked.Read(ref sDiagnosticVersion); }
        }

        /// <summary>
        /// 获取当前登记的对象池数量。
        /// </summary>
        public static int PoolCount
        {
            get
            {
                lock (sLock)
                {
                    return sPools.Count;
                }
            }
        }

        /// <summary>
        /// 获取当前事件历史数量。
        /// </summary>
        public static int EventHistoryCount
        {
            get
            {
                lock (sLock)
                {
                    return sEventHistory.Count;
                }
            }
        }

        /// <summary>
        /// 接收 Runtime 桥接的对象池登记并分配当前会话稳定标识。
        /// </summary>
        /// <param name="registration">Runtime 提供的对象池登记信息。</param>
        private static void OnPoolRegistered(PoolEditorRegistration registration)
        {
            if (registration.Pool == null)
            {
                return;
            }

            lock (sLock)
            {
                if (sPools.TryGetValue(registration.Pool, out PoolDebugInfo existing))
                {
                    existing.Name = string.IsNullOrEmpty(registration.Name)
                        ? registration.Pool.GetType().Name
                        : registration.Name;
                    existing.MaxCacheCount = registration.MaxCacheCount;
                    ApplyPoolCounts(existing, registration.InactiveCount);
                    return;
                }

                sPools.Add(
                    registration.Pool,
                    CreateInfo(
                        registration.Pool,
                        registration.Name,
                        registration.MaxCacheCount,
                        registration.InactiveCount));
                BumpDiagnosticVersion();
            }
        }

        /// <summary>
        /// 接收 Runtime 桥接的对象池注销并清理关联活跃对象。
        /// </summary>
        /// <param name="pool">对象池实例。</param>
        private static void OnPoolUnregistered(object pool)
        {
            if (pool == null)
            {
                return;
            }

            lock (sLock)
            {
                if (!sPools.TryGetValue(pool, out PoolDebugInfo info))
                {
                    return;
                }

                RemoveActiveMappings(info);
                info.InactiveObjects.Clear();
                info.InactiveObjectTotal = 0;
                sPools.Remove(pool);
                BumpDiagnosticVersion();
            }
        }

        /// <summary>
        /// 清空全部运行时诊断状态。
        /// </summary>
        public static void Clear()
        {
            lock (sLock)
            {
                sPools.Clear();
                sObjectToPool.Clear();
                sEventHistory.Clear();
                BumpDiagnosticVersion();
            }

            PoolEditorHook.ClearRegisteredPools();
        }

        /// <summary>
        /// 清空全部运行时监控状态；保留旧版命名入口。
        /// </summary>
        public static void ClearRuntimeMonitorState()
        {
            Clear();
        }

        /// <summary>
        /// 原子应用三个诊断开关，并修正它们之间的依赖关系。
        /// </summary>
        /// <param name="trackingEnabled">是否记录借出对象。</param>
        /// <param name="eventHistoryEnabled">是否记录事件历史。</param>
        /// <param name="stackTraceEnabled">是否采集调用堆栈。</param>
        internal static void Configure(
            bool trackingEnabled,
            bool eventHistoryEnabled,
            bool stackTraceEnabled)
        {
            if (stackTraceEnabled)
            {
                trackingEnabled = true;
                eventHistoryEnabled = true;
            }

            if (!trackingEnabled)
            {
                eventHistoryEnabled = false;
                stackTraceEnabled = false;
            }
            else if (!eventHistoryEnabled)
            {
                stackTraceEnabled = false;
            }

            lock (sLock)
            {
                if (sEnableTracking == trackingEnabled
                    && sEnableEventHistory == eventHistoryEnabled
                    && sEnableStackTrace == stackTraceEnabled)
                {
                    return;
                }

                if (sEnableTracking && !trackingEnabled)
                {
                    ClearTrackedObjectsLocked();
                }

                sEnableTracking = trackingEnabled;
                sEnableEventHistory = eventHistoryEnabled;
                sEnableStackTrace = stackTraceEnabled;
                PoolEditorHook.SetTrackingEnabled(trackingEnabled);
                BumpDiagnosticVersion();
            }
        }

        /// <summary>
        /// 更新指定池的缓存数量；仅由清空等没有单个对象事件的操作调用。
        /// </summary>
        /// <param name="pool">发生缓存变化的对象池。</param>
        /// <param name="inactiveCount">当前缓存对象数量。</param>
        internal static void UpdatePoolCounts(object pool, int inactiveCount)
        {
            if (pool == null)
            {
                return;
            }

            lock (sLock)
            {
                if (sPools.TryGetValue(pool, out PoolDebugInfo info))
                {
                    ApplyPoolCounts(info, inactiveCount);
                }
            }
        }

        /// <summary>
        /// 从 Runtime 桥接补齐当前仍存活的对象池，支持诊断器在对象池创建之后才初始化。
        /// </summary>
        private static void SynchronizeRegisteredPools()
        {
            var registrations = new List<PoolEditorRegistration>();
            PoolEditorHook.CopyRegisteredPools(registrations);
            for (var index = 0; index < registrations.Count; index++)
            {
                OnPoolRegistered(registrations[index]);
            }
        }

        /// <summary>
        /// 创建对象池诊断信息。
        /// </summary>
        /// <param name="pool">对象池实例。</param>
        /// <param name="name">显示名称。</param>
        /// <param name="maxCacheCount">最大缓存数量。</param>
        /// <param name="inactiveCount">当前缓存对象数量。</param>
        /// <returns>诊断信息。</returns>
        private static PoolDebugInfo CreateInfo(object pool, string name, int maxCacheCount, int inactiveCount)
        {
            int normalizedInactiveCount = inactiveCount > 0 ? inactiveCount : 0;
            return new PoolDebugInfo
            {
                PoolId = "pool-" + Interlocked.Increment(ref sNextPoolId).ToString(CultureInfo.InvariantCulture),
                Name = string.IsNullOrEmpty(name) ? pool.GetType().Name : name,
                TypeName = pool.GetType().Name,
                PoolRef = pool,
                MaxCacheCount = maxCacheCount,
                InactiveObjectTotal = normalizedInactiveCount,
                TotalCount = normalizedInactiveCount,
                PeakCount = normalizedInactiveCount
            };
        }

        /// <summary>
        /// 使用真实缓存数量和已跟踪借出数更新对象池统计与峰值。
        /// </summary>
        /// <param name="info">需要更新的对象池诊断信息。</param>
        /// <param name="inactiveCount">当前缓存对象数量。</param>
        private static void ApplyPoolCounts(PoolDebugInfo info, int inactiveCount)
        {
            int normalizedInactiveCount = inactiveCount > 0 ? inactiveCount : 0;
            int totalCount = normalizedInactiveCount + info.ActiveCount;
            if (info.InactiveObjectTotal == normalizedInactiveCount && info.TotalCount == totalCount)
            {
                return;
            }

            info.InactiveObjectTotal = normalizedInactiveCount;
            info.TotalCount = totalCount;
            if (totalCount > info.PeakCount)
            {
                info.PeakCount = totalCount;
            }

            BumpDiagnosticVersion();
        }

        /// <summary>
        /// 移除对象池全部活跃对象映射。
        /// </summary>
        /// <param name="info">对象池诊断信息。</param>
        private static void RemoveActiveMappings(PoolDebugInfo info)
        {
            for (var index = 0; index < info.ActiveObjects.Count; index++)
            {
                object obj = info.ActiveObjects[index].Obj;
                if (obj != null)
                {
                    sObjectToPool.Remove(obj);
                }
            }
        }

        /// <summary>
        /// 关闭跟踪时移除旧借出映射，避免重新开启后把历史对象误报为当前泄漏。
        /// </summary>
        private static void ClearTrackedObjectsLocked()
        {
            foreach (KeyValuePair<object, PoolDebugInfo> pair in sPools)
            {
                pair.Value.ActiveObjects.Clear();
                pair.Value.ActiveCount = 0;
            }

            sObjectToPool.Clear();
        }

        /// <summary>
        /// 递增诊断版本。
        /// </summary>
        private static void BumpDiagnosticVersion()
        {
            Interlocked.Increment(ref sDiagnosticVersion);
        }
    }
}
#endif
