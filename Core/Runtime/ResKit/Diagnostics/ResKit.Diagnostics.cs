#if UNITY_EDITOR || (GODOT && TOOLS)
using System;
using System.Collections.Generic;
using System.Globalization;

namespace YokiFrame
{
    public static partial class ResKit
    {
        /// <summary>填充全部已加载资源的隔离诊断副本；该方法不会把底层资源对象暴露给工具层，每个资源至多返回一条来源摘要。</summary>
        /// <param name="result">接收结果的列表；为空时不执行操作。</param>
        public static void GetLoadedAssets(List<ResDebugInfo> result)
        {
            if (result == null) return;
            ResKitDiagnosticSnapshot snapshot = CaptureDiagnosticSnapshot(int.MaxValue, 0, 1);
            result.Clear();
            result.AddRange(snapshot.Resources);
        }

        /// <summary>按最新优先顺序填充固定历史环的隔离副本。</summary>
        /// <param name="result">接收结果的列表；为空时不执行操作。</param>
        public static void GetUnloadHistory(List<ResUnloadRecord> result)
        {
            if (result == null) return;
            ResKitDiagnosticSnapshot snapshot = CaptureDiagnosticSnapshot(0, MAX_UNLOAD_HISTORY);
            result.Clear();
            result.AddRange(snapshot.History);
        }

        /// <summary>清空卸载历史并递增诊断版本，不影响资源缓存。</summary>
        public static void ClearUnloadHistory()
        {
            lock (sLock)
            {
                sUnloadHistory.Clear();
                BumpDiagnosticVersionLocked();
            }
        }

        /// <summary>原子复制指定数量的资源与历史，JSON 构建和排序在状态锁外完成。</summary>
        internal static ResKitDiagnosticSnapshot CaptureDiagnosticSnapshot(int resourceLimit, int historyLimit)
        {
            return CaptureDiagnosticSnapshot(resourceLimit, historyLimit, 0);
        }

        /// <summary>按每个资源的来源上限复制诊断值，常态 state 使用零避免来源分配。</summary>
        internal static ResKitDiagnosticSnapshot CaptureDiagnosticSnapshot(
            int resourceLimit,
            int historyLimit,
            int sourceLimit)
        {
            if (resourceLimit < 0) throw new ArgumentOutOfRangeException(nameof(resourceLimit));
            if (historyLimit < 0) throw new ArgumentOutOfRangeException(nameof(historyLimit));
            if (sourceLimit < 0) throw new ArgumentOutOfRangeException(nameof(sourceLimit));
            DiagnosticCapture capture;
            lock (sLock)
            {
                capture = CaptureLocked(resourceLimit, historyLimit, sourceLimit);
            }

            Array.Sort(capture.Resources, CompareResources);
            ResDebugInfo[] resources = TrimResources(capture.Resources, resourceLimit);
            ResUnloadRecord[] history = CreateUnloadRecords(capture.History);
            return CreateSnapshot(capture, resources, history);
        }

        /// <summary>在状态锁内复制引用和基础值，避免诊断读到撕裂计数。</summary>
        private static DiagnosticCapture CaptureLocked(int resourceLimit, int historyLimit, int sourceLimit)
        {
            int historyCount = Math.Min(historyLimit, sUnloadHistory.Count);
            ResUnloadEvent[] history = new ResUnloadEvent[historyCount];
            ResDebugInfo[] resources = CaptureResourcesLocked(
                resourceLimit,
                sourceLimit,
                out int totalRefCount);

            for (var index = 0; index < historyCount; index++) history[index] = sUnloadHistory.GetNewest(index);
            return new DiagnosticCapture(
                DiagnosticVersion, CreateProviderSnapshotLocked(),
                new ResKitStatsSnapshot(sCache.Count, sPendingLoads.Count, totalRefCount,
                    sUnloadHistory.Count, sEnableLoadLocationTracking),
                resources, sCache.Count, history, sUnloadHistory.Count,
                sUnloadHistory.DroppedCount, sLastBackgroundFailure?.Message ?? string.Empty);
        }

