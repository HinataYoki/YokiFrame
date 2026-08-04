using YokiFrame.Tooling.Application.Installer;

namespace YokiFrame.Tooling.Application.Tests;

/// <summary>
/// 覆盖 Installer 应用会话状态机、进度、日志和提交保护。
/// </summary>
public sealed class InstallerSessionServiceTests
{
    /// <summary>
    /// 验证成功安装按检测、计划、应用、校验和成功顺序推进。
    /// </summary>
    [Fact]
    public async Task SuccessfulInstallMovesThroughExpectedStates()
    {
        var nowUtc = new DateTimeOffset(2026, 7, 10, 12, 30, 0, TimeSpan.Zero);
        var gateway = new FakeInstallerWorkflowGateway();
        gateway.ProgressUpdates.Add(new InstallerProgressUpdate(InstallerProgressStage.Verifying, 8, 10, "verify package"));
        var service = new InstallerSessionService(gateway, new FixedTimeProvider(nowUtc));
        List<InstallerSessionStatus> statuses = new() { service.State.Status };
        service.StateChanged += (_, args) => statuses.Add(args.State.Status);

        await service.PrepareAsync(CreateUnityLocalOptions());
        var finalState = await service.ApplyAsync();

        Assert.Equal(
            new[]
            {
                InstallerSessionStatus.Idle,
                InstallerSessionStatus.Detecting,
                InstallerSessionStatus.PlanReady,
                InstallerSessionStatus.Applying,
                InstallerSessionStatus.Verifying,
                InstallerSessionStatus.Succeeded
            },
            statuses);
        Assert.Same(gateway.Result, finalState.Result);
        Assert.All(finalState.Logs, entry => Assert.Equal(nowUtc, entry.TimestampUtc));
        Assert.All(finalState.Logs, entry => Assert.False(string.IsNullOrWhiteSpace(entry.Message)));
        Assert.Contains(finalState.Logs, entry => entry.Level == InstallerLogLevel.Information);
    }

    /// <summary>
    /// 验证待验证终态仍保留结果证据，供 CLI error.evidencePaths 投影使用。
    /// </summary>
    [Fact]
    public async Task CommittedNeedsVerificationPublishesResultEvidence()
    {
        var evidencePaths = new[]
        {
            "C:/projects/Game/addons/yokiframe/plugin.cfg",
            "C:/projects/Game/.yokiframe/installer/godot/diagnostics/tx.json"
        };
        var gateway = new FakeInstallerWorkflowGateway
        {
            Result = new InstallerExecutionResult(
                "C:/projects/Game/addons/yokiframe",
                changed: true,
                replacedExistingPackage: true,
                evidencePaths,
                committedNeedsVerification: true,
                verificationError: "Godot project build failed.")
        };
        var service = new InstallerSessionService(gateway, new FixedTimeProvider(DateTimeOffset.UtcNow));

        await service.PrepareAsync(CreateUnityLocalOptions());
        var finalState = await service.ApplyAsync();

        Assert.Equal(InstallerSessionStatus.CommittedNeedsVerification, finalState.Status);
        Assert.Equal(evidencePaths, finalState.EvidencePaths);
        Assert.Equal(evidencePaths, finalState.Result!.EvidencePaths);
    }

    /// <summary>
    /// 验证 Core 在零写入点拒绝冲突时进入 Conflict，并公开稳定冲突路径。
    /// </summary>
    [Fact]
    public async Task OwnershipRejectionMovesSessionToConflict()
    {
        var gateway = new FakeInstallerWorkflowGateway
        {
            ExecutionException = new InstallerConflictException(
                "Managed package content was modified.",
                new[] { "Core/Runtime/Changed.cs" })
        };
        var service = new InstallerSessionService(gateway, new FixedTimeProvider(DateTimeOffset.UtcNow));

        await service.PrepareAsync(CreateUnityLocalOptions());
        var finalState = await service.ApplyAsync();

        Assert.Equal(InstallerSessionStatus.Conflict, finalState.Status);
        Assert.Equal(new[] { "Core/Runtime/Changed.cs" }, finalState.ConflictPaths);
        Assert.Null(finalState.Result);
        Assert.Contains(finalState.Logs, entry => entry.Level == InstallerLogLevel.Warning);
    }

