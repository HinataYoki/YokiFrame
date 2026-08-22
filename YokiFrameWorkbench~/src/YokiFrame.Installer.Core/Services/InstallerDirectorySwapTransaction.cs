using System.Security.Cryptography;
using YokiFrame.Installer.Core.IO;
using YokiFrame.Installer.Core.Models;

namespace YokiFrame.Installer.Core.Services;

/// <summary>
/// 提供 Unity 包目录替换与 Godot add-on 目录替换共用的目录交换原子操作：
/// 锁校验、投影文件哈希复验、staging 复制与所有权验证、备份移动、提交移动、
/// 提交后所有权复验以及 journal 成功/失败收尾。两个引擎服务只保留各自的编排顺序、
/// 检查点枚举和引擎特有步骤（Godot 项目 owner 文件），不再维护第二份相同实现。
/// </summary>
internal static class InstallerDirectorySwapTransaction
{
    /// <summary>确认调用方传入的项目锁属于当前事务项目，避免跨项目误用锁租约。</summary>
    /// <param name="projectRoot">当前事务项目根。</param>
    /// <param name="projectLock">调用方持有的项目锁租约。</param>
    public static void ValidateProjectLock(
        string projectRoot,
        InstallerProjectLockLease projectLock)
    {
        ArgumentNullException.ThrowIfNull(projectLock);
        var fullProjectRoot = InstallerPathGuard.RequireFullPath(projectRoot, nameof(projectRoot));
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        if (!string.Equals(fullProjectRoot, projectLock.ProjectRoot, comparison))
        {
            throw new InvalidOperationException("Installer project lock belongs to a different project.");
        }
    }

    /// <summary>校验 staging 文件长度和 SHA-256，捕获复制期间的源文件变化或磁盘写入损坏。</summary>
    /// <param name="targetPath">staging 文件完整路径。</param>
    /// <param name="expected">投影中的期望摘要。</param>
    /// <param name="errorPrefix">异常消息前缀，区分 Unity/Godot 诊断来源。</param>
    public static void VerifyProjectedFile(
        string targetPath,
        PackageProjectionFile expected,
        string errorPrefix)
    {
        using var stream = File.OpenRead(targetPath);
        var hash = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        if (stream.Length != expected.Length
            || !string.Equals(hash, expected.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new IOException(errorPrefix + " staged file hash mismatch: " + expected.RelativePath);
        }
    }

    /// <summary>
    /// 将投影复制到隔离 staging：逐文件取消检查、目录创建、复制、哈希复验，
    /// 随后写入 owner manifest 并对 staging 做一次 Clean 所有权复验。
    /// </summary>
    public static void StageFiles(
        string stagingPackageRoot,
        PackageProjection projection,
        PackageOwnerManifestStore manifestStore,
        PackageOwnershipInspector ownershipInspector,
        string errorPrefix,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(stagingPackageRoot);
        foreach (var file in projection.Files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var targetPath = InstallerPathGuard.CombineInside(
                stagingPackageRoot,
                file.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            File.Copy(file.SourcePath, targetPath, overwrite: false);
            VerifyProjectedFile(targetPath, file, errorPrefix);
        }

        manifestStore.Write(stagingPackageRoot, manifestStore.Create(projection));
        EnsureOwnershipClean(
            stagingPackageRoot,
            ownershipInspector,
            errorPrefix + " staging verification failed");
    }

    /// <summary>将已有正式目录整体移入同卷备份区；目标不存在时不做任何动作。</summary>
    /// <returns>存在并已完成备份时返回 true；原本不存在返回 false。</returns>
    public static bool BackupExistingDirectory(string targetRoot, string backupRoot)
    {
        if (!Directory.Exists(targetRoot))
        {
            return false;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(backupRoot)!);
        InstallerDirectoryTransaction.MoveWithRetry(targetRoot, backupRoot);
        return true;
    }

    /// <summary>把已校验的 staging 整目录移动为正式位置，目录移动限定在同一项目卷内。</summary>
    public static void CommitStagedDirectory(string stagingRoot, string targetRoot)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(targetRoot)!);
        InstallerDirectoryTransaction.MoveWithRetry(stagingRoot, targetRoot);
    }

    /// <summary>对指定目录执行 Clean 所有权复验；非 Clean 视为该阶段失败。</summary>
    public static void EnsureOwnershipClean(
        string root,
        PackageOwnershipInspector ownershipInspector,
        string errorMessage)
    {
        var inspection = ownershipInspector.Inspect(root);
        if (inspection.State != PackageOwnershipState.Clean)
        {
            throw new IOException(errorMessage + ": " + string.Join(", ", inspection.ConflictPaths));
        }
    }

    /// <summary>在失败回滚后删除已恢复 journal；回滚或 journal 写入失败时保留 RecoveryRequired 证据。</summary>
    /// <returns>journal 收尾后仍然有效的回滚结果。</returns>
    public static bool CompleteFailureJournal(
        InstallerTransactionJournal? journal,
        bool rollbackSucceeded)
    {
        if (journal == null)
        {
            return rollbackSucceeded;
        }

        try
        {
            if (rollbackSucceeded)
            {
                journal.Complete();
            }
            else
            {
                journal.MarkRecoveryRequired();
            }
        }
        catch
        {
            rollbackSucceeded = false;
        }

        return rollbackSucceeded;
    }
}