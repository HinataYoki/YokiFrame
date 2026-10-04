using YokiFrame.Tooling.Application.Models.TableKit;
using YokiFrame.Workbench.Avalonia.Services;

namespace YokiFrame.Workbench.Avalonia.ViewModels;

/// <summary>TableKit 预览表的页面投影。</summary>
public sealed class TableKitPreviewTableViewModel
{
    /// <summary>从 Application 已完成的 JSON 投影创建页面项。</summary>
    /// <param name="model">原始预览模型。</param>
    public TableKitPreviewTableViewModel(TableKitPreviewTable model)
    {
        Name = model.Name;
        PreviewJson = model.PreviewJson;
        (IReadOnlyList<TableKitPreviewRecord> records, bool isTruncated) = TableKitPreviewProjection.CreateRecords(model);
        Records = records.Select(static record => new TableKitPreviewRecordViewModel(record)).ToArray();
        IsRecordPreviewTruncated = isTruncated;
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
}