        /// <summary>在锁内复制资源统计；有界 state 仅为排序靠前的资源创建诊断对象。</summary>
        private static ResDebugInfo[] CaptureResourcesLocked(
            int resourceLimit,
            int sourceLimit,
            out int totalRefCount)
        {
            totalRefCount = 0;
            if (resourceLimit == 0)
            {
                foreach (ResCacheEntry entry in sCache.Values) totalRefCount += entry.RefCount;
                return Array.Empty<ResDebugInfo>();
            }

            if (resourceLimit >= sCache.Count)
            {
                ResDebugInfo[] resources = new ResDebugInfo[sCache.Count];
                var resourceIndex = 0;
                foreach (ResCacheEntry entry in sCache.Values)
                {
                    totalRefCount += entry.RefCount;
                    resources[resourceIndex++] = CreateResourceInfo(entry, sourceLimit);
                }

                return resources;
            }

            ResCacheEntry[] selectedEntries = new ResCacheEntry[resourceLimit];
            var selectedCount = 0;
            foreach (ResCacheEntry entry in sCache.Values)
            {
                totalRefCount += entry.RefCount;
                InsertTopResourceEntry(selectedEntries, ref selectedCount, entry);
            }

            ResDebugInfo[] result = new ResDebugInfo[selectedCount];
            for (var index = 0; index < selectedCount; index++)
            {
                result[index] = CreateResourceInfo(selectedEntries[index], sourceLimit);
            }

            return result;
        }

        /// <summary>把候选条目插入有序固定数组，容量已满时仅保留排序更靠前的资源。</summary>
        private static void InsertTopResourceEntry(
            ResCacheEntry[] entries,
            ref int count,
            ResCacheEntry candidate)
        {
            var insertIndex = count;
            if (count == entries.Length)
            {
                if (CompareResourceEntries(candidate, entries[count - 1]) >= 0) return;
                insertIndex = count - 1;
            }
            else
            {
                count++;
            }

            while (insertIndex > 0
                && CompareResourceEntries(candidate, entries[insertIndex - 1]) < 0)
            {
                entries[insertIndex] = entries[insertIndex - 1];
                insertIndex--;
            }

            entries[insertIndex] = candidate;
        }

        /// <summary>按 state 的稳定顺序比较缓存条目，避免为淘汰项分配诊断对象。</summary>
        private static int CompareResourceEntries(ResCacheEntry left, ResCacheEntry right)
        {
            int count = right.RefCount.CompareTo(left.RefCount);
            if (count != 0) return count;
            int path = string.Compare(left.Key.Path, right.Key.Path, StringComparison.Ordinal);
            return path != 0
                ? path
                : string.Compare(
                    left.Key.AssetType.FullName ?? left.Key.AssetType.Name,
                    right.Key.AssetType.FullName ?? right.Key.AssetType.Name,
                    StringComparison.Ordinal);
        }

        /// <summary>复制一个缓存条目及其独立 lease 来源，不保留内部对象引用。</summary>
        private static ResDebugInfo CreateResourceInfo(ResCacheEntry entry, int sourceLimit)
        {
            ResLoadSourceInfo[] sources = CreateSources(entry, sourceLimit, out int trackedCount);
            ResLoadSourceInfo first = FindFirstTrackedSource(sources);
            return new ResDebugInfo
            {
                Path = entry.Key.Path,
                TypeName = entry.Key.AssetType.FullName ?? entry.Key.AssetType.Name,
                RefCount = entry.RefCount,
                IsDone = entry.IsValid,
                ProviderName = entry.ProviderName,
                ProviderGeneration = entry.ProviderGeneration,
                Source = first?.Display ?? string.Empty,
                SourceFile = first?.FilePath ?? string.Empty,
                SourceLine = first?.Line ?? 0,
                TrackedSourceCount = trackedCount,
                SourceTotalCount = entry.Leases.Count,
                Sources = sources
            };
        }

        /// <summary>复制有界来源并统计全部 tracked lease；单项摘要优先返回真实调用位置。</summary>
        private static ResLoadSourceInfo[] CreateSources(
            ResCacheEntry entry,
            int sourceLimit,
            out int trackedCount)
        {
            int sourceCount = Math.Min(entry.Leases.Count, sourceLimit);
            ResLoadSourceInfo[] sources = new ResLoadSourceInfo[sourceCount];
            trackedCount = 0;
            ResLease firstTracked = null;
            for (var index = 0; index < entry.Leases.Count; index++)
            {
                ResLease lease = entry.Leases[index];
                if (index < sourceCount) sources[index] = CreateSourceInfo(lease);
                if (!lease.Source.Tracked) continue;
                trackedCount++;
                if (firstTracked == null) firstTracked = lease;
            }

            if (sourceCount == 1 && firstTracked != null) sources[0] = CreateSourceInfo(firstTracked);
            return sources;
        }

