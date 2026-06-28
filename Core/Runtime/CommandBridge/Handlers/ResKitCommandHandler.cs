using System;
using System.Collections.Generic;
using System.Text;

namespace YokiFrame
{
    /// <summary>
    /// ResKit 命令处理器：查询资源缓存、引用计数和卸载历史。
    /// </summary>
    public sealed class ResKitCommandHandler : IKitCommandHandler, IKitSnapshotInvalidationProvider
    {
        /// <inheritdoc />
        public string KitName => "ResKit";

        /// <inheritdoc />
        public string[] SupportedActions => new[]
        {
            "stats",
            "get_workbench_snapshot",
            "list_resources",
            "get_resource_detail",
            "diagnose_resource",
            "get_unload_history",
            "clear_history",
            "set_tracking"
        };

        /// <inheritdoc />
        public string GetSnapshotInvalidationKey()
        {
            return ResKit.DiagnosticVersion.ToString();
        }

        /// <inheritdoc />
        public string HandleAction(string action, string payloadJson)
        {
            switch (action)
            {
                case "stats":
                    return GetStats();
                case "get_workbench_snapshot":
                    return GetWorkbenchSnapshot();
                case "list_resources":
                    return ListResources();
                case "get_resource_detail":
                    return GetResourceDetail(payloadJson);
                case "diagnose_resource":
                    return DiagnoseResource(payloadJson);
                case "get_unload_history":
                    return GetUnloadHistory();
                case "clear_history":
                    ResKit.ClearUnloadHistory();
                    return "{\"cleared\":true}";
                case "set_tracking":
                    ResKit.EnableLoadLocationTracking = ExtractBool(payloadJson, "loadLocationTrackingEnabled", ResKit.EnableLoadLocationTracking);
                    return GetStats();
                default:
                    throw new NotSupportedException($"Unknown ResKit action '{action}'");
            }
        }

        private static string GetStats()
        {
            var sb = new StringBuilder(128);
            sb.Append("{\"providerName\":\"");
            sb.Append(JsonHelper.EscapeString(ResKit.ProviderName));
            sb.Append("\",\"loadedCount\":");
            sb.Append(ResKit.LoadedCount);
            sb.Append(",\"totalRefCount\":");
            sb.Append(ResKit.TotalRefCount);
            sb.Append(",\"unloadHistoryCount\":");
            sb.Append(ResKit.UnloadHistoryCount);
            sb.Append(",\"loadLocationTrackingEnabled\":");
            sb.Append(ResKit.EnableLoadLocationTracking ? "true" : "false");
            sb.Append('}');
            return sb.ToString();
        }

        private static string GetWorkbenchSnapshot()
        {
            var stats = GetStats();
            var list = ListResources();
            var history = GetUnloadHistory();

            var sb = new StringBuilder(stats.Length + list.Length + history.Length + 48);
            sb.Append("{\"stats\":");
            sb.Append(stats);
            sb.Append(",\"list\":");
            sb.Append(list);
            sb.Append(",\"history\":");
            sb.Append(history);
            sb.Append('}');
            return sb.ToString();
        }

        private static string ListResources()
        {
            var resources = new List<ResDebugInfo>();
            ResKit.GetLoadedAssets(resources);

            var sb = new StringBuilder(256);
            sb.Append("{\"resources\":[");
            for (var i = 0; i < resources.Count; i++)
            {
                if (i > 0) sb.Append(',');
                AppendResource(sb, resources[i]);
            }
            sb.Append("],\"count\":");
            sb.Append(resources.Count);
            sb.Append(",\"providerName\":\"");
            sb.Append(JsonHelper.EscapeString(ResKit.ProviderName));
            sb.Append("\"}");
            return sb.ToString();
        }

        private static string GetResourceDetail(string payloadJson)
        {
            var path = JsonHelper.ExtractString(payloadJson, "path");
            var typeName = JsonHelper.ExtractString(payloadJson, "typeName");
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("Missing 'path' in payload");

            var resources = new List<ResDebugInfo>();
            ResKit.GetLoadedAssets(resources);
            for (var i = 0; i < resources.Count; i++)
            {
                var item = resources[i];
                if (item.Path != path)
                    continue;

                if (!string.IsNullOrEmpty(typeName) && item.TypeName != typeName)
                    continue;

                var sb = new StringBuilder(128);
                AppendResource(sb, item);
                return sb.ToString();
            }

            throw new KeyNotFoundException($"Resource '{path}' not found");
        }

