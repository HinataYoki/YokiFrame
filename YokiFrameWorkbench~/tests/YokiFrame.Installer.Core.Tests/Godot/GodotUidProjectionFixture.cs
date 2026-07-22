using System.Security.Cryptography;
using YokiFrame.Installer.Core.Models;

namespace YokiFrame.Installer.Core.Tests.Godot;

/// <summary>
/// 为 UID 投影测试提供真实源文件、目标 sidecar 和稳定 SHA-256 投影。
/// </summary>
internal sealed class GodotUidProjectionFixture : IDisposable
{
    /// <summary>
    /// 创建完全隔离的源包和目标包目录。
    /// </summary>
    private GodotUidProjectionFixture()
    {
        Root = Path.Combine(Path.GetTempPath(), "yokiframe-godot-uid-tests", Guid.NewGuid().ToString("N"));
        SourcePackageRoot = Path.Combine(Root, "source", "YokiFrame");
        TargetPackageRoot = Path.Combine(Root, "target", "YokiFrame");
        Directory.CreateDirectory(SourcePackageRoot);
        Directory.CreateDirectory(TargetPackageRoot);
    }

    /// <summary>获取测试临时根目录。</summary>
    internal string Root { get; }

    /// <summary>获取模拟源包根。</summary>
    internal string SourcePackageRoot { get; }

    /// <summary>获取模拟正式目标包根。</summary>
    internal string TargetPackageRoot { get; }

    /// <summary>
    /// 创建新的 UID 投影 fixture。
    /// </summary>
    /// <returns>已建立隔离目录的 fixture。</returns>
    internal static GodotUidProjectionFixture Create()
    {
        return new GodotUidProjectionFixture();
    }

    /// <summary>
    /// 按相对路径创建真实源文件和稳定投影。
    /// </summary>
    /// <param name="relativePaths">需要进入基础投影的包相对路径。</param>
    /// <returns>按路径稳定排序的基础投影。</returns>
    internal PackageProjection CreateProjection(params string[] relativePaths)
    {
        List<PackageProjectionFile> files = new();
        foreach (var relativePath in relativePaths.OrderBy(static path => path, StringComparer.OrdinalIgnoreCase))
        {
            var sourcePath = Combine(SourcePackageRoot, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
            File.WriteAllText(sourcePath, relativePath);
            var content = File.ReadAllBytes(sourcePath);
            files.Add(new PackageProjectionFile(
                sourcePath,
                relativePath,
                Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant(),
                content.LongLength));
        }

        return new PackageProjection(SourcePackageRoot, "win-x64", files);
    }

    /// <summary>
    /// 在正式目标包写入既有 UID sidecar，供保留和修复测试读取。
    /// </summary>
    /// <param name="relativePath">sidecar 的包相对路径。</param>
    /// <param name="content">既有 UID 文件完整文本。</param>
    internal void WriteTargetSidecar(string relativePath, string content)
    {
        var path = Combine(TargetPackageRoot, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    /// <summary>
    /// 删除 fixture 创建的临时目录。
    /// </summary>
    public void Dispose()
    {
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, recursive: true);
        }
    }

    /// <summary>
    /// 将正斜杠相对路径组合到当前平台目录。
    /// </summary>
    /// <param name="root">组合根目录。</param>
    /// <param name="relativePath">包相对路径。</param>
    /// <returns>完整平台路径。</returns>
    private static string Combine(string root, string relativePath)
    {
        return Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
    }
}
