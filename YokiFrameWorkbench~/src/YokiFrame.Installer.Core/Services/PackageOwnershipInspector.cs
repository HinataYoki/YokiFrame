using System.Security.Cryptography;
using YokiFrame.Installer.Core.IO;
using YokiFrame.Installer.Core.Models;

namespace YokiFrame.Installer.Core.Services;

/// <summary>
/// 对照 owner manifest 检查受管包的缺失、内容变化和额外文件。
/// </summary>
public sealed class PackageOwnershipInspector
{
    private const string WORKBENCH_RUNTIME_PREFIX = "WorkbenchRuntime~/";
    private const string WORKBENCH_SOURCE_PREFIX = "YokiFrameWorkbench~/";
    private const string WORKBENCH_ARTIFACTS_DIRECTORY_PREFIX = ".artifacts";
    private const string LEGACY_WEBVIEW2_CACHE_PREFIX = "YokiFrame.Workbench.Avalonia.exe.WebView2/";

    private readonly PackageOwnerManifestStore mManifestStore = new();

    /// <summary>
    /// 检查目标包所有权状态；只读取文件，不修改目标目录。
    /// </summary>
    /// <param name="packageRoot">目标受管包根目录。</param>
    /// <returns>所有权状态和稳定冲突路径。</returns>
    public PackageOwnershipInspection Inspect(string packageRoot)
    {
        var fullPackageRoot = InstallerPathGuard.RequireFullPath(packageRoot, nameof(packageRoot));
        if (!Directory.Exists(fullPackageRoot))
        {
            return new PackageOwnershipInspection(PackageOwnershipState.NotInstalled, Array.Empty<string>());
        }

        var manifestPath = mManifestStore.GetManifestPath(fullPackageRoot);
        if (!File.Exists(manifestPath))
        {
            return new PackageOwnershipInspection(PackageOwnershipState.UnmanagedLegacy, Array.Empty<string>());
        }

        var manifest = mManifestStore.Read(fullPackageRoot);
        var conflicts = FindConflicts(fullPackageRoot, manifest, manifestPath);
        var state = conflicts.Count == 0 ? PackageOwnershipState.Clean : PackageOwnershipState.Modified;
        return new PackageOwnershipInspection(state, conflicts);
    }

    /// <summary>
    /// 比较 manifest 文件与磁盘实际文件，并收集缺失、变化和额外路径。
    /// </summary>
    /// <param name="packageRoot">受管包根目录。</param>
    /// <param name="manifest">已读取 owner manifest。</param>
    /// <param name="manifestPath">manifest 自身路径，扫描时需排除。</param>
    /// <returns>稳定排序的冲突相对路径。</returns>
    private static IReadOnlyList<string> FindConflicts(
        string packageRoot,
        PackageOwnerManifest manifest,
        string manifestPath)
    {
        HashSet<string> conflicts = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, PackageOwnerFile> expected = new(StringComparer.OrdinalIgnoreCase);
        foreach (var file in manifest.Files)
        {
            if (!expected.TryAdd(file.RelativePath, file))
            {
                throw new InvalidDataException("Package owner manifest contains a duplicate path: " + file.RelativePath);
            }

            if (!MatchesExpectedFile(packageRoot, file))
            {
                conflicts.Add(file.RelativePath);
            }
        }

        AddUnexpectedFiles(packageRoot, manifestPath, expected, conflicts);
        return conflicts.OrderBy(static path => path, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    /// <summary>
    /// 检查受管文件是否存在且长度、SHA-256 与 manifest 一致。
    /// </summary>
    /// <param name="packageRoot">受管包根目录。</param>
    /// <param name="expected">manifest 文件记录。</param>
    /// <returns>完全一致时返回 true。</returns>
    private static bool MatchesExpectedFile(string packageRoot, PackageOwnerFile expected)
    {
        var targetPath = InstallerPathGuard.CombineInside(
            packageRoot,
            expected.RelativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(targetPath))
        {
            return false;
        }

        FileInfo info = new(targetPath);
        if (info.Length != expected.Length)
        {
            return false;
        }

        using var stream = File.OpenRead(targetPath);
        var hash = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        return string.Equals(hash, expected.Sha256, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 扫描 manifest 未声明的普通文件，并跳过重解析点与 manifest 自身。
    /// </summary>
    /// <param name="packageRoot">受管包根目录。</param>
    /// <param name="manifestPath">manifest 自身路径。</param>
    /// <param name="expected">manifest 文件索引。</param>
    /// <param name="conflicts">冲突路径收集目标。</param>
    private static void AddUnexpectedFiles(
        string packageRoot,
        string manifestPath,
        IReadOnlyDictionary<string, PackageOwnerFile> expected,
        ISet<string> conflicts)
    {
        EnumerationOptions options = new()
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = false,
            AttributesToSkip = FileAttributes.ReparsePoint
        };
        foreach (var path in Directory.EnumerateFiles(packageRoot, "*", options))
        {
            if (Path.GetFullPath(path).Equals(Path.GetFullPath(manifestPath), StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var relativePath = Path.GetRelativePath(packageRoot, path).Replace('\\', '/');
            if (!expected.ContainsKey(relativePath)
                && !IsLegacyWorkbenchWebView2Cache(relativePath)
                && !IsWorkbenchBuildArtifact(relativePath))
            {
                conflicts.Add(relativePath);
            }
        }
    }

    /// <summary>
    /// 判断路径是否为 Workbench 源码目录下可再生的构建缓存；这些文件由 `dotnet run` bootstrap 产生，
    /// 不属于用户对 embedded package 的修改，下一次安装投影会安全丢弃并按需重新生成。
    /// </summary>
    /// <param name="relativePath">已标准化为正斜杠的包相对路径。</param>
    /// <returns>仅当文件位于 `YokiFrameWorkbench~/.artifacts*` 顶层缓存目录时返回 true。</returns>
    private static bool IsWorkbenchBuildArtifact(string relativePath)
    {
        if (!relativePath.StartsWith(WORKBENCH_SOURCE_PREFIX, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var workbenchRelativePath = relativePath.Substring(WORKBENCH_SOURCE_PREFIX.Length);
        var directorySeparator = workbenchRelativePath.IndexOf('/');
        if (directorySeparator <= 0)
        {
            return false;
        }

        var topLevelDirectory = workbenchRelativePath.Substring(0, directorySeparator);
        return topLevelDirectory.StartsWith(WORKBENCH_ARTIFACTS_DIRECTORY_PREFIX, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 判断路径是否为旧版 Workbench 在受管 Windows Runtime 目录旁错误生成的 WebView2 缓存。
    /// 该目录只承载浏览器临时状态，更新时可安全替换；任意其他未声明文件仍必须作为用户修改报告。
    /// </summary>
    /// <param name="relativePath">已标准化为正斜杠的包相对路径。</param>
    /// <returns>仅精确匹配旧 WebView2 缓存布局时返回 true。</returns>
    private static bool IsLegacyWorkbenchWebView2Cache(string relativePath)
    {
        if (!relativePath.StartsWith(WORKBENCH_RUNTIME_PREFIX, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var runtimeRelativePath = relativePath.Substring(WORKBENCH_RUNTIME_PREFIX.Length);
        var profileSeparator = runtimeRelativePath.IndexOf('/');
        if (profileSeparator <= 0)
        {
            return false;
        }

        var profileFilePath = runtimeRelativePath.Substring(profileSeparator + 1);
        return profileFilePath.StartsWith(LEGACY_WEBVIEW2_CACHE_PREFIX, StringComparison.OrdinalIgnoreCase);
    }
}