        private static string DiagnoseResource(string payloadJson)
        {
            var path = JsonHelper.ExtractString(payloadJson, "path");
            var typeName = JsonHelper.ExtractString(payloadJson, "typeName");
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("Missing 'path' in payload");

            var resources = new List<ResDebugInfo>();
            ResKit.GetLoadedAssets(resources);
            ResDebugInfo matched = null;
            for (var i = 0; i < resources.Count; i++)
            {
                var item = resources[i];
                if (item.Path != path)
                    continue;

                if (!string.IsNullOrEmpty(typeName) && item.TypeName != typeName)
                    continue;

                matched = item;
                break;
            }

            var unloads = new List<ResUnloadRecord>();
            ResKit.GetUnloadHistory(unloads);
            var relatedUnloadCount = 0;
            ResUnloadRecord latestUnload = null;
            for (var i = 0; i < unloads.Count; i++)
            {
                var item = unloads[i];
                if (item.Path != path)
                    continue;

                if (!string.IsNullOrEmpty(typeName) && item.TypeName != typeName)
                    continue;

                relatedUnloadCount++;
                if (latestUnload == null)
                    latestUnload = item;
            }

            var sb = new StringBuilder(256);
            sb.Append("{\"path\":\"");
            sb.Append(JsonHelper.EscapeString(path));
            sb.Append("\",\"typeName\":\"");
            sb.Append(JsonHelper.EscapeString(typeName));
            sb.Append("\",\"isLoaded\":");
            sb.Append(matched != null ? "true" : "false");
            sb.Append(",\"providerName\":\"");
            sb.Append(JsonHelper.EscapeString(matched != null ? matched.ProviderName : ResKit.ProviderName));
            sb.Append("\",\"loadedCount\":");
            sb.Append(ResKit.LoadedCount);
            sb.Append(",\"totalRefCount\":");
            sb.Append(ResKit.TotalRefCount);
            sb.Append(",\"relatedUnloadCount\":");
            sb.Append(relatedUnloadCount);
            sb.Append(",\"resource\":");
            if (matched != null)
                AppendResource(sb, matched);
            else
                sb.Append("null");
            sb.Append(",\"latestUnload\":");
            if (latestUnload != null)
                AppendUnloadRecord(sb, latestUnload);
            else
                sb.Append("null");
            sb.Append('}');
            return sb.ToString();
        }

        private static string GetUnloadHistory()
        {
            var records = new List<ResUnloadRecord>();
            ResKit.GetUnloadHistory(records);

            var sb = new StringBuilder(256);
            sb.Append("{\"history\":[");
            for (var i = 0; i < records.Count; i++)
            {
                if (i > 0) sb.Append(',');
                AppendUnloadRecord(sb, records[i]);
            }
            sb.Append("],\"count\":");
            sb.Append(records.Count);
            sb.Append('}');
            return sb.ToString();
        }

        private static void AppendUnloadRecord(StringBuilder sb, ResUnloadRecord item)
        {
            sb.Append("{\"path\":\"");
            sb.Append(JsonHelper.EscapeString(item.Path));
            sb.Append("\",\"typeName\":\"");
            sb.Append(JsonHelper.EscapeString(item.TypeName));
            sb.Append("\",\"providerName\":\"");
            sb.Append(JsonHelper.EscapeString(item.ProviderName));
            sb.Append("\",\"unloadTimeUtc\":\"");
            sb.Append(JsonHelper.EscapeString(item.UnloadTimeUtc));
            sb.Append("\"}");
        }

        private static void AppendResource(StringBuilder sb, ResDebugInfo item)
        {
            sb.Append("{\"path\":\"");
            sb.Append(JsonHelper.EscapeString(item.Path));
            sb.Append("\",\"typeName\":\"");
            sb.Append(JsonHelper.EscapeString(item.TypeName));
            sb.Append("\",\"refCount\":");
            sb.Append(item.RefCount);
            sb.Append(",\"isDone\":");
            sb.Append(item.IsDone ? "true" : "false");
            sb.Append(",\"providerName\":\"");
            sb.Append(JsonHelper.EscapeString(item.ProviderName));
            sb.Append("\",\"source\":\"");
            sb.Append(JsonHelper.EscapeString(item.Source));
            sb.Append("\",\"sourceFile\":\"");
            sb.Append(JsonHelper.EscapeString(item.SourceFile));
            sb.Append("\",\"sourceLine\":");
            sb.Append(item.SourceLine);
            sb.Append('}');
        }

        private static bool ExtractBool(string json, string fieldName, bool fallback)
        {
            if (string.IsNullOrEmpty(json))
                return fallback;

            var search = "\"" + fieldName + "\"";
            var idx = json.IndexOf(search, StringComparison.Ordinal);
            if (idx < 0)
                return fallback;

            idx += search.Length;
            while (idx < json.Length && (json[idx] == ' ' || json[idx] == ':' || json[idx] == '\t' || json[idx] == '\r' || json[idx] == '\n'))
                idx++;

            if (idx >= json.Length)
                return fallback;

            if (json.IndexOf("true", idx, StringComparison.OrdinalIgnoreCase) == idx)
                return true;

            if (json.IndexOf("false", idx, StringComparison.OrdinalIgnoreCase) == idx)
                return false;

            return fallback;
        }
    }
}
