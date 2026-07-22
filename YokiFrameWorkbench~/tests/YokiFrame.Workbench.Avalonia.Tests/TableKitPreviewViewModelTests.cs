using YokiFrame.Tooling.Application.Models.TableKit;
using YokiFrame.Tooling.Application.Services.TableKit;
using YokiFrame.Workbench.Avalonia.ViewModels;

namespace YokiFrame.Workbench.Avalonia.Tests;

/// <summary>验证 TableKit 大表预览的有界投影和验证结果替换行为。</summary>
public sealed class TableKitPreviewViewModelTests
{
    /// <summary>成功验证未产出预览表时清除旧选择，避免页面继续展示过期数据。</summary>
    [Fact]
    public void SuccessfulValidationWithoutTablesClearsPreviousPreview()
    {
        TableKitPageViewModel viewModel = new();
        viewModel.ApplyOperationResult(CreateSuccessfulResult("[{\"id\":1}]"), true);

        viewModel.ApplyOperationResult(
            new TableKitOperationResult
            {
                Succeeded = true,
                Log = "验证完成。",
                PreviewTables = Array.Empty<TableKitPreviewTable>()
            },
            true);

        Assert.Empty(viewModel.PreviewTables);
        Assert.Null(viewModel.SelectedPreviewTable);
        Assert.Null(viewModel.SelectedPreviewRecord);
    }

    /// <summary>大表只物化固定数量的记录，仍保留总数和截断状态供页面说明。</summary>
    [Fact]
    public void PreviewMaterializesBoundedRecordCount()
    {
        string records = string.Join(
            ",",
            Enumerable.Range(1, 201).Select(static index => "{\"id\":" + index + "}"));
        TableKitPreviewTableViewModel preview = new(
            new TableKitPreviewTable
            {
                Name = "items",
                Count = 201,
                PreviewJson = "[" + records + "]"
            });

        Assert.Equal(200, preview.Records.Count);
        Assert.True(preview.IsRecordPreviewTruncated);
        Assert.Equal("显示 200 / 201 条", preview.RecordSummary);
    }

    /// <summary>创建页面状态替换测试使用的成功验证结果。</summary>
    /// <param name="previewJson">单表 JSON 预览文本。</param>
    /// <returns>包含一张预览表的成功结果。</returns>
    private static TableKitOperationResult CreateSuccessfulResult(string previewJson)
    {
        return new TableKitOperationResult
        {
            Succeeded = true,
            Log = "验证完成。",
            PreviewTables = new[]
            {
                new TableKitPreviewTable
                {
                    Name = "items",
                    Count = 1,
                    PreviewJson = previewJson
                }
            }
        };
    }
}
