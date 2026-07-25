using YokiFrame.Tooling.Application.Models.TableKit;

namespace YokiFrame.Workbench.Avalonia.ViewModels;

public sealed partial class TableKitPageViewModel
{
    /// <summary>保存 Runtime Settings 后执行正式 Luban 生成并写入项目代码和宿主程序集边界。</summary>
    private async Task GenerateAsync()
    {
        StatusText = "正在生成";
        StatusDetailText = "正在写入正式输出和生成契约。";
        IsConsoleExpanded = true;
        if (!TryPersistConfiguration()) return;
        AppendConsole("SUCCESS", "TableKit 配置已保存到当前项目。", false);
        AppendConsole("INFO", "开始生成配置表。", false);
        TableKitOperationResult result = await mService.GenerateAsync(CreateOptions());
        ApplyOperationResult(result, false);
    }
}
