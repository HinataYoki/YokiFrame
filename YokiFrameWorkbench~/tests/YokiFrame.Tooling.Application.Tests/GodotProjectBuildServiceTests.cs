using YokiFrame.Installer.Core.Models;
using YokiFrame.Installer.Core.Services;

namespace YokiFrame.Tooling.Application.Tests;

/// <summary>
/// 验证 Godot 主项目构建进程的参数边界，不启动真实 dotnet 子进程。
/// </summary>
public sealed class GodotProjectBuildServiceTests
{
    /// <summary>
    /// 验证 restore 参数使用独立参数列表，并保留带空格的项目路径。
    /// </summary>
    [Fact]
    public void CreateRestoreStartInfoKeepsProjectPathAsOneArgument()
    {
        var projectPath = Path.Combine(Path.GetTempPath(), "godot project", "FirstDemo.csproj");
        var workingDirectory = Path.GetDirectoryName(projectPath)!;

        var startInfo = YokiFrame.Tooling.Application.Installer.GodotProjectBuildService.CreateRestoreStartInfo(
            projectPath,
            workingDirectory);

        Assert.Equal("dotnet", startInfo.FileName);
        Assert.Equal(
            new[]
            {
                "restore",
                projectPath,
                "-p:GodotTarget=Editor",
                "--verbosity",
                "minimal"
            },
            startInfo.ArgumentList.ToArray());
        Assert.False(startInfo.UseShellExecute);
        Assert.True(startInfo.RedirectStandardOutput);
        Assert.True(startInfo.RedirectStandardError);
    }

    /// <summary>
    /// 验证 build 参数复用 restore 结果且没有偷偷切换为串行项目图 workaround。
    /// </summary>
    [Fact]
    public void CreateBuildStartInfoDisablesRestoreWithoutChangingParallelism()
    {
        var projectPath = Path.Combine(Path.GetTempPath(), "godot project", "FirstDemo.csproj");
        var workingDirectory = Path.GetDirectoryName(projectPath)!;

        var startInfo = YokiFrame.Tooling.Application.Installer.GodotProjectBuildService.CreateBuildStartInfo(
            projectPath,
            workingDirectory);

        Assert.Equal(
            new[]
            {
                "build",
                projectPath,
                "--no-restore",
                "--no-incremental",
                "-p:GodotTarget=Editor",
                "--verbosity",
                "minimal"
            },
            startInfo.ArgumentList.ToArray());
        Assert.DoesNotContain("-m:1", startInfo.ArgumentList);
        Assert.DoesNotContain("--no-parallel", startInfo.ArgumentList);
    }

    /// <summary>
    /// 验证 Godot 4.7 .NET 的主程序集路径包含 mono/temp 工作区目录。
    /// </summary>
    [Fact]
    public void GetAssemblyOutputPathUsesGodotMonoTempDirectory()
    {
        var projectRoot = Path.Combine(Path.GetTempPath(), "yokiframe-godot-output", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(projectRoot);
        var projectPath = Path.Combine(projectRoot, "GodotYokiframe.csproj");
        var settingsPath = Path.Combine(projectRoot, "project.godot");
        try
        {
            File.WriteAllText(
                projectPath,
                "<Project><PropertyGroup><AssemblyName>GodotYokiframe</AssemblyName></PropertyGroup></Project>");
            File.WriteAllText(settingsPath, "[dotnet]\nproject/assembly_name=\"GodotYokiframe\"\n");

            var outputPath = YokiFrame.Tooling.Application.Installer.GodotProjectBuildService.GetAssemblyOutputPath(
                projectRoot,
                projectPath,
                settingsPath);

            Assert.Equal(
                Path.Combine(projectRoot, ".godot", "mono", "temp", "bin", "Debug", "GodotYokiframe.dll"),
                outputPath);
        }
        finally
        {
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    /// <summary>
    /// 验证程序集校验同时覆盖 Godot 4.7 的 mono/temp 输出和旧版 mono 输出布局。
    /// </summary>
    [Fact]
    public void GetAssemblyOutputCandidatesIncludesGodotOutputLayouts()
    {
        using var fixture = InstallerApplicationFixture.Create();
        var plan = new GodotInstallService().CreatePlan(
            new GodotInstallRequest(
                fixture.SourcePackageRoot,
                fixture.GodotProjectRoot,
                "win-x64-aot",
                repairProjectSettings: false,
                enablePlugin: false,
                UnmanagedPackagePolicy.Reject));

        var candidates = YokiFrame.Tooling.Application.Installer.GodotProjectBuildService
            .GetAssemblyOutputCandidates(plan);

        Assert.Contains(
            Path.Combine(fixture.GodotProjectRoot, ".godot", "mono", "temp", "bin", "Debug", "FirstDemo.dll"),
            candidates);
        Assert.Contains(
            Path.Combine(fixture.GodotProjectRoot, ".godot", "mono", "bin", "Debug", "FirstDemo.dll"),
            candidates);
    }
}
