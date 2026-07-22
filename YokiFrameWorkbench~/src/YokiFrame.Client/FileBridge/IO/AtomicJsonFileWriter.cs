using System.Text;

namespace YokiFrame.Client.FileBridge.IO;

/// <summary>
/// 以临时文件加原子替换的方式写入 JSON，避免命令消费者读到半写文件。
/// </summary>
internal static class AtomicJsonFileWriter
{
    /// <summary>
    /// 将 JSON 文本写入目标文件；写入完成前只暴露同目录临时文件。
    /// </summary>
    /// <param name="targetPath">最终目标路径。</param>
    /// <param name="json">待写入 JSON 文本。</param>
    public static void WriteAllText(string targetPath, string json)
    {
        var targetDirectory = Path.GetDirectoryName(targetPath)
            ?? throw new DirectoryNotFoundException($"Target path has no directory: {targetPath}");
        Directory.CreateDirectory(targetDirectory);

        var tempPath = Path.Combine(targetDirectory, $"{Path.GetFileName(targetPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
            {
                writer.Write(json);
                writer.Flush();
                stream.Flush(true);
            }

            File.Move(tempPath, targetPath, true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }
}
