using System.Text.Json;

namespace YokiFrame.Tooling.Application.Models.UIKit;

/// <summary>把 Unity UIKit payload 转换为稳定强类型 read model。</summary>
internal static class WorkbenchUIKitStateParser
{
    /// <summary>解析完整 state；无效输入转换为空状态并保留 stale 原因。</summary>
    /// <param name="source">包含原始 payload 和来源证据的数据源。</param>
    /// <returns>可供 Workbench 绑定的稳定状态。</returns>
    internal static WorkbenchUIKitState Parse(WorkbenchUIKitDataSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (string.IsNullOrWhiteSpace(source.RawPayloadJson))
        {
            return CreateEmpty(source, "UIKit payload is empty.");
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(source.RawPayloadJson);
            JsonElement payload = document.RootElement;
            if (!TryGetRequiredObjects(payload, out UIKitPayloadObjects objects))
            {
                return CreateEmpty(source, "UIKit payload is missing required objects.");
            }

            return ParsePayload(source, payload, objects);
        }
        catch (JsonException exception)
        {
            return CreateEmpty(source, "UIKit payload is invalid JSON: " + exception.Message);
        }
    }

    /// <summary>解析已经确认顶层结构的 UIKit payload。</summary>
    private static WorkbenchUIKitState ParsePayload(
        WorkbenchUIKitDataSource source,
        JsonElement payload,
        UIKitPayloadObjects objects)
    {
        IReadOnlyList<WorkbenchUIKitPanel> panels = ReadPanels(objects.Panels);
        IReadOnlyList<WorkbenchUIKitStack> stacks = ReadStacks(objects.Stacks);
        JsonElement states = TryGetObject(objects.Stats, "states", out JsonElement stateValues)
            ? stateValues
            : default;
        return new WorkbenchUIKitState(
            source,
            ReadInt32(payload, "schemaVersion"),
            new WorkbenchUIKitRoot(ReadBoolean(objects.Root, "exists")),
            ReadStats(objects.Stats, states),
            ReadCache(objects.Cache),
            new WorkbenchUIKitModal(
                ReadBoolean(objects.Modal, "blockerActive"),
                ReadInt32(objects.Modal, "panelCount")),
            panels,
            stacks,
            ReadInt32(objects.Panels, "total", panels.Count),
            ReadInt32(objects.Panels, "returned", panels.Count),
            ReadBoolean(objects.Panels, "truncated"),
            ReadInt32(objects.Stacks, "total", stacks.Count),
            ReadInt32(objects.Stacks, "returned", stacks.Count),
            ReadBoolean(objects.Stacks, "truncated"));
    }

    /// <summary>一次性读取固定顶层对象，避免缺失 schema 被误报成全零状态。</summary>
    private static bool TryGetRequiredObjects(JsonElement payload, out UIKitPayloadObjects objects)
    {
        objects = default;
        if (!TryGetObject(payload, "root", out JsonElement root)
            || !TryGetObject(payload, "stats", out JsonElement stats)
            || !TryGetObject(payload, "cache", out JsonElement cache)
            || !TryGetObject(payload, "modal", out JsonElement modal)
            || !TryGetObject(payload, "panels", out JsonElement panels)
            || !TryGetObject(payload, "stacks", out JsonElement stacks))
        {
            return false;
        }

        objects = new UIKitPayloadObjects(root, stats, cache, modal, panels, stacks);
        return true;
    }

    /// <summary>读取面板、栈和生命周期数量。</summary>
    private static WorkbenchUIKitStats ReadStats(JsonElement stats, JsonElement states)
    {
        return new WorkbenchUIKitStats(
            ReadInt32(stats, "panelCount"),
            ReadInt32(stats, "stackCount"),
            ReadInt32(stats, "stackMembershipCount"),
            new WorkbenchUIKitPanelStates(
                ReadInt32(states, "preloaded"),
                ReadInt32(states, "opening"),
                ReadInt32(states, "open"),
                ReadInt32(states, "hiding"),
                ReadInt32(states, "hidden"),
                ReadInt32(states, "closing"),
                ReadInt32(states, "cached"),
                ReadInt32(states, "closed")));
    }

