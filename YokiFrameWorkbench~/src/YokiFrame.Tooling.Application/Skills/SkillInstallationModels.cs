namespace YokiFrame.Tooling.Application.Skills;

/// <summary>
/// 描述一个随 YokiFrame 包提供的 AI Skill。
/// </summary>
public sealed class SkillPackageInfo
{
    /// <summary>
    /// 创建包内 Skill 只读模型。
    /// </summary>
    /// <param name="name">Skill 目录名。</param>
    /// <param name="packaged">是否已找到 SKILL.md。</param>
    /// <param name="sourcePath">包内 Skill 源目录。</param>
    public SkillPackageInfo(string name, bool packaged, string sourcePath)
    {
        Name = name;
        Packaged = packaged;
        SourcePath = sourcePath;
    }

    /// <summary>
    /// 获取 Skill 目录名。
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 获取 Skill 是否已随包提供。
    /// </summary>
    public bool Packaged { get; }

    /// <summary>
    /// 获取包内 Skill 源目录。
    /// </summary>
    public string SourcePath { get; }
}

/// <summary>
/// 描述一个 AI 助手 Skill 安装目标的当前状态。
/// </summary>
public sealed class SkillInstallationTarget
{
    /// <summary>
    /// 创建 Skill 安装目标只读模型。
    /// </summary>
    /// <param name="id">目标标识。</param>
    /// <param name="label">目标显示名。</param>
    /// <param name="relativePath">相对项目根的安装目录。</param>
    /// <param name="supportsCustomPath">是否支持自定义路径。</param>
    /// <param name="installedSkills">当前已安装 Skill 名称。</param>
    public SkillInstallationTarget(
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
        InstalledSkills = installedSkills.ToArray();
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
    /// 获取当前已安装 Skill 名称快照。
    /// </summary>
    public IReadOnlyList<string> InstalledSkills { get; }
}

/// <summary>
/// 描述当前项目内包源和全部 AI 目标的 Skill 安装状态。
/// </summary>
public sealed class SkillInstallationStatus
{
    /// <summary>
    /// 创建 Skill 安装状态只读模型。
    /// </summary>
    /// <param name="projectRoot">项目根。</param>
    /// <param name="sourceRoot">包内 Skill 源根。</param>
    /// <param name="skills">包内 Skill 列表。</param>
    /// <param name="targets">安装目标列表。</param>
    public SkillInstallationStatus(
        string projectRoot,
        string sourceRoot,
        IReadOnlyList<SkillPackageInfo> skills,
        IReadOnlyList<SkillInstallationTarget> targets)
    {
        ProjectRoot = projectRoot;
        SourceRoot = sourceRoot;
        Skills = skills.ToArray();
        Targets = targets.ToArray();
    }

    /// <summary>
    /// 获取项目根。
    /// </summary>
    public string ProjectRoot { get; }

    /// <summary>
    /// 获取包内 Skill 源根。
    /// </summary>
    public string SourceRoot { get; }

    /// <summary>
    /// 获取包内 Skill 列表快照。
    /// </summary>
    public IReadOnlyList<SkillPackageInfo> Skills { get; }

    /// <summary>
    /// 获取安装目标列表快照。
    /// </summary>
    public IReadOnlyList<SkillInstallationTarget> Targets { get; }
}

/// <summary>
/// 描述一次 Skill 安装或卸载后的统一结果。
/// </summary>
public sealed class SkillOperationResult
{
    /// <summary>
    /// 创建 Skill 操作结果。
    /// </summary>
    /// <param name="success">操作是否成功。</param>
    /// <param name="installed">操作后是否已安装。</param>
    /// <param name="skillName">Skill 名称。</param>
    /// <param name="targetId">目标标识。</param>
    /// <param name="targetPath">最终目标目录。</param>
    /// <param name="log">可显示日志。</param>
    public SkillOperationResult(
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
    /// 获取操作后是否已安装。
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
