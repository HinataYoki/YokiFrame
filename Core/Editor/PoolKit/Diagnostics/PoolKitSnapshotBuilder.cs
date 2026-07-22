#if UNITY_EDITOR || (GODOT && TOOLS)
using System;
using System.Collections.Generic;
using System.Text;

namespace YokiFrame
{
    /// <summary>从 PoolDebugger 复制适合 Shared Memory 的有界、无对象引用快照。</summary>
    internal static class PoolKitSnapshotBuilder
    {
        internal const int MAX_POOLS = 24;
        internal const int MAX_OBJECTS_PER_KIND = 8;
        internal const int MAX_OBJECTS_TOTAL = 32;
        internal const int MAX_EVENTS = 24;
        internal const int MAX_LEAKS = 24;
        /// <summary>创建当前 PoolKit Workbench 状态；调用频率由诊断版本变化控制。</summary>
        internal static PoolKitWorkbenchSnapshot Create()
        {
            List<PoolDebugInfo> sourcePools = new(MAX_POOLS);
            List<PoolEvent> sourceEvents = new(MAX_EVENTS);
            PoolDebugger.GetAllPools(sourcePools, MAX_OBJECTS_PER_KIND, MAX_OBJECTS_PER_KIND);
            PoolDebugger.GetEventHistory(sourceEvents);
            sourcePools.Sort(ComparePools);
            PoolKitStatsSnapshot stats = CreateStats(sourcePools);
            PoolKitPoolSnapshot[] pools = CreatePools(sourcePools);
            PoolKitEventSnapshot[] events = CreateEvents(sourceEvents);
            PoolKitLeakSnapshot[] leaks = CreateLeaks(sourcePools, out int suspectedLeakTotal);
            return new PoolKitWorkbenchSnapshot(
                PoolDebugger.DiagnosticVersion,
                stats,
                pools,
                events,
                leaks,
                suspectedLeakTotal,
                sourcePools.Count > pools.Length,
                sourceEvents.Count > events.Length,
                suspectedLeakTotal > leaks.Length);
        }

        /// <summary>把可能包含长文本的值裁剪到固定 UTF-8 字节预算。</summary>
        internal static string NormalizeText(string value, int maxUtf8Bytes)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            if (Encoding.UTF8.GetByteCount(value) <= maxUtf8Bytes) return value;
            var length = value.Length;
            while (length > 0 && Encoding.UTF8.GetByteCount(value, 0, length) > maxUtf8Bytes)
            {
                length--;
                if (length > 0 && char.IsHighSurrogate(value[length - 1])) length--;
            }

            return value.Substring(0, length);
        }

        /// <summary>安全读取对象显示名；诊断不能让业务 ToString 异常中断宿主发布。</summary>
        internal static string NormalizeObjectName(object value)
        {
            try
            {
                return NormalizeText(value != null ? value.ToString() : "null", 96);
            }
            catch
            {
                return value != null ? value.GetType().FullName : "null";
            }
        }

        /// <summary>聚合全部已登记对象池统计，不因明细裁剪丢失总量。</summary>
        private static PoolKitStatsSnapshot CreateStats(IReadOnlyList<PoolDebugInfo> pools)
        {
            int totalCount = 0;
            int totalActive = 0;
            int totalInactive = 0;
            int totalPeak = 0;
            for (var index = 0; index < pools.Count; index++)
            {
                totalCount += pools[index].TotalCount;
                totalActive += pools[index].ActiveCount;
                totalInactive += pools[index].InactiveCount;
                totalPeak += pools[index].PeakCount;
            }

            return new PoolKitStatsSnapshot(
                pools.Count, totalCount, totalActive, totalInactive, totalPeak,
                PoolDebugger.EnableTracking, PoolDebugger.EnableStackTrace,
                PoolDebugger.EnableEventHistory, PoolDebugger.EventHistoryCount);
        }

