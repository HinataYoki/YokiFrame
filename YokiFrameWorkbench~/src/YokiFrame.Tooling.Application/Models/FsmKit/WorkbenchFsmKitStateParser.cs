using System.Text.Json;

namespace YokiFrame.Tooling.Application.Models.FsmKit;

/// <summary>
/// 把 Runtime FsmKit 工作台 payload 转换为稳定强类型 read model。
/// </summary>
internal static class WorkbenchFsmKitStateParser
{
    private const int MAX_STATE_TREE_DEPTH = 16;

    /// <summary>
    /// 解析一次完整 FsmKit payload；无效输入转换为空状态并保留 stale 原因，不中断 dashboard 刷新。
    /// </summary>
    /// <param name="dataSource">携带原始 payload 的来源元数据。</param>
    /// <returns>可直接供 Workbench 使用的强类型状态。</returns>
    internal static WorkbenchFsmKitState Parse(WorkbenchFsmKitDataSource dataSource)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        if (string.IsNullOrWhiteSpace(dataSource.RawPayloadJson))
        {
            return CreateEmpty(dataSource, "FsmKit payload is empty.");
        }

        try
        {
            using var document = JsonDocument.Parse(dataSource.RawPayloadJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return CreateEmpty(dataSource, "FsmKit payload root must be an object.");
            }

            if (!WorkbenchFsmKitPayloadValidator.TryValidate(document.RootElement, out var reason))
            {
                return CreateEmpty(dataSource, reason);
            }

            return ParseRoot(document.RootElement, dataSource);
        }
        catch (JsonException exception)
        {
            return CreateEmpty(dataSource, "FsmKit payload is invalid JSON: " + exception.Message);
        }
    }

    /// <summary>
    /// 解析已确认是 JSON 对象的 payload 根节点。
    /// </summary>
    /// <param name="root">payload 根节点。</param>
    /// <param name="dataSource">来源元数据。</param>
    /// <returns>完整强类型状态。</returns>
    private static WorkbenchFsmKitState ParseRoot(
        JsonElement root,
        WorkbenchFsmKitDataSource dataSource)
    {
        var machines = ReadMachines(root);
        var selected = ReadSelected(root);
        var history = ReadHistory(root, out var historyCount);
        var stateEvents = ReadStateEvents(root, out var eventCount);
        return new WorkbenchFsmKitState(
            dataSource,
            ReadString(root, "fsmName"),
            ReadString(root, "instanceId"),
            ReadInt32(root, "count"),
            machines,
            selected,
            history,
            historyCount,
            stateEvents,
            eventCount);
    }

    /// <summary>
    /// 读取按注册顺序输出的 FSM 摘要列表。
    /// </summary>
    /// <param name="root">payload 根节点。</param>
    /// <returns>FSM 摘要列表。</returns>
    private static IReadOnlyList<WorkbenchFsmMachineSummary> ReadMachines(JsonElement root)
    {
        if (!TryGetArray(root, "fsms", out var elements))
        {
            return Array.Empty<WorkbenchFsmMachineSummary>();
        }

        List<WorkbenchFsmMachineSummary> machines = new();
        foreach (var element in elements.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            machines.Add(new WorkbenchFsmMachineSummary(
                ReadString(element, "instanceId"),
                ReadString(element, "name"),
                ReadString(element, "machineState"),
                ReadString(element, "currentState"),
                ReadInt32(element, "currentStateId", -1),
                ReadInt32(element, "stateCount")));
        }

        return machines;
    }

    /// <summary>
    /// 读取当前选中 FSM；空对象表示没有可选实例。
    /// </summary>
    /// <param name="root">payload 根节点。</param>
    /// <returns>选中详情；没有选择时为空。</returns>
    private static WorkbenchFsmMachineDetails? ReadSelected(JsonElement root)
    {
        if (!TryGetObject(root, "selected", out var selected)
            || !selected.EnumerateObject().MoveNext())
        {
            return null;
        }

        return new WorkbenchFsmMachineDetails(
            ReadString(selected, "fsmName"),
            ReadString(selected, "instanceId"),
            ReadString(selected, "machineState"),
            ReadString(selected, "currentState"),
            ReadInt32(selected, "currentStateId", -1),
            ReadInt32(selected, "stateCount"),
            ReadStateNodes(selected, "states", 0));
    }

    /// <summary>
    /// 递归读取状态树，并以深度上限防御异常或外部 provider 生成的过深 payload。
    /// </summary>
    /// <param name="parent">包含状态数组的父节点。</param>
    /// <param name="propertyName">状态数组属性名。</param>
    /// <param name="depth">当前递归深度。</param>
    /// <returns>状态节点列表。</returns>
    private static IReadOnlyList<WorkbenchFsmStateNode> ReadStateNodes(
        JsonElement parent,
        string propertyName,
        int depth)
    {
        if (depth > MAX_STATE_TREE_DEPTH || !TryGetArray(parent, propertyName, out var elements))
        {
            return Array.Empty<WorkbenchFsmStateNode>();
        }

        List<WorkbenchFsmStateNode> nodes = new();
        foreach (var element in elements.EnumerateArray())
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                nodes.Add(ReadStateNode(element, depth));
            }
        }

        return nodes;
    }

    /// <summary>
    /// 读取一个普通或复合状态节点，并保留复合状态机的全部摘要字段。
    /// </summary>
    /// <param name="element">状态节点 JSON。</param>
    /// <param name="depth">当前递归深度。</param>
    /// <returns>强类型状态节点。</returns>
    private static WorkbenchFsmStateNode ReadStateNode(JsonElement element, int depth)
    {
        return new WorkbenchFsmStateNode(
            ReadInt32(element, "id", -1),
            ReadInt32(element, "orderIndex", -1),
            ReadString(element, "name"),
            ReadInt64(element, "entryCount"),
            ReadString(element, "stateType"),
            ReadBoolean(element, "isCurrent"),
            ReadBoolean(element, "isComposite"),
            ReadString(element, "childMachineName"),
            ReadString(element, "machineState"),
            ReadString(element, "currentState"),
            ReadInt32(element, "currentStateId", -1),
            ReadInt32(element, "stateCount"),
            ReadStateNodes(element, "children", depth + 1));
    }

    /// <summary>
    /// 读取转换历史对象，并返回 payload 声明数量供 UI 识别裁剪或漂移。
    /// </summary>
    /// <param name="root">payload 根节点。</param>
    /// <param name="declaredCount">payload 声明数量。</param>
    /// <returns>转换历史列表。</returns>
    private static IReadOnlyList<WorkbenchFsmTransition> ReadHistory(
        JsonElement root,
        out int declaredCount)
    {
        declaredCount = 0;
        if (!TryGetObject(root, "history", out var container))
        {
            return Array.Empty<WorkbenchFsmTransition>();
        }

        declaredCount = ReadInt32(container, "count");
        if (!TryGetArray(container, "history", out var elements))
        {
            return Array.Empty<WorkbenchFsmTransition>();
        }

        List<WorkbenchFsmTransition> history = new();
        foreach (var element in elements.EnumerateArray())
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                history.Add(new WorkbenchFsmTransition(
                    ReadString(element, "from"),
                    ReadString(element, "to"),
                    ReadString(element, "time")));
            }
        }

        return history;
    }

    /// <summary>
    /// 读取状态生命周期事件对象，并返回 payload 声明数量。
    /// </summary>
    /// <param name="root">payload 根节点。</param>
    /// <param name="declaredCount">payload 声明数量。</param>
    /// <returns>状态事件列表。</returns>
    private static IReadOnlyList<WorkbenchFsmStateEvent> ReadStateEvents(
        JsonElement root,
        out int declaredCount)
    {
        declaredCount = 0;
        if (!TryGetObject(root, "stateEvents", out var container))
        {
            return Array.Empty<WorkbenchFsmStateEvent>();
        }

        declaredCount = ReadInt32(container, "count");
        if (!TryGetArray(container, "events", out var elements))
        {
            return Array.Empty<WorkbenchFsmStateEvent>();
        }

        List<WorkbenchFsmStateEvent> events = new();
        foreach (var element in elements.EnumerateArray())
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                events.Add(new WorkbenchFsmStateEvent(
                    ReadString(element, "eventName"),
                    ReadString(element, "state"),
                    ReadString(element, "time")));
            }
        }

        return events;
    }

    /// <summary>
    /// 创建没有业务节点的安全状态，并保留原始 payload 和明确 stale 原因。
    /// </summary>
    /// <param name="dataSource">原始数据源。</param>
    /// <param name="reason">新增失败原因。</param>
    /// <returns>空 FsmKit 状态。</returns>
    private static WorkbenchFsmKitState CreateEmpty(
        WorkbenchFsmKitDataSource dataSource,
        string reason)
    {
        var staleReason = string.IsNullOrWhiteSpace(dataSource.StaleReason)
            ? reason
            : dataSource.StaleReason + " " + reason;
        return new WorkbenchFsmKitState(
            dataSource.WithStaleReason(staleReason),
            string.Empty,
            string.Empty,
            0,
            Array.Empty<WorkbenchFsmMachineSummary>(),
            null,
            Array.Empty<WorkbenchFsmTransition>(),
            0,
            Array.Empty<WorkbenchFsmStateEvent>(),
            0);
    }

    /// <summary>
    /// 尝试读取对象属性，缺失或类型不符时返回 false。
    /// </summary>
    private static bool TryGetObject(JsonElement parent, string name, out JsonElement value)
    {
        return parent.TryGetProperty(name, out value) && value.ValueKind == JsonValueKind.Object;
    }

    /// <summary>
    /// 尝试读取数组属性，缺失或类型不符时返回 false。
    /// </summary>
    private static bool TryGetArray(JsonElement parent, string name, out JsonElement value)
    {
        return parent.TryGetProperty(name, out value) && value.ValueKind == JsonValueKind.Array;
    }

    /// <summary>
    /// 读取字符串字段；非字符串标量保留其 JSON 文本，复杂值回落空字符串。
    /// </summary>
    private static string ReadString(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out var value))
        {
            return string.Empty;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? string.Empty,
            JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => value.ToString(),
            _ => string.Empty
        };
    }

    /// <summary>
    /// 读取整数或整数字符串字段，缺失和不兼容值使用调用方默认值。
    /// </summary>
    private static int ReadInt32(JsonElement parent, string name, int defaultValue = 0)
    {
        if (!parent.TryGetProperty(name, out var value))
        {
            return defaultValue;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
        {
            return number;
        }

        return value.ValueKind == JsonValueKind.String
            && int.TryParse(value.GetString(), out var parsed)
                ? parsed
                : defaultValue;
    }

    /// <summary>
    /// 读取 Int64 JSON number；累计次数缺失或不兼容时回落零。
    /// </summary>
    private static long ReadInt64(JsonElement parent, string name)
    {
        return parent.TryGetProperty(name, out var value)
            && value.ValueKind == JsonValueKind.Number
            && value.TryGetInt64(out var number)
                ? number
                : 0L;
    }

    /// <summary>
    /// 读取布尔或布尔字符串字段，不兼容值回落 false。
    /// </summary>
    private static bool ReadBoolean(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out var value))
        {
            return false;
        }

        if (value.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            return value.GetBoolean();
        }

        return value.ValueKind == JsonValueKind.String
            && bool.TryParse(value.GetString(), out var parsed)
            && parsed;
    }
}
