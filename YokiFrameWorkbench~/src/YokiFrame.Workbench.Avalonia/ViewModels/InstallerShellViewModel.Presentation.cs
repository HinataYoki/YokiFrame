using System.Globalization;
using YokiFrame.Tooling.Application.Installer;
using YokiFrame.Workbench.Avalonia.Services;

namespace YokiFrame.Workbench.Avalonia.ViewModels;

public sealed partial class InstallerShellViewModel
{
    /// <summary>
    /// 响应语言切换并重新投影当前 Installer 会话的动态展示文本；路径、错误原文和协议值保持不变。
    /// </summary>
    private void OnCultureChanged()
    {
        if (mIsDisposed)
        {
            return;
        }

        var state = mSession.State;
        EngineStatusText = GetEngineText(mTargetKind);
        OnPropertyChanged(nameof(CurrentPlatformText));
        OnPropertyChanged(nameof(SelectedInstallModeText));
        if (state.Plan == null && mTargetKind == InstallerTargetKind.Unknown)
        {
            TargetStatusText = string.IsNullOrWhiteSpace(TargetProjectRoot)
                ? GetLocalizedText("String.Installer.TargetWaiting", "等待选择目录")
                : GetLocalizedText("String.Installer.TargetInvalid", "路径无效或不是支持的项目");
        }
        SessionStatusText = mIsGodotRuntimeBootstrapRunning
            ? GetBootstrapStatusText(mIsGodotRuntimeBootstrapOpeningInstaller)
            : GetSessionStatusText(state.Status);
        ApplyPlanSummary(state.Plan);
        ApplyCompletionSummary(state);
        ApplyOutcomeDetails(state);
    }

    /// <summary>
    /// 从当前语言表读取 Installer 展示文本，并在资源尚未加载时使用中文兜底。
    /// </summary>
    /// <param name="key">资源键。</param>
    /// <param name="fallback">中文兜底文本。</param>
    /// <returns>当前语言文本。</returns>
    private static string GetLocalizedText(string key, string fallback)
    {
        return WorkbenchI18nService.Instance.GetString(key, fallback);
    }

    /// <summary>
    /// 格式化当前语言的 Installer 文案；动态参数只包含数量、路径或已确认的业务值。
    /// </summary>
    /// <param name="key">带复合格式占位符的资源键。</param>
    /// <param name="fallback">中文格式兜底文本。</param>
    /// <param name="arguments">格式化参数。</param>
    /// <returns>格式化后的当前语言文本。</returns>
    private static string FormatLocalizedText(string key, string fallback, params object[] arguments)
    {
        return string.Format(
            CultureInfo.CurrentCulture,
            GetLocalizedText(key, fallback),
            arguments);
    }

    /// <summary>
    /// 接收 Application 会话快照，并在需要时切回创建 ViewModel 的 UI 上下文。
    /// </summary>
    /// <param name="sender">Installer 会话。</param>
    /// <param name="eventArgs">变化后的不可变状态。</param>
    private void OnSessionStateChanged(
        object? sender,
        InstallerSessionStateChangedEventArgs eventArgs)
    {
        if (mIsDisposed)
        {
            return;
        }

        if (mSynchronizationContext != null
            && !ReferenceEquals(SynchronizationContext.Current, mSynchronizationContext))
        {
            mSynchronizationContext.Post(
                static state =>
                {
                    var payload = (SessionStateDispatch)state!;
                    payload.ViewModel.ApplySessionState(payload.State);
                },
                new SessionStateDispatch(this, eventArgs.State));
            return;
        }

        ApplySessionState(eventArgs.State);
    }

    /// <summary>
    /// 把会话状态投影为页面状态、进度、冲突入口和增量日志。
    /// </summary>
    /// <param name="state">Application 会话快照。</param>
    private void ApplySessionState(InstallerSessionState state)
    {
        if (state.Plan != null)
        {
            mTargetKind = state.Plan.Engine;
            EngineStatusText = GetEngineText(state.Plan.Engine);
            TargetStatusText = state.Plan.PackageTarget;
        }

        SessionStatusText = GetSessionStatusText(state.Status);
        ApplyPlanSummary(state.Plan);
        ApplyProgress(state);
        ApplyCompletionSummary(state);
        ApplyOutcomeDetails(state);
        IsTakeoverConfirmationVisible = state.Status == InstallerSessionStatus.Conflict
            && IsLegacyConflict(state.ErrorMessage);
        AppendSessionLogs(state.Logs);
        NotifyTargetPresentationChanged();
        RaiseCommandStates();
    }

