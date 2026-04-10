using System;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEngine;
#endif

namespace YokiFrame
{
    /// <summary>
    /// Pool registration and borrow/return tracking for <see cref="PoolDebugger"/>.
    /// </summary>
    public static partial class PoolDebugger
    {
#if UNITY_EDITOR
        /// <summary>
        /// Registers a pool instance with the debugger.
        /// </summary>
        /// <param name="pool">Pool instance.</param>
        /// <param name="name">Display name for the pool.</param>
        /// <param name="maxCacheCount">Configured max cache count, or -1 if unlimited.</param>
        public static void RegisterPool(object pool, string name, int maxCacheCount = -1)
        {
            if (pool == default) return;
            if (sPools.ContainsKey(pool)) return;

            var info = new PoolDebugInfo
            {
                Name = name,
                TypeName = pool.GetType().Name,
                PoolRef = pool,
                MaxCacheCount = maxCacheCount
            };
            sPools[pool] = info;

            if (EnableTracking)
            {
                NotifyEditorDataChanged(CHANNEL_POOL_LIST_CHANGED, info);
            }
        }

        /// <summary>
        /// Unregisters a pool instance and removes all tracked active-object mappings for it.
        /// </summary>
        public static void UnregisterPool(object pool)
        {
            if (pool == default) return;

            if (sPools.TryGetValue(pool, out var info))
            {
                foreach (var activeObj in info.ActiveObjects)
                {
                    if (activeObj.Obj != default)
                    {
                        sObjectToPool.Remove(activeObj.Obj);
                    }
                }

                sPools.Remove(pool);
                NotifyEditorDataChanged(CHANNEL_POOL_LIST_CHANGED, info);
            }
        }

        /// <summary>
        /// Records that an object was borrowed from a pool.
        /// </summary>
        public static void TrackAllocate(object pool, object obj)
        {
            if (pool == default || obj == default || !EnableTracking) return;
            if (!sPools.TryGetValue(pool, out var info)) return;

            var objHash = obj.GetHashCode();
            for (int i = 0; i < info.ActiveObjects.Count; i++)
            {
                var storedObj = info.ActiveObjects[i].Obj;
                if (ReferenceEquals(storedObj, obj) ||
                    (storedObj.GetHashCode() == objHash && Equals(storedObj, obj)))
                {
                    UnityEngine.Debug.LogWarning($"[PoolDebugger] TrackAllocate duplicate detected: Pool={info.Name}, HashCode={objHash}, Index={i}");
                    return;
                }
            }

            var stackTrace = EnableStackTrace ? GetCallerStackTrace() : string.Empty;
            var activeInfo = new ActiveObjectInfo
            {
                Obj = obj,
                SpawnTime = Time.realtimeSinceStartup,
                StackTrace = stackTrace
            };

            info.ActiveObjects.Add(activeInfo);
            info.ActiveCount = info.ActiveObjects.Count;
            sObjectToPool[obj] = pool;

            if (info.ActiveCount > info.PeakCount)
            {
                info.PeakCount = info.ActiveCount;
            }

            if (EnableEventHistory)
            {
                RecordEvent(PoolEventType.Spawn, info.Name, obj, stackTrace);
            }

            NotifyEditorDataChanged(CHANNEL_POOL_ACTIVE_CHANGED, info);
        }

        /// <summary>
        /// Records that an object was returned to a pool.
        /// </summary>
        public static void TrackRecycle(object pool, object obj)
        {
            if (pool == default || obj == default || !EnableTracking) return;
            if (!sPools.TryGetValue(pool, out var info)) return;

            var found = false;
            var objHash = obj.GetHashCode();

            for (int i = info.ActiveObjects.Count - 1; i >= 0; i--)
            {
                var storedObj = info.ActiveObjects[i].Obj;
                if (ReferenceEquals(storedObj, obj) ||
                    (storedObj.GetHashCode() == objHash && Equals(storedObj, obj)))
                {
                    info.ActiveObjects.RemoveAt(i);
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                UnityEngine.Debug.LogWarning($"[PoolDebugger] TrackRecycle target not found: Pool={info.Name}, HashCode={objHash}, ActiveCount={info.ActiveObjects.Count}");
            }

            info.ActiveCount = info.ActiveObjects.Count;
            sObjectToPool.Remove(obj);

            if (EnableEventHistory)
            {
                var stackTrace = EnableStackTrace ? Environment.StackTrace : string.Empty;
                RecordEvent(PoolEventType.Return, info.Name, obj, stackTrace);
            }

            NotifyEditorDataChanged(CHANNEL_POOL_ACTIVE_CHANGED, info);
        }

        /// <summary>
        /// Updates the total object count for a pool.
        /// </summary>
        public static void UpdateTotalCount(object pool, int totalCount)
        {
            if (pool == default || !EnableTracking) return;

            if (sPools.TryGetValue(pool, out var info))
            {
                info.TotalCount = totalCount;
            }
        }

        /// <summary>
        /// Returns the number of currently active objects for a pool.
        /// </summary>
        public static int GetActiveCount(object pool)
        {
            if (pool == default || !EnableTracking) return 0;

            if (sPools.TryGetValue(pool, out var info))
            {
                return info.ActiveCount;
            }

            return 0;
        }

        /// <summary>
        /// Returns whether an object is currently known to the debugger as tracked.
        /// </summary>
        public static bool IsObjectTracked(object obj)
        {
            if (obj == default || !EnableTracking) return false;
            return sObjectToPool.ContainsKey(obj);
        }

        /// <summary>
        /// Updates the configured max cache count for a pool.
        /// </summary>
        public static void UpdateMaxCacheCount(object pool, int maxCacheCount)
        {
            if (pool == default || !EnableTracking) return;

            if (sPools.TryGetValue(pool, out var info))
            {
                info.MaxCacheCount = maxCacheCount;
            }
        }

        /// <summary>
        /// Returns debug info for all tracked pools.
        /// </summary>
        /// <param name="result">Target list that will be cleared first.</param>
        public static void GetAllPools(List<PoolDebugInfo> result)
        {
            result.Clear();
            foreach (var kvp in sPools)
            {
                result.Add(kvp.Value);
            }
        }

        /// <summary>
        /// Forces an object back into a pool through reflection.
        /// Intended for Editor debugging workflows.
        /// </summary>
        public static bool ForceReturn(object pool, object obj)
        {
            if (pool == default || obj == default) return false;

            string poolName = "Unknown";
            if (sPools.TryGetValue(pool, out var info))
            {
                poolName = info.Name;
            }

            var poolType = pool.GetType();
            var recycleMethod = poolType.GetMethod("Recycle");
            if (recycleMethod == default) return false;

            try
            {
                recycleMethod.Invoke(pool, new[] { obj });

                if (EnableEventHistory)
                {
                    RecordEvent(PoolEventType.Forced, poolName, obj, "ForceReturn");
                }

                return true;
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning($"[PoolDebugger] ForceReturn failed: {e.Message}");
                return false;
            }
        }
#endif
    }
}
