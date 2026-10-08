using System.Text;

namespace YokiFrame.Installer.Core.Tests;

/// <summary>
/// 用字节比较安装器写出的文件，避免文本读取或子串断言冒充副作用。
/// </summary>
internal static class InstallerFileAssert
{
    /// <summary>
    /// 断言文件与期望 UTF-8 文本逐字节一致，不接受 BOM。
    /// </summary>
    /// <param name="path">待检查文件。</param>
    /// <param name="expected">不含 BOM 的期望文本。</param>
    internal static void EqualUtf8(string path, string expected)
    {
        Assert.Equal(Encoding.UTF8.GetBytes(expected), File.ReadAllBytes(path));
    }

    /// <summary>
    /// 断言两个文件的字节完全一致，用于证明复制或回滚没有改写内容。
    /// </summary>
    /// <param name="expectedPath">期望内容来源。</param>
    /// <param name="actualPath">实际写出的文件。</param>
    internal static void EqualFile(string expectedPath, string actualPath)
    {
        Assert.Equal(File.ReadAllBytes(expectedPath), File.ReadAllBytes(actualPath));
    }
}