    /// <summary>
    /// 把统一计划动作和非阻断警告投影为右侧摘要，避免用户必须打开日志才能判断覆盖范围。
    /// </summary>
    /// <param name="plan">当前统一计划；输入尚未稳定时为空。</param>
    private void ApplyPlanSummary(InstallerPlanPreview? plan)
    {
        if (plan == null)
        {
            PlanActionsText = GetLocalizedText(
                "String.Installer.Plan.Waiting",
                "等待生成安装计划");
            PlanWarningsText = string.Empty;
            IsPlanWarningVisible = false;
            return;
        }

        PlanActionsText = plan.Actions.Count == 0
            ? GetLocalizedText(
                "String.Installer.Plan.NoChanges",
                "当前配置无需写入变更")
            : string.Join(Environment.NewLine, plan.Actions.Select(CreatePlanActionText));
        PlanWarningsText = string.Join(Environment.NewLine, plan.Warnings);
        IsPlanWarningVisible = plan.Warnings.Count > 0;
    }

    /// <summary>
    /// 将统一动作转换为面向用户的简短中文说明，目标路径由独立摘要字段展示。
    /// </summary>
    /// <param name="action">Application 统一计划动作。</param>
    /// <returns>带列表前缀的动作说明。</returns>
    private static string CreatePlanActionText(InstallerPlanActionPreview action)
    {
        var text = action.Kind switch
        {
            InstallerPlanActionKind.InstallPackage => GetLocalizedText(
                "String.Installer.Plan.InstallPackage",
                "完整安装或替换本地包"),
            InstallerPlanActionKind.RemovePackage => GetLocalizedText(
                "String.Installer.Plan.RemovePackage",
                "移除现有 embedded 包"),
            InstallerPlanActionKind.SetEmbeddedDependency => GetLocalizedText(
                "String.Installer.Plan.SetEmbeddedDependency",
                "登记 Unity 本地包依赖"),
            InstallerPlanActionKind.SetGitDependency => GetLocalizedText(
                "String.Installer.Plan.SetGitDependency",
                "更新 Unity Git 依赖"),
            InstallerPlanActionKind.PatchProjectFile => GetLocalizedText(
                "String.Installer.Plan.PatchProjectFile",
                "更新 Godot C# 项目引用"),
            InstallerPlanActionKind.PatchProjectSettings => GetLocalizedText(
                "String.Installer.Plan.PatchProjectSettings",
                "更新 Godot 项目设置"),
            _ => throw new ArgumentOutOfRangeException(nameof(action), action.Kind, "Unsupported installer plan action.")
        };
        return GetLocalizedText("String.Installer.Plan.ItemPrefix", "- ") + text;
    }

    /// <summary>
    /// 把 Application 进度换算为 0-100 百分比，并在成功后保持完成态可见。
    /// </summary>
    /// <param name="state">Application 会话快照。</param>
    private void ApplyProgress(InstallerSessionState state)
    {
        if (state.Status is InstallerSessionStatus.Succeeded
            or InstallerSessionStatus.CommittedNeedsVerification)
        {
            ProgressValue = 100;
            IsProgressVisible = true;
            return;
        }

        if (state.Progress == null)
        {
            ProgressValue = 0;
            IsProgressVisible = false;
            return;
        }

        ProgressValue = state.Progress.Completed * 100d / state.Progress.Total;
        IsProgressVisible = state.Status is InstallerSessionStatus.Applying
            or InstallerSessionStatus.Verifying
            or InstallerSessionStatus.RollingBack;
    }

