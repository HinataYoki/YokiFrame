using YokiFrame.Installer.Core.Models;
using YokiFrame.Installer.Core.Services;

namespace YokiFrame.Installer.Core.Tests.Transactions;

/// <summary>
/// 验证持久 Installer journal 在进程中断后的幂等恢复边界。
/// </summary>
public sealed class InstallerTransactionRecoveryTests
{
    /// <summary>
    /// 旧包已经备份但新包尚未提交时，恢复器必须把旧包移回正式目录。
    /// </summary>
    [Fact]
    public void ExistingBackupIsRestoredBeforeCommit()
    {
        var projectRoot = CreateProjectRoot();
        try
        {
            var transactionId = Guid.NewGuid().ToString("N");
            var targetRoot = Path.Combine(projectRoot, "Packages", "com.hinatayoki.yokiframe");
            var stagingRoot = Path.Combine(projectRoot, ".yokiframe", "installer", "staging", transactionId);
            var backupRoot = Path.Combine(projectRoot, ".yokiframe", "installer", "backups", transactionId);
            Directory.CreateDirectory(backupRoot);
            File.WriteAllText(Path.Combine(backupRoot, "old.txt"), "old");
            var journal = InstallerTransactionJournal.Create(
                projectRoot,
                "unity-package",
                transactionId,
                targetRoot,
                stagingRoot,
                backupRoot,
                targetOriginallyExists: true);
            journal.Advance(InstallerTransactionPhase.ExistingTargetBackedUp);

            InstallerPackageTransactionRecovery.Recover(projectRoot);

            Assert.Equal("old", File.ReadAllText(Path.Combine(targetRoot, "old.txt")));
            Assert.False(Directory.Exists(stagingRoot));
            Assert.False(Directory.Exists(backupRoot));
            Assert.Empty(InstallerTransactionJournal.ReadAll(projectRoot));
        }
        finally
        {
            DeleteProjectRoot(projectRoot);
        }
    }

    /// <summary>
    /// 目录提交已经发生但 checkpoint 仍停留在备份阶段时，恢复器仍必须回到旧包。
    /// </summary>
    [Fact]
    public void NewTargetIsDiscardedWhenCommitCheckpointWasNotPersisted()
    {
        var projectRoot = CreateProjectRoot();
        try
        {
            var transactionId = Guid.NewGuid().ToString("N");
            var targetRoot = Path.Combine(projectRoot, "Packages", "com.hinatayoki.yokiframe");
            var stagingRoot = Path.Combine(projectRoot, ".yokiframe", "installer", "staging", transactionId);
            var backupRoot = Path.Combine(projectRoot, ".yokiframe", "installer", "backups", transactionId);
            Directory.CreateDirectory(targetRoot);
            File.WriteAllText(Path.Combine(targetRoot, "new.txt"), "new");
            Directory.CreateDirectory(backupRoot);
            File.WriteAllText(Path.Combine(backupRoot, "old.txt"), "old");
            var journal = InstallerTransactionJournal.Create(
                projectRoot,
                "unity-package",
                transactionId,
                targetRoot,
                stagingRoot,
                backupRoot,
                targetOriginallyExists: true);
            journal.Advance(InstallerTransactionPhase.ExistingTargetBackedUp);

            InstallerPackageTransactionRecovery.Recover(projectRoot);

            Assert.Equal("old", File.ReadAllText(Path.Combine(targetRoot, "old.txt")));
            Assert.False(File.Exists(Path.Combine(targetRoot, "new.txt")));
            Assert.Empty(InstallerTransactionJournal.ReadAll(projectRoot));
        }
        finally
        {
            DeleteProjectRoot(projectRoot);
        }
    }

