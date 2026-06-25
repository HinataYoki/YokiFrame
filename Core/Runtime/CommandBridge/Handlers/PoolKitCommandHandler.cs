using System;
using System.Collections.Generic;
using System.Globalization;

namespace YokiFrame
{
    /// <summary>
    /// PoolKit 命令处理器：查询对象池统计、列表、详情和泄漏检查。
    /// </summary>
    public sealed class PoolKitCommandHandler : IKitCommandHandler
    {
        private const int MAX_SNAPSHOT_POOL_DETAILS = 128;
        private const int MAX_SNAPSHOT_ACTIVE_OBJECTS_PER_POOL = 64;
        private const int MAX_SNAPSHOT_INACTIVE_OBJECTS_PER_POOL = 64;

        /// <inheritdoc />
        public string KitName => "PoolKit";

        /// <inheritdoc />
        public string[] SupportedActions => new[]
        {
            "stats",
            "get_workbench_snapshot",
            "list_pools",
            "get_pool_detail",
            "get_event_history",
            "set_tracking",
            "clear_history",
            "check_leak"
        };

        /// <inheritdoc />
        public string HandleAction(string action, string payloadJson)
        {
            switch (action)
            {
                case "stats":
                    return GetStats();
                case "get_workbench_snapshot":
                    return GetWorkbenchSnapshot();
                case "list_pools":
                    return ListPools();
                case "get_pool_detail":
                    return GetPoolDetail(payloadJson);
                case "get_event_history":
                    return GetEventHistory(payloadJson);
                case "set_tracking":
                    return SetTracking(payloadJson);
                case "clear_history":
                    PoolDebugger.ClearEventHistory();
                    return "{\"cleared\":true}";
                case "check_leak":
                    return CheckLeak();
                default:
                    throw new NotSupportedException($"Unknown PoolKit action '{action}'");
            }
        }

        private static string GetStats()
        {
            var pools = new List<PoolDebugInfo>();
            PoolDebugger.GetAllPools(pools);
            var totalCount = 0;
            var totalActive = 0;
            var totalPeak = 0;
            var totalIdle = 0;
            for (var i = 0; i < pools.Count; i++)
            {
                totalCount += pools[i].TotalCount;
                totalActive += pools[i].ActiveCount;
                totalPeak += pools[i].PeakCount;
                totalIdle += pools[i].InactiveCount;
            }

            var sb = new System.Text.StringBuilder(128);
            sb.Append("{\"poolCount\":");
            sb.Append(PoolDebugger.PoolCount);
            sb.Append(",\"totalPools\":");
            sb.Append(pools.Count);
            sb.Append(",\"totalCount\":");
            sb.Append(totalCount);
            sb.Append(",\"totalActive\":");
            sb.Append(totalActive);
            sb.Append(",\"totalIdle\":");
            sb.Append(totalIdle);
            sb.Append(",\"totalPeak\":");
            sb.Append(totalPeak);
            sb.Append(",\"trackingEnabled\":");
            sb.Append(PoolDebugger.EnableTracking ? "true" : "false");
            sb.Append(",\"stackTraceEnabled\":");
            sb.Append(PoolDebugger.EnableStackTrace ? "true" : "false");
            sb.Append(",\"eventHistoryEnabled\":");
            sb.Append(PoolDebugger.EnableEventHistory ? "true" : "false");
            sb.Append(",\"eventHistoryCount\":");
            sb.Append(PoolDebugger.EventHistoryCount);
            sb.Append('}');
            return sb.ToString();
        }

        private static string GetWorkbenchSnapshot()
        {
            var stats = GetStats();
            var list = ListPools();
            var events = GetEventHistory("{}");
            var leaks = CheckLeak();
            var details = GetSnapshotPoolDetails();

            var sb = new System.Text.StringBuilder(stats.Length + list.Length + events.Length + leaks.Length + details.Length + 96);
            sb.Append("{\"stats\":");
            sb.Append(stats);
            sb.Append(",\"list\":");
            sb.Append(list);
            sb.Append(",\"events\":");
            sb.Append(events);
            sb.Append(",\"leaks\":");
            sb.Append(leaks);
            sb.Append(",\"details\":");
            sb.Append(details);
            sb.Append('}');
            return sb.ToString();
        }

