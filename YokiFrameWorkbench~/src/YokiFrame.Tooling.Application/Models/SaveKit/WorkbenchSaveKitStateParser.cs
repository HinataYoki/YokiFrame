using System.Text.Json;

namespace YokiFrame.Tooling.Application.Models.SaveKit;

/// <summary>把 SaveKit state payload 转换为 Avalonia 只消费的强类型 read model。</summary>
internal static class WorkbenchSaveKitStateParser
{
    private const int MAX_RUNTIME_METADATA_PER_KIND = 64;

    /// <summary>解析 schemaVersion 1 state；无效输入回落空状态并记录 stale 原因。</summary>
    internal static WorkbenchSaveKitState Parse(WorkbenchSaveKitDataSource source)
    {
        if (string.IsNullOrWhiteSpace(source.RawPayloadJson))
        {
            return CreateEmpty(source, "SaveKit payload is empty.");
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(source.RawPayloadJson);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || ReadInt(root, "schemaVersion", 0) != 1)
            {
                return CreateEmpty(source, "SaveKit payload requires schemaVersion 1.");
            }

            return ParseRoot(source, root);
        }
        catch (Exception exception) when (exception is JsonException or InvalidDataException)
        {
            return CreateEmpty(source, "SaveKit payload is invalid: " + exception.Message);
        }
    }

    /// <summary>解析完整 Runtime 状态及两类容器头列表。</summary>
    private static WorkbenchSaveKitState ParseRoot(WorkbenchSaveKitDataSource source, JsonElement root)
    {
        List<WorkbenchSaveKitRuntimeMeta> slots = ReadMetadata(root, "slots");
        List<WorkbenchSaveKitRuntimeMeta> globals = ReadMetadata(root, "globals");
        return new WorkbenchSaveKitState(
            source,
            ReadLong(root, "version", 0L),
            ReadBackend(root),
            ReadAutoSave(root),
            slots,
            ReadNonNegativeInt(root, "slotCount", slots.Count),
            Math.Max(slots.Count, ReadNonNegativeInt(root, "slotTotal", slots.Count)),
            ReadBool(root, "slotsTruncated", false),
            globals,
            ReadNonNegativeInt(root, "globalCount", globals.Count),
            Math.Max(globals.Count, ReadNonNegativeInt(root, "globalTotal", globals.Count)),
            ReadBool(root, "globalsTruncated", false),
            ReadBool(root, "metadataAvailable", false),
            ReadBool(root, "metadataReadFailed", false));
    }

    /// <summary>解析后端配置对象；缺失字段按未初始化状态处理。</summary>
    private static WorkbenchSaveKitBackend ReadBackend(JsonElement root)
    {
        if (!root.TryGetProperty("backend", out JsonElement backend)
            || backend.ValueKind != JsonValueKind.Object)
        {
            return new WorkbenchSaveKitBackend(false, false, false, string.Empty, string.Empty, string.Empty);
        }

        return new WorkbenchSaveKitBackend(
            ReadBool(backend, "storageConfigured", false),
            ReadBool(backend, "serializerConfigured", false),
            ReadBool(backend, "ready", false),
            ReadString(backend, "storageType"),
            ReadString(backend, "serializerId"),
            ReadString(backend, "encryptorId"));
    }

    /// <summary>解析自动保存状态；未启用时明确不保留默认 Slot(0) 目标。</summary>
    private static WorkbenchSaveKitAutoSave ReadAutoSave(JsonElement root)
    {
        if (!root.TryGetProperty("autoSave", out JsonElement autoSave)
            || autoSave.ValueKind != JsonValueKind.Object)
        {
            return new WorkbenchSaveKitAutoSave(false, null, 0f, 0f);
        }

        bool enabled = ReadBool(autoSave, "enabled", false);
        return new WorkbenchSaveKitAutoSave(
            enabled,
            enabled ? ReadTarget(autoSave, "target") : null,
            ReadFiniteFloat(autoSave, "intervalSeconds", 0f),
            ReadFiniteFloat(autoSave, "elapsedSeconds", 0f));
    }

    /// <summary>读取一类最多 64 个安全容器头，拒绝异常 payload 造成大集合分配。</summary>
    private static List<WorkbenchSaveKitRuntimeMeta> ReadMetadata(JsonElement root, string propertyName)
    {
        List<WorkbenchSaveKitRuntimeMeta> result = new();
        if (!root.TryGetProperty(propertyName, out JsonElement values)
            || values.ValueKind != JsonValueKind.Array)
        {
            return result;
        }

        foreach (JsonElement value in values.EnumerateArray())
        {
            if (result.Count >= MAX_RUNTIME_METADATA_PER_KIND)
            {
                break;
            }

            if (value.ValueKind == JsonValueKind.Object
                && TryReadMetadataEntry(value, out WorkbenchSaveKitRuntimeMeta entry))
            {
                result.Add(entry);
            }
        }

        return result;
    }

    /// <summary>读取一个容器头字段集；没有有效目标对象的条目不会进入列表。</summary>
    private static bool TryReadMetadataEntry(JsonElement value, out WorkbenchSaveKitRuntimeMeta entry)
    {
        WorkbenchSaveKitTarget? target = ReadTarget(value, "target");
        if (target == null || string.IsNullOrWhiteSpace(target.Kind) || string.IsNullOrWhiteSpace(target.Name))
        {
            entry = null!;
            return false;
        }

        entry = new WorkbenchSaveKitRuntimeMeta(
            target,
            ReadString(value, "displayName"),
            ReadInt(value, "containerVersion", 0),
            ReadLong(value, "createdTimestamp", 0L),
            ReadLong(value, "lastSavedTimestamp", 0L),
            ReadString(value, "serializerId"));
        return true;
    }

    /// <summary>解析嵌套目标对象；null 或非对象表示未提供目标。</summary>
    private static WorkbenchSaveKitTarget? ReadTarget(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out JsonElement value)
            || value.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return new WorkbenchSaveKitTarget(
            ReadString(value, "kind"),
            ReadString(value, "name"),
            ReadInt(value, "slotId", -1));
    }

    /// <summary>创建解析失败时仍可安全绑定的空状态。</summary>
    private static WorkbenchSaveKitState CreateEmpty(WorkbenchSaveKitDataSource source, string reason)
    {
        return new WorkbenchSaveKitState(
            source.WithStaleReason(reason),
            0L,
            new WorkbenchSaveKitBackend(false, false, false, string.Empty, string.Empty, string.Empty),
            new WorkbenchSaveKitAutoSave(false, null, 0f, 0f),
            Array.Empty<WorkbenchSaveKitRuntimeMeta>(),
            0,
            0,
            false,
            Array.Empty<WorkbenchSaveKitRuntimeMeta>(),
            0,
            0,
            false,
            false,
            false);
    }

    /// <summary>读取字符串字段；不存在或类型不匹配时返回空。</summary>
    private static string ReadString(JsonElement value, string name)
    {
        return value.TryGetProperty(name, out JsonElement property)
            && property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? string.Empty
            : string.Empty;
    }

    /// <summary>读取整数字段；不存在或溢出时使用回退值。</summary>
    private static int ReadInt(JsonElement value, string name, int fallback)
    {
        return value.TryGetProperty(name, out JsonElement property)
            && property.TryGetInt32(out int result)
            ? result
            : fallback;
    }

    /// <summary>读取非负计数字段，拒绝异常 payload 把负数带入统计 UI。</summary>
    private static int ReadNonNegativeInt(JsonElement value, string name, int fallback)
    {
        return Math.Max(0, ReadInt(value, name, fallback));
    }

    /// <summary>读取长整数字段；不存在或溢出时使用回退值。</summary>
    private static long ReadLong(JsonElement value, string name, long fallback)
    {
        return value.TryGetProperty(name, out JsonElement property)
            && property.TryGetInt64(out long result)
            ? result
            : fallback;
    }

    /// <summary>读取布尔字段；不存在或不是布尔值时使用回退值。</summary>
    private static bool ReadBool(JsonElement value, string name, bool fallback)
    {
        return value.TryGetProperty(name, out JsonElement property)
            && (property.ValueKind == JsonValueKind.True || property.ValueKind == JsonValueKind.False)
            ? property.GetBoolean()
            : fallback;
    }

    /// <summary>读取有限浮点数，拒绝 NaN 和 Infinity 进入 ViewModel。</summary>
    private static float ReadFiniteFloat(JsonElement value, string name, float fallback)
    {
        return value.TryGetProperty(name, out JsonElement property)
            && property.TryGetSingle(out float result)
            && !float.IsNaN(result)
            && !float.IsInfinity(result)
                ? result
                : fallback;
    }
}
