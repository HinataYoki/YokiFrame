using System.Text;
using YokiFrame.Installer.Core.IO;
using YokiFrame.Installer.Core.Models;

namespace YokiFrame.Installer.Core.Services;

/// <summary>
/// 执行 Godot 完整 add-on 的 staging、整目录替换、外部项目文件提交和失败回滚。
/// </summary>
internal sealed partial class GodotAddonInstallTransactionService
{
    private readonly IGodotInstallFaultInjector mFaultInjector;
    private readonly GodotUidProjectionMaterializer mUidMaterializer = new();
    private readonly GodotAddonProjectionBuilder mAddonProjectionBuilder = new();
    private readonly PackageOwnerManifestStore mManifestStore = new();
    private readonly PackageOwnershipInspector mStagingOwnershipInspector = new();

    /// <summary>
    /// 创建携带指定故障注入器的完整 add-on 事务服务。
    /// </summary>
    /// <param name="faultInjector">内部测试检查点观察器。</param>
    public GodotAddonInstallTransactionService(IGodotInstallFaultInjector faultInjector)
    {
        mFaultInjector = faultInjector ?? throw new ArgumentNullException(nameof(faultInjector));
    }

    /// <summary>
    /// 提交完整 add-on 与项目 owner 文件；任何提交失败都会恢复原目录和已改写的项目文件。
    /// </summary>
    /// <param name="plan">已经完成全部只读验证的安装计划。</param>
    /// <returns>稳定的 Godot 安装结果。</returns>
    public GodotInstallResult Execute(
        GodotInstallPlan plan,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        cancellationToken.ThrowIfCancellationRequested();
        using var projectLock = InstallerProjectLock.Acquire(plan.ProjectRoot);
        return Execute(plan, projectLock, cancellationToken);
    }

    /// <summary>
    /// 在调用方已经持有项目锁时执行完整 add-on 事务。
    /// </summary>
    /// <remarks>
    /// 恢复由 GodotInstallService 在最终计划前负责；该持锁重载不重复扫描 journal。
    /// </remarks>
    /// <param name="plan">已经完成全部只读验证的安装计划。</param>
    /// <param name="projectLock">当前目标项目锁。</param>
    /// <returns>稳定的 Godot 安装结果。</returns>
    internal GodotInstallResult Execute(
        GodotInstallPlan plan,
        InstallerProjectLockLease projectLock,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        InstallerDirectorySwapTransaction.ValidateProjectLock(plan.ProjectRoot, projectLock);
        cancellationToken.ThrowIfCancellationRequested();
        GodotInstallTransactionContext context = new(plan);
        context.InitializeJournal();
        try
        {
            var packageProjection = mUidMaterializer.Materialize(
                plan.Projection,
                plan.PackageUidSidecars,
                context.GeneratedPackageRoot);
            var addonProjection = mAddonProjectionBuilder.Build(
                packageProjection,
                plan,
                context.GeneratedAddonRoot);
            StageAddon(context, addonProjection, cancellationToken);
            StageProjectFiles(context, cancellationToken);
            BackupProjectFiles(context, cancellationToken);
            BackupExistingAddon(context, cancellationToken);
            CommitAddon(context, cancellationToken);
            CommitProjectFiles(context);
            VerifyCommittedState(context);
            CompleteTransaction(context);
            return CreateResult(plan, context);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested && !context.CommitStarted)
        {
            var rollbackSucceeded = TryRollback(context);
            rollbackSucceeded = InstallerDirectorySwapTransaction.CompleteFailureJournal(
                context.Journal,
                rollbackSucceeded);
            if (rollbackSucceeded)
            {
                throw;
            }

            var cancellation = new OperationCanceledException(cancellationToken);
            var evidencePath = WriteFailureEvidence(context, rollbackSucceeded, cancellation);
            throw new GodotInstallException(
                "Godot transaction cancellation rollback was incomplete.",
                evidencePath,
                rollbackSucceeded,
                cancellation);
        }
        catch (Exception exception)
        {
            var rollbackSucceeded = TryRollback(context);
            rollbackSucceeded = InstallerDirectorySwapTransaction.CompleteFailureJournal(
                context.Journal,
                rollbackSucceeded);
            var evidencePath = WriteFailureEvidence(context, rollbackSucceeded, exception);
            throw new GodotInstallException(
                "Godot installation failed at " + GetCheckpointName(context) + ".",
                evidencePath,
                rollbackSucceeded,
                exception);
        }
    }