    /// <summary>
    /// 在成功终态投影一次完整安装摘要；新计划或失败状态会清除旧结果，避免展示过期目标。
    /// </summary>
    /// <param name="state">最新 Installer 会话状态。</param>
    private void ApplyCompletionSummary(InstallerSessionState state)
    {
        if (state.Status is not (InstallerSessionStatus.Succeeded or InstallerSessionStatus.CommittedNeedsVerification)
            || state.Plan == null
            || state.Result == null)
        {
            ClearCompletionSummary();
            return;
        }

        if (ReferenceEquals(mPresentedResult, state.Result))
        {
            CompletionSummaryText = CreateCompletionSummary(state.Plan, state.Result, state.Options);
            return;
        }

        mPresentedResult = state.Result;
        CompletionSummaryText = CreateCompletionSummary(state.Plan, state.Result, state.Options);
        IsCompletionSummaryVisible = true;
        AppendLocalLog(state.Status == InstallerSessionStatus.Succeeded
            ? FormatLocalizedText(
                "String.Installer.Log.Completed",
                "安装完成: {0} / {1}",
                GetEngineText(state.Plan.Engine),
                GetModeText(state.Plan.Mode))
            : FormatLocalizedText(
                "String.Installer.Log.CommittedNeedsVerification",
                "安装已提交，等待验证: {0} / {1}",
                GetEngineText(state.Plan.Engine),
                GetModeText(state.Plan.Mode)));
    }

    /// <summary>
    /// 清除已失效的成功摘要，使下一次计划或错误状态不会复用旧事务结果。
    /// </summary>
    private void ClearCompletionSummary()
    {
        mPresentedResult = null;
        CompletionSummaryText = string.Empty;
        IsCompletionSummaryVisible = false;
    }

