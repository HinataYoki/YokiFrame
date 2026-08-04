using System.Runtime.ExceptionServices;
using YokiFrame.Installer.Core.IO;
using YokiFrame.Installer.Core.Models;

namespace YokiFrame.Installer.Core.Services;

/// <summary>
/// 统一规划并执行 Unity embedded 与 Git URL 两种互斥安装来源。
/// </summary>
public sealed class UnityInstallService
{
    private readonly IUnityInstallFaultInjector mFaultInjector;
    private readonly PackageInstallTransactionService mPackageTransactionService = new();
    private readonly PackageOwnershipInspector mOwnershipInspector = new();
    private readonly TargetProjectDetector mTargetDetector = new();
    private readonly UnityManifestDependencyStore mManifestStore = new();
    private readonly UnityEmbeddedPackageGraphChangeDetector mPackageGraphChangeDetector = new();
    private readonly UnityPackageProjectionBuilder mProjectionBuilder = new();

    /// <summary>
    /// 创建生产 Unity 安装服务，不启用故障注入。
    /// </summary>
    public UnityInstallService()
        : this(NoOpFaultInjector.Instance)
    {
    }

    /// <summary>
    /// 创建带 Git 依赖持久化检查点的测试安装服务。
    /// </summary>
    /// <param name="faultInjector">内部测试故障注入器。</param>
    internal UnityInstallService(IUnityInstallFaultInjector faultInjector)
    {
        mFaultInjector = faultInjector ?? throw new ArgumentNullException(nameof(faultInjector));
    }

    /// <summary>
    /// 只读验证项目、版本、manifest、包所有权和源输入，并生成明确的来源互斥计划。
    /// </summary>
    /// <param name="request">Unity 安装请求。</param>
    /// <returns>不修改目标项目的安装计划。</returns>
    public UnityInstallPlan CreatePlan(UnityInstallRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var target = DetectUnityTarget(request.ProjectRoot);
        var manifest = mManifestStore.Read(target.ProjectRoot);
        var ownership = mOwnershipInspector.Inspect(target.PackageRoot);
        ValidateOwnership(ownership, request.UnmanagedPackagePolicy);

        return request.Mode switch
        {
            UnityInstallMode.Embedded => CreateEmbeddedPlan(request, target, manifest, ownership),
            UnityInstallMode.GitUrl => CreateGitPlan(request, target, manifest, ownership),
            _ => throw new ArgumentOutOfRangeException(nameof(request), request.Mode, "Unsupported Unity install mode.")
        };
    }

    /// <summary>
    /// 在重新执行全部只读门控后提交计划，并保证最终只保留一种 Unity 安装来源。
    /// </summary>
    /// <param name="request">Unity 安装请求。</param>
    /// <returns>成功执行的计划、包事务和 manifest 变化结果。</returns>
    public UnityInstallResult Execute(
        UnityInstallRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        _ = CreatePlan(request);
        using var projectLock = InstallerProjectLock.Acquire(request.ProjectRoot);
        InstallerPackageTransactionRecovery.Recover(request.ProjectRoot);
        cancellationToken.ThrowIfCancellationRequested();
        var plan = CreatePlan(request);
        return plan.Request.Mode switch
        {
            UnityInstallMode.Embedded => ExecuteEmbedded(plan, projectLock, cancellationToken),
            UnityInstallMode.GitUrl => ExecuteGit(plan, projectLock, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(request), plan.Request.Mode, "Unsupported Unity install mode.")
        };
    }

