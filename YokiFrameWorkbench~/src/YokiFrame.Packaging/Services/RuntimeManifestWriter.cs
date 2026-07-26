using System.Text.Json;
using System.Text.Json.Serialization;
using YokiFrame.Packaging.Models;

namespace YokiFrame.Packaging.Services;

/// <summary>
/// 写入运行副本 manifest。
/// </summary>
public sealed class RuntimeManifestWriter
{
    /// <summary>
    /// 将 manifest 写入指定文件。
    /// </summary>
    /// <param name="manifest">运行副本 manifest。</param>
    /// <param name="path">manifest 文件路径。</param>
    public void Write(RuntimeManifest manifest, string path)
    {
        var fullPath = Path.GetFullPath(path);
        var directoryPath = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrEmpty(directoryPath))
        {
            throw new DirectoryNotFoundException("Manifest output path has no directory.");
        }

        Directory.CreateDirectory(directoryPath);
        var temporaryPath = fullPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            WriteTemporaryManifest(manifest, temporaryPath);
            CommitTemporaryManifest(temporaryPath, fullPath);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    /// <summary>
    /// 将完整 manifest 写入同目录临时文件并强制刷新，再由调用方执行原子替换。
    /// </summary>
    /// <param name="manifest">运行副本 manifest。</param>
    /// <param name="temporaryPath">临时文件路径。</param>
    private static void WriteTemporaryManifest(RuntimeManifest manifest, string temporaryPath)
    {
        var json = JsonSerializer.Serialize(manifest, RuntimePackagingJsonContext.Default.RuntimeManifest)
            + Environment.NewLine;
        using var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using var writer = new StreamWriter(stream);
        writer.Write(json);
        writer.Flush();
        stream.Flush(true);
    }

    /// <summary>
    /// 使用同卷替换提交临时文件；首次写入没有目标文件时直接重命名。
    /// </summary>
    /// <param name="temporaryPath">已完整刷新的临时文件。</param>
    /// <param name="targetPath">正式 manifest 路径。</param>
    private static void CommitTemporaryManifest(string temporaryPath, string targetPath)
    {
        if (File.Exists(targetPath))
        {
            File.Replace(temporaryPath, targetPath, null, true);
            return;
        }

        File.Move(temporaryPath, targetPath);
    }
}
