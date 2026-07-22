namespace YokiFrame.Tooling.Application.Installer;

/// <summary>
/// 描述 Installer 互斥的安装来源与目标组合。
/// </summary>
public enum InstallerInstallMode
{
    /// <summary>
    /// 从本地 YokiFrame 包安装到 Unity embedded package。
    /// </summary>
    UnityLocal,

    /// <summary>
    /// 通过 Git URL 配置 Unity Package Manager。
    /// </summary>
    UnityGit,

    /// <summary>
    /// 从本地 YokiFrame 包投影到 Godot .NET 项目。
    /// </summary>
    GodotLocal
}
