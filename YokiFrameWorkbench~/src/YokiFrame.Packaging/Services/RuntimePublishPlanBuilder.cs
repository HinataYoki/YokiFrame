using System.Runtime.InteropServices;
using YokiFrame.Packaging.Models;

namespace YokiFrame.Packaging.Services;

/// <summary>
/// 从任意独立 YokiFrame 包根创建当前平台项目 Runtime 缓存发布计划。
/// </summary>
public sealed class RuntimePublishPlanBuilder
{
    /// <summary>
    /// 创建指定宿主平台的发布路径计划，不执行任何文件写入或外部进程。
    /// </summary>
    /// <param name="packageRoot">YokiFrame 包根。</param>
    /// <param name="runtimeRoot">包外的项目级 Runtime 缓存根。</param>
    /// <param name="configuration">构建配置，例如 Release。</param>
    /// <param name="platform">宿主操作系统。</param>
    /// <param name="architecture">宿主进程架构。</param>
    /// <returns>当前平台发布计划。</returns>
    public RuntimePublishPlan Build(
        string packageRoot,
        string runtimeRoot,
        string configuration,
        OSPlatform platform,
        Architecture architecture)
    {
        return Build(packageRoot, runtimeRoot, configuration, RuntimePublishProfileResolver.Resolve(platform, architecture));
    }

    /// <summary>
    /// 创建当前进程所在平台的发布路径计划。
    /// </summary>
    /// <param name="packageRoot">YokiFrame 包根。</param>
    /// <param name="runtimeRoot">包外的项目级 Runtime 缓存根。</param>
    /// <param name="configuration">构建配置。</param>
    /// <returns>当前平台发布计划。</returns>
    public RuntimePublishPlan BuildCurrent(string packageRoot, string runtimeRoot, string configuration)
    {
        return Build(packageRoot, runtimeRoot, configuration, RuntimePublishProfileResolver.ResolveCurrent());
    }

    /// <summary>
    /// 根据 allowlist profile 标识创建维护发布计划；标识和优化组合由统一 resolver 校验。
    /// </summary>
    /// <param name="packageRoot">YokiFrame 包根。</param>
    /// <param name="runtimeRoot">包外的项目级 Runtime 缓存根。</param>
    /// <param name="configuration">构建配置。</param>
    /// <param name="runtimeIdentifier">受支持的项目 Runtime profile 标识。</param>
    /// <param name="startupOptimized">是否启用 ReadyToRun 启动优化。</param>
    /// <returns>指定 profile 的发布计划。</returns>
    public RuntimePublishPlan Build(
        string packageRoot,
        string runtimeRoot,
        string configuration,
        string runtimeIdentifier,
        bool startupOptimized)
    {
        return Build(
            packageRoot,
            runtimeRoot,
            configuration,
            RuntimePublishProfileResolver.Resolve(runtimeIdentifier, startupOptimized));
    }

    /// <summary>
    /// 根据已解析 profile 生成所有源码与输出路径。
    /// </summary>
    /// <param name="packageRoot">YokiFrame 包根。</param>
    /// <param name="runtimeRoot">包外的项目级 Runtime 缓存根。</param>
    /// <param name="configuration">构建配置。</param>
    /// <param name="profile">当前平台发布 profile。</param>
    /// <returns>当前平台发布计划。</returns>
    private static RuntimePublishPlan Build(
        string packageRoot,
        string runtimeRoot,
        string configuration,
        RuntimePublishProfile profile)
    {
        var fullPackageRoot = RequireDirectory(packageRoot, nameof(packageRoot));
        var fullRuntimeRoot = RequireExternalRuntimeRoot(fullPackageRoot, runtimeRoot);
        if (string.IsNullOrWhiteSpace(configuration))
        {
            throw new ArgumentException("Build configuration is required.", nameof(configuration));
        }

        var workbenchRoot = Path.Combine(fullPackageRoot, "YokiFrameWorkbench~");
        var guiProjectPath = RequireFile(Path.Combine(
            workbenchRoot,
            "src",
            "YokiFrame.Workbench.Avalonia",
            "YokiFrame.Workbench.Avalonia.csproj"));
        var cliProjectPath = RequireFile(Path.Combine(
            workbenchRoot,
            "src",
            "YokiFrame.Cli",
            "YokiFrame.Cli.csproj"));
        var stagingRoot = Path.Combine(fullRuntimeRoot, ".staging", profile.RuntimeIdentifier);
        var publishRoot = Path.Combine(fullRuntimeRoot, profile.RuntimeIdentifier);
        var manifestPath = Path.Combine(fullRuntimeRoot, "tool-manifest.json");
        return new RuntimePublishPlan(
            fullPackageRoot,
            configuration.Trim(),
            profile,
            workbenchRoot,
            guiProjectPath,
            cliProjectPath,
            fullRuntimeRoot,
            stagingRoot,
            publishRoot,
            manifestPath);
    }

    /// <summary>
    /// 校验必需目录并返回规范化完整路径。
    /// </summary>
    /// <param name="path">目录路径。</param>
    /// <param name="parameterName">参数名。</param>
    /// <returns>规范化完整路径。</returns>
    private static string RequireDirectory(string path, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Package root is required.", parameterName);
        }

        var fullPath = Path.GetFullPath(path);
        return Directory.Exists(fullPath)
            ? fullPath
            : throw new DirectoryNotFoundException("YokiFrame package root was not found: " + fullPath);
    }

    /// <summary>
    /// 规范化 Runtime 输出根，并拒绝包根或包内路径，避免 Git URL 与 embedded package 被可再生产物污染。
    /// </summary>
    /// <param name="packageRoot">已验证的 YokiFrame 包根。</param>
    /// <param name="runtimeRoot">调用方提供的 Runtime 输出根。</param>
    /// <returns>包外的规范化 Runtime 输出根。</returns>
    private static string RequireExternalRuntimeRoot(string packageRoot, string runtimeRoot)
    {
        if (string.IsNullOrWhiteSpace(runtimeRoot))
        {
            throw new ArgumentException("Runtime root is required.", nameof(runtimeRoot));
        }

        var fullRuntimeRoot = Path.GetFullPath(runtimeRoot);
        var relativePath = Path.GetRelativePath(packageRoot, fullRuntimeRoot);
        var isInsidePackage = string.Equals(relativePath, ".", StringComparison.Ordinal)
            || (!Path.IsPathRooted(relativePath)
                && relativePath != ".."
                && !relativePath.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                && !relativePath.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal));
        if (isInsidePackage)
        {
            throw new ArgumentException("Runtime root must be outside the YokiFrame package root.", nameof(runtimeRoot));
        }

        return fullRuntimeRoot;
    }

    /// <summary>
    /// 校验必需项目文件并返回原路径。
    /// </summary>
    /// <param name="path">项目文件路径。</param>
    /// <returns>存在的项目文件路径。</returns>
    private static string RequireFile(string path)
    {
        return File.Exists(path)
            ? path
            : throw new FileNotFoundException("Workbench Runtime cache publish project was not found.", path);
    }

}
