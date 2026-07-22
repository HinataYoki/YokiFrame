using YokiFrame.Installer.Core.Models;

namespace YokiFrame.Installer.Core.Services;

/// <summary>
/// 比较当前受管 embedded 包与待安装投影，只在 Unity 程序集图可能变化时请求 Package Manager 刷新。
/// </summary>
internal sealed class UnityEmbeddedPackageGraphChangeDetector
{
    private const string PACKAGE_MANIFEST_FILE_NAME = "package.json";

    private readonly PackageOwnerManifestStore mOwnerManifestStore = new();

    /// <summary>
    /// 判断当前 embedded 包替换后是否需要通过 manifest 唤醒 Unity Package Manager。
    /// 旧包不存在、缺少 owner manifest 或程序集图相关文件发生增删改时返回 true。
    /// </summary>
    /// <param name="packageRoot">当前 Unity embedded 包根目录。</param>
    /// <param name="projection">即将提交的完整文件投影。</param>
    /// <returns>必须刷新 Unity Package Manager 时返回 true。</returns>
    internal bool RequiresPackageManagerRefresh(string packageRoot, PackageProjection projection)
    {
        if (!Directory.Exists(packageRoot))
        {
            return true;
        }

        var ownerManifestPath = mOwnerManifestStore.GetManifestPath(packageRoot);
        if (!File.Exists(ownerManifestPath))
        {
            return true;
        }

        var installed = mOwnerManifestStore.Read(packageRoot);
        return HasPackageGraphChanges(installed.Files, projection.Files);
    }