    /// <summary>
    /// 首次安装在目标切换后中断且没有旧包时，恢复器必须删除不完整的新目录。
    /// </summary>
    [Fact]
    public void FirstInstallTargetIsRemovedAfterCommitInterruption()
    {
        var projectRoot = CreateProjectRoot();
        try
        {
            var transactionId = Guid.NewGuid().ToString("N");
            var targetRoot = Path.Combine(projectRoot, "Packages", "com.hinatayoki.yokiframe");
            var stagingRoot = Path.Combine(projectRoot, ".yokiframe", "installer", "staging", transactionId);
            var backupRoot = Path.Combine(projectRoot, ".yokiframe", "installer", "backups", transactionId);
            Directory.CreateDirectory(targetRoot);
            File.WriteAllText(Path.Combine(targetRoot, "partial.txt"), "partial");
            var journal = InstallerTransactionJournal.Create(
                projectRoot,
                "unity-package",
                transactionId,
                targetRoot,
                stagingRoot,
                backupRoot,
                targetOriginallyExists: false);
            journal.Advance(InstallerTransactionPhase.TargetCommitted);

            InstallerPackageTransactionRecovery.Recover(projectRoot);

            Assert.False(Directory.Exists(targetRoot));
            Assert.Empty(InstallerTransactionJournal.ReadAll(projectRoot));
        }
        finally
        {
            DeleteProjectRoot(projectRoot);
        }
    }

    /// <summary>
    /// 标记为 RecoveryRequired 的事务不得被静默删除或猜测恢复。
    /// </summary>
    [Fact]
    public void RecoveryRequiredBlocksAutomaticCleanup()
    {
        var projectRoot = CreateProjectRoot();
        try
        {
            var transactionId = Guid.NewGuid().ToString("N");
            var targetRoot = Path.Combine(projectRoot, "Packages", "com.hinatayoki.yokiframe");
            var stagingRoot = Path.Combine(projectRoot, ".yokiframe", "installer", "staging", transactionId);
            var backupRoot = Path.Combine(projectRoot, ".yokiframe", "installer", "backups", transactionId);
            var journal = InstallerTransactionJournal.Create(
                projectRoot,
                "unity-package",
                transactionId,
                targetRoot,
                stagingRoot,
                backupRoot,
                targetOriginallyExists: true);
            journal.MarkRecoveryRequired();

            var exception = Assert.Throws<InstallerRecoveryRequiredException>(
                () => InstallerPackageTransactionRecovery.Recover(projectRoot));

            Assert.Equal(journal.JournalPath, exception.JournalPath);
            Assert.Single(InstallerTransactionJournal.ReadAll(projectRoot));
        }
        finally
        {
            DeleteProjectRoot(projectRoot);
        }
    }

    /// <summary>
    /// Godot add-on 与项目 owner 文件部分提交后，恢复器必须按 journal 恢复全部旧内容。
    /// </summary>
    [Fact]
    public void GodotProjectFilesAndAddonAreRestoredAfterPartialCommit()
    {
        var projectRoot = CreateProjectRoot();
        try
        {
            var transactionId = Guid.NewGuid().ToString("N");
            var targetRoot = Path.Combine(projectRoot, "addons", "yokiframe");
            var cleanupRoot = Path.Combine(projectRoot, ".yokiframe", "installer", "godot", transactionId);
            var stagingRoot = Path.Combine(cleanupRoot, "staging", "addon", "yokiframe");
            var backupRoot = Path.Combine(cleanupRoot, "backup", "addon", "yokiframe");
            var projectFile = Path.Combine(projectRoot, "Game.csproj");
            var projectBackup = Path.Combine(cleanupRoot, "backup", "project-files", "Game.csproj");
            Directory.CreateDirectory(targetRoot);
            File.WriteAllText(Path.Combine(targetRoot, "version.txt"), "new");
            Directory.CreateDirectory(backupRoot);
            File.WriteAllText(Path.Combine(backupRoot, "version.txt"), "old");
            File.WriteAllText(projectFile, "new-project");
            Directory.CreateDirectory(Path.GetDirectoryName(projectBackup)!);
            File.WriteAllText(projectBackup, "old-project");

            var journal = InstallerTransactionJournal.Create(
                projectRoot,
                "godot-addon",
                transactionId,
                targetRoot,
                stagingRoot,
                backupRoot,
                targetOriginallyExists: true,
                cleanupRoot: cleanupRoot,
                projectFiles: new[]
                {
                    new InstallerProjectFileJournalEntry(projectFile, projectBackup, OriginalExists: true, Committed: false)
                });
            journal.Advance(InstallerTransactionPhase.TargetCommitted);
            journal.MarkProjectFileCommitted(projectFile);

            InstallerGodotTransactionRecovery.Recover(projectRoot);

            Assert.Equal("old", File.ReadAllText(Path.Combine(targetRoot, "version.txt")));
            Assert.Equal("old-project", File.ReadAllText(projectFile));
            Assert.False(Directory.Exists(cleanupRoot));
            Assert.Empty(InstallerTransactionJournal.ReadAll(projectRoot));
        }
        finally
        {
            DeleteProjectRoot(projectRoot);
        }
    }

