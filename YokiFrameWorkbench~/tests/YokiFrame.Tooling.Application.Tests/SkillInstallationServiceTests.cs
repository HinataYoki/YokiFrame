using YokiFrame.Tooling.Application.Skills;

namespace YokiFrame.Tooling.Application.Tests;

/// <summary>
/// 覆盖 Application Skill 安装用例与自有 DTO 映射。
/// </summary>
public sealed class SkillInstallationServiceTests
{
    /// <summary>
    /// 验证状态读取返回包内 Skill、预设目标和已安装列表。
    /// </summary>
    [Fact]
    public void GetStatusMapsPackagedSkillsAndTargets()
    {
        using var fixture = SkillInstallationFixture.Create("yokiframe");
        fixture.SeedInstalledSkill(".agents/skills", "yokiframe");

        var status = new SkillInstallationService().GetStatus(fixture.ProjectRoot);

        Assert.Equal(Path.GetFullPath(fixture.ProjectRoot), Path.GetFullPath(status.ProjectRoot));
        Assert.Contains(status.Skills, skill => skill.Name == "yokiframe" && skill.Packaged);
        Assert.Contains(status.Targets, target => target.Id == "codex" && !target.SupportsCustomPath);
        var agents = Assert.Single(status.Targets, target => target.Id == "agents");
        Assert.Contains("yokiframe", agents.InstalledSkills);
    }

    /// <summary>
    /// 验证安装和卸载通过 Core 文件能力执行，但只返回 Application 结果。
    /// </summary>
    [Fact]
    public void InstallAndUninstallReturnApplicationResults()
    {
        using var fixture = SkillInstallationFixture.Create("yokiframe");
        var service = new SkillInstallationService();

        var installed = service.Install(fixture.ProjectRoot, "codex", "yokiframe");
        var uninstalled = service.Uninstall(fixture.ProjectRoot, "codex", "yokiframe");

        Assert.True(installed.Success);
        Assert.True(installed.Installed);
        Assert.Equal("codex", installed.TargetId);
        Assert.False(uninstalled.Installed);
        Assert.False(File.Exists(Path.Combine(fixture.ProjectRoot, ".codex", "skills", "yokiframe", "SKILL.md")));
    }

    /// <summary>
    /// 验证 Skills 公开 API 不向 Avalonia 泄漏 Installer.Core 类型。
    /// </summary>
    [Fact]
    public void SkillPublicApiDoesNotExposeInstallerCoreTypes()
    {
        var types = typeof(SkillInstallationService).Assembly
            .GetExportedTypes()
            .Where(static type => type.Namespace == "YokiFrame.Tooling.Application.Skills")
            .ToArray();

        foreach (var type in types)
        {
            Assert.All(type.GetConstructors(), static constructor =>
                Assert.All(constructor.GetParameters(), static parameter => AssertNotCoreType(parameter.ParameterType)));
            Assert.All(type.GetProperties(), static property => AssertNotCoreType(property.PropertyType));
            Assert.All(type.GetMethods(), static method =>
            {
                AssertNotCoreType(method.ReturnType);
                Assert.All(method.GetParameters(), static parameter => AssertNotCoreType(parameter.ParameterType));
            });
        }
    }

    /// <summary>
    /// 递归检查普通、数组和泛型公开类型是否来自 Installer.Core。
    /// </summary>
    /// <param name="type">待检查类型。</param>
    private static void AssertNotCoreType(Type type)
    {
        Assert.DoesNotContain("YokiFrame.Installer.Core", type.FullName ?? string.Empty, StringComparison.Ordinal);
        if (type.IsArray)
        {
            AssertNotCoreType(type.GetElementType()!);
        }

        foreach (var argument in type.GetGenericArguments())
        {
            AssertNotCoreType(argument);
        }
    }

    /// <summary>
    /// 提供隔离项目和包内 Skill 源。
    /// </summary>
    private sealed class SkillInstallationFixture : IDisposable
    {
        /// <summary>
        /// 创建指定 Skill 的隔离项目。
        /// </summary>
        /// <param name="skillName">包内 Skill 名称。</param>
        private SkillInstallationFixture(string skillName)
        {
            ProjectRoot = Path.Combine(Path.GetTempPath(), "yokiframe-application-skill-tests", Guid.NewGuid().ToString("N"));
            var skillRoot = Path.Combine(ProjectRoot, "Assets", "YokiFrame", "Core", "Editor", "Skills", skillName);
            Directory.CreateDirectory(skillRoot);
            File.WriteAllText(Path.Combine(skillRoot, "SKILL.md"), "---\nname: " + skillName + "\ndescription: test\n---\n");
            File.WriteAllText(Path.Combine(skillRoot, "SKILL.md.meta"), "fileFormatVersion: 2\n");
        }

        /// <summary>
        /// 获取隔离项目根。
        /// </summary>
        internal string ProjectRoot { get; }

        /// <summary>
        /// 创建指定 Skill 的隔离项目。
        /// </summary>
        /// <param name="skillName">包内 Skill 名称。</param>
        /// <returns>测试 fixture。</returns>
        internal static SkillInstallationFixture Create(string skillName)
        {
            return new SkillInstallationFixture(skillName);
        }

        /// <summary>
        /// 在指定目标目录预置已安装 Skill。
        /// </summary>
        /// <param name="relativePath">目标相对目录。</param>
        /// <param name="skillName">Skill 名称。</param>
        internal void SeedInstalledSkill(string relativePath, string skillName)
        {
            var skillRoot = Path.Combine(ProjectRoot, relativePath.Replace('/', Path.DirectorySeparatorChar), skillName);
            Directory.CreateDirectory(skillRoot);
            File.WriteAllText(Path.Combine(skillRoot, "SKILL.md"), "installed");
        }

        /// <summary>
        /// 删除测试项目及安装结果。
        /// </summary>
        public void Dispose()
        {
            if (Directory.Exists(ProjectRoot))
            {
                Directory.Delete(ProjectRoot, recursive: true);
            }
        }
    }
}
