using System.Text.Json;
using YokiFrame.Installer.Core.Models;
using YokiFrame.Installer.Core.Services;

namespace YokiFrame.Installer.Core.Tests.Transactions;

/// <summary>
/// 锁定 PackageProjection 从 staging 到提交、回滚和 owner manifest 的事务执行契约。
/// </summary>
public sealed class PackageInstallTransactionTests
{
    /// <summary>
    /// 验证完整投影已经写入并验证 staging 后，事务才允许触碰已有正式包。
    /// </summary>
    [Fact]
    public void ExecuteStagesCompleteProjectionBeforeReplacingTarget()
    {
        using PackageInstallTransactionFixture fixture = PackageInstallTransactionFixture.Create();
        var projection = fixture.CreateProjection(
            new PackageProjectionSpecification("Core/Runtime/Alpha.cs", "Core/Runtime/Alpha.cs", "alpha-v2"),
            new PackageProjectionSpecification("Tools/ActionKit/Runtime/Beta.cs", "Tools/ActionKit/Runtime/Beta.cs", "beta-v2"));
        fixture.WriteTargetFile("legacy.marker", "stable-package");
        var checkpointObserved = false;
        CallbackTransactionFaultInjector faultInjector = new(checkpoint =>
        {
            if (checkpoint != PackageInstallTransactionCheckpoint.StagingVerified)
            {
                return;
            }

            checkpointObserved = true;
            Assert.Equal("stable-package", File.ReadAllText(fixture.GetTargetPath("legacy.marker")));
            Assert.Equal("alpha-v2", File.ReadAllText(fixture.FindTransactionFile("staging", "Core/Runtime/Alpha.cs")));
            Assert.Equal("beta-v2", File.ReadAllText(fixture.FindTransactionFile("staging", "Tools/ActionKit/Runtime/Beta.cs")));
            throw new InvalidOperationException("Injected failure after staging verification.");
        });

        Assert.Throws<PackageInstallTransactionException>(() => ExecuteTakeOver(fixture, projection, faultInjector));

        Assert.True(checkpointObserved);
        Assert.Equal("stable-package", File.ReadAllText(fixture.GetTargetPath("legacy.marker")));
        Assert.False(File.Exists(fixture.GetTargetPath("Core/Runtime/Alpha.cs")));
    }

    /// <summary>
    /// 验证已有正式包在提交新投影前已经进入备份区，并能在故障后恢复。
    /// </summary>
    [Fact]
    public void ExecuteBacksUpExistingPackageBeforeTargetCommit()
    {
        using PackageInstallTransactionFixture fixture = PackageInstallTransactionFixture.Create();
        var projection = fixture.CreateProjection(
            new PackageProjectionSpecification("Core/Runtime/Alpha.cs", "Core/Runtime/Alpha.cs", "alpha-v2"));
        fixture.WriteTargetFile("legacy.marker", "stable-package");
        var checkpointObserved = false;
        CallbackTransactionFaultInjector faultInjector = new(checkpoint =>
        {
            if (checkpoint != PackageInstallTransactionCheckpoint.ExistingPackageBackedUp)
            {
                return;
            }

            checkpointObserved = true;
            var backupPath = fixture.FindTransactionFile("backups", "legacy.marker");
            Assert.Equal("stable-package", File.ReadAllText(backupPath));
            throw new InvalidOperationException("Injected failure after package backup.");
        });

        var exception = Assert.Throws<PackageInstallTransactionException>(() => ExecuteTakeOver(fixture, projection, faultInjector));

        Assert.True(checkpointObserved);
        Assert.True(exception.RollbackSucceeded);
        Assert.Equal("stable-package", File.ReadAllText(fixture.GetTargetPath("legacy.marker")));
        fixture.AssertTransactionAreasClean("staging", "backups");
    }

