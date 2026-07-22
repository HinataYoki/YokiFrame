using System.Security.Cryptography;
using YokiFrame.Installer.Core.Models;
using YokiFrame.Installer.Core.Services;

namespace YokiFrame.Installer.Core.Tests.Transactions;

/// <summary>
/// 提供完全位于系统临时目录的包投影、目标项目和事务目录测试夹具。
/// </summary>
internal sealed class PackageInstallTransactionFixture : IDisposable
{
    private const string RUNTIME_PROFILE = "win-x64";

    /// <summary>
    /// 创建事务夹具并建立隔离的源包和目标项目目录。
    /// </summary>
    private PackageInstallTransactionFixture()
    {
        Root = Path.Combine(
            Path.GetTempPath(),
            "yokiframe-install-transaction-tests",
            Guid.NewGuid().ToString("N"));
        SourcePackageRoot = Path.Combine(Root, "source", "YokiFrame");
        ProjectRoot = Path.Combine(Root, "project");
        TargetPackageRoot = Path.Combine(ProjectRoot, "Packages", "com.hinatayoki.yokiframe");
        Directory.CreateDirectory(SourcePackageRoot);
        Directory.CreateDirectory(ProjectRoot);
    }

    /// <summary>
    /// 获取整个测试夹具根目录。
    /// </summary>
    internal string Root { get; }

    /// <summary>
    /// 获取模拟目标项目根目录。
    /// </summary>
    internal string ProjectRoot { get; }

    /// <summary>
    /// 获取模拟源 YokiFrame 包根。
    /// </summary>
    internal string SourcePackageRoot { get; }

    /// <summary>
    /// 获取模拟 Unity embedded 包正式目录。
    /// </summary>
    internal string TargetPackageRoot { get; }

    /// <summary>
    /// 创建新的隔离事务测试夹具。
    /// </summary>
    /// <returns>已创建目录的夹具。</returns>
    internal static PackageInstallTransactionFixture Create()
    {
        return new PackageInstallTransactionFixture();
    }

