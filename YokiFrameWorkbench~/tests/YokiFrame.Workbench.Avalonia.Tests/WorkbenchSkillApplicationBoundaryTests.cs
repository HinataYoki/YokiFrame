namespace YokiFrame.Workbench.Avalonia.Tests;

/// <summary>
/// 锁定 Workbench Skill 面板只能通过 Tooling.Application 调用安装能力。
/// </summary>
public sealed class WorkbenchSkillApplicationBoundaryTests
{
    /// <summary>
    /// 验证 Avalonia 项目不直接引用 Installer.Core。
    /// </summary>
    [Fact]
    public void AvaloniaProjectDoesNotReferenceInstallerCore()
    {
        var projectSource = File.ReadAllText(FindSourceFile("YokiFrame.Workbench.Avalonia.csproj"));

        Assert.DoesNotContain("YokiFrame.Installer.Core", projectSource, StringComparison.Ordinal);
        Assert.Contains("YokiFrame.Tooling.Application", projectSource, StringComparison.Ordinal);
    }

    /// <summary>
    /// 验证 Windows 原生控件宿主所需的兼容性清单已嵌入 Workbench 可执行文件。
    /// </summary>
    [Fact]
    public void AvaloniaProjectEmbedsWindowsCompatibilityManifest()
    {
        var projectSource = File.ReadAllText(FindSourceFile("YokiFrame.Workbench.Avalonia.csproj"));
        var manifestSource = File.ReadAllText(FindSourceFile("app.manifest"));

        Assert.Contains("<ApplicationManifest>app.manifest</ApplicationManifest>", projectSource, StringComparison.Ordinal);
        Assert.Contains("{8e0f7a12-bfb3-4fe8-b9a5-48fd50a15a9a}", manifestSource, StringComparison.Ordinal);
        Assert.Contains("PerMonitorV2", manifestSource, StringComparison.Ordinal);
    }

    /// <summary>
    /// 验证 Skills partial 使用 Application 用例和 DTO，不直接导入 Core。
    /// </summary>
    [Fact]
    public void SkillsViewModelUsesApplicationSkillUseCase()
    {
        var source = File.ReadAllText(FindSourceFile("ViewModels", "WorkbenchShellViewModel.Skills.cs"));

        Assert.Contains("YokiFrame.Tooling.Application.Skills", source, StringComparison.Ordinal);
        Assert.Contains("SkillInstallationService", source, StringComparison.Ordinal);
        Assert.DoesNotContain("YokiFrame.Installer.Core", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SkillInstallService", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// 从测试输出目录向上定位 Avalonia 源文件。
    /// </summary>
    /// <param name="segments">相对于 Avalonia 项目的路径片段。</param>
    /// <returns>源文件绝对路径。</returns>
    private static string FindSourceFile(params string[] segments)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            var relativeSegments = new[] { directory.FullName, "src", "YokiFrame.Workbench.Avalonia" }.Concat(segments).ToArray();
            var candidate = Path.Combine(relativeSegments);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("无法定位 Avalonia 源文件: " + string.Join('/', segments));
    }
}