        private static string ListPools()
        {
            var pools = new List<PoolDebugInfo>();
            PoolDebugger.GetAllPools(pools);

            var sb = new System.Text.StringBuilder(256);
            sb.Append("{\"pools\":[");
            for (int i = 0; i < pools.Count; i++)
            {
                if (i > 0) sb.Append(',');
                var p = pools[i];
                sb.Append("{\"name\":\"");
                sb.Append(EscapeJson(p.Name));
                sb.Append("\",\"typeName\":\"");
                sb.Append(EscapeJson(p.TypeName));
                sb.Append("\",\"totalCount\":");
                sb.Append(p.TotalCount);
                sb.Append(",\"activeCount\":");
                sb.Append(p.ActiveCount);
                sb.Append(",\"inactiveCount\":");
                sb.Append(p.InactiveCount);
                sb.Append(",\"peakCount\":");
                sb.Append(p.PeakCount);
                sb.Append(",\"maxCacheCount\":");
                sb.Append(p.MaxCacheCount);
                sb.Append(",\"usageRate\":");
                sb.Append(p.UsageRate.ToString("F2", CultureInfo.InvariantCulture));
                sb.Append(",\"healthStatus\":\"");
                sb.Append(p.HealthStatus.ToString());
                sb.Append("\"}");
            }
            sb.Append("],\"count\":");
            sb.Append(pools.Count);
            sb.Append('}');
            return sb.ToString();
        }

        private static string GetPoolDetail(string payloadJson)
        {
            var poolName = JsonHelper.ExtractString(payloadJson, "poolName");
            if (string.IsNullOrEmpty(poolName))
                throw new ArgumentException("Missing 'poolName' in payload");

            var pools = new List<PoolDebugInfo>();
            PoolDebugger.GetAllPools(pools);

            foreach (var p in pools)
            {
                if (p.Name == poolName)
                {
                    var sb = new System.Text.StringBuilder(256);
                    AppendPoolDetail(sb, p, int.MaxValue);
                    return sb.ToString();
                }
            }

            throw new KeyNotFoundException($"Pool '{poolName}' not found");
        }

        private static string GetEventHistory(string payloadJson)
        {
            var poolName = JsonHelper.ExtractString(payloadJson, "poolName");
            PoolEventType? filter = null;
            var filterText = JsonHelper.ExtractString(payloadJson, "eventType");
            if (!string.IsNullOrEmpty(filterText) && Enum.TryParse(filterText, true, out PoolEventType parsed))
                filter = parsed;

            var events = new List<PoolEvent>();
            PoolDebugger.GetEventHistory(events, filter, poolName);

            var sb = new System.Text.StringBuilder(256);
            sb.Append("{\"events\":[");
            for (var i = 0; i < events.Count; i++)
            {
                if (i > 0) sb.Append(',');
                var evt = events[i];
                sb.Append("{\"eventType\":\"");
                sb.Append(evt.EventType.ToString());
                sb.Append("\",\"timestamp\":");
                sb.Append(evt.Timestamp.ToString("F2", CultureInfo.InvariantCulture));
                sb.Append(",\"poolName\":\"");
                sb.Append(EscapeJson(evt.PoolName));
                sb.Append("\",\"objectName\":\"");
                sb.Append(EscapeJson(evt.ObjectName));
                sb.Append("\",\"source\":\"");
                sb.Append(EscapeJson(evt.Source));
                sb.Append("\",\"sourceFile\":\"");
                sb.Append(EscapeJson(evt.SourceFile));
                sb.Append("\",\"sourceLine\":");
                sb.Append(evt.SourceLine);
                sb.Append('}');
            }
            sb.Append("],\"count\":");
            sb.Append(events.Count);
            sb.Append('}');
            return sb.ToString();
        }

        private static string SetTracking(string payloadJson)
        {
            var trackingEnabled = ExtractBool(payloadJson, "trackingEnabled", PoolDebugger.EnableTracking);
            var eventHistoryEnabled = ExtractBool(payloadJson, "eventHistoryEnabled", PoolDebugger.EnableEventHistory);
            var stackTraceEnabled = ExtractBool(payloadJson, "stackTraceEnabled", PoolDebugger.EnableStackTrace);

            if (stackTraceEnabled)
            {
                trackingEnabled = true;
                eventHistoryEnabled = true;
            }

            PoolDebugger.EnableTracking = trackingEnabled;
            PoolDebugger.EnableEventHistory = eventHistoryEnabled;
            PoolDebugger.EnableStackTrace = stackTraceEnabled;
            return GetStats();
        }

        private static string CheckLeak()
        {
            var pools = new List<PoolDebugInfo>();
            PoolDebugger.GetAllPools(pools);

            var sb = new System.Text.StringBuilder(256);
            var count = 0;
            sb.Append("{\"suspectedLeaks\":[");
            for (var i = 0; i < pools.Count; i++)
            {
                var pool = pools[i];
                if (pool.ActiveCount <= 0)
                    continue;

                if (count > 0) sb.Append(',');
                sb.Append("{\"poolName\":\"");
                sb.Append(EscapeJson(pool.Name));
                sb.Append("\",\"activeCount\":");
                sb.Append(pool.ActiveCount);
                sb.Append(",\"peakCount\":");
                sb.Append(pool.PeakCount);
                sb.Append('}');
                count++;
            }

            sb.Append("],\"count\":");
            sb.Append(count);
            sb.Append(",\"trackingEnabled\":");
            sb.Append(PoolDebugger.EnableTracking ? "true" : "false");
            sb.Append('}');
            return sb.ToString();
        }

