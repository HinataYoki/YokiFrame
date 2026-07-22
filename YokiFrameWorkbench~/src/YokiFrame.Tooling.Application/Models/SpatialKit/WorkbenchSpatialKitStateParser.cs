using System.Text.Json;

namespace YokiFrame.Tooling.Application.Models.SpatialKit;

/// <summary>把 SpatialKit payload 转换为稳定强类型 read model。</summary>
internal static class WorkbenchSpatialKitStateParser
{
    /// <summary>解析固定 schema；无效数据转为空状态并保留 stale 原因。</summary>
    internal static WorkbenchSpatialKitState Parse(WorkbenchSpatialKitDataSource source)
    {
        if (string.IsNullOrWhiteSpace(source.RawPayloadJson))
        {
            return CreateEmpty(source, "SpatialKit payload is empty.");
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(source.RawPayloadJson);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || ReadInt(root, "schemaVersion", 0) != 1)
            {
                return CreateEmpty(source, "SpatialKit payload requires schemaVersion 1.");
            }

            return ParseRoot(source, root);
        }
        catch (Exception exception) when (exception is JsonException or InvalidDataException)
        {
            return CreateEmpty(source, "SpatialKit payload is invalid: " + exception.Message);
        }
    }

    /// <summary>解析顶层统计与索引数组。</summary>
    private static WorkbenchSpatialKitState ParseRoot(
        WorkbenchSpatialKitDataSource source,
        JsonElement root)
    {
        List<WorkbenchSpatialIndex> indexes = new List<WorkbenchSpatialIndex>();
        if (root.TryGetProperty("indexes", out JsonElement items)
            && items.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in items.EnumerateArray())
            {
                indexes.Add(ReadIndex(item));
            }
        }

        return new WorkbenchSpatialKitState(
            source,
            ReadLong(root, "version", 0L),
            ReadInt(root, "stats", "activeIndexCount", ReadInt(root, "activeIndexCount", indexes.Count)),
            ReadInt(root, "stats", "entityCount", ReadInt(root, "entityCount", 0)),
            ReadInt(root, "stats", "partitionCount", ReadInt(root, "partitionCount", 0)),
            ReadInt(root, "stats", "hashGridCount", ReadInt(root, "hashGridCount", 0)),
            ReadInt(root, "stats", "quadtreeCount", ReadInt(root, "quadtreeCount", 0)),
            ReadInt(root, "stats", "octreeCount", ReadInt(root, "octreeCount", 0)),
            indexes,
            ReadBool(root, "indexesTruncated", false));
    }

    /// <summary>解析单个索引实例。</summary>
    private static WorkbenchSpatialIndex ReadIndex(JsonElement value)
    {
        RequireObject(value, "SpatialKit index");
        return new WorkbenchSpatialIndex(
            ReadString(value, "diagnosticsId"),
            ReadString(value, "indexKind"),
            ReadString(value, "entityTypeName"),
            ReadInt(value, "count", 0),
            ReadString(value, "plane"),
            ReadFloat(value, "cellSize", 0f),
            ReadInt(value, "maxDepth", 0),
            ReadInt(value, "maxEntitiesPerNode", 0),
            ReadInt(value, "partitionCount", 0),
            ReadDateTime(value, "createdAtUtc"),
            ReadBounds2D(value),
            ReadBounds3D(value),
            ReadDensity(value));
    }

    /// <summary>解析二维边界。</summary>
    private static WorkbenchSpatialBounds2D? ReadBounds2D(JsonElement value)
    {
        if (!value.TryGetProperty("bounds2D", out JsonElement bounds)
            || bounds.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return new WorkbenchSpatialBounds2D(
            ReadFloat(bounds, "x", 0f), ReadFloat(bounds, "y", 0f),
            ReadFloat(bounds, "width", 0f), ReadFloat(bounds, "height", 0f));
    }

    /// <summary>解析三维边界。</summary>
    private static WorkbenchSpatialBounds3D? ReadBounds3D(JsonElement value)
    {
        if (!value.TryGetProperty("bounds3D", out JsonElement bounds)
            || bounds.ValueKind != JsonValueKind.Object
            || !bounds.TryGetProperty("center", out JsonElement center)
            || !bounds.TryGetProperty("size", out JsonElement size))
        {
            return null;
        }

        return new WorkbenchSpatialBounds3D(ReadVector3(center), ReadVector3(size));
    }

    /// <summary>解析密度网格及热点摘要。</summary>
    private static WorkbenchSpatialDensity? ReadDensity(JsonElement value)
    {
        if (!value.TryGetProperty("density", out JsonElement density)
            || density.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        List<int> bins = ReadInts(density, "bins");
        List<WorkbenchSpatialHotspot> hotspots = new List<WorkbenchSpatialHotspot>();
        if (density.TryGetProperty("hotspots", out JsonElement hotspotArray)
            && hotspotArray.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement hotspot in hotspotArray.EnumerateArray())
            {
                hotspots.Add(new WorkbenchSpatialHotspot(
                    ReadInt(hotspot, "x", 0),
                    ReadInt(hotspot, "y", 0),
                    ReadInt(hotspot, "count", 0)));
            }
        }

        return new WorkbenchSpatialDensity(
            ReadString(density, "diagnosticsId"),
            ReadString(density, "indexKind"),
            ReadString(density, "plane"),
            ReadInt(density, "resolution", 0),
            ReadFloat(density, "minA", 0f), ReadFloat(density, "minB", 0f),
            ReadFloat(density, "maxA", 0f), ReadFloat(density, "maxB", 0f),
            ReadInt(density, "totalBins", bins.Count),
            ReadInt(density, "occupiedBins", 0), ReadInt(density, "minCount", 0),
            ReadInt(density, "meanCount", 0), ReadInt(density, "p95Count", 0),
            ReadInt(density, "maxCount", 0), bins, hotspots);
    }

    /// <summary>解析向量对象。</summary>
    private static WorkbenchSpatialVector3 ReadVector3(JsonElement value)
    {
        return new WorkbenchSpatialVector3(
            ReadFloat(value, "x", 0f), ReadFloat(value, "y", 0f), ReadFloat(value, "z", 0f));
    }

    /// <summary>解析整数数组并限制异常 payload 的单次分配。</summary>
    private static List<int> ReadInts(JsonElement root, string name)
    {
        List<int> result = new List<int>();
        if (!root.TryGetProperty(name, out JsonElement values)
            || values.ValueKind != JsonValueKind.Array)
        {
            return result;
        }

        const int MAX_BINS = 4096;
        foreach (JsonElement value in values.EnumerateArray())
        {
            if (result.Count >= MAX_BINS)
            {
                break;
            }

            if (value.TryGetInt32(out int count))
            {
                result.Add(Math.Max(0, count));
            }
        }

        return result;
    }

    /// <summary>创建解析失败时仍可安全绑定的空状态。</summary>
    private static WorkbenchSpatialKitState CreateEmpty(
        WorkbenchSpatialKitDataSource source,
        string reason)
    {
        return new WorkbenchSpatialKitState(
            source.WithStaleReason(reason), 0L, 0, 0, 0, 0, 0, 0,
            Array.Empty<WorkbenchSpatialIndex>(), false);
    }

    /// <summary>要求 JSON 值为对象。</summary>
    private static void RequireObject(JsonElement value, string scope)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException(scope + " must be an object.");
        }
    }

    /// <summary>读取字符串字段。</summary>
    private static string ReadString(JsonElement value, string name)
    {
        return value.TryGetProperty(name, out JsonElement property)
            && property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? string.Empty
            : string.Empty;
    }

    /// <summary>读取整数并提供降级默认值。</summary>
    private static int ReadInt(JsonElement value, string name, int fallback)
    {
        return value.TryGetProperty(name, out JsonElement property)
            && property.TryGetInt32(out int result) ? result : fallback;
    }

    /// <summary>读取嵌套对象中的整数并提供降级默认值。</summary>
    private static int ReadInt(JsonElement value, string objectName, string name, int fallback)
    {
        return value.TryGetProperty(objectName, out JsonElement nested)
            && nested.ValueKind == JsonValueKind.Object
            ? ReadInt(nested, name, fallback)
            : fallback;
    }

    /// <summary>读取长整数并提供降级默认值。</summary>
    private static long ReadLong(JsonElement value, string name, long fallback)
    {
        return value.TryGetProperty(name, out JsonElement property)
            && property.TryGetInt64(out long result) ? result : fallback;
    }

    /// <summary>读取有限浮点数并提供降级默认值。</summary>
    private static float ReadFloat(JsonElement value, string name, float fallback)
    {
        return value.TryGetProperty(name, out JsonElement property)
            && property.TryGetSingle(out float result)
            && !float.IsNaN(result) && !float.IsInfinity(result) ? result : fallback;
    }

    /// <summary>读取布尔值并提供降级默认值。</summary>
    private static bool ReadBool(JsonElement value, string name, bool fallback)
    {
        return value.TryGetProperty(name, out JsonElement property)
            && (property.ValueKind == JsonValueKind.True || property.ValueKind == JsonValueKind.False)
            ? property.GetBoolean() : fallback;
    }

    /// <summary>读取 UTC 时间并在无效时返回最小值。</summary>
    private static DateTimeOffset ReadDateTime(JsonElement value, string name)
    {
        string text = ReadString(value, name);
        return DateTimeOffset.TryParse(text, out DateTimeOffset result)
            ? result.ToUniversalTime() : DateTimeOffset.MinValue;
    }
}
