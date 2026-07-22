#if UNITY_EDITOR || (GODOT && TOOLS)
using System;

namespace YokiFrame
{
    /// <summary>保存 PoolKit Workbench 状态的一次有界快照。</summary>
    internal sealed class PoolKitWorkbenchSnapshot
    {
        /// <summary>创建由 builder 完整填充的 PoolKit 状态。</summary>
        internal PoolKitWorkbenchSnapshot(
            long version,
            PoolKitStatsSnapshot stats,
            PoolKitPoolSnapshot[] pools,
            PoolKitEventSnapshot[] events,
            PoolKitLeakSnapshot[] suspectedLeaks,
            int suspectedLeakTotal,
            bool poolsTruncated,
            bool eventsTruncated,
            bool suspectedLeaksTruncated)
        {
            Version = version;
            Stats = stats;
            Pools = pools;
            Events = events;
            SuspectedLeaks = suspectedLeaks;
            SuspectedLeakTotal = suspectedLeakTotal;
            PoolsTruncated = poolsTruncated;
            EventsTruncated = eventsTruncated;
            SuspectedLeaksTruncated = suspectedLeaksTruncated;
        }

        internal long Version { get; }
        internal PoolKitStatsSnapshot Stats { get; }
        internal PoolKitPoolSnapshot[] Pools { get; }
        internal PoolKitEventSnapshot[] Events { get; }
        internal PoolKitLeakSnapshot[] SuspectedLeaks { get; }
        internal int SuspectedLeakTotal { get; }
        internal bool PoolsTruncated { get; }
        internal bool EventsTruncated { get; }
        internal bool SuspectedLeaksTruncated { get; }
    }

    /// <summary>保存全部对象池的聚合统计和诊断开关。</summary>
    internal readonly struct PoolKitStatsSnapshot
    {
        /// <summary>创建聚合统计。</summary>
        internal PoolKitStatsSnapshot(
            int poolCount,
            int totalCount,
            int totalActive,
            int totalInactive,
            int totalPeak,
            bool trackingEnabled,
            bool stackTraceEnabled,
            bool eventHistoryEnabled,
            int eventHistoryCount)
        {
            PoolCount = poolCount;
            TotalCount = totalCount;
            TotalActive = totalActive;
            TotalInactive = totalInactive;
            TotalPeak = totalPeak;
            TrackingEnabled = trackingEnabled;
            StackTraceEnabled = stackTraceEnabled;
            EventHistoryEnabled = eventHistoryEnabled;
            EventHistoryCount = eventHistoryCount;
        }

        internal int PoolCount { get; }
        internal int TotalCount { get; }
        internal int TotalActive { get; }
        internal int TotalInactive { get; }
        internal int TotalPeak { get; }
        internal bool TrackingEnabled { get; }
        internal bool StackTraceEnabled { get; }
        internal bool EventHistoryEnabled { get; }
        internal int EventHistoryCount { get; }
    }

    /// <summary>保存一个对象池的指标和有界对象明细。</summary>
    internal sealed class PoolKitPoolSnapshot
    {
        /// <summary>创建单个对象池快照。</summary>
        internal PoolKitPoolSnapshot(
            PoolDebugInfo source,
            PoolKitObjectSnapshot[] activeObjects,
            PoolKitObjectSnapshot[] inactiveObjects)
        {
            PoolId = PoolKitSnapshotBuilder.NormalizeText(source.PoolId, 48);
            Name = PoolKitSnapshotBuilder.NormalizeText(source.Name, 96);
            TypeName = PoolKitSnapshotBuilder.NormalizeText(source.TypeName, 128);
            TotalCount = source.TotalCount;
            ActiveCount = source.ActiveCount;
            InactiveCount = source.InactiveCount;
            PeakCount = source.PeakCount;
            MaxCacheCount = source.MaxCacheCount;
            HealthStatus = source.HealthStatus.ToString();
            ActiveObjectTotal = source.ActiveCount;
            InactiveObjectTotal = source.InactiveObjectTotal;
            ActiveObjects = activeObjects;
            InactiveObjects = inactiveObjects;
        }

        internal string PoolId { get; }
        internal string Name { get; }
        internal string TypeName { get; }
        internal int TotalCount { get; }
        internal int ActiveCount { get; }
        internal int InactiveCount { get; }
        internal int PeakCount { get; }
        internal int MaxCacheCount { get; }
        internal string HealthStatus { get; }
        internal int ActiveObjectTotal { get; }
        internal int InactiveObjectTotal { get; }
        internal PoolKitObjectSnapshot[] ActiveObjects { get; }
        internal PoolKitObjectSnapshot[] InactiveObjects { get; }
        internal bool ActiveObjectTruncated => ActiveObjectTotal > ActiveObjects.Length;
        internal bool InactiveObjectTruncated => InactiveObjectTotal > InactiveObjects.Length;
        internal double UsageRate => TotalCount > 0 ? (double)ActiveCount / TotalCount : 0d;
    }

    /// <summary>保存一个借出或池内对象的安全显示信息。</summary>
    internal readonly struct PoolKitObjectSnapshot
    {
        /// <summary>创建对象显示快照。</summary>
        internal PoolKitObjectSnapshot(string objectName, float spawnTime, string sourceFile, int sourceLine)
        {
            ObjectName = objectName;
            SpawnTime = spawnTime;
            SourceFile = sourceFile;
            SourceLine = sourceLine;
        }

        internal string ObjectName { get; }
        internal float SpawnTime { get; }
        internal string SourceFile { get; }
        internal int SourceLine { get; }
    }

    /// <summary>保存一条有界事件流记录。</summary>
    internal readonly struct PoolKitEventSnapshot
    {
        /// <summary>创建事件显示快照。</summary>
        internal PoolKitEventSnapshot(PoolEvent source)
        {
            EventType = source.EventType.ToString();
            Timestamp = source.Timestamp;
            PoolId = PoolKitSnapshotBuilder.NormalizeText(source.PoolId, 48);
            PoolName = PoolKitSnapshotBuilder.NormalizeText(source.PoolName, 96);
            ObjectName = PoolKitSnapshotBuilder.NormalizeObjectName(source.ObjectName);
            SourceFile = PoolKitSnapshotBuilder.NormalizeText(source.SourceFile, 180);
            SourceLine = source.SourceLine;
        }

        internal string EventType { get; }
        internal float Timestamp { get; }
        internal string PoolId { get; }
        internal string PoolName { get; }
        internal string ObjectName { get; }
        internal string SourceFile { get; }
        internal int SourceLine { get; }
    }

    /// <summary>保存一个仍有借出对象的疑似泄漏摘要。</summary>
    internal readonly struct PoolKitLeakSnapshot
    {
        /// <summary>创建疑似泄漏摘要。</summary>
        internal PoolKitLeakSnapshot(PoolDebugInfo pool)
        {
            PoolId = PoolKitSnapshotBuilder.NormalizeText(pool.PoolId, 48);
            PoolName = PoolKitSnapshotBuilder.NormalizeText(pool.Name, 96);
            ActiveCount = pool.ActiveCount;
            PeakCount = pool.PeakCount;
        }

        internal string PoolId { get; }
        internal string PoolName { get; }
        internal int ActiveCount { get; }
        internal int PeakCount { get; }
    }
}
#endif
