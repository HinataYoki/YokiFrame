using YokiFrame.Tooling.Application.Models.TableKit;

namespace YokiFrame.Workbench.Avalonia.ViewModels;

public sealed partial class TableKitPageViewModel
{
    /// <summary>保存 Runtime Settings 后执行正式 Luban 生成并写入项目代码和宿主程序集边界。</summary>
    private async Task GenerateAsync()
    {
        StatusText = GetString(GeneratingKey, "正在生成");
        StatusDetailText = GetString(WritingOutputsKey, "正在写入正式输出和生成契约。");
        IsConsoleExpanded = true;
        if (!TryPersistConfiguration()) return;
        AppendConsole("SUCCESS", GetString(ConfigSavedKey, "TableKit 配置已保存到当前项目。"), false);
        AppendConsole("INFO", GetString(StartGenerateKey, "开始生成配置表。"), false);
        TableKitOperationResult result = await mService.GenerateAsync(CreateOptions());
        ApplyOperationResult(result, false);
    }

    /// <summary>正在生成状态资源 key。</summary>
    private const string GeneratingKey = "String.TableKit.Generating";

    /// <summary>正在写入输出提示资源 key。</summary>
    private const string WritingOutputsKey = "String.TableKit.WritingOutputs";

    /// <summary>开始生成提示资源 key。</summary>
    private const string StartGenerateKey = "String.TableKit.StartGenerate";
}