    /// <summary>
    /// 将完整 add-on 投影复制到隔离 staging 并复验；原子操作由共享目录交换事务承载。
    /// </summary>
    /// <param name="context">当前目录替换事务上下文。</param>
    /// <param name="projection">以 add-on 根为起点的最终投影。</param>
    private void StageAddon(
        GodotInstallTransactionContext context,
        PackageProjection projection,
        CancellationToken cancellationToken)
    {
        InstallerDirectorySwapTransaction.StageFiles(
            context.StagingAddonRoot,
            projection,
            mManifestStore,
            mStagingOwnershipInspector,
            "Godot add-on transaction",
            cancellationToken);
        AdvanceCheckpoint(context, GodotInstallCheckpoint.AddonStagingVerified);
    }

    /// <summary>
    /// 将 add-on 根外的项目 owner 文件写入 staging，并复验完整文本。
    /// </summary>
    /// <param name="context">当前目录替换事务上下文。</param>
    private static void StageProjectFiles(
        GodotInstallTransactionContext context,
        CancellationToken cancellationToken)
    {
        foreach (var entry in context.ProjectFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            WriteTextDurably(entry.StagedPath, entry.Content);
            if (!string.Equals(File.ReadAllText(entry.StagedPath), entry.Content, StringComparison.Ordinal))
            {
                throw new IOException("Godot staged project file verification failed: " + entry.TargetPath);
            }
        }
    }

