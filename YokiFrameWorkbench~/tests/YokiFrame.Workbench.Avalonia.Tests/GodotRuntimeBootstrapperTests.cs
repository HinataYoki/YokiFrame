using YokiFrame.Workbench.Avalonia.Services;

namespace YokiFrame.Workbench.Avalonia.Tests;

/// <summary>
/// 覆盖 Installer 触发 Godot Runtime bootstrap 时的无 shell 参数转发。
/// </summary>
public sealed class GodotRuntimeBootstrapperTests
{
    /// <summary>
    /// 验证 bootstrap 通过 Packaging 项目传入 source、target 和打开新 Installer 开关。
    /// </summary>
    [Fact]
    public void CreateStartInfoUsesPackagingBootstrapWithOpenInstaller()
    {
        var sourcePackageRoot = Path.Combine(Path.GetTempPath(), "yokiframe source");
        var targetProjectRoot = Path.Combine(Path.GetTempPath(), "godot project");
        var packagingProjectPath = Path.Combine(
            sourcePackageRoot,
            "YokiFrameWorkbench~",
            "src",
            "YokiFrame.Packaging",
            "YokiFrame.Packaging.csproj");

        var startInfo = GodotRuntimeBootstrapper.CreateStartInfo(
            sourcePackageRoot,
            targetProjectRoot,
            packagingProjectPath);

        Assert.Equal("dotnet", startInfo.FileName);
        Assert.Equal(sourcePackageRoot, startInfo.WorkingDirectory);
        Assert.False(startInfo.UseShellExecute);
        Assert.Equal(
            new[]
            {
                "run",
                "--project",
                packagingProjectPath,
                "--",
                "runtime",
                "bootstrap",
                "--package-root",
                sourcePackageRoot,
                "--project-root",
                targetProjectRoot,
                "--configuration",
                "Release",
                "--open-installer"
            },
            startInfo.ArgumentList.ToArray());
    }
}
