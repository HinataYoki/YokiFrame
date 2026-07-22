using System.Text.Json;

namespace YokiFrame.Tooling.Application.Models.EventKit;

/// <summary>
/// 把 Runtime EventKit 工作台 payload 转换为稳定强类型 read model。
/// </summary>
internal static class WorkbenchEventKitStateParser
{
    /// <summary>解析完整 payload；无效输入转换为空状态并保留 stale 原因。</summary>
    internal static WorkbenchEventKitState Parse(WorkbenchEventKitDataSource dataSource)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        if (string.IsNullOrWhiteSpace(dataSource.RawPayloadJson))
        {
            return CreateEmpty(dataSource, "EventKit payload is empty.");
        }

        try
        {
            using var document = JsonDocument.Parse(dataSource.RawPayloadJson);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("events", out JsonElement eventsElement)
                || eventsElement.ValueKind != JsonValueKind.Array)
            {
                return CreateEmpty(dataSource, "EventKit payload must contain an events array.");
            }

            return ParseRoot(root, eventsElement, dataSource);
        }
        catch (JsonException exception)
        {
            return CreateEmpty(dataSource, "EventKit payload is invalid JSON: " + exception.Message);
        }
    }

    /// <summary>解析已经确认结构正确的 payload 根。</summary>
    private static WorkbenchEventKitState ParseRoot(
        JsonElement root,
        JsonElement eventsElement,
        WorkbenchEventKitDataSource dataSource)
    {
        IReadOnlyList<WorkbenchEventKitEvent> events = ReadEvents(eventsElement);
        IReadOnlyList<WorkbenchEventKitActivity> activities = ReadActivities(root);
        JsonElement counts = root.TryGetProperty("counts", out JsonElement countsElement)
            && countsElement.ValueKind == JsonValueKind.Object
                ? countsElement
                : default;
        return new WorkbenchEventKitState(
            dataSource,
            ReadInt64(root, "version"),
            ReadInt64(root, "sequence"),
            ReadInt32(counts, "typeEvents"),
            ReadInt32(counts, "enumEvents"),
            ReadInt32(counts, "stringEvents"),
            ReadInt32(counts, "totalEvents", events.Count),
            ReadInt32(counts, "totalHandlers", events.Sum(static item => item.HandlerCount)),
            ReadInt32(counts, "recentActivities", activities.Count),
            events,
            activities);
    }

    /// <summary>读取 Runtime 事件列表并忽略非对象条目。</summary>
    private static IReadOnlyList<WorkbenchEventKitEvent> ReadEvents(JsonElement array)
    {
        List<WorkbenchEventKitEvent> events = new();
        foreach (JsonElement element in array.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            events.Add(new WorkbenchEventKitEvent(
                ReadString(element, "channel"),
                ReadString(element, "eventKey"),
                ReadString(element, "payloadType"),
                ReadInt32(element, "handlerCount"),
                ReadInt64(element, "lastSequence"),
                ReadString(element, "lastTime"),
                ReadBoolean(element, "deprecated")));
        }

        return events;
    }

    /// <summary>读取 recentEvents.events 有界活动数组。</summary>
    private static IReadOnlyList<WorkbenchEventKitActivity> ReadActivities(JsonElement root)
    {
        if (!root.TryGetProperty("recentEvents", out JsonElement recent)
            || recent.ValueKind != JsonValueKind.Object
            || !recent.TryGetProperty("events", out JsonElement array)
            || array.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<WorkbenchEventKitActivity>();
        }

        List<WorkbenchEventKitActivity> activities = new();
        foreach (JsonElement element in array.EnumerateArray())
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                activities.Add(ReadActivity(element));
            }
        }

        return activities;
    }

    /// <summary>读取单条活动。</summary>
    private static WorkbenchEventKitActivity ReadActivity(JsonElement element)
    {
        return new WorkbenchEventKitActivity(
            ReadInt64(element, "sequence"),
            ReadString(element, "kind"),
            ReadString(element, "channel"),
            ReadString(element, "eventKey"),
            ReadString(element, "payloadType"),
            ReadString(element, "handler"),
            ReadString(element, "time"));
    }

    /// <summary>创建保留来源证据的安全空状态。</summary>
    private static WorkbenchEventKitState CreateEmpty(
        WorkbenchEventKitDataSource dataSource,
        string reason)
    {
        string staleReason = string.IsNullOrWhiteSpace(dataSource.StaleReason)
            ? reason
            : dataSource.StaleReason + " " + reason;
        return new WorkbenchEventKitState(
            dataSource.WithStaleReason(staleReason),
            0L,
            0L,
            0,
            0,
            0,
            0,
            0,
            0,
            Array.Empty<WorkbenchEventKitEvent>(),
            Array.Empty<WorkbenchEventKitActivity>());
    }

    /// <summary>安全读取字符串属性。</summary>
    private static string ReadString(JsonElement parent, string name)
    {
        return parent.ValueKind == JsonValueKind.Object
            && parent.TryGetProperty(name, out JsonElement value)
            && value.ValueKind == JsonValueKind.String
                ? value.GetString() ?? string.Empty
                : string.Empty;
    }

    /// <summary>安全读取 Int32 属性。</summary>
    private static int ReadInt32(JsonElement parent, string name, int defaultValue = 0)
    {
        return parent.ValueKind == JsonValueKind.Object
            && parent.TryGetProperty(name, out JsonElement value)
            && value.TryGetInt32(out int result)
                ? result
                : defaultValue;
    }

    /// <summary>安全读取 Int64 属性。</summary>
    private static long ReadInt64(JsonElement parent, string name)
    {
        return parent.ValueKind == JsonValueKind.Object
            && parent.TryGetProperty(name, out JsonElement value)
            && value.TryGetInt64(out long result)
                ? result
                : 0L;
    }

    /// <summary>安全读取布尔属性。</summary>
    private static bool ReadBoolean(JsonElement parent, string name)
    {
        return parent.ValueKind == JsonValueKind.Object
            && parent.TryGetProperty(name, out JsonElement value)
            && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            && value.GetBoolean();
    }
}