        /// <summary>把内部 lease 来源转换为不可修改内部状态的公开诊断值。</summary>
        private static ResLoadSourceInfo CreateSourceInfo(ResLease lease)
        {
            return new ResLoadSourceInfo
            {
                Display = lease.Source.Display,
                FilePath = lease.Source.FilePath,
                Line = lease.Source.Line,
                RefCount = lease.Count,
                IsAnonymous = lease.Anonymous,
                IsTracked = lease.Source.Tracked
            };
        }

        /// <summary>返回第一条真实跟踪来源，未启用跟踪时返回空。</summary>
        private static ResLoadSourceInfo FindFirstTrackedSource(IReadOnlyList<ResLoadSourceInfo> sources)
        {
            for (var index = 0; index < sources.Count; index++)
            {
                if (sources[index].IsTracked) return sources[index];
            }

            return null;
        }

        /// <summary>在不调用 Provider 业务方法的前提下复制能力声明。</summary>
        private static ResKitProviderSnapshot CreateProviderSnapshotLocked()
        {
            return new ResKitProviderSnapshot(
                sProviderName,
                sProviderGeneration,
                sSupportsRawBytes,
                sSupportsRawText);
        }

        /// <summary>把内部卸载事件转换为最新优先的公开隔离副本。</summary>
        private static ResUnloadRecord[] CreateUnloadRecords(IReadOnlyList<ResUnloadEvent> source)
        {
            ResUnloadRecord[] result = new ResUnloadRecord[source.Count];
            for (var index = 0; index < source.Count; index++)
            {
                ResUnloadEvent item = source[index];
                result[index] = new ResUnloadRecord
                {
                    Path = item.Path,
                    TypeName = item.TypeName,
                    ProviderName = item.ProviderName,
                    UnloadTimeUtc = item.UnloadTimeUtc.ToString("O", CultureInfo.InvariantCulture)
                };
            }

            return result;
        }

        /// <summary>按引用数降序、路径和类型升序提供稳定 Workbench 顺序。</summary>
        private static int CompareResources(ResDebugInfo left, ResDebugInfo right)
        {
            int count = right.RefCount.CompareTo(left.RefCount);
            if (count != 0) return count;
            int path = string.Compare(left.Path, right.Path, StringComparison.Ordinal);
            return path != 0 ? path : string.Compare(left.TypeName, right.TypeName, StringComparison.Ordinal);
        }

        /// <summary>在完整稳定排序后裁剪资源，避免字典枚举顺序影响 Workbench 前几项。</summary>
        private static ResDebugInfo[] TrimResources(ResDebugInfo[] resources, int limit)
        {
            if (resources.Length <= limit) return resources;
            ResDebugInfo[] result = new ResDebugInfo[limit];
            Array.Copy(resources, result, limit);
            return result;
        }

        /// <summary>从已复制状态创建最终快照对象。</summary>
        private static ResKitDiagnosticSnapshot CreateSnapshot(
            DiagnosticCapture capture,
            ResDebugInfo[] resources,
            ResUnloadRecord[] history)
        {
            return new ResKitDiagnosticSnapshot(
                capture.Version, capture.Provider, capture.Stats, resources,
                capture.ResourceTotal, history, capture.HistoryTotal,
                capture.HistoryDroppedCount, capture.LastBackgroundFailure);
        }

        /// <summary>承载锁内原子复制结果，离开锁后再做排序和时间格式化。</summary>
        private sealed class DiagnosticCapture
        {
            /// <summary>创建一次内部诊断捕获。</summary>
            internal DiagnosticCapture(
                long version, ResKitProviderSnapshot provider, ResKitStatsSnapshot stats,
                ResDebugInfo[] resources, int resourceTotal, ResUnloadEvent[] history,
                int historyTotal, long historyDroppedCount, string lastBackgroundFailure)
            {
                Version = version; Provider = provider; Stats = stats; Resources = resources;
                ResourceTotal = resourceTotal; History = history; HistoryTotal = historyTotal;
                HistoryDroppedCount = historyDroppedCount; LastBackgroundFailure = lastBackgroundFailure;
            }

            internal long Version { get; }
            internal ResKitProviderSnapshot Provider { get; }
            internal ResKitStatsSnapshot Stats { get; }
            internal ResDebugInfo[] Resources { get; }
            internal int ResourceTotal { get; }
            internal ResUnloadEvent[] History { get; }
            internal int HistoryTotal { get; }
            internal long HistoryDroppedCount { get; }
            internal string LastBackgroundFailure { get; }
        }
    }
}
#endif
