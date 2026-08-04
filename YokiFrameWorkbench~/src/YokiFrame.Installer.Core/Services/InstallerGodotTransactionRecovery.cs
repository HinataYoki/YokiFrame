using YokiFrame.Installer.Core.Models;

namespace YokiFrame.Installer.Core.Services;

/// <summary>
/// 在取得项目锁后恢复 Godot add-on 事务留下的持久 journal。
/// </summary>
internal static class InstallerGodotTransactionRecovery
{
    private const string TRANSACTION_KIND = "godot-addon";
    private const string ADDON_TARGET = "addons/yokiframe";
    private const string GODOT_CLEANUP_PREFIX = ".yokiframe/installer/godot/";
    private const string PROJECT_FILE_BACKUP_PREFIX = "backup/project-files/";

    /// <summary>
    /// 恢复所有可识别的 Godot journal；不确定状态会阻止下一次写入。
    /// </summary>
    /// <param name="projectRoot">目标 Godot 项目根。</param>
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
    /// 按最后持久阶段恢复 add-on、项目 owner 文件和事务辅助目录。
    /// </summary>
    /// <param name="journal">待恢复的 Godot journal。</param>
    private static void RecoverOne(InstallerTransactionJournal journal)
    {
        ValidateGodotJournal(journal);
        var targetPath = journal.ResolvePath(journal.Record.TargetRelativePath);
        var stagingPath = journal.ResolvePath(journal.Record.StagingRelativePath);
        var backupPath = journal.ResolvePath(journal.Record.BackupRelativePath);
        var cleanupPath = journal.Record.CleanupRootRelativePath == null
            ? throw new InstallerRecoveryRequiredException(
                journal.ProjectRoot,
                journal.JournalPath,
                "Godot transaction journal is missing its cleanup root.")
            : journal.ResolvePath(journal.Record.CleanupRootRelativePath);

        try
        {
            switch (journal.Record.Phase)
            {
                case InstallerTransactionPhase.Prepared:
                case InstallerTransactionPhase.StagingVerified:
                    // 旧 add-on 可能已经移动到 backup，但 checkpoint 尚未来得及更新。
                    RestoreOriginalAddon(journal, targetPath, backupPath);
                    DeleteDirectoryOrFile(stagingPath);
                    break;
                case InstallerTransactionPhase.ExistingTargetBackedUp:
                    // add-on 目录切换可能已经完成但 checkpoint 尚未来得及更新。
                    RestoreCommittedAddon(journal, targetPath, backupPath);
                    DeleteDirectoryOrFile(stagingPath);
                    break;
                case InstallerTransactionPhase.TargetCommitted:
                case InstallerTransactionPhase.ProjectFilesCommitted:
                    RestoreCommittedProjectFiles(journal);
                    RestoreCommittedAddon(journal, targetPath, backupPath);
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
                        "Godot transaction is marked RecoveryRequired: " + journal.JournalPath);
                default:
                    throw new ArgumentOutOfRangeException();
            }

            DeleteDirectoryOrFile(cleanupPath);
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
                "Godot transaction recovery failed: " + exception.Message);
        }
    }

    /// <summary>
    /// 校验被持久化的路径只能指向 Godot add-on 事务的固定布局。
    /// </summary>
    /// <param name="journal">待读取的 journal。</param>
    private static void ValidateGodotJournal(InstallerTransactionJournal journal)
    {
        var target = Normalize(journal.Record.TargetRelativePath);
        var staging = Normalize(journal.Record.StagingRelativePath);
        var backup = Normalize(journal.Record.BackupRelativePath);
        var cleanup = journal.Record.CleanupRootRelativePath == null
            ? null
            : Normalize(journal.Record.CleanupRootRelativePath);
        var expectedCleanup = GODOT_CLEANUP_PREFIX + journal.Record.TransactionId;
        var expectedStaging = expectedCleanup + "/staging/addon/yokiframe";
        var expectedBackup = expectedCleanup + "/backup/addon/yokiframe";
        if (!string.Equals(target, ADDON_TARGET, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(cleanup, expectedCleanup, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(staging, expectedStaging, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(backup, expectedBackup, StringComparison.OrdinalIgnoreCase))
        {
            throw new InstallerRecoveryRequiredException(
                journal.ProjectRoot,
                journal.JournalPath,
                "Godot transaction journal paths are outside the supported recovery layout.");
        }

        foreach (var file in journal.Record.ProjectFiles)
        {
            var targetFile = Normalize(file.TargetRelativePath);
            var backupFile = Normalize(file.BackupRelativePath);
            if (!IsSupportedProjectFile(targetFile)
                || !backupFile.StartsWith(expectedCleanup + "/" + PROJECT_FILE_BACKUP_PREFIX, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(
                    Path.GetFileName(targetFile),
                    Path.GetFileName(backupFile),
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InstallerRecoveryRequiredException(
                    journal.ProjectRoot,
                    journal.JournalPath,
                    "Godot transaction project file paths are outside the supported recovery layout.");
            }
        }
    }

    /// <summary>
    /// 恢复 add-on 提交前的旧目录状态。
    /// </summary>
    private static void RestoreOriginalAddon(
        InstallerTransactionJournal journal,
        string targetPath,
        string backupPath)
    {
        if (Directory.Exists(backupPath))
        {
            if (Directory.Exists(targetPath) || File.Exists(targetPath))
            {
                throw new IOException("Godot recovery found an unexpected add-on target: " + targetPath);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            Directory.Move(backupPath, targetPath);
            return;
        }

        if (journal.Record.TargetOriginallyExists)
        {
            throw new IOException("Godot add-on backup is missing: " + backupPath);
        }

        DeleteDirectoryOrFile(targetPath);
    }

    /// <summary>
    /// 恢复 add-on 已切换后的旧目录状态。
    /// </summary>
    private static void RestoreCommittedAddon(
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
            throw new IOException("Godot recovery cannot restore the original add-on: " + backupPath);
        }

        DeleteDirectoryOrFile(targetPath);
    }

    /// <summary>
    /// 反向恢复已在 journal 中确认提交的项目 owner 文件。
    /// </summary>
    private static void RestoreCommittedProjectFiles(InstallerTransactionJournal journal)
    {
        for (var index = journal.Record.ProjectFiles.Count - 1; index >= 0; index--)
        {
            var file = journal.Record.ProjectFiles[index];
            if (!file.Committed)
            {
                continue;
            }

            var targetPath = journal.ResolvePath(file.TargetRelativePath);
            var backupPath = journal.ResolvePath(file.BackupRelativePath);
            if (!file.OriginalExists)
            {
                DeleteDirectoryOrFile(targetPath);
                continue;
            }

            if (!File.Exists(backupPath))
            {
                throw new IOException("Godot project file backup is missing: " + backupPath);
            }

            File.Move(backupPath, targetPath, overwrite: true);
        }
    }

    /// <summary>
    /// 幂等删除经过 journal 路径守卫的文件或目录。
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

    /// <summary>
    /// 只允许 Godot 当前事务拥有的顶层 project.godot 或主 csproj。
    /// </summary>
    private static bool IsSupportedProjectFile(string path)
    {
        if (path.Contains("/", StringComparison.Ordinal))
        {
            return false;
        }

        return string.Equals(path, "project.godot", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 统一 journal 路径的分隔符并去除末尾分隔符。
    /// </summary>
    private static string Normalize(string path)
    {
        return path.Replace('\\', '/').TrimEnd('/');
    }
}
