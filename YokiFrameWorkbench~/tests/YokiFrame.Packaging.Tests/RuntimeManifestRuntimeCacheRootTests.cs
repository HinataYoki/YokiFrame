using YokiFrame.Packaging.Services;

namespace YokiFrame.Packaging.Tests;

/// <summary>
/// 覆盖项目级 `.yokiframe/runtime` 路径下生成 manifest 的边界行为。
/// </summary>
public sealed class RuntimeManifestRuntimeCacheRootTests
{
    /// <summary>
    /// 验证 Runtime 根自身位于 `.yokiframe` 下时仍记录平台载荷，只忽略平台内部状态目录。
    /// </summary>
    [Fact]
    public void BuildIncludesPayloadBelowProjectRuntimeCacheRoot()
    {
        var projectRoot = Path.Combine(Path.GetTempPath(), "yokiframe-runtime-root-tests", Guid.NewGuid().ToString("N"));
        var runtimeRoot = Path.Combine(projectRoot, ".yokiframe", "runtime", "fingerprint");
        var platformRoot = Path.Combine(runtimeRoot, "win-x64");
        var guiPath = Path.Combine(platformRoot, "Workbench.exe");
        Directory.CreateDirectory(platformRoot);
        File.WriteAllText(guiPath, "gui");
        var ignoredPath = Path.Combine(platformRoot, ".yokiframe", "session.json");
        Directory.CreateDirectory(Path.GetDirectoryName(ignoredPath)!);
        File.WriteAllText(ignoredPath, "state");

        var manifest = new RuntimeManifestBuilder().Build(runtimeRoot, "YokiFrameTool", "win-x64", "Workbench.exe");

        var platform = Assert.Single(manifest.Platforms);
        Assert.Single(platform.Files);
        Assert.Equal("win-x64/Workbench.exe", platform.Files[0].RelativePath);
    }
}
