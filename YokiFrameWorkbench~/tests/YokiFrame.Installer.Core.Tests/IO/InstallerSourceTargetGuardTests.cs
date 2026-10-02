using YokiFrame.Installer.Core.IO;
using YokiFrame.Installer.Core.Models;
using YokiFrame.Installer.Core.Services;
using YokiFrame.Installer.Core.Tests.Unity;

namespace YokiFrame.Installer.Core.Tests.IO;

/// <summary>
/// 锁定安装来源与目标项目重叠时的拒绝行为，避免覆盖开发源码。
/// </summary>
public sealed class InstallerSourceTargetGuardTests
{
    /// <summary>
    /// 验证相同路径、互相包含会重叠，共享前缀的兄弟目录不会重叠。
    /// </summary>
    [Fact]
    public void OverlapsDetectsNestedDirectoriesButNotSiblingPrefixes()
    {
        var root = Path.Combine(Path.GetTempPath(), "yokiframe-source-target-" + Guid.NewGuid().ToString("N"));
        var project = Path.Combine(root, "YokiFrame");
        var source = Path.Combine(project, "Assets", "YokiFrame");
        var sibling = Path.Combine(root, "YokiFrameBackup");

        Assert.True(InstallerSourceTargetGuard.Overlaps(source, project));
        Assert.True(InstallerSourceTargetGuard.Overlaps(project, source));
        Assert.True(InstallerSourceTargetGuard.Overlaps(source, source));
        Assert.False(InstallerSourceTargetGuard.Overlaps(project, sibling));
        Assert.False(InstallerSourceTargetGuard.Overlaps(source, null));
        Assert.False(InstallerSourceTargetGuard.Overlaps(" ", project));
    }

    /// <summary>
    /// 验证 Unity 计划在写入前拒绝源码位于目标项目内，且不创建 embedded 包目录。
    /// </summary>
    [Fact]
    public void UnityPlanRejectsDevelopmentProjectBeforeWriting()
    {
        using UnityInstallFixture fixture = UnityInstallFixture.Create();
        var source = Path.Combine(fixture.ProjectRoot, "Assets", "YokiFrame");
        Directory.CreateDirectory(source);
        UnityInstallRequest request = new(
            source,
            fixture.ProjectRoot,
            "win-x64",
            UnityInstallMode.Embedded,
            gitUrl: null,
            UnmanagedPackagePolicy.Reject);

        var exception = Assert.Throws<InvalidOperationException>(
            () => new UnityInstallService().CreatePlan(request));

        Assert.Equal(InstallerSourceTargetGuard.OverlapMessage, exception.Message);
        Assert.False(Directory.Exists(fixture.EmbeddedPackageRoot));
        Assert.Throws<InvalidOperationException>(() => new UnityInstallService().Execute(request));
        Assert.False(Directory.Exists(fixture.EmbeddedPackageRoot));
    }

    /// <summary>
    /// 验证 Godot 执行在创建 add-on 前拒绝目标项目包含源目录。
    /// </summary>
    [Fact]
    public void GodotExecuteRejectsSourceInsideTargetBeforeCreatingAddon()
    {
        var root = Path.Combine(Path.GetTempPath(), "yokiframe-godot-source-target-" + Guid.NewGuid().ToString("N"));
        var project = Path.Combine(root, "DevProject");
        var source = Path.Combine(project, "Assets", "YokiFrame");
        Directory.CreateDirectory(source);
        try
        {
            var exception = Assert.Throws<InvalidOperationException>(
                () => new GodotInstallService().Execute(
                    source,
                    project,
                    "win-x64",
                    UnmanagedPackagePolicy.Reject));

            Assert.Equal(InstallerSourceTargetGuard.OverlapMessage, exception.Message);
            Assert.False(Directory.Exists(Path.Combine(project, "addons")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
