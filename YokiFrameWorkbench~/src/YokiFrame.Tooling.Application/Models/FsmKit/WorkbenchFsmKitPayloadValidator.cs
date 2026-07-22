using System.Text.Json;

namespace YokiFrame.Tooling.Application.Models.FsmKit;

/// <summary>
/// 校验 Runtime FsmKit 工作台根对象的稳定协议形状，防止字段漂移被静默解释为空状态。
/// </summary>
internal static class WorkbenchFsmKitPayloadValidator
{
    private const int MAX_STATE_TREE_DEPTH = 16;

    /// <summary>
    /// 校验工作台根标量、实例列表、选中详情、历史和生命周期事件容器。
    /// </summary>
    /// <param name="root">已确认是对象的 payload 根。</param>
    /// <param name="reason">失败时返回可展示的具体字段原因。</param>
    /// <returns>根对象符合当前 FsmKit 工作台契约时返回 true。</returns>
    internal static bool TryValidate(JsonElement root, out string reason)
    {
        return TryRequireKind(root, "fsmName", JsonValueKind.String, out _, out reason)
            && TryRequireKind(root, "instanceId", JsonValueKind.String, out _, out reason)
            && TryValidateMachines(root, out reason)
            && TryRequireInt32(root, "count", out _, out reason)
            && TryValidateSelected(root, out reason)
            && TryValidateHistory(root, out reason)
            && TryValidateStateEvents(root, out reason);
    }

