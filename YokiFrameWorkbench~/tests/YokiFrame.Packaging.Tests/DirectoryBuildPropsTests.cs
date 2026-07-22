using System.Diagnostics;
using System.Text.Json;

namespace YokiFrame.Packaging.Tests;

/// <summary>
/// 覆盖 Workbench 工具链在独立 YokiFrame 包根中的真实 MSBuild 输出路径计算。
/// </summary>
public sealed class DirectoryBuildPropsTests
{
    private const string PROJECT_NAME = "Standalone.Build.Probe";
    private const string TEST_ROOT_NAME = "yokiframe-directory-build-props-tests";

    /// <summary>
    /// 验证默认构建缓存分别留在各自工具链目录内，不依赖 Unity 工程的固定祖先层级。
    /// </summary>
    [Fact]
    public async Task DefaultBuildPathsStayInsideEachStandalonePackage()
    {
        var firstPackageRoot = CreateStandalonePackageRoot();
        var secondPackageRoot = CreateStandalonePackageRoot();

        try
        {
            var firstPaths = await EvaluateBuildPathsAsync(firstPackageRoot);
            var secondPaths = await EvaluateBuildPathsAsync(secondPackageRoot);

            AssertBuildPathsUseRoot(firstPaths, GetDefaultBuildRoot(firstPackageRoot));
            AssertBuildPathsUseRoot(secondPaths, GetDefaultBuildRoot(secondPackageRoot));
            Assert.False(PathsEqual(firstPaths.BaseOutputPath, secondPaths.BaseOutputPath));
            Assert.False(PathsEqual(firstPaths.BaseIntermediateOutputPath, secondPaths.BaseIntermediateOutputPath));
        }
        finally
        {
            DeletePackageRoot(firstPackageRoot);
            DeletePackageRoot(secondPackageRoot);
        }
    }

    /// <summary>
    /// 验证调用方可以显式覆盖构建根，同时让 bin 与 obj 保持项目级隔离。
    /// </summary>
    [Fact]
    public async Task ExplicitBuildRootOverrideControlsBothOutputRoots()
    {
        var packageRoot = CreateStandalonePackageRoot();
        var customBuildRoot = Path.Combine(packageRoot, "custom-build-cache");

        try
        {
            var paths = await EvaluateBuildPathsAsync(packageRoot, customBuildRoot);

            AssertBuildPathsUseRoot(paths, customBuildRoot);
        }
        finally
        {
            DeletePackageRoot(packageRoot);
        }
    }

