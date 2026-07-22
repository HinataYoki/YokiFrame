using System.Text.Json;

namespace YokiFrame.Installer.Core.Services;

/// <summary>
/// 承载 Godot add-on 事务失败后的项目文件恢复、目录回滚与诊断证据写入。
/// </summary>
internal sealed partial class GodotAddonInstallTransactionService
{
    /// <summary>
    /// 以反向项目文件顺序和整目录恢复顺序回滚当前事务。
    /// </summary>
    /// <param name="context">发生异常的事务上下文。</param>
    /// <returns>所有已提交状态都成功恢复时返回 true。</returns>
    private static bool TryRollback(GodotInstallTransactionContext context)
    {
        var rollbackSucceeded = RestoreProjectFiles(context);
        if (rollbackSucceeded)
        {
            rollbackSucceeded = RestoreAddon(context);
        }

        return rollbackSucceeded && TryDeleteDirectory(context.TransactionRoot);
    }

    /// <summary>
    /// 按反向顺序恢复所有已提交的外部项目 owner 文件。
    /// </summary>
    /// <param name="context">当前事务上下文。</param>
    /// <returns>全部文件恢复成功时返回 true。</returns>
    private static bool RestoreProjectFiles(GodotInstallTransactionContext context)
    {
        for (var index = context.ProjectFiles.Count - 1; index >= 0; index--)
        {
            try
            {
                RestoreProjectFile(context.ProjectFiles[index]);
            }
            catch
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 恢复一个已提交项目文件；事务前不存在时删除本次新建文件。
    /// </summary>
    /// <param name="entry">项目文件事务项。</param>
    private static void RestoreProjectFile(GodotProjectFileTransactionEntry entry)
    {
        if (!entry.Committed)
        {
            return;
        }

        if (!entry.OriginalExists)
        {
            if (File.Exists(entry.TargetPath))
            {
                File.Delete(entry.TargetPath);
            }

            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(entry.TargetPath)!);
        File.Move(entry.BackupPath, entry.TargetPath, overwrite: true);
    }

    /// <summary>
    /// 删除新 add-on 并将备份目录移回正式位置；未安装时只删除本次新建目录。
    /// </summary>
    /// <param name="context">当前目录替换事务上下文。</param>
    /// <returns>目录状态恢复成功时返回 true。</returns>
    private static bool RestoreAddon(GodotInstallTransactionContext context)
    {
        try
        {
            if (context.AddonCommitted)
            {
                DeleteDirectoryIfExists(context.AddonRoot);
            }

            if (context.ExistingAddonBackedUp && Directory.Exists(context.BackupAddonRoot))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(context.AddonRoot)!);
                MoveDirectoryWithRetry(context.BackupAddonRoot, context.AddonRoot);
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 把错误、检查点和回滚结果持久化为独立诊断 JSON。
    /// </summary>
    /// <param name="context">失败事务上下文。</param>
    /// <param name="rollbackSucceeded">回滚是否成功。</param>
    /// <param name="exception">原始异常。</param>
    /// <returns>诊断文件完整路径。</returns>
    private static string WriteFailureEvidence(
        GodotInstallTransactionContext context,
        bool rollbackSucceeded,
        Exception exception)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(context.DiagnosticEvidencePath)!);
        var temporaryPath = context.DiagnosticEvidencePath + ".tmp";
        try
        {
            using (FileStream stream = new(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            using (Utf8JsonWriter writer = new(stream, new JsonWriterOptions { Indented = true }))
            {
                writer.WriteStartObject();
                writer.WriteString("transactionId", context.TransactionId);
                writer.WriteString("checkpoint", GetCheckpointName(context));
                writer.WriteBoolean("rollbackSucceeded", rollbackSucceeded);
                writer.WriteString("error", exception.Message);
                writer.WriteEndObject();
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, context.DiagnosticEvidencePath, overwrite: true);
            return context.DiagnosticEvidencePath;
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    /// <summary>
    /// 删除事务独占目录；调用方只对经过路径守卫计算的目录调用此方法。
    /// </summary>
    /// <param name="path">待清理目录。</param>
    /// <returns>不存在或删除成功时返回 true。</returns>
    private static bool TryDeleteDirectory(string path)
    {
        try
        {
            DeleteDirectoryIfExists(path);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>
    /// 删除已确认由当前事务创建或替换的目录；不存在时保持幂等。
    /// </summary>
    /// <param name="path">待删除目录。</param>
    private static void DeleteDirectoryIfExists(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }
}
