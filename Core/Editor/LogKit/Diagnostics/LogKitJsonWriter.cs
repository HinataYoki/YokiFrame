#if UNITY_EDITOR || (GODOT && TOOLS)
using System;
using System.Text;

namespace YokiFrame
{
    /// <summary>
    /// 将 LogKit 状态和显式文件预览写成稳定 JSON，不依赖 Unity 或 Godot 序列化器。
    /// </summary>
    internal static class LogKitJsonWriter
    {
        private static readonly UTF8Encoding sUtf8 = new UTF8Encoding(false);

        /// <summary>
        /// 写入固定 Workbench state；极端转义文本导致超限时按最旧方向继续缩减历史。
        /// </summary>
        /// <param name="snapshot">LogKit 有界状态。</param>
        /// <returns>不超过 Shared Memory 默认 payload 上限的 JSON。</returns>
        internal static string WriteWorkbench(LogKitWorkbenchSnapshot snapshot)
        {
            int entryCount = snapshot.Entries.Length;
            string json;
            do
            {
                json = WriteWorkbench(snapshot, entryCount);
                if (sUtf8.GetByteCount(json) <= YokiFrameSharedMemoryTelemetryContract.DEFAULT_MAX_PAYLOAD_BYTES)
                {
                    return json;
                }

                entryCount--;
            }
            while (entryCount >= 0);

            return json;
        }

        /// <summary>写入一次指定历史数量的完整 state。</summary>
        private static string WriteWorkbench(LogKitWorkbenchSnapshot snapshot, int entryCount)
        {
            var builder = new StringBuilder(4096);
            builder.Append("{\"schemaVersion\":").Append(snapshot.SchemaVersion);
            builder.Append(",\"diagnosticVersion\":").Append(snapshot.DiagnosticVersion);
            builder.Append(",\"settingsVersion\":").Append(snapshot.SettingsVersion);
            AppendStats(builder, snapshot.Stats);
            AppendSettings(builder, snapshot.Settings);
            AppendCapabilities(builder, snapshot.Host);
            AppendFiles(builder, snapshot.Host.Directory, snapshot.EditorFile, snapshot.PlayerFile);
            AppendHistory(builder, snapshot, Math.Max(0, entryCount));
            builder.Append('}');
            return builder.ToString();
        }

        /// <summary>写入显式 read_log_file 的固定响应对象。</summary>
        /// <param name="preview">有界尾部读取结果。</param>
        /// <returns>文件元数据、尾部正文和错误字段 JSON。</returns>
        internal static string WriteFilePreview(LogKitFilePreview preview)
        {
            var builder = new StringBuilder(preview.Content.Length + 256);
            builder.Append("{\"kind\":");
            AppendString(builder, preview.Kind);
            builder.Append(",\"path\":");
            AppendString(builder, preview.Path);
            builder.Append(",\"fileName\":");
            AppendString(builder, preview.FileName);
            builder.Append(",\"exists\":").Append(preview.Exists ? "true" : "false");
            builder.Append(",\"sizeBytes\":").Append(preview.SizeBytes);
            builder.Append(",\"modifiedUtc\":");
            AppendString(builder, preview.ModifiedUtc);
            builder.Append(",\"lineCount\":").Append(preview.LineCount);
            builder.Append(",\"truncated\":").Append(preview.Truncated ? "true" : "false");
            builder.Append(",\"content\":");
            AppendString(builder, preview.Content);
            builder.Append(",\"errorMessage\":");
            AppendString(builder, preview.ErrorMessage);
            builder.Append('}');
            return builder.ToString();
        }

        /// <summary>追加当前后端、过滤和历史统计。</summary>
        private static void AppendStats(StringBuilder builder, LogKitStats stats)
        {
            builder.Append(",\"stats\":{\"loggerName\":");
            AppendString(builder, stats.LoggerName);
            builder.Append(",\"hasLogger\":").Append(stats.HasLogger ? "true" : "false");
            builder.Append(",\"enabled\":").Append(stats.Enabled ? "true" : "false");
            builder.Append(",\"minimumLevel\":");
            AppendString(builder, stats.MinimumLevel.ToString());
            builder.Append(",\"historyCount\":").Append(stats.HistoryCount);
            builder.Append(",\"droppedCount\":").Append(stats.DroppedCount).Append('}');
        }

        /// <summary>追加当前进程实际读取到的完整 Runtime Settings 对象。</summary>
        private static void AppendSettings(StringBuilder builder, LogKitSettingsSnapshot settings)
        {
            builder.Append(",\"settings\":{\"enabled\":").Append(settings.Enabled ? "true" : "false");
            builder.Append(",\"minimumLevel\":");
            AppendString(builder, settings.MinimumLevel);
            builder.Append(",\"saveLogInEditor\":").Append(settings.SaveLogInEditor ? "true" : "false");
            builder.Append(",\"saveLogInPlayer\":").Append(settings.SaveLogInPlayer ? "true" : "false");
            builder.Append(",\"enableIMGUIInPlayer\":").Append(settings.EnableImGuiInPlayer ? "true" : "false");
            builder.Append(",\"enableEncryption\":").Append(settings.EnableEncryption ? "true" : "false");
            builder.Append(",\"maxQueueSize\":").Append(settings.MaxQueueSize);
            builder.Append(",\"maxSameLogCount\":").Append(settings.MaxSameLogCount);
            builder.Append(",\"maxRetentionDays\":").Append(settings.MaxRetentionDays);
            builder.Append(",\"maxFileSizeMB\":").Append(settings.MaxFileSizeMb);
            builder.Append(",\"imguiMaxLogCount\":").Append(settings.ImGuiMaxLogCount);
            AppendStorageSettings(builder, settings);
            builder.Append('}');
        }

