using System.Text.Json;
using YokiFrame.Tooling.Application.Models.TableKit;
using YokiFrame.Workbench.Avalonia.Services;

namespace YokiFrame.Workbench.Avalonia.ViewModels;

/// <summary>TableKit 预览表的页面投影。</summary>
public sealed class TableKitPreviewTableViewModel
{
    private const int MAX_RECORD_PREVIEW_COUNT = 200;
    private static readonly string[] sPreferredCollectionKeys = { "data", "items", "rows", "list" };

    /// <summary>从 Application 预览模型创建页面项。</summary>
    /// <param name="model">预览模型。</param>
    public TableKitPreviewTableViewModel(TableKitPreviewTable model)
    {
        Name = model.Name;
        PreviewJson = model.PreviewJson;
        (Records, IsRecordPreviewTruncated) = ParseRecords(model.PreviewJson);
        Count = IsRecordPreviewTruncated
            ? Math.Max(model.Count, Records.Count)
            : Records.Count > 0 ? Records.Count : model.Count;
    }

    /// <summary>表名。</summary>
    public string Name { get; }
    /// <summary>记录数。</summary>
    public int Count { get; }
    /// <summary>格式化 JSON。</summary>
    public string PreviewJson { get; }
    /// <summary>从表 JSON 投影出的可浏览记录。</summary>
    public IReadOnlyList<TableKitPreviewRecordViewModel> Records { get; }
    /// <summary>获取当前表是否因编辑器预览上限而只显示部分记录。</summary>
    public bool IsRecordPreviewTruncated { get; }
    /// <summary>获取用于记录列表页头的完整或受限预览摘要。</summary>
    public string RecordSummary => IsRecordPreviewTruncated
        ? string.Format(WorkbenchI18nService.Instance.GetString(
            "String.TableKit.RecordSummaryTruncatedTemplate", "显示 {0} / {1} 条"), Records.Count, Count)
        : string.Format(WorkbenchI18nService.Instance.GetString(
            "String.TableKit.RecordCountTemplate", "{0} 条记录"), Count);

    /// <summary>解析常见 Luban JSON 结构，至多物化固定数量的记录以限制编辑器开销。</summary>
    /// <param name="json">完整表 JSON。</param>
    /// <returns>可供第二级列表浏览的记录集合及是否发生截断。</returns>
    private static (IReadOnlyList<TableKitPreviewRecordViewModel> Records, bool IsTruncated) ParseRecords(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return (Array.Empty<TableKitPreviewRecordViewModel>(), false);
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            List<TableKitPreviewRecordViewModel> records = new();
            bool isTruncated = CollectRecords(document.RootElement, records);
            return (records, isTruncated);
        }
        catch (JsonException)
        {
            return (Array.Empty<TableKitPreviewRecordViewModel>(), false);
        }
    }

    /// <summary>按数组、常见集合属性或对象本身投影记录，并在达到上限时停止枚举。</summary>
    /// <param name="root">表 JSON 根节点。</param>
    /// <param name="records">接收页面记录的缓冲区。</param>
    /// <returns>记录数超过预览上限时返回 true。</returns>
    private static bool CollectRecords(JsonElement root, List<TableKitPreviewRecordViewModel> records)
    {
        if (root.ValueKind == JsonValueKind.Array)
        {
            return AddArrayRecords(root, records);
        }

        if (root.ValueKind != JsonValueKind.Object)
        {
            records.Add(new TableKitPreviewRecordViewModel(root, 0));
            return false;
        }

        JsonElement collection = FindRecordCollection(root);
        if (collection.ValueKind == JsonValueKind.Array) return AddArrayRecords(collection, records);
        records.Add(new TableKitPreviewRecordViewModel(root, 0));
        return false;
    }

    /// <summary>把 JSON 数组前固定数量的元素转换为页面记录，避免大表一次性创建大量视图模型。</summary>
    /// <param name="array">包含配置记录的 JSON 数组。</param>
    /// <param name="records">接收页面记录的缓冲区。</param>
    /// <returns>数组仍有未投影元素时返回 true。</returns>
    private static bool AddArrayRecords(JsonElement array, List<TableKitPreviewRecordViewModel> records)
    {
        foreach (JsonElement element in array.EnumerateArray())
        {
            if (records.Count >= MAX_RECORD_PREVIEW_COUNT) return true;
            records.Add(new TableKitPreviewRecordViewModel(element, records.Count));
        }

        return false;
    }

    /// <summary>优先选择 Luban 常见集合字段，否则采用对象中的首个数组字段。</summary>
    /// <param name="root">对象类型的表 JSON 根节点。</param>
    /// <returns>记录数组；未找到时返回未定义节点。</returns>
    private static JsonElement FindRecordCollection(JsonElement root)
    {
        foreach (string key in sPreferredCollectionKeys)
        {
            if (root.TryGetProperty(key, out JsonElement value) && value.ValueKind == JsonValueKind.Array) return value;
        }

        foreach (JsonProperty property in root.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.Array) return property.Value;
        }

        return default;
    }
}
