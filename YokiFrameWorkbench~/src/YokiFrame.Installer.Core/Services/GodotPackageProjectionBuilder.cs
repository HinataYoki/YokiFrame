using System.Security.Cryptography;
using YokiFrame.Installer.Core.IO;
using YokiFrame.Installer.Core.Models;

namespace YokiFrame.Installer.Core.Services;

/// <summary>
/// 为 Godot 本地安装生成文件级受控投影，避免递归复制工具源码、测试和非目标运行产物。
/// </summary>
public sealed class GodotPackageProjectionBuilder
{
    private const string DOCUMENTATION_DIRECTORY = "Documentation~";
    private const string PUBLIC_API_DOCUMENTATION_DIRECTORY = "Api";
    private const string PUBLIC_GUIDES_DOCUMENTATION_DIRECTORY = "Guides";

    private static readonly HashSet<string> sExcludedDirectoryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git",
        ".idea",
        ".vs",
        "bin",
        "Library",
        "Logs",
        "obj",
        "Temp",
        "Tests",
        "WorkbenchRuntime~",
        "YokiFrameWorkbench~"
    };

    private static readonly HashSet<string> sDroppedKitNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "BuffKit",
        "InputKit"
    };

    /// <summary>
    /// 扫描源包并生成稳定文件清单；不会写入源包或目标项目。
    /// </summary>
    /// <param name="sourcePackageRoot">源 YokiFrame 包根。</param>
    /// <param name="targetRuntimeProfile">已验证的项目缓存 Runtime profile，用于绑定安装计划而不进入投影。</param>
    /// <returns>包含路径、长度和 SHA-256 的确定性投影。</returns>
    public PackageProjection Build(string sourcePackageRoot, string targetRuntimeProfile)
    {
        var packageRoot = InstallerPathGuard.RequireFullPath(sourcePackageRoot, nameof(sourcePackageRoot));
        if (!Directory.Exists(packageRoot))
        {
            throw new DirectoryNotFoundException("YokiFrame package root was not found: " + packageRoot);
        }

        ValidateRuntimeProfile(targetRuntimeProfile);
        List<PackageProjectionFile> files = new();
        foreach (var sourcePath in EnumerateSourceFiles(packageRoot))
        {
            var relativePath = NormalizeRelativePath(Path.GetRelativePath(packageRoot, sourcePath));
            if (ShouldInclude(relativePath))
            {
                files.Add(CreateProjectionFile(sourcePath, relativePath));
            }
        }

        files.Sort(static (left, right) => StringComparer.OrdinalIgnoreCase.Compare(left.RelativePath, right.RelativePath));
        return new PackageProjection(packageRoot, targetRuntimeProfile, files);
    }

    /// <summary>
    /// 枚举源包普通文件并跳过重解析点，防止符号链接把安装投影带出包根。
    /// </summary>
    /// <param name="packageRoot">源包根绝对路径。</param>
    /// <returns>源包内可读取文件序列。</returns>
    private static IEnumerable<string> EnumerateSourceFiles(string packageRoot)
    {
        EnumerationOptions options = new()
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = false,
            AttributesToSkip = FileAttributes.ReparsePoint
        };
        return Directory.EnumerateFiles(packageRoot, "*", options);
    }

    /// <summary>
    /// 判断相对路径是否属于 Godot 可交付投影。
    /// </summary>
    /// <param name="relativePath">使用正斜杠的包相对路径。</param>
    /// <returns>应进入投影时返回 true。</returns>
    private static bool ShouldInclude(string relativePath)
    {
        if (relativePath.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)
            || relativePath.EndsWith(".uid", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var segments = relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (IsExcludedDocumentationContent(segments)
            || segments.Any(static segment => IsExcludedDirectoryName(segment))
            || segments.Any(static segment => sDroppedKitNames.Contains(segment))
            || IsExcludedEngineContent(segments))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// 仅保留 Workbench 可公开浏览的 API 与 Guides 文档，避免架构、审查和开发资料进入 Godot 交付包。
    /// </summary>
    /// <param name="segments">包相对路径片段。</param>
    /// <returns>路径属于应排除的文档内容时返回 true。</returns>
    private static bool IsExcludedDocumentationContent(IReadOnlyList<string> segments)
    {
        if (segments.Count == 0
            || !string.Equals(segments[0], DOCUMENTATION_DIRECTORY, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return segments.Count < 2
            || (!string.Equals(segments[1], PUBLIC_API_DOCUMENTATION_DIRECTORY, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(segments[1], PUBLIC_GUIDES_DOCUMENTATION_DIRECTORY, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 判断目录是否属于安装投影必须排除的生成缓存或开发目录。
    /// </summary>
    /// <param name="directoryName">单个相对路径片段。</param>
    /// <returns>目录不应进入发布投影时返回 true。</returns>
    private static bool IsExcludedDirectoryName(string directoryName)
    {
        return sExcludedDirectoryNames.Contains(directoryName)
            || directoryName.StartsWith(".artifacts", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>排除仅属于 Unity 的 Adapter/Integration，同时保留共享目录名中的普通 Unity 文本。</summary>
    private static bool IsExcludedEngineContent(IReadOnlyList<string> segments)
    {
        for (var index = 1; index < segments.Count; index++)
        {
            if (!string.Equals(segments[index], "Unity", StringComparison.OrdinalIgnoreCase)) continue;
            string parent = segments[index - 1];
            if (string.Equals(parent, "Adapters", StringComparison.OrdinalIgnoreCase)
                || string.Equals(parent, "Integrations", StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    /// <summary>
    /// 创建包含稳定内容摘要的投影文件记录。
    /// </summary>
    /// <param name="sourcePath">源文件绝对路径。</param>
    /// <param name="relativePath">包相对路径。</param>
    /// <returns>投影文件记录。</returns>
    private static PackageProjectionFile CreateProjectionFile(string sourcePath, string relativePath)
    {
        using var stream = File.OpenRead(sourcePath);
        var hash = SHA256.HashData(stream);
        return new PackageProjectionFile(sourcePath, relativePath, Convert.ToHexString(hash).ToLowerInvariant(), stream.Length);
    }

    /// <summary>
    /// 验证 Runtime profile 是单个安全目录名，阻止路径穿越进入投影选择逻辑。
    /// </summary>
    /// <param name="runtimeProfile">待验证 profile。</param>
    private static void ValidateRuntimeProfile(string runtimeProfile)
    {
        if (string.IsNullOrWhiteSpace(runtimeProfile)
            || runtimeProfile is "." or ".."
            || runtimeProfile.IndexOfAny(new[] { '/', '\\' }) >= 0)
        {
            throw new ArgumentException("Runtime profile must be a single safe directory name.", nameof(runtimeProfile));
        }
    }

    /// <summary>
    /// 统一包相对路径分隔符，供清单、hash 和跨平台测试稳定使用。
    /// </summary>
    /// <param name="relativePath">平台相关相对路径。</param>
    /// <returns>使用正斜杠的相对路径。</returns>
    private static string NormalizeRelativePath(string relativePath)
    {
        return relativePath.Replace('\\', '/');
    }
}
