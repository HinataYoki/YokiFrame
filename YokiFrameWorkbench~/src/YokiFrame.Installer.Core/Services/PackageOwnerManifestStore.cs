using System.Text;
using System.Text.Json;
using YokiFrame.Installer.Core.IO;
using YokiFrame.Installer.Core.Models;
using YokiFrame.Installer.Core.Serialization;

namespace YokiFrame.Installer.Core.Services;

/// <summary>
/// 创建、读取并原子写入受管包 owner manifest。
/// </summary>
public sealed class PackageOwnerManifestStore
{
    private const int SCHEMA_VERSION = 1;
    internal const string MANIFEST_FILE_NAME = ".yokiframe-owner.json";

    /// <summary>
    /// 从文件级投影创建不含绝对路径的 owner manifest。
    /// </summary>
    /// <param name="projection">已验证的包投影。</param>
    /// <returns>稳定排序的 owner manifest。</returns>
    public PackageOwnerManifest Create(PackageProjection projection)
    {
        var files = projection.Files
            .OrderBy(static file => file.RelativePath, StringComparer.OrdinalIgnoreCase)
            .Select(static file => new PackageOwnerFile(file.RelativePath, file.Sha256, file.Length))
            .ToArray();
        return new PackageOwnerManifest(SCHEMA_VERSION, projection.RuntimeProfile, files);
    }

    /// <summary>
    /// 原子写入 owner manifest；失败时不留下部分 JSON。
    /// </summary>
    /// <param name="packageRoot">受管包根目录。</param>
    /// <param name="manifest">待写入 manifest。</param>
    public void Write(string packageRoot, PackageOwnerManifest manifest)
    {
        var manifestPath = GetManifestPath(packageRoot);
        Directory.CreateDirectory(Path.GetDirectoryName(manifestPath)!);
        var json = JsonSerializer.Serialize(
            manifest,
            InstallerJsonContext.Default.PackageOwnerManifest);
        WriteAtomic(manifestPath, json);
    }

    /// <summary>
    /// 读取并验证 owner manifest schema。
    /// </summary>
    /// <param name="packageRoot">受管包根目录。</param>
    /// <returns>已解析 manifest。</returns>
    public PackageOwnerManifest Read(string packageRoot)
    {
        var manifestPath = GetManifestPath(packageRoot);
        if (!File.Exists(manifestPath))
        {
            throw new FileNotFoundException("Package owner manifest was not found.", manifestPath);
        }

        var manifest = JsonSerializer.Deserialize(
                File.ReadAllText(manifestPath),
                InstallerJsonContext.Default.PackageOwnerManifest)
            ?? throw new InvalidDataException("Package owner manifest is empty: " + manifestPath);
        if (manifest.SchemaVersion != SCHEMA_VERSION)
        {
            throw new InvalidDataException("Unsupported package owner manifest schema: " + manifest.SchemaVersion);
        }

        return manifest;
    }

    /// <summary>
    /// 获取 owner manifest 在受管包内的固定路径。
    /// </summary>
    /// <param name="packageRoot">受管包根目录。</param>
    /// <returns>manifest 绝对路径。</returns>
    public string GetManifestPath(string packageRoot)
    {
        var fullPackageRoot = InstallerPathGuard.RequireFullPath(packageRoot, nameof(packageRoot));
        return InstallerPathGuard.CombineInside(fullPackageRoot, MANIFEST_FILE_NAME);
    }

    /// <summary>
    /// 使用同目录临时文件、强制 flush 和原子重命名提交 manifest。
    /// </summary>
    /// <param name="targetPath">manifest 正式路径。</param>
    /// <param name="content">完整 JSON 文本。</param>
    private static void WriteAtomic(string targetPath, string content)
    {
        var temporaryPath = targetPath + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            using (FileStream stream = new(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            using (StreamWriter writer = new(stream, new UTF8Encoding(false)))
            {
                writer.Write(content);
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, targetPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }
}
