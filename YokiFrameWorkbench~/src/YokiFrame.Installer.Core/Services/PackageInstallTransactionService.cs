using System.Security.Cryptography;
using YokiFrame.Installer.Core.IO;
using YokiFrame.Installer.Core.Models;

namespace YokiFrame.Installer.Core.Services;

/// <summary>
/// 执行文件投影的 staging、校验、备份、目录提交、复验和失败回滚事务。
/// </summary>
public sealed partial class PackageInstallTransactionService
{
    private const int DIRECTORY_MOVE_MAX_ATTEMPTS = 20;
    private const int DIRECTORY_MOVE_RETRY_DELAY_MILLISECONDS = 100;

    private readonly IPackageInstallTransactionFaultInjector mFaultInjector;
    private readonly PackageOwnerManifestStore mManifestStore = new();
    private readonly PackageOwnershipInspector mOwnershipInspector = new();

    /// <summary>
    /// 创建生产事务服务，不启用故障注入。
    /// </summary>
    public PackageInstallTransactionService()
        : this(NoOpFaultInjector.Instance)
    {
    }

    /// <summary>
    /// 创建带测试检查点观察器的事务服务。
    /// </summary>
    /// <param name="faultInjector">内部测试故障注入器。</param>
    internal PackageInstallTransactionService(IPackageInstallTransactionFaultInjector faultInjector)
    {
        mFaultInjector = faultInjector;
    }

    /// <summary>
    /// 执行一次包安装事务；输入与所有权拒绝发生在创建任何事务文件之前。
    /// </summary>
    /// <param name="projection">待提交的确定性文件投影。</param>
    /// <param name="projectRoot">目标项目根目录。</param>
    /// <param name="targetPackageRoot">正式受管包根目录。</param>
    /// <param name="policy">legacy 包接管策略。</param>
    /// <param name="replaceModifiedPackage">是否允许受管目录存在手动修改时仍执行整目录替换。</param>
    /// <returns>成功提交结果。</returns>
    public PackageInstallTransactionResult Execute(
        PackageProjection projection,
        string projectRoot,
        string targetPackageRoot,
        UnmanagedPackagePolicy policy,
        bool replaceModifiedPackage = false)
    {
        return Execute(
            projection,
            projectRoot,
            targetPackageRoot,
            policy,
            replaceModifiedPackage,
            postCommitAction: null);
    }

    /// <summary>
    /// 执行包替换，并允许调用方在新包已提交、旧包 backup 仍可恢复时完成外部持久化验证。
    /// 回调抛出异常会触发同一事务回滚，适用于 Unity manifest 这类必须与目录切换保持一致的写入。
    /// </summary>
    /// <param name="projection">待提交的确定性文件投影。</param>
    /// <param name="projectRoot">目标项目根目录。</param>
    /// <param name="targetPackageRoot">正式受管包根目录。</param>
    /// <param name="policy">legacy 包接管策略。</param>
    /// <param name="replaceModifiedPackage">是否允许受管目录存在手动修改时仍执行整目录替换。</param>
    /// <param name="postCommitAction">新包 post-verify 后、删除旧包 backup 前执行的外部提交和验证回调。</param>
    /// <returns>成功提交结果。</returns>
    internal PackageInstallTransactionResult Execute(
        PackageProjection projection,
        string projectRoot,
        string targetPackageRoot,
        UnmanagedPackagePolicy policy,
        bool replaceModifiedPackage,
        Action? postCommitAction)
    {
        var context = CreateContext(projection, projectRoot, targetPackageRoot);
        var ownership = mOwnershipInspector.Inspect(context.TargetPackageRoot);
        RejectUnsafeOwnership(ownership, policy, replaceModifiedPackage);
        context.ReplacedExistingPackage = Directory.Exists(context.TargetPackageRoot);

        try
        {
            StageProjection(context);
            MoveExistingPackageToBackup(context);
            CommitStaging(context);
            VerifyCommittedPackage(context);
            postCommitAction?.Invoke();
            CompleteTransaction(context);
            return new PackageInstallTransactionResult(
                context.TargetPackageRoot,
                mManifestStore.GetManifestPath(context.TargetPackageRoot),
                context.ReplacedExistingPackage);
        }
        catch (Exception exception) when (exception is not PackageInstallRejectedException)
        {
            var rollbackSucceeded = TryRollback(context);
            var evidencePath = WriteFailureEvidence(context, rollbackSucceeded, exception);
            throw new PackageInstallTransactionException(
                "YokiFrame package transaction failed at " + context.Checkpoint + ".",
                evidencePath,
                rollbackSucceeded,
                exception);
        }
    }