    /// <summary>
    /// 验证成功提交会写入 owner manifest，并使目标包立即处于干净受管状态。
    /// </summary>
    [Fact]
    public void ExecuteWritesOwnerManifestAndReturnsCommittedResult()
    {
        using PackageInstallTransactionFixture fixture = PackageInstallTransactionFixture.Create();
        var projection = fixture.CreateProjection(
            new PackageProjectionSpecification("Core/Runtime/Alpha.cs", "Core/Runtime/Alpha.cs", "alpha-v2"),
            new PackageProjectionSpecification("Tools/ActionKit/Runtime/Beta.cs", "Tools/ActionKit/Runtime/Beta.cs", "beta-v2"));
        PackageInstallTransactionService service = new();

        var result = service.Execute(
            projection,
            fixture.ProjectRoot,
            fixture.TargetPackageRoot,
            UnmanagedPackagePolicy.Reject);

        PackageOwnerManifestStore manifestStore = new();
        Assert.Equal(fixture.TargetPackageRoot, result.TargetPackageRoot);
        Assert.Equal(manifestStore.GetManifestPath(fixture.TargetPackageRoot), result.OwnerManifestPath);
        Assert.False(result.ReplacedExistingPackage);
        Assert.True(File.Exists(result.OwnerManifestPath));
        Assert.Equal("alpha-v2", File.ReadAllText(fixture.GetTargetPath("Core/Runtime/Alpha.cs")));
        Assert.Equal(PackageOwnershipState.Clean, new PackageOwnershipInspector().Inspect(fixture.TargetPackageRoot).State);
        fixture.AssertTransactionAreasClean("staging", "backups");
    }

    /// <summary>
    /// 验证外部 post-commit 验证失败时，事务仍持有旧包 backup 并能恢复更新前的正式目录。
    /// </summary>
    [Fact]
    public void ExecuteRestoresPreviousPackageWhenPostCommitActionFails()
    {
        using PackageInstallTransactionFixture fixture = PackageInstallTransactionFixture.Create();
        PackageInstallTransactionService service = new();
        var initialProjection = fixture.CreateProjection(
            new PackageProjectionSpecification("Core/Runtime/Alpha.cs", "Core/Runtime/Alpha.cs", "alpha-v1"));
        _ = service.Execute(
            initialProjection,
            fixture.ProjectRoot,
            fixture.TargetPackageRoot,
            UnmanagedPackagePolicy.Reject);
        var replacementProjection = fixture.CreateProjection(
            new PackageProjectionSpecification("Core/Runtime/Alpha.cs", "Core/Runtime/Alpha.cs", "alpha-v2"));
        var callbackObserved = false;

        var exception = Assert.Throws<PackageInstallTransactionException>(() => service.Execute(
            replacementProjection,
            fixture.ProjectRoot,
            fixture.TargetPackageRoot,
            UnmanagedPackagePolicy.Reject,
            replaceModifiedPackage: false,
            postCommitAction: () =>
            {
                callbackObserved = true;
                Assert.Equal("alpha-v2", File.ReadAllText(fixture.GetTargetPath("Core/Runtime/Alpha.cs")));
                Assert.Equal("alpha-v1", File.ReadAllText(fixture.FindTransactionFile("backups", "Core/Runtime/Alpha.cs")));
                throw new InvalidOperationException("Injected failure after external post-commit verification.");
            }));

        Assert.True(callbackObserved);
        Assert.True(exception.RollbackSucceeded);
        Assert.Equal("alpha-v1", File.ReadAllText(fixture.GetTargetPath("Core/Runtime/Alpha.cs")));
        fixture.AssertTransactionAreasClean("staging", "backups");
    }

    /// <summary>
    /// 验证 Windows 文件扫描器短暂占用 staging 文件时，目录提交会等待占用释放而不是立即回滚失败。
    /// </summary>
    [Fact]
    public async Task ExecuteRetriesTransientWindowsStagingLock()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using PackageInstallTransactionFixture fixture = PackageInstallTransactionFixture.Create();
        var projection = fixture.CreateProjection(
            new PackageProjectionSpecification("Core/Runtime/Alpha.cs", "Core/Runtime/Alpha.cs", "alpha-v2"));
        fixture.WriteTargetFile("legacy.marker", "stable-package");
        Task releaseTask = Task.CompletedTask;
        FileStream? lockedStream = null;
        CallbackTransactionFaultInjector faultInjector = new(checkpoint =>
        {
            if (checkpoint != PackageInstallTransactionCheckpoint.StagingVerified)
            {
                return;
            }

            var stagedPath = fixture.FindTransactionFile("staging", "Core/Runtime/Alpha.cs");
            lockedStream = new FileStream(stagedPath, FileMode.Open, FileAccess.Read, FileShare.None);
            releaseTask = Task.Run(async () =>
            {
                await Task.Delay(250);
                lockedStream.Dispose();
            });
        });

