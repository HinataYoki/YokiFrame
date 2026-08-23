using YokiFrame.Tooling.Application.Installer;
using YokiFrame.Workbench.Avalonia.Services;

namespace YokiFrame.Workbench.Avalonia.ViewModels;

public sealed partial class InstallerShellViewModel
{
    /// <summary>
    /// 使用启动路径执行首次目标检测和计划生成。
    /// </summary>
    /// <returns>首次检测完成任务。</returns>
    public Task InitializeAsync()
    {
        return RefreshPlanAsync();
    }

    /// <summary>
    /// 根据当前输入调度 latest-wins 计划生成，连续输入只保留最后一次。
    /// </summary>
    /// <returns>本次调度被替代或完成时结束的任务。</returns>
    public Task RefreshPlanAsync()
    {
        if (IsSessionBusy())
        {
            return Task.CompletedTask;
        }

        mSession.InvalidatePlan();
        var options = TryCreateInstallOptions();
        if (options == null)
        {
            return Task.CompletedTask;
        }

        return mInputDetection.ScheduleAsync(options, PreparePlanAsync);
    }

    /// <summary>
    /// 显式生成最新计划并展开动作与警告日志，保留自动检测的安静行为。
    /// </summary>
    /// <returns>计划和日志投影完成任务。</returns>
    private async Task PreviewPlanAsync()
    {
        await RefreshPlanAsync();
        var state = mSession.State;
        if (state.Status == InstallerSessionStatus.PlanReady && state.Plan != null)
        {
            PresentPlanOnce(state.Plan);
        }
    }

    /// <summary>
    /// 打开源包目录选择器并用选择结果刷新安装计划。
    /// </summary>
    /// <returns>目录选择和检测完成任务。</returns>
    public async Task PickSourceAsync()
    {
        var selected = await mFolderPicker.PickFolderAsync(
            WorkbenchI18nService.Instance.GetString(
                "String.Installer.SourceDirectoryPickerTitle",
                "选择 YokiFrame 源目录"));
        if (string.IsNullOrWhiteSpace(selected))
        {
            return;
        }

        SourcePackageRoot = selected;
        await RefreshPlanAsync();
    }

    /// <summary>
    /// 打开目标项目目录选择器并用选择结果刷新引擎检测。
    /// </summary>
    /// <returns>目录选择和检测完成任务。</returns>
    public async Task PickTargetAsync()
    {
        var selected = await mFolderPicker.PickFolderAsync(
            WorkbenchI18nService.Instance.GetString(
                "String.Installer.TargetDirectoryPickerTitle",
                "选择 Unity 或 Godot 项目根目录"));
        if (string.IsNullOrWhiteSpace(selected))
        {
            return;
        }

        TargetProjectRoot = selected;
        await RefreshPlanAsync();
    }

    /// <summary>
    /// 调用 Application 执行当前计划，并由状态事件持续更新进度和日志。
    /// </summary>
    /// <returns>安装会话完成任务。</returns>
    private async Task InstallAsync()
    {
        try
        {
            await mSession.ApplyAsync();
        }
        catch (Exception exception)
        {
            ShowLocalError(exception.Message);
        }
    }

    /// <summary>
    /// 在用户明确点击后从当前选择的源码包构建 Godot 项目缓存，并打开与该源码匹配的新 Installer。
    /// </summary>
    /// <returns>构建和新 Installer 启动完成任务。</returns>
    private async Task BootstrapGodotRuntimeAsync()
    {
        if (!CanBootstrapGodotRuntime())
        {
            return;
        }

        await mGodotRuntimeBootstrapGate.WaitAsync();
        try
        {
            if (!CanBootstrapGodotRuntime())
            {
                return;
            }

            await RunGodotRuntimeBootstrapProcessAsync(
                openInstaller: true,
                CancellationToken.None).ConfigureAwait(true);
            mSession.InvalidatePlan();
            SessionStatusText = WorkbenchI18nService.Instance.GetString(
                "String.Installer.Session.RuntimeReadyNewInstaller",
                "当前 Runtime 已准备完成，新的安装器已打开");
            AppendLocalLog(WorkbenchI18nService.Instance.GetString(
                "String.Installer.Log.RuntimeReadyNewInstaller",
                "Runtime 已构建完成，已打开与当前源码包匹配的新安装器。"));
        }
        catch (Exception exception)
        {
            ShowLocalError(exception.Message);
        }
        finally
        {
            mGodotRuntimeBootstrapGate.Release();
        }
    }

