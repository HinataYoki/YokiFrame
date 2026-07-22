using YokiFrame.Installer.Core.Models;
using YokiFrame.Installer.Core.Services;

namespace YokiFrame.Tooling.Application.Skills;

/// <summary>
/// 提供 Workbench 和 CLI 共用的 Skill 状态、安装与卸载用例。
/// </summary>
public sealed class SkillInstallationService
{
    private readonly SkillInstallService mCoreService = new();

    /// <summary>
    /// 读取项目内包源和全部目标的 Skill 安装状态。
    /// </summary>
    /// <param name="projectRoot">项目根。</param>
    /// <returns>Application 自有状态模型。</returns>
    public SkillInstallationStatus GetStatus(string projectRoot)
    {
        return MapStatus(mCoreService.GetStatus(projectRoot));
    }

    /// <summary>
    /// 把指定 Skill 安装到预设或自定义目标。
    /// </summary>
    /// <param name="projectRoot">项目根。</param>
    /// <param name="targetId">目标标识。</param>
    /// <param name="skillName">Skill 名称。</param>
    /// <param name="customPath">custom 目标使用的项目内相对目录。</param>
    /// <returns>Application 自有操作结果。</returns>
    public SkillOperationResult Install(
        string projectRoot,
        string targetId,
        string skillName,
        string? customPath = null)
    {
        return MapResult(mCoreService.Install(projectRoot, targetId, skillName, customPath));
    }

    /// <summary>
    /// 从预设或自定义目标卸载指定 Skill。
    /// </summary>
    /// <param name="projectRoot">项目根。</param>
    /// <param name="targetId">目标标识。</param>
    /// <param name="skillName">Skill 名称。</param>
    /// <param name="customPath">custom 目标使用的项目内相对目录。</param>
    /// <returns>Application 自有操作结果。</returns>
    public SkillOperationResult Uninstall(
        string projectRoot,
        string targetId,
        string skillName,
        string? customPath = null)
    {
        return MapResult(mCoreService.Uninstall(projectRoot, targetId, skillName, customPath));
    }

    /// <summary>
    /// 把 Core 状态树完整复制为 Application DTO。
    /// </summary>
    /// <param name="status">Core Skill 状态。</param>
    /// <returns>Application Skill 状态。</returns>
    private static SkillInstallationStatus MapStatus(SkillInstallStatus status)
    {
        var skills = status.Skills.Select(static skill => new SkillPackageInfo(
            skill.Name,
            skill.Packaged,
            skill.SourcePath)).ToArray();
        var targets = status.Targets.Select(static target => new SkillInstallationTarget(
            target.Id,
            target.Label,
            target.RelativePath,
            target.SupportsCustomPath,
            target.InstalledSkills)).ToArray();
        return new SkillInstallationStatus(status.ProjectRoot, status.SourceRoot, skills, targets);
    }

    /// <summary>
    /// 把 Core 文件操作结果复制为 Application DTO。
    /// </summary>
    /// <param name="result">Core Skill 操作结果。</param>
    /// <returns>Application Skill 操作结果。</returns>
    private static SkillOperationResult MapResult(SkillInstallResult result)
    {
        return new SkillOperationResult(
            result.Success,
            result.Installed,
            result.SkillName,
            result.TargetId,
            result.TargetPath,
            result.Log);
    }
}
