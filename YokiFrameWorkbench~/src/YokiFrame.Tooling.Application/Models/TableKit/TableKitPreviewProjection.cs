using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace YokiFrame.Tooling.Application.Models.TableKit;

/// <summary>把 Luban JSON 预览投影为有限记录和字段，避免 UI 层解析 JSON。</summary>
public static partial class TableKitPreviewProjection
{
    private const int MAX_RECORD_PREVIEW_COUNT = 200;
    private static readonly string[] sPreferredCollectionKeys = { "data", "items", "rows", "list" };
    private static readonly JsonSerializerOptions sIndentedJsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };
    private static readonly TableKitPreviewJsonContext sJsonContext = new(sIndentedJsonOptions);

    /// <summary>解析一张预览表，最多物化固定数量的记录。</summary>
    /// <param name="table">Application 返回的原始预览表。</param>
    /// <returns>记录集合及是否因上限发生截断。</returns>
    public static (IReadOnlyList<TableKitPreviewRecord> Records, bool IsTruncated) CreateRecords(TableKitPreviewTable table)
    {
        if (string.IsNullOrWhiteSpace(table.PreviewJson))
        {
            return (Array.Empty<TableKitPreviewRecord>(), false);
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(table.PreviewJson);
            List<TableKitPreviewRecord> records = new();
            bool isTruncated = CollectRecords(document.RootElement, records);
            return (records, isTruncated);
        }
        catch (JsonException)
        {
            return (Array.Empty<TableKitPreviewRecord>(), false);
        }
    }

    /// <summary>按数组、常见集合字段或对象本身收集记录。</summary>
    /// <param name="root">表 JSON 根节点。</param>
    /// <param name="records">接收记录的缓冲区。</param>
    /// <returns>记录超过预览上限时返回 true。</returns>
    private static bool CollectRecords(JsonElement root, List<TableKitPreviewRecord> records)
    {
        if (root.ValueKind == JsonValueKind.Array)
        {
            return AddArrayRecords(root, records);
        }

        if (root.ValueKind != JsonValueKind.Object)
        {
            records.Add(CreateRecord(root, 0));
            return false;
        }

        JsonElement collection = FindRecordCollection(root);
        if (collection.ValueKind == JsonValueKind.Array)
        {
            return AddArrayRecords(collection, records);
        }

        records.Add(CreateRecord(root, 0));
        return false;
    }

    /// <summary>把数组前固定数量的元素转换为记录。</summary>
    /// <param name="array">记录数组。</param>
    /// <param name="records">接收记录的缓冲区。</param>
    /// <returns>数组仍有未投影元素时返回 true。</returns>
    private static bool AddArrayRecords(JsonElement array, List<TableKitPreviewRecord> records)
    {
        foreach (JsonElement element in array.EnumerateArray())
        {
            if (records.Count >= MAX_RECORD_PREVIEW_COUNT)
            {
                return true;
            }

            records.Add(CreateRecord(element, records.Count));
        }

        return false;
    }

    /// <summary>创建一条记录及其字段投影。</summary>
    /// <param name="element">记录 JSON 节点。</param>
    /// <param name="index">记录的零基索引。</param>
    /// <returns>可供页面直接显示的记录。</returns>
    private static TableKitPreviewRecord CreateRecord(JsonElement element, int index)
    {
        IReadOnlyList<TableKitPreviewField> fields = CreateFields(element);
        return new TableKitPreviewRecord
        {
            Index = index + 1,
            Kind = element.ValueKind == JsonValueKind.Object ? "object" : element.ValueKind.ToString().ToLowerInvariant(),
            Title = (index + 1) + ". " + ResolveIdentity(element, index + 1),
            Fields = fields,
            PreviewJson = JsonSerializer.Serialize(element, sJsonContext.JsonElement)
        };
    }

    /// <summary>把对象属性或标量值转换为字段集合。</summary>
    /// <param name="element">记录 JSON 节点。</param>
    /// <returns>字段投影。</returns>
    private static IReadOnlyList<TableKitPreviewField> CreateFields(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return new[] { CreateField("value", element) };
        }

        return element.EnumerateObject()
            .Select(static property => CreateField(property.Name, property.Value))
            .ToArray();
    }

    /// <summary>创建单个字段的类型和值摘要。</summary>
    /// <param name="name">字段名。</param>
    /// <param name="element">字段 JSON 节点。</param>
    /// <returns>有限长度的字段投影。</returns>
    private static TableKitPreviewField CreateField(string name, JsonElement element)
    {
        string fullValue = Truncate(ResolveValueText(element), 4096);
        return new TableKitPreviewField
        {
            Name = name,
            TypeName = ResolveTypeName(element),
            FullValueText = fullValue,
            ValueText = Truncate(fullValue, 96),
            IsString = element.ValueKind == JsonValueKind.String,
            IsNumber = element.ValueKind == JsonValueKind.Number,
            IsBoolean = element.ValueKind is JsonValueKind.True or JsonValueKind.False,
            IsComplex = element.ValueKind is JsonValueKind.Array or JsonValueKind.Object
        };
    }

    /// <summary>优先使用 id、key 或 name 生成稳定记录标题。</summary>
    /// <param name="element">记录 JSON 节点。</param>
    /// <param name="index">记录的一基序号。</param>
    /// <returns>短标题文本。</returns>
    private static string ResolveIdentity(JsonElement element, int index)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (string key in new[] { "id", "Id", "key", "Key", "name", "Name" })
            {
                if (element.TryGetProperty(key, out JsonElement value) && IsScalar(value))
                {
                    return ScalarText(value);
                }
            }
        }

        return "记录 " + index;
    }

    /// <summary>优先查找常见集合字段，否则使用对象中的首个数组。</summary>
    /// <param name="root">对象类型的表 JSON 根节点。</param>
    /// <returns>记录数组；未找到时返回未定义节点。</returns>
    private static JsonElement FindRecordCollection(JsonElement root)
    {
        foreach (string key in sPreferredCollectionKeys)
        {
            if (root.TryGetProperty(key, out JsonElement value) && value.ValueKind == JsonValueKind.Array)
            {
                return value;
            }
        }

        foreach (JsonProperty property in root.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.Array)
            {
                return property.Value;
            }
        }

        return default;
    }

    /// <summary>判断节点是否适合作为短标题。</summary>
    /// <param name="element">待检查节点。</param>
    /// <returns>字符串、数字或布尔节点返回 true。</returns>
    private static bool IsScalar(JsonElement element)
    {
        return element.ValueKind is JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False;
    }

    /// <summary>读取标量节点的无引号文本。</summary>
    /// <param name="element">标量 JSON 节点。</param>
    /// <returns>用于标题展示的文本。</returns>
    private static string ScalarText(JsonElement element)
    {
        return element.ValueKind == JsonValueKind.String
            ? element.GetString() ?? string.Empty
            : element.GetRawText();
    }

    /// <summary>把 JSON 值类型转换为短类型名。</summary>
    /// <param name="element">字段 JSON 节点。</param>
    /// <returns>小写类型名。</returns>
    private static string ResolveTypeName(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => "string",
            JsonValueKind.Number => "number",
            JsonValueKind.True or JsonValueKind.False => "boolean",
            JsonValueKind.Array => "array",
            JsonValueKind.Object => "object",
            JsonValueKind.Null => "null",
            _ => "unknown"
        };
    }

    /// <summary>生成紧凑的字段值文本。</summary>
    /// <param name="element">字段 JSON 节点。</param>
    /// <returns>字段值摘要。</returns>
    private static string ResolveValueText(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.Array => element.GetArrayLength() + " 项",
            JsonValueKind.Object => CountObjectProperties(element) + " 字段",
            JsonValueKind.Null => "null",
            _ => element.GetRawText()
        };
    }

    /// <summary>统计对象的直接属性数量。</summary>
    /// <param name="element">对象类型 JSON 节点。</param>
    /// <returns>直接属性数量。</returns>
    private static int CountObjectProperties(JsonElement element)
    {
        int count = 0;
        foreach (JsonProperty _ in element.EnumerateObject())
        {
            count++;
        }

        return count;
    }

    /// <summary>限制字段文本长度，避免预览持有超大字符串。</summary>
    /// <param name="value">原始文本。</param>
    /// <param name="maxLength">允许保留的最大字符数。</param>
    /// <returns>原文本或追加省略号的受限文本。</returns>
    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength] + "...";
    }

}

