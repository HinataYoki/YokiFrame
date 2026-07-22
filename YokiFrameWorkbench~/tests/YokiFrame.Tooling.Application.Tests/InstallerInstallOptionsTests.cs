using YokiFrame.Tooling.Application.Installer;

namespace YokiFrame.Tooling.Application.Tests;

/// <summary>
/// 覆盖 Installer 三种互斥安装模式及其专属选项。
/// </summary>
public sealed class InstallerInstallOptionsTests
{
    /// <summary>
    /// 验证 Unity 本地模式保留源包路径和 legacy 接管策略。
    /// </summary>
    [Fact]
    public void CreateUnityLocalKeepsLocalPackageOptions()
    {
        var options = InstallerInstallOptions.CreateUnityLocal(
            "C:/packages/YokiFrame",
            "C:/projects/UnityGame",
            InstallerLegacyPackagePolicy.TakeOverConfirmed);

        Assert.Equal(InstallerInstallMode.UnityLocal, options.Mode);
        Assert.Equal("C:/packages/YokiFrame", options.SourcePackageRoot);
        Assert.Equal("C:/projects/UnityGame", options.TargetProjectRoot);
        Assert.Equal(InstallerLegacyPackagePolicy.TakeOverConfirmed, options.LegacyPackagePolicy);
        Assert.Equal("win-x64-aot", options.RuntimeProfile);
        Assert.Null(options.GitUrl);
        Assert.Null(options.GodotOptions);
    }

    /// <summary>
    /// 验证 Unity Git 模式只携带目标项目和可编辑 Git URL。
    /// </summary>
    [Fact]
    public void CreateUnityGitKeepsGitUrlWithoutLocalSource()
    {
        var options = InstallerInstallOptions.CreateUnityGit(
            "C:/projects/UnityGame",
            "https://github.com/HinataYoki/YokiFrame.git?path=Assets/YokiFrame");

        Assert.Equal(InstallerInstallMode.UnityGit, options.Mode);
        Assert.Null(options.SourcePackageRoot);
        Assert.Equal("C:/projects/UnityGame", options.TargetProjectRoot);
        Assert.Equal("https://github.com/HinataYoki/YokiFrame.git?path=Assets/YokiFrame", options.GitUrl);
        Assert.Null(options.GodotOptions);
        Assert.Equal(InstallerLegacyPackagePolicy.Reject, options.LegacyPackagePolicy);
        Assert.Equal("win-x64-aot", options.RuntimeProfile);
    }

    /// <summary>
    /// 验证 Godot 本地模式独立表达 project.godot 修复和插件登记选项。
    /// </summary>
    [Fact]
    public void CreateGodotLocalKeepsGodotProjectOptions()
    {
        GodotInstallOptions godotOptions = new(repairProjectSettings: true, enablePlugin: false);

        var options = InstallerInstallOptions.CreateGodotLocal(
            "C:/packages/YokiFrame",
            "C:/projects/GodotGame",
            godotOptions,
            InstallerLegacyPackagePolicy.Reject);

        Assert.Equal(InstallerInstallMode.GodotLocal, options.Mode);
        var actualGodotOptions = Assert.IsType<GodotInstallOptions>(options.GodotOptions);
        Assert.Same(godotOptions, actualGodotOptions);
        Assert.True(actualGodotOptions.RepairProjectSettings);
        Assert.False(actualGodotOptions.EnablePlugin);
        Assert.Null(options.GitUrl);
    }
}