    /// <summary>
    /// 规范化并验证项目、目标包与每个投影路径，确保写入范围不会逃逸。
    /// </summary>
    /// <param name="projection">待提交投影。</param>
    /// <param name="projectRoot">目标项目根。</param>
    /// <param name="targetPackageRoot">正式包根。</param>
    /// <returns>尚未创建磁盘事务区域的上下文。</returns>
    private static TransactionContext CreateContext(
        PackageProjection projection,
        string projectRoot,
        string targetPackageRoot)
    {
        ArgumentNullException.ThrowIfNull(projection);
        var fullProjectRoot = InstallerPathGuard.RequireFullPath(projectRoot, nameof(projectRoot));
        if (!Directory.Exists(fullProjectRoot))
        {
            throw new DirectoryNotFoundException("Target project root was not found: " + fullProjectRoot);
        }

        var fullTargetRoot = InstallerPathGuard.RequireFullPath(targetPackageRoot, nameof(targetPackageRoot));
        var targetRelativePath = Path.GetRelativePath(fullProjectRoot, fullTargetRoot);
        var guardedTargetRoot = InstallerPathGuard.CombineInside(fullProjectRoot, targetRelativePath);
        ValidateProjectionPaths(projection, guardedTargetRoot);
        return new TransactionContext(projection, fullProjectRoot, guardedTargetRoot);
    }

    /// <summary>
    /// 在零写入阶段验证每个投影目标路径和源文件存在性。
    /// </summary>
    /// <param name="projection">待提交投影。</param>
    /// <param name="targetPackageRoot">受路径守卫约束的正式包根。</param>
    private static void ValidateProjectionPaths(PackageProjection projection, string targetPackageRoot)
    {
        HashSet<string> relativePaths = new(StringComparer.OrdinalIgnoreCase);
        foreach (var file in projection.Files)
        {
            _ = InstallerPathGuard.CombineInside(
                targetPackageRoot,
                file.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!relativePaths.Add(file.RelativePath))
            {
                throw new InvalidDataException("Package projection contains a duplicate path: " + file.RelativePath);
            }

            if (!File.Exists(file.SourcePath))
            {
                throw new FileNotFoundException("Package projection source file was not found.", file.SourcePath);
            }
        }
    }

    /// <summary>
    /// 根据目标所有权状态决定是否允许进入写事务。
    /// </summary>
    /// <param name="ownership">只读所有权检查结果。</param>
    /// <param name="policy">legacy 接管策略。</param>
    /// <param name="replaceModifiedPackage">是否允许覆盖已受管目录中的用户修改。</param>
    private static void RejectUnsafeOwnership(
        PackageOwnershipInspection ownership,
        UnmanagedPackagePolicy policy,
        bool replaceModifiedPackage)
    {
        if (ownership.State == PackageOwnershipState.Modified && !replaceModifiedPackage)
        {
            throw new PackageInstallRejectedException(ownership.State, ownership.ConflictPaths);
        }

        if (ownership.State == PackageOwnershipState.UnmanagedLegacy
            && policy != UnmanagedPackagePolicy.TakeOverConfirmed)
        {
            throw new PackageInstallRejectedException(ownership.State, ownership.ConflictPaths);
        }
    }

