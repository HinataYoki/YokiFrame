using System.Collections.Specialized;
using YokiFrame.Tooling.Application.Models.TableKit;

namespace YokiFrame.Workbench.Avalonia.ViewModels;

/// <summary>承载 TableKit 验证预览和操作结果投影。</summary>
public sealed partial class TableKitPageViewModel
{
    /// <summary>执行配置验证和预览读取；文件检查与 JSON 投影都在返回 UI 前完成。</summary>
    private async Task ValidateAsync()
    {
        BeginOperation(GetString(ValidatingKey, "正在验证"));
        StatusDetailText = GetString(ReadingTempOutputKey, "正在读取 Luban 临时输出。");
        IsConsoleExpanded = true;
        try
        {
            await RefreshConfigurationAsync();
            AppendConsole("INFO", GetString(StartValidateKey, "开始验证配置并生成临时 JSON 预览。"), false);
            TableKitOptions options = CreateOptions();
            (TableKitOperationResult Result, IReadOnlyList<TableKitPreviewTableViewModel> PreviewTables) operation =
                await Task.Run(() => CreateValidationProjection(options));
            await ApplyOperationResultAsync(operation.Result, false);
            if (operation.Result.Succeeded)
            {
                ApplyPreviewViewModels(operation.PreviewTables);
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>在后台执行 Luban 验证，并把 JSON 预览预先投影成页面模型。</summary>
    /// <param name="options">当前页面配置快照。</param>
    /// <returns>原始操作结果和可直接绑定的预览模型。</returns>
    private (TableKitOperationResult Result, IReadOnlyList<TableKitPreviewTableViewModel> PreviewTables) CreateValidationProjection(
        TableKitOptions options)
    {
        TableKitOperationResult result = mService.ValidateAsync(options).GetAwaiter().GetResult();
        IReadOnlyList<TableKitPreviewTableViewModel> previewTables = result.Succeeded
            ? result.PreviewTables.Select(static table => new TableKitPreviewTableViewModel(table)).ToArray()
            : Array.Empty<TableKitPreviewTableViewModel>();
        return (result, previewTables);
    }

    /// <summary>响应预览表集合变化，刷新任务页可用性和状态摘要。</summary>
    /// <param name="sender">预览表集合。</param>
    /// <param name="args">集合变化参数。</param>
    private void OnPreviewTablesChanged(object? sender, NotifyCollectionChangedEventArgs args)
    {
        OnPropertyChanged(nameof(HasPreviewTables));
        OnPropertyChanged(nameof(PreviewCountText));
        OnPropertyChanged(nameof(PreviewStatusText));
        RebuildFilteredPreviewTables();
        OnPropertyChanged(nameof(ConsoleSummaryText));
    }

    /// <summary>重建过滤后的预览表缓存并通知绑定系统。</summary>
    private void RebuildFilteredPreviewTables()
    {
        mFilteredPreviewTables = string.IsNullOrWhiteSpace(PreviewSearch)
            ? PreviewTables
            : PreviewTables.Where(table => table.Name.Contains(PreviewSearch, StringComparison.OrdinalIgnoreCase)).ToList();
        OnPropertyChanged(nameof(FilteredPreviewTables));
    }

    /// <summary>将 Application 结果投影到状态、日志和任务工作区。</summary>
    /// <param name="result">TableKit 操作结果。</param>
    /// <param name="showDataOnSuccess">成功后是否进入数据浏览任务。</param>
    internal async Task ApplyOperationResultAsync(TableKitOperationResult result, bool showDataOnSuccess)
    {
        if (!string.IsNullOrWhiteSpace(result.Log))
        {
            AppendConsoleLines(result.Succeeded ? "INFO" : "ERROR", result.Log);
        }

        TablesType = result.Contract?.TablesType ?? GetString(UnresolvedKey, "未解析");
        DataExtension = result.Contract?.DataExtension ?? GetString(UnresolvedKey, "未解析");
        PreviewDirectory = result.PreviewDirectory;
        if (showDataOnSuccess)
        {
            IReadOnlyList<TableKitPreviewTableViewModel> previewTables = await Task.Run(
                () => (IReadOnlyList<TableKitPreviewTableViewModel>)result.PreviewTables
                    .Select(static table => new TableKitPreviewTableViewModel(table))
                    .ToArray());
            ApplyPreviewViewModels(previewTables);
        }

        StatusText = result.Succeeded ? GetString(SuccessKey, "成功") : GetString(FailedShortKey, "失败");
        StatusDetailText = result.Succeeded
            ? (result.Contract == null ? GetString(OperationDoneKey, "操作完成。") : result.Contract.TablesType + " · " + result.Contract.DataTarget)
            : string.Join("; ", result.Diagnostics);
        CommandPreviewText = CreateCommandPreview();
        await RefreshEnvironmentAsync();
        if (result.Succeeded)
        {
            if (showDataOnSuccess && HasPreviewTables)
            {
                SelectedWorkspaceIndex = 1;
            }

            IsConsoleExpanded = false;
        }
        else
        {
            IsConsoleExpanded = true;
        }
    }

    /// <summary>替换预览模型并选中第一张表；JSON 解析已在后台完成。</summary>
    /// <param name="tables">已完成记录投影的预览表。</param>
    private void ApplyPreviewViewModels(IReadOnlyList<TableKitPreviewTableViewModel> tables)
    {
        PreviewTables.Clear();
        foreach (TableKitPreviewTableViewModel table in tables)
        {
            PreviewTables.Add(table);
        }

        SelectedPreviewTable = PreviewTables.FirstOrDefault();
    }

    /// <summary>清空预览及其三级选择状态。</summary>
    private void ClearPreview()
    {
        SelectedPreviewRecord = null;
        SelectedPreviewTable = null;
        PreviewTables.Clear();
        PreviewSearch = string.Empty;
    }
}
