#if UNITY_EDITOR || (GODOT && TOOLS)
using System;
using System.Text;

namespace YokiFrame
{
    /// <summary>把 ResKit 诊断值写成有界、宿主无关的稳定 JSON。</summary>
    internal static class ResKitJsonWriter
    {
        internal const int MAX_STATE_RESOURCES = 48;
        internal const int MAX_STATE_HISTORY = 16;
        internal const int MAX_PAGE_SIZE = 64;
        internal const int MAX_DETAIL_SOURCES = 16;
        private const int MAX_PATH_BYTES = 256;
        private const int MAX_TYPE_BYTES = 192;
        private const int MAX_PROVIDER_BYTES = 128;
        private const int MAX_SOURCE_BYTES = 160;
        private const int MAX_SOURCE_FILE_BYTES = 256;
        private const int MAX_FAILURE_BYTES = 512;

        /// <summary>创建唯一 ResKit/state payload，并在极端文本下继续缩减明细数量。</summary>
        internal static string WriteState()
        {
            ResKitDiagnosticSnapshot snapshot = ResKit.CaptureDiagnosticSnapshot(
                MAX_STATE_RESOURCES, MAX_STATE_HISTORY, 1);
            int resourceCount = snapshot.Resources.Length;
            int historyCount = snapshot.History.Length;
            while (true)
            {
                string json = WriteState(snapshot, resourceCount, historyCount);
                if (Encoding.UTF8.GetByteCount(json) <= YokiFrameSharedMemoryTelemetryContract.DEFAULT_MAX_PAYLOAD_BYTES)
                {
                    return json;
                }

                if (resourceCount > 0) resourceCount /= 2;
                else if (historyCount > 0) historyCount /= 2;
                else return WriteMinimalState(snapshot);
            }
        }

        /// <summary>写入聚合统计，不包含资源和历史明细。</summary>
        internal static string WriteStats(ResKitDiagnosticSnapshot snapshot)
        {
            StringBuilder builder = new(320);
            builder.Append("{\"schemaVersion\":1,\"diagnosticVersion\":").Append(snapshot.Version);
            AppendProvider(builder, snapshot.Provider);
            AppendStats(builder, snapshot.Stats);
            builder.Append(",\"historyDroppedCount\":").Append(snapshot.HistoryDroppedCount).Append('}');
            return builder.ToString();
        }

        /// <summary>写入已排序资源的指定页，并保留总量和当前诊断版本。</summary>
        internal static string WriteResourcePage(ResKitDiagnosticSnapshot snapshot, int offset, int limit)
        {
            int end = CalculatePageEnd(snapshot.Resources.Length, offset, limit);
            StringBuilder builder = new(512);
            builder.Append("{\"schemaVersion\":1,\"diagnosticVersion\":").Append(snapshot.Version);
            builder.Append(",\"resources\":[");
            for (var index = offset; index < end; index++)
            {
                if (index > offset) builder.Append(',');
                AppendResource(builder, snapshot.Resources[index], false);
            }

            builder.Append("],\"offset\":").Append(offset);
            builder.Append(",\"count\":").Append(Math.Max(0, end - offset));
            builder.Append(",\"totalCount\":").Append(snapshot.ResourceTotal);
            builder.Append(",\"hasMore\":").Append(ToJson(end < snapshot.ResourceTotal)).Append('}');
            return builder.ToString();
        }

        /// <summary>写入单个资源及有界独立 lease 来源。</summary>
        internal static string WriteResourceDetail(ResKitDiagnosticSnapshot snapshot, ResDebugInfo resource)
        {
            StringBuilder builder = new(768);
            builder.Append("{\"schemaVersion\":1,\"diagnosticVersion\":").Append(snapshot.Version);
            builder.Append(",\"resource\":");
            AppendResource(builder, resource, true);
            builder.Append('}');
            return builder.ToString();
        }

        /// <summary>写入卸载历史指定页，记录固定环覆盖数量。</summary>
        internal static string WriteHistoryPage(ResKitDiagnosticSnapshot snapshot, int offset, int limit)
        {
            int end = CalculatePageEnd(snapshot.History.Length, offset, limit);
            StringBuilder builder = new(512);
            builder.Append("{\"schemaVersion\":1,\"diagnosticVersion\":").Append(snapshot.Version);
            builder.Append(",\"history\":[");
            for (var index = offset; index < end; index++)
            {
                if (index > offset) builder.Append(',');
                AppendUnload(builder, snapshot.History[index]);
            }

            builder.Append("],\"offset\":").Append(offset);
            builder.Append(",\"count\":").Append(Math.Max(0, end - offset));
            builder.Append(",\"totalCount\":").Append(snapshot.HistoryTotal);
            builder.Append(",\"droppedCount\":").Append(snapshot.HistoryDroppedCount);
            builder.Append(",\"hasMore\":").Append(ToJson(end < snapshot.HistoryTotal)).Append('}');
            return builder.ToString();
        }

