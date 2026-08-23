using System.Text.Json;
using YokiFrame.Workbench.Avalonia.Services;

namespace YokiFrame.Workbench.Avalonia.ViewModels;

/// <summary>TableKit 记录中的单个结构化字段。</summary>
public sealed class TableKitPreviewFieldViewModel
{
    private const int MAX_PREVIEW_LENGTH = 96;
    private const int MAX_TOOLTIP_LENGTH = 4096;

    /// <summary>从 JSON 属性创建字段投影。</summary>
    /// <param name="name">字段名。</param>
    /// <param name="element">字段值节点。</param>
    public TableKitPreviewFieldViewModel(string name, JsonElement element)
    {
        Name = name;
        TypeName = ResolveTypeName(element);
        FullValueText = Truncate(ResolveValueText(element), MAX_TOOLTIP_LENGTH);
        ValueText = Truncate(FullValueText, MAX_PREVIEW_LENGTH);
        IsString = element.ValueKind == JsonValueKind.String;
        IsNumber = element.ValueKind == JsonValueKind.Number;
        IsBoolean = element.ValueKind is JsonValueKind.True or JsonValueKind.False;
        IsComplex = element.ValueKind is JsonValueKind.Array or JsonValueKind.Object;
    }

    /// <summary>字段名。</summary>
    public string Name { get; }
    /// <summary>字段类型名。</summary>
    public string TypeName { get; }
    /// <summary>受控长度的字段值预览。</summary>
    public string ValueText { get; }
    /// <summary>用于悬浮提示的受控长度字段值。</summary>
    public string FullValueText { get; }
    /// <summary>字段是否为字符串。</summary>
    public bool IsString { get; }
    /// <summary>字段是否为数字。</summary>
    public bool IsNumber { get; }
    /// <summary>字段是否为布尔值。</summary>
    public bool IsBoolean { get; }
    /// <summary>字段是否为数组或对象。</summary>
    public bool IsComplex { get; }

    /// <summary>把 JSON 值类型转换为页面使用的短类型名。</summary>
    /// <param name="element">字段 JSON 节点。</param>
    /// <returns>小写类型名。</returns>
    private static string ResolveTypeName(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => "string",
        JsonValueKind.Number => "number",
        JsonValueKind.True or JsonValueKind.False => "boolean",
        JsonValueKind.Array => "array",
        JsonValueKind.Object => "object",
        JsonValueKind.Null => "null",
        _ => "unknown"
    };

    /// <summary>生成既紧凑又保留语义的字段值文本。</summary>
    /// <param name="element">字段 JSON 节点。</param>
    /// <returns>字段值摘要。</returns>
    private static string ResolveValueText(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString() ?? string.Empty,
        JsonValueKind.Array => string.Format(WorkbenchI18nService.Instance.GetString(
            "String.TableKit.ItemsSuffixTemplate", "{0} 项"), element.GetArrayLength()),
        JsonValueKind.Object => string.Format(WorkbenchI18nService.Instance.GetString(
            "String.TableKit.FieldsSuffixTemplate", "{0} 字段"), CountObjectProperties(element)),
        JsonValueKind.Null => "null",
        _ => element.GetRawText()
    };

    /// <summary>统计 JSON 对象的直接属性数量，不为字段卡片复制完整嵌套 JSON。</summary>
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

    /// <summary>把字段文本限制在固定长度内，避免悬浮提示持有超大字符串。</summary>
    /// <param name="value">原始字段文本。</param>
    /// <param name="maxLength">允许保留的最大字符数。</param>
    /// <returns>原文本或追加省略号的受限文本。</returns>
    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength
            ? value
            : value[..maxLength] + "...";
    }
}
