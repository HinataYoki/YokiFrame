#if UNITY_EDITOR || (GODOT && TOOLS)
namespace YokiFrame
{
    /// <summary>保存一次原子复制的 ResKit 状态，供 JSON writer 和命令查询使用。</summary>
    internal sealed class ResKitDiagnosticSnapshot
    {
        /// <summary>创建完整诊断快照。</summary>
        internal ResKitDiagnosticSnapshot(
            long version,
            ResKitProviderSnapshot provider,
            ResKitStatsSnapshot stats,
            ResDebugInfo[] resources,
            int resourceTotal,
            ResUnloadRecord[] history,
            int historyTotal,
            long historyDroppedCount,
            string lastBackgroundFailure)
        {
            Version = version;
            Provider = provider;
            Stats = stats;
            Resources = resources;
            ResourceTotal = resourceTotal;
            History = history;
            HistoryTotal = historyTotal;
            HistoryDroppedCount = historyDroppedCount;
            LastBackgroundFailure = lastBackgroundFailure;
        }

        internal long Version { get; }
        internal ResKitProviderSnapshot Provider { get; }
        internal ResKitStatsSnapshot Stats { get; }
        internal ResDebugInfo[] Resources { get; }
        internal int ResourceTotal { get; }
        internal ResUnloadRecord[] History { get; }
        internal int HistoryTotal { get; }
        internal long HistoryDroppedCount { get; }
        internal string LastBackgroundFailure { get; }
        internal bool ResourcesTruncated => Resources.Length < ResourceTotal;
        internal bool HistoryTruncated => History.Length < HistoryTotal;
    }

    /// <summary>保存 Provider 身份、代次和可选能力。</summary>
    internal readonly struct ResKitProviderSnapshot
    {
        /// <summary>创建 Provider 诊断状态。</summary>
        internal ResKitProviderSnapshot(
            string name,
            long generation,
            bool supportsRawBytes,
            bool supportsRawText)
        {
            Name = name;
            Generation = generation;
            SupportsRawBytes = supportsRawBytes;
            SupportsRawText = supportsRawText;
        }

        internal string Name { get; }
        internal long Generation { get; }
        internal bool SupportsRawBytes { get; }
        internal bool SupportsRawText { get; }
    }

    /// <summary>保存一次原子读取的 ResKit 聚合计数。</summary>
    internal readonly struct ResKitStatsSnapshot
    {
        /// <summary>创建聚合统计。</summary>
        internal ResKitStatsSnapshot(
            int loadedCount,
            int inFlightCount,
            int totalLeaseCount,
            int unloadHistoryCount,
            bool trackingEnabled)
        {
            LoadedCount = loadedCount;
            InFlightCount = inFlightCount;
            TotalLeaseCount = totalLeaseCount;
            UnloadHistoryCount = unloadHistoryCount;
            TrackingEnabled = trackingEnabled;
        }

        internal int LoadedCount { get; }
        internal int InFlightCount { get; }
        internal int TotalLeaseCount { get; }
        internal int UnloadHistoryCount { get; }
        internal bool TrackingEnabled { get; }
    }
}
#endif
