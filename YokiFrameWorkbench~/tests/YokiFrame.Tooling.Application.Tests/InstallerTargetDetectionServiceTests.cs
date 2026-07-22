using YokiFrame.Tooling.Application.Installer;

namespace YokiFrame.Tooling.Application.Tests;

/// <summary>
/// 覆盖 Installer 自动目标检测的 Application read model 映射。
/// </summary>
public sealed class InstallerTargetDetectionServiceTests
{
    /// <summary>
    /// 验证 Unity 项目返回包目标和检测证据，不泄漏 Core DTO。
    /// </summary>
    [Fact]
    public void DetectReturnsUnityTargetReadModel()
    {
        using var fixture = InstallerApplicationFixture.Create();

        var target = new InstallerTargetDetectionService().Detect(fixture.UnityProjectRoot);

        Assert.Equal(InstallerTargetKind.Unity, target.Kind);
        Assert.Equal(Path.GetFullPath(fixture.UnityProjectRoot), target.ProjectRoot);
        Assert.Equal(fixture.UnityPackageRoot, target.PackageTarget);
        Assert.True(target.IsRecognized);
        Assert.Contains(fixture.UnityManifestPath, target.EvidencePaths);
    }

    /// <summary>
    /// 验证 Godot 4.7 .NET 项目返回插件包目标。
    /// </summary>
    [Fact]
    public void DetectReturnsGodotTargetReadModel()
    {
        using var fixture = InstallerApplicationFixture.Create();

        var target = new InstallerTargetDetectionService().Detect(fixture.GodotProjectRoot);

        Assert.Equal(InstallerTargetKind.Godot, target.Kind);
        Assert.Equal(fixture.GodotPackageRoot, target.PackageTarget);
        Assert.True(target.IsRecognized);
        Assert.Contains(Path.Combine(fixture.GodotProjectRoot, "project.godot"), target.EvidencePaths);
    }

    /// <summary>
    /// 验证普通目录返回 Unknown read model，而不是伪装成任一宿主。
    /// </summary>
    [Fact]
    public void DetectReturnsUnknownForUnsupportedDirectory()
    {
        using var fixture = InstallerApplicationFixture.Create();

        var target = new InstallerTargetDetectionService().Detect(fixture.UnknownProjectRoot);

        Assert.Equal(InstallerTargetKind.Unknown, target.Kind);
        Assert.False(target.IsRecognized);
        Assert.Empty(target.PackageTarget);
        Assert.Empty(target.EvidencePaths);
    }
}
