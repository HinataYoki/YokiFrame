using System.Text.Json;

namespace YokiFrame.Tooling.Application.Models.PoolKit;

/// <summary>把 Runtime PoolKit payload 转换为稳定强类型 read model。</summary>
internal static class WorkbenchPoolKitStateParser
{
    /// <summary>解析完整 payload；无效输入转换为空状态并保留 stale 原因。</summary>
    internal static WorkbenchPoolKitState Parse(WorkbenchPoolKitDataSource dataSource)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        if (string.IsNullOrWhiteSpace(dataSource.RawPayloadJson))
        {
            return CreateEmpty(dataSource, "PoolKit payload is empty.");
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(dataSource.RawPayloadJson);
            JsonElement root = document.RootElement;
            if (!TryGetArray(root, "pools", out JsonElement pools))
            {
                return CreateEmpty(dataSource, "PoolKit payload must contain a pools array.");
            }

            return ParseRoot(root, pools, dataSource);
        }
        catch (JsonException exception)
        {
            return CreateEmpty(dataSource, "PoolKit payload is invalid JSON: " + exception.Message);
        }
    }

    /// <summary>解析已经确认顶层结构的 PoolKit payload。</summary>
    private static WorkbenchPoolKitState ParseRoot(
        JsonElement root,
        JsonElement poolArray,
        WorkbenchPoolKitDataSource dataSource)
    {
        IReadOnlyList<WorkbenchPoolKitPool> pools = ReadPools(poolArray);
        IReadOnlyList<WorkbenchPoolKitEvent> events = ReadEvents(root);
        WorkbenchPoolKitStats stats = ReadStats(root, pools, events.Count);
        WorkbenchPoolKitLeakReport leaks = ReadLeaks(root);
        return new WorkbenchPoolKitState(
            dataSource,
            ReadInt64(root, "version"),
            stats,
            pools,
            events,
            leaks,
            ReadInt32(root, "poolTotal", stats.PoolCount),
            ReadInt32(root, "eventTotal", stats.EventHistoryCount),
            ReadBoolean(root, "poolsTruncated"),
            ReadBoolean(root, "eventsTruncated"));
    }

    /// <summary>读取聚合统计；缺失字段由列表安全回推。</summary>
    private static WorkbenchPoolKitStats ReadStats(
        JsonElement root,
        IReadOnlyList<WorkbenchPoolKitPool> pools,
        int eventCount)
    {
        JsonElement stats = TryGetObject(root, "stats", out JsonElement value) ? value : default;
        return new WorkbenchPoolKitStats(
            ReadInt32(stats, "poolCount", pools.Count),
            ReadInt32(stats, "totalCount", pools.Sum(static item => item.TotalCount)),
            ReadInt32(stats, "totalActive", pools.Sum(static item => item.ActiveCount)),
            ReadInt32(stats, "totalInactive", pools.Sum(static item => item.InactiveCount)),
            ReadInt32(stats, "totalPeak", pools.Sum(static item => item.PeakCount)),
            ReadBoolean(stats, "trackingEnabled"),
            ReadBoolean(stats, "stackTraceEnabled"),
            ReadBoolean(stats, "eventHistoryEnabled"),
            ReadInt32(stats, "eventHistoryCount", eventCount));
    }

    /// <summary>读取对象池数组并为同名池生成确定性帧内身份。</summary>
    private static IReadOnlyList<WorkbenchPoolKitPool> ReadPools(JsonElement array)
    {
        List<WorkbenchPoolKitPool> pools = new();
        Dictionary<string, int> occurrences = new(StringComparer.Ordinal);
        foreach (JsonElement item in array.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;
            string name = ReadString(item, "name");
            string typeName = ReadString(item, "typeName");
            string poolId = ReadString(item, "poolId");
            string baseIdentity = name + "\u001f" + typeName;
            int occurrence = occurrences.TryGetValue(baseIdentity, out int current) ? current : 0;
            occurrences[baseIdentity] = occurrence + 1;
            string identity = string.IsNullOrWhiteSpace(poolId)
                ? baseIdentity + "\u001f" + occurrence
                : poolId;
            pools.Add(ReadPool(item, identity, poolId));
        }

        return pools;
    }

    /// <summary>读取单个对象池指标与对象明细。</summary>
    private static WorkbenchPoolKitPool ReadPool(JsonElement item, string identity, string poolId)
    {
        return new WorkbenchPoolKitPool(
            identity,
            ReadString(item, "name"),
            ReadString(item, "typeName"),
            ReadInt32(item, "totalCount"),
            ReadInt32(item, "activeCount"),
            ReadInt32(item, "inactiveCount"),
            ReadInt32(item, "peakCount"),
            ReadInt32(item, "maxCacheCount", -1),
            ReadDouble(item, "usageRate"),
            ReadString(item, "healthStatus"),
            ReadInt32(item, "activeObjectTotal"),
            ReadBoolean(item, "activeObjectTruncated"),
            ReadInt32(item, "inactiveObjectTotal"),
            ReadBoolean(item, "inactiveObjectTruncated"),
            ReadObjects(item, "activeObjects"),
            ReadObjects(item, "inactiveObjects"))
        {
            PoolId = poolId
        };
    }

    /// <summary>读取借出或池内对象数组。</summary>
    private static IReadOnlyList<WorkbenchPoolKitObject> ReadObjects(JsonElement parent, string name)
    {
        if (!TryGetArray(parent, name, out JsonElement array)) return Array.Empty<WorkbenchPoolKitObject>();
        List<WorkbenchPoolKitObject> objects = new();
        foreach (JsonElement item in array.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;
            objects.Add(new WorkbenchPoolKitObject(
                ReadString(item, "objectName"),
                ReadDouble(item, "spawnTime"),
                ReadString(item, "sourceFile"),
                ReadInt32(item, "sourceLine")));
        }

        return objects;
    }

    /// <summary>读取最新优先事件流。</summary>
    private static IReadOnlyList<WorkbenchPoolKitEvent> ReadEvents(JsonElement root)
    {
        if (!TryGetArray(root, "events", out JsonElement array)) return Array.Empty<WorkbenchPoolKitEvent>();
        List<WorkbenchPoolKitEvent> events = new();
        foreach (JsonElement item in array.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;
            events.Add(new WorkbenchPoolKitEvent(
                ReadString(item, "eventType"),
                ReadDouble(item, "timestamp"),
                ReadString(item, "poolName"),
                ReadString(item, "objectName"),
                ReadString(item, "sourceFile"),
                ReadInt32(item, "sourceLine"))
            {
                PoolId = ReadString(item, "poolId")
            });
        }

        return events;
    }

    /// <summary>读取疑似未归还对象报告。</summary>
    private static WorkbenchPoolKitLeakReport ReadLeaks(JsonElement root)
    {
        if (!TryGetObject(root, "leaks", out JsonElement leaks))
        {
            return new WorkbenchPoolKitLeakReport(Array.Empty<WorkbenchPoolKitSuspectedLeak>(), 0, false);
        }

        List<WorkbenchPoolKitSuspectedLeak> rows = new();
        if (TryGetArray(leaks, "suspectedLeaks", out JsonElement array))
        {
            foreach (JsonElement item in array.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Object)
                {
                    rows.Add(new WorkbenchPoolKitSuspectedLeak(
                        ReadString(item, "poolName"),
                        ReadInt32(item, "activeCount"),
                        ReadInt32(item, "peakCount"))
                    {
                        PoolId = ReadString(item, "poolId")
                    });
                }
            }
        }

        int visibleCount = ReadInt32(leaks, "count", rows.Count);
        return new WorkbenchPoolKitLeakReport(
            rows,
            visibleCount,
            ReadBoolean(leaks, "trackingEnabled"))
        {
            Total = ReadInt32(leaks, "total", visibleCount),
            Truncated = ReadBoolean(leaks, "truncated")
        };
    }

    /// <summary>创建保留来源证据的安全空状态。</summary>
    private static WorkbenchPoolKitState CreateEmpty(WorkbenchPoolKitDataSource source, string reason)
    {
        return new WorkbenchPoolKitState(
            source.WithStaleReason(reason), 0L,
            new WorkbenchPoolKitStats(0, 0, 0, 0, 0, false, false, false, 0),
            Array.Empty<WorkbenchPoolKitPool>(),
            Array.Empty<WorkbenchPoolKitEvent>(),
            new WorkbenchPoolKitLeakReport(Array.Empty<WorkbenchPoolKitSuspectedLeak>(), 0, false),
            0, 0, false, false);
    }

    /// <summary>尝试读取对象属性。</summary>
    private static bool TryGetObject(JsonElement parent, string name, out JsonElement value)
    {
        value = default;
        return parent.ValueKind == JsonValueKind.Object
            && parent.TryGetProperty(name, out value)
            && value.ValueKind == JsonValueKind.Object;
    }

    /// <summary>尝试读取数组属性。</summary>
    private static bool TryGetArray(JsonElement parent, string name, out JsonElement value)
    {
        value = default;
        return parent.ValueKind == JsonValueKind.Object
            && parent.TryGetProperty(name, out value)
            && value.ValueKind == JsonValueKind.Array;
    }

    /// <summary>安全读取字符串。</summary>
    private static string ReadString(JsonElement parent, string name)
    {
        return parent.ValueKind == JsonValueKind.Object
            && parent.TryGetProperty(name, out JsonElement value)
            && value.ValueKind == JsonValueKind.String
                ? value.GetString() ?? string.Empty
                : string.Empty;
    }

    /// <summary>安全读取 Int32。</summary>
    private static int ReadInt32(JsonElement parent, string name, int fallback = 0)
    {
        return parent.ValueKind == JsonValueKind.Object
            && parent.TryGetProperty(name, out JsonElement value)
            && value.TryGetInt32(out int result)
                ? result
                : fallback;
    }

    /// <summary>安全读取 Int64。</summary>
    private static long ReadInt64(JsonElement parent, string name)
    {
        return parent.ValueKind == JsonValueKind.Object
            && parent.TryGetProperty(name, out JsonElement value)
            && value.TryGetInt64(out long result)
                ? result
                : 0L;
    }

    /// <summary>安全读取 Double。</summary>
    private static double ReadDouble(JsonElement parent, string name)
    {
        return parent.ValueKind == JsonValueKind.Object
            && parent.TryGetProperty(name, out JsonElement value)
            && value.TryGetDouble(out double result)
                ? result
                : 0d;
    }

    /// <summary>安全读取布尔值。</summary>
    private static bool ReadBoolean(JsonElement parent, string name)
    {
        return parent.ValueKind == JsonValueKind.Object
            && parent.TryGetProperty(name, out JsonElement value)
            && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            && value.GetBoolean();
    }
}