    /// <summary>
    /// 在首次 Godot 计划缺少 Runtime 缓存时自动构建，并在同一 Installer 会话中重新规划。
    /// </summary>
    /// <param name="options">触发缓存门控的当前安装输入。</param>
    /// <param name="cancellationToken">输入被替代或窗口关闭时使用的令牌。</param>
    /// <returns>缓存构建和重新规划完成任务。</returns>
    private async Task BootstrapGodotRuntimeForPlanAsync(
        InstallerInstallOptions options,
        CancellationToken cancellationToken)
    {
        await mGodotRuntimeBootstrapGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!RequiresGodotRuntimeBootstrap(mSession.State, options))
            {
                return;
            }

            await RunGodotRuntimeBootstrapProcessAsync(
                openInstaller: false,
                cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            PostToUi(() => AppendLocalLog(WorkbenchI18nService.Instance.GetString(
                "String.Installer.Log.RuntimeReadyReplanning",
                "Runtime 已构建完成，正在重新生成安装计划。")));
            await mSession.PrepareAsync(options, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            mGodotRuntimeBootstrapGate.Release();
        }
    }

    /// <summary>
    /// 执行一次 Runtime bootstrap 子进程，并统一管理构建期间的页面状态。
    /// </summary>
    /// <param name="openInstaller">成功后是否启动新的 Installer。</param>
    /// <param name="cancellationToken">当前构建取消令牌。</param>
    /// <returns>子进程完成任务。</returns>
    private async Task RunGodotRuntimeBootstrapProcessAsync(
        bool openInstaller,
        CancellationToken cancellationToken)
    {
        BeginGodotRuntimeBootstrapPresentation(openInstaller);
        var succeeded = false;
        try
        {
            if (openInstaller)
            {
                await mGodotRuntimeBootstrapper.BootstrapAndOpenInstallerAsync(
                    SourcePackageRoot,
                    TargetProjectRoot,
                    cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await mGodotRuntimeBootstrapper.BootstrapAsync(
                    SourcePackageRoot,
                    TargetProjectRoot,
                    cancellationToken).ConfigureAwait(false);
            }

            succeeded = true;
        }
        finally
        {
            await EndGodotRuntimeBootstrapPresentationAsync(succeeded).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// 发布 Runtime 构建开始状态；后台计划线程通过 UI 上下文更新 Avalonia 绑定。
    /// </summary>
    /// <param name="openInstaller">成功后是否会启动新的 Installer。</param>
    private void BeginGodotRuntimeBootstrapPresentation(bool openInstaller)
    {
        mIsGodotRuntimeBootstrapRunning = true;
        mIsGodotRuntimeBootstrapOpeningInstaller = openInstaller;
        var message = GetBootstrapStatusText(openInstaller);
        PostToUi(() =>
        {
            OnPropertyChanged(nameof(IsGodotRuntimeBootstrapVisible));
            OnPropertyChanged(nameof(IsProgressIndeterminate));
            IsProgressVisible = true;
            ProgressValue = 0;
            SessionStatusText = message;
            ClearOutcomeDetails();
            AppendLocalLog(WorkbenchI18nService.Instance.GetString(
                "String.Installer.Log.BuildingGodotRuntime",
                "正在从选定 YokiFrame 源码包构建 Godot 项目 Runtime。"));
            RaiseCommandStates();
        });
    }

    /// <summary>
    /// 发布 Runtime 构建结束状态；成功时清除旧的前置失败，失败时恢复真实错误详情。
    /// </summary>
    /// <param name="succeeded">Runtime 构建是否成功。</param>
    private Task EndGodotRuntimeBootstrapPresentationAsync(bool succeeded)
    {
        mIsGodotRuntimeBootstrapRunning = false;
        mIsGodotRuntimeBootstrapOpeningInstaller = false;
        return PostToUiAndWaitAsync(() =>
        {
            OnPropertyChanged(nameof(IsGodotRuntimeBootstrapVisible));
            OnPropertyChanged(nameof(IsProgressIndeterminate));
            IsProgressVisible = false;
            if (succeeded)
            {
                ClearOutcomeDetails();
            }
            else
            {
                ApplyOutcomeDetails(mSession.State);
            }

            RaiseCommandStates();
        });
    }

    /// <summary>
    /// 等待当前线程之前投递到 UI 上下文的状态投影完成，确保工作流任务返回时页面已达到同一终态。
    /// </summary>
    /// <param name="action">需要在 UI 上下文执行的状态更新。</param>
    /// <returns>状态更新执行完成任务。</returns>
    private Task PostToUiAndWaitAsync(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (mSynchronizationContext == null
            || ReferenceEquals(SynchronizationContext.Current, mSynchronizationContext))
        {
            action();
            return Task.CompletedTask;
        }

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        mSynchronizationContext.Post(
            static state =>
            {
                var dispatch = (UiDispatch)state!;
                try
                {
                    dispatch.Action();
                    dispatch.Completion.TrySetResult();
                }
                catch (Exception exception)
                {
                    dispatch.Completion.TrySetException(exception);
                }
            },
            new UiDispatch(action, completion));
        return completion.Task;
    }

    /// <summary>
    /// 将非 UI 计划线程的页面更新投递回创建 ViewModel 的上下文。
    /// </summary>
    /// <param name="action">需要在 UI 上下文执行的更新。</param>
    private void PostToUi(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (mSynchronizationContext == null
            || ReferenceEquals(SynchronizationContext.Current, mSynchronizationContext))
        {
            action();
            return;
        }

        mSynchronizationContext.Post(static state => ((Action)state!).Invoke(), action);
    }

    /// <summary>
    /// 把稳定输入交给 Application 会话生成计划。
    /// </summary>
    /// <param name="options">输入快照。</param>
    /// <param name="cancellationToken">latest-wins 取消令牌。</param>
    /// <returns>计划生成完成任务。</returns>
    private async Task PreparePlanAsync(
        InstallerInstallOptions options,
        CancellationToken cancellationToken)
    {
        await mSession.PrepareAsync(options, cancellationToken).ConfigureAwait(false);
        if (RequiresGodotRuntimeBootstrap(mSession.State, options))
        {
            await BootstrapGodotRuntimeForPlanAsync(options, cancellationToken).ConfigureAwait(false);
        }

        // 计划任务完成必须与页面看到的最新会话终态一致，不能只等待一个空投递。
        await PostToUiAndWaitAsync(() => ApplySessionState(mSession.State)).ConfigureAwait(false);
    }

    /// <summary>
    /// 表示一次必须等待完成的 UI 状态投递，避免后台计划任务在页面终态可见前提前返回。
    /// </summary>
    private sealed class UiDispatch
    {
        /// <summary>创建 UI 投递。</summary>
        /// <param name="action">UI 状态更新。</param>
        /// <param name="completion">完成通知。</param>
        public UiDispatch(Action action, TaskCompletionSource completion)
        {
            Action = action;
            Completion = completion;
        }

        /// <summary>获取 UI 状态更新。</summary>
        public Action Action { get; }

        /// <summary>获取完成通知。</summary>
        public TaskCompletionSource Completion { get; }
    }

    /// <summary>
    /// 为输入变化启动自动刷新，并在后台任务内部收口预期异常。
    /// </summary>
    private void ScheduleAutomaticRefresh()
    {
        _ = RefreshPlanSafelyAsync();
    }

    /// <summary>
    /// 执行自动刷新并把未进入 Application 状态机的错误显示到当前页面。
    /// </summary>
    /// <returns>自动刷新完成任务。</returns>
    private async Task RefreshPlanSafelyAsync()
    {
        try
        {
            await RefreshPlanAsync();
        }
        catch (OperationCanceledException)
        {
            // 输入变化采用 latest-wins，旧刷新被后继请求取消属于预期控制流。
        }
        catch (Exception exception)
        {
            ShowLocalError(exception.Message);
        }
    }

    /// <summary>
    /// 检测当前目标并创建与宿主、来源和 legacy 策略一致的 Application 输入。
    /// </summary>
    /// <returns>有效输入；目标无效时返回 null。</returns>
    private InstallerInstallOptions? TryCreateInstallOptions()
    {
        InstallerTargetInfo target;
        try
        {
            target = mTargetDetection.Detect(TargetProjectRoot);
        }
        catch (Exception exception)
        {
            ApplyDetectionFailure(exception.Message);
            return null;
        }

        ApplyTargetInfo(target);
        if (!target.IsRecognized)
        {
            return null;
        }

        var policy = ConfirmLegacyTakeover
            ? InstallerLegacyPackagePolicy.TakeOverConfirmed
            : InstallerLegacyPackagePolicy.Reject;
        return mInstallMode switch
        {
            InstallerInstallMode.UnityLocal => InstallerInstallOptions.CreateUnityLocal(
                SourcePackageRoot,
                target.ProjectRoot,
                policy),
            InstallerInstallMode.UnityGit => InstallerInstallOptions.CreateUnityGit(
                target.ProjectRoot,
                string.IsNullOrWhiteSpace(GitUrl) ? DEFAULT_GIT_URL : GitUrl),
            InstallerInstallMode.GodotLocal => InstallerInstallOptions.CreateGodotLocal(
                SourcePackageRoot,
                target.ProjectRoot,
                new GodotInstallOptions(RepairGodotProjectSettings, EnableGodotPlugin),
                policy),
            _ => throw new ArgumentOutOfRangeException(nameof(mInstallMode))
        };
    }

    /// <summary>
    /// 应用检测结果并在 Unity/Godot 间自动切换对应选项组。
    /// </summary>
    /// <param name="target">Application 目标只读模型。</param>
    private void ApplyTargetInfo(InstallerTargetInfo target)
    {
        mTargetKind = target.Kind;
        if (target.Kind == InstallerTargetKind.Godot)
        {
            SetInstallMode(InstallerInstallMode.GodotLocal, scheduleRefresh: false);
        }
        else if (target.Kind == InstallerTargetKind.Unity
            && mInstallMode == InstallerInstallMode.GodotLocal)
        {
            SetInstallMode(InstallerInstallMode.UnityLocal, scheduleRefresh: false);
        }

        EngineStatusText = target.Kind switch
        {
            InstallerTargetKind.Unity => GetEngineText(InstallerTargetKind.Unity),
            InstallerTargetKind.Godot => GetEngineText(InstallerTargetKind.Godot),
            _ => GetEngineText(InstallerTargetKind.Unknown)
        };
        TargetStatusText = target.IsRecognized
            ? target.PackageTarget
            : WorkbenchI18nService.Instance.GetString(
                "String.Installer.TargetInvalid",
                "路径无效或不是支持的项目");
        NotifyTargetPresentationChanged();
    }

    /// <summary>
    /// 显示路径、版本或项目结构检测失败，并使旧计划失去安装资格。
    /// </summary>
    /// <param name="message">检测错误。</param>
    private void ApplyDetectionFailure(string message)
    {
        mTargetKind = InstallerTargetKind.Unknown;
        EngineStatusText = GetEngineText(InstallerTargetKind.Unknown);
        TargetStatusText = WorkbenchI18nService.Instance.GetString(
            "String.Installer.TargetInvalid",
            "路径无效或不是支持的项目");
        SessionStatusText = message;
        NotifyTargetPresentationChanged();
        RaiseCommandStates();
    }

    /// <summary>
    /// 切换 Unity 本地/Git 或 Godot 安装模式，并刷新所有派生显隐状态。
    /// </summary>
    /// <param name="mode">新安装模式。</param>
    /// <param name="scheduleRefresh">是否按新模式重新生成计划。</param>
    private void SetInstallMode(InstallerInstallMode mode, bool scheduleRefresh = true)
    {
        if (mInstallMode == mode)
        {
            return;
        }

        mInstallMode = mode;
        OnPropertyChanged(nameof(IsUnityLocalSelected));
        OnPropertyChanged(nameof(IsUnityGitSelected));
        OnPropertyChanged(nameof(IsSourcePathVisible));
        OnPropertyChanged(nameof(IsGitUrlVisible));
        OnPropertyChanged(nameof(SelectedInstallModeText));
        if (scheduleRefresh)
        {
            ScheduleAutomaticRefresh();
        }
    }

    /// <summary>
    /// 判断当前输入是否可以生成预览。
    /// </summary>
    /// <returns>目标已识别且会话不忙时返回 true。</returns>
    private bool CanPreviewPlan()
    {
        return mTargetKind != InstallerTargetKind.Unknown && !IsSessionBusy();
    }

    /// <summary>
    /// 判断当前 Application 计划是否仍与页面输入一致并允许执行。
    /// </summary>
    /// <returns>计划就绪且输入未漂移时返回 true。</returns>
    private bool CanInstallPlan()
    {
        var state = mSession.State;
        return state.Status == InstallerSessionStatus.PlanReady
            && state.Plan != null
            && state.Plan.Mode == mInstallMode
            && string.Equals(
                Path.GetFullPath(state.Plan.TargetProjectRoot),
                Path.GetFullPath(TargetProjectRoot),
                StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 判断失败或冲突状态是否允许重新检测。
    /// </summary>
    /// <returns>目标仍有效且当前为失败终态时返回 true。</returns>
    private bool CanRetryPlan()
    {
        var status = mSession.State.Status;
        return mTargetKind != InstallerTargetKind.Unknown
            && status is InstallerSessionStatus.Conflict or InstallerSessionStatus.Failed;
    }

    /// <summary>
    /// 判断当前失败是否由可恢复的 Godot Runtime 缓存前置条件导致，且没有同类构建正在运行。
    /// </summary>
    /// <returns>可从源码包构建缓存并重新打开 Installer 时返回 true。</returns>
    private bool CanBootstrapGodotRuntime()
    {
        var state = mSession.State;
        return !mIsGodotRuntimeBootstrapRunning
            && mTargetKind == InstallerTargetKind.Godot
            && state.Status == InstallerSessionStatus.Failed
            && state.RuntimeBootstrapRequired;
    }

    /// <summary>
    /// 判断某次计划失败是否仍属于当前输入且可以自动构建 Godot Runtime。
    /// </summary>
    /// <param name="state">当前 Installer 会话状态。</param>
    /// <param name="options">触发计划的输入快照。</param>
    /// <returns>当前输入因 Runtime 缓存缺失失败时返回 true。</returns>
    private static bool RequiresGodotRuntimeBootstrap(
        InstallerSessionState state,
        InstallerInstallOptions options)
    {
        return options.Mode == InstallerInstallMode.GodotLocal
            && state.Status == InstallerSessionStatus.Failed
            && state.RuntimeBootstrapRequired
            && ReferenceEquals(state.Options, options);
    }

    /// <summary>
    /// 判断 Application 是否正在占用安装写事务。
    /// </summary>
    /// <returns>应用、校验或回滚时返回 true。</returns>
    private bool IsSessionBusy()
    {
        return mSession.State.Status is InstallerSessionStatus.Applying
            or InstallerSessionStatus.Verifying
            or InstallerSessionStatus.RollingBack;
    }
}
