namespace YokiFrame.Installer.Core.Services;

/// <summary>
/// 标识 Godot 整体安装中可验证和故障注入的目录与项目文件提交边界。
/// </summary>
internal enum GodotInstallCheckpoint
{
    /// <summary>
    /// 完整 add-on staging 已通过投影、哈希和 owner manifest 校验。
    /// </summary>
    AddonStagingVerified,

    /// <summary>
    /// 已有 `addons/yokiframe` 已移动到同项目卷内备份目录。
    /// </summary>
    ExistingAddonBackedUp,

    /// <summary>
    /// 完整 staging add-on 已移动为正式 `addons/yokiframe` 目录。
    /// </summary>
    AddonCommitted,

    /// <summary>
    /// 唯一顶层 Godot C# 项目已提交 YokiFrame owner group。
    /// </summary>
    ProjectFileCommitted,

    /// <summary>
    /// project.godot 已提交 YokiFrame 插件、autoload 和 owner section。
    /// </summary>
    ProjectSettingsCommitted
}
