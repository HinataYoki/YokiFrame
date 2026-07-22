using System.Security.Cryptography;
using YokiFrame.Installer.Core.Models;

namespace YokiFrame.Installer.Core.Services;

/// <summary>
/// 为 Unity embedded 安装生成保留 Unity meta 的确定性文件级包投影。
/// </summary>
public sealed class UnityPackageProjectionBuilder
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
        "WorkbenchRuntime~"
    };

    private static readonly HashSet<string> sDroppedKitNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "BuffKit",
        "InputKit"
    };

    private readonly SourcePackageResolver mSourceResolver = new();

    /// <summary>
    /// 扫描源包并生成稳定的文件、长度和 SHA-256 清单，不修改源包或目标项目。
    /// </summary>
    /// <param name="sourcePackageRoot">YokiFrame 源包根。</param>
    /// <param name="targetRuntimeProfile">已验证的项目缓存 Runtime profile，用于绑定安装计划而不进入投影。</param>
    /// <returns>可交给包安装事务提交的确定性投影。</returns>
    public PackageProjection Build(string sourcePackageRoot, string targetRuntimeProfile)
    {
        var source = mSourceResolver.Resolve(sourcePackageRoot);
        ValidateRuntimeProfile(targetRuntimeProfile);
        List<PackageProjectionFile> files = new();
        foreach (var sourcePath in EnumerateSourceFiles(source.PackageRoot))
        {
            var relativePath = NormalizeRelativePath(Path.GetRelativePath(source.PackageRoot, sourcePath));
            if (ShouldInclude(relativePath))
            {
                files.Add(CreateProjectionFile(sourcePath, relativePath));
            }
        }

        files.Sort(static (left, right) => StringComparer.OrdinalIgnoreCase.Compare(left.RelativePath, right.RelativePath));
        return new PackageProjection(source.PackageRoot, targetRuntimeProfile, files);
    }

    /// <summary>
    /// 枚举源包普通文件并跳过重解析点，防止链接把投影带出包根。
    /// </summary>
    /// <param name="packageRoot">已验证的源包根。</param>
    /// <returns>源包内普通文件序列。</returns>
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
    /// 判断文件是否属于 Unity 可交付包，并保留非排除目录中的 Unity meta。
    /// </summary>
    /// <param name="relativePath">使用正斜杠的包相对路径。</param>
    /// <returns>文件应进入 embedded 投影时返回 true。</returns>
    private static bool ShouldInclude(string relativePath)
    {
        if (string.Equals(
                relativePath,
                PackageOwnerManifestStore.MANIFEST_FILE_NAME,
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var segments = relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (IsExcludedDocumentationContent(segments)
            || IsDocumentationMeta(relativePath, segments)
            || segments.Any(static segment => IsExcludedDirectoryName(segment))
            || segments.Any(static segment => sDroppedKitNames.Contains(segment))
            || IsExcludedDirectoryMeta(segments))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// 仅保留 Workbench 可公开浏览的 API 与 Guides 文档，避免架构、审查和开发资料进入 Unity embedded 包。
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

    /// <summary>
    /// Documentation~ 不参与 Unity 导入，因此不把该目录及其 Markdown 的 meta 文件带入 embedded 包。
    /// </summary>
    /// <param name="relativePath">使用正斜杠的包相对路径。</param>
    /// <param name="segments">包相对路径片段。</param>
    /// <returns>路径属于公开文档目录的 meta 文件时返回 true。</returns>
    private static bool IsDocumentationMeta(string relativePath, IReadOnlyList<string> segments)
    {
        if (!relativePath.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return string.Equals(
                   relativePath,
                   DOCUMENTATION_DIRECTORY + ".meta",
                   StringComparison.OrdinalIgnoreCase)
               || (segments.Count > 0
                   && string.Equals(segments[0], DOCUMENTATION_DIRECTORY, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 排除 Tests、工具源码和废弃 Kit 目录对应的同级 meta 文件。
    /// </summary>
    /// <param name="segments">包相对路径片段。</param>
    /// <returns>文件是被排除目录的 meta 时返回 true。</returns>
    private static bool IsExcludedDirectoryMeta(IReadOnlyList<string> segments)
    {
        if (segments.Count == 0 || !segments[^1].EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var directoryName = segments[^1][..^5];
        return IsExcludedDirectoryName(directoryName) || sDroppedKitNames.Contains(directoryName);
    }

    /// <summary>
    /// 读取源文件并创建稳定的长度和 SHA-256 投影记录。
    /// </summary>
    /// <param name="sourcePath">源文件完整路径。</param>
    /// <param name="relativePath">包相对路径。</param>
    /// <returns>文件级投影记录。</returns>
    private static PackageProjectionFile CreateProjectionFile(string sourcePath, string relativePath)
    {
        using var stream = File.OpenRead(sourcePath);
        var hash = SHA256.HashData(stream);
        return new PackageProjectionFile(
            sourcePath,
            relativePath,
            Convert.ToHexString(hash).ToLowerInvariant(),
            stream.Length);
    }

    /// <summary>
    /// 验证 Runtime profile 是单个安全目录名，防止路径穿越进入筛选逻辑。
    /// </summary>
    /// <param name="runtimeProfile">待验证的 profile。</param>
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
    /// 将平台目录分隔符统一为 owner manifest 使用的正斜杠。
    /// </summary>
    /// <param name="relativePath">平台相关相对路径。</param>
    /// <returns>使用正斜杠的相对路径。</returns>
    private static string NormalizeRelativePath(string relativePath)
    {
        return relativePath.Replace('\\', '/');
    }
}
