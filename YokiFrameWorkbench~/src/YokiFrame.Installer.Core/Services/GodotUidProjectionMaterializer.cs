using System.Security.Cryptography;
using System.Text;
using YokiFrame.Installer.Core.IO;
using YokiFrame.Installer.Core.Models;

namespace YokiFrame.Installer.Core.Services;

/// <summary>
/// 在 Godot 安装事务目录中物化 UID sidecar，并把它们合并为可校验的最终包投影。
/// </summary>
internal sealed class GodotUidProjectionMaterializer
{
    /// <summary>
    /// 写入全部生成 sidecar，计算 SHA-256，并与基础文件合并为 owner manifest 的唯一事实源。
    /// </summary>
    /// <param name="projection">已过滤的基础包投影。</param>
    /// <param name="sidecars">待生成的 UID sidecar。</param>
    /// <param name="generatedRoot">同项目卷内事务生成目录。</param>
    /// <returns>包含生成 UID 文件的最终包投影。</returns>
    public PackageProjection Materialize(
        PackageProjection projection,
        IReadOnlyList<GodotUidSidecar> sidecars,
        string generatedRoot)
    {
        List<PackageProjectionFile> files = new(projection.Files);
        foreach (var sidecar in sidecars)
        {
            var sourcePath = InstallerPathGuard.CombineInside(
                generatedRoot,
                sidecar.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            WriteTextDurably(sourcePath, sidecar.Content);
            files.Add(CreateProjectionFile(sourcePath, sidecar.RelativePath));
        }

        files.Sort(static (left, right) =>
            StringComparer.OrdinalIgnoreCase.Compare(left.RelativePath, right.RelativePath));
        return new PackageProjection(projection.SourcePackageRoot, projection.RuntimeProfile, files);
    }

    /// <summary>
    /// 为已物化 sidecar 创建长度和 SHA-256 投影记录。
    /// </summary>
    /// <param name="sourcePath">事务目录中的 sidecar 完整路径。</param>
    /// <param name="relativePath">sidecar 包相对路径。</param>
    /// <returns>可供包事务复验的投影文件。</returns>
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
    /// 使用 UTF-8 无 BOM 和 WriteThrough 物化 sidecar，确保包事务读取前文件已关闭并刷新。
    /// </summary>
    /// <param name="path">事务生成文件路径。</param>
    /// <param name="content">完整 UID 文本。</param>
    private static void WriteTextDurably(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using FileStream stream = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough);
        using StreamWriter writer = new(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(content);
        writer.Flush();
        stream.Flush(flushToDisk: true);
    }
}
