using System;
using System.Text;

namespace YokiFrame
{
    /// <summary>
    /// 文件命令桥核心的状态与诊断输出片段。
    /// </summary>
    public sealed partial class YokiCommandBridgeCore
    {
        /// <summary>
        /// 构建当前命令桥状态的 JSON 快照。
        /// </summary>
        /// <returns>包含实时队列、限流和错误状态的轻量 JSON 字符串。</returns>
        public string BuildStatusJson()
        {
            return BuildStatusJson(false);
        }

        /// <summary>
        /// 构建当前命令桥状态的详细 JSON 快照，包含需要扫描协议目录的存储统计。
        /// </summary>
        /// <returns>包含队列、存储、限流和错误状态的 JSON 字符串。</returns>
        public string BuildStatusDetailJson()
        {
            return BuildStatusJson(true);
        }

        private string BuildStatusJson(bool includeStorageStats)
        {
            var sb = new StringBuilder(512);
            sb.Append("{\"protocolVersion\":2");
            sb.Append(",\"engineId\":\"");
            sb.Append(JsonHelper.EscapeString(mOptions.EngineId ?? string.Empty));
            sb.Append('"');
            AppendCount(sb, "pendingCommandCount", mCommandDir, "*.json");
            AppendNumber(sb, "processingCommandCount", CountFiles(mProcessingDir, "*.json", ShouldCountAsVisibleProcessing));
            AppendNumber(sb, "staleProcessingCommandCount", CountStaleProcessingCommands());
            sb.Append(",\"storageStatsIncluded\":");
            sb.Append(includeStorageStats ? "true" : "false");
            if (includeStorageStats)
            {
                var storageSnapshot = GetStatusStorageSnapshot();
                AppendNumber(sb, "archiveCommandCount", storageSnapshot.ArchiveCommandCount);
                AppendNumber(sb, "deadletterCommandCount", storageSnapshot.DeadletterCommandCount);
                AppendNumber(sb, "resultCount", storageSnapshot.ResultCount);
                AppendNumber(sb, "errorCount", storageSnapshot.ErrorCount);
                AppendNumber(sb, "snapshotCount", storageSnapshot.SnapshotCount);
                var storageStats = storageSnapshot.ProtocolStats;
                AppendNumber(sb, "protocolFileCount", storageStats.FileCount);
                AppendNumber(sb, "protocolBytes", storageStats.TotalBytes);
                AppendDate(sb, "oldestProtocolFileUtc", storageStats.OldestFileUtc);
                sb.Append(",\"statusStorageStatsTruncated\":");
                sb.Append(storageSnapshot.Truncated || storageStats.Truncated ? "true" : "false");
            }
            AppendNumber(sb, "processedCommandCount", mProcessedCommandCount);
            AppendNumber(sb, "deadletteredProcessingCommandCount", mDeadletterCommandCount);
            AppendNumber(sb, "cleanedFileCount", mCleanedFileCount);
            AppendNumber(sb, "duplicateCommandCount", mDuplicateCommandCount);
            AppendNumber(sb, "payloadTooLargeCount", mPayloadTooLargeCount);
            AppendNumber(sb, "resultTooLargeCount", mResultTooLargeCount);
            AppendNumber(sb, "bridgeBusyCount", mBridgeBusyCount);
            sb.Append(",\"backpressureActive\":");
            sb.Append(mBackpressureActive ? "true" : "false");
            if (!string.IsNullOrEmpty(mLastPollLimitReason))
            {
                sb.Append(",\"lastPollLimitReason\":\"");
                sb.Append(JsonHelper.EscapeString(mLastPollLimitReason));
                sb.Append('"');
            }
            AppendDate(sb, "lastPollUtc", mLastPollUtc);
            AppendDate(sb, "lastCleanupUtc", mLastCleanupUtc);
            AppendDate(sb, "lastErrorUtc", mLastErrorUtc);
            if (!string.IsNullOrEmpty(mLastErrorMessage))
            {
                sb.Append(",\"lastError\":\"");
                sb.Append(JsonHelper.EscapeString(mLastErrorMessage));
                sb.Append('"');
            }
            sb.Append('}');
            return sb.ToString();
        }

        private void AppendCount(StringBuilder sb, string fieldName, string dir, string pattern)
        {
            AppendNumber(sb, fieldName, CountFiles(dir, pattern, null));
        }

        private static void AppendNumber(StringBuilder sb, string fieldName, long value)
        {
            sb.Append(",\"");
            sb.Append(fieldName);
            sb.Append("\":");
            sb.Append(value);
        }

        private static void AppendDate(StringBuilder sb, string fieldName, DateTime? value)
        {
            sb.Append(",\"");
            sb.Append(fieldName);
            sb.Append("\":");
            if (value.HasValue)
            {
                sb.Append('"');
                sb.Append(value.Value.ToString("O"));
                sb.Append('"');
            }
            else
            {
                sb.Append("null");
            }
        }
    }
}