        /// <summary>写入资源是否加载及相关最近卸载记录的诊断摘要。</summary>
        internal static string WriteDiagnosis(
            ResKitDiagnosticSnapshot snapshot,
            string path,
            string typeName,
            ResDebugInfo resource,
            ResUnloadRecord latestUnload,
            int relatedUnloadCount)
        {
            StringBuilder builder = new(768);
            builder.Append("{\"schemaVersion\":1,\"diagnosticVersion\":").Append(snapshot.Version);
            builder.Append(",\"path\":"); AppendString(builder, path, MAX_PATH_BYTES);
            builder.Append(",\"typeName\":"); AppendString(builder, typeName, MAX_TYPE_BYTES);
            builder.Append(",\"isLoaded\":").Append(ToJson(resource != null));
            builder.Append(",\"relatedUnloadCount\":").Append(relatedUnloadCount);
            builder.Append(",\"resource\":");
            if (resource == null) builder.Append("null"); else AppendResource(builder, resource, true);
            builder.Append(",\"latestUnload\":");
            if (latestUnload == null) builder.Append("null"); else AppendUnload(builder, latestUnload);
            builder.Append('}');
            return builder.ToString();
        }

        /// <summary>写入完整 state 的指定明细预算。</summary>
        private static string WriteState(ResKitDiagnosticSnapshot snapshot, int resourceCount, int historyCount)
        {
            StringBuilder builder = new(2048);
            builder.Append("{\"schemaVersion\":1,\"diagnosticVersion\":").Append(snapshot.Version);
            AppendProvider(builder, snapshot.Provider);
            AppendStats(builder, snapshot.Stats);
            AppendStateResources(builder, snapshot, resourceCount);
            AppendStateHistory(builder, snapshot, historyCount);
            builder.Append(",\"lastBackgroundFailure\":");
            AppendString(builder, snapshot.LastBackgroundFailure, MAX_FAILURE_BYTES);
            builder.Append('}');
            return builder.ToString();
        }

        /// <summary>写入 Provider 身份、代次和 raw 能力。</summary>
        private static void AppendProvider(StringBuilder builder, ResKitProviderSnapshot provider)
        {
            builder.Append(",\"provider\":{\"name\":");
            AppendString(builder, provider.Name, MAX_PROVIDER_BYTES);
            builder.Append(",\"generation\":").Append(provider.Generation);
            builder.Append(",\"capabilities\":{\"rawBytes\":").Append(ToJson(provider.SupportsRawBytes));
            builder.Append(",\"rawText\":").Append(ToJson(provider.SupportsRawText));
            builder.Append("}}");
        }

        /// <summary>写入原子聚合计数和跟踪开关。</summary>
        private static void AppendStats(StringBuilder builder, ResKitStatsSnapshot stats)
        {
            builder.Append(",\"stats\":{\"loadedCount\":").Append(stats.LoadedCount);
            builder.Append(",\"inFlightCount\":").Append(stats.InFlightCount);
            builder.Append(",\"totalLeaseCount\":").Append(stats.TotalLeaseCount);
            builder.Append(",\"unloadHistoryCount\":").Append(stats.UnloadHistoryCount);
            builder.Append(",\"loadLocationTrackingEnabled\":").Append(ToJson(stats.TrackingEnabled));
            builder.Append('}');
        }

        /// <summary>写入 state 中按预算截断的资源摘要。</summary>
        private static void AppendStateResources(
            StringBuilder builder,
            ResKitDiagnosticSnapshot snapshot,
            int resourceCount)
        {
            int count = Math.Min(resourceCount, snapshot.Resources.Length);
            builder.Append(",\"resources\":{\"items\":[");
            for (var index = 0; index < count; index++)
            {
                if (index > 0) builder.Append(',');
                AppendResource(builder, snapshot.Resources[index], true);
            }

            builder.Append("],\"totalCount\":").Append(snapshot.ResourceTotal);
            builder.Append(",\"truncated\":").Append(ToJson(count < snapshot.ResourceTotal)).Append('}');
        }

        /// <summary>写入 state 中最新优先的有界卸载历史。</summary>
        private static void AppendStateHistory(
            StringBuilder builder,
            ResKitDiagnosticSnapshot snapshot,
            int historyCount)
        {
            int count = Math.Min(historyCount, snapshot.History.Length);
            builder.Append(",\"unloadHistory\":{\"items\":[");
            for (var index = 0; index < count; index++)
            {
                if (index > 0) builder.Append(',');
                AppendUnload(builder, snapshot.History[index]);
            }

            builder.Append("],\"totalCount\":").Append(snapshot.HistoryTotal);
            builder.Append(",\"droppedCount\":").Append(snapshot.HistoryDroppedCount);
            builder.Append(",\"truncated\":").Append(ToJson(count < snapshot.HistoryTotal)).Append('}');
        }

