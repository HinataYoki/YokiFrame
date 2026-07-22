using System;
using System.Collections.Generic;
using System.Threading;

namespace YokiFrame
{
    /// <summary>提供引擎无关的资源加载、缓存、租约与诊断状态入口。</summary>
    public static partial class ResKit
    {
#if UNITY_EDITOR || (GODOT && TOOLS)
        /// <summary>卸载历史固定容量；超过容量后覆盖最旧记录。</summary>
        public const int MAX_UNLOAD_HISTORY = 100;
#endif

#if UNITY_EDITOR || (GODOT && TOOLS)
        private const string NO_PROVIDER_NAME = "None";
#endif
        private static readonly object sLock = new();
        private static readonly Dictionary<ResCacheKey, ResCacheEntry> sCache = new();
        private static readonly Dictionary<ResCacheKey, ResPendingLoad> sPendingLoads = new();
#if UNITY_EDITOR || (GODOT && TOOLS)
        private static readonly ResUnloadHistory sUnloadHistory = new(MAX_UNLOAD_HISTORY);
#endif
        private static long sCacheEpoch;
#if UNITY_EDITOR || (GODOT && TOOLS)
        private static long sDiagnosticVersion;
        private static bool sEnableLoadLocationTracking;
        private static Exception sLastBackgroundFailure;
#endif

#if UNITY_EDITOR || (GODOT && TOOLS)
        /// <summary>获取是否记录每次 lease 的调用位置；默认关闭以避免堆栈采集成本。</summary>
        public static bool EnableLoadLocationTracking
        {
            get
            {
                lock (sLock)
                {
                    return sEnableLoadLocationTracking;
                }
            }
            set
            {
                lock (sLock)
                {
                    if (sEnableLoadLocationTracking == value)
                    {
                        return;
                    }

                    sEnableLoadLocationTracking = value;
                    BumpDiagnosticVersionLocked();
                }
            }
        }

        /// <summary>获取当前 ResKit 诊断状态的单调递增版本。</summary>
        public static long DiagnosticVersion => Interlocked.Read(ref sDiagnosticVersion);
#endif

#if UNITY_EDITOR || (GODOT && TOOLS)
        /// <summary>获取当前缓存中的已完成资源条目数量。</summary>
        public static int LoadedCount
        {
            get
            {
                lock (sLock)
                {
                    return sCache.Count;
                }
            }
        }

        /// <summary>获取当前仍在执行的同步或异步底层加载数量。</summary>
        public static int InFlightCount
        {
            get
            {
                lock (sLock)
                {
                    return sPendingLoads.Count;
                }
            }
        }

        /// <summary>获取全部活动 lease 的总引用数。</summary>
        public static int TotalRefCount
        {
            get
            {
                lock (sLock)
                {
                    var total = 0;
                    foreach (ResCacheEntry entry in sCache.Values)
                    {
                        total += entry.RefCount;
                    }

                    return total;
                }
            }
        }

        /// <summary>获取固定历史环中当前保留的卸载记录数。</summary>
        public static int UnloadHistoryCount
        {
            get
            {
                lock (sLock)
                {
                    return sUnloadHistory.Count;
                }
            }
        }

        /// <summary>获取当前 Provider 代次，供诊断快照识别后端切换。</summary>
        internal static long ProviderGeneration
        {
            get
            {
                lock (sLock)
                {
                    return sProviderGeneration;
                }
            }
        }

        /// <summary>获取当前缓存代次，供内部拒绝 ClearAll 之前的加载结果。</summary>
        internal static long CacheEpoch
        {
            get
            {
                lock (sLock)
                {
                    return sCacheEpoch;
                }
            }
        }
#endif

        /// <summary>撤销全部缓存和在途加载，并由各条目自己的 Provider 尝试释放资源。</summary>
        /// <exception cref="AggregateException">一个或多个取消回调或 Provider 释放失败时抛出。</exception>
        public static void ClearAll()
        {
            DetachedState detached;
            lock (sLock)
            {
                sCacheEpoch++;
                detached = DetachStateLocked("ResKit cache was cleared before the load completed.");
            }

            try
            {
                ExecuteDetachedCleanup(detached);
            }
            catch (AggregateException exception)
            {
                RecordBackgroundFailure(exception);
                throw;
            }
        }

        /// <summary>撤销上一宿主会话的静态资源状态，但保留宿主已注册的默认 Provider 工厂。</summary>
        /// <remarks>该入口不会把旧会话清理异常抛入引擎启动钩子，异常会进入诊断状态。</remarks>
        internal static void ResetRuntimeDefaults()
        {
            ResetRuntimeState(false);
        }

        /// <summary>为测试撤销全部静态状态，包括宿主默认 Provider 工厂。</summary>
        internal static void ResetForTests()
        {
            ResetRuntimeState(true);
        }

        /// <summary>按调用场景重置 Provider、缓存和诊断状态，并在锁外完成旧资源清理。</summary>
        /// <param name="clearDefaultProviderFactory">是否同时清除宿主默认 Provider 工厂。</param>
        private static void ResetRuntimeState(bool clearDefaultProviderFactory)
        {
            DetachedState detached = ResetStaticState(clearDefaultProviderFactory);
            try
            {
                ExecuteDetachedCleanup(detached);
            }
            catch (AggregateException exception)
            {
                RecordBackgroundFailure(exception);
            }
        }

