using YokiFrame.Installer.Core.Services;

namespace YokiFrame.Tooling.Application.Installer;

/// <summary>
/// 编排 Installer 检测、规划和事务执行，并向 UI 或 CLI 发布稳定状态快照。
/// </summary>
public sealed class InstallerSessionService
{
    private readonly object mSyncRoot = new();
    private readonly IInstallerWorkflowGateway mGateway;
    private readonly TimeProvider mTimeProvider;
    private InstallerSessionState mState = new();
    private Task<InstallerSessionState>? mApplyTask;
    private long mPreparationGeneration;

    /// <summary>
    /// 使用系统时间创建 Installer 会话服务。
    /// </summary>
    /// <param name="gateway">Installer.Core 编排 gateway。</param>
    public InstallerSessionService(IInstallerWorkflowGateway gateway)
        : this(gateway, TimeProvider.System)
    {
    }

    /// <summary>
    /// 使用可控时间源创建 Installer 会话服务。
    /// </summary>
    /// <param name="gateway">Installer.Core 编排 gateway。</param>
    /// <param name="timeProvider">日志 UTC 时间来源。</param>
    public InstallerSessionService(IInstallerWorkflowGateway gateway, TimeProvider timeProvider)
    {
        mGateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
        mTimeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <summary>
    /// 在会话状态变化后发布不可变快照。
    /// </summary>
    public event EventHandler<InstallerSessionStateChangedEventArgs>? StateChanged;

    /// <summary>
    /// 获取当前 Installer 会话快照。
    /// </summary>
    public InstallerSessionState State
    {
        get
        {
            lock (mSyncRoot)
            {
                return mState;
            }
        }
    }

    /// <summary>
    /// 立即废弃尚未执行的计划，避免 UI 输入变更后在防抖窗口内继续提交旧 options/plan。
    /// </summary>
    public void InvalidatePlan()
    {
        long preparationGeneration;
        lock (mSyncRoot)
        {
            if (IsBusy(mState.Status))
            {
                return;
            }

            mApplyTask = null;
            preparationGeneration = ++mPreparationGeneration;
        }

        UpdateState(
            current => current with
            {
                Status = InstallerSessionStatus.Idle,
                Options = null,
                Plan = null,
                Result = null,
                Progress = null,
                ConflictPaths = Array.Empty<string>(),
                EvidencePaths = Array.Empty<string>(),
                RollbackSucceeded = null,
                ErrorMessage = string.Empty,
                RuntimeBootstrapRequired = false
            },
            InstallerLogLevel.Information,
            "安装输入已变更，当前计划已失效。",
            preparationGeneration);
    }

    /// <summary>
    /// 检测当前输入并生成安装计划；预期错误转换为 Conflict 或 Failed 状态。
    /// </summary>
    /// <param name="options">安装输入。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>检测结束后的会话快照。</returns>
    public async Task<InstallerSessionState> PrepareAsync(
        InstallerInstallOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        var preparationGeneration = BeginPreparation(options);
        try
        {
            var plan = await mGateway.CreatePlanAsync(options, cancellationToken).ConfigureAwait(false);
            SetPlanReady(preparationGeneration, plan);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (InstallerConflictException exception)
        {
            SetConflict(exception, preparationGeneration);
        }
        catch (Exception exception)
        {
            SetFailure(exception, preparationGeneration);
        }

        return State;
    }

    /// <summary>
    /// 执行当前计划；并发重复提交复用同一任务，避免重复进入 Core 写事务。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>安装结束后的会话快照。</returns>
    public Task<InstallerSessionState> ApplyAsync(CancellationToken cancellationToken = default)
    {
        TaskCompletionSource<InstallerSessionState> completion;
        InstallerInstallOptions options;
        InstallerPlanPreview plan;
        lock (mSyncRoot)
        {
            if (mApplyTask is { IsCompleted: false })
            {
                return cancellationToken.CanBeCanceled
                    ? mApplyTask.WaitAsync(cancellationToken)
                    : mApplyTask;
            }

            EnsurePlanReady();
            options = mState.Options!;
            plan = mState.Plan!;
            completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            mApplyTask = completion.Task;
        }

        _ = CompleteApplyAsync(options, plan, cancellationToken, completion);
        return completion.Task;
    }

    /// <summary>
    /// 清空旧执行任务、分配新 generation 并发布新一轮检测状态。
    /// </summary>
    /// <param name="options">新一轮安装输入。</param>
    /// <returns>仅允许本轮规划提交终态的 generation。</returns>
    private long BeginPreparation(InstallerInstallOptions options)
    {
        long preparationGeneration;
        lock (mSyncRoot)
        {
            if (IsBusy(mState.Status))
            {
                throw new InvalidOperationException("Installer cannot prepare a new plan while an installation is running.");
            }

            mApplyTask = null;
            preparationGeneration = ++mPreparationGeneration;
        }

        UpdateState(
            _ => new InstallerSessionState { Status = InstallerSessionStatus.Detecting, Options = options },
            InstallerLogLevel.Information,
            "正在检测目标项目并生成安装计划。",
            preparationGeneration);
        return preparationGeneration;
    }

    /// <summary>
    /// 执行安装并把所有终态写入共享完成源，防止后台异常遗失。
    /// </summary>
    /// <param name="options">生成计划时的安装输入。</param>
    /// <param name="plan">待执行计划。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <param name="completion">重复提交共享的完成源。</param>
    private async Task CompleteApplyAsync(
        InstallerInstallOptions options,
        InstallerPlanPreview plan,
        CancellationToken cancellationToken,
        TaskCompletionSource<InstallerSessionState> completion)
    {
        try
        {
            var state = await ExecuteApplyAsync(options, plan, cancellationToken).ConfigureAwait(false);
            completion.TrySetResult(state);
        }
        catch (Exception exception)
        {
            SetFailure(exception);
            completion.TrySetResult(State);
        }
    }

    /// <summary>
    /// 调用 Core gateway，并把成功、冲突和事务回滚映射为应用状态。
    /// </summary>
    /// <param name="options">生成计划时的安装输入。</param>
    /// <param name="plan">待执行计划。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>执行完成后的会话快照。</returns>
    private async Task<InstallerSessionState> ExecuteApplyAsync(
        InstallerInstallOptions options,
        InstallerPlanPreview plan,
        CancellationToken cancellationToken)
    {
        SetApplying();
        try
        {
            InlineProgress progress = new(ReportProgress);
            var result = await mGateway.ExecuteAsync(options, plan, progress, cancellationToken).ConfigureAwait(false);
            SetSucceeded(result);
        }
        catch (InstallerConflictException exception)
        {
            SetConflict(exception);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            SetCancelled();
        }
        catch (InstallerExecutionException exception)
        {
            EnsureRollbackVisible(exception);
            SetTransactionFailure(exception);
        }
        catch (Exception exception)
        {
            SetFailure(exception);
        }

        return State;
    }

    /// <summary>
    /// 校验当前状态具备唯一可执行计划。
    /// </summary>
    private void EnsurePlanReady()
    {
        if (mState.Status != InstallerSessionStatus.PlanReady || mState.Options == null || mState.Plan == null)
        {
            throw new InvalidOperationException("Installer requires a ready plan before apply.");
        }
    }

    /// <summary>
    /// 发布计划就绪状态并保留 Core dry-run 结果。
    /// </summary>
    /// <param name="preparationGeneration">生成此计划的会话 generation。</param>
    /// <param name="plan">已生成计划。</param>
    private void SetPlanReady(long preparationGeneration, InstallerPlanPreview plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        UpdateState(
            current => current with { Status = InstallerSessionStatus.PlanReady, Plan = plan },
            InstallerLogLevel.Information,
            "安装计划已生成，可以开始安装。",
            preparationGeneration);
    }

    /// <summary>
    /// 发布进入 Core 写事务前的应用状态。
    /// </summary>
    private void SetApplying()
    {
        InstallerProgressUpdate progress = new(InstallerProgressStage.Applying, 0, 1, "开始应用安装计划。");
        UpdateState(
            current => current with { Status = InstallerSessionStatus.Applying, Progress = progress },
            InstallerLogLevel.Information,
            progress.Message);
    }

    /// <summary>
    /// 把 gateway 进度阶段映射为应用会话状态并追加日志。
    /// </summary>
    /// <param name="progress">gateway 进度更新。</param>
    private void ReportProgress(InstallerProgressUpdate progress)
    {
        ArgumentNullException.ThrowIfNull(progress);
        var status = progress.Stage switch
        {
            InstallerProgressStage.Applying => InstallerSessionStatus.Applying,
            InstallerProgressStage.Verifying => InstallerSessionStatus.Verifying,
            InstallerProgressStage.RollingBack => InstallerSessionStatus.RollingBack,
            _ => throw new ArgumentOutOfRangeException(nameof(progress))
        };
        UpdateState(
            current => current with { Status = status, Progress = progress },
            InstallerLogLevel.Information,
            progress.Message);
    }

    /// <summary>
    /// 发布成功终态并清除旧错误信息。
    /// </summary>
    /// <param name="result">Core 事务结果。</param>
    private void SetSucceeded(InstallerExecutionResult result)
    {
        var needsVerification = result.CommittedNeedsVerification;
        UpdateState(
            current => current with
            {
                Status = needsVerification
                    ? InstallerSessionStatus.CommittedNeedsVerification
                    : InstallerSessionStatus.Succeeded,
                Result = result,
                EvidencePaths = result.EvidencePaths.ToArray(),
                ErrorMessage = needsVerification ? result.VerificationError : string.Empty,
                RuntimeBootstrapRequired = false
            },
            needsVerification ? InstallerLogLevel.Warning : InstallerLogLevel.Information,
            needsVerification
                ? "YokiFrame 已提交，但宿主 post-verify 尚未完成。"
                : "YokiFrame 安装和校验已完成。");
    }

    /// <summary>
    /// 发布所有权冲突终态，并保留 Core 返回的稳定相对路径。
    /// </summary>
    /// <param name="exception">Core 所有权拒绝。</param>
    /// <param name="expectedPreparationGeneration">规划终态必须匹配的 generation；执行期为空。</param>
    private void SetConflict(
        InstallerConflictException exception,
        long? expectedPreparationGeneration = null)
    {
        UpdateState(
            current => current with
            {
                Status = InstallerSessionStatus.Conflict,
                Result = null,
                ConflictPaths = exception.ConflictPaths.ToArray(),
                ErrorMessage = exception.Message,
                RuntimeBootstrapRequired = false
            },
            InstallerLogLevel.Warning,
            exception.Message,
            expectedPreparationGeneration);
    }

    /// <summary>
    /// 在 gateway 未显式上报时补发 RollingBack，确保事务恢复对调用方可见。
    /// </summary>
    /// <param name="exception">包含 Core 回滚结果的事务异常。</param>
    private void EnsureRollbackVisible(InstallerExecutionException exception)
    {
        if (State.Status == InstallerSessionStatus.RollingBack)
        {
            return;
        }

        var message = exception.RollbackSucceeded ? "安装失败，已恢复原安装。" : "安装失败，回滚未完整完成。";
        ReportProgress(new InstallerProgressUpdate(InstallerProgressStage.RollingBack, 1, 1, message));
    }

    /// <summary>
    /// 发布事务失败终态，并保留持久化诊断证据和回滚结果。
    /// </summary>
    /// <param name="exception">Core 事务异常。</param>
    private void SetTransactionFailure(InstallerExecutionException exception)
    {
        UpdateState(
            current => current with
            {
                Status = InstallerSessionStatus.Failed,
                Result = null,
                EvidencePaths = exception.EvidencePaths.ToArray(),
                RollbackSucceeded = exception.RollbackSucceeded,
                ErrorMessage = exception.Message,
                RuntimeBootstrapRequired = false
            },
            InstallerLogLevel.Error,
            exception.Message);
    }

    /// <summary>
    /// 发布取消终态；取消只表示调用方停止等待，不推断 Runtime/Installer 没有发生写入。
    /// </summary>
    private void SetCancelled()
    {
        UpdateState(
            current => current with
            {
                Status = InstallerSessionStatus.Cancelled,
                Result = null,
                ErrorMessage = "安装操作已取消；如取消发生在提交阶段，请先检查证据和目标目录。",
                RuntimeBootstrapRequired = false
            },
            InstallerLogLevel.Warning,
            "安装操作已取消；请根据证据确认目标项目状态。 ");
    }

    /// <summary>
    /// 发布不带 Core 事务证据的普通失败终态。
    /// </summary>
    /// <param name="exception">检测、规划或执行异常。</param>
    /// <param name="expectedPreparationGeneration">规划终态必须匹配的 generation；执行期为空。</param>
    private void SetFailure(Exception exception, long? expectedPreparationGeneration = null)
    {
        UpdateState(
            current => current with
            {
                Status = InstallerSessionStatus.Failed,
                Result = null,
                ErrorMessage = exception.Message,
                RuntimeBootstrapRequired = RuntimeCacheBootstrapRequirement.IsRequired(exception)
            },
            InstallerLogLevel.Error,
            exception.Message,
            expectedPreparationGeneration);
    }

    /// <summary>
    /// 原子更新会话快照、追加时间戳日志，并在锁外发布事件；可选 generation 防止旧规划回写。
    /// </summary>
    /// <param name="update">基于当前快照创建下一快照的函数。</param>
    /// <param name="level">本次状态变化的日志级别。</param>
    /// <param name="message">本次状态变化的日志说明。</param>
    /// <param name="expectedPreparationGeneration">仅接受指定规划 generation 的更新；为空时不做限制。</param>
    /// <returns>状态实际提交时返回 true；被较新 generation 丢弃时返回 false。</returns>
    private bool UpdateState(
        Func<InstallerSessionState, InstallerSessionState> update,
        InstallerLogLevel level,
        string message,
        long? expectedPreparationGeneration = null)
    {
        InstallerSessionState nextState;
        EventHandler<InstallerSessionStateChangedEventArgs>? handler;
        lock (mSyncRoot)
        {
            if (expectedPreparationGeneration.HasValue
                && expectedPreparationGeneration.Value != mPreparationGeneration)
            {
                return false;
            }

            nextState = update(mState);
            nextState = nextState with { Logs = AppendLog(nextState.Logs, level, message) };
            mState = nextState;
            handler = StateChanged;
        }

        handler?.Invoke(this, new InstallerSessionStateChangedEventArgs(nextState));
        return true;
    }

    /// <summary>
    /// 复制旧日志并追加一条带 UTC 时间戳的新日志，避免外部修改内部集合。
    /// </summary>
    /// <param name="logs">当前日志快照。</param>
    /// <param name="level">新日志级别。</param>
    /// <param name="message">新日志说明。</param>
    /// <returns>追加后的日志快照。</returns>
    private IReadOnlyList<InstallerLogEntry> AppendLog(
        IReadOnlyList<InstallerLogEntry> logs,
        InstallerLogLevel level,
        string message)
    {
        InstallerLogEntry[] appended = new InstallerLogEntry[logs.Count + 1];
        for (var index = 0; index < logs.Count; index++)
        {
            appended[index] = logs[index];
        }

        appended[^1] = new InstallerLogEntry(mTimeProvider.GetUtcNow(), level, message);
        return appended;
    }

    /// <summary>
    /// 判断状态是否正在占用 Core 写事务。
    /// </summary>
    /// <param name="status">待检查状态。</param>
    /// <returns>正在应用、校验或回滚时返回 true。</returns>
    private static bool IsBusy(InstallerSessionStatus status)
    {
        return status is InstallerSessionStatus.Applying
            or InstallerSessionStatus.Verifying
            or InstallerSessionStatus.RollingBack;
    }

    /// <summary>
    /// 同步转发 gateway 进度，避免 Progress 泛型捕获 UI SynchronizationContext 后改变状态顺序。
    /// </summary>
    /// <param name="report">进度处理函数。</param>
    private sealed class InlineProgress(Action<InstallerProgressUpdate> report) : IProgress<InstallerProgressUpdate>
    {
        /// <summary>
        /// 在 gateway 调用线程同步发布进度。
        /// </summary>
        /// <param name="value">进度更新。</param>
        public void Report(InstallerProgressUpdate value)
        {
            report(value);
        }
    }
}