    /// <summary>
    /// 根据统一计划和执行结果创建可审阅的完成摘要，不伪造 Core 未提供的复制或配置数量。
    /// </summary>
    /// <param name="plan">已执行的安装计划。</param>
    /// <param name="result">Core 提交后的统一结果。</param>
    /// <param name="options">执行时使用的安装输入。</param>
    /// <returns>供页面显示的多行摘要。</returns>
    private static string CreateCompletionSummary(
        InstallerPlanPreview plan,
        InstallerExecutionResult result,
        InstallerInstallOptions? options)
    {
        var changeText = result.Changed
            ? GetLocalizedText("String.Installer.Summary.Changed", "已提交变更")
            : GetLocalizedText("String.Installer.Summary.NoChanges", "无需写入变更");
        if (result.CommittedNeedsVerification)
        {
            changeText += GetLocalizedText(
                "String.Installer.Summary.NeedsVerificationSuffix",
                "，但宿主 post-verify 尚未完成");
        }
        if (result.ReplacedExistingPackage)
        {
            changeText += GetLocalizedText(
                "String.Installer.Summary.ReplacedSuffix",
                "，已替换既有安装来源");
        }

        List<string> lines = new()
        {
            FormatLocalizedText("String.Installer.Summary.Engine", "引擎: {0}", GetEngineText(plan.Engine)),
            FormatLocalizedText("String.Installer.Summary.Mode", "模式: {0}", GetModeText(plan.Mode)),
            FormatLocalizedText("String.Installer.Summary.Platform", "平台: {0}", GetCurrentPlatformText()),
            FormatLocalizedText("String.Installer.Summary.Target", "目标: {0}", result.TargetPath),
            FormatLocalizedText("String.Installer.Summary.Result", "结果: {0}", changeText),
            FormatLocalizedText("String.Installer.Summary.ActionCount", "计划动作: {0} 项", plan.Actions.Count),
            FormatLocalizedText("String.Installer.Summary.EvidenceCount", "校验证据: {0} 项", result.EvidencePaths.Count)
        };
        if (plan.Engine == InstallerTargetKind.Godot)
        {
            var pluginEnabled = options?.GodotOptions?.EnablePlugin == true;
            lines.Add(pluginEnabled
                ? GetLocalizedText(
                    "String.Installer.Summary.GodotPluginEnabled",
                    "Godot: 请刷新文件系统并确认插件已启用；之后可从 Project > Tools > YokiFrame > Open Workbench 或按 Ctrl+E 打开工作台，系统热键冲突时按 Ctrl+Alt+E。")
                : GetLocalizedText(
                    "String.Installer.Summary.GodotPluginDisabled",
                    "Godot: 请刷新文件系统；当前计划未自动启用 YokiFrame 插件。"));
        }

        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>
    /// 把冲突路径、事务回滚结果和诊断证据投影为页面可直接审阅的详情。
    /// </summary>
    /// <param name="state">最新 Installer 会话状态。</param>
    private void ApplyOutcomeDetails(InstallerSessionState state)
    {
        if (mIsGodotRuntimeBootstrapRunning)
        {
            ClearOutcomeDetails();
            return;
        }

        if (state.Status == InstallerSessionStatus.Conflict)
        {
            OutcomeDetailsTitle = GetLocalizedText("String.Installer.Outcome.ConflictTitle", "安装冲突");
            OutcomeDetailsText = CreateConflictDetails(state);
            IsOutcomeDetailsVisible = true;
            return;
        }

        if (state.Status == InstallerSessionStatus.Failed)
        {
            OutcomeDetailsTitle = GetLocalizedText("String.Installer.Outcome.FailedTitle", "安装失败");
            OutcomeDetailsText = CreateFailureDetails(state);
            IsOutcomeDetailsVisible = true;
            return;
        }

        if (state.Status == InstallerSessionStatus.CommittedNeedsVerification)
        {
            OutcomeDetailsTitle = GetLocalizedText(
                "String.Installer.Outcome.CommittedNeedsVerificationTitle",
                "已提交但待验证");
            OutcomeDetailsText = string.IsNullOrWhiteSpace(state.ErrorMessage)
                ? GetLocalizedText(
                    "String.Installer.Outcome.CommittedNeedsVerificationText",
                    "Core 已完成写入，但宿主构建或插件登记尚未完成。请修复构建问题后重新验证。")
                : state.ErrorMessage;
            IsOutcomeDetailsVisible = true;
            return;
        }

        ClearOutcomeDetails();
    }

    /// <summary>
    /// 清除当前事务的冲突或失败详情，避免把已经解决的前置错误继续显示给用户。
    /// </summary>
    private void ClearOutcomeDetails()
    {
        OutcomeDetailsTitle = string.Empty;
        OutcomeDetailsText = string.Empty;
        IsOutcomeDetailsVisible = false;
    }

    /// <summary>
    /// 创建包含错误说明和全部稳定冲突路径的文本。
    /// </summary>
    /// <param name="state">冲突状态。</param>
    /// <returns>多行冲突详情。</returns>
    private static string CreateConflictDetails(InstallerSessionState state)
    {
        List<string> lines = new() { state.ErrorMessage };
        if (state.ConflictPaths.Count > 0)
        {
            lines.Add(GetLocalizedText("String.Installer.Outcome.ConflictPaths", "冲突路径:"));
            lines.AddRange(state.ConflictPaths.Select(static path => "- " + path));
        }

        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>
    /// 创建包含失败说明、回滚结论和持久化诊断证据的文本。
    /// </summary>
    /// <param name="state">失败状态。</param>
    /// <returns>多行失败详情。</returns>
    private static string CreateFailureDetails(InstallerSessionState state)
    {
        List<string> lines = new() { state.ErrorMessage };
        if (state.RollbackSucceeded.HasValue)
        {
            lines.Add(state.RollbackSucceeded.Value
                ? GetLocalizedText("String.Installer.Outcome.RollbackSucceeded", "回滚成功，已恢复安装前状态。")
                : GetLocalizedText("String.Installer.Outcome.RollbackIncomplete", "回滚未完整完成，需要人工检查目标项目。"));
        }

        if (state.EvidencePaths.Count > 0)
        {
            lines.Add(GetLocalizedText("String.Installer.Outcome.EvidencePaths", "诊断证据:"));
            lines.AddRange(state.EvidencePaths.Select(static path => "- " + path));
        }

        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>
    /// 首次看到一个新计划时把来源、目标和动作列表追加到日志，形成可审阅预览。
    /// </summary>
    /// <param name="plan">统一安装计划预览。</param>
    private void PresentPlanOnce(InstallerPlanPreview plan)
    {
        if (ReferenceEquals(mPresentedPlan, plan))
        {
            return;
        }

        mPresentedPlan = plan;
        AppendLocalLog(FormatLocalizedText(
            "String.Installer.Log.Plan",
            "计划: {0} / {1}",
            GetEngineText(plan.Engine),
            GetModeText(plan.Mode)));
        AppendLocalLog(FormatLocalizedText(
            "String.Installer.Log.Target",
            "安装目标: {0}",
            plan.PackageTarget));
        foreach (var action in plan.Actions)
        {
            AppendLocalLog(action.Kind + ": " + action.TargetPath);
        }

        foreach (var warning in plan.Warnings)
        {
            AppendLocalLog(FormatLocalizedText("String.Installer.Log.Warning", "警告: {0}", warning));
        }
    }

    /// <summary>
    /// 仅追加 Application 新产生的日志，避免状态刷新重复填充旧行。
    /// </summary>
    /// <param name="logs">当前会话日志快照。</param>
    private void AppendSessionLogs(IReadOnlyList<InstallerLogEntry> logs)
    {
        if (logs.Count < mProjectedLogCount)
        {
            mProjectedLogCount = 0;
        }

        for (var index = mProjectedLogCount; index < logs.Count; index++)
        {
            var entry = logs[index];
            var timestamp = entry.TimestampUtc.ToLocalTime().ToString("HH:mm:ss", CultureInfo.InvariantCulture);
            LogEntries.Add(new InstallerLogLine("[" + timestamp + "]", FormatLogMessage(entry)));
        }

        mProjectedLogCount = logs.Count;
    }

    /// <summary>
    /// 追加一条仅属于当前 UI 会话的即时日志。
    /// </summary>
    /// <param name="message">日志消息。</param>
    private void AppendLocalLog(string message)
    {
        var timestamp = DateTimeOffset.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
        LogEntries.Add(new InstallerLogLine("[" + timestamp + "]", message));
    }

    /// <summary>
    /// 清空当前可见日志，并跳过 Application 已产生的旧日志快照。
    /// </summary>
    private void ClearLog()
    {
        LogEntries.Clear();
        mProjectedLogCount = mSession.State.Logs.Count;
    }

    /// <summary>
    /// 显示未进入 Application 状态机的 UI 或平台错误。
    /// </summary>
    /// <param name="message">错误消息。</param>
    internal void ShowLocalError(string message)
    {
        SessionStatusText = GetLocalizedText("String.Installer.Session.OperationFailed", "操作失败");
        AppendLocalLog(FormatLocalizedText("String.Installer.Log.Error", "错误: {0}", message));
        RaiseCommandStates();
    }

    /// <summary>
    /// 刷新由目标类型和安装模式派生的显隐属性。
    /// </summary>
    private void NotifyTargetPresentationChanged()
    {
        OnPropertyChanged(nameof(IsEngineOptionsVisible));
        OnPropertyChanged(nameof(IsUnityOptionsVisible));
        OnPropertyChanged(nameof(IsGodotOptionsVisible));
        OnPropertyChanged(nameof(IsGodotRuntimeBootstrapVisible));
        OnPropertyChanged(nameof(IsGitUrlVisible));
        OnPropertyChanged(nameof(IsCurrentPlatformVisible));
        OnPropertyChanged(nameof(IsSourcePathVisible));
    }

    /// <summary>
    /// 通知所有工作流命令和重试可见性重新计算。
    /// </summary>
    private void RaiseCommandStates()
    {
        PreviewCommand.RaiseCanExecuteChanged();
        InstallCommand.RaiseCanExecuteChanged();
        RetryCommand.RaiseCanExecuteChanged();
        BootstrapGodotRuntimeCommand.RaiseCanExecuteChanged();
        OnPropertyChanged(nameof(CanRetry));
    }

    /// <summary>
    /// 根据当前运行平台返回旧版 Installer 使用的简洁标签。
    /// </summary>
    /// <returns>Windows、Linux、macOS 或当前系统。</returns>
    private static string GetCurrentPlatformText()
    {
        if (OperatingSystem.IsWindows())
        {
            return GetLocalizedText("String.Installer.Platform.Windows", "Windows");
        }

        if (OperatingSystem.IsLinux())
        {
            return GetLocalizedText("String.Installer.Platform.Linux", "Linux");
        }

        return OperatingSystem.IsMacOS()
            ? GetLocalizedText("String.Installer.Platform.MacOS", "macOS")
            : GetLocalizedText("String.Installer.Platform.CurrentSystem", "当前系统");
    }

    /// <summary>
    /// 把目标引擎枚举转换为当前语言的显示标签；未知值仍保留协议枚举文本用于诊断。
    /// </summary>
    /// <param name="targetKind">Application 目标引擎枚举。</param>
    /// <returns>用户可读引擎标签。</returns>
    private static string GetEngineText(InstallerTargetKind targetKind)
    {
        return targetKind switch
        {
            InstallerTargetKind.Unity => GetLocalizedText("String.Installer.Engine.Unity", "Unity"),
            InstallerTargetKind.Godot => GetLocalizedText("String.Installer.Engine.Godot", "Godot"),
            _ => GetLocalizedText("String.Installer.EngineNotDetected", "未检测")
        };
    }

    /// <summary>
    /// 返回 Godot Runtime 构建阶段的当前语言状态提示。
    /// </summary>
    /// <param name="openInstaller">成功后是否会打开新的 Installer。</param>
    /// <returns>构建阶段状态文本。</returns>
    private static string GetBootstrapStatusText(bool openInstaller)
    {
        return openInstaller
            ? GetLocalizedText(
                "String.Installer.Session.BuildingRuntime",
                "正在为 Godot 构建当前平台 Runtime")
            : GetLocalizedText(
                "String.Installer.Session.AutoBuildingRuntime",
                "正在为 Godot 自动构建当前平台 Runtime");
    }

    /// <summary>
    /// 把 Application 会话枚举转换为稳定的简体中文状态。
    /// </summary>
    /// <param name="status">会话状态。</param>
    /// <returns>用户可读状态。</returns>
    private static string GetSessionStatusText(InstallerSessionStatus status)
    {
        return status switch
        {
            InstallerSessionStatus.Idle => GetLocalizedText("String.Installer.Session.Ready", "安装器已就绪"),
            InstallerSessionStatus.Detecting => GetLocalizedText("String.Installer.Session.Detecting", "正在检测"),
            InstallerSessionStatus.PlanReady => GetLocalizedText("String.Installer.Session.PlanReady", "计划已就绪"),
            InstallerSessionStatus.Applying => GetLocalizedText("String.Installer.Session.Applying", "正在安装"),
            InstallerSessionStatus.Verifying => GetLocalizedText("String.Installer.Session.Verifying", "正在校验"),
            InstallerSessionStatus.RollingBack => GetLocalizedText("String.Installer.Session.RollingBack", "正在回滚"),
            InstallerSessionStatus.Succeeded => GetLocalizedText("String.Installer.Session.Succeeded", "安装完成"),
            InstallerSessionStatus.CommittedNeedsVerification => GetLocalizedText(
                "String.Installer.Session.CommittedNeedsVerification",
                "已提交，待验证"),
            InstallerSessionStatus.Conflict => GetLocalizedText("String.Installer.Session.Conflict", "检测到冲突"),
            InstallerSessionStatus.Failed => GetLocalizedText("String.Installer.Session.Failed", "安装失败"),
            InstallerSessionStatus.Cancelled => GetLocalizedText("String.Installer.Session.Cancelled", "已取消"),
            _ => status.ToString()
        };
    }

    /// <summary>
    /// 把安装模式转换为日志中使用的简洁标签。
    /// </summary>
    /// <param name="mode">安装模式。</param>
    /// <returns>模式标签。</returns>
    private static string GetModeText(InstallerInstallMode mode)
    {
        return mode switch
        {
            InstallerInstallMode.UnityLocal => GetLocalizedText("String.Installer.Mode.UnityLocal", "Unity 本地包"),
            InstallerInstallMode.UnityGit => GetLocalizedText("String.Installer.Mode.UnityGit", "Unity Git 包"),
            InstallerInstallMode.GodotLocal => GetLocalizedText("String.Installer.Mode.GodotLocal", "Godot 本地包"),
            _ => mode.ToString()
        };
    }

    /// <summary>
    /// 为不同日志等级补充文本前缀，避免仅依赖颜色表达严重度。
    /// </summary>
    /// <param name="entry">Application 日志。</param>
    /// <returns>带必要严重度前缀的消息。</returns>
    private static string FormatLogMessage(InstallerLogEntry entry)
    {
        return entry.Level switch
        {
            InstallerLogLevel.Warning => FormatLocalizedText("String.Installer.Log.Warning", "警告: {0}", entry.Message),
            InstallerLogLevel.Error => FormatLocalizedText("String.Installer.Log.Error", "错误: {0}", entry.Message),
            _ => entry.Message
        };
    }

    /// <summary>
    /// 判断冲突是否来自尚未确认接管的旧版安装。
    /// </summary>
    /// <param name="message">Application 冲突说明。</param>
    /// <returns>包含 legacy 或 unmanaged 语义时返回 true。</returns>
    private static bool IsLegacyConflict(string message)
    {
        return message.Contains("legacy", StringComparison.OrdinalIgnoreCase)
            || message.Contains("unmanaged", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 携带切换 UI 上下文所需的 ViewModel 与状态快照。
    /// </summary>
    /// <param name="ViewModel">目标 ViewModel。</param>
    /// <param name="State">待应用状态。</param>
    private sealed record SessionStateDispatch(
        InstallerShellViewModel ViewModel,
        InstallerSessionState State);
}