        /// <summary>追加目录和两个文件名，避免设置方法超过单一职责边界。</summary>
        private static void AppendStorageSettings(StringBuilder builder, LogKitSettingsSnapshot settings)
        {
            builder.Append(",\"logDirectory\":");
            AppendString(builder, settings.LogDirectory);
            builder.Append(",\"editorFileName\":");
            AppendString(builder, settings.EditorFileName);
            builder.Append(",\"playerFileName\":");
            AppendString(builder, settings.PlayerFileName);
        }

        /// <summary>追加当前宿主明确声明的实际能力，未实现项始终为 false。</summary>
        private static void AppendCapabilities(StringBuilder builder, LogKitHostEnvironmentSnapshot host)
        {
            builder.Append(",\"capabilities\":{\"settingsApply\":").Append(host.SettingsApply ? "true" : "false");
            builder.Append(",\"filePreview\":").Append(host.FilePreview ? "true" : "false");
            builder.Append(",\"fileWriter\":").Append(host.FileWriter ? "true" : "false");
            builder.Append(",\"playerImGui\":").Append(host.PlayerImGui ? "true" : "false");
            builder.Append(",\"encryption\":").Append(host.Encryption ? "true" : "false").Append('}');
        }

        /// <summary>追加解析目录以及 Editor/Player 文件元数据。</summary>
        private static void AppendFiles(
            StringBuilder builder,
            string directory,
            LogKitFileState editor,
            LogKitFileState player)
        {
            builder.Append(",\"files\":{\"directory\":");
            AppendString(builder, directory);
            builder.Append(",\"editor\":");
            AppendFile(builder, editor);
            builder.Append(",\"player\":");
            AppendFile(builder, player);
            builder.Append('}');
        }

        /// <summary>追加一个不读取正文的文件元数据对象。</summary>
        private static void AppendFile(StringBuilder builder, LogKitFileState file)
        {
            builder.Append("{\"kind\":");
            AppendString(builder, file.Kind);
            builder.Append(",\"path\":");
            AppendString(builder, file.Path);
            builder.Append(",\"fileName\":");
            AppendString(builder, file.FileName);
            builder.Append(",\"exists\":").Append(file.Exists ? "true" : "false");
            builder.Append(",\"sizeBytes\":").Append(file.SizeBytes);
            builder.Append(",\"modifiedUtc\":");
            AppendString(builder, file.ModifiedUtc);
            builder.Append('}');
        }

        /// <summary>追加有界历史对象，并保留 Runtime 总量、丢弃量和裁剪事实。</summary>
        private static void AppendHistory(
            StringBuilder builder,
            LogKitWorkbenchSnapshot snapshot,
            int entryCount)
        {
            builder.Append(",\"history\":{\"entries\":[");
            for (var index = 0; index < entryCount; index++)
            {
                if (index > 0) builder.Append(',');
                AppendEntry(builder, snapshot.Entries[index]);
            }

            builder.Append("],\"count\":").Append(entryCount);
            builder.Append(",\"totalCount\":").Append(snapshot.TotalCount);
            builder.Append(",\"droppedCount\":").Append(snapshot.DroppedCount);
            builder.Append(",\"truncated\":")
                .Append(entryCount < snapshot.TotalCount ? "true" : "false")
                .Append('}');
        }

        /// <summary>追加一条已经过字段级裁剪的内存历史。</summary>
        private static void AppendEntry(StringBuilder builder, LogKitEntry entry)
        {
            builder.Append("{\"level\":");
            AppendString(builder, entry.Level.ToString());
            builder.Append(",\"message\":");
            AppendString(builder, entry.Message);
            builder.Append(",\"context\":");
            AppendString(builder, entry.Context);
            builder.Append(",\"exceptionType\":");
            AppendString(builder, entry.ExceptionType);
            builder.Append(",\"exceptionMessage\":");
            AppendString(builder, entry.ExceptionMessage);
            builder.Append(",\"stackTrace\":");
            AppendString(builder, entry.StackTrace);
            builder.Append(",\"timestampUtc\":");
            AppendString(builder, entry.TimestampUtc);
            builder.Append('}');
        }

        /// <summary>追加经过统一转义的 JSON 字符串。</summary>
        private static void AppendString(StringBuilder builder, string value)
        {
            builder.Append('"');
            JsonHelper.AppendEscapedString(builder, value ?? string.Empty);
            builder.Append('"');
        }
    }
}
#endif