    /// <summary>
    /// 创建包含真实 Directory.Build.props 副本与最小 SDK 项目的独立包根。
    /// </summary>
    /// <returns>位于系统临时目录中的独立 YokiFrame 包根。</returns>
    private static string CreateStandalonePackageRoot()
    {
        var packageRoot = Path.Combine(
            Path.GetTempPath(),
            TEST_ROOT_NAME,
            "standalone-package-" + Guid.NewGuid().ToString("N"));
        var workbenchRoot = Path.Combine(packageRoot, "YokiFrameWorkbench~");
        var projectRoot = Path.Combine(workbenchRoot, "src", PROJECT_NAME);
        Directory.CreateDirectory(projectRoot);
        File.Copy(FindAuthoritativePropsPath(), Path.Combine(workbenchRoot, "Directory.Build.props"));
        File.WriteAllText(
            Path.Combine(projectRoot, PROJECT_NAME + ".csproj"),
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
        return Path.GetFullPath(packageRoot);
    }

    /// <summary>
    /// 通过真实 dotnet msbuild 评估复制后的 props，并读取最终输出属性。
    /// </summary>
    /// <param name="packageRoot">独立 YokiFrame 包根。</param>
    /// <param name="buildRootOverride">可选的工具链构建根覆盖值。</param>
    /// <returns>规范化后的 bin 与 obj 基础路径。</returns>
    private static async Task<BuildPaths> EvaluateBuildPathsAsync(
        string packageRoot,
        string? buildRootOverride = null)
    {
        var projectPath = Path.Combine(
            packageRoot,
            "YokiFrameWorkbench~",
            "src",
            PROJECT_NAME,
            PROJECT_NAME + ".csproj");
        ProcessStartInfo startInfo = new("dotnet")
        {
            WorkingDirectory = packageRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add("msbuild");
        startInfo.ArgumentList.Add(projectPath);
        startInfo.ArgumentList.Add("--nologo");
        startInfo.ArgumentList.Add("-getProperty:BaseOutputPath,BaseIntermediateOutputPath");
        if (buildRootOverride != null)
        {
            startInfo.ArgumentList.Add("-p:YokiFrameWorkbenchBuildRoot=" + buildRootOverride);
        }

        return await RunMsBuildAsync(startInfo, Path.GetDirectoryName(projectPath)!);
    }

    /// <summary>
    /// 执行 MSBuild 子进程并把 JSON 属性结果转换为可比较路径。
    /// </summary>
    /// <param name="startInfo">已经配置完成的 MSBuild 进程参数。</param>
    /// <param name="projectRoot">用于解析相对属性值的项目目录。</param>
    /// <returns>规范化后的构建路径。</returns>
    private static async Task<BuildPaths> RunMsBuildAsync(ProcessStartInfo startInfo, string projectRoot)
    {
        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("无法启动 dotnet msbuild 进程。");
        var standardOutputTask = process.StandardOutput.ReadToEndAsync();
        var standardErrorTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        var standardOutput = await standardOutputTask;
        var standardError = await standardErrorTask;
        Assert.True(process.ExitCode == 0, standardError + Environment.NewLine + standardOutput);

        using var document = JsonDocument.Parse(standardOutput);
        var properties = document.RootElement.GetProperty("Properties");
        return new BuildPaths(
            NormalizePath(properties.GetProperty("BaseOutputPath").GetString()!, projectRoot),
            NormalizePath(properties.GetProperty("BaseIntermediateOutputPath").GetString()!, projectRoot));
    }

    /// <summary>
    /// 断言 MSBuild 的 bin 与 obj 根均由指定构建根和项目名组成。
    /// </summary>
    /// <param name="paths">MSBuild 返回的实际路径。</param>
    /// <param name="expectedBuildRoot">期望使用的构建根。</param>
    private static void AssertBuildPathsUseRoot(BuildPaths paths, string expectedBuildRoot)
    {
        AssertPathEqual(
            Path.Combine(expectedBuildRoot, "bin", PROJECT_NAME),
            paths.BaseOutputPath);
        AssertPathEqual(
            Path.Combine(expectedBuildRoot, "obj", PROJECT_NAME),
            paths.BaseIntermediateOutputPath);
    }

    /// <summary>
    /// 返回独立包默认应使用的 Workbench 生成缓存根。
    /// </summary>
    /// <param name="packageRoot">独立 YokiFrame 包根。</param>
    /// <returns>位于 YokiFrameWorkbench~ 内的生成缓存目录。</returns>
    private static string GetDefaultBuildRoot(string packageRoot)
    {
        return Path.Combine(packageRoot, "YokiFrameWorkbench~", ".artifacts");
    }

    /// <summary>
    /// 从测试工作目录或程序集目录向上定位当前工具链的权威 props 文件。
    /// </summary>
    /// <returns>当前仓库中的 Directory.Build.props 绝对路径。</returns>
    private static string FindAuthoritativePropsPath()
    {
        var path = FindAuthoritativePropsPathFrom(Directory.GetCurrentDirectory())
            ?? FindAuthoritativePropsPathFrom(AppContext.BaseDirectory);
        return path ?? throw new FileNotFoundException("无法定位 YokiFrameWorkbench~/Directory.Build.props。");
    }

    /// <summary>
    /// 从指定目录向上兼容查找独立包根或 Unity 工程内的 Workbench props。
    /// </summary>
    /// <param name="startPath">向上查找的起始目录。</param>
    /// <returns>找到的 props 路径；未找到时返回 null。</returns>
    private static string? FindAuthoritativePropsPathFrom(string startPath)
    {
        var current = new DirectoryInfo(startPath);
        while (current != null)
        {
            var directPath = Path.Combine(current.FullName, "Directory.Build.props");
            if (current.Name == "YokiFrameWorkbench~" && File.Exists(directPath))
            {
                return directPath;
            }

            var unityPackagePath = Path.Combine(
                current.FullName,
                "Assets",
                "YokiFrame",
                "YokiFrameWorkbench~",
                "Directory.Build.props");
            if (File.Exists(unityPackagePath))
            {
                return unityPackagePath;
            }

            current = current.Parent;
        }

        return null;
    }

    /// <summary>
    /// 将 MSBuild 返回路径按项目目录解析并移除尾部分隔符，便于跨平台比较。
    /// </summary>
    /// <param name="path">MSBuild 属性中的路径。</param>
    /// <param name="projectRoot">相对路径的解析基准。</param>
    /// <returns>规范化后的绝对路径。</returns>
    private static string NormalizePath(string path, string projectRoot)
    {
        var fullPath = Path.IsPathRooted(path)
            ? Path.GetFullPath(path)
            : Path.GetFullPath(path, projectRoot);
        return Path.TrimEndingDirectorySeparator(fullPath);
    }

    /// <summary>
    /// 使用当前操作系统的路径大小写规则断言两个路径相同。
    /// </summary>
    /// <param name="expected">期望路径。</param>
    /// <param name="actual">实际路径。</param>
    private static void AssertPathEqual(string expected, string actual)
    {
        var normalizedExpected = Path.TrimEndingDirectorySeparator(Path.GetFullPath(expected));
        Assert.True(
            PathsEqual(normalizedExpected, actual),
            $"路径不一致。期望: {normalizedExpected}{Environment.NewLine}实际: {actual}");
    }

    /// <summary>
    /// 按当前操作系统的文件系统约定比较两个绝对路径。
    /// </summary>
    /// <param name="left">左侧路径。</param>
    /// <param name="right">右侧路径。</param>
    /// <returns>路径相同时返回 true。</returns>
    private static bool PathsEqual(string left, string right)
    {
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        return string.Equals(left, right, comparison);
    }

    /// <summary>
    /// 删除测试创建的独立包根，避免临时构建探针累积。
    /// </summary>
    /// <param name="packageRoot">由当前测试创建的包根。</param>
    private static void DeletePackageRoot(string packageRoot)
    {
        if (Directory.Exists(packageRoot))
        {
            Directory.Delete(packageRoot, true);
        }
    }

    /// <summary>
    /// 表示 MSBuild 评估得到的两个基础输出路径。
    /// </summary>
    /// <param name="BaseOutputPath">最终输出基础路径。</param>
    /// <param name="BaseIntermediateOutputPath">中间输出基础路径。</param>
    private sealed record BuildPaths(string BaseOutputPath, string BaseIntermediateOutputPath);
}
