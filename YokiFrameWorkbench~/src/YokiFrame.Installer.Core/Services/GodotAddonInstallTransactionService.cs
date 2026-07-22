using System.Security.Cryptography;
using System.Text;
using YokiFrame.Installer.Core.IO;
using YokiFrame.Installer.Core.Models;

namespace YokiFrame.Installer.Core.Services;

/// <summary>
/// 执行 Godot 完整 add-on 的 staging、整目录替换、外部项目文件提交和失败回滚。
/// </summary>
internal sealed partial class GodotAddonInstallTransactionService
{
    private const int DIRECTORY_MOVE_MAX_ATTEMPTS = 20;
    private const int DIRECTORY_MOVE_RETRY_DELAY_MILLISECONDS = 100;

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
    public GodotInstallResult Execute(GodotInstallPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        GodotInstallTransactionContext context = new(plan);
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
            StageAddon(context, addonProjection);
            StageProjectFiles(context);
            BackupProjectFiles(context);
            BackupExistingAddon(context);
            CommitAddon(context);
            CommitProjectFiles(context);
            VerifyCommittedState(context);
            CompleteTransaction(context);
            return CreateResult(plan, context);
        }
        catch (Exception exception)
        {
            var rollbackSucceeded = TryRollback(context);
            var evidencePath = WriteFailureEvidence(context, rollbackSucceeded, exception);
            throw new GodotInstallException(
                "Godot installation failed at " + GetCheckpointName(context) + ".",
                evidencePath,
                rollbackSucceeded,
                exception);
        }
    }

    /// <summary>
    /// 将完整 add-on 投影复制到隔离 staging，逐文件校验后写入 add-on 级 owner manifest。
    /// </summary>
    /// <param name="context">当前目录替换事务上下文。</param>
    /// <param name="projection">以 add-on 根为起点的最终投影。</param>
    private void StageAddon(GodotInstallTransactionContext context, PackageProjection projection)
    {
        Directory.CreateDirectory(context.StagingAddonRoot);
        foreach (var file in projection.Files)
        {
            var targetPath = InstallerPathGuard.CombineInside(
                context.StagingAddonRoot,
                file.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            File.Copy(file.SourcePath, targetPath, overwrite: false);
            VerifyProjectedFile(targetPath, file);
        }

        mManifestStore.Write(context.StagingAddonRoot, mManifestStore.Create(projection));
        var inspection = mStagingOwnershipInspector.Inspect(context.StagingAddonRoot);
        if (inspection.State != PackageOwnershipState.Clean)
        {
            throw new IOException("Godot add-on staging verification failed: " + string.Join(", ", inspection.ConflictPaths));
        }

        AdvanceCheckpoint(context, GodotInstallCheckpoint.AddonStagingVerified);
    }

    /// <summary>
    /// 校验刚复制到 staging 的文件仍与投影中的长度和 SHA-256 一致。
    /// </summary>
    /// <param name="targetPath">staging 中的完整文件路径。</param>
    /// <param name="expected">投影中的期望摘要。</param>
    private static void VerifyProjectedFile(string targetPath, PackageProjectionFile expected)
    {
        FileInfo info = new(targetPath);
        using var stream = File.OpenRead(targetPath);
        var hash = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        if (info.Length != expected.Length || !string.Equals(hash, expected.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new IOException("Godot staged file hash mismatch: " + expected.RelativePath);
        }
    }

    /// <summary>
    /// 将 add-on 根外的项目 owner 文件写入 staging，并复验完整文本。
    /// </summary>
    /// <param name="context">当前目录替换事务上下文。</param>
    private static void StageProjectFiles(GodotInstallTransactionContext context)
    {
        foreach (var entry in context.ProjectFiles)
        {
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
    private static void BackupProjectFiles(GodotInstallTransactionContext context)
    {
        foreach (var entry in context.ProjectFiles)
        {
            entry.OriginalExists = File.Exists(entry.TargetPath);
            if (!entry.OriginalExists)
            {
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(entry.BackupPath)!);
            File.Copy(entry.TargetPath, entry.BackupPath, overwrite: false);
        }
    }

    /// <summary>
    /// 将旧 `addons/yokiframe` 整目录移入同卷备份区；不读取或比较其内部内容。
    /// </summary>
    /// <param name="context">当前目录替换事务上下文。</param>
    private void BackupExistingAddon(GodotInstallTransactionContext context)
    {
        context.AddonOriginallyExists = Directory.Exists(context.AddonRoot);
        if (!context.AddonOriginallyExists)
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(context.BackupAddonRoot)!);
        MoveDirectoryWithRetry(context.AddonRoot, context.BackupAddonRoot);
        context.ExistingAddonBackedUp = true;
        AdvanceCheckpoint(context, GodotInstallCheckpoint.ExistingAddonBackedUp);
    }

    /// <summary>
    /// 将已经校验的 staging add-on 整目录移动到正式位置，避免旧新文件产生混合状态。
    /// </summary>
    /// <param name="context">当前目录替换事务上下文。</param>
    private void CommitAddon(GodotInstallTransactionContext context)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(context.AddonRoot)!);
        MoveDirectoryWithRetry(context.StagingAddonRoot, context.AddonRoot);
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
            File.Move(entry.StagedPath, entry.TargetPath, overwrite: true);
            entry.Committed = true;
            AdvanceCheckpoint(context, entry.Checkpoint);
        }
    }

    /// <summary>
    /// 复验正式 add-on 和项目 owner 文件，确保不能只因 staging 成功就报告安装完成。
    /// </summary>
    /// <param name="context">当前目录替换事务上下文。</param>
    private void VerifyCommittedState(GodotInstallTransactionContext context)
    {
        var inspection = mStagingOwnershipInspector.Inspect(context.AddonRoot);
        if (inspection.State != PackageOwnershipState.Clean)
        {
            throw new IOException("Godot committed add-on verification failed: " + string.Join(", ", inspection.ConflictPaths));
        }

        foreach (var entry in context.ProjectFiles)
        {
            if (!string.Equals(File.ReadAllText(entry.TargetPath), entry.Content, StringComparison.Ordinal))
            {
                throw new IOException("Godot committed project file verification failed: " + entry.TargetPath);
            }
        }
    }

    /// <summary>
    /// 在 Windows 文件扫描器短暂占用目录时有界重试同卷移动，超过窗口后交由回滚处理。
    /// </summary>
    /// <param name="sourcePath">必须仍存在的源目录。</param>
    /// <param name="destinationPath">必须尚不存在的目标目录。</param>
    private static void MoveDirectoryWithRetry(string sourcePath, string destinationPath)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                Directory.Move(sourcePath, destinationPath);
                return;
            }
            catch (Exception exception) when (CanRetryDirectoryMove(sourcePath, destinationPath, attempt, exception))
            {
                Thread.Sleep(DIRECTORY_MOVE_RETRY_DELAY_MILLISECONDS);
            }
        }
    }

    /// <summary>
    /// 只重试源目录仍存在、目标未出现的临时 IO 或访问冲突，避免掩盖真实路径错误。
    /// </summary>
    /// <param name="sourcePath">目录移动源路径。</param>
    /// <param name="destinationPath">目录移动目标路径。</param>
    /// <param name="attempt">当前尝试序号。</param>
    /// <param name="exception">本次移动异常。</param>
    /// <returns>错误仍可安全重试时返回 true。</returns>
    private static bool CanRetryDirectoryMove(
        string sourcePath,
        string destinationPath,
        int attempt,
        Exception exception)
    {
        return attempt < DIRECTORY_MOVE_MAX_ATTEMPTS
            && exception is IOException or UnauthorizedAccessException
            && exception is not DirectoryNotFoundException
            && exception is not PathTooLongException
            && Directory.Exists(sourcePath)
            && !Directory.Exists(destinationPath)
            && !File.Exists(destinationPath);
    }

    /// <summary>
    /// 清理成功事务的 staging 与备份目录；清理失败不撤销已经完成的安装。
    /// </summary>
    /// <param name="context">已完成验证的事务上下文。</param>
    private static void CompleteTransaction(GodotInstallTransactionContext context)
    {
        _ = TryDeleteDirectory(context.TransactionRoot);
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
