using System.Text.Json;

namespace YokiFrame.Tooling.Application.Models.ResKit;

/// <summary>把 Runtime ResKit payload 转换为稳定强类型 read model。</summary>
internal static class WorkbenchResKitStateParser
{
    /// <summary>解析完整 state；无效输入转换为空状态并保留 stale 原因。</summary>
    internal static WorkbenchResKitState Parse(WorkbenchResKitDataSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (string.IsNullOrWhiteSpace(source.RawPayloadJson))
        {
            return CreateEmpty(source, "ResKit payload is empty.");
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(source.RawPayloadJson);
            JsonElement root = document.RootElement;
            if (!TryGetObject(root, "provider", out JsonElement provider)
                || !TryGetObject(root, "stats", out JsonElement stats)
                || !TryGetObject(root, "resources", out JsonElement resources)
                || !TryGetObject(root, "unloadHistory", out JsonElement history))
            {
                return CreateEmpty(source, "ResKit payload is missing required objects.");
            }

            return ParseRoot(source, root, provider, stats, resources, history);
        }
        catch (JsonException exception)
        {
            return CreateEmpty(source, "ResKit payload is invalid JSON: " + exception.Message);
        }
    }

    /// <summary>解析 get_resource_detail 命令返回的单个资源。</summary>
    internal static WorkbenchResKitResourceDetail ParseResourceDetail(string payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            throw new InvalidDataException("ResKit resource detail payload is empty.");
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(payloadJson);
            JsonElement root = document.RootElement;
            if (!TryGetObject(root, "resource", out JsonElement resource))
            {
                throw new InvalidDataException("ResKit resource detail payload requires one resource object.");
            }

            return new WorkbenchResKitResourceDetail(
                ReadInt64(root, "diagnosticVersion"),
                ReadResource(resource));
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("ResKit resource detail payload is invalid JSON.", exception);
        }
    }

    /// <summary>解析已经确认顶层结构的 ResKit state。</summary>
    private static WorkbenchResKitState ParseRoot(
        WorkbenchResKitDataSource source,
        JsonElement root,
        JsonElement provider,
        JsonElement stats,
        JsonElement resources,
        JsonElement history)
    {
        IReadOnlyList<WorkbenchResKitResource> resourceItems = ReadResources(resources);
        IReadOnlyList<WorkbenchResKitUnloadRecord> historyItems = ReadHistory(history);
        return new WorkbenchResKitState(
            source,
            ReadInt64(root, "diagnosticVersion"),
            ReadProvider(provider),
            ReadStats(stats),
            resourceItems,
            historyItems,
            ReadInt32(resources, "totalCount", resourceItems.Count),
            ReadInt32(history, "totalCount", historyItems.Count),
            ReadInt64(history, "droppedCount"),
            ReadBoolean(resources, "truncated"),
            ReadBoolean(history, "truncated"),
            ReadString(root, "lastBackgroundFailure"));
    }

    /// <summary>读取 Provider 与 raw bytes/text capability。</summary>
    private static WorkbenchResKitProvider ReadProvider(JsonElement provider)
    {
        JsonElement capabilities = TryGetObject(provider, "capabilities", out JsonElement value) ? value : default;
        return new WorkbenchResKitProvider(
            ReadString(provider, "name"),
            ReadInt64(provider, "generation"),
            ReadBoolean(capabilities, "rawBytes"),
            ReadBoolean(capabilities, "rawText"));
    }

    /// <summary>读取聚合统计与加载位置跟踪开关。</summary>
    private static WorkbenchResKitStats ReadStats(JsonElement stats)
    {
        return new WorkbenchResKitStats(
            ReadInt32(stats, "loadedCount"),
            ReadInt32(stats, "inFlightCount"),
            ReadInt32(stats, "totalLeaseCount"),
            ReadInt32(stats, "unloadHistoryCount"),
            ReadBoolean(stats, "loadLocationTrackingEnabled"));
    }

    /// <summary>读取有界已加载资源数组。</summary>
    private static IReadOnlyList<WorkbenchResKitResource> ReadResources(JsonElement resources)
    {
        if (!TryGetArray(resources, "items", out JsonElement items)) return Array.Empty<WorkbenchResKitResource>();
        List<WorkbenchResKitResource> result = new();
        foreach (JsonElement item in items.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Object) result.Add(ReadResource(item));
        }

        return result;
    }

    /// <summary>读取单个资源摘要或详情。</summary>
    private static WorkbenchResKitResource ReadResource(JsonElement item)
    {
        string path = ReadString(item, "path");
        string typeName = ReadString(item, "typeName");
        IReadOnlyList<WorkbenchResKitLoadSource> sources = ReadSources(item);
        return new WorkbenchResKitResource(
            path + "\u001f" + typeName,
            path,
            typeName,
            ReadString(item, "state"),
            ReadInt32(item, "leaseCount"),
            ReadString(item, "providerName"),
            ReadInt64(item, "providerGeneration"),
            ReadInt32(item, "trackedSourceCount"),
            sources,
            ReadInt32(item, "sourceTotal", sources.Count),
            ReadBoolean(item, "sourcesTruncated"));
    }

    /// <summary>读取按需详情中的独立 lease 来源。</summary>
    private static IReadOnlyList<WorkbenchResKitLoadSource> ReadSources(JsonElement resource)
    {
        if (!TryGetArray(resource, "sources", out JsonElement items)) return Array.Empty<WorkbenchResKitLoadSource>();
        List<WorkbenchResKitLoadSource> result = new();
        foreach (JsonElement item in items.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;
            result.Add(new WorkbenchResKitLoadSource(
                ReadString(item, "display"),
                ReadString(item, "filePath"),
                ReadInt32(item, "line"),
                ReadInt32(item, "refCount"),
                ReadBoolean(item, "anonymous"),
                ReadBoolean(item, "tracked")));
        }

        return result;
    }

    /// <summary>读取最新优先卸载历史。</summary>
    private static IReadOnlyList<WorkbenchResKitUnloadRecord> ReadHistory(JsonElement history)
    {
        if (!TryGetArray(history, "items", out JsonElement items)) return Array.Empty<WorkbenchResKitUnloadRecord>();
        List<WorkbenchResKitUnloadRecord> result = new();
        var occurrence = 0;
        foreach (JsonElement item in items.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;
            string path = ReadString(item, "path");
            string typeName = ReadString(item, "typeName");
            string time = ReadString(item, "unloadTimeUtc");
            result.Add(new WorkbenchResKitUnloadRecord(
                path + "\u001f" + typeName + "\u001f" + time + "\u001f" + occurrence++,
                path,
                typeName,
                ReadString(item, "providerName"),
                time));
        }

        return result;
    }

    /// <summary>创建保留来源证据的安全空状态。</summary>
    private static WorkbenchResKitState CreateEmpty(WorkbenchResKitDataSource source, string reason)
    {
        return new WorkbenchResKitState(
            source.WithStaleReason(reason), 0L,
            new WorkbenchResKitProvider(string.Empty, 0L, false, false),
            new WorkbenchResKitStats(0, 0, 0, 0, false),
            Array.Empty<WorkbenchResKitResource>(), Array.Empty<WorkbenchResKitUnloadRecord>(),
            0, 0, 0, false, false, string.Empty);
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
            && value.TryGetInt32(out int result) ? result : fallback;
    }

    /// <summary>安全读取 Int64。</summary>
    private static long ReadInt64(JsonElement parent, string name)
    {
        return parent.ValueKind == JsonValueKind.Object
            && parent.TryGetProperty(name, out JsonElement value)
            && value.TryGetInt64(out long result) ? result : 0L;
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
