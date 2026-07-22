namespace YokiFrame.Installer.Core.Models;

/// <summary>
/// 描述当前项目内 YokiFrame Skill 源和各 AI 目标安装状态。
/// </summary>
public sealed class SkillInstallStatus
{
    /// <summary>
    /// 创建 Skill 安装状态。
    /// </summary>
    /// <param name="projectRoot">项目根目录。</param>
    /// <param name="sourceRoot">包内 Skill 源目录。</param>
    /// <param name="skills">包内 Skill 列表。</param>
    /// <param name="targets">安装目标列表。</param>
    public SkillInstallStatus(
        string projectRoot,
        string sourceRoot,
        IReadOnlyList<SkillInstallInfo> skills,
        IReadOnlyList<SkillInstallTargetStatus> targets)
    {
        ProjectRoot = projectRoot;
        SourceRoot = sourceRoot;
        Skills = skills;
        Targets = targets;
    }

    /// <summary>
    /// 获取项目根目录。
    /// </summary>
    public string ProjectRoot { get; }

    /// <summary>
    /// 获取包内 Skill 源目录。
    /// </summary>
    public string SourceRoot { get; }

    /// <summary>
    /// 获取包内 Skill 列表。
    /// </summary>
    public IReadOnlyList<SkillInstallInfo> Skills { get; }

    /// <summary>
    /// 获取安装目标列表。
    /// </summary>
    public IReadOnlyList<SkillInstallTargetStatus> Targets { get; }
}
