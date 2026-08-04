using YokiFrame.Installer.Core.Models;
using YokiFrame.Installer.Core.Services;

namespace YokiFrame.Installer.Core.Tests.Transactions;

/// <summary>
/// 固定 Installer 项目锁的并发隔离和句柄生命周期语义。
/// </summary>
public sealed class InstallerProjectLockTests
{
    /// <summary>
    /// 同一项目的第二个独立 lease 必须立即得到可诊断的忙碌异常，释放后可重新取得。
    /// </summary>
    [Fact]
    public void SameProjectCannotAcquireTwoLeases()
    {
        var projectRoot = CreateProjectRoot();
        try
        {
            using var firstLease = InstallerProjectLock.Acquire(projectRoot);
            var exception = Assert.Throws<InstallerProjectBusyException>(
                () => InstallerProjectLock.Acquire(projectRoot));

            Assert.Equal(Path.GetFullPath(projectRoot), exception.ProjectRoot);
            Assert.Equal(
                Path.Combine(projectRoot, ".yokiframe", "installer", "project.lock"),
                exception.LockPath);

            firstLease.Dispose();
            Assert.Contains("pid=", File.ReadAllText(exception.LockPath), StringComparison.Ordinal);
            using var secondLease = InstallerProjectLock.Acquire(projectRoot);
            Assert.Equal(Path.GetFullPath(projectRoot), secondLease.ProjectRoot);
        }
        finally
        {
            DeleteProjectRoot(projectRoot);
        }
    }

    /// <summary>
    /// 不同项目使用不同锁文件，可以同时进入写事务。
    /// </summary>
    [Fact]
    public void DifferentProjectsAreIndependent()
    {
        var firstProjectRoot = CreateProjectRoot();
        var secondProjectRoot = CreateProjectRoot();
        try
        {
            using var firstLease = InstallerProjectLock.Acquire(firstProjectRoot);
            using var secondLease = InstallerProjectLock.Acquire(secondProjectRoot);

            Assert.NotEqual(firstLease.LockPath, secondLease.LockPath);
            Assert.True(File.Exists(firstLease.LockPath));
            Assert.True(File.Exists(secondLease.LockPath));
        }
        finally
        {
            DeleteProjectRoot(firstProjectRoot);
            DeleteProjectRoot(secondProjectRoot);
        }
    }

    /// <summary>
    /// 目标项目不存在时不得创建项目外目录或锁文件。
    /// </summary>
    [Fact]
    public void MissingProjectIsRejectedBeforeLockCreation()
    {
        var missingProjectRoot = Path.Combine(
            Path.GetTempPath(),
            "yokiframe-installer-lock-tests",
            Guid.NewGuid().ToString("N"));

        Assert.Throws<DirectoryNotFoundException>(
            () => InstallerProjectLock.Acquire(missingProjectRoot));
        Assert.False(Directory.Exists(missingProjectRoot));
    }

    /// <summary>
    /// 创建最小目标项目目录。
    /// </summary>
    private static string CreateProjectRoot()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "yokiframe-installer-lock-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    /// <summary>
    /// 清理测试目录；锁 lease 已在调用方退出前释放。
    /// </summary>
    private static void DeleteProjectRoot(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }
}
