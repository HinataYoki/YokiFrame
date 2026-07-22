using System.Text.Json;

namespace YokiFrame.Tooling.Application.Models.AudioKit;

/// <summary>把 Runtime AudioKit payload 转换为稳定强类型 read model。</summary>
internal static class WorkbenchAudioKitStateParser
{
    /// <summary>解析固定 schema；无效数据转为携带 stale 原因的空状态。</summary>
    internal static WorkbenchAudioKitState Parse(WorkbenchAudioKitDataSource source)
    {
        if (string.IsNullOrWhiteSpace(source.RawPayloadJson))
            return CreateEmpty(source, "AudioKit payload is empty.");
        try
        {
            using JsonDocument document = JsonDocument.Parse(source.RawPayloadJson);
            JsonElement root = document.RootElement;
            if (!HasValidSchema(root))
                return CreateEmpty(source, "AudioKit payload requires schemaVersion 1 and fixed objects or arrays.");
            return ParseRoot(source, root);
        }
        catch (Exception exception) when (exception is JsonException or InvalidDataException)
        {
            return CreateEmpty(source, "AudioKit payload is invalid: " + exception.Message);
        }
    }

    /// <summary>验证固定 schema 和页面依赖的顶层结构。</summary>
    private static bool HasValidSchema(JsonElement root)
    {
        return root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty("schemaVersion", out JsonElement schema)
            && schema.ValueKind == JsonValueKind.Number
            && schema.TryGetInt32(out int version)
            && version == 1
            && HasKind(root, "backend", JsonValueKind.Object)
            && HasKind(root, "master", JsonValueKind.Object)
            && HasKind(root, "buses", JsonValueKind.Array)
            && HasKind(root, "voices", JsonValueKind.Array)
            && HasKind(root, "history", JsonValueKind.Array);
    }

    /// <summary>验证指定属性存在且使用要求的 JSON 类型。</summary>
    private static bool HasKind(JsonElement root, string name, JsonValueKind kind)
    {
        return root.TryGetProperty(name, out JsonElement value) && value.ValueKind == kind;
    }

    /// <summary>解析已经确认顶层结构的完整状态。</summary>
    private static WorkbenchAudioKitState ParseRoot(
        WorkbenchAudioKitDataSource source,
        JsonElement root)
    {
        IReadOnlyList<WorkbenchAudioBus> buses = ReadBuses(root.GetProperty("buses"));
        IReadOnlyList<WorkbenchAudioVoice> voices = ReadVoices(root.GetProperty("voices"));
        IReadOnlyList<WorkbenchAudioHistoryEntry> history = ReadHistory(root.GetProperty("history"));
        return new WorkbenchAudioKitState(
            source,
            ReadLong(root, "version"),
            ReadBackend(root.GetProperty("backend")),
            ReadMaster(root.GetProperty("master")),
            buses,
            voices,
            history,
            ReadInt(root, "busTotal", buses.Count),
            ReadInt(root, "voiceTotal", voices.Count),
            ReadLong(root, "historyTotal", history.Count),
            ReadBool(root, "busesTruncated"),
            ReadBool(root, "voicesTruncated"),
            ReadBool(root, "historyTruncated"));
    }

    /// <summary>解析后端能力摘要。</summary>
    private static WorkbenchAudioBackend ReadBackend(JsonElement value)
    {
        return new WorkbenchAudioBackend(
            ReadString(value, "name"),
            ReadInt(value, "capabilities"),
            ReadString(value, "capabilityNames"),
            ReadString(value, "resourceLoader"));
    }

    /// <summary>解析 Master 混音状态。</summary>
    private static WorkbenchAudioMaster ReadMaster(JsonElement value)
    {
        return new WorkbenchAudioMaster(
            ReadFloat(value, "volume"),
            ReadFloat(value, "effectiveVolume"),
            ReadBool(value, "muted"),
            ReadInt(value, "activeVoiceCount"));
    }

    /// <summary>解析有界总线数组。</summary>
    private static IReadOnlyList<WorkbenchAudioBus> ReadBuses(JsonElement array)
    {
        List<WorkbenchAudioBus> result = new();
        foreach (JsonElement item in array.EnumerateArray())
        {
            RequireObject(item, "AudioKit bus");
            result.Add(new WorkbenchAudioBus(
                ReadString(item, "name"),
                ReadFloat(item, "volume"),
                ReadFloat(item, "effectiveVolume"),
                ReadBool(item, "muted"),
                ReadBool(item, "isMaster"),
                ReadInt(item, "activeVoiceCount"),
                ReadBool(item, "isBuiltIn"),
                ReadBool(item, "isRegistered")));
        }
        return result;
    }

    /// <summary>解析有界 active voice 数组。</summary>
    private static IReadOnlyList<WorkbenchAudioVoice> ReadVoices(JsonElement array)
    {
        List<WorkbenchAudioVoice> result = new();
        foreach (JsonElement item in array.EnumerateArray()) result.Add(ReadVoice(item));
        return result;
    }

