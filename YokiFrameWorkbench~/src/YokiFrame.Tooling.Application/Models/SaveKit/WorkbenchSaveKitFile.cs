namespace YokiFrame.Tooling.Application.Models.SaveKit;

/// <summary>SaveKit 存档文件的 Workbench 元信息；不包含真实 payload。</summary>
public sealed record WorkbenchSaveKitFile(
    string Kind,
    string Name,
    string FileName,
    string FullPath,
    long SizeBytes,
    DateTime LastWriteTimeUtc)
{
    /// <summary>获取面向用户的文件大小文本。</summary>
    public string SizeText => FormatSize(SizeBytes);

    /// <summary>获取面向用户的本地修改时间文本。</summary>
    public string ModifiedText => LastWriteTimeUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");

    /// <summary>把字节数量转换为紧凑文件大小文本。</summary>
    /// <param name="bytes">文件字节数。</param>
    /// <returns>带单位的文件大小。</returns>
    private static string FormatSize(long bytes)
    {
        if (bytes < 1024)
        {
            return bytes + " B";
        }

        if (bytes < 1024 * 1024)
        {
            return (bytes / 1024d).ToString("0.0") + " KB";
        }

        return (bytes / (1024d * 1024d)).ToString("0.0") + " MB";
    }
}
