namespace YokiFrame.Tooling.Application.Models.SaveKit;

/// <summary>SaveKit Workbench 用于显示设置写入结果的轻量结果模型。</summary>
public sealed record SaveKitSettingsSaveResult(
    bool Saved,
    bool Conflict,
    WorkbenchSaveKitProjectSettings Settings,
    string ErrorMessage);
