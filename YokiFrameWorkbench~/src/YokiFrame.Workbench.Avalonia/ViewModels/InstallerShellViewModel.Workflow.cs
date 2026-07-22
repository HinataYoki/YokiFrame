using YokiFrame.Tooling.Application.Installer;

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
        var selected = await mFolderPicker.PickFolderAsync("选择 YokiFrame 源目录");
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
        var selected = await mFolderPicker.PickFolderAsync("选择 Unity 或 Godot 项目根目录");
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

        mIsGodotRuntimeBootstrapRunning = true;
        OnPropertyChanged(nameof(IsGodotRuntimeBootstrapVisible));
        OnPropertyChanged(nameof(IsProgressIndeterminate));
        IsProgressVisible = true;
        ProgressValue = 0;
        SessionStatusText = "正在为 Godot 构建当前平台 Runtime";
        AppendLocalLog("正在从选定 YokiFrame 源码包构建 Godot 项目 Runtime。");
        RaiseCommandStates();
        try
        {
            await mGodotRuntimeBootstrapper.BootstrapAndOpenInstallerAsync(
                SourcePackageRoot,
                TargetProjectRoot).ConfigureAwait(true);
            mSession.InvalidatePlan();
            SessionStatusText = "当前 Runtime 已准备完成，新的安装器已打开";
            AppendLocalLog("Runtime 已构建完成，已打开与当前源码包匹配的新安装器。");
        }
        catch (Exception exception)
        {
            ShowLocalError(exception.Message);
        }
        finally
        {
            mIsGodotRuntimeBootstrapRunning = false;
            OnPropertyChanged(nameof(IsGodotRuntimeBootstrapVisible));
            OnPropertyChanged(nameof(IsProgressIndeterminate));
            IsProgressVisible = false;
            RaiseCommandStates();
        }
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
        await mSession.PrepareAsync(options, cancellationToken);
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
            InstallerTargetKind.Unity => "Unity",
            InstallerTargetKind.Godot => "Godot",
            _ => "未检测"
        };
        TargetStatusText = target.IsRecognized ? target.PackageTarget : "路径无效或不是支持的项目";
        NotifyTargetPresentationChanged();
    }

    /// <summary>
    /// 显示路径、版本或项目结构检测失败，并使旧计划失去安装资格。
    /// </summary>
    /// <param name="message">检测错误。</param>
    private void ApplyDetectionFailure(string message)
    {
        mTargetKind = InstallerTargetKind.Unknown;
        EngineStatusText = "未检测";
        TargetStatusText = "路径无效或不是支持的项目";
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
