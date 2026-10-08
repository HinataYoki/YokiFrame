using System.Diagnostics;
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
            Assert.True(
                TryCreateDirectoryLink(linkPath, outsideRoot),
                "当前进程不能创建目录符号链接或 junction，无法观察重解析点拒绝。");

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
    /// 创建目录重解析点。符号链接无权限时，在 Windows 上改用同样会被拒绝的 junction。
    /// </summary>
    /// <param name="linkPath">链接路径。</param>
    /// <param name="targetPath">链接目标。</param>
    /// <returns>链接创建成功时返回 true。</returns>
    private static bool TryCreateDirectoryLink(string linkPath, string targetPath)
    {
        if (TryCreateSymbolicLink(linkPath, targetPath))
        {
            return true;
        }

        return OperatingSystem.IsWindows() && TryCreateWindowsJunction(linkPath, targetPath);
    }

    /// <summary>
    /// 尝试创建目录符号链接。
    /// </summary>
    /// <param name="linkPath">链接路径。</param>
    /// <param name="targetPath">链接目标。</param>
    /// <returns>当前进程允许创建符号链接时返回 true。</returns>
    private static bool TryCreateSymbolicLink(string linkPath, string targetPath)
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
    /// 用 mklink /J 创建不需要管理员权限的目录 junction。
    /// </summary>
    /// <param name="linkPath">链接路径。</param>
    /// <param name="targetPath">已存在的目录目标。</param>
    /// <returns>junction 创建成功时返回 true。</returns>
    private static bool TryCreateWindowsJunction(string linkPath, string targetPath)
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = "/d /c mklink /J \"" + linkPath + "\" \"" + targetPath + "\"",
            CreateNoWindow = true,
            UseShellExecute = false
        });
        if (process == null)
        {
            return false;
        }

        process.WaitForExit();
        return process.ExitCode == 0 && IsDirectoryReparsePoint(linkPath);
    }

    /// <summary>
    /// 判断路径是否为目录重解析点。
    /// </summary>
    /// <param name="path">待检查路径。</param>
    /// <returns>存在且带重解析点属性时返回 true。</returns>
    private static bool IsDirectoryReparsePoint(string path)
    {
        return Directory.Exists(path)
            && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
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
