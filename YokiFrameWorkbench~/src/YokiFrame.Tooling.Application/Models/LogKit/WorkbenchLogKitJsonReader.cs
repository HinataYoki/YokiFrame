using System.Text.Json;

namespace YokiFrame.Tooling.Application.Models.LogKit;

/// <summary>集中提供 LogKit schema 的无异常标量读取。</summary>
internal static class WorkbenchLogKitJsonReader
{
    /// <summary>读取对象属性；缺失或类型不匹配时返回默认元素。</summary>
    internal static JsonElement ReadObject(JsonElement parent, string name)
    {
        return parent.ValueKind == JsonValueKind.Object
            && parent.TryGetProperty(name, out var value)
            && value.ValueKind == JsonValueKind.Object
                ? value
                : default;
    }

    /// <summary>读取数组属性；缺失或类型不匹配时返回默认元素。</summary>
    internal static JsonElement ReadArray(JsonElement parent, string name)
    {
        return parent.ValueKind == JsonValueKind.Object
            && parent.TryGetProperty(name, out var value)
            && value.ValueKind == JsonValueKind.Array
                ? value
                : default;
    }

    /// <summary>读取字符串属性。</summary>
    internal static string ReadString(JsonElement parent, string name, string fallback = "")
    {
        return parent.ValueKind == JsonValueKind.Object
            && parent.TryGetProperty(name, out var value)
            && value.ValueKind == JsonValueKind.String
                ? value.GetString() ?? fallback
                : fallback;
    }

    /// <summary>读取布尔属性。</summary>
    internal static bool ReadBoolean(JsonElement parent, string name, bool fallback = false)
    {
        return parent.ValueKind == JsonValueKind.Object
            && parent.TryGetProperty(name, out var value)
            && value.ValueKind is JsonValueKind.True or JsonValueKind.False
                ? value.GetBoolean()
                : fallback;
    }

    /// <summary>读取 Int32 属性。</summary>
    internal static int ReadInt32(JsonElement parent, string name, int fallback = 0)
    {
        return parent.ValueKind == JsonValueKind.Object
            && parent.TryGetProperty(name, out var value)
            && value.TryGetInt32(out var result)
                ? result
                : fallback;
    }

    /// <summary>读取 Int64 属性。</summary>
    internal static long ReadInt64(JsonElement parent, string name, long fallback = 0L)
    {
        return parent.ValueKind == JsonValueKind.Object
            && parent.TryGetProperty(name, out var value)
            && value.TryGetInt64(out var result)
                ? result
                : fallback;
    }
}