    /// <summary>
    /// 创建 embedded 文件投影计划，并显式列出 package 投影和本地 file 依赖登记动作。
    /// </summary>
    /// <param name="request">Unity 安装请求。</param>
    /// <param name="target">已验证 Unity 目标。</param>
    /// <param name="manifest">只读 manifest 快照。</param>
    /// <param name="ownership">计划生成时的现有 embedded 包状态。</param>
    /// <returns>embedded 安装计划。</returns>
    private UnityInstallPlan CreateEmbeddedPlan(
        UnityInstallRequest request,
        InstallerProjectInfo target,
        UnityManifestSnapshot manifest,
        PackageOwnershipInspection ownership)
    {
        var projection = mProjectionBuilder.Build(request.SourcePackageRoot, request.RuntimeProfile);
        var installReason = ownership.State == PackageOwnershipState.NotInstalled
            ? "安装已验证的本地包投影。"
            : "备份现有 embedded 包后完整替换，提交失败时恢复原包。";
        List<UnityInstallPlanAction> actions = new()
        {
            new(
                UnityInstallPlanActionKind.InstallEmbeddedPackage,
                target.PackageRoot,
                null,
                installReason)
        };
        if (!string.Equals(
                manifest.CurrentDependency,
                UnityManifestDependencyStore.EMBEDDED_PACKAGE_DEPENDENCY,
                StringComparison.Ordinal))
        {
            actions.Add(new UnityInstallPlanAction(
                UnityInstallPlanActionKind.SetEmbeddedDependency,
                manifest.ManifestPath,
                UnityManifestDependencyStore.EMBEDDED_PACKAGE_DEPENDENCY,
                "Register the embedded package as the local Unity manifest dependency."));
        }

        return new UnityInstallPlan(
            request,
            target,
            projection,
            actions,
            ownership.State,
            ownership.ConflictPaths);
    }

    /// <summary>
    /// 创建 Git URL 计划，并显式列出已有 embedded 包移除与依赖设置动作。
    /// </summary>
    /// <param name="request">Unity 安装请求。</param>
    /// <param name="target">已验证 Unity 目标。</param>
    /// <param name="manifest">只读 manifest 快照。</param>
    /// <param name="ownership">当前 embedded 包所有权状态。</param>
    /// <returns>Git URL 安装计划。</returns>
    private static UnityInstallPlan CreateGitPlan(
        UnityInstallRequest request,
        InstallerProjectInfo target,
        UnityManifestSnapshot manifest,
        PackageOwnershipInspection ownership)
    {
        UnityManifestDependencyStore.ValidateGitUrl(request.GitUrl);
        List<UnityInstallPlanAction> actions = new();
        if (ownership.State != PackageOwnershipState.NotInstalled)
        {
            actions.Add(new UnityInstallPlanAction(
                UnityInstallPlanActionKind.RemoveEmbeddedPackage,
                target.PackageRoot,
                null,
                "Remove the current embedded source before enabling the Git source."));
        }

        if (!string.Equals(manifest.CurrentDependency, request.GitUrl, StringComparison.Ordinal))
        {
            actions.Add(new UnityInstallPlanAction(
                UnityInstallPlanActionKind.SetGitDependency,
                manifest.ManifestPath,
                request.GitUrl,
                "Set the structured YokiFrame Git dependency."));
        }

        return new UnityInstallPlan(
            request,
            target,
            null,
            actions,
            ownership.State,
            ownership.ConflictPaths);
    }

