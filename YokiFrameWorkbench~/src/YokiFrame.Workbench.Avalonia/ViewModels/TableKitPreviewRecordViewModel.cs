using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace YokiFrame.Workbench.Avalonia.ViewModels;

/// <summary>TableKit 预览中的单条配置记录。</summary>
public sealed partial class TableKitPreviewRecordViewModel
{
    private static readonly JsonSerializerOptions sIndentedJsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };
    private static readonly TableKitPreviewJsonContext sJsonContext = new(sIndentedJsonOptions);

    /// <summary>从 JSON 节点创建记录投影。</summary>
    /// <param name="element">记录 JSON 节点。</param>
    /// <param name="index">记录在表内的零基索引。</param>
    public TableKitPreviewRecordViewModel(JsonElement element, int index)
    {
        Index = index + 1;
        Kind = element.ValueKind == JsonValueKind.Object ? "object" : element.ValueKind.ToString().ToLowerInvariant();
        Fields = CreateFields(element);
        Title = Index + ". " + ResolveIdentity(element, Index);
        FieldCountText = Fields.Count + " 字段";
        PreviewJson = JsonSerializer.Serialize(element, sJsonContext.JsonElement);
    }

    /// <summary>记录的一基序号。</summary>
    public int Index { get; }
    /// <summary>用于列表展示的记录标识。</summary>
    public string Title { get; }
    /// <summary>JSON 记录类型。</summary>
    public string Kind { get; }
    /// <summary>记录字段数量摘要。</summary>
    public string FieldCountText { get; }
    /// <summary>结构化字段投影。</summary>
    public IReadOnlyList<TableKitPreviewFieldViewModel> Fields { get; }
    /// <summary>当前记录格式化后的原始 JSON。</summary>
    public string PreviewJson { get; }

    /// <summary>把对象属性或标量值转换为字段集合。</summary>
    /// <param name="element">记录节点。</param>
    /// <returns>可供字段区展示的投影。</returns>
    private static IReadOnlyList<TableKitPreviewFieldViewModel> CreateFields(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return new[] { new TableKitPreviewFieldViewModel("value", element) };
        }

        return element.EnumerateObject()
            .Select(static property => new TableKitPreviewFieldViewModel(property.Name, property.Value))
            .ToArray();
    }

    /// <summary>优先使用 id/key/name 字段生成稳定的记录标题。</summary>
    /// <param name="element">记录节点。</param>
    /// <param name="index">记录的一基序号。</param>
    /// <returns>短记录标识。</returns>
    private static string ResolveIdentity(JsonElement element, int index)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (string key in new[] { "id", "Id", "key", "Key", "name", "Name" })
            {
                if (element.TryGetProperty(key, out JsonElement value) && IsScalar(value)) return ScalarText(value);
            }
        }

        return "记录 " + index;
    }

    /// <summary>判断节点是否适合作为短标题标识。</summary>
    /// <param name="element">待检查节点。</param>
    /// <returns>字符串、数字或布尔节点返回 true。</returns>
    private static bool IsScalar(JsonElement element) => element.ValueKind is JsonValueKind.String
        or JsonValueKind.Number
        or JsonValueKind.True
        or JsonValueKind.False;

    /// <summary>读取标量节点的无引号文本。</summary>
    /// <param name="element">标量 JSON 节点。</param>
    /// <returns>用于标题展示的文本。</returns>
    private static string ScalarText(JsonElement element) => element.ValueKind == JsonValueKind.String
        ? element.GetString() ?? string.Empty
        : element.GetRawText();

    /// <summary>为 Native AOT 预览记录格式化提供无反射 JSON 元数据。</summary>
    [JsonSerializable(typeof(JsonElement))]
    private sealed partial class TableKitPreviewJsonContext : JsonSerializerContext
    {
    }
}
