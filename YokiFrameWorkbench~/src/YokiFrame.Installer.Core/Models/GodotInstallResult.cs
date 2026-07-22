namespace YokiFrame.Installer.Core.Models;

/// <summary>
/// 描述 Godot 包和全部 Installer owner 文件成功提交后的稳定结果。
/// </summary>
public sealed class GodotInstallResult
{
    /// <summary>
    /// 创建 Godot 安装结果。
    /// </summary>
    /// <param name="packageResult">受控包投影事务结果。</param>
    /// <param name="projectFilePath">已 patch 的唯一顶层 Godot C# 项目文件。</param>
    /// <param name="projectSettingsPath">已 patch 的 project.godot 路径。</param>
    /// <param name="pluginConfigPath">已提交的 plugin.cfg 路径。</param>
    /// <param name="pluginScriptPath">已提交的薄 C# EditorPlugin bootstrap 路径。</param>
    /// <param name="pluginScriptUidPath">已提交的 EditorPlugin bootstrap UID 路径。</param>
    /// <param name="runtimeBootstrapPath">已提交的宿主项目薄 C# bootstrap 路径。</param>
    /// <param name="runtimeBootstrapUidPath">已提交的 bootstrap UID sidecar 路径。</param>
    public GodotInstallResult(
        PackageInstallTransactionResult packageResult,
        string projectFilePath,
        string projectSettingsPath,
        string pluginConfigPath,
        string pluginScriptPath,
        string pluginScriptUidPath,
        string runtimeBootstrapPath,
        string runtimeBootstrapUidPath)
    {
        PackageResult = packageResult;
        ProjectFilePath = projectFilePath;
        ProjectSettingsPath = projectSettingsPath;
        PluginConfigPath = pluginConfigPath;
        PluginScriptPath = pluginScriptPath;
        PluginScriptUidPath = pluginScriptUidPath;
        RuntimeBootstrapPath = runtimeBootstrapPath;
        RuntimeBootstrapUidPath = runtimeBootstrapUidPath;
    }

    /// <summary>
    /// 获取受控包投影事务结果。
    /// </summary>
    public PackageInstallTransactionResult PackageResult { get; }

    /// <summary>
    /// 获取已 patch 的唯一顶层 Godot C# 项目文件。
    /// </summary>
    public string ProjectFilePath { get; }

    /// <summary>
    /// 获取已 patch 的 project.godot 路径。
    /// </summary>
    public string ProjectSettingsPath { get; }

    /// <summary>
    /// 获取已提交的 plugin.cfg 路径。
    /// </summary>
    public string PluginConfigPath { get; }

    /// <summary>
    /// 获取已提交的薄 C# EditorPlugin bootstrap 路径。
    /// </summary>
    public string PluginScriptPath { get; }

    /// <summary>
    /// 获取已提交的 EditorPlugin bootstrap UID 路径。
    /// </summary>
    public string PluginScriptUidPath { get; }

    /// <summary>
    /// 获取已提交的宿主项目薄 C# bootstrap 路径。
    /// </summary>
    public string RuntimeBootstrapPath { get; }

    /// <summary>
    /// 获取已提交的 bootstrap UID sidecar 路径。
    /// </summary>
    public string RuntimeBootstrapUidPath { get; }
}
