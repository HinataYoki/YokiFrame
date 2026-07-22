#if UNITY_EDITOR || (GODOT && TOOLS)
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace YokiFrame
{
    /// <summary>把 SaveKit 的安全诊断摘要写为有界 state JSON，不接触模块 payload。</summary>
    internal static class SaveKitSnapshotWriter
    {
        private const int MAX_METADATA_PER_KIND = 32;
        private const int MAX_STORAGE_TYPE_LENGTH = 128;
        private const int MAX_IDENTIFIER_LENGTH = 128;
        private const int MAX_TARGET_NAME_LENGTH = 128;
        private const int MAX_DISPLAY_NAME_LENGTH = 256;

        /// <summary>创建仅含后端、自动保存和容器数量的轻量统计 JSON。</summary>
        /// <returns>固定 schema 的 SaveKit 统计结果。</returns>
        internal static string WriteStats()
        {
            SaveKitDiagnosticsSnapshot snapshot = SaveKit.CreateDiagnosticsSnapshot();
            var builder = new StringBuilder(512);
            AppendHeader(builder, snapshot);
            AppendBackend(builder, snapshot);
            AppendAutoSave(builder, snapshot);
            AppendCollectionStats(builder, "slot", snapshot.Slots.Count, snapshot.SlotTotal, snapshot.SlotsTruncated);
            AppendCollectionStats(builder, "global", snapshot.Globals.Count, snapshot.GlobalTotal, snapshot.GlobalsTruncated);
            builder.Append(",\"metadataAvailable\":").Append(ToJson(snapshot.MetadataAvailable));
            builder.Append(",\"metadataReadFailed\":").Append(ToJson(snapshot.MetadataReadFailed));
            builder.Append('}');
            return builder.ToString();
        }

        /// <summary>创建完整的 SaveKit Snapshot state，并在超限时收缩条目数。</summary>
        /// <returns>不包含存档 payload 的有界 JSON。</returns>
        internal static string WriteWorkbench()
        {
            SaveKitDiagnosticsSnapshot snapshot = SaveKit.CreateDiagnosticsSnapshot();
            int slotLimit = Math.Min(snapshot.Slots.Count, MAX_METADATA_PER_KIND);
            int globalLimit = Math.Min(snapshot.Globals.Count, MAX_METADATA_PER_KIND);
            while (true)
            {
                string json = BuildWorkbench(snapshot, slotLimit, globalLimit);
                if (Encoding.UTF8.GetByteCount(json)
                    <= YokiFrameSharedMemoryTelemetryContract.DEFAULT_MAX_PAYLOAD_BYTES)
                {
                    return json;
                }

                if (slotLimit == 0 && globalLimit == 0)
                {
                    return json;
                }

                ReduceLargestLimit(ref slotLimit, ref globalLimit);
            }
        }

        /// <summary>按当前条目预算生成完整 state JSON。</summary>
        private static string BuildWorkbench(
            SaveKitDiagnosticsSnapshot snapshot,
            int slotLimit,
            int globalLimit)
        {
            var builder = new StringBuilder(8192);
            AppendHeader(builder, snapshot);
            AppendBackend(builder, snapshot);
            AppendAutoSave(builder, snapshot);
            AppendCollection(
                builder,
                "slots",
                "slot",
                snapshot.Slots,
                snapshot.SlotTotal,
                snapshot.SlotsTruncated,
                slotLimit);
            AppendCollection(
                builder,
                "globals",
                "global",
                snapshot.Globals,
                snapshot.GlobalTotal,
                snapshot.GlobalsTruncated,
                globalLimit);
            builder.Append(",\"metadataAvailable\":").Append(ToJson(snapshot.MetadataAvailable));
            builder.Append(",\"metadataReadFailed\":").Append(ToJson(snapshot.MetadataReadFailed));
            builder.Append('}');
            return builder.ToString();
        }

        /// <summary>写入固定 state schema。</summary>
        private static void AppendHeader(StringBuilder builder, SaveKitDiagnosticsSnapshot snapshot)
        {
            builder.Append("{\"schemaVersion\":1,\"version\":").Append(snapshot.StateVersion);
        }

        /// <summary>写入已存在后端的配置事实，不触发后端惰性创建。</summary>
        private static void AppendBackend(StringBuilder builder, SaveKitDiagnosticsSnapshot snapshot)
        {
            builder.Append(",\"backend\":{\"storageConfigured\":")
                .Append(ToJson(snapshot.StorageConfigured));
            builder.Append(",\"serializerConfigured\":").Append(ToJson(snapshot.SerializerConfigured));
            builder.Append(",\"ready\":").Append(ToJson(snapshot.StorageConfigured && snapshot.SerializerConfigured));
            builder.Append(",\"storageType\":");
            AppendString(builder, snapshot.StorageType, MAX_STORAGE_TYPE_LENGTH);
            builder.Append(",\"serializerId\":");
            AppendString(builder, snapshot.SerializerId, MAX_IDENTIFIER_LENGTH);
            builder.Append(",\"encryptorId\":");
            AppendString(builder, snapshot.EncryptorId, MAX_IDENTIFIER_LENGTH);
            builder.Append('}');
        }

        /// <summary>写入自动保存开关与时间摘要；未启用时不暴露默认目标。</summary>
        private static void AppendAutoSave(StringBuilder builder, SaveKitDiagnosticsSnapshot snapshot)
        {
            builder.Append(",\"autoSave\":{\"enabled\":").Append(ToJson(snapshot.AutoSaveEnabled));
            if (snapshot.AutoSaveEnabled)
            {
                builder.Append(",\"target\":");
                AppendTarget(builder, snapshot.AutoSaveTarget);
                builder.Append(",\"intervalSeconds\":");
                AppendFiniteFloat(builder, snapshot.AutoSaveIntervalSeconds);
                builder.Append(",\"elapsedSeconds\":");
                AppendFiniteFloat(builder, snapshot.AutoSaveElapsedSeconds);
            }
            else
            {
                builder.Append(",\"target\":null,\"intervalSeconds\":0,\"elapsedSeconds\":0");
            }

            builder.Append('}');
        }

        /// <summary>写入指定目标域的条目、总量和裁剪事实。</summary>
        private static void AppendCollection(
            StringBuilder builder,
            string arrayName,
            string fieldPrefix,
            IReadOnlyList<SaveKitDiagnosticsMeta> entries,
            int targetTotal,
            bool diagnosticTruncated,
            int limit)
        {
            int count = Math.Min(entries.Count, Math.Max(0, limit));
            builder.Append(",\"").Append(arrayName).Append("\":[");
            for (int index = 0; index < count; index++)
            {
                if (index > 0)
                {
                    builder.Append(',');
                }

                AppendMeta(builder, entries[index]);
            }

            builder.Append(']');
            AppendCollectionStats(builder, fieldPrefix, count, targetTotal, diagnosticTruncated || entries.Count > count);
        }

        /// <summary>写入目标域的已写入数量、Storage 总数与截断标记。</summary>
        private static void AppendCollectionStats(
            StringBuilder builder,
            string prefix,
            int count,
            int total,
            bool truncated)
        {
            builder.Append(",\"").Append(prefix).Append("Count\":").Append(count);
            builder.Append(",\"").Append(prefix).Append("Total\":").Append(total);
            builder.Append(",\"").Append(prefix).Append("sTruncated\":").Append(ToJson(truncated));
        }

        /// <summary>写入单个已经通过容器头验证的安全元数据。</summary>
        private static void AppendMeta(StringBuilder builder, SaveKitDiagnosticsMeta meta)
        {
            builder.Append("{\"target\":");
            AppendTarget(builder, meta.Target);
            builder.Append(",\"displayName\":");
            AppendString(builder, meta.DisplayName, MAX_DISPLAY_NAME_LENGTH);
            builder.Append(",\"containerVersion\":").Append(meta.ContainerVersion);
            builder.Append(",\"createdTimestamp\":").Append(meta.CreatedTimestamp);
            builder.Append(",\"lastSavedTimestamp\":").Append(meta.LastSavedTimestamp);
            builder.Append(",\"serializerId\":");
            AppendString(builder, meta.SerializerId, MAX_IDENTIFIER_LENGTH);
            builder.Append('}');
        }

        /// <summary>写入 Slot 或 Global 目标的稳定且无路径字段。</summary>
        private static void AppendTarget(StringBuilder builder, SaveTarget target)
        {
            builder.Append("{\"kind\":");
            AppendString(builder, target.IsSlot ? "Slot" : "Global", 16);
            builder.Append(",\"name\":");
            AppendString(builder, target.Name, MAX_TARGET_NAME_LENGTH);
            builder.Append(",\"slotId\":").Append(target.SlotId).Append('}');
        }

        /// <summary>把较大的条目预算折半，确保超长显示名不能突破共享内存上限。</summary>
        private static void ReduceLargestLimit(ref int slotLimit, ref int globalLimit)
        {
            if (slotLimit >= globalLimit && slotLimit > 0)
            {
                slotLimit /= 2;
                return;
            }

            if (globalLimit > 0)
            {
                globalLimit /= 2;
            }
        }

        /// <summary>写入裁剪到安全字符边界的 JSON 字符串。</summary>
        private static void AppendString(StringBuilder builder, string value, int maxLength)
        {
            string normalized = Truncate(value ?? string.Empty, maxLength);
            builder.Append('"').Append(JsonHelper.EscapeString(normalized)).Append('"');
        }

        /// <summary>在不切断 UTF-16 代理对的前提下截断显示字段。</summary>
        private static string Truncate(string value, int maxLength)
        {
            if (value.Length <= maxLength)
            {
                return value;
            }

            int length = maxLength;
            if (length > 0 && char.IsHighSurrogate(value[length - 1]) && char.IsLowSurrogate(value[length]))
            {
                length--;
            }

            return value.Substring(0, length);
        }

        /// <summary>把有限秒数格式化为合法 JSON 数字，异常值回落零。</summary>
        private static void AppendFiniteFloat(StringBuilder builder, float value)
        {
            builder.Append(float.IsNaN(value) || float.IsInfinity(value)
                ? "0"
                : value.ToString("R", CultureInfo.InvariantCulture));
        }

        /// <summary>返回 JSON 布尔字面量。</summary>
        private static string ToJson(bool value)
        {
            return value ? "true" : "false";
        }
    }
}
#endif