        PackageInstallTransactionResult? result = null;
        try
        {
            PackageInstallTransactionService service = new(faultInjector);
            result = service.Execute(
                projection,
                fixture.ProjectRoot,
                fixture.TargetPackageRoot,
                UnmanagedPackagePolicy.TakeOverConfirmed);
        }
        finally
        {
            await releaseTask;
            lockedStream?.Dispose();
        }

        Assert.NotNull(result);
        Assert.Equal("alpha-v2", File.ReadAllText(fixture.GetTargetPath("Core/Runtime/Alpha.cs")));
        fixture.AssertTransactionAreasClean("staging", "backups");
    }

    /// <summary>
    /// 验证正式目录切换后的故障会恢复原目录、清理临时事务区，并保留结构化诊断证据。
    /// </summary>
    [Fact]
    public void ExecuteRestoresOriginalPackageAndPreservesDiagnosticsAfterCommitFault()
    {
        using PackageInstallTransactionFixture fixture = PackageInstallTransactionFixture.Create();
        var projection = fixture.CreateProjection(
            new PackageProjectionSpecification("Core/Runtime/Alpha.cs", "Core/Runtime/Alpha.cs", "alpha-v2"));
        fixture.WriteTargetFile("legacy.marker", "stable-package");
        CallbackTransactionFaultInjector faultInjector = new(checkpoint =>
        {
            if (checkpoint == PackageInstallTransactionCheckpoint.TargetCommitted)
            {
                throw new InvalidOperationException("Injected failure after target commit.");
            }
        });

        var exception = Assert.Throws<PackageInstallTransactionException>(() => ExecuteTakeOver(fixture, projection, faultInjector));

        Assert.True(exception.RollbackSucceeded);
        Assert.Equal("stable-package", File.ReadAllText(fixture.GetTargetPath("legacy.marker")));
        Assert.False(File.Exists(fixture.GetTargetPath("Core/Runtime/Alpha.cs")));
        Assert.False(File.Exists(new PackageOwnerManifestStore().GetManifestPath(fixture.TargetPackageRoot)));
        fixture.AssertTransactionAreasClean("staging", "backups");
        AssertFailureEvidence(exception.DiagnosticEvidencePath);
    }

    /// <summary>
    /// 验证受管文件被用户修改后，事务在创建 staging 或其它安装证据前拒绝写入。
    /// </summary>
    [Fact]
    public void ExecuteRejectsManagedConflictWithoutWriting()
    {
        using PackageInstallTransactionFixture fixture = PackageInstallTransactionFixture.Create();
        var projection = fixture.CreateProjection(
            new PackageProjectionSpecification("Core/Runtime/Alpha.cs", "Core/Runtime/Alpha.cs", "alpha-v2"));
        PackageInstallTransactionService service = new();
        _ = service.Execute(projection, fixture.ProjectRoot, fixture.TargetPackageRoot, UnmanagedPackagePolicy.Reject);
        fixture.WriteTargetFile("Core/Runtime/Alpha.cs", "manual-change");
        var before = DirectoryTreeSnapshot.Capture(fixture.Root);

        var exception = Assert.Throws<PackageInstallRejectedException>(() =>
            service.Execute(projection, fixture.ProjectRoot, fixture.TargetPackageRoot, UnmanagedPackagePolicy.Reject));

        Assert.Equal(PackageOwnershipState.Modified, exception.OwnershipState);
        Assert.Contains("Core/Runtime/Alpha.cs", exception.ConflictPaths);
        before.AssertMatches(DirectoryTreeSnapshot.Capture(fixture.Root));
    }

    /// <summary>
    /// 验证没有 owner manifest 的 legacy 包必须显式确认接管，否则项目保持逐字节不变。
    /// </summary>
    [Fact]
    public void ExecuteRejectsUnmanagedLegacyWithoutConfirmationAndDoesNotWrite()
    {
        using PackageInstallTransactionFixture fixture = PackageInstallTransactionFixture.Create();
        var projection = fixture.CreateProjection(
            new PackageProjectionSpecification("Core/Runtime/Alpha.cs", "Core/Runtime/Alpha.cs", "alpha-v2"));
        fixture.WriteTargetFile("legacy.marker", "stable-package");
        var before = DirectoryTreeSnapshot.Capture(fixture.Root);

        var exception = Assert.Throws<PackageInstallRejectedException>(() =>
            new PackageInstallTransactionService().Execute(
                projection,
                fixture.ProjectRoot,
                fixture.TargetPackageRoot,
                UnmanagedPackagePolicy.Reject));

        Assert.Equal(PackageOwnershipState.UnmanagedLegacy, exception.OwnershipState);
        Assert.Empty(exception.ConflictPaths);
        before.AssertMatches(DirectoryTreeSnapshot.Capture(fixture.Root));
    }

    /// <summary>
    /// 验证正式包根逃逸项目目录时在任何事务写入前由路径守卫拒绝。
    /// </summary>
    [Fact]
    public void ExecuteRejectsTargetPackageRootOutsideProject()
    {
        using PackageInstallTransactionFixture fixture = PackageInstallTransactionFixture.Create();
        var projection = fixture.CreateProjection(
            new PackageProjectionSpecification("Core/Runtime/Alpha.cs", "Core/Runtime/Alpha.cs", "alpha-v2"));
        var escapedTargetRoot = Path.Combine(fixture.Root, "outside", "YokiFrame");
        var before = DirectoryTreeSnapshot.Capture(fixture.Root);

        Assert.Throws<IOException>(() => new PackageInstallTransactionService().Execute(
            projection,
            fixture.ProjectRoot,
            escapedTargetRoot,
            UnmanagedPackagePolicy.Reject));

        before.AssertMatches(DirectoryTreeSnapshot.Capture(fixture.Root));
        Assert.False(Directory.Exists(escapedTargetRoot));
    }

    /// <summary>
    /// 验证投影内的相对目标路径不能通过上级片段逃逸正式包根。
    /// </summary>
    [Fact]
    public void ExecuteRejectsProjectionPathEscapingTargetPackageRoot()
    {
        using PackageInstallTransactionFixture fixture = PackageInstallTransactionFixture.Create();
        var projection = fixture.CreateProjection(
            new PackageProjectionSpecification("payload/escape.txt", "../escape.txt", "escaped-content"));
        var before = DirectoryTreeSnapshot.Capture(fixture.Root);

        Assert.Throws<IOException>(() => new PackageInstallTransactionService().Execute(
            projection,
            fixture.ProjectRoot,
            fixture.TargetPackageRoot,
            UnmanagedPackagePolicy.Reject));

        before.AssertMatches(DirectoryTreeSnapshot.Capture(fixture.Root));
        Assert.False(File.Exists(Path.Combine(Path.GetDirectoryName(fixture.TargetPackageRoot)!, "escape.txt")));
    }

    /// <summary>
    /// 使用明确接管策略执行旧包事务，统一故障测试中的服务构造和参数。
    /// </summary>
    /// <param name="fixture">事务测试目录。</param>
    /// <param name="projection">待提交投影。</param>
    /// <param name="faultInjector">测试专用故障注入器。</param>
    private static void ExecuteTakeOver(
        PackageInstallTransactionFixture fixture,
        PackageProjection projection,
        IPackageInstallTransactionFaultInjector faultInjector)
    {
        PackageInstallTransactionService service = new(faultInjector);
        _ = service.Execute(
            projection,
            fixture.ProjectRoot,
            fixture.TargetPackageRoot,
            UnmanagedPackagePolicy.TakeOverConfirmed);
    }

    /// <summary>
    /// 验证事务异常指向持久化 JSON 证据，并记录提交检查点与成功回滚状态。
    /// </summary>
    /// <param name="evidencePath">异常公开的诊断证据路径。</param>
    private static void AssertFailureEvidence(string evidencePath)
    {
        Assert.True(File.Exists(evidencePath));
        using var document = JsonDocument.Parse(File.ReadAllText(evidencePath));
        Assert.Equal("TargetCommitted", document.RootElement.GetProperty("checkpoint").GetString());
        Assert.True(document.RootElement.GetProperty("rollbackSucceeded").GetBoolean());
    }
}
