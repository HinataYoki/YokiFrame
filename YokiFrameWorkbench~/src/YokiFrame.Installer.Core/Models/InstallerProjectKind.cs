namespace YokiFrame.Installer.Core.Models;

/// <summary>
/// 描述安装器识别出的目标项目类型。
/// </summary>
public enum InstallerProjectKind
{
    /// <summary>
    /// 未识别的项目。
    /// </summary>
    Unknown,

    /// <summary>
    /// Unity 项目。
    /// </summary>
    Unity,

    /// <summary>
    /// Godot 项目。
    /// </summary>
    Godot
}
