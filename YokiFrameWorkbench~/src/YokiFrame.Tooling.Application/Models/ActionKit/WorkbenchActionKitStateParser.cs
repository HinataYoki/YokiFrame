using System.Text.Json;

namespace YokiFrame.Tooling.Application.Models.ActionKit;

/// <summary>把 Runtime ActionKit payload 转换为稳定强类型 read model。</summary>
internal static class WorkbenchActionKitStateParser
{
    private const int MAX_PARSE_DEPTH = 32;

    /// <summary>解析完整 payload；无效输入转换为空状态并保留 stale 原因。</summary>
    /// <param name="dataSource">原始 payload 与宿主身份。</param>
    /// <returns>可安全绑定的 ActionKit 状态。</returns>
    internal static WorkbenchActionKitState Parse(WorkbenchActionKitDataSource dataSource)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        if (string.IsNullOrWhiteSpace(dataSource.RawPayloadJson))
        {
            return CreateEmpty(dataSource, "ActionKit payload is empty.");
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(dataSource.RawPayloadJson);
            JsonElement root = document.RootElement;
            if (!TryReadSchema(root, out string? error)
                || !TryGetObject(root, "stats", out JsonElement stats)
                || !TryGetArray(root, "roots", out JsonElement roots)
                || !TryGetArray(root, "events", out JsonElement events))
            {
                return CreateEmpty(dataSource, error ?? "ActionKit payload is missing required objects or arrays.");
            }

            return ParseRoot(dataSource, root, stats, roots, events);
        }
        catch (JsonException exception)
        {
            return CreateEmpty(dataSource, "ActionKit payload is invalid JSON: " + exception.Message);
        }
        catch (InvalidDataException exception)
        {
            return CreateEmpty(dataSource, exception.Message);
        }
    }

    /// <summary>解析已经确认顶层 schema 的 ActionKit payload。</summary>
    private static WorkbenchActionKitState ParseRoot(
        WorkbenchActionKitDataSource source,
        JsonElement root,
        JsonElement stats,
        JsonElement roots,
        JsonElement events)
    {
        IReadOnlyList<WorkbenchActionKitRoot> parsedRoots = ReadRoots(roots);
        IReadOnlyList<WorkbenchActionKitEvent> parsedEvents = ReadEvents(events);
        WorkbenchActionKitStats parsedStats = ReadStats(stats, parsedRoots.Count);
        return new WorkbenchActionKitState(
            source,
            ReadInt64(root, "version"),
            parsedStats,
            parsedRoots,
            parsedEvents,
            ReadInt32(root, "rootTotal", parsedRoots.Count),
            ReadInt64(root, "eventTotal", parsedEvents.Count),
            ReadBoolean(root, "rootsTruncated"),
            ReadBoolean(root, "nodesTruncated"),
            ReadBoolean(root, "depthTruncated"),
            ReadBoolean(root, "stackTruncated"),
            ReadBoolean(root, "eventsTruncated"));
    }

    /// <summary>验证固定 schemaVersion，拒绝把其它 payload 当作 ActionKit state。</summary>
    private static bool TryReadSchema(JsonElement root, out string? error)
    {
        error = null;
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("schemaVersion", out JsonElement schema)
            || !schema.TryGetInt32(out int version)
            || version != 1)
        {
            error = "ActionKit payload requires schemaVersion 1.";
            return false;
        }

        return true;
    }

    /// <summary>读取累计指标；活动根数量缺失时由列表回推。</summary>
    private static WorkbenchActionKitStats ReadStats(JsonElement stats, int rootCount)
    {
        return new WorkbenchActionKitStats(
            ReadInt64(stats, "frameCount"),
            ReadInt32(stats, "activeRootCount", rootCount),
            ReadInt64(stats, "finishedCount"),
            ReadInt64(stats, "cancelledCount"),
            ReadInt64(stats, "faultedCount"),
            ReadInt64(stats, "terminalEventCount"),
            ReadBoolean(stats, "stackTraceEnabled"),
            ReadInt32(stats, "stackTraceCount"));
    }

    /// <summary>读取活动根数组，并要求 Action ID 保持字符串。</summary>
    private static IReadOnlyList<WorkbenchActionKitRoot> ReadRoots(JsonElement array)
    {
        List<WorkbenchActionKitRoot> roots = new();
        foreach (JsonElement item in array.EnumerateArray())
        {
            RequireObject(item, "ActionKit root");
            IReadOnlyList<WorkbenchActionKitNode> children = ReadChildren(item, 0);
            roots.Add(new WorkbenchActionKitRoot(
                ReadRequiredString(item, "actionId"),
                ReadString(item, "type"),
                ReadString(item, "status"),
                ReadBoolean(item, "paused"),
                ReadBoolean(item, "deinited"),
                ReadString(item, "debugInfo"),
                ReadString(item, "updateMode"),
                ReadBoolean(item, "cancelRequested"),
                ReadStackTrace(item),
                children,
                ReadInt32(item, "childCount", children.Count),
                ReadInt32(item, "currentChildIndex", -1),
                ReadString(item, "executorName", "PlayerLoop")));
        }

        return roots;
    }

    /// <summary>递归读取子动作，并限制恶意或损坏 payload 的最大深度。</summary>
    private static IReadOnlyList<WorkbenchActionKitNode> ReadChildren(JsonElement parent, int depth)
    {
        if (depth >= MAX_PARSE_DEPTH)
        {
            throw new InvalidDataException("ActionKit payload exceeds the supported tree depth.");
        }

        if (!TryGetArray(parent, "children", out JsonElement children))
        {
            return Array.Empty<WorkbenchActionKitNode>();
        }

        List<WorkbenchActionKitNode> result = new();
        foreach (JsonElement item in children.EnumerateArray())
        {
            RequireObject(item, "ActionKit node");
            IReadOnlyList<WorkbenchActionKitNode> nestedChildren = ReadChildren(item, depth + 1);
            result.Add(new WorkbenchActionKitNode(
                ReadRequiredString(item, "actionId"),
                ReadString(item, "type"),
                ReadString(item, "status"),
                ReadBoolean(item, "paused"),
                ReadBoolean(item, "deinited"),
                ReadString(item, "debugInfo"),
                nestedChildren,
                ReadInt32(item, "childCount", nestedChildren.Count),
                ReadInt32(item, "currentChildIndex", -1),
                ReadString(item, "executorName", "PlayerLoop"),
                ReadString(item, "updateMode")));
        }

        return result;
    }

    /// <summary>读取根 Action 捕获的调用帧。</summary>
    private static IReadOnlyList<WorkbenchActionKitStackFrame> ReadStackTrace(JsonElement root)
    {
        if (!TryGetArray(root, "stackTrace", out JsonElement frames))
        {
            return Array.Empty<WorkbenchActionKitStackFrame>();
        }

        List<WorkbenchActionKitStackFrame> result = new();
        foreach (JsonElement frame in frames.EnumerateArray())
        {
            if (frame.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            result.Add(new WorkbenchActionKitStackFrame(
                ReadString(frame, "method"),
                ReadString(frame, "file"),
                ReadInt32(frame, "line")));
        }

        return result;
    }

    /// <summary>读取最新优先的终态事件。</summary>
    private static IReadOnlyList<WorkbenchActionKitEvent> ReadEvents(JsonElement array)
    {
        List<WorkbenchActionKitEvent> result = new();
        foreach (JsonElement item in array.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            result.Add(new WorkbenchActionKitEvent(
                ReadRequiredString(item, "actionId"),
                ReadString(item, "actionType"),
                ReadString(item, "outcome"),
                ReadInt64(item, "frame"),
                ReadString(item, "errorMessage")));
        }

        return result;
    }

    /// <summary>创建保留来源证据的安全空状态。</summary>
    private static WorkbenchActionKitState CreateEmpty(
        WorkbenchActionKitDataSource source,
        string reason)
    {
        return new WorkbenchActionKitState(
            source.WithStaleReason(reason),
            0L,
            new WorkbenchActionKitStats(0L, 0, 0L, 0L, 0L, 0L, false, 0),
            Array.Empty<WorkbenchActionKitRoot>(),
            Array.Empty<WorkbenchActionKitEvent>(),
            0,
            0L,
            false,
            false,
            false,
            false,
            false);
    }

    /// <summary>要求节点是 JSON 对象。</summary>
    private static void RequireObject(JsonElement value, string label)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException(label + " must be an object.");
        }
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

    /// <summary>读取必需字符串，拒绝数值 Action ID 造成精度丢失。</summary>
    private static string ReadRequiredString(JsonElement parent, string name)
    {
        if (parent.TryGetProperty(name, out JsonElement value)
            && value.ValueKind == JsonValueKind.String)
        {
            return value.GetString() ?? string.Empty;
        }

        throw new InvalidDataException("ActionKit field " + name + " must be a string.");
    }

    /// <summary>安全读取可选字符串。</summary>
    private static string ReadString(JsonElement parent, string name, string fallback = "")
    {
        return parent.ValueKind == JsonValueKind.Object
            && parent.TryGetProperty(name, out JsonElement value)
            && value.ValueKind == JsonValueKind.String
                ? value.GetString() ?? fallback
                : fallback;
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
    private static long ReadInt64(JsonElement parent, string name, long fallback = 0L)
    {
        return parent.ValueKind == JsonValueKind.Object
            && parent.TryGetProperty(name, out JsonElement value)
            && value.TryGetInt64(out long result)
                ? result
                : fallback;
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
