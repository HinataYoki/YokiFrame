namespace YokiFrame.Installer.Core.Models;

/// <summary>
/// 描述 owner manifest 中单个受管文件的可搬运校验事实。
/// </summary>
public sealed class PackageOwnerFile
{
    /// <summary>
    /// 创建受管文件记录。
    /// </summary>
    /// <param name="relativePath">使用正斜杠的包相对路径。</param>
    /// <param name="sha256">小写十六进制 SHA-256。</param>
    /// <param name="length">文件长度。</param>
    public PackageOwnerFile(string relativePath, string sha256, long length)
    {
        RelativePath = relativePath;
        Sha256 = sha256;
        Length = length;
    }

    /// <summary>
    /// 获取使用正斜杠的包相对路径。
    /// </summary>
    public string RelativePath { get; }

    /// <summary>
    /// 获取小写十六进制 SHA-256。
    /// </summary>
    public string Sha256 { get; }

    /// <summary>
    /// 获取文件长度。
    /// </summary>
    public long Length { get; }
}
