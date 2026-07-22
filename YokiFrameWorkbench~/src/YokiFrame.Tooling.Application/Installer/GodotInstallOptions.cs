namespace YokiFrame.Tooling.Application.Installer;

/// <summary>
/// 描述 Godot 本地安装时由 Installer 独占执行的项目配置选项。
/// </summary>
public sealed class GodotInstallOptions
{
    /// <summary>
    /// 创建 Godot 项目配置选项。
    /// </summary>
    /// <param name="repairProjectSettings">是否修复 YokiFrame 管理的 project.godot 配置。</param>
    /// <param name="enablePlugin">是否自动登记并启用 YokiFrame 插件。</param>
    public GodotInstallOptions(bool repairProjectSettings, bool enablePlugin)
    {
        RepairProjectSettings = repairProjectSettings;
        EnablePlugin = enablePlugin;
    }

    /// <summary>
    /// 获取是否修复 YokiFrame 管理的 project.godot 配置。
    /// </summary>
    public bool RepairProjectSettings { get; }

    /// <summary>
    /// 获取是否自动登记并启用 YokiFrame 插件。
    /// </summary>
    public bool EnablePlugin { get; }
}
