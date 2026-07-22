using System.Text.Json;

namespace YokiFrame.Installer.Core.Services;

/// <summary>
/// 承载包安装事务失败后的目录恢复、清理与诊断证据写入。
/// </summary>
public sealed partial class PackageInstallTransactionService
{
    /// <summary>
    /// 按实际越过的提交边界恢复旧包或删除新包，并清理 staging/backup 事务目录。
    /// </summary>
    /// <param name="context">事务上下文。</param>
    /// <returns>回滚全部步骤成功时返回 true。</returns>
    private static bool TryRollback(TransactionContext context)
    {
        try
        {
            if (context.TargetCommitted)
            {
                DeleteDirectoryIfExists(context.TargetPackageRoot);
            }

            if (context.ExistingPackageBackedUp && Directory.Exists(context.BackupPackageRoot))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(context.TargetPackageRoot)!);
                Directory.Move(context.BackupPackageRoot, context.TargetPackageRoot);
            }

            DeleteDirectoryIfExists(context.StagingTransactionRoot);
            DeleteDirectoryIfExists(context.BackupTransactionRoot);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 原子写入失败检查点、错误和回滚结果，供 UI、CLI 与人工诊断引用。
    /// </summary>
    /// <param name="context">事务上下文。</param>
    /// <param name="rollbackSucceeded">回滚结果。</param>
    /// <param name="exception">原始异常。</param>
    /// <returns>诊断 JSON 路径。</returns>
    private static string WriteFailureEvidence(
        TransactionContext context,
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
                writer.WriteString("checkpoint", context.Checkpoint.ToString());
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
    /// 删除事务创建或独占拥有的目录；不存在时保持幂等。
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