    /// <summary>
    /// 在写正式项目文件前备份其原始内容，使 add-on 替换后的失败仍能恢复项目引用和设置。
    /// </summary>
    /// <param name="context">当前目录替换事务上下文。</param>
    private static void BackupProjectFiles(
        GodotInstallTransactionContext context,
        CancellationToken cancellationToken)
    {
        foreach (var entry in context.ProjectFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!entry.OriginalExists)
            {
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(entry.BackupPath)!);
            File.Copy(entry.TargetPath, entry.BackupPath, overwrite: false);
        }
    }

    private void BackupExistingAddon(
        GodotInstallTransactionContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        context.AddonOriginallyExists = Directory.Exists(context.AddonRoot);
        if (!context.AddonOriginallyExists)
        {
            return;
        }

        InstallerDirectorySwapTransaction.BackupExistingDirectory(context.AddonRoot, context.BackupAddonRoot);
        context.ExistingAddonBackedUp = true;
        AdvanceCheckpoint(context, GodotInstallCheckpoint.ExistingAddonBackedUp);
    }

    /// <summary>将已经校验的 staging add-on 整目录移动到正式位置，避免旧新文件产生混合状态。</summary>
    private void CommitAddon(
        GodotInstallTransactionContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        context.CommitStarted = true;
        InstallerDirectorySwapTransaction.CommitStagedDirectory(
            context.StagingAddonRoot,
            context.AddonRoot);
        context.AddonCommitted = true;
        AdvanceCheckpoint(context, GodotInstallCheckpoint.AddonCommitted);
    }

    /// <summary>
    /// 原子替换全部外部项目 owner 文件，并在每个稳定边界允许测试注入故障。
    /// </summary>
    /// <param name="context">当前目录替换事务上下文。</param>
    private void CommitProjectFiles(GodotInstallTransactionContext context)
    {
        foreach (var entry in context.ProjectFiles)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(entry.TargetPath)!);
            entry.Committed = true;
            // 先持久化提交意图，再切换正式文件；崩溃发生在两步之间时恢复器仍会还原原文。
            context.Journal?.MarkProjectFileCommitted(entry.TargetPath);
            File.Move(entry.StagedPath, entry.TargetPath, overwrite: true);
            AdvanceCheckpoint(context, entry.Checkpoint);
        }
    }

    /// <summary>
    /// 复验正式 add-on 和项目 owner 文件，确保不能只因 staging 成功就报告安装完成。
    /// </summary>
    /// <param name="context">当前目录替换事务上下文。</param>
    private void VerifyCommittedState(GodotInstallTransactionContext context)
    {
        InstallerDirectorySwapTransaction.EnsureOwnershipClean(
            context.AddonRoot,
            mStagingOwnershipInspector,
            "Godot committed add-on verification failed");

        foreach (var entry in context.ProjectFiles)
        {
            if (!string.Equals(File.ReadAllText(entry.TargetPath), entry.Content, StringComparison.Ordinal))
            {
                throw new IOException("Godot committed project file verification failed: " + entry.TargetPath);
            }
        }
    }

    /// <summary>
    /// 清理成功事务的 staging 与备份目录；清理失败不撤销已经完成的安装。
    /// </summary>
    /// <param name="context">已完成验证的事务上下文。</param>
    private static void CompleteTransaction(GodotInstallTransactionContext context)
    {
        context.Journal?.Advance(InstallerTransactionPhase.PostVerified);
        if (TryDeleteDirectory(context.TransactionRoot))
        {
            context.Journal?.Complete();
        }
    }

    /// <summary>
    /// 使用 UTF-8 无 BOM 与 WriteThrough 写入项目文件 staging，确保提交前流已关闭并刷新。
    /// </summary>
    /// <param name="path">staging 目标路径。</param>
    /// <param name="content">完整文本内容。</param>
    private static void WriteTextDurably(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using FileStream stream = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough);
        using StreamWriter writer = new(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(content);
        writer.Flush();
        stream.Flush(flushToDisk: true);
    }

    /// <summary>
    /// 更新稳定检查点并通知测试故障注入器。
    /// </summary>
    /// <param name="context">当前事务上下文。</param>
    /// <param name="checkpoint">刚完成的稳定边界。</param>
    private void AdvanceCheckpoint(GodotInstallTransactionContext context, GodotInstallCheckpoint checkpoint)
    {
        context.Checkpoint = checkpoint;
        if (context.Journal != null)
        {
            context.Journal.Advance(checkpoint switch
            {
                GodotInstallCheckpoint.AddonStagingVerified => InstallerTransactionPhase.StagingVerified,
                GodotInstallCheckpoint.ExistingAddonBackedUp => InstallerTransactionPhase.ExistingTargetBackedUp,
                GodotInstallCheckpoint.AddonCommitted => InstallerTransactionPhase.TargetCommitted,
                GodotInstallCheckpoint.ProjectFileCommitted or GodotInstallCheckpoint.ProjectSettingsCommitted
                    => InstallerTransactionPhase.ProjectFilesCommitted,
                _ => throw new ArgumentOutOfRangeException(nameof(checkpoint), checkpoint, "Unsupported Godot transaction checkpoint.")
            });
        }
        mFaultInjector.OnCheckpoint(checkpoint);
    }

    /// <summary>
    /// 从事务上下文创建成功结果，避免提交后进行新的可失败业务计算。
    /// </summary>
    /// <param name="plan">已执行的安装计划。</param>
    /// <param name="context">已成功提交的事务上下文。</param>
    /// <returns>供 Application、CLI 和 UI 消费的安装结果。</returns>
    private PackageInstallTransactionResult CreatePackageResult(
        GodotInstallPlan plan,
        GodotInstallTransactionContext context)
    {
        return new PackageInstallTransactionResult(
            plan.AddonRoot,
            mManifestStore.GetManifestPath(plan.AddonRoot),
            context.AddonOriginallyExists);
    }

    /// <summary>
    /// 将 add-on 事务结果与计划中的稳定入口路径组合为 Godot 安装结果。
    /// </summary>
    /// <param name="plan">已执行的安装计划。</param>
    /// <param name="context">已成功提交的事务上下文。</param>
    /// <returns>完整安装结果。</returns>
    private GodotInstallResult CreateResult(GodotInstallPlan plan, GodotInstallTransactionContext context)
    {
        return new GodotInstallResult(
            CreatePackageResult(plan, context),
            plan.ProjectFilePath,
            plan.ProjectSettingsPath,
            plan.PluginConfigPath,
            plan.PluginScriptPath,
            plan.PluginScriptUidPath,
            plan.RuntimeBootstrapPath,
            plan.RuntimeBootstrapUidPath);
    }

    /// <summary>
    /// 获取当前稳定检查点名称；尚未越过任何边界时返回 Preparing。
    /// </summary>
    /// <param name="context">当前事务上下文。</param>
    /// <returns>供异常和诊断使用的阶段名。</returns>
    private static string GetCheckpointName(GodotInstallTransactionContext context)
    {
        return context.Checkpoint?.ToString() ?? "Preparing";
    }
}
