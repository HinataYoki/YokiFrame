using YokiFrame.Packaging.Models;

namespace YokiFrame.Packaging.Services;

/// <summary>
/// 通过备份和阶段 marker 提交平台 profile，使进程中断后能够恢复到一致状态。
/// </summary>
internal static class RuntimePublishTransaction
{
    private const string PROFILE_BACKUP_DIRECTORY = "profile.previous";
    private const string MANIFEST_BACKUP_FILE = "manifest.previous";
    private const string PROFILE_MISSING_MARKER = "profile-missing.marker";
    private const string MANIFEST_MISSING_MARKER = "manifest-missing.marker";
    private const string PROFILE_BACKED_UP_MARKER = "profile-backed-up.marker";
    private const string PROFILE_SWITCH_STARTED_MARKER = "profile-switch-started.marker";
    private const string PROFILE_COMMITTED_MARKER = "profile-committed.marker";
    private const string MANIFEST_COMMITTED_MARKER = "manifest-committed.marker";

    /// <summary>
    /// 提交 staging profile，并在 manifest 提交失败时立即恢复旧 profile 和旧 manifest。
    /// </summary>
    /// <param name="plan">当前平台发布计划。</param>
    /// <param name="commitManifest">基于新 profile 写入共享 manifest 的动作。</param>
    internal static void Commit(RuntimePublishPlan plan, Action commitManifest)
    {
        var transactionRoot = GetTransactionRoot(plan);
        Directory.CreateDirectory(transactionRoot);
        try
        {
            BackupManifest(plan, transactionRoot);
            BackupProfile(plan, transactionRoot);
            CreateMarker(transactionRoot, PROFILE_SWITCH_STARTED_MARKER);
            Directory.Move(plan.StagingRoot, plan.PublishRoot);
            CreateMarker(transactionRoot, PROFILE_COMMITTED_MARKER);
            commitManifest();
            CreateMarker(transactionRoot, MANIFEST_COMMITTED_MARKER);
            Directory.Delete(transactionRoot, true);
        }
        catch (Exception commitException)
        {
            RecoverAfterFailure(plan, commitException);
            throw;
        }
    }

    /// <summary>
    /// 在新发布开始前处理上次中断事务；manifest 已提交则完成清理，否则回滚到旧状态。
    /// </summary>
    /// <param name="plan">当前平台发布计划。</param>
    internal static void Recover(RuntimePublishPlan plan)
    {
        var transactionRoot = GetTransactionRoot(plan);
        if (!Directory.Exists(transactionRoot))
        {
            return;
        }

        if (HasMarker(transactionRoot, MANIFEST_COMMITTED_MARKER))
        {
            Directory.Delete(transactionRoot, true);
            return;
        }

        RestoreProfile(plan, transactionRoot);
        RestoreManifest(plan, transactionRoot);
        Directory.Delete(transactionRoot, true);
    }

    /// <summary>
    /// 保存旧 manifest 或记录其原本不存在，确保 profile 切换后可恢复共享索引。
    /// </summary>
    /// <param name="plan">当前发布计划。</param>
    /// <param name="transactionRoot">事务目录。</param>
    private static void BackupManifest(RuntimePublishPlan plan, string transactionRoot)
    {
        if (!File.Exists(plan.ManifestPath))
        {
            CreateMarker(transactionRoot, MANIFEST_MISSING_MARKER);
            return;
        }

        CopyFileAndFlush(plan.ManifestPath, Path.Combine(transactionRoot, MANIFEST_BACKUP_FILE));
    }

    /// <summary>
    /// 将旧 profile 移入事务目录；不存在旧 profile 时写入明确 marker。
    /// </summary>
    /// <param name="plan">当前发布计划。</param>
    /// <param name="transactionRoot">事务目录。</param>
    private static void BackupProfile(RuntimePublishPlan plan, string transactionRoot)
    {
        if (!Directory.Exists(plan.PublishRoot))
        {
            CreateMarker(transactionRoot, PROFILE_MISSING_MARKER);
            return;
        }

        Directory.Move(plan.PublishRoot, Path.Combine(transactionRoot, PROFILE_BACKUP_DIRECTORY));
        CreateMarker(transactionRoot, PROFILE_BACKED_UP_MARKER);
    }

    /// <summary>
    /// 恢复旧 profile；首次发布中断时只删除尚未完成 manifest 提交的新 profile。
    /// </summary>
    /// <param name="plan">当前发布计划。</param>
    /// <param name="transactionRoot">事务目录。</param>
    private static void RestoreProfile(RuntimePublishPlan plan, string transactionRoot)
    {
        var backupRoot = Path.Combine(transactionRoot, PROFILE_BACKUP_DIRECTORY);
        if (Directory.Exists(backupRoot))
        {
            DeleteDirectoryIfExists(plan.PublishRoot);
            Directory.Move(backupRoot, plan.PublishRoot);
            return;
        }

        if (HasMarker(transactionRoot, PROFILE_MISSING_MARKER)
            && HasProfileSwitchStarted(transactionRoot))
        {
            DeleteDirectoryIfExists(plan.PublishRoot);
        }
    }

