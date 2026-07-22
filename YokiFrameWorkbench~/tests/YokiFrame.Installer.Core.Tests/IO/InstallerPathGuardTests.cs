using YokiFrame.Installer.Core.IO;

namespace YokiFrame.Installer.Core.Tests.IO;

/// <summary>
/// 覆盖 Installer 目标路径的宿主大小写语义与重解析点边界。
/// </summary>
public sealed class InstallerPathGuardTests
{
    /// <summary>
    /// 验证仅大小写不同的兄弟目录在 POSIX 上不能冒充根目录，Windows 则保持原生大小写不敏感语义。
    /// </summary>
    [Fact]
    public void CombineInsideUsesHostPathCaseSemantics()
    {
        var parentRoot = Path.Combine(Path.GetTempPath(), "yokiframe-path-guard", Guid.NewGuid().ToString("N"));
        var root = Path.Combine(parentRoot, "ProjectRoot");
        var siblingRelativePath = Path.Combine("..", "projectroot", "payload.txt");

        if (OperatingSystem.IsWindows())
        {
            var path = InstallerPathGuard.CombineInside(root, siblingRelativePath);
            Assert.Equal(Path.GetFullPath(Path.Combine(root, siblingRelativePath)), path);
            return;
        }

        Assert.Throws<IOException>(() => InstallerPathGuard.CombineInside(root, siblingRelativePath));
    }

    /// <summary>
    /// 验证项目内已有目录链接时拒绝继续组合子路径，防止事务把载荷写入项目外目标。
    /// </summary>
    [Fact]
    public void CombineInsideRejectsExistingDirectoryLink()
    {
        var fixtureRoot = Path.Combine(Path.GetTempPath(), "yokiframe-path-guard", Guid.NewGuid().ToString("N"));
        var projectRoot = Path.Combine(fixtureRoot, "project");
        var outsideRoot = Path.Combine(fixtureRoot, "outside");
        var linkPath = Path.Combine(projectRoot, "Packages");
        Directory.CreateDirectory(projectRoot);
        Directory.CreateDirectory(outsideRoot);
        try
        {
            if (!TryCreateDirectoryLink(linkPath, outsideRoot))
            {
                return;
            }

            var exception = Assert.Throws<IOException>(() =>
                InstallerPathGuard.CombineInside(projectRoot, "Packages", "payload.txt"));
            Assert.Contains("symbolic link or junction", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            DeleteFixture(fixtureRoot, linkPath);
        }
    }

    /// <summary>
    /// 尝试创建目录链接；宿主策略禁止创建链接时由发布环境的专项测试继续覆盖。
    /// </summary>
    /// <param name="linkPath">链接路径。</param>
    /// <param name="targetPath">链接目标。</param>
    /// <returns>链接创建成功时返回 true。</returns>
    private static bool TryCreateDirectoryLink(string linkPath, string targetPath)
    {
        try
        {
            Directory.CreateSymbolicLink(linkPath, targetPath);
            return true;
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException or PlatformNotSupportedException)
        {
            return false;
        }
    }

    /// <summary>
    /// 先移除目录链接再递归清理隔离根，避免清理逻辑触达链接目标。
    /// </summary>
    /// <param name="fixtureRoot">隔离测试根。</param>
    /// <param name="linkPath">可能已经创建的目录链接。</param>
    private static void DeleteFixture(string fixtureRoot, string linkPath)
    {
        if (Directory.Exists(linkPath))
        {
            Directory.Delete(linkPath);
        }

        if (Directory.Exists(fixtureRoot))
        {
            Directory.Delete(fixtureRoot, recursive: true);
        }
    }
}
