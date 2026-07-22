#if UNITY_EDITOR || (GODOT && TOOLS)
using System.Diagnostics;

namespace YokiFrame
{
    /// <summary>
    /// PoolDebugger 的对象借出、归还和强制归还跟踪逻辑。
    /// </summary>
    public static partial class PoolDebugger
    {
        /// <summary>
        /// 记录对象被借出。
        /// </summary>
        /// <param name="pool">对象池实例。</param>
        /// <param name="obj">被借出的对象。</param>
        /// <param name="inactiveCount">借出后当前缓存对象数量。</param>
        public static void TrackAllocate(object pool, object obj, int inactiveCount)
        {
            if (!EnableTracking || pool == null || obj == null)
            {
                return;
            }

            lock (sLock)
            {
                if (!sPools.TryGetValue(pool, out PoolDebugInfo info) || sObjectToPool.ContainsKey(obj))
                {
                    return;
                }

                AddActiveObjectLocked(info, pool, obj);
                ApplyPoolCounts(info, inactiveCount);
                BumpDiagnosticVersion();
            }
        }

        /// <summary>
        /// 记录对象被归还。
        /// </summary>
        /// <param name="pool">对象池实例。</param>
        /// <param name="obj">被归还的对象。</param>
        /// <param name="inactiveCount">归还后当前缓存对象数量。</param>
        public static void TrackRecycle(object pool, object obj, int inactiveCount)
        {
            if (!EnableTracking || pool == null || obj == null)
            {
                return;
            }

            lock (sLock)
            {
                if (!sPools.TryGetValue(pool, out PoolDebugInfo info))
                {
                    return;
                }

                if (sObjectToPool.TryGetValue(obj, out object owner) && ReferenceEquals(owner, pool))
                {
                    RemoveActiveObjectLocked(info, obj);
                    sObjectToPool.Remove(obj);
                    RecordReturnEventIfNeeded(info, obj);
                }

                ApplyPoolCounts(info, inactiveCount);
                BumpDiagnosticVersion();
            }
        }

        /// <summary>
        /// 更新对象池最大缓存数量。
        /// </summary>
        /// <param name="pool">对象池实例。</param>
        /// <param name="maxCacheCount">最大缓存数量。</param>
        public static void UpdateMaxCacheCount(object pool, int maxCacheCount)
        {
            if (pool == null)
            {
                return;
            }

            lock (sLock)
            {
                if (!sPools.TryGetValue(pool, out PoolDebugInfo info) || info.MaxCacheCount == maxCacheCount)
                {
                    return;
                }

                info.MaxCacheCount = maxCacheCount;
                BumpDiagnosticVersion();
            }
        }

        /// <summary>
        /// 尝试将对象强制归还到指定对象池。
        /// </summary>
        /// <param name="pool">对象池实例。</param>
        /// <param name="obj">需要归还的对象。</param>
        /// <returns>强制归还成功时返回 true。</returns>
        public static bool ForceReturn(object pool, object obj)
        {
            if (pool == null || obj == null)
            {
                return false;
            }

            var debugReturn = pool as IPoolDebugReturn;
            return debugReturn != null && ForceReturnCore(pool, obj, debugReturn);
        }

        /// <summary>
        /// 检查对象是否仍被诊断表视为已借出。
        /// </summary>
        /// <param name="obj">对象实例。</param>
        /// <returns>仍被跟踪时返回 true。</returns>
        public static bool IsObjectTracked(object obj)
        {
            if (obj == null)
            {
                return false;
            }

            lock (sLock)
            {
                return sObjectToPool.ContainsKey(obj);
            }
        }

        /// <summary>
        /// 添加活跃对象记录。
        /// </summary>
        /// <param name="info">对象池诊断信息。</param>
        /// <param name="pool">对象池实例。</param>
        /// <param name="obj">被借出的对象。</param>
        private static void AddActiveObjectLocked(PoolDebugInfo info, object pool, object obj)
        {
            StackTrace stackTraceObject = EnableStackTrace ? new StackTrace(1, true) : null;
            string stackTrace = stackTraceObject != null ? stackTraceObject.ToString() : string.Empty;
            SourceLocation location = ParseStackTraceLocation(stackTraceObject, stackTrace);
            info.ActiveObjects.Add(CreateActiveInfo(obj, stackTrace, location));
            info.ActiveCount = info.ActiveObjects.Count;
            if (info.ActiveCount > info.PeakCount)
            {
                info.PeakCount = info.ActiveCount;
            }

            sObjectToPool[obj] = pool;
            if (EnableEventHistory)
            {
                RecordEventLocked(PoolEventType.Spawn, info.PoolId, info.Name, obj, stackTrace, location);
            }
        }

        /// <summary>
        /// 创建活跃对象诊断记录。
        /// </summary>
        /// <param name="obj">对象实例。</param>
        /// <param name="stackTrace">调用堆栈。</param>
        /// <param name="location">调用位置。</param>
        /// <returns>活跃对象诊断记录。</returns>
        private static ActiveObjectInfo CreateActiveInfo(object obj, string stackTrace, SourceLocation location)
        {
            return new ActiveObjectInfo
            {
                Obj = obj,
                SpawnTime = GetElapsedSeconds(),
                StackTrace = stackTrace,
                SourceFile = location.FilePath,
                SourceLine = location.Line
            };
        }

        /// <summary>
        /// 执行强制归还，并吞掉诊断路径异常。
        /// </summary>
        /// <param name="pool">对象池实例。</param>
        /// <param name="obj">需要归还的对象。</param>
        /// <param name="debugReturn">强制归还契约。</param>
        /// <returns>强制归还成功时返回 true。</returns>
        private static bool ForceReturnCore(object pool, object obj, IPoolDebugReturn debugReturn)
        {
            try
            {
                if (!debugReturn.TryRecycleObject(obj))
                {
                    return false;
                }

                RecordForcedReturn(pool, obj);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
#endif