    /// <summary>
    /// 恢复旧 manifest；首次发布中断且可能已经写入新 manifest 时删除该不完整索引。
    /// </summary>
    /// <param name="plan">当前发布计划。</param>
    /// <param name="transactionRoot">事务目录。</param>
    private static void RestoreManifest(RuntimePublishPlan plan, string transactionRoot)
    {
        var backupPath = Path.Combine(transactionRoot, MANIFEST_BACKUP_FILE);
        if (File.Exists(backupPath))
        {
            ReplaceFile(backupPath, plan.ManifestPath);
            return;
        }

        if (HasMarker(transactionRoot, MANIFEST_MISSING_MARKER)
            && HasProfileSwitchStarted(transactionRoot)
            && File.Exists(plan.ManifestPath))
        {
            File.Delete(plan.ManifestPath);
        }
    }

    /// <summary>
    /// 提交异常后执行同步恢复；恢复本身失败时同时保留提交和恢复错误。
    /// </summary>
    /// <param name="plan">当前发布计划。</param>
    /// <param name="commitException">原始提交异常。</param>
    private static void RecoverAfterFailure(RuntimePublishPlan plan, Exception commitException)
    {
        try
        {
            Recover(plan);
        }
        catch (Exception recoveryException)
        {
            throw new InvalidOperationException(
                "WorkbenchRuntime publish failed and its previous state could not be restored.",
                new AggregateException(commitException, recoveryException));
        }
    }

    /// <summary>
    /// 复制 manifest 备份并强制刷新，确保 marker 出现前备份已经落盘。
    /// </summary>
    /// <param name="sourcePath">正式 manifest。</param>
    /// <param name="targetPath">事务备份路径。</param>
    private static void CopyFileAndFlush(string sourcePath, string targetPath)
    {
        using var source = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var target = new FileStream(targetPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        source.CopyTo(target);
        target.Flush(true);
    }

    /// <summary>
    /// 使用同卷替换恢复 manifest；目标不存在时直接移动备份。
    /// </summary>
    /// <param name="backupPath">事务中的旧 manifest。</param>
    /// <param name="targetPath">正式 manifest 路径。</param>
    private static void ReplaceFile(string backupPath, string targetPath)
    {
        if (File.Exists(targetPath))
        {
            File.Replace(backupPath, targetPath, null, true);
            return;
        }

        File.Move(backupPath, targetPath);
    }

    /// <summary>
    /// 写入并强制刷新空 marker；marker 只在其前置文件操作完成后创建。
    /// </summary>
    /// <param name="transactionRoot">事务目录。</param>
    /// <param name="markerName">阶段 marker 文件名。</param>
    private static void CreateMarker(string transactionRoot, string markerName)
    {
        using var stream = new FileStream(
            Path.Combine(transactionRoot, markerName),
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None);
        stream.Flush(true);
    }

    /// <summary>
    /// 判断事务阶段 marker 是否已经持久化。
    /// </summary>
    /// <param name="transactionRoot">事务目录。</param>
    /// <param name="markerName">marker 文件名。</param>
    /// <returns>marker 存在时返回 true。</returns>
    private static bool HasMarker(string transactionRoot, string markerName)
    {
        return File.Exists(Path.Combine(transactionRoot, markerName));
    }

    /// <summary>
    /// 判断 profile 切换是否已经开始；兼容首版事务留下的 committed marker。
    /// </summary>
    /// <param name="transactionRoot">事务目录。</param>
    /// <returns>目录移动可能已经发生时返回 true。</returns>
    private static bool HasProfileSwitchStarted(string transactionRoot)
    {
        return HasMarker(transactionRoot, PROFILE_SWITCH_STARTED_MARKER)
            || HasMarker(transactionRoot, PROFILE_COMMITTED_MARKER);
    }

    /// <summary>
    /// 计算当前 RID 的独立事务目录，避免与构建 staging 目录互相移动。
    /// </summary>
    /// <param name="plan">当前发布计划。</param>
    /// <returns>事务目录完整路径。</returns>
    private static string GetTransactionRoot(RuntimePublishPlan plan)
    {
        return Path.Combine(
            plan.RuntimeRoot,
            ".staging",
            ".transactions",
            plan.Profile.RuntimeIdentifier);
    }

    /// <summary>
    /// 删除存在的 profile 目录，供恢复过程替换未提交的新 profile。
    /// </summary>
    /// <param name="path">待删除目录。</param>
    private static void DeleteDirectoryIfExists(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, true);
        }
    }
}