    /// <summary>
    /// 按给定源路径、投影路径和内容创建带真实 SHA-256 的包投影。
    /// </summary>
    /// <param name="specifications">投影文件规格。</param>
    /// <returns>稳定按相对路径排序的投影。</returns>
    internal PackageProjection CreateProjection(params PackageProjectionSpecification[] specifications)
    {
        List<PackageProjectionFile> files = new();
        foreach (var specification in specifications.OrderBy(
                     static item => item.ProjectionRelativePath,
                     StringComparer.OrdinalIgnoreCase))
        {
            var sourcePath = GetFullPath(SourcePackageRoot, specification.SourceRelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
            File.WriteAllText(sourcePath, specification.Content);
            var content = File.ReadAllBytes(sourcePath);
            files.Add(new PackageProjectionFile(
                sourcePath,
                specification.ProjectionRelativePath,
                Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant(),
                content.LongLength));
        }

        return new PackageProjection(SourcePackageRoot, RUNTIME_PROFILE, files);
    }

    /// <summary>
    /// 在正式目标包中写入一个文件，用于构造 legacy、受管修改或稳定旧版本。
    /// </summary>
    /// <param name="relativePath">目标包内相对路径。</param>
    /// <param name="content">文件内容。</param>
    internal void WriteTargetFile(string relativePath, string content)
    {
        var path = GetTargetPath(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    /// <summary>
    /// 获取正式目标包内相对文件的完整路径。
    /// </summary>
    /// <param name="relativePath">目标包内相对路径。</param>
    /// <returns>完整文件路径。</returns>
    internal string GetTargetPath(string relativePath)
    {
        return GetFullPath(TargetPackageRoot, relativePath);
    }

    /// <summary>
    /// 在指定事务区域递归寻找唯一匹配的相对文件，供检查点断言阶段事实。
    /// </summary>
    /// <param name="areaName">事务区域名，例如 staging 或 backups。</param>
    /// <param name="relativePath">待定位文件的相对后缀。</param>
    /// <returns>唯一匹配文件的完整路径。</returns>
    internal string FindTransactionFile(string areaName, string relativePath)
    {
        var areaRoot = GetInstallerAreaRoot(areaName);
        Assert.True(Directory.Exists(areaRoot), "Transaction area does not exist: " + areaRoot);
        var normalizedSuffix = "/" + NormalizeRelativePath(relativePath);
        var matches = Directory.EnumerateFiles(areaRoot, "*", SearchOption.AllDirectories)
            .Where(path => NormalizeRelativePath(path).EndsWith(normalizedSuffix, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        return Assert.Single(matches);
    }

    /// <summary>
    /// 验证指定事务区域不存在，或已经清除所有事务子目录与文件。
    /// </summary>
    /// <param name="areaNames">需要检查的事务区域名。</param>
    internal void AssertTransactionAreasClean(params string[] areaNames)
    {
        foreach (var areaName in areaNames)
        {
            var areaRoot = GetInstallerAreaRoot(areaName);
            if (!Directory.Exists(areaRoot))
            {
                continue;
            }

            Assert.Empty(Directory.EnumerateFileSystemEntries(areaRoot, "*", SearchOption.AllDirectories));
        }
    }

    /// <summary>
    /// 删除夹具产生的临时目录，确保故障测试不会长期留下大批投影副本。
    /// </summary>
    public void Dispose()
    {
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, recursive: true);
        }
    }

    /// <summary>
    /// 获取项目内 Installer 事务区域根目录。
    /// </summary>
    /// <param name="areaName">事务区域名。</param>
    /// <returns>区域完整路径。</returns>
    private string GetInstallerAreaRoot(string areaName)
    {
        return Path.Combine(ProjectRoot, ".yokiframe", "installer", areaName);
    }

    /// <summary>
    /// 把正斜杠相对路径组合为当前平台完整路径。
    /// </summary>
    /// <param name="root">组合根目录。</param>
    /// <param name="relativePath">使用正斜杠的相对路径。</param>
    /// <returns>当前平台完整路径。</returns>
    private static string GetFullPath(string root, string relativePath)
    {
        return Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
    }

    /// <summary>
    /// 将路径分隔符统一为正斜杠，便于跨平台后缀比较。
    /// </summary>
    /// <param name="path">待规范化路径。</param>
    /// <returns>使用正斜杠的路径。</returns>
    private static string NormalizeRelativePath(string path)
    {
        return path.Replace('\\', '/');
    }
}

/// <summary>
/// 描述测试源文件路径、投影目标路径和期望内容。
/// </summary>
/// <param name="SourceRelativePath">源包内安全相对路径。</param>
/// <param name="ProjectionRelativePath">事务使用的投影相对路径。</param>
/// <param name="Content">源文件内容。</param>
internal readonly record struct PackageProjectionSpecification(
    string SourceRelativePath,
    string ProjectionRelativePath,
    string Content);

/// <summary>
/// 通过回调观察事务检查点，并在指定阶段抛出测试故障。
/// </summary>
internal sealed class CallbackTransactionFaultInjector : IPackageInstallTransactionFaultInjector
{
    private readonly Action<PackageInstallTransactionCheckpoint> mCallback;

    /// <summary>
    /// 创建回调式测试故障注入器。
    /// </summary>
    /// <param name="callback">每个事务检查点调用的测试回调。</param>
    internal CallbackTransactionFaultInjector(Action<PackageInstallTransactionCheckpoint> callback)
    {
        mCallback = callback;
    }

    /// <summary>
    /// 把当前检查点交给测试回调，由测试决定观察或抛出故障。
    /// </summary>
    /// <param name="checkpoint">当前事务检查点。</param>
    public void OnCheckpoint(PackageInstallTransactionCheckpoint checkpoint)
    {
        mCallback(checkpoint);
    }
}

/// <summary>
/// 保存目录树的路径、文件长度与内容哈希，用于证明拒绝流程没有产生可观察写入。
/// </summary>
internal sealed class DirectoryTreeSnapshot
{
    private readonly IReadOnlyList<string> mEntries;

    /// <summary>
    /// 保存已经稳定排序的目录树条目。
    /// </summary>
    /// <param name="entries">目录和文件摘要。</param>
    private DirectoryTreeSnapshot(IReadOnlyList<string> entries)
    {
        mEntries = entries;
    }

    /// <summary>
    /// 捕获指定根目录下全部目录与文件内容摘要。
    /// </summary>
    /// <param name="root">待捕获目录根。</param>
    /// <returns>可重复比较的目录树快照。</returns>
    internal static DirectoryTreeSnapshot Capture(string root)
    {
        List<string> entries = new();
        foreach (var directory in Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories))
        {
            entries.Add("D|" + NormalizePath(Path.GetRelativePath(root, directory)));
        }

        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            var content = File.ReadAllBytes(file);
            entries.Add(
                "F|" + NormalizePath(Path.GetRelativePath(root, file))
                + "|" + content.LongLength
                + "|" + Convert.ToHexString(SHA256.HashData(content)));
        }

        entries.Sort(StringComparer.Ordinal);
        return new DirectoryTreeSnapshot(entries);
    }

    /// <summary>
    /// 验证另一个快照与当前目录、文件和内容哈希完全一致。
    /// </summary>
    /// <param name="actual">操作后捕获的实际快照。</param>
    internal void AssertMatches(DirectoryTreeSnapshot actual)
    {
        Assert.Equal(mEntries, actual.mEntries);
    }

    /// <summary>
    /// 将平台目录分隔符统一为正斜杠。
    /// </summary>
    /// <param name="path">待规范化相对路径。</param>
    /// <returns>使用正斜杠的相对路径。</returns>
    private static string NormalizePath(string path)
    {
        return path.Replace('\\', '/');
    }
}