        /// <summary>在状态锁内重置 Provider、缓存、历史和跟踪开关，并返回锁外清理批次。</summary>
        /// <param name="clearDefaultProviderFactory">是否同时清除宿主默认 Provider 工厂。</param>
        private static DetachedState ResetStaticState(bool clearDefaultProviderFactory)
        {
            lock (sLock)
            {
                sProvider = null;
                if (clearDefaultProviderFactory)
                {
                    sDefaultProviderFactory = null;
                }
#if UNITY_EDITOR || (GODOT && TOOLS)
                sProviderName = NO_PROVIDER_NAME;
#endif
                sSupportsRawBytes = false;
                sSupportsRawText = false;
                sProviderGeneration++;
                sCacheEpoch++;
                DetachedState detached = DetachStateLocked(
                    "ResKit runtime defaults were reset before the load completed.");
#if UNITY_EDITOR || (GODOT && TOOLS)
                sUnloadHistory.Clear();
                sEnableLoadLocationTracking = false;
                sLastBackgroundFailure = null;
                BumpDiagnosticVersionLocked();
#endif
                return detached;
            }
        }

        /// <summary>在状态锁内分离全部条目和在途加载，使后续调用无法观察旧状态。</summary>
        private static DetachedState DetachStateLocked(string staleReason)
        {
            DetachedState detached = new(sCache.Count, sPendingLoads.Count);
            foreach (ResCacheEntry entry in sCache.Values)
            {
#if UNITY_EDITOR || (GODOT && TOOLS)
                AddUnloadRecordLocked(entry);
#endif
                detached.Releases.Add(new ResReleaseWork(entry));
                entry.Invalidate();
            }

            foreach (ResPendingLoad pending in sPendingLoads.Values)
            {
                pending.StaleReason = staleReason;
                detached.PendingLoads.Add(pending);
            }

            sCache.Clear();
            sPendingLoads.Clear();
#if UNITY_EDITOR || (GODOT && TOOLS)
            BumpDiagnosticVersionLocked();
#endif
            return detached;
        }

        /// <summary>在状态锁外取消在途加载并 best-effort 释放全部已分离资源。</summary>
        private static void ExecuteDetachedCleanup(DetachedState detached)
        {
            List<Exception> errors = null;
            for (var index = 0; index < detached.PendingLoads.Count; index++)
            {
                InvalidatePendingLoad(detached.PendingLoads[index], ref errors);
            }

            for (var index = 0; index < detached.Releases.Count; index++)
            {
                ReleaseWork(detached.Releases[index], ref errors);
            }

            if (errors != null)
            {
                throw new AggregateException("One or more ResKit cleanup operations failed.", errors);
            }
        }

        /// <summary>立即结束旧等待者，再 best-effort 取消仍在执行的异步 Provider 调用。</summary>
        private static void InvalidatePendingLoad(ResPendingLoad pending, ref List<Exception> errors)
        {
            pending.SignalStale();
            if (pending.IsSynchronous)
            {
                return;
            }

            try
            {
                pending.CancelLoad();
            }
            catch (Exception exception)
            {
                errors ??= new List<Exception>();
                errors.Add(exception);
            }
        }

        /// <summary>调用创建资源的 Provider 释放底层对象，并收集异常以继续后续清理。</summary>
        private static void ReleaseWork(ResReleaseWork work, ref List<Exception> errors)
        {
            if (!work.IsValid)
            {
                return;
            }

            try
            {
                work.Provider.Release(work.Asset);
            }
            catch (Exception exception)
            {
                errors ??= new List<Exception>();
                errors.Add(exception);
            }
        }

        /// <summary>在无同步调用方可接收异常时报告后台清理失败；Editor 同时保留诊断证据。</summary>
        private static void RecordBackgroundFailure(Exception exception)
        {
#if UNITY_EDITOR || (GODOT && TOOLS)
            lock (sLock)
            {
                sLastBackgroundFailure = exception;
                BumpDiagnosticVersionLocked();
            }
#else
            LogKit.Exception(exception);
#endif
        }

#if UNITY_EDITOR || (GODOT && TOOLS)
        /// <summary>递增诊断版本；调用方必须已经持有状态锁。</summary>
        private static void BumpDiagnosticVersionLocked()
        {
            Interlocked.Increment(ref sDiagnosticVersion);
        }
#endif

        /// <summary>承载已从共享状态原子分离的清理工作。</summary>
        private sealed class DetachedState
        {
            /// <summary>按已知容量创建清理批次，避免扩容。</summary>
            internal DetachedState(int releaseCapacity, int pendingCapacity)
            {
                Releases = new List<ResReleaseWork>(releaseCapacity);
                PendingLoads = new List<ResPendingLoad>(pendingCapacity);
            }

            internal List<ResReleaseWork> Releases { get; }
            internal List<ResPendingLoad> PendingLoads { get; }
        }
    }
}
