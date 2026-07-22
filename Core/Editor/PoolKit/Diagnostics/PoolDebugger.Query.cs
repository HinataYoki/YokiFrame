#if UNITY_EDITOR || (GODOT && TOOLS)
using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// PoolDebugger 的查询、快照克隆和历史读取逻辑。
    /// </summary>
    public static partial class PoolDebugger
    {
        /// <summary>
        /// 获取对象池活跃对象数量。
        /// </summary>
        /// <param name="pool">对象池实例。</param>
        /// <returns>当前活跃对象数量。</returns>
        public static int GetActiveCount(object pool)
        {
            if (pool == null)
            {
                return 0;
            }

            lock (sLock)
            {
                return sPools.TryGetValue(pool, out PoolDebugInfo info) ? info.ActiveCount : 0;
            }
        }

        /// <summary>
        /// 复制全部对象池诊断快照。
        /// </summary>
        /// <param name="result">接收快照的列表；方法会先清空。</param>
        public static void GetAllPools(List<PoolDebugInfo> result)
        {
            GetAllPools(result, int.MaxValue, int.MaxValue);
        }

        /// <summary>
        /// 复制全部对象池诊断快照，并限制单池对象明细复制量以控制 Workbench 读取成本。
        /// </summary>
        /// <param name="result">接收快照的列表；方法会先清空。</param>
        /// <param name="maxActiveObjectsPerPool">每个池最多复制的借出对象数量。</param>
        /// <param name="maxInactiveObjectsPerPool">每个池最多复制的缓存对象数量。</param>
        public static void GetAllPools(
            List<PoolDebugInfo> result,
            int maxActiveObjectsPerPool,
            int maxInactiveObjectsPerPool)
        {
            if (result == null)
            {
                return;
            }

            SynchronizeRegisteredPools();
            result.Clear();
            int activeLimit = maxActiveObjectsPerPool > 0 ? maxActiveObjectsPerPool : 0;
            int inactiveLimit = maxInactiveObjectsPerPool > 0 ? maxInactiveObjectsPerPool : 0;
            lock (sLock)
            {
                foreach (KeyValuePair<object, PoolDebugInfo> pair in sPools)
                {
                    RefreshInactiveObjectsLocked(pair.Value, inactiveLimit);
                    result.Add(CloneInfo(pair.Value, activeLimit));
                }
            }
        }

        /// <summary>
        /// 按时间倒序获取事件历史。
        /// </summary>
        /// <param name="result">接收事件的列表；方法会先清空。</param>
        /// <param name="filterType">可选事件类型过滤。</param>
        /// <param name="poolName">可选对象池名称过滤。</param>
        public static void GetEventHistory(List<PoolEvent> result, PoolEventType? filterType = null, string poolName = null)
        {
            if (result == null)
            {
                return;
            }

            result.Clear();
            lock (sLock)
            {
                PoolEvent[] events = sEventHistory.ToArray();
                AddFilteredEvents(result, events, filterType, poolName);
            }
        }

        /// <summary>
        /// 清空事件历史。
        /// </summary>
        public static void ClearEventHistory()
        {
            lock (sLock)
            {
                sEventHistory.Clear();
                BumpDiagnosticVersion();
            }
        }

        /// <summary>
        /// 移除活跃对象记录。
        /// </summary>
        /// <param name="info">对象池诊断信息。</param>
        /// <param name="obj">对象实例。</param>
        private static void RemoveActiveObjectLocked(PoolDebugInfo info, object obj)
        {
            for (var index = info.ActiveObjects.Count - 1; index >= 0; index--)
            {
                if (ReferenceEquals(info.ActiveObjects[index].Obj, obj))
                {
                    info.ActiveObjects.RemoveAt(index);
                    break;
                }
            }

            info.ActiveCount = info.ActiveObjects.Count;
        }

        /// <summary>
        /// 按过滤条件把事件追加到结果列表。
        /// </summary>
        /// <param name="result">结果列表。</param>
        /// <param name="events">事件数组。</param>
        /// <param name="filterType">事件类型过滤。</param>
        /// <param name="poolName">对象池名称过滤。</param>
        private static void AddFilteredEvents(List<PoolEvent> result, PoolEvent[] events, PoolEventType? filterType, string poolName)
        {
            for (var index = events.Length - 1; index >= 0; index--)
            {
                if (ShouldIncludeEvent(events[index], filterType, poolName))
                {
                    result.Add(CloneEvent(events[index]));
                }
            }
        }

        /// <summary>
        /// 检查事件是否满足过滤条件。
        /// </summary>
        /// <param name="item">事件记录。</param>
        /// <param name="filterType">事件类型过滤。</param>
        /// <param name="poolName">对象池名称过滤。</param>
        /// <returns>满足条件时返回 true。</returns>
        private static bool ShouldIncludeEvent(PoolEvent item, PoolEventType? filterType, string poolName)
        {
            if (filterType.HasValue && item.EventType != filterType.Value)
            {
                return false;
            }

            return string.IsNullOrEmpty(poolName) || string.Equals(item.PoolName, poolName, StringComparison.Ordinal);
        }

        /// <summary>
        /// 克隆对象池诊断信息。
        /// </summary>
        /// <param name="info">原始诊断信息。</param>
        /// <returns>诊断信息副本。</returns>
        private static PoolDebugInfo CloneInfo(PoolDebugInfo info, int maxActiveObjects)
        {
            var clone = new PoolDebugInfo
            {
                PoolId = info.PoolId,
                Name = info.Name,
                TypeName = info.TypeName,
                TotalCount = info.TotalCount,
                ActiveCount = info.ActiveCount,
                PeakCount = info.PeakCount,
                MaxCacheCount = info.MaxCacheCount,
                InactiveObjectTotal = info.InactiveObjectTotal,
                PoolRef = info.PoolRef
            };
            CloneObjectLists(info, clone, maxActiveObjects);
            return clone;
        }

        /// <summary>
        /// 克隆活跃和非活跃对象列表。
        /// </summary>
        /// <param name="source">源诊断信息。</param>
        /// <param name="target">目标诊断信息。</param>
        private static void CloneObjectLists(PoolDebugInfo source, PoolDebugInfo target, int maxActiveObjects)
        {
            int activeCount = Math.Min(source.ActiveObjects.Count, maxActiveObjects);
            for (var index = 0; index < activeCount; index++)
            {
                target.ActiveObjects.Add(CloneActiveInfo(source.ActiveObjects[index]));
            }

            for (var index = 0; index < source.InactiveObjects.Count; index++)
            {
                target.InactiveObjects.Add(new InactiveObjectInfo { Obj = source.InactiveObjects[index].Obj });
            }
        }

        /// <summary>
        /// 克隆活跃对象记录。
        /// </summary>
        /// <param name="info">原始记录。</param>
        /// <returns>记录副本。</returns>
        private static ActiveObjectInfo CloneActiveInfo(ActiveObjectInfo info)
        {
            return new ActiveObjectInfo
            {
                Obj = info.Obj,
                SpawnTime = info.SpawnTime,
                StackTrace = info.StackTrace,
                SourceFile = info.SourceFile,
                SourceLine = info.SourceLine
            };
        }

        /// <summary>
        /// 克隆事件记录。
        /// </summary>
        /// <param name="item">原始事件。</param>
        /// <returns>事件副本。</returns>
        private static PoolEvent CloneEvent(PoolEvent item)
        {
            return new PoolEvent
            {
                PoolId = item.PoolId,
                EventType = item.EventType,
                Timestamp = item.Timestamp,
                PoolName = item.PoolName,
                ObjectName = item.ObjectName,
                Source = item.Source,
                SourceFile = item.SourceFile,
                SourceLine = item.SourceLine,
                StackTrace = item.StackTrace,
                ObjRef = item.ObjRef
            };
        }

        /// <summary>
        /// 在明确读取诊断快照时从真实对象池有界复制缓存明细，并同步总量统计。
        /// </summary>
        /// <param name="info">要刷新的对象池诊断信息。</param>
        /// <param name="maxInactiveObjects">最多复制的缓存对象数量。</param>
        private static void RefreshInactiveObjectsLocked(PoolDebugInfo info, int maxInactiveObjects)
        {
            var snapshot = info.PoolRef as IPoolDebugSnapshot;
            if (snapshot == null)
            {
                return;
            }

            sInactiveObjectBuffer.Clear();
            int inactiveCount = snapshot.CopyInactiveObjects(sInactiveObjectBuffer, maxInactiveObjects);
            info.InactiveObjects.Clear();
            for (var index = 0; index < sInactiveObjectBuffer.Count; index++)
            {
                info.InactiveObjects.Add(new InactiveObjectInfo { Obj = sInactiveObjectBuffer[index] });
            }

            ApplyPoolCounts(info, inactiveCount);
        }
    }
}
#endif
