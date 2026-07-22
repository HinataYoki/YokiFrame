using System.Reflection;

namespace YokiFrame.Workbench.Avalonia.Tests;

/// <summary>
/// 验证 Workbench 与 Installer 启动诊断的目录所有权。
/// </summary>
public sealed class WorkbenchStartupTraceTests
{
    /// <summary>
    /// 验证 Workbench 诊断仍写入对应项目的 `.yokiframe`，方便项目级冷启动排查。
    /// </summary>
    [Fact]
    public void WorkbenchTraceRootBelongsToProject()
    {
        var projectRoot = CreatePath("project");
        ToolStartupOptions options = new(
            ToolStartupMode.Workbench,
            projectRoot,
            Path.Combine(projectRoot, "Assets", "YokiFrame"),
            projectRoot);

        var traceRoot = ResolveTraceRoot(options);

        Assert.Equal(Path.Combine(projectRoot, ".yokiframe", "workbench"), traceRoot);
    }

    /// <summary>
    /// 验证 Installer 诊断进入用户本地数据目录，禁止污染项目级 Runtime 缓存。
    /// </summary>
    [Fact]
    public void InstallerTraceRootDoesNotBelongToProjectRuntimeCache()
    {
        var runtimeRoot = CreatePath(".yokiframe", "runtime", "com.hinatayoki.yokiframe", "fingerprint");
        ToolStartupOptions options = new(
            ToolStartupMode.Installer,
            runtimeRoot,
            CreatePath("package"),
            runtimeRoot);

        var traceRoot = ResolveTraceRoot(options);

        Assert.False(
            Path.GetFullPath(traceRoot).StartsWith(
                Path.GetFullPath(runtimeRoot) + Path.DirectorySeparatorChar,
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal));
        Assert.EndsWith(
            Path.Combine("YokiFrame", "Workbench", "startup"),
            traceRoot,
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }

    /// <summary>
    /// 反射调用诊断路径解析方法，使缺失行为以明确断言失败而不是测试编译失败。
    /// </summary>
    /// <param name="options">已解析的工具启动选项。</param>
    /// <returns>当前模式应使用的诊断目录。</returns>
    private static string ResolveTraceRoot(ToolStartupOptions options)
    {
        var type = typeof(WorkbenchWindow).Assembly.GetType(
            "YokiFrame.Workbench.Avalonia.Diagnostics.WorkbenchStartupTrace");
        Assert.NotNull(type);
        var method = type.GetMethod("ResolveTraceRoot", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return (string)method.Invoke(null, new object[] { options })!;
    }

    /// <summary>
    /// 生成无需落盘的唯一绝对路径，隔离并行测试的路径身份。
    /// </summary>
    /// <param name="segments">测试路径语义片段。</param>
    /// <returns>位于系统临时目录下的绝对路径。</returns>
    private static string CreatePath(params string[] segments)
    {
        return Path.Combine(
            new[] { Path.GetTempPath(), "yokiframe-startup-trace-tests", Guid.NewGuid().ToString("N") }
                .Concat(segments)
                .ToArray());
    }
}
