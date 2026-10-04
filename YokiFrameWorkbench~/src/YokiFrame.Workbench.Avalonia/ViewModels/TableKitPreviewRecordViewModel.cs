using YokiFrame.Tooling.Application.Models.TableKit;
using YokiFrame.Workbench.Avalonia.Services;

namespace YokiFrame.Workbench.Avalonia.ViewModels;

/// <summary>TableKit 预览中的单条配置记录。</summary>
public sealed class TableKitPreviewRecordViewModel
{
    /// <summary>从 Application 投影创建记录页面项。</summary>
    /// <param name="record">已完成 JSON 解析的记录。</param>
    public TableKitPreviewRecordViewModel(TableKitPreviewRecord record)
    {
        Index = record.Index;
        Kind = record.Kind;
        Fields = record.Fields.Select(static field => new TableKitPreviewFieldViewModel(field)).ToArray();
        Title = record.Title;
        FieldCountText = string.Format(WorkbenchI18nService.Instance.GetString(
            "String.TableKit.FieldsSuffixTemplate", "{0} 字段"), Fields.Count);
        PreviewJson = record.PreviewJson;
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
}
