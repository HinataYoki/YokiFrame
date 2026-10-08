using YokiFrame.Installer.Core.Services;

namespace YokiFrame.Installer.Core.Tests;

/// <summary>
/// 覆盖 YokiFrame 包内 Skill 安装服务，确保 Workbench 面板不是静态占位。
/// </summary>
public sealed class SkillInstallServiceTests : IDisposable
{
    private readonly List<string> mRoots = new();

    /// <summary>
    /// 删除本用例创建的临时项目，避免 Skill 安装夹具残留。
    /// </summary>
    public void Dispose()
    {
        foreach (var root in mRoots)
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

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
        InstallerFileAssert.EqualFile(
            Path.Combine(projectRoot, "Assets", "YokiFrame", "Core", "Editor", "Skills", "yokiframe", "SKILL.md"),
            Path.Combine(projectRoot, ".codex", "skills", "yokiframe", "SKILL.md"));
    }

    /// <summary>
    /// 验证对已安装 Skill 再次安装会删除旧目录并替换为包内新文档。
    /// </summary>
    [Fact]
    public void InstallReplacesExistingSkillDirectory()
    {
        var projectRoot = CreateProjectWithPackagedSkill("yokiframe");
        var targetRoot = Path.Combine(projectRoot, ".codex", "skills", "yokiframe");
        Directory.CreateDirectory(targetRoot);
        File.WriteAllText(Path.Combine(targetRoot, "SKILL.md"), "old");
        File.WriteAllText(Path.Combine(targetRoot, "stale.md"), "stale");
        File.WriteAllText(
            Path.Combine(projectRoot, "Assets", "YokiFrame", "Core", "Editor", "Skills", "yokiframe", "SKILL.md"),
            "new-content");

        var result = new SkillInstallService().Install(projectRoot, "codex", "yokiframe");

        Assert.True(result.Success);
        Assert.Contains("已更新", result.Log, StringComparison.Ordinal);
        InstallerFileAssert.EqualUtf8(Path.Combine(targetRoot, "SKILL.md"), "new-content");
        Assert.False(File.Exists(Path.Combine(targetRoot, "stale.md")));
    }

    /// <summary>
    /// 验证安装到 AI 目录时不会把 Unity 导入用的 meta 文件复制过去。
    /// </summary>
    [Fact]
    public void InstallSkipsUnityMetaFilesForAiTargets()
    {
        var projectRoot = CreateProjectWithPackagedSkill("yokiframe");

        _ = new SkillInstallService().Install(projectRoot, "agents", "yokiframe");

        InstallerFileAssert.EqualFile(
            Path.Combine(projectRoot, "Assets", "YokiFrame", "Core", "Editor", "Skills", "yokiframe", "SKILL.md"),
            Path.Combine(projectRoot, ".agents", "skills", "yokiframe", "SKILL.md"));
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
    private string CreateProjectWithPackagedSkill(string skillName)
    {
        var root = Path.Combine(Path.GetTempPath(), "yokiframe-skill-tests", Guid.NewGuid().ToString("N"));
        var skillRoot = Path.Combine(root, "Assets", "YokiFrame", "Core", "Editor", "Skills", skillName);
        Directory.CreateDirectory(skillRoot);
        File.WriteAllText(Path.Combine(skillRoot, "SKILL.md"), "---\nname: " + skillName + "\ndescription: test\n---\n");
        File.WriteAllText(Path.Combine(skillRoot, "SKILL.md.meta"), "fileFormatVersion: 2\n");
        mRoots.Add(root);
        return root;
    }

    /// <summary>
    /// 验证真实包内 Skill 安装后带上 SKILL.md 与全部 references（含 RoslynKit 能力页与 yoki exec 编排说明）。
    /// </summary>
    /// <remarks>
    /// 这条守卫锁定 Workbench"点击安装"要写入的文件数：新增能力页时必须同步更新期望值，
    /// 避免新页只存在于包内、安装到 AI 目录后却缺失。
    /// </remarks>
    [Fact]
    public void InstallCopiesEveryPackagedReferenceIncludingRoslynKit()
    {
        var packageRoot = FindPackageRoot();
        var packagedSkillRoot = Path.Combine(packageRoot, "Core", "Editor", "Skills", "yokiframe");
        var packagedReferences = Directory.GetFiles(Path.Combine(packagedSkillRoot, "references"), "*.md");

        Assert.Equal(7, packagedReferences.Length);
        Assert.Contains(packagedReferences, path => Path.GetFileName(path) == "roslyn-kit.md");
        Assert.Contains(packagedReferences, path => Path.GetFileName(path) == "tablekit-luban.md");

        var projectRoot = Path.Combine(Path.GetTempPath(), "yokiframe-skill-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var packagedCopyRoot = Path.Combine(projectRoot, "Assets", "YokiFrame", "Core", "Editor", "Skills");
            CopyDirectory(Path.Combine(packageRoot, "Core", "Editor", "Skills"), packagedCopyRoot);

            _ = new SkillInstallService().Install(projectRoot, "agents", "yokiframe");

            var installedRoot = Path.Combine(projectRoot, ".agents", "skills", "yokiframe");
            InstallerFileAssert.EqualFile(
                Path.Combine(packagedCopyRoot, "yokiframe", "SKILL.md"),
                Path.Combine(installedRoot, "SKILL.md"));
            var installedReferences = Directory.GetFiles(Path.Combine(installedRoot, "references"), "*.md");
            Assert.Equal(
                packagedReferences.Select(Path.GetFileName).OrderBy(static name => name, StringComparer.Ordinal),
                installedReferences.Select(Path.GetFileName).OrderBy(static name => name, StringComparer.Ordinal));
            foreach (var packagedPath in packagedReferences)
            {
                InstallerFileAssert.EqualFile(
                    packagedPath,
                    Path.Combine(installedRoot, "references", Path.GetFileName(packagedPath)));
            }

            Assert.False(File.Exists(Path.Combine(installedRoot, "references", "roslyn-kit.md.meta")));
        }
        finally
        {
            if (Directory.Exists(projectRoot))
            {
                Directory.Delete(projectRoot, recursive: true);
            }
        }
    }

    /// <summary>
    /// 从测试输出目录向上定位包根。
    /// </summary>
    /// <returns>包含 Core/Editor/Skills 的包根路径。</returns>
    private static string FindPackageRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "Core", "Editor", "Skills")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("YokiFrame package root was not found above " + AppContext.BaseDirectory);
    }

    /// <summary>
    /// 递归复制目录，供真实包夹具使用。
    /// </summary>
    /// <param name="source">源目录。</param>
    /// <param name="target">目标目录。</param>
    private static void CopyDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (var file in Directory.GetFiles(source))
        {
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)), overwrite: true);
        }

        foreach (var directory in Directory.GetDirectories(source))
        {
            CopyDirectory(directory, Path.Combine(target, Path.GetFileName(directory)));
        }
    }
}
