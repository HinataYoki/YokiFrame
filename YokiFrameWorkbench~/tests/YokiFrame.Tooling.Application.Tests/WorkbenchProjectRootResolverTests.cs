using YokiFrame.Tooling.Application.Environment;

namespace YokiFrame.Tooling.Application.Tests;

/// <summary>
/// 覆盖 Workbench 项目根目录解析规则。
/// </summary>
public sealed class WorkbenchProjectRootResolverTests
{
    /// <summary>
    /// 验证明示 `--project` 参数优先于当前目录探测。
    /// </summary>
    [Fact]
    public void ResolvePrefersExplicitProjectOption()
    {
        var explicitRoot = Path.Combine(Path.GetTempPath(), "yokiframe-explicit-root");
        var resolvedRoot = WorkbenchProjectRootResolver.Resolve(new[] { "--project", explicitRoot }, Directory.GetCurrentDirectory());

        Assert.Equal(Path.GetFullPath(explicitRoot), resolvedRoot);
    }

    /// <summary>
    /// 验证从子目录向上找到包含 `.yokiframe` 的项目根。
    /// </summary>
    [Fact]
    public void ResolveFindsNearestYokiFrameRoot()
    {
        var projectRoot = CreateProjectRoot();
        var childDirectory = Path.Combine(projectRoot, "Assets", "YokiFrame");
        Directory.CreateDirectory(Path.Combine(projectRoot, ".yokiframe"));
        Directory.CreateDirectory(childDirectory);

        var resolvedRoot = WorkbenchProjectRootResolver.Resolve(Array.Empty<string>(), childDirectory);

        Assert.Equal(projectRoot, resolvedRoot);
    }

    /// <summary>
    /// 创建唯一测试项目根目录。
    /// </summary>
    /// <returns>测试项目根目录。</returns>
    private static string CreateProjectRoot()
    {
        return Path.Combine(Path.GetTempPath(), "yokiframe-workbench-root-tests", Guid.NewGuid().ToString("N"));
    }
}