    /// <summary>解析单个 active voice 及其位置。</summary>
    private static WorkbenchAudioVoice ReadVoice(JsonElement item)
    {
        RequireObject(item, "AudioKit voice");
        JsonElement position = RequireProperty(item, "position", JsonValueKind.Object);
        return new WorkbenchAudioVoice(
            ReadLong(item, "backendGeneration"), ReadInt(item, "voiceId"),
            ReadString(item, "path"), ReadString(item, "bus"), ReadString(item, "backendName"),
            ReadBool(item, "loop"), ReadBool(item, "playing"), ReadBool(item, "paused"),
            ReadFloat(item, "volume"), ReadFloat(item, "pitch"),
            ReadFloat(item, "duration"), ReadFloat(item, "elapsed"), ReadBool(item, "is3D"),
            new WorkbenchAudioPosition(
                ReadFloat(position, "x"), ReadFloat(position, "y"), ReadFloat(position, "z")),
            ReadString(item, "followTarget"), ReadFloat(item, "minDistance"),
            ReadFloat(item, "maxDistance"), ReadString(item, "rolloffMode"));
    }

    /// <summary>解析最新优先的有界历史数组。</summary>
    private static IReadOnlyList<WorkbenchAudioHistoryEntry> ReadHistory(JsonElement array)
    {
        List<WorkbenchAudioHistoryEntry> result = new();
        foreach (JsonElement item in array.EnumerateArray())
        {
            RequireObject(item, "AudioKit history entry");
            result.Add(new WorkbenchAudioHistoryEntry(
                ReadLong(item, "sequence"), ReadString(item, "eventType"),
                ReadLong(item, "backendGeneration"), ReadInt(item, "voiceId"),
                ReadString(item, "path"), ReadString(item, "bus"),
                ReadFloat(item, "volume"), ReadString(item, "timestampUtc")));
        }
        return result;
    }

    /// <summary>创建解析失败时仍可安全绑定的空状态。</summary>
    private static WorkbenchAudioKitState CreateEmpty(
        WorkbenchAudioKitDataSource source,
        string reason)
    {
        return new WorkbenchAudioKitState(
            source.WithStaleReason(reason), 0L,
            new WorkbenchAudioBackend("Unavailable", 0, "None", string.Empty),
            new WorkbenchAudioMaster(1f, 1f, false, 0),
            Array.Empty<WorkbenchAudioBus>(), Array.Empty<WorkbenchAudioVoice>(),
            Array.Empty<WorkbenchAudioHistoryEntry>(), 0, 0, 0L, false, false, false);
    }

    /// <summary>要求值为 JSON 对象。</summary>
    private static void RequireObject(JsonElement value, string scope)
    {
        if (value.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException(scope + " must be an object.");
    }

    /// <summary>读取指定类型的必需属性。</summary>
    private static JsonElement RequireProperty(JsonElement value, string name, JsonValueKind kind)
    {
        if (!value.TryGetProperty(name, out JsonElement property) || property.ValueKind != kind)
            throw new InvalidDataException("AudioKit field " + name + " has an invalid type.");
        return property;
    }

    /// <summary>读取必需字符串。</summary>
    private static string ReadString(JsonElement value, string name)
    {
        return RequireProperty(value, name, JsonValueKind.String).GetString() ?? string.Empty;
    }

    /// <summary>读取必需布尔值。</summary>
    private static bool ReadBool(JsonElement value, string name)
    {
        if (!value.TryGetProperty(name, out JsonElement property)
            || property.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            throw new InvalidDataException("AudioKit field " + name + " must be a boolean.");
        return property.GetBoolean();
    }

    /// <summary>读取必需或带默认值的整数。</summary>
    private static int ReadInt(JsonElement value, string name, int fallback = int.MinValue)
    {
        if (!value.TryGetProperty(name, out JsonElement property) || !property.TryGetInt32(out int result))
        {
            if (fallback != int.MinValue) return fallback;
            throw new InvalidDataException("AudioKit field " + name + " must be an integer.");
        }
        return result;
    }

    /// <summary>读取必需或带默认值的长整数。</summary>
    private static long ReadLong(JsonElement value, string name, long fallback = long.MinValue)
    {
        if (!value.TryGetProperty(name, out JsonElement property) || !property.TryGetInt64(out long result))
        {
            if (fallback != long.MinValue) return fallback;
            throw new InvalidDataException("AudioKit field " + name + " must be a long integer.");
        }
        return result;
    }

    /// <summary>读取必需有限单精度数值。</summary>
    private static float ReadFloat(JsonElement value, string name)
    {
        if (!value.TryGetProperty(name, out JsonElement property) || !property.TryGetSingle(out float result)
            || float.IsNaN(result) || float.IsInfinity(result))
            throw new InvalidDataException("AudioKit field " + name + " must be a finite number.");
        return result;
    }
}
