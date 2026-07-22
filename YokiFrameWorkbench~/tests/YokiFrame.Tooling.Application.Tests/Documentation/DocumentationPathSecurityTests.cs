using YokiFrame.Tooling.Application.Documentation;

namespace YokiFrame.Tooling.Application.Tests.Documentation;

/// <summary>
/// 覆盖离线文档受控根自身的重解析点边界。
/// </summary>
public sealed class DocumentationPathSecurityTests
{
    /// <summary>
    /// 验证传入包根自身是符号链接时，文档索引不会跟随链接读取目标目录。
    /// </summary>
    [Fact]
    public void GetIndexRejectsReparsePointPackageRoot()
    {
        var testRoot = Path.Combine(Path.GetTempPath(), "yokiframe-documentation-root-tests", Guid.NewGuid().ToString("N"));
        var realPackageRoot = Path.Combine(testRoot, "real-package");
        var linkedPackageRoot = Path.Combine(testRoot, "linked-package");
        Directory.CreateDirectory(Path.Combine(realPackageRoot, "Documentation~", "Guides"));
        File.WriteAllText(
            Path.Combine(realPackageRoot, "package.json"),
            "{\"name\":\"com.hinatayoki.yokiframe\",\"version\":\"test\"}");
        File.WriteAllText(Path.Combine(realPackageRoot, "Documentation~", "Guides", "Secret.md"), "# secret");
        try
        {
            if (!TryCreateDirectoryLink(linkedPackageRoot, realPackageRoot))
            {
                return;
            }

            var service = new OfflineDocumentationService(linkedPackageRoot);

            Assert.Throws<UnauthorizedAccessException>(() => service.GetIndex());
        }
        finally
        {
            DeleteLinkAndRoot(linkedPackageRoot, testRoot);
        }
    }

    /// <summary>
    /// 尝试创建测试目录链接；宿主不支持或权限不足时跳过当前平台断言。
    /// </summary>
    /// <param name="linkPath">链接路径。</param>
    /// <param name="targetPath">真实目标目录。</param>
    /// <returns>链接创建成功时返回 true。</returns>
    private static bool TryCreateDirectoryLink(string linkPath, string targetPath)
    {
        try
        {
            Directory.CreateSymbolicLink(linkPath, targetPath);
            return true;
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException
                                          or PlatformNotSupportedException
                                          or NotSupportedException
                                          or IOException)
        {
            return false;
        }
    }

    /// <summary>
    /// 先删除目录链接再递归删除测试根，避免清理过程进入链接目标。
    /// </summary>
    /// <param name="linkPath">可能存在的目录链接。</param>
    /// <param name="testRoot">隔离测试根。</param>
    private static void DeleteLinkAndRoot(string linkPath, string testRoot)
    {
        if (Directory.Exists(linkPath))
        {
            Directory.Delete(linkPath);
        }

        if (Directory.Exists(testRoot))
        {
            Directory.Delete(testRoot, recursive: true);
        }
    }
}