    /// <summary>
    /// 校验 FSM 摘要数组及每个摘要的稳定字段类型。
    /// </summary>
    private static bool TryValidateMachines(JsonElement root, out string reason)
    {
        if (!TryRequireKind(root, "fsms", JsonValueKind.Array, out var machines, out reason))
        {
            return false;
        }

        foreach (var machine in machines.EnumerateArray())
        {
            if (machine.ValueKind != JsonValueKind.Object
                || !TryRequireKind(machine, "instanceId", JsonValueKind.String, out _, out reason)
                || !TryRequireKind(machine, "name", JsonValueKind.String, out _, out reason)
                || !TryRequireKind(machine, "machineState", JsonValueKind.String, out _, out reason)
                || !TryRequireKind(machine, "currentState", JsonValueKind.String, out _, out reason)
                || !TryRequireInt32(machine, "currentStateId", out _, out reason)
                || !TryRequireInt32(machine, "stateCount", out _, out reason))
            {
                reason = string.IsNullOrWhiteSpace(reason)
                    ? "FsmKit property 'fsms' must contain objects."
                    : "FsmKit fsms item is invalid. " + reason;
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 校验选中详情；空对象表示当前没有可选实例。
    /// </summary>
    private static bool TryValidateSelected(JsonElement root, out string reason)
    {
        if (!TryRequireKind(root, "selected", JsonValueKind.Object, out var selected, out reason))
        {
            return false;
        }

        if (!selected.EnumerateObject().MoveNext())
        {
            return true;
        }

        if (TryRequireKind(selected, "fsmName", JsonValueKind.String, out _, out reason)
            && TryRequireKind(selected, "instanceId", JsonValueKind.String, out _, out reason)
            && TryRequireKind(selected, "machineState", JsonValueKind.String, out _, out reason)
            && TryRequireKind(selected, "currentState", JsonValueKind.String, out _, out reason)
            && TryRequireInt32(selected, "currentStateId", out _, out reason)
            && TryRequireInt32(selected, "stateCount", out _, out reason)
            && TryValidateStateArray(selected, "states", 0, out reason))
        {
            return true;
        }

        reason = "FsmKit property 'selected' is invalid. " + reason;
        return false;
    }

    /// <summary>
    /// 校验状态节点数组及递归 children 的容器类型，避免复杂值被静默跳过。
    /// </summary>
    private static bool TryValidateStateArray(
        JsonElement parent,
        string propertyName,
        int depth,
        out string reason)
    {
        if (depth > MAX_STATE_TREE_DEPTH)
        {
            reason = "FsmKit state tree exceeds the maximum supported depth.";
            return false;
        }

        if (!TryRequireKind(parent, propertyName, JsonValueKind.Array, out var states, out reason))
        {
            return false;
        }

        foreach (var state in states.EnumerateArray())
        {
            if (state.ValueKind != JsonValueKind.Object)
            {
                reason = "FsmKit property '" + propertyName + "' must contain objects.";
                return false;
            }

            if (!TryRequireInt64(state, "entryCount", out _, out reason))
            {
                return false;
            }

            if (state.TryGetProperty("children", out var children)
                && children.ValueKind != JsonValueKind.Array)
            {
                reason = "FsmKit property 'children' must be an array.";
                return false;
            }

            if (state.TryGetProperty("children", out _)
                && !TryValidateStateArray(state, "children", depth + 1, out reason))
            {
                return false;
            }
        }

        reason = string.Empty;
        return true;
    }

    /// <summary>
    /// 校验转换历史容器、数量和每条记录的字符串字段。
    /// </summary>
    private static bool TryValidateHistory(JsonElement root, out string reason)
    {
        if (!TryRequireKind(root, "history", JsonValueKind.Object, out var history, out reason)
            || !TryRequireInt32(history, "count", out _, out reason)
            || !TryRequireKind(history, "history", JsonValueKind.Array, out var records, out reason))
        {
            reason = "FsmKit property 'history' is invalid. " + reason;
            return false;
        }

        foreach (var record in records.EnumerateArray())
        {
            if (record.ValueKind != JsonValueKind.Object
                || !TryRequireKind(record, "from", JsonValueKind.String, out _, out reason)
                || !TryRequireKind(record, "to", JsonValueKind.String, out _, out reason)
                || !TryRequireKind(record, "time", JsonValueKind.String, out _, out reason))
            {
                reason = "FsmKit history item is invalid. " + reason;
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 校验状态生命周期事件容器、数量和每条记录的字符串字段。
    /// </summary>
    private static bool TryValidateStateEvents(JsonElement root, out string reason)
    {
        if (!TryRequireKind(root, "stateEvents", JsonValueKind.Object, out var stateEvents, out reason)
            || !TryRequireInt32(stateEvents, "count", out _, out reason)
            || !TryRequireKind(stateEvents, "events", JsonValueKind.Array, out var records, out reason))
        {
            reason = "FsmKit property 'stateEvents' is invalid. " + reason;
            return false;
        }

        foreach (var record in records.EnumerateArray())
        {
            if (record.ValueKind != JsonValueKind.Object
                || !TryRequireKind(record, "eventName", JsonValueKind.String, out _, out reason)
                || !TryRequireKind(record, "state", JsonValueKind.String, out _, out reason)
                || !TryRequireKind(record, "time", JsonValueKind.String, out _, out reason))
            {
                reason = "FsmKit stateEvents item is invalid. " + reason;
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 要求指定属性存在且具有精确 JSON 类型。
    /// </summary>
    private static bool TryRequireKind(
        JsonElement parent,
        string name,
        JsonValueKind expectedKind,
        out JsonElement value,
        out string reason)
    {
        if (parent.TryGetProperty(name, out value) && value.ValueKind == expectedKind)
        {
            reason = string.Empty;
            return true;
        }

        reason = "FsmKit requires property '" + name + "' as " + expectedKind + ".";
        return false;
    }

    /// <summary>
    /// 要求指定属性是 Int32 范围内的 JSON number。
    /// </summary>
    private static bool TryRequireInt32(
        JsonElement parent,
        string name,
        out int value,
        out string reason)
    {
        if (parent.TryGetProperty(name, out var element)
            && element.ValueKind == JsonValueKind.Number
            && element.TryGetInt32(out value))
        {
            reason = string.Empty;
            return true;
        }

        value = 0;
        reason = "FsmKit requires property '" + name + "' as Int32.";
        return false;
    }

    /// <summary>
    /// 要求指定属性是 Int64 范围内的非负 JSON number。
    /// </summary>
    private static bool TryRequireInt64(
        JsonElement parent,
        string name,
        out long value,
        out string reason)
    {
        if (parent.TryGetProperty(name, out var element)
            && element.ValueKind == JsonValueKind.Number
            && element.TryGetInt64(out value)
            && value >= 0L)
        {
            reason = string.Empty;
            return true;
        }

        value = 0L;
        reason = "FsmKit requires property '" + name + "' as a non-negative Int64.";
        return false;
    }
}