    /// <summary>
    /// 保持旧 embedded 包可见直到新投影完成 staging，再在同一事务的短暂目录切换窗口内提交并登记本地 file 依赖。
    /// </summary>
    /// <param name="plan">已验证 embedded 计划。</param>
    /// <returns>embedded 安装结果。</returns>
    private UnityInstallResult ExecuteEmbedded(
        UnityInstallPlan plan,
        InstallerProjectLockLease projectLock,
        CancellationToken cancellationToken)
    {
        var projection = plan.Projection
            ?? throw new InvalidOperationException("Embedded Unity install plan is missing its package projection.");
        var manifest = mManifestStore.Read(plan.Target.ProjectRoot);
        var refreshPackageGraph = mPackageGraphChangeDetector.RequiresPackageManagerRefresh(
            plan.Target.PackageRoot,
            projection);
        var manifestChanged = false;
        var manifestWritten = false;
        var postCommitActionStarted = false;
        try
        {
            var transaction = mPackageTransactionService.Execute(
                projection,
                plan.Target.ProjectRoot,
                plan.Target.PackageRoot,
                plan.Request.UnmanagedPackagePolicy,
                replaceModifiedPackage: true,
                projectLock: projectLock,
                cancellationToken: cancellationToken,
                postCommitAction: () =>
                {
                    postCommitActionStarted = true;
                    manifestChanged = mManifestStore.SetEmbeddedDependency(plan.Target.ProjectRoot);
                    manifestWritten = manifestChanged;
                    if (!manifestChanged && refreshPackageGraph)
                    {
                        mManifestStore.RefreshEmbeddedPackageGraph(plan.Target.ProjectRoot);
                        manifestWritten = true;
                    }

                    if (manifestChanged)
                    {
                        mFaultInjector.OnCheckpoint(UnityInstallCheckpoint.EmbeddedDependencyPersisted);
                    }

                    mManifestStore.VerifyEmbeddedDependency(plan.Target.ProjectRoot);
                });
            return new UnityInstallResult(plan, transaction, manifestChanged);
        }
        catch (PackageInstallTransactionException exception)
            when (postCommitActionStarted
                  && exception.RollbackSucceeded
                  && exception.InnerException != null)
        {
            RestoreManifestAndRethrow(exception.InnerException, manifest, manifestWritten);
            throw new InvalidOperationException("Unreachable embedded post-commit rollback path.");
        }
        catch (Exception exception)
        {
            RestoreManifestAndRethrow(exception, manifest, manifestWritten);
            throw new InvalidOperationException("Unreachable embedded rollback path.");
        }
    }

    /// <summary>
    /// 先备份并移出 embedded 包，再原子设置 Git 依赖，任一步失败都恢复原来源。
    /// </summary>
    /// <param name="plan">已验证 Git URL 计划。</param>
    /// <returns>Git URL 安装结果。</returns>
    private UnityInstallResult ExecuteGit(
        UnityInstallPlan plan,
        InstallerProjectLockLease projectLock,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var manifest = mManifestStore.Read(plan.Target.ProjectRoot);
        var backup = MoveExistingPackageToBackup(plan);
        var manifestChanged = false;
        try
        {
            manifestChanged = mManifestStore.SetGitDependency(plan.Target.ProjectRoot, plan.Request.GitUrl);
            if (manifestChanged)
            {
                mFaultInjector.OnCheckpoint(UnityInstallCheckpoint.GitDependencyPersisted);
            }

            mManifestStore.VerifyGitDependency(plan.Target.ProjectRoot, plan.Request.GitUrl);
            DeleteBackup(backup);
            return new UnityInstallResult(plan, null, manifestChanged);
        }
        catch (Exception exception)
        {
            RollbackAndRethrow(exception, plan, manifest, manifestChanged, backup, removeCurrentPackage: false);
            throw new InvalidOperationException("Unreachable Git rollback path.");
        }
    }

    /// <summary>
    /// 在任何来源写入前重新检查 embedded 所有权，并把现有包移动到同项目备份区。
    /// </summary>
    /// <param name="plan">即将执行的 Unity 安装计划。</param>
    /// <returns>现有包备份；目标包不存在时返回空。</returns>
    private UnityPackageBackup? MoveExistingPackageToBackup(UnityInstallPlan plan)
    {
        var ownership = mOwnershipInspector.Inspect(plan.Target.PackageRoot);
        ValidateOwnership(ownership, plan.Request.UnmanagedPackagePolicy);
        if (ownership.State == PackageOwnershipState.NotInstalled)
        {
            return null;
        }

        var backupRoot = InstallerPathGuard.CombineInside(
            plan.Target.ProjectRoot,
            ".yokiframe",
            "installer",
            "unity",
            "backups",
            Guid.NewGuid().ToString("N"));
        var backupPackageRoot = InstallerPathGuard.CombineInside(backupRoot, UnityManifestDependencyStore.PACKAGE_ID);
        Directory.CreateDirectory(backupRoot);
        Directory.Move(plan.Target.PackageRoot, backupPackageRoot);
        return new UnityPackageBackup(backupRoot, backupPackageRoot);
    }