/// <summary>为 Native AOT 记录格式化提供无反射 JSON 元数据。</summary>
[JsonSerializable(typeof(JsonElement))]
internal sealed partial class TableKitPreviewJsonContext : JsonSerializerContext
{
}

/// <summary>TableKit 预览中的一条已解析记录。</summary>
public sealed record TableKitPreviewRecord
{
    /// <summary>记录的一基序号。</summary>
    public required int Index { get; init; }

    /// <summary>用于列表展示的记录标识。</summary>
    public required string Title { get; init; }

    /// <summary>JSON 记录类型。</summary>
    public required string Kind { get; init; }

    /// <summary>结构化字段投影。</summary>
    public required IReadOnlyList<TableKitPreviewField> Fields { get; init; }

    /// <summary>当前记录格式化后的 JSON。</summary>
    public required string PreviewJson { get; init; }
}

/// <summary>TableKit 记录中的一个已解析字段。</summary>
public sealed record TableKitPreviewField
{
    /// <summary>字段名。</summary>
    public required string Name { get; init; }

    /// <summary>字段类型名。</summary>
    public required string TypeName { get; init; }

    /// <summary>受控长度的字段值预览。</summary>
    public required string ValueText { get; init; }

    /// <summary>用于悬浮提示的受控长度字段值。</summary>
    public required string FullValueText { get; init; }

    /// <summary>字段是否为字符串。</summary>
    public required bool IsString { get; init; }

    /// <summary>字段是否为数字。</summary>
    public required bool IsNumber { get; init; }

    /// <summary>字段是否为布尔值。</summary>
    public required bool IsBoolean { get; init; }

    /// <summary>字段是否为数组或对象。</summary>
    public required bool IsComplex { get; init; }
}