    /// <summary>
    /// 对比旧 owner manifest 与新投影中会改变 Unity 编译或包解析关系的文件摘要。
    /// </summary>
    /// <param name="installedFiles">当前受管包记录的文件摘要。</param>
    /// <param name="projectedFiles">即将提交的文件摘要。</param>
    /// <returns>相关文件集合或内容不同则返回 true。</returns>
    private static bool HasPackageGraphChanges(
        IReadOnlyList<PackageOwnerFile> installedFiles,
        IReadOnlyList<PackageProjectionFile> projectedFiles)
    {
        Dictionary<string, FileDigest> installedGraphFiles = CreateGraphFileMap(installedFiles);
        Dictionary<string, FileDigest> projectedGraphFiles = CreateGraphFileMap(projectedFiles);
        if (installedGraphFiles.Count != projectedGraphFiles.Count)
        {
            return true;
        }

        foreach (var projectedFile in projectedGraphFiles)
        {
            if (!installedGraphFiles.TryGetValue(projectedFile.Key, out var installedFile)
                || !installedFile.Matches(projectedFile.Value))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 从已安装 owner manifest 提取程序集图相关文件并建立路径索引。
    /// </summary>
    /// <param name="files">当前受管包文件记录。</param>
    /// <returns>按不区分大小写路径索引的文件摘要。</returns>
    private static Dictionary<string, FileDigest> CreateGraphFileMap(IReadOnlyList<PackageOwnerFile> files)
    {
        Dictionary<string, FileDigest> graphFiles = new(StringComparer.OrdinalIgnoreCase);
        foreach (var file in files)
        {
            AddGraphFile(graphFiles, file.RelativePath, file.Sha256, file.Length);
        }

        return graphFiles;
    }

    /// <summary>
    /// 从待安装投影提取程序集图相关文件并建立路径索引。
    /// </summary>
    /// <param name="files">待提交投影文件记录。</param>
    /// <returns>按不区分大小写路径索引的文件摘要。</returns>
    private static Dictionary<string, FileDigest> CreateGraphFileMap(IReadOnlyList<PackageProjectionFile> files)
    {
        Dictionary<string, FileDigest> graphFiles = new(StringComparer.OrdinalIgnoreCase);
        foreach (var file in files)
        {
            AddGraphFile(graphFiles, file.RelativePath, file.Sha256, file.Length);
        }

        return graphFiles;
    }

    /// <summary>
    /// 将程序集图相关文件加入索引，并拒绝会导致后续事务目标冲突的重复路径。
    /// </summary>
    /// <param name="graphFiles">待填充的路径索引。</param>
    /// <param name="relativePath">使用正斜杠的包相对路径。</param>
    /// <param name="sha256">文件内容摘要。</param>
    /// <param name="length">文件长度。</param>
    private static void AddGraphFile(
        IDictionary<string, FileDigest> graphFiles,
        string relativePath,
        string sha256,
        long length)
    {
        if (!IsPackageGraphFile(relativePath))
        {
            return;
        }

        if (!graphFiles.TryAdd(relativePath, new FileDigest(sha256, length)))
        {
            throw new InvalidDataException("Package graph contains a duplicate path: " + relativePath);
        }
    }

    /// <summary>
    /// 判断文件是否可能改变 package 解析、程序集定义、插件加载或编译器参数。
    /// </summary>
    /// <param name="relativePath">使用正斜杠的包相对路径。</param>
    /// <returns>文件需要参与 Package Manager 刷新判断时返回 true。</returns>
    private static bool IsPackageGraphFile(string relativePath)
    {
        return string.Equals(relativePath, PACKAGE_MANIFEST_FILE_NAME, StringComparison.OrdinalIgnoreCase)
            || string.Equals(relativePath, PACKAGE_MANIFEST_FILE_NAME + ".meta", StringComparison.OrdinalIgnoreCase)
            || relativePath.EndsWith(".asmdef", StringComparison.OrdinalIgnoreCase)
            || relativePath.EndsWith(".asmdef.meta", StringComparison.OrdinalIgnoreCase)
            || relativePath.EndsWith(".asmref", StringComparison.OrdinalIgnoreCase)
            || relativePath.EndsWith(".asmref.meta", StringComparison.OrdinalIgnoreCase)
            || relativePath.EndsWith(".rsp", StringComparison.OrdinalIgnoreCase)
            || relativePath.EndsWith(".rsp.meta", StringComparison.OrdinalIgnoreCase)
            || HasPluginsDirectory(relativePath);
    }

    /// <summary>
    /// 判断路径是否位于名为 Plugins 的目录，目录内插件和 importer meta 都可能影响 Unity 编译图。
    /// </summary>
    /// <param name="relativePath">使用正斜杠的包相对路径。</param>
    /// <returns>路径包含精确 Plugins 目录片段时返回 true。</returns>
    private static bool HasPluginsDirectory(string relativePath)
    {
        return relativePath.StartsWith("Plugins/", StringComparison.OrdinalIgnoreCase)
            || relativePath.IndexOf("/Plugins/", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    /// <summary>
    /// 保存比较程序集图文件时需要的内容摘要，避免依赖绝对路径或文件系统时间戳。
    /// </summary>
    private readonly struct FileDigest
    {
        /// <summary>
        /// 创建文件内容摘要。
        /// </summary>
        /// <param name="sha256">文件内容的 SHA-256 摘要。</param>
        /// <param name="length">文件字节长度。</param>
        internal FileDigest(string sha256, long length)
        {
            Sha256 = sha256;
            Length = length;
        }

        /// <summary>
        /// 获取文件内容的 SHA-256 摘要。
        /// </summary>
        private string Sha256 { get; }

        /// <summary>
        /// 获取文件字节长度。
        /// </summary>
        private long Length { get; }

        /// <summary>
        /// 比较两个文件摘要是否指向完全相同的内容。
        /// </summary>
        /// <param name="other">待比较的另一份摘要。</param>
        /// <returns>摘要和长度均一致时返回 true。</returns>
        internal bool Matches(FileDigest other)
        {
            return Length == other.Length
                && string.Equals(Sha256, other.Sha256, StringComparison.OrdinalIgnoreCase);
        }
    }
}
