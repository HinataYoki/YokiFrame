using System;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEngine;
#endif

namespace YokiFrame
{
    /// <summary>
    /// PoolDebugger - 追踪功能（注册、借出、归还）
    /// </summary>
    public static partial class PoolDebugger
    {
#if UNITY_EDITOR
        /// <summary>
        /// 注册池到调试器
        /// </summary>
        /// <param name="pool">池实例</param>
        /// <param name="name">池名称</param>
        /// <param name="maxCacheCount">最大缓存容量（-1 表示无限制）</param>
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
        /// 注销池
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
        /// 记录对象借出
        /// </summary>
        public static void TrackAllocate(object pool, object obj)
        {
            if (pool == default || obj == default || !EnableTracking) return;

            if (!sPools.TryGetValue(pool, out var info)) return;

            // 检查对象是否已经在活跃列表中（避免重复添加）
            var objHash = obj.GetHashCode();
            for (int i = 0; i < info.ActiveObjects.Count; i++)
            {
                var storedObj = info.ActiveObjects[i].Obj;
                if (ReferenceEquals(storedObj, obj) || 
                    (storedObj.GetHashCode() == objHash && Equals(storedObj, obj)))
                {
                    UnityEngine.Debug.LogWarning($"[PoolDebugger] TrackAllocate 检测到重复: Pool={info.Name}, HashCode={objHash}, 已存在于索引 {i}");
                    return; // 已存在，不重复添加
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
        /// 记录对象归还
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
                
                // 优先使用 ReferenceEquals，如果失败则使用 Equals 和 HashCode
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
                UnityEngine.Debug.LogWarning($"[PoolDebugger] TrackRecycle 未找到匹配: Pool={info.Name}, HashCode={objHash}, ActiveCount={info.ActiveObjects.Count}");
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
        /// 更新池的总容量
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
        /// 获取池的活跃对象数量
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
        /// 更新池的最大缓存容量
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
        /// 获取所有池的调试信息
        /// </summary>
        public static void GetAllPools(List<PoolDebugInfo> result)
        {
            result.Clear();
            foreach (var kvp in sPools)
            {
                result.Add(kvp.Value);
            }
        }

        /// <summary>
        /// 强制归还对象
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
                UnityEngine.Debug.LogWarning($"[PoolDebugger] 强制归还失败: {e.Message}");
                return false;
            }
        }
#endif
    }
}