    /// <summary>
    /// 将全部投影文件和 owner manifest 写入隔离 staging，并用 manifest 立即复验。
    /// </summary>
    /// <param name="context">事务上下文。</param>
    private void StageProjection(TransactionContext context)
    {
        Directory.CreateDirectory(context.StagingPackageRoot);
        foreach (var file in context.Projection.Files)
        {
            var targetPath = InstallerPathGuard.CombineInside(
                context.StagingPackageRoot,
                file.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            File.Copy(file.SourcePath, targetPath, overwrite: false);
            VerifyProjectedFile(targetPath, file);
        }

        mManifestStore.Write(context.StagingPackageRoot, mManifestStore.Create(context.Projection));
        var stagingInspection = mOwnershipInspector.Inspect(context.StagingPackageRoot);
        if (stagingInspection.State != PackageOwnershipState.Clean)
        {
            throw new IOException("Staging verification failed: " + string.Join(", ", stagingInspection.ConflictPaths));
        }

        AdvanceCheckpoint(context, PackageInstallTransactionCheckpoint.StagingVerified);
    }

    /// <summary>
    /// 校验 staging 文件长度和 SHA-256，捕获复制期间的源文件变化或磁盘写入损坏。
    /// </summary>
    /// <param name="targetPath">staging 文件路径。</param>
    /// <param name="expected">投影中的期望摘要。</param>
    private static void VerifyProjectedFile(string targetPath, PackageProjectionFile expected)
    {
        FileInfo info = new(targetPath);
        using var stream = File.OpenRead(targetPath);
        var hash = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        if (info.Length != expected.Length || !string.Equals(hash, expected.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new IOException("Staged file hash mismatch: " + expected.RelativePath);
        }
    }

    /// <summary>
    /// 将已有正式包目录移动到同项目事务备份区，保证提交前存在完整恢复源。
    /// </summary>
    /// <param name="context">事务上下文。</param>
    private void MoveExistingPackageToBackup(TransactionContext context)
    {
        if (!Directory.Exists(context.TargetPackageRoot))
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(context.BackupPackageRoot)!);
        MoveDirectoryWithRetry(context.TargetPackageRoot, context.BackupPackageRoot);
        context.ExistingPackageBackedUp = true;
        AdvanceCheckpoint(context, PackageInstallTransactionCheckpoint.ExistingPackageBackedUp);
    }

    /// <summary>
    /// 把已验证 staging 目录移动为正式包根，目录移动限定在同一项目卷内。
    /// </summary>
    /// <param name="context">事务上下文。</param>
    private void CommitStaging(TransactionContext context)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(context.TargetPackageRoot)!);
        MoveDirectoryWithRetry(context.StagingPackageRoot, context.TargetPackageRoot);
        context.TargetCommitted = true;
        AdvanceCheckpoint(context, PackageInstallTransactionCheckpoint.TargetCommitted);
    }

    /// <summary>
    /// 在 Windows 文件扫描器短暂占用包文件时有界重试目录切换，超过窗口后保留原始异常进入回滚。
    /// </summary>
    /// <param name="sourcePath">必须仍然存在的源目录。</param>
    /// <param name="destinationPath">尚未存在的目标目录。</param>
    private static void MoveDirectoryWithRetry(string sourcePath, string destinationPath)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                Directory.Move(sourcePath, destinationPath);
                return;
            }
            catch (Exception exception) when (CanRetryDirectoryMove(
                       sourcePath,
                       destinationPath,
                       attempt,
                       exception))
            {
                Thread.Sleep(DIRECTORY_MOVE_RETRY_DELAY_MILLISECONDS);
            }
        }
    }

    /// <summary>
    /// 仅重试源目录仍存在、目标尚未创建的短暂 IO 或访问冲突，避免掩盖路径和目标冲突错误。
    /// </summary>
    /// <param name="sourcePath">目录移动源路径。</param>
    /// <param name="destinationPath">目录移动目标路径。</param>
    /// <param name="attempt">当前尝试序号，从 1 开始。</param>
    /// <param name="exception">本次目录移动异常。</param>
    /// <returns>仍处于有界重试窗口且错误可能由短暂占用引起时返回 true。</returns>
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
    /// 提交后再次对正式包执行 owner manifest 检查，防止只验证 staging 就宣称成功。
    /// </summary>
    /// <param name="context">事务上下文。</param>
    private void VerifyCommittedPackage(TransactionContext context)
    {
        var inspection = mOwnershipInspector.Inspect(context.TargetPackageRoot);
        if (inspection.State != PackageOwnershipState.Clean)
        {
            throw new IOException("Committed package verification failed: " + string.Join(", ", inspection.ConflictPaths));
        }
    }

    /// <summary>
    /// 清理已成功事务的 staging 与备份副本，只保留正式包和 owner manifest。
    /// </summary>
    /// <param name="context">事务上下文。</param>
    private static void CompleteTransaction(TransactionContext context)
    {
        DeleteDirectoryIfExists(context.StagingTransactionRoot);
        DeleteDirectoryIfExists(context.BackupTransactionRoot);
    }

    /// <summary>
    /// 更新上下文检查点并通知内部测试 seam。
    /// </summary>
    /// <param name="context">事务上下文。</param>
    /// <param name="checkpoint">新检查点。</param>
    private void AdvanceCheckpoint(TransactionContext context, PackageInstallTransactionCheckpoint checkpoint)
    {
        context.Checkpoint = checkpoint;
        mFaultInjector.OnCheckpoint(checkpoint);
    }

    /// <summary>
    /// 保存一次事务的受守卫路径、检查点和提交状态。
    /// </summary>
    private sealed class TransactionContext
    {
        /// <summary>
        /// 创建尚未写入磁盘的事务上下文并计算项目内事务路径。
        /// </summary>
        /// <param name="projection">待提交投影。</param>
        /// <param name="projectRoot">目标项目根。</param>
        /// <param name="targetPackageRoot">正式包根。</param>
        public TransactionContext(PackageProjection projection, string projectRoot, string targetPackageRoot)
        {
            Projection = projection;
            TargetPackageRoot = targetPackageRoot;
            TransactionId = Guid.NewGuid().ToString("N");
            var installerRoot = InstallerPathGuard.CombineInside(projectRoot, ".yokiframe", "installer");
            StagingTransactionRoot = InstallerPathGuard.CombineInside(installerRoot, "staging", TransactionId);
            BackupTransactionRoot = InstallerPathGuard.CombineInside(installerRoot, "backups", TransactionId);
            StagingPackageRoot = InstallerPathGuard.CombineInside(StagingTransactionRoot, "package");
            BackupPackageRoot = InstallerPathGuard.CombineInside(BackupTransactionRoot, "package");
            DiagnosticEvidencePath = InstallerPathGuard.CombineInside(installerRoot, "diagnostics", TransactionId + ".json");
        }

        /// <summary>
        /// 获取待提交投影。
        /// </summary>
        public PackageProjection Projection { get; }

        /// <summary>
        /// 获取事务标识。
        /// </summary>
        public string TransactionId { get; }

        /// <summary>
        /// 获取正式包根。
        /// </summary>
        public string TargetPackageRoot { get; }

        /// <summary>
        /// 获取 staging 事务根。
        /// </summary>
        public string StagingTransactionRoot { get; }

        /// <summary>
        /// 获取备份事务根。
        /// </summary>
        public string BackupTransactionRoot { get; }

        /// <summary>
        /// 获取 staging 包根。
        /// </summary>
        public string StagingPackageRoot { get; }

        /// <summary>
        /// 获取备份包根。
        /// </summary>
        public string BackupPackageRoot { get; }

        /// <summary>
        /// 获取失败诊断路径。
        /// </summary>
        public string DiagnosticEvidencePath { get; }

        /// <summary>
        /// 获取或设置是否替换已有包。
        /// </summary>
        public bool ReplacedExistingPackage { get; set; }

        /// <summary>
        /// 获取或设置已有包是否已进入备份区。
        /// </summary>
        public bool ExistingPackageBackedUp { get; set; }

        /// <summary>
        /// 获取或设置新包是否已成为正式目录。
        /// </summary>
        public bool TargetCommitted { get; set; }

        /// <summary>
        /// 获取或设置当前稳定检查点。
        /// </summary>
        public PackageInstallTransactionCheckpoint Checkpoint { get; set; }
    }

    /// <summary>
    /// 生产环境无操作故障注入器。
    /// </summary>
    private sealed class NoOpFaultInjector : IPackageInstallTransactionFaultInjector
    {
        /// <summary>
        /// 获取共享无操作实例。
        /// </summary>
        public static NoOpFaultInjector Instance { get; } = new();

        /// <summary>
        /// 忽略事务检查点。
        /// </summary>
        /// <param name="checkpoint">当前检查点。</param>
        public void OnCheckpoint(PackageInstallTransactionCheckpoint checkpoint)
        {
        }
    }
}