    /// <summary>读取缓存容量和策略数量。</summary>
    private static WorkbenchUIKitCache ReadCache(JsonElement cache)
    {
        return new WorkbenchUIKitCache(
            ReadInt32(cache, "capacity"),
            ReadInt32(cache, "transient"),
            ReadInt32(cache, "reusable"),
            ReadInt32(cache, "reusableCached"),
            ReadInt32(cache, "persistent"));
    }

    /// <summary>读取有界面板列表，并忽略不是对象的损坏条目。</summary>
    private static IReadOnlyList<WorkbenchUIKitPanel> ReadPanels(JsonElement panels)
    {
        if (!TryGetArray(panels, "items", out JsonElement items))
        {
            return Array.Empty<WorkbenchUIKitPanel>();
        }

        List<WorkbenchUIKitPanel> result = new();
        foreach (JsonElement item in items.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;
            result.Add(new WorkbenchUIKitPanel(
                ReadString(item, "type"),
                ReadString(item, "name"),
                ReadString(item, "state"),
                ReadString(item, "level"),
                ReadInt32(item, "levelOrder"),
                ReadInt32(item, "subLevel"),
                ReadString(item, "cachePolicy"),
                ReadBoolean(item, "modal"),
                ReadNullableString(item, "stack")));
        }

        return result;
    }

    /// <summary>读取有界命名栈列表，并忽略不是对象的损坏条目。</summary>
    private static IReadOnlyList<WorkbenchUIKitStack> ReadStacks(JsonElement stacks)
    {
        if (!TryGetArray(stacks, "items", out JsonElement items))
        {
            return Array.Empty<WorkbenchUIKitStack>();
        }

        List<WorkbenchUIKitStack> result = new();
        foreach (JsonElement item in items.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;
            result.Add(new WorkbenchUIKitStack(
                ReadString(item, "name"),
                ReadInt32(item, "depth"),
                ReadNullableString(item, "topPanelType"),
                ReadNullableString(item, "topPanelName")));
        }

        return result;
    }

    /// <summary>创建保留来源证据的安全空状态。</summary>
    private static WorkbenchUIKitState CreateEmpty(WorkbenchUIKitDataSource source, string reason)
    {
        WorkbenchUIKitDataSource failedSource = source.WithStaleReason(reason);
        return new WorkbenchUIKitState(
            failedSource,
            0,
            new WorkbenchUIKitRoot(false),
            new WorkbenchUIKitStats(0, 0, 0, new WorkbenchUIKitPanelStates(0, 0, 0, 0, 0, 0, 0, 0)),
            new WorkbenchUIKitCache(0, 0, 0, 0, 0),
            new WorkbenchUIKitModal(false, 0),
            Array.Empty<WorkbenchUIKitPanel>(),
            Array.Empty<WorkbenchUIKitStack>(),
            0, 0, false, 0, 0, false);
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

    /// <summary>安全读取字符串属性。</summary>
    private static string ReadString(JsonElement parent, string name)
    {
        return ReadNullableString(parent, name) ?? string.Empty;
    }

    /// <summary>安全读取可空字符串属性，并保留 JSON null 语义。</summary>
    private static string? ReadNullableString(JsonElement parent, string name)
    {
        return parent.ValueKind == JsonValueKind.Object
            && parent.TryGetProperty(name, out JsonElement value)
            && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
    }

    /// <summary>安全读取 Int32 属性。</summary>
    private static int ReadInt32(JsonElement parent, string name, int fallback = 0)
    {
        return parent.ValueKind == JsonValueKind.Object
            && parent.TryGetProperty(name, out JsonElement value)
            && value.TryGetInt32(out int result)
                ? result
                : fallback;
    }

    /// <summary>安全读取布尔属性。</summary>
    private static bool ReadBoolean(JsonElement parent, string name)
    {
        return parent.ValueKind == JsonValueKind.Object
            && parent.TryGetProperty(name, out JsonElement value)
            && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            && value.GetBoolean();
    }

    /// <summary>保存固定顶层对象，缩短主解析路径参数列表。</summary>
    private readonly record struct UIKitPayloadObjects(
        JsonElement Root,
        JsonElement Stats,
        JsonElement Cache,
        JsonElement Modal,
        JsonElement Panels,
        JsonElement Stacks);
}
