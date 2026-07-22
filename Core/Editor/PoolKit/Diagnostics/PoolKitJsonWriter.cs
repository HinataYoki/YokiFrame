#if UNITY_EDITOR || (GODOT && TOOLS)
using System.Globalization;
using System.Text;

namespace YokiFrame
{
    /// <summary>把 PoolKit 有界快照写成不依赖宿主序列化器的稳定 JSON。</summary>
    internal static class PoolKitJsonWriter
    {
        /// <summary>创建固定 schema 的 PoolKit Workbench payload。</summary>
        internal static string WriteWorkbench(PoolKitWorkbenchSnapshot snapshot)
        {
            var builder = new StringBuilder(4096);
            builder.Append("{\"schemaVersion\":1,\"version\":").Append(snapshot.Version);
            AppendStats(builder, snapshot.Stats);
            AppendPools(builder, snapshot);
            AppendEvents(builder, snapshot);
            AppendLeaks(builder, snapshot);
            builder.Append('}');
            return builder.ToString();
        }

        /// <summary>追加聚合统计和诊断开关。</summary>
        private static void AppendStats(StringBuilder builder, PoolKitStatsSnapshot stats)
        {
            builder.Append(",\"stats\":{\"poolCount\":").Append(stats.PoolCount);
            builder.Append(",\"totalCount\":").Append(stats.TotalCount);
            builder.Append(",\"totalActive\":").Append(stats.TotalActive);
            builder.Append(",\"totalInactive\":").Append(stats.TotalInactive);
            builder.Append(",\"totalPeak\":").Append(stats.TotalPeak);
            builder.Append(",\"trackingEnabled\":").Append(ToJson(stats.TrackingEnabled));
            builder.Append(",\"stackTraceEnabled\":").Append(ToJson(stats.StackTraceEnabled));
            builder.Append(",\"eventHistoryEnabled\":").Append(ToJson(stats.EventHistoryEnabled));
            builder.Append(",\"eventHistoryCount\":").Append(stats.EventHistoryCount).Append('}');
        }

        /// <summary>追加对象池列表和顶层裁剪事实。</summary>
        private static void AppendPools(StringBuilder builder, PoolKitWorkbenchSnapshot snapshot)
        {
            builder.Append(",\"pools\":[");
            for (var index = 0; index < snapshot.Pools.Length; index++)
            {
                if (index > 0) builder.Append(',');
                AppendPool(builder, snapshot.Pools[index]);
            }

            builder.Append("],\"poolCount\":").Append(snapshot.Pools.Length);
            builder.Append(",\"poolTotal\":").Append(snapshot.Stats.PoolCount);
            builder.Append(",\"poolsTruncated\":").Append(ToJson(snapshot.PoolsTruncated));
        }

        /// <summary>追加单个对象池指标与有界对象明细。</summary>
        private static void AppendPool(StringBuilder builder, PoolKitPoolSnapshot pool)
        {
            builder.Append("{\"poolId\":");
            AppendString(builder, pool.PoolId);
            builder.Append(",\"name\":");
            AppendString(builder, pool.Name);
            builder.Append(",\"typeName\":");
            AppendString(builder, pool.TypeName);
            builder.Append(",\"totalCount\":").Append(pool.TotalCount);
            builder.Append(",\"activeCount\":").Append(pool.ActiveCount);
            builder.Append(",\"inactiveCount\":").Append(pool.InactiveCount);
            builder.Append(",\"peakCount\":").Append(pool.PeakCount);
            builder.Append(",\"maxCacheCount\":").Append(pool.MaxCacheCount);
            builder.Append(",\"usageRate\":").Append(pool.UsageRate.ToString("F4", CultureInfo.InvariantCulture));
            builder.Append(",\"healthStatus\":");
            AppendString(builder, pool.HealthStatus);
            AppendObjects(builder, pool);
            builder.Append('}');
        }

