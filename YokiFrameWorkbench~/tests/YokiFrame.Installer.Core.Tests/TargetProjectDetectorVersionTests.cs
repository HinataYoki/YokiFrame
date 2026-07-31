using YokiFrame.Installer.Core.Models;
using YokiFrame.Installer.Core.Services;

namespace YokiFrame.Installer.Core.Tests;

/// <summary>
/// 验证 Installer 在生成计划前执行 Unity、Godot .NET SDK 与目标框架下限门控。
/// </summary>
public sealed class TargetProjectDetectorVersionTests
{
    /// <summary>
    /// 验证 Unity 2022.3.x 是允许安装的最低版本。
    /// </summary>
    [Fact]
    public void DetectAcceptsUnity2022_3()
    {
        var root = CreateUnityProject("2022.3.0f1");

        var info = new TargetProjectDetector().Detect(root);

        Assert.Equal(InstallerProjectKind.Unity, info.Kind);
    }

    /// <summary>
    /// 验证 Unity 2022.3 之前的项目会被明确拒绝，而不是继续生成覆盖计划。
    /// </summary>
    [Fact]
    public void DetectRejectsUnityBefore2022_3()
    {
        var root = CreateUnityProject("2021.3.45f1");

        var error = Assert.Throws<InvalidDataException>(() => new TargetProjectDetector().Detect(root));

        Assert.Contains("2022.3", error.Message, StringComparison.Ordinal);
        Assert.Contains("2021.3.45f1", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 验证当前 Godot 4.7 .NET 基线可进入安装计划。
    /// </summary>
    [Fact]
    public void DetectAcceptsGodot47DotNet()
    {
        var root = CreateGodotProject("4.7.0", "net8.0");

        var info = new TargetProjectDetector().Detect(root);

        Assert.Equal(InstallerProjectKind.Godot, info.Kind);
    }

    /// <summary>
    /// 验证 Godot .NET 新项目尚未生成主 csproj 时，目录仍能先识别为 Godot。
    /// </summary>
    [Fact]
    public void DetectAcceptsGodotDotNetBeforeCsprojGeneration()
    {
        var root = CreateGodotDotNetProjectWithoutCsproj();

        var info = new TargetProjectDetector().Detect(root);

        Assert.Equal(InstallerProjectKind.Godot, info.Kind);
        Assert.Contains(Path.Combine(root, "project.godot"), info.EvidencePaths);
    }

    /// <summary>
    /// 验证普通 Godot 项目不会仅凭 project.godot 被误识别为受支持的 .NET 目标。
    /// </summary>
    [Fact]
    public void DetectKeepsNonDotNetGodotProjectUnknown()
    {
        var root = CreateTempRoot("godot-non-dotnet");
        File.WriteAllText(Path.Combine(root, "project.godot"), "config_version=5\n");

        var info = new TargetProjectDetector().Detect(root);

        Assert.Equal(InstallerProjectKind.Unknown, info.Kind);
    }

    /// <summary>
    /// 验证 Godot .NET 编辑器缓存存在时，即使配置 section 尚未写入也能识别项目。
    /// </summary>
    [Fact]
    public void DetectAcceptsGodotDotNetMonoCacheEvidence()
    {
        var root = CreateTempRoot("godot-mono-cache");
        File.WriteAllText(Path.Combine(root, "project.godot"), "config_version=5\n");
        Directory.CreateDirectory(Path.Combine(root, ".godot", "mono"));

        var info = new TargetProjectDetector().Detect(root);

        Assert.Equal(InstallerProjectKind.Godot, info.Kind);
    }

    /// <summary>
    /// 验证空项目生成主 csproj 前会拒绝低于当前 Godot 4.7 基线的项目特征版本。
    /// </summary>
    [Fact]
    public void ValidateGodotProjectFeatureVersionRejectsBefore47()
    {
        var root = CreateTempRoot("godot-feature-version");
        var settingsPath = Path.Combine(root, "project.godot");
        File.WriteAllText(settingsPath, "config/features=PackedStringArray(\"4.6\", \"Forward Plus\")\n");

        var error = Assert.Throws<InvalidDataException>(
            () => TargetProjectDetector.ValidateGodotProjectFeatureVersion(settingsPath));

        Assert.Contains("4.7", error.Message, StringComparison.Ordinal);
        Assert.Contains("4.6", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 验证当前 Godot 4.7 特征版本可进入空项目主 csproj 生成流程。
    /// </summary>
    [Fact]
    public void ValidateGodotProjectFeatureVersionAccepts47()
    {
        var root = CreateTempRoot("godot-feature-version");
        var settingsPath = Path.Combine(root, "project.godot");
        File.WriteAllText(settingsPath, "config/features=PackedStringArray(\"4.7\", \"Forward Plus\")\n");

        TargetProjectDetector.ValidateGodotProjectFeatureVersion(settingsPath);
    }

    /// <summary>
    /// 验证当前基线之后的 Godot .NET SDK 与目标框架不会被固定版本门控误拒绝。
    /// </summary>
    /// <param name="sdkVersion">待验证 Godot.NET.Sdk 版本。</param>
    [Theory]
    [InlineData("4.8.0", "net8.0")]
    [InlineData("5.0.0", "net10.0")]
    public void DetectAcceptsGodotDotNetAfterCurrentBaseline(string sdkVersion, string targetFramework)
    {
        var root = CreateGodotProject(sdkVersion, targetFramework);

        var info = new TargetProjectDetector().Detect(root);

        Assert.Equal(InstallerProjectKind.Godot, info.Kind);
    }

    /// <summary>
    /// 验证当前 4.7 SDK 基线之前的 Godot .NET 版本会被明确拒绝。
    /// </summary>
    [Fact]
    public void DetectRejectsGodotSdkBefore47()
    {
        var root = CreateGodotProject("4.6.2", "net8.0");

        var error = Assert.Throws<InvalidDataException>(() => new TargetProjectDetector().Detect(root));

        Assert.Contains("4.7", error.Message, StringComparison.Ordinal);
        Assert.Contains("4.6.2", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 验证低于 net8.0 的目标框架会被拒绝，后续 SDK 可继续使用更高 TFM。
    /// </summary>
    [Fact]
    public void DetectRejectsGodotDesktopTargetBeforeNet8()
    {
        var root = CreateGodotProject("4.7.0", "net7.0");

        var error = Assert.Throws<InvalidDataException>(() => new TargetProjectDetector().Detect(root));

        Assert.Contains("net8.0", error.Message, StringComparison.Ordinal);
        Assert.Contains("net7.0", error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 创建带真实版本文件的最小 Unity 项目 fixture。
    /// </summary>
    /// <param name="editorVersion">写入 ProjectVersion.txt 的 Unity 版本。</param>
    /// <returns>Unity 项目根目录。</returns>
    private static string CreateUnityProject(string editorVersion)
    {
        var root = CreateTempRoot("unity-version");
        Directory.CreateDirectory(Path.Combine(root, "Assets"));
        Directory.CreateDirectory(Path.Combine(root, "Packages"));
        Directory.CreateDirectory(Path.Combine(root, "ProjectSettings"));
        File.WriteAllText(Path.Combine(root, "Packages", "manifest.json"), "{\"dependencies\":{}}");
        File.WriteAllText(
            Path.Combine(root, "ProjectSettings", "ProjectVersion.txt"),
            "m_EditorVersion: " + editorVersion + Environment.NewLine);
        return root;
    }

    /// <summary>
    /// 创建带指定 SDK 与桌面 TFM 的最小 Godot .NET 项目 fixture。
    /// </summary>
    /// <param name="sdkVersion">Godot.NET.Sdk 版本。</param>
    /// <param name="targetFramework">桌面目标框架。</param>
    /// <returns>Godot 项目根目录。</returns>
    private static string CreateGodotProject(string sdkVersion, string targetFramework)
    {
        var root = CreateTempRoot("godot-version");
        File.WriteAllText(Path.Combine(root, "project.godot"), "config_version=5");
        File.WriteAllText(
            Path.Combine(root, "Game.csproj"),
            "<Project Sdk=\"Godot.NET.Sdk/" + sdkVersion + "\"><PropertyGroup><TargetFramework>"
            + targetFramework + "</TargetFramework></PropertyGroup></Project>");
        return root;
    }

    /// <summary>
    /// 创建只包含 Godot .NET 配置证据、尚未由编辑器生成 csproj 的新项目。
    /// </summary>
    /// <returns>临时 Godot .NET 项目根目录。</returns>
    private static string CreateGodotDotNetProjectWithoutCsproj()
    {
        var root = CreateTempRoot("godot-dotnet-no-csproj");
        File.WriteAllText(
            Path.Combine(root, "project.godot"),
            "config_version=5\n\n[dotnet]\nproject/assembly_name=\"GodotFixture\"\n");
        return root;
    }

    /// <summary>
    /// 创建测试专用临时根目录。
    /// </summary>
    /// <param name="prefix">目录名称前缀。</param>
    /// <returns>临时根目录。</returns>
    private static string CreateTempRoot(string prefix)
    {
        var root = Path.Combine(Path.GetTempPath(), "yokiframe-installer-tests", prefix + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }
}