    /// <summary>
    /// 删除已成功提交后不再需要的外层包备份。
    /// </summary>
    /// <param name="backup">待删除备份；没有旧包时为空。</param>
    private static void DeleteBackup(UnityPackageBackup? backup)
    {
        if (backup != null && Directory.Exists(backup.BackupRoot))
        {
            Directory.Delete(backup.BackupRoot, recursive: true);
        }
    }

    /// <summary>
    /// 当内部包事务已自行恢复目录时，只在依赖内容确实改写过的情况下恢复 manifest 原文并保留原始异常堆栈。
    /// </summary>
    /// <param name="exception">触发外部 manifest 回滚的原始异常。</param>
    /// <param name="manifest">写入前读取的 manifest 快照。</param>
    /// <param name="manifestWritten">manifest 是否已经成功写入新依赖或程序集图刷新通知。</param>
    private void RestoreManifestAndRethrow(
        Exception exception,
        UnityManifestSnapshot manifest,
        bool manifestWritten)
    {
        List<Exception> rollbackErrors = new();
        TryRestoreManifest(manifest, manifestWritten, rollbackErrors);
        if (rollbackErrors.Count > 0)
        {
            rollbackErrors.Insert(0, exception);
            throw new IOException("Unity embedded install failed and manifest rollback was incomplete.", new AggregateException(rollbackErrors));
        }

        ExceptionDispatchInfo.Capture(exception).Throw();
    }

    /// <summary>
    /// 尝试恢复 Git 来源切换前的 manifest 原文和 embedded 包；回滚失败时聚合证据，否则保留原异常堆栈。
    /// </summary>
    /// <param name="exception">触发回滚的原始异常。</param>
    /// <param name="plan">正在执行的计划。</param>
    /// <param name="manifest">写入前 manifest 快照。</param>
    /// <param name="manifestChanged">manifest 是否已经成功改写。</param>
    /// <param name="backup">执行前包备份。</param>
    /// <param name="removeCurrentPackage">是否需要先删除本次新安装的 embedded 包。</param>
    private void RollbackAndRethrow(
        Exception exception,
        UnityInstallPlan plan,
        UnityManifestSnapshot manifest,
        bool manifestChanged,
        UnityPackageBackup? backup,
        bool removeCurrentPackage)
    {
        List<Exception> rollbackErrors = new();
        TryRestoreManifest(manifest, manifestChanged, rollbackErrors);
        TryRestorePackage(plan.Target.PackageRoot, backup, removeCurrentPackage, rollbackErrors);
        if (rollbackErrors.Count > 0)
        {
            rollbackErrors.Insert(0, exception);
            throw new IOException("Unity install failed and rollback was incomplete.", new AggregateException(rollbackErrors));
        }

        ExceptionDispatchInfo.Capture(exception).Throw();
    }

    /// <summary>
    /// 在 manifest 已改写时恢复完整原文，并把失败加入统一回滚错误列表。
    /// </summary>
    /// <param name="manifest">写入前 manifest 快照。</param>
    /// <param name="manifestChanged">manifest 是否已经成功改写。</param>
    /// <param name="rollbackErrors">回滚错误列表。</param>
    private void TryRestoreManifest(
        UnityManifestSnapshot manifest,
        bool manifestChanged,
        ICollection<Exception> rollbackErrors)
    {
        if (!manifestChanged)
        {
            return;
        }

        try
        {
            mManifestStore.RestoreOriginal(manifest);
        }
        catch (Exception rollbackException)
        {
            rollbackErrors.Add(rollbackException);
        }
    }

