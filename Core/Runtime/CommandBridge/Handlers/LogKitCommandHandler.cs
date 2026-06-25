using System;
using System.Collections.Generic;
using System.Text;

namespace YokiFrame
{
    /// <summary>
    /// LogKit 命令处理器：查询日志配置、最近日志和工作台快照。
    /// </summary>
    public sealed class LogKitCommandHandler : IKitCommandHandler
    {
        /// <inheritdoc />
        public string KitName => "LogKit";

        /// <inheritdoc />
        public string[] SupportedActions => new[]
        {
            "stats",
            "get_settings",
            "set_settings",
            "reset_settings",
            "get_history",
            "get_workbench_snapshot",
            "clear_history"
        };

        /// <inheritdoc />
        public string HandleAction(string action, string payloadJson)
        {
            switch (action)
            {
                case "stats":
                    return GetStats();
                case "get_settings":
                    return GetSettings();
                case "set_settings":
                    LogKitSettings.ApplyPayload(payloadJson);
                    return GetSettings();
                case "reset_settings":
                    LogKitSettings.ResetToDefaults();
                    return GetSettings();
                case "get_history":
                    return GetHistory();
                case "get_workbench_snapshot":
                    return GetWorkbenchSnapshot();
                case "clear_history":
                    LogKit.ClearHistory();
                    return "{\"cleared\":true}";
                default:
                    throw new NotSupportedException($"Unknown LogKit action '{action}'");
            }
        }

        private static string GetWorkbenchSnapshot()
        {
            var stats = GetStats();
            var settings = GetSettings();
            var history = GetHistory();

            var sb = new StringBuilder(stats.Length + settings.Length + history.Length + 48);
            sb.Append("{\"stats\":");
            sb.Append(stats);
            sb.Append(",\"settings\":");
            sb.Append(settings);
            sb.Append(",\"history\":");
            sb.Append(history);
            sb.Append('}');
            return sb.ToString();
        }

        private static string GetSettings()
        {
            return LogKitSettings.BuildJson();
        }

        private static string GetStats()
        {
            var stats = LogKit.GetStats();
            var sb = new StringBuilder(160);
            sb.Append("{\"loggerName\":\"");
            sb.Append(JsonHelper.EscapeString(stats.LoggerName));
            sb.Append("\",\"hasLogger\":");
            sb.Append(stats.HasLogger ? "true" : "false");
            sb.Append(",\"enabled\":");
            sb.Append(stats.Enabled ? "true" : "false");
            sb.Append(",\"minimumLevel\":\"");
            sb.Append(JsonHelper.EscapeString(stats.MinimumLevel.ToString()));
            sb.Append("\",\"historyCount\":");
            sb.Append(stats.HistoryCount);
            sb.Append(",\"droppedCount\":");
            sb.Append(stats.DroppedCount);
            sb.Append('}');
            return sb.ToString();
        }

        private static string GetHistory()
        {
            var entries = new List<LogKitEntry>(128);
            LogKit.GetHistory(entries);

            var sb = new StringBuilder(256);
            sb.Append("{\"entries\":[");
            for (var i = 0; i < entries.Count; i++)
            {
                if (i > 0) sb.Append(',');
                AppendEntry(sb, entries[i]);
            }
            sb.Append("],\"count\":");
            sb.Append(entries.Count);
            sb.Append('}');
            return sb.ToString();
        }

        private static void AppendEntry(StringBuilder sb, LogKitEntry entry)
        {
            sb.Append("{\"level\":\"");
            sb.Append(JsonHelper.EscapeString(entry.Level.ToString()));
            sb.Append("\",\"message\":\"");
            sb.Append(JsonHelper.EscapeString(entry.Message));
            sb.Append("\",\"context\":\"");
            sb.Append(JsonHelper.EscapeString(entry.Context));
            sb.Append("\",\"exceptionType\":\"");
            sb.Append(JsonHelper.EscapeString(entry.ExceptionType));
            sb.Append("\",\"exceptionMessage\":\"");
            sb.Append(JsonHelper.EscapeString(entry.ExceptionMessage));
            sb.Append("\",\"stackTrace\":\"");
            sb.Append(JsonHelper.EscapeString(entry.StackTrace));
            sb.Append("\",\"timestampUtc\":\"");
            sb.Append(JsonHelper.EscapeString(entry.TimestampUtc));
            sb.Append("\"}");
        }
    }
}
