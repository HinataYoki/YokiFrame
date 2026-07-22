namespace YokiFrame.Installer.Core.Models;

/// <summary>
/// 描述受控安装投影中的单个文件及其稳定内容摘要。
/// </summary>
public sealed class PackageProjectionFile
{
    /// <summary>
    /// 创建投影文件描述。
    /// </summary>
    /// <param name="sourcePath">源包内文件的绝对路径。</param>
    /// <param name="relativePath">使用正斜杠的包相对路径。</param>
    /// <param name="sha256">小写十六进制 SHA-256 摘要。</param>
    /// <param name="length">文件长度。</param>
    public PackageProjectionFile(string sourcePath, string relativePath, string sha256, long length)
    {
        SourcePath = sourcePath;
        RelativePath = relativePath;
        Sha256 = sha256;
        Length = length;
    }

    /// <summary>
    /// 获取源包内文件的绝对路径。
    /// </summary>
    public string SourcePath { get; }

    /// <summary>
    /// 获取使用正斜杠的包相对路径。
    /// </summary>
    public string RelativePath { get; }

    /// <summary>
    /// 获取小写十六进制 SHA-256 摘要。
    /// </summary>
    public string Sha256 { get; }

    /// <summary>
    /// 获取文件长度。
    /// </summary>
    public long Length { get; }
}