    /// <summary>
    /// 验证事务失败会先公开 RollingBack，再进入 Failed 并保留诊断证据。
    /// </summary>
    [Fact]
    public async Task TransactionFailureReportsRollbackBeforeFailed()
    {
        var gateway = new FakeInstallerWorkflowGateway
        {
            ExecutionException = new InstallerExecutionException(
                "commit failed",
                rollbackSucceeded: true,
                new[] { "C:/project/.yokiframe/installer/diagnostics/tx.json" },
                new IOException("disk failure"))
        };
        gateway.ProgressUpdates.Add(new InstallerProgressUpdate(InstallerProgressStage.RollingBack, 1, 1, "restore backup"));
        var service = new InstallerSessionService(gateway, new FixedTimeProvider(DateTimeOffset.UtcNow));
        List<InstallerSessionStatus> statuses = new();
        service.StateChanged += (_, args) => statuses.Add(args.State.Status);

        await service.PrepareAsync(CreateUnityLocalOptions());
        var finalState = await service.ApplyAsync();

        Assert.Equal(
            new[]
            {
                InstallerSessionStatus.Applying,
                InstallerSessionStatus.RollingBack,
                InstallerSessionStatus.Failed
            },
            statuses.TakeLast(3));
        Assert.True(finalState.RollbackSucceeded);
        Assert.Contains("C:/project/.yokiframe/installer/diagnostics/tx.json", finalState.EvidencePaths);
        Assert.Contains(finalState.Logs, entry => entry.Level == InstallerLogLevel.Error);
    }

    /// <summary>
    /// 验证安装尚未完成时的重复提交复用同一执行，不会二次调用 Core gateway。
    /// </summary>
    [Fact]
    public async Task ConcurrentApplyRequestsExecuteGatewayOnlyOnce()
    {
        var gateway = new FakeInstallerWorkflowGateway { HoldExecution = true };
        var service = new InstallerSessionService(gateway, new FixedTimeProvider(DateTimeOffset.UtcNow));
        await service.PrepareAsync(CreateUnityLocalOptions());

        var firstApply = service.ApplyAsync();
        await gateway.ExecutionStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var duplicateApply = service.ApplyAsync();
        await Task.Yield();

        Assert.Equal(1, gateway.ExecutionCount);
        gateway.CompleteExecution();
        var states = await Task.WhenAll(firstApply, duplicateApply);
        Assert.All(states, state => Assert.Equal(InstallerSessionStatus.Succeeded, state.Status));
        Assert.Equal(1, gateway.ExecutionCount);
    }

    /// <summary>
    /// 验证较早的计划生成在较新的计划就绪后才返回时，不能覆盖当前可执行的输入和计划。
    /// </summary>
    [Fact]
    public async Task LatestPrepareWinsWhenEarlierPlanCompletesLast()
    {
        DeferredPlanGateway gateway = new();
        InstallerSessionService service = new(gateway, new FixedTimeProvider(DateTimeOffset.UtcNow));
        var earlierOptions = CreateUnityLocalOptions("C:/packages/Earlier");
        var latestOptions = CreateUnityLocalOptions("C:/packages/Latest");

        var earlierPrepare = service.PrepareAsync(earlierOptions);
        await gateway.WaitForRequestAsync();
        var latestPrepare = service.PrepareAsync(latestOptions);
        await gateway.WaitForRequestAsync();

        gateway.CompleteLatest(latestOptions);
        var latestState = await latestPrepare;
        gateway.CompleteEarlier(earlierOptions);
        var finalState = await earlierPrepare;

        Assert.Equal(InstallerSessionStatus.PlanReady, latestState.Status);
        Assert.Same(latestOptions, latestState.Options);
        Assert.Equal("C:/packages/Latest", latestState.Plan!.Source);
        Assert.Equal(InstallerSessionStatus.PlanReady, finalState.Status);
        Assert.Same(latestOptions, finalState.Options);
        Assert.Equal("C:/packages/Latest", finalState.Plan!.Source);
    }

    /// <summary>
    /// 验证已被新输入替代且随后取消的旧计划生成不会把最新计划改写为失败状态。
    /// </summary>
    [Fact]
    public async Task CancelledStalePrepareDoesNotPublishFailedState()
    {
        DeferredPlanGateway gateway = new();
        InstallerSessionService service = new(gateway, new FixedTimeProvider(DateTimeOffset.UtcNow));
        var earlierOptions = CreateUnityLocalOptions("C:/packages/Earlier");
        var latestOptions = CreateUnityLocalOptions("C:/packages/Latest");
        using CancellationTokenSource earlierCancellation = new();

        var earlierPrepare = service.PrepareAsync(earlierOptions, earlierCancellation.Token);
        await gateway.WaitForRequestAsync();
        var latestPrepare = service.PrepareAsync(latestOptions);
        await gateway.WaitForRequestAsync();

        gateway.CompleteLatest(latestOptions);
        await latestPrepare;
        earlierCancellation.Cancel();
        var finalState = await earlierPrepare;

        Assert.Equal(InstallerSessionStatus.PlanReady, finalState.Status);
        Assert.Same(latestOptions, finalState.Options);
        Assert.Equal("C:/packages/Latest", finalState.Plan!.Source);
    }