        /// <summary>追加借出和池内对象以及各自总量/裁剪状态。</summary>
        private static void AppendObjects(StringBuilder builder, PoolKitPoolSnapshot pool)
        {
            builder.Append(",\"activeObjectTotal\":").Append(pool.ActiveObjectTotal);
            builder.Append(",\"activeObjectTruncated\":").Append(ToJson(pool.ActiveObjectTruncated));
            builder.Append(",\"inactiveObjectTotal\":").Append(pool.InactiveObjectTotal);
            builder.Append(",\"inactiveObjectTruncated\":").Append(ToJson(pool.InactiveObjectTruncated));
            builder.Append(",\"activeObjects\":[");
            for (var index = 0; index < pool.ActiveObjects.Length; index++)
            {
                if (index > 0) builder.Append(',');
                AppendObject(builder, pool.ActiveObjects[index], true);
            }

            builder.Append("],\"inactiveObjects\":[");
            for (var index = 0; index < pool.InactiveObjects.Length; index++)
            {
                if (index > 0) builder.Append(',');
                AppendObject(builder, pool.InactiveObjects[index], false);
            }

            builder.Append(']');
        }

        /// <summary>追加单个对象显示信息。</summary>
        private static void AppendObject(StringBuilder builder, PoolKitObjectSnapshot item, bool active)
        {
            builder.Append("{\"objectName\":");
            AppendString(builder, item.ObjectName);
            if (active)
            {
                builder.Append(",\"spawnTime\":").Append(item.SpawnTime.ToString("F2", CultureInfo.InvariantCulture));
                builder.Append(",\"sourceFile\":");
                AppendString(builder, item.SourceFile);
                builder.Append(",\"sourceLine\":").Append(item.SourceLine);
            }

            builder.Append('}');
        }

        /// <summary>追加最新优先的事件流与裁剪状态。</summary>
        private static void AppendEvents(StringBuilder builder, PoolKitWorkbenchSnapshot snapshot)
        {
            builder.Append(",\"events\":[");
            for (var index = 0; index < snapshot.Events.Length; index++)
            {
                if (index > 0) builder.Append(',');
                AppendEvent(builder, snapshot.Events[index]);
            }

            builder.Append("],\"eventCount\":").Append(snapshot.Events.Length);
            builder.Append(",\"eventTotal\":").Append(snapshot.Stats.EventHistoryCount);
            builder.Append(",\"eventsTruncated\":").Append(ToJson(snapshot.EventsTruncated));
        }

        /// <summary>追加一条对象池事件。</summary>
        private static void AppendEvent(StringBuilder builder, PoolKitEventSnapshot item)
        {
            builder.Append("{\"eventType\":");
            AppendString(builder, item.EventType);
            builder.Append(",\"timestamp\":").Append(item.Timestamp.ToString("F2", CultureInfo.InvariantCulture));
            builder.Append(",\"poolId\":");
            AppendString(builder, item.PoolId);
            builder.Append(",\"poolName\":");
            AppendString(builder, item.PoolName);
            builder.Append(",\"objectName\":");
            AppendString(builder, item.ObjectName);
            builder.Append(",\"sourceFile\":");
            AppendString(builder, item.SourceFile);
            builder.Append(",\"sourceLine\":").Append(item.SourceLine).Append('}');
        }

        /// <summary>追加疑似未归还池摘要；它只表达诊断线索，不宣称真实泄漏。</summary>
        private static void AppendLeaks(StringBuilder builder, PoolKitWorkbenchSnapshot snapshot)
        {
            builder.Append(",\"leaks\":{\"suspectedLeaks\":[");
            for (var index = 0; index < snapshot.SuspectedLeaks.Length; index++)
            {
                if (index > 0) builder.Append(',');
                PoolKitLeakSnapshot leak = snapshot.SuspectedLeaks[index];
                builder.Append("{\"poolId\":");
                AppendString(builder, leak.PoolId);
                builder.Append(",\"poolName\":");
                AppendString(builder, leak.PoolName);
                builder.Append(",\"activeCount\":").Append(leak.ActiveCount);
                builder.Append(",\"peakCount\":").Append(leak.PeakCount).Append('}');
            }

            builder.Append("],\"count\":").Append(snapshot.SuspectedLeaks.Length);
            builder.Append(",\"total\":").Append(snapshot.SuspectedLeakTotal);
            builder.Append(",\"truncated\":").Append(ToJson(snapshot.SuspectedLeaksTruncated));
            builder.Append(",\"trackingEnabled\":").Append(ToJson(snapshot.Stats.TrackingEnabled)).Append("}");
        }

        /// <summary>追加统一转义的 JSON 字符串。</summary>
        private static void AppendString(StringBuilder builder, string value)
        {
            builder.Append('"').Append(JsonHelper.EscapeString(value ?? string.Empty)).Append('"');
        }

        /// <summary>返回 JSON 布尔字面量。</summary>
        private static string ToJson(bool value) => value ? "true" : "false";
    }
}
#endif
