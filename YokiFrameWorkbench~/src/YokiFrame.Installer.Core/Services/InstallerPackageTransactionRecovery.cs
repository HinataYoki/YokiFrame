using YokiFrame.Installer.Core.Models;

namespace YokiFrame.Installer.Core.Services;

/// <summary>
/// 在取得项目锁后恢复上一次 Unity package 事务留下的持久 journal。
/// </summary>
internal static class InstallerPackageTransactionRecovery
{
    private const string TRANSACTION_KIND = "unity-package";
    private const string PACKAGE_DIRECTORY_PREFIX = "Packages/";
    private const string PACKAGE_DIRECTORY_NAME = "com.hinatayoki.yokiframe";
    private const string INSTALLER_STAGING_PREFIX = ".yokiframe/installer/staging/";
    private const string INSTALLER_BACKUP_PREFIX = ".yokiframe/installer/backups/";

    /// <summary>
    /// 恢复所有可识别的 Unity package journal；不确定状态会阻止下一次写入。
    /// </summary>
    /// <param name="projectRoot">目标项目根。</param>
    internal static void Recover(string projectRoot)
    {
        var journals = InstallerTransactionJournal.ReadAll(projectRoot);
        foreach (var journal in journals.Where(static value =>
                     string.Equals(value.Record.Kind, TRANSACTION_KIND, StringComparison.Ordinal)))
        {
            RecoverOne(journal);
        }
    }

    /// <summary>
    /// 根据最后持久 checkpoint 选择完成清理或恢复旧包。
    /// </summary>
    /// <param name="journal">待恢复 journal。</param>
    private static void RecoverOne(InstallerTransactionJournal journal)
    {
        ValidatePackageJournal(journal);
        var targetPath = journal.ResolvePath(journal.Record.TargetRelativePath);
        var stagingPath = journal.ResolvePath(journal.Record.StagingRelativePath);
        var backupPath = journal.ResolvePath(journal.Record.BackupRelativePath);

        try
        {
            switch (journal.Record.Phase)
            {
                case InstallerTransactionPhase.Prepared:
                case InstallerTransactionPhase.StagingVerified:
                    RestoreBackupIfPresent(journal, targetPath, backupPath);
                    DeleteDirectoryOrFile(stagingPath);
                    break;
                case InstallerTransactionPhase.ExistingTargetBackedUp:
                    // CommitDirectory 可能已经完成但 checkpoint 尚未来得及落盘；
                    // 按提交后状态恢复可同时覆盖“目标不存在”和“新目标已出现”两种物理状态。
                    RestoreBackupAfterCommit(journal, targetPath, backupPath);
                    DeleteDirectoryOrFile(stagingPath);
                    break;
                case InstallerTransactionPhase.TargetCommitted:
                    RestoreBackupAfterCommit(journal, targetPath, backupPath);
                    DeleteDirectoryOrFile(stagingPath);
                    break;
                case InstallerTransactionPhase.PostVerified:
                    DeleteDirectoryOrFile(stagingPath);
                    DeleteDirectoryOrFile(backupPath);
                    break;
                case InstallerTransactionPhase.RecoveryRequired:
                    throw new InstallerRecoveryRequiredException(
                        journal.ProjectRoot,
                        journal.JournalPath,
                        "Installer transaction is marked RecoveryRequired: " + journal.JournalPath);
                default:
                    throw new ArgumentOutOfRangeException();
            }

            journal.Complete();
        }
        catch (InstallerRecoveryRequiredException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new InstallerRecoveryRequiredException(
                journal.ProjectRoot,
                journal.JournalPath,
                "Installer transaction recovery failed: " + exception.Message);
        }
    }

    /// <summary>
    /// 防止被篡改的 journal 把恢复器引向任意项目文件。
    /// </summary>
    /// <param name="journal">待读取 journal。</param>
    private static void ValidatePackageJournal(InstallerTransactionJournal journal)
    {
        var target = journal.Record.TargetRelativePath.Replace('\\', '/');
        var staging = journal.Record.StagingRelativePath.Replace('\\', '/');
        var backup = journal.Record.BackupRelativePath.Replace('\\', '/');
        var isPackage = target.StartsWith(PACKAGE_DIRECTORY_PREFIX, StringComparison.OrdinalIgnoreCase)
            && string.Equals(
                target.TrimEnd('/').Split('/').Last(),
                PACKAGE_DIRECTORY_NAME,
                StringComparison.OrdinalIgnoreCase);
        var isStaging = staging.StartsWith(INSTALLER_STAGING_PREFIX, StringComparison.OrdinalIgnoreCase);
        var isBackup = backup.StartsWith(INSTALLER_BACKUP_PREFIX, StringComparison.OrdinalIgnoreCase);
        if (!isPackage || !isStaging || !isBackup)
        {
            throw new InstallerRecoveryRequiredException(
                journal.ProjectRoot,
                journal.JournalPath,
                "Installer package journal paths are outside the supported recovery layout.");
        }
    }

    /// <summary>
    /// 备份存在时恢复旧包；没有备份时只清理残留 staging。
    /// </summary>
    private static void RestoreBackupIfPresent(
        InstallerTransactionJournal journal,
        string targetPath,
        string backupPath)
    {
        if (Directory.Exists(backupPath))
        {
            RestoreBackup(journal, targetPath, backupPath);
        }
    }

    /// <summary>
    /// 在目标尚未提交或旧包已明确备份时恢复旧目录。
    /// </summary>
    private static void RestoreBackup(
        InstallerTransactionJournal journal,
        string targetPath,
        string backupPath)
    {
        if (!Directory.Exists(backupPath))
        {
            if (journal.Record.TargetOriginallyExists)
            {
                throw new IOException("Installer package backup is missing: " + backupPath);
            }

            DeleteDirectoryOrFile(targetPath);
            return;
        }

        if (Directory.Exists(targetPath) || File.Exists(targetPath))
        {
            throw new IOException("Installer package recovery found an unexpected target: " + targetPath);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        Directory.Move(backupPath, targetPath);
    }

    /// <summary>
    /// 目标已切换但未完成 post-verify 时回到事务前状态。
    /// </summary>
    private static void RestoreBackupAfterCommit(
        InstallerTransactionJournal journal,
        string targetPath,
        string backupPath)
    {
        if (Directory.Exists(backupPath))
        {
            DeleteDirectoryOrFile(targetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            Directory.Move(backupPath, targetPath);
            return;
        }

        if (journal.Record.TargetOriginallyExists)
        {
            throw new IOException("Installer package recovery cannot restore the original package: " + backupPath);
        }

        DeleteDirectoryOrFile(targetPath);
    }

    /// <summary>
    /// 幂等删除目录或文件，不跟随已验证事务路径之外的链接。
    /// </summary>
    private static void DeleteDirectoryOrFile(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
        else if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