    /// <summary>
    /// 删除本次新包并恢复执行前备份，同时保留失败备份作为诊断证据。
    /// </summary>
    /// <param name="targetPackageRoot">正式 embedded 包路径。</param>
    /// <param name="backup">执行前包备份。</param>
    /// <param name="removeCurrentPackage">是否删除本次新安装包。</param>
    /// <param name="rollbackErrors">回滚错误列表。</param>
    private static void TryRestorePackage(
        string targetPackageRoot,
        UnityPackageBackup? backup,
        bool removeCurrentPackage,
        ICollection<Exception> rollbackErrors)
    {
        try
        {
            if (removeCurrentPackage && Directory.Exists(targetPackageRoot))
            {
                Directory.Delete(targetPackageRoot, recursive: true);
            }

            if (backup != null && !Directory.Exists(targetPackageRoot))
            {
                Directory.Move(backup.BackupPackageRoot, targetPackageRoot);
                DeleteBackup(backup);
            }
        }
        catch (Exception rollbackException)
        {
            rollbackErrors.Add(rollbackException);
        }
    }

    /// <summary>
    /// 使用既有 detector 执行 Unity 结构和 2022.3 最低版本门控。
    /// </summary>
    /// <param name="projectRoot">目标项目根。</param>
    /// <returns>已验证 Unity 目标信息。</returns>
    private InstallerProjectInfo DetectUnityTarget(string projectRoot)
    {
        var target = mTargetDetector.Detect(projectRoot);
        if (target.Kind != InstallerProjectKind.Unity)
        {
            throw new InvalidOperationException("Target project is not a supported Unity project: " + target.ProjectRoot);
        }

        return target;
    }

    /// <summary>
    /// 在零写入阶段只拒绝未经确认的 legacy 包；受管修改由整包替换事务备份后覆盖。
    /// </summary>
    /// <param name="ownership">embedded 包所有权检查结果。</param>
    /// <param name="policy">legacy 包接管策略。</param>
    private static void ValidateOwnership(
        PackageOwnershipInspection ownership,
        UnmanagedPackagePolicy policy)
    {
        if (ownership.State == PackageOwnershipState.UnmanagedLegacy
            && policy != UnmanagedPackagePolicy.TakeOverConfirmed)
        {
            throw new PackageInstallRejectedException(ownership.State, ownership.ConflictPaths);
        }
    }

    /// <summary>
    /// 生产环境无操作故障注入器。
    /// </summary>
    private sealed class NoOpFaultInjector : IUnityInstallFaultInjector
    {
        /// <summary>获取共享无操作实例。</summary>
        public static NoOpFaultInjector Instance { get; } = new();

        /// <summary>
        /// 忽略生产环境的 Git 依赖持久化检查点。
        /// </summary>
        /// <param name="checkpoint">刚完成的 Unity 安装检查点。</param>
        public void OnCheckpoint(UnityInstallCheckpoint checkpoint)
        {
        }
    }
}

/// <summary>
/// 保存来源切换期间移出正式目录的 embedded 包备份路径。
/// </summary>
internal sealed class UnityPackageBackup
{
    /// <summary>
    /// 创建 embedded 包备份描述。
    /// </summary>
    /// <param name="backupRoot">本次来源切换备份根。</param>
    /// <param name="backupPackageRoot">移出的 embedded 包目录。</param>
    internal UnityPackageBackup(string backupRoot, string backupPackageRoot)
    {
        BackupRoot = backupRoot;
        BackupPackageRoot = backupPackageRoot;
    }

    /// <summary>
    /// 获取本次来源切换备份根。
    /// </summary>
    internal string BackupRoot { get; }

    /// <summary>
    /// 获取移出的 embedded 包目录。
    /// </summary>
    internal string BackupPackageRoot { get; }
}