        /// <summary>复制有界对象池和全局对象明细预算。</summary>
        private static PoolKitPoolSnapshot[] CreatePools(IReadOnlyList<PoolDebugInfo> source)
        {
            int count = Math.Min(source.Count, MAX_POOLS);
            PoolKitPoolSnapshot[] result = new PoolKitPoolSnapshot[count];
            int remainingObjects = MAX_OBJECTS_TOTAL;
            for (var index = 0; index < count; index++)
            {
                PoolDebugInfo pool = source[index];
                PoolKitObjectSnapshot[] active = CreateActiveObjects(pool.ActiveObjects, ref remainingObjects);
                PoolKitObjectSnapshot[] inactive = CreateInactiveObjects(pool.InactiveObjects, ref remainingObjects);
                result[index] = new PoolKitPoolSnapshot(pool, active, inactive);
            }

            return result;
        }

        /// <summary>复制有界借出对象，并扣减全局 payload 预算。</summary>
        private static PoolKitObjectSnapshot[] CreateActiveObjects(
            IReadOnlyList<ActiveObjectInfo> source,
            ref int remainingObjects)
        {
            int count = Math.Min(Math.Min(source.Count, MAX_OBJECTS_PER_KIND), remainingObjects);
            PoolKitObjectSnapshot[] result = new PoolKitObjectSnapshot[count];
            for (var index = 0; index < count; index++)
            {
                ActiveObjectInfo item = source[index];
                result[index] = new PoolKitObjectSnapshot(
                    NormalizeObjectName(item.Obj),
                    item.SpawnTime,
                    NormalizeText(item.SourceFile, 180),
                    item.SourceLine);
            }

            remainingObjects -= count;
            return result;
        }

        /// <summary>复制有界池内对象，并扣减全局 payload 预算。</summary>
        private static PoolKitObjectSnapshot[] CreateInactiveObjects(
            IReadOnlyList<InactiveObjectInfo> source,
            ref int remainingObjects)
        {
            int count = Math.Min(Math.Min(source.Count, MAX_OBJECTS_PER_KIND), remainingObjects);
            PoolKitObjectSnapshot[] result = new PoolKitObjectSnapshot[count];
            for (var index = 0; index < count; index++)
            {
                result[index] = new PoolKitObjectSnapshot(
                    NormalizeObjectName(source[index].Obj), 0f, string.Empty, 0);
            }

            remainingObjects -= count;
            return result;
        }

        /// <summary>复制最新优先的有界事件流。</summary>
        private static PoolKitEventSnapshot[] CreateEvents(IReadOnlyList<PoolEvent> source)
        {
            int count = Math.Min(source.Count, MAX_EVENTS);
            PoolKitEventSnapshot[] result = new PoolKitEventSnapshot[count];
            for (var index = 0; index < count; index++) result[index] = new PoolKitEventSnapshot(source[index]);
            return result;
        }

        /// <summary>把全部仍有借出对象的池投影为有界疑似泄漏摘要，避免可见池预算掩盖后续候选。</summary>
        private static PoolKitLeakSnapshot[] CreateLeaks(IReadOnlyList<PoolDebugInfo> pools, out int total)
        {
            total = 0;
            for (var index = 0; index < pools.Count; index++)
            {
                if (pools[index].ActiveCount > 0)
                {
                    total++;
                }
            }

            int count = Math.Min(total, MAX_LEAKS);
            PoolKitLeakSnapshot[] result = new PoolKitLeakSnapshot[count];
            var resultIndex = 0;
            for (var index = 0; index < pools.Count && resultIndex < count; index++)
            {
                if (pools[index].ActiveCount > 0) result[resultIndex++] = new PoolKitLeakSnapshot(pools[index]);
            }

            return result;
        }

        /// <summary>按名称和稳定池标识排序，使截断前的对象池与泄漏候选顺序可重复。</summary>
        private static int ComparePools(PoolDebugInfo left, PoolDebugInfo right)
        {
            int byName = string.Compare(left.Name, right.Name, StringComparison.Ordinal);
            return byName != 0 ? byName : string.Compare(left.PoolId, right.PoolId, StringComparison.Ordinal);
        }
    }
}
#endif