    /// <summary>
    /// 验证执行期取消进入 Cancelled，而不是被错误归类为 Failed。
    /// </summary>
    [Fact]
    public async Task CancelledApplyPublishesCancelledState()
    {
        var gateway = new FakeInstallerWorkflowGateway { HoldExecution = true };
        var service = new InstallerSessionService(gateway, new FixedTimeProvider(DateTimeOffset.UtcNow));
        await service.PrepareAsync(CreateUnityLocalOptions());
        using CancellationTokenSource cancellation = new();

        var applyTask = service.ApplyAsync(cancellation.Token);
        await gateway.ExecutionStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        cancellation.Cancel();

        var finalState = await applyTask;

        Assert.Equal(InstallerSessionStatus.Cancelled, finalState.Status);
        Assert.Contains(finalState.Logs, entry => entry.Level == InstallerLogLevel.Warning);
    }

    /// <summary>
    /// 创建可用于状态机测试的 Unity 本地安装选项。
    /// </summary>
    /// <returns>Unity 本地安装选项。</returns>
    private static InstallerInstallOptions CreateUnityLocalOptions(string sourcePackageRoot = "C:/packages/YokiFrame")
    {
        return InstallerInstallOptions.CreateUnityLocal(
            sourcePackageRoot,
            "C:/projects/Game",
            InstallerLegacyPackagePolicy.Reject);
    }

    /// <summary>
    /// 提供完全可控的 Core gateway，用于隔离 Application 编排行为。
    /// </summary>
    private sealed class FakeInstallerWorkflowGateway : IInstallerWorkflowGateway
    {
        private readonly TaskCompletionSource<InstallerExecutionResult> mExecutionCompletion = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// 获取模拟安装计划。
        /// </summary>
        public InstallerPlanPreview Plan { get; } = CreatePlan();

        /// <summary>
        /// 获取模拟安装结果。
        /// </summary>
        public InstallerExecutionResult Result { get; init; } = new(
            "C:/projects/Game/Packages/com.hinatayoki.yokiframe",
            changed: true,
            replacedExistingPackage: false);

        /// <summary>
        /// 获取执行开始信号。
        /// </summary>
        public TaskCompletionSource ExecutionStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// 获取 gateway 执行次数。
        /// </summary>
        public int ExecutionCount { get; private set; }

        /// <summary>
        /// 获取或设置是否阻塞执行，供重复提交测试控制完成时机。
        /// </summary>
        public bool HoldExecution { get; init; }

        /// <summary>
        /// 获取或设置执行阶段最终抛出的异常。
        /// </summary>
        public Exception? ExecutionException { get; init; }

        /// <summary>
        /// 获取执行期间依次上报的进度。
        /// </summary>
        public List<InstallerProgressUpdate> ProgressUpdates { get; } = new();

        /// <summary>
        /// 返回预置安装计划，不访问真实文件系统。
        /// </summary>
        public Task<InstallerPlanPreview> CreatePlanAsync(
            InstallerInstallOptions options,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(Plan);
        }

        /// <summary>
        /// 按测试配置上报进度、阻塞、成功或抛出 Core 异常。
        /// </summary>
        public async Task<InstallerExecutionResult> ExecuteAsync(
            InstallerInstallOptions options,
            InstallerPlanPreview plan,
            IProgress<InstallerProgressUpdate> progress,
            CancellationToken cancellationToken)
        {
            ExecutionCount++;
            ExecutionStarted.TrySetResult();
            foreach (var update in ProgressUpdates)
            {
                progress.Report(update);
            }

            if (HoldExecution)
            {
                return await mExecutionCompletion.Task.WaitAsync(cancellationToken);
            }

            if (ExecutionException != null)
            {
                throw ExecutionException;
            }

            return Result;
        }

        /// <summary>
        /// 解除受控执行阻塞并返回成功结果。
        /// </summary>
        public void CompleteExecution()
        {
            mExecutionCompletion.TrySetResult(Result);
        }

