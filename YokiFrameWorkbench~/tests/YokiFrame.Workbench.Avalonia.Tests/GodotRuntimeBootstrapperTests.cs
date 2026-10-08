using YokiFrame.Workbench.Avalonia.Services;

namespace YokiFrame.Workbench.Avalonia.Tests;

/// <summary>
/// 覆盖 Installer 触发 Godot Runtime bootstrap 时的无 shell 参数转发。
/// </summary>
public sealed class GodotRuntimeBootstrapperTests
{
    /// <summary>
    /// 打开安装器时转发完整 Packaging bootstrap 命令，且参数列表包含打开开关。
    /// </summary>
    [Fact]
    public void 创建启动信息_打开安装器_转发完整命令且包含打开开关()
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

    /// <summary>
    /// 不打开安装器时仍转发完整 Packaging bootstrap 命令，且参数列表不包含打开开关。
    /// </summary>
    [Fact]
    public void 创建启动信息_不打开安装器_保留完整命令且不含打开开关()
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
            packagingProjectPath,
            openInstaller: false);

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
                "Release"
            },
            startInfo.ArgumentList.ToArray());
    }
}
