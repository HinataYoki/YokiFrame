namespace YokiFrame.Packaging.Models;

/// <summary>
/// 描述发布产物中的单个文件。
/// </summary>
public sealed class RuntimeManifestFile
{
    /// <summary>
    /// 创建发布文件记录。
    /// </summary>
    /// <param name="relativePath">相对 runtime root 的路径。</param>
    /// <param name="sizeBytes">文件大小。</param>
    /// <param name="sha256">文件 SHA256。</param>
    public RuntimeManifestFile(string relativePath, long sizeBytes, string sha256)
    {
        RelativePath = relativePath;
        SizeBytes = sizeBytes;
        Sha256 = sha256;
    }

    /// <summary>
    /// 获取相对 runtime root 的路径。
    /// </summary>
    public string RelativePath { get; }

    /// <summary>
    /// 获取文件大小。
    /// </summary>
    public long SizeBytes { get; }

    /// <summary>
    /// 获取文件 SHA256。
    /// </summary>
    public string Sha256 { get; }
}
