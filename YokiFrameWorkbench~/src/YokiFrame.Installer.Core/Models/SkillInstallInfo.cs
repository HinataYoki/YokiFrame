namespace YokiFrame.Installer.Core.Models;

/// <summary>
/// 描述一个随 YokiFrame 包提供的 AI Skill。
/// </summary>
public sealed class SkillInstallInfo
{
    /// <summary>
    /// 创建 Skill 描述。
    /// </summary>
    /// <param name="name">Skill 目录名。</param>
    /// <param name="packaged">是否已在包内找到 Skill 文件。</param>
    /// <param name="sourcePath">包内 Skill 源目录。</param>
    public SkillInstallInfo(string name, bool packaged, string sourcePath)
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