        /// <summary>写入单个资源摘要，并按需追加有界 lease 来源。</summary>
        private static void AppendResource(StringBuilder builder, ResDebugInfo resource, bool includeSources)
        {
            builder.Append("{\"path\":"); AppendString(builder, resource.Path, MAX_PATH_BYTES);
            builder.Append(",\"typeName\":"); AppendString(builder, resource.TypeName, MAX_TYPE_BYTES);
            builder.Append(",\"state\":\"Ready\",\"leaseCount\":").Append(resource.RefCount);
            builder.Append(",\"providerName\":"); AppendString(builder, resource.ProviderName, MAX_PROVIDER_BYTES);
            builder.Append(",\"providerGeneration\":").Append(resource.ProviderGeneration);
            builder.Append(",\"trackedSourceCount\":").Append(resource.TrackedSourceCount);
            if (includeSources) AppendSources(builder, resource);
            builder.Append('}');
        }

        /// <summary>追加最多十六条独立 lease 来源，避免详情 payload 无界。</summary>
        private static void AppendSources(StringBuilder builder, ResDebugInfo resource)
        {
            System.Collections.Generic.IReadOnlyList<ResLoadSourceInfo> sources = resource.Sources;
            int count = Math.Min(sources.Count, MAX_DETAIL_SOURCES);
            builder.Append(",\"sources\":[");
            for (var index = 0; index < count; index++)
            {
                if (index > 0) builder.Append(',');
                ResLoadSourceInfo source = sources[index];
                builder.Append("{\"display\":"); AppendString(builder, source.Display, MAX_SOURCE_BYTES);
                builder.Append(",\"filePath\":"); AppendString(builder, source.FilePath, MAX_SOURCE_FILE_BYTES);
                builder.Append(",\"line\":").Append(source.Line);
                builder.Append(",\"refCount\":").Append(source.RefCount);
                builder.Append(",\"anonymous\":").Append(ToJson(source.IsAnonymous));
                builder.Append(",\"tracked\":").Append(ToJson(source.IsTracked)).Append('}');
            }

            builder.Append("],\"sourceTotal\":").Append(resource.SourceTotalCount);
            builder.Append(",\"sourcesTruncated\":").Append(ToJson(count < resource.SourceTotalCount));
        }

        /// <summary>写入一条不可变卸载历史副本。</summary>
        private static void AppendUnload(StringBuilder builder, ResUnloadRecord item)
        {
            builder.Append("{\"path\":"); AppendString(builder, item.Path, MAX_PATH_BYTES);
            builder.Append(",\"typeName\":"); AppendString(builder, item.TypeName, MAX_TYPE_BYTES);
            builder.Append(",\"providerName\":"); AppendString(builder, item.ProviderName, MAX_PROVIDER_BYTES);
            builder.Append(",\"unloadTimeUtc\":"); AppendString(builder, item.UnloadTimeUtc, 64);
            builder.Append('}');
        }

        /// <summary>在极端情况下返回只含聚合计数的合法 state。</summary>
        private static string WriteMinimalState(ResKitDiagnosticSnapshot snapshot)
        {
            StringBuilder builder = new(384);
            builder.Append("{\"schemaVersion\":1,\"diagnosticVersion\":").Append(snapshot.Version);
            AppendProvider(builder, snapshot.Provider);
            AppendStats(builder, snapshot.Stats);
            builder.Append(",\"resources\":{\"items\":[],\"totalCount\":").Append(snapshot.ResourceTotal);
            builder.Append(",\"truncated\":true},\"unloadHistory\":{\"items\":[],\"totalCount\":");
            builder.Append(snapshot.HistoryTotal).Append(",\"droppedCount\":").Append(snapshot.HistoryDroppedCount);
            builder.Append(",\"truncated\":true},\"lastBackgroundFailure\":\"payload-truncated\"}");
            return builder.ToString();
        }

        /// <summary>把文本裁剪到 UTF-8 字节预算后写入安全 JSON 字符串。</summary>
        private static void AppendString(StringBuilder builder, string value, int maxUtf8Bytes)
        {
            string normalized = NormalizeText(value, maxUtf8Bytes);
            builder.Append('\"').Append(JsonHelper.EscapeString(normalized)).Append('\"');
        }

        /// <summary>按 UTF-8 字节裁剪文本，并保持代理项完整。</summary>
        private static string NormalizeText(string value, int maxUtf8Bytes)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            if (Encoding.UTF8.GetByteCount(value) <= maxUtf8Bytes) return value;
            int length = value.Length;
            while (length > 0 && Encoding.UTF8.GetByteCount(value, 0, length) > maxUtf8Bytes)
            {
                length--;
                if (length > 0 && char.IsHighSurrogate(value[length - 1])) length--;
            }

            return value.Substring(0, length);
        }

        /// <summary>用剩余数量计算页尾，避免 offset 与 limit 直接相加发生整数溢出。</summary>
        private static int CalculatePageEnd(int itemCount, int offset, int limit)
        {
            return offset >= itemCount
                ? itemCount
                : offset + Math.Min(itemCount - offset, limit);
        }

        /// <summary>返回 JSON 布尔字面量。</summary>
        private static string ToJson(bool value) => value ? "true" : "false";
    }
}
#endif
