using YokiFrame.Installer.Core.Services;

namespace YokiFrame.Installer.Core.Tests;

/// <summary>
/// 覆盖 YokiFrame 包内 Skill 安装服务，确保 Workbench 面板不是静态占位。
/// </summary>
public sealed class SkillInstallServiceTests
{
    /// <summary>
    /// 验证服务能把包内 Skill 安装到 Codex 目标目录。
    /// </summary>
    [Fact]
    public void InstallCopiesPackagedSkillToCodexTarget()
    {
        var projectRoot = CreateProjectWithPackagedSkill("yokiframe");
        var result = new SkillInstallService().Install(projectRoot, "codex", "yokiframe");

        Assert.True(result.Success);
        Assert.True(result.Installed);
        Assert.Equal("codex", result.TargetId);
        Assert.True(File.Exists(Path.Combine(projectRoot, ".codex", "skills", "yokiframe", "SKILL.md")));
    }

    /// <summary>
    /// 验证安装到 AI 目录时不会把 Unity 导入用的 meta 文件复制过去。
    /// </summary>
    [Fact]
    public void InstallSkipsUnityMetaFilesForAiTargets()
    {
        var projectRoot = CreateProjectWithPackagedSkill("yokiframe");

        _ = new SkillInstallService().Install(projectRoot, "agents", "yokiframe");

        Assert.False(File.Exists(Path.Combine(projectRoot, ".agents", "skills", "yokiframe", "SKILL.md.meta")));
    }

    /// <summary>
    /// 验证状态扫描会列出包内 Skill 和已安装目标。
    /// </summary>
    [Fact]
    public void StatusListsPackagedSkillsAndInstalledTargets()
    {
        var projectRoot = CreateProjectWithPackagedSkill("yokiframe");
        Directory.CreateDirectory(Path.Combine(projectRoot, ".agents", "skills", "yokiframe"));
        File.WriteAllText(Path.Combine(projectRoot, ".agents", "skills", "yokiframe", "SKILL.md"), "installed");

        var status = new SkillInstallService().GetStatus(projectRoot);

        Assert.Contains(status.Skills, skill => skill.Name == "yokiframe" && skill.Packaged);
        Assert.Contains(status.Targets, target => target.Id == "codex");
        var agents = Assert.Single(status.Targets, target => target.Id == "agents");
        Assert.Contains("yokiframe", agents.InstalledSkills);
    }

    /// <summary>
    /// 验证自定义安装路径不能使用相对逃逸路径写出项目根目录。
    /// </summary>
    [Fact]
    public void CustomPathOutsideProjectRootIsRejected()
    {
        var projectRoot = CreateProjectWithPackagedSkill("yokiframe");

        var error = Assert.Throws<ArgumentException>(() =>
            new SkillInstallService().Install(projectRoot, "custom", "yokiframe", "../outside/skills"));

        Assert.Contains("项目根目录", error.Message);
        Assert.False(Directory.Exists(Path.Combine(Directory.GetParent(projectRoot)!.FullName, "outside")));
    }

    /// <summary>
    /// 创建包含一个包内 Skill 的最小 Unity 项目。
    /// </summary>
    /// <param name="skillName">Skill 名称。</param>
    /// <returns>测试项目根目录。</returns>
    private static string CreateProjectWithPackagedSkill(string skillName)
    {
        var root = Path.Combine(Path.GetTempPath(), "yokiframe-skill-tests", Guid.NewGuid().ToString("N"));
        var skillRoot = Path.Combine(root, "Assets", "YokiFrame", "Core", "Editor", "Skills", skillName);
        Directory.CreateDirectory(skillRoot);
        File.WriteAllText(Path.Combine(skillRoot, "SKILL.md"), "---\nname: " + skillName + "\ndescription: test\n---\n");
        File.WriteAllText(Path.Combine(skillRoot, "SKILL.md.meta"), "fileFormatVersion: 2\n");
        return root;
    }
}
