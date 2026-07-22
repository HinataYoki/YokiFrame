using System.Runtime.InteropServices;
using YokiFrame.Packaging.Services;

namespace YokiFrame.Packaging.Tests;

/// <summary>
/// 覆盖从任意 YokiFrame 包根生成当前平台发布路径计划。
/// </summary>
public sealed class RuntimePublishPlanBuilderTests
{
    /// <summary>
    /// 验证发布计划相对包根定位工具源码，并把可再生 Runtime 输出放在包外项目缓存。
    /// </summary>
    [Fact]
    public void BuildResolvesSourceAndOutputPathsFromPackageRoot()
    {
        var packageRoot = CreatePackageRoot();
        var runtimeRoot = CreateExternalRuntimeRoot();

        var plan = new RuntimePublishPlanBuilder().Build(
            packageRoot,
            runtimeRoot,
            "Release",
            OSPlatform.OSX,
            Architecture.Arm64);

        Assert.Equal(packageRoot, plan.PackageRoot);
        Assert.Equal("Release", plan.Configuration);
        Assert.Equal("osx-arm64", plan.Profile.RuntimeIdentifier);
        Assert.Equal(Path.Combine(packageRoot, "YokiFrameWorkbench~"), plan.WorkbenchRoot);
        Assert.Equal(
            Path.Combine(packageRoot, "YokiFrameWorkbench~", "src", "YokiFrame.Workbench.Avalonia", "YokiFrame.Workbench.Avalonia.csproj"),
            plan.GuiProjectPath);
        Assert.Equal(
            Path.Combine(packageRoot, "YokiFrameWorkbench~", "src", "YokiFrame.Cli", "YokiFrame.Cli.csproj"),
            plan.CliProjectPath);
        Assert.Equal(runtimeRoot, plan.RuntimeRoot);
        Assert.Equal(Path.Combine(runtimeRoot, "osx-arm64"), plan.PublishRoot);
        Assert.Equal(Path.Combine(runtimeRoot, "tool-manifest.json"), plan.ManifestPath);
    }

    /// <summary>
    /// 验证包根可以位于任意目录，发布计划不会重新拼接历史 `Assets/YokiFrame` 路径。
    /// </summary>
    [Fact]
    public void BuildDoesNotRequireAssetsYokiFrameAncestor()
    {
        var packageRoot = CreatePackageRoot();
        var runtimeRoot = CreateExternalRuntimeRoot();

        var plan = new RuntimePublishPlanBuilder().Build(
            packageRoot,
            runtimeRoot,
            "Debug",
            OSPlatform.Windows,
            Architecture.X64);

        Assert.StartsWith(packageRoot, plan.GuiProjectPath, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(runtimeRoot, plan.RuntimeRoot);
        Assert.False(plan.RuntimeRoot.StartsWith(packageRoot, StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(Path.Combine("Assets", "YokiFrame", "Assets", "YokiFrame"), plan.RuntimeRoot, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 验证缺少 Workbench GUI 源码项目时拒绝创建不可执行的发布计划。
    /// </summary>
    [Fact]
    public void BuildRejectsPackageWithoutGuiProject()
    {
        var packageRoot = CreatePackageRoot();
        var runtimeRoot = CreateExternalRuntimeRoot();
        File.Delete(Path.Combine(
            packageRoot,
            "YokiFrameWorkbench~",
            "src",
            "YokiFrame.Workbench.Avalonia",
            "YokiFrame.Workbench.Avalonia.csproj"));

        var exception = Assert.Throws<FileNotFoundException>(() => new RuntimePublishPlanBuilder().Build(
            packageRoot,
            runtimeRoot,
            "Release",
            OSPlatform.Linux,
            Architecture.X64));

        Assert.Contains("YokiFrame.Workbench.Avalonia.csproj", exception.FileName, StringComparison.Ordinal);
    }

    /// <summary>
    /// 验证维护发布可以通过受控 profile 标识创建计划，而不把任意 RID 直接拼接到输出路径。
    /// </summary>
    [Fact]
    public void BuildExposesAllowlistedProfilePlanEntryPoint()
    {
        var method = typeof(RuntimePublishPlanBuilder).GetMethod(
            nameof(RuntimePublishPlanBuilder.Build),
            new[] { typeof(string), typeof(string), typeof(string), typeof(string), typeof(bool) });

        Assert.NotNull(method);
    }

    /// <summary>
    /// 验证发布计划拒绝把 Runtime 输出重新写回只读源码包，避免 Git URL 或 embedded package 被构建产物污染。
    /// </summary>
    [Fact]
    public void BuildRejectsRuntimeRootInsidePackage()
    {
        var packageRoot = CreatePackageRoot();
        var runtimeRoot = Path.Combine(packageRoot, "WorkbenchRuntime~");

        Assert.Throws<ArgumentException>(() => new RuntimePublishPlanBuilder().Build(
            packageRoot,
            runtimeRoot,
            "Release",
            OSPlatform.Windows,
            Architecture.X64));
    }

    /// <summary>
    /// 创建包含 GUI 与 CLI 最小项目占位文件的独立 YokiFrame 包根。
    /// </summary>
    /// <returns>不依赖 `Assets/YokiFrame` 祖先目录的包根绝对路径。</returns>
    private static string CreatePackageRoot()
    {
        var packageRoot = Path.Combine(
            Path.GetTempPath(),
            "yokiframe-packaging-tests",
            "standalone-package-" + Guid.NewGuid().ToString("N"));
        WriteProject(packageRoot, "YokiFrame.Workbench.Avalonia");
        WriteProject(packageRoot, "YokiFrame.Cli");
        return Path.GetFullPath(packageRoot);
    }

    /// <summary>
    /// 创建不位于源码包内的项目级 Runtime 缓存根，模拟 `.yokiframe/runtime` 指纹目录。
    /// </summary>
    /// <returns>可供发布计划使用的包外目录。</returns>
    private static string CreateExternalRuntimeRoot()
    {
        return Path.Combine(
            Path.GetTempPath(),
            "yokiframe-packaging-tests",
            "project-runtime-" + Guid.NewGuid().ToString("N"),
            ".yokiframe",
            "runtime",
            "com.hinatayoki.yokiframe",
            "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef");
    }

    /// <summary>
    /// 写入发布计划路径校验所需的最小项目占位文件。
    /// </summary>
    /// <param name="packageRoot">测试包根。</param>
    /// <param name="projectName">项目名。</param>
    private static void WriteProject(string packageRoot, string projectName)
    {
        var projectPath = Path.Combine(
            packageRoot,
            "YokiFrameWorkbench~",
            "src",
            projectName,
            projectName + ".csproj");
        Directory.CreateDirectory(Path.GetDirectoryName(projectPath)!);
        File.WriteAllText(projectPath, "<Project />");
    }
}
