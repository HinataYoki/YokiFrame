using YokiFrame.Workbench.Avalonia.Services;

namespace YokiFrame.Workbench.Avalonia.Tests;

/// <summary>
/// 验证 Workbench 在 Godot 受控投影包中的更新检测降级行为。
/// </summary>
public sealed class WorkbenchRuntimeUpdateServiceTests
{
    /// <summary>
    /// 验证缺少 Workbench 源码的 Godot 投影包不会被误报为新版检测失败。
    /// </summary>
    [Fact]
    public async Task CheckSkipsProjectedPackageWithoutWorkbenchSource()
    {
        var sourceRoot = Path.Combine(Path.GetTempPath(), "yokiframe-workbench-projection", Guid.NewGuid().ToString("N"));
        var projectRoot = Path.Combine(Path.GetTempPath(), "yokiframe-workbench-project", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(sourceRoot);
        Directory.CreateDirectory(projectRoot);
        try
        {
            var service = new WorkbenchRuntimeUpdateService();
            var result = await service.CheckAsync(
                sourceRoot,
                projectRoot,
                "running-fingerprint",
                CancellationToken.None);

            Assert.False(result.UpdateAvailable);
            Assert.Equal("running-fingerprint", result.SourceFingerprint);
        }
        finally
        {
            Directory.Delete(sourceRoot, recursive: true);
            Directory.Delete(projectRoot, recursive: true);
        }
    }
}