        /// <summary>
        /// 创建不依赖磁盘的最小 Core 安装计划。
        /// </summary>
        /// <returns>最小安装计划。</returns>
        private static InstallerPlanPreview CreatePlan()
        {
            return new InstallerPlanPreview(
                InstallerTargetKind.Unity,
                InstallerInstallMode.UnityLocal,
                "C:/packages/YokiFrame",
                "C:/projects/Game",
                "C:/projects/Game/Packages/com.hinatayoki.yokiframe",
                Array.Empty<InstallerPlanActionPreview>(),
                Array.Empty<string>());
        }
    }

    /// <summary>
    /// 按请求顺序延迟返回计划，专门模拟忽略取消或完成乱序的底层规划器。
    /// </summary>
    private sealed class DeferredPlanGateway : IInstallerWorkflowGateway
    {
        private readonly TaskCompletionSource<InstallerPlanPreview> mEarlierPlan = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<InstallerPlanPreview> mLatestPlan = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly SemaphoreSlim mRequests = new(0);
        private int mRequestCount;

        /// <summary>
        /// 记录计划请求，并让第一请求响应其取消令牌、第二请求独立等待显式完成。
        /// </summary>
        /// <param name="options">当前请求的不可变安装输入。</param>
        /// <param name="cancellationToken">当前计划请求的取消令牌。</param>
        /// <returns>由测试决定完成时机的计划任务。</returns>
        public Task<InstallerPlanPreview> CreatePlanAsync(
            InstallerInstallOptions options,
            CancellationToken cancellationToken)
        {
            var requestNumber = Interlocked.Increment(ref mRequestCount);
            mRequests.Release();
            return requestNumber == 1
                ? mEarlierPlan.Task.WaitAsync(cancellationToken)
                : mLatestPlan.Task;
        }

        /// <summary>
        /// 此测试只覆盖计划生成乱序，任何执行调用都是不符合预期的失败。
        /// </summary>
        /// <param name="options">安装输入。</param>
        /// <param name="plan">待执行计划。</param>
        /// <param name="progress">执行进度通道。</param>
        /// <param name="cancellationToken">执行取消令牌。</param>
        /// <returns>不会成功完成的任务。</returns>
        public Task<InstallerExecutionResult> ExecuteAsync(
            InstallerInstallOptions options,
            InstallerPlanPreview plan,
            IProgress<InstallerProgressUpdate> progress,
            CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("DeferredPlanGateway does not support execution.");
        }

        /// <summary>
        /// 等待下一次计划请求已到达延迟 gateway。
        /// </summary>
        /// <returns>请求到达后完成的任务。</returns>
        public Task WaitForRequestAsync()
        {
            return mRequests.WaitAsync();
        }

        /// <summary>
        /// 完成第一条较早的计划请求。
        /// </summary>
        /// <param name="options">用于构造可识别预览的旧输入。</param>
        public void CompleteEarlier(InstallerInstallOptions options)
        {
            mEarlierPlan.TrySetResult(CreatePlan(options));
        }

        /// <summary>
        /// 完成第二条最新的计划请求。
        /// </summary>
        /// <param name="options">用于构造可识别预览的新输入。</param>
        public void CompleteLatest(InstallerInstallOptions options)
        {
            mLatestPlan.TrySetResult(CreatePlan(options));
        }

        /// <summary>
        /// 根据请求输入创建用于断言归属关系的最小计划。
        /// </summary>
        /// <param name="options">计划所属的安装输入。</param>
        /// <returns>包含输入来源和目标的最小预览。</returns>
        private static InstallerPlanPreview CreatePlan(InstallerInstallOptions options)
        {
            return new InstallerPlanPreview(
                InstallerTargetKind.Unity,
                options.Mode,
                options.SourcePackageRoot ?? string.Empty,
                options.TargetProjectRoot,
                "C:/projects/Game/Packages/com.hinatayoki.yokiframe",
                Array.Empty<InstallerPlanActionPreview>(),
                Array.Empty<string>());
        }
    }

    /// <summary>
    /// 提供固定 UTC 时间，确保日志时间戳断言稳定。
    /// </summary>
    /// <param name="utcNow">固定 UTC 时间。</param>
    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        /// <summary>
        /// 返回测试指定的固定 UTC 时间。
        /// </summary>
        /// <returns>固定 UTC 时间。</returns>
        public override DateTimeOffset GetUtcNow()
        {
            return utcNow;
        }
    }
}
