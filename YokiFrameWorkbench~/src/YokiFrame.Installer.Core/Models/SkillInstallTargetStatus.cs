namespace YokiFrame.Installer.Core.Models;

/// <summary>
/// 描述一个 AI 助手 Skill 安装目标的当前状态。
/// </summary>
public sealed class SkillInstallTargetStatus
{
    /// <summary>
    /// 创建目标状态。
    /// </summary>
    /// <param name="id">目标标识。</param>
    /// <param name="label">目标显示名。</param>
    /// <param name="relativePath">相对项目根的安装目录。</param>
    /// <param name="supportsCustomPath">是否支持自定义路径。</param>
    /// <param name="installedSkills">当前已安装的 Skill 名称。</param>
    public SkillInstallTargetStatus(
        string id,
        string label,
        string relativePath,
        bool supportsCustomPath,
        IReadOnlyList<string> installedSkills)
    {
        Id = id;
        Label = label;
        RelativePath = relativePath;
        SupportsCustomPath = supportsCustomPath;
        InstalledSkills = installedSkills;
    }

    /// <summary>
    /// 获取目标标识。
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// 获取目标显示名。
    /// </summary>
    public string Label { get; }

    /// <summary>
    /// 获取相对项目根的安装目录。
    /// </summary>
    public string RelativePath { get; }

    /// <summary>
    /// 获取是否支持自定义路径。
    /// </summary>
    public bool SupportsCustomPath { get; }

    /// <summary>
    /// 获取当前已安装的 Skill 名称。
    /// </summary>
    public IReadOnlyList<string> InstalledSkills { get; }
}
