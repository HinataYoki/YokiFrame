namespace YokiFrame.Installer.Core.Models;

/// <summary>
/// 描述一次 Skill 安装或卸载操作的结果。
/// </summary>
public sealed class SkillInstallResult
{
    /// <summary>
    /// 创建 Skill 操作结果。
    /// </summary>
    /// <param name="success">操作是否成功。</param>
    /// <param name="installed">操作后是否处于已安装状态。</param>
    /// <param name="skillName">Skill 名称。</param>
    /// <param name="targetId">目标标识。</param>
    /// <param name="targetPath">最终目标目录。</param>
    /// <param name="log">可显示日志。</param>
    public SkillInstallResult(
        bool success,
        bool installed,
        string skillName,
        string targetId,
        string targetPath,
        string log)
    {
        Success = success;
        Installed = installed;
        SkillName = skillName;
        TargetId = targetId;
        TargetPath = targetPath;
        Log = log;
    }

    /// <summary>
    /// 获取操作是否成功。
    /// </summary>
    public bool Success { get; }

    /// <summary>
    /// 获取操作后是否处于已安装状态。
    /// </summary>
    public bool Installed { get; }

    /// <summary>
    /// 获取 Skill 名称。
    /// </summary>
    public string SkillName { get; }

    /// <summary>
    /// 获取目标标识。
    /// </summary>
    public string TargetId { get; }

    /// <summary>
    /// 获取最终目标目录。
    /// </summary>
    public string TargetPath { get; }

    /// <summary>
    /// 获取可显示日志。
    /// </summary>
    public string Log { get; }
}