    /// <summary>
    /// Godot add-on 已切换但 checkpoint 尚未来得及更新时，恢复器必须丢弃新目录并恢复旧目录。
    /// </summary>
    [Fact]
    public void GodotNewAddonIsDiscardedWhenCommitCheckpointWasNotPersisted()
    {
        var projectRoot = CreateProjectRoot();
        try
        {
            var transactionId = Guid.NewGuid().ToString("N");
            var targetRoot = Path.Combine(projectRoot, "addons", "yokiframe");
            var cleanupRoot = Path.Combine(projectRoot, ".yokiframe", "installer", "godot", transactionId);
            var stagingRoot = Path.Combine(cleanupRoot, "staging", "addon", "yokiframe");
            var backupRoot = Path.Combine(cleanupRoot, "backup", "addon", "yokiframe");
            Directory.CreateDirectory(targetRoot);
            File.WriteAllText(Path.Combine(targetRoot, "version.txt"), "new");
            Directory.CreateDirectory(backupRoot);
            File.WriteAllText(Path.Combine(backupRoot, "version.txt"), "old");
            var journal = InstallerTransactionJournal.Create(
                projectRoot,
                "godot-addon",
                transactionId,
                targetRoot,
                stagingRoot,
                backupRoot,
                targetOriginallyExists: true,
                cleanupRoot: cleanupRoot);
            journal.Advance(InstallerTransactionPhase.ExistingTargetBackedUp);

            InstallerGodotTransactionRecovery.Recover(projectRoot);

            Assert.Equal("old", File.ReadAllText(Path.Combine(targetRoot, "version.txt")));
            Assert.Empty(InstallerTransactionJournal.ReadAll(projectRoot));
        }
        finally
        {
            DeleteProjectRoot(projectRoot);
        }
    }

    /// <summary>
    /// Godot post-verify journal 只清理辅助目录，不能撤销已经完成的正式投影。
    /// </summary>
    [Fact]
    public void GodotPostVerifiedJournalPreservesCommittedFiles()
    {
        var projectRoot = CreateProjectRoot();
        try
        {
            var transactionId = Guid.NewGuid().ToString("N");
            var targetRoot = Path.Combine(projectRoot, "addons", "yokiframe");
            var cleanupRoot = Path.Combine(projectRoot, ".yokiframe", "installer", "godot", transactionId);
            var stagingRoot = Path.Combine(cleanupRoot, "staging", "addon", "yokiframe");
            var backupRoot = Path.Combine(cleanupRoot, "backup", "addon", "yokiframe");
            Directory.CreateDirectory(targetRoot);
            File.WriteAllText(Path.Combine(targetRoot, "version.txt"), "current");
            Directory.CreateDirectory(stagingRoot);
            Directory.CreateDirectory(backupRoot);
            var journal = InstallerTransactionJournal.Create(
                projectRoot,
                "godot-addon",
                transactionId,
                targetRoot,
                stagingRoot,
                backupRoot,
                targetOriginallyExists: false,
                cleanupRoot: cleanupRoot);
            journal.Advance(InstallerTransactionPhase.PostVerified);

            InstallerGodotTransactionRecovery.Recover(projectRoot);

            Assert.Equal("current", File.ReadAllText(Path.Combine(targetRoot, "version.txt")));
            Assert.False(Directory.Exists(cleanupRoot));
            Assert.Empty(InstallerTransactionJournal.ReadAll(projectRoot));
        }
        finally
        {
            DeleteProjectRoot(projectRoot);
        }
    }

    /// <summary>
    /// 创建最小项目根目录。
    /// </summary>
    private static string CreateProjectRoot()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "yokiframe-installer-recovery-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    /// <summary>
    /// 清理测试项目目录。
    /// </summary>
    private static void DeleteProjectRoot(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }
}