        private static string GetSnapshotPoolDetails()
        {
            var pools = new List<PoolDebugInfo>();
            PoolDebugger.GetAllPools(pools);

            var sb = new System.Text.StringBuilder(256);
            sb.Append("{\"pools\":[");
            var count = 0;
            for (var i = 0; i < pools.Count && count < MAX_SNAPSHOT_POOL_DETAILS; i++)
            {
                if (count > 0) sb.Append(',');
                AppendPoolDetail(sb, pools[i], MAX_SNAPSHOT_ACTIVE_OBJECTS_PER_POOL);
                count++;
            }

            sb.Append("],\"count\":");
            sb.Append(count);
            sb.Append(",\"total\":");
            sb.Append(pools.Count);
            sb.Append(",\"truncated\":");
            sb.Append(pools.Count > count ? "true" : "false");
            sb.Append('}');
            return sb.ToString();
        }

        private static void AppendPoolDetail(System.Text.StringBuilder sb, PoolDebugInfo p, int maxActiveObjects)
        {
            var activeLimit = maxActiveObjects < 0 ? 0 : maxActiveObjects;
            var activeObjectCount = p.ActiveObjects.Count;
            var visibleActiveObjectCount = Math.Min(activeObjectCount, activeLimit);
            var inactiveLimit = maxActiveObjects == int.MaxValue ? int.MaxValue : MAX_SNAPSHOT_INACTIVE_OBJECTS_PER_POOL;
            var inactiveObjectCount = p.InactiveObjects.Count;
            var visibleInactiveObjectCount = Math.Min(inactiveObjectCount, inactiveLimit);

            sb.Append("{\"name\":\"");
            sb.Append(EscapeJson(p.Name));
            sb.Append("\",\"typeName\":\"");
            sb.Append(EscapeJson(p.TypeName));
            sb.Append("\",\"totalCount\":");
            sb.Append(p.TotalCount);
            sb.Append(",\"activeCount\":");
            sb.Append(p.ActiveCount);
            sb.Append(",\"peakCount\":");
            sb.Append(p.PeakCount);
            sb.Append(",\"inactiveCount\":");
            sb.Append(p.InactiveCount);
            sb.Append(",\"maxCacheCount\":");
            sb.Append(p.MaxCacheCount);
            sb.Append(",\"activeObjectTotal\":");
            sb.Append(activeObjectCount);
            sb.Append(",\"activeObjectTruncated\":");
            sb.Append(activeObjectCount > visibleActiveObjectCount ? "true" : "false");
            sb.Append(",\"inactiveObjectTotal\":");
            sb.Append(inactiveObjectCount);
            sb.Append(",\"inactiveObjectTruncated\":");
            sb.Append(inactiveObjectCount > visibleInactiveObjectCount ? "true" : "false");
            sb.Append(",\"activeObjects\":[");
            for (int i = 0; i < visibleActiveObjectCount; i++)
            {
                if (i > 0) sb.Append(',');
                var obj = p.ActiveObjects[i];
                sb.Append("{\"objectName\":\"");
                sb.Append(EscapeJson(obj.Obj != null ? obj.Obj.ToString() : "null"));
                sb.Append("\",\"spawnTime\":");
                sb.Append(obj.SpawnTime.ToString("F2", CultureInfo.InvariantCulture));
                sb.Append(",\"stackTrace\":\"");
                sb.Append(EscapeJson(obj.StackTrace));
                sb.Append("\",\"sourceFile\":\"");
                sb.Append(EscapeJson(obj.SourceFile));
                sb.Append("\",\"sourceLine\":");
                sb.Append(obj.SourceLine);
                sb.Append('}');
            }
            sb.Append("],\"inactiveObjects\":[");
            for (var i = 0; i < visibleInactiveObjectCount; i++)
            {
                if (i > 0) sb.Append(',');
                var obj = p.InactiveObjects[i];
                sb.Append("{\"objectName\":\"");
                sb.Append(EscapeJson(obj.Obj != null ? obj.Obj.ToString() : "null"));
                sb.Append("\"}");
            }
            sb.Append("]}");
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

        private static string EscapeJson(string s)
        {
            return JsonHelper.EscapeString(s);
        }
    }
}
