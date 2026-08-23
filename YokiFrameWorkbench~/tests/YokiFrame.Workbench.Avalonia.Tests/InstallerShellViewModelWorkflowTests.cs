using YokiFrame.Installer.Core.Services;
using YokiFrame.Tooling.Application.Installer;
using YokiFrame.Workbench.Avalonia.Services;
using YokiFrame.Workbench.Avalonia.ViewModels;

namespace YokiFrame.Workbench.Avalonia.Tests;

/// <summary>
/// 验证 Installer ViewModel 只消费 Application 会话，并完整投影旧版交互流程。
/// </summary>
public sealed partial class InstallerShellViewModelWorkflowTests
{
    private const string DefaultGitUrl = "https://github.com/HinataYoki/YokiFrame.git";

    /// <summary>
    /// 验证 Installer 的状态占位和计划等待文案随语言切换重投影，释放后不再响应静态语言事件。
    /// </summary>
    [Fact]
    public async Task InstallerViewModel_LocalizesPresentationAndDetachesOnDispose()
    {
        InstallerHeadlessTestApplication.EnsureInitialized();
        await global::Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            var service = WorkbenchI18nService.Instance;
            service.SetCulture("zh-CN");
            using InstallerViewModelFixture fixture = InstallerViewModelFixture.CreateUnity();
            var notificationCount = 0;
            fixture.ViewModel.PropertyChanged += (_, _) => notificationCount++;
            try
            {
                service.SetCulture("en-US");

                Assert.Equal("Installer ready", fixture.ViewModel.SessionStatusText);
                Assert.Equal("Waiting for an install plan", fixture.ViewModel.PlanActionsText);

                notificationCount = 0;
                fixture.ViewModel.Dispose();
                service.SetCulture("zh-CN");

                Assert.Equal(0, notificationCount);
            }
            finally
            {
                fixture.ViewModel.Dispose();
                service.SetCulture("zh-CN");
            }
        });
    }

    /// <summary>
    /// 验证目标路径指向 Godot 4.7 .NET 时自动显示 Godot 选项并生成计划。
    /// </summary>
    [Fact]
    public async Task InitializeAsyncDetectsGodotAndShowsGodotOptions()
    {
        using InstallerViewModelFixture fixture = InstallerViewModelFixture.CreateGodot();

        await fixture.ViewModel.InitializeAsync();

        Assert.True(fixture.ViewModel.IsGodotOptionsVisible);
        Assert.False(fixture.ViewModel.IsUnityOptionsVisible);
        Assert.Equal("Godot", fixture.ViewModel.EngineStatusText);
        Assert.Equal(fixture.PackageTarget, fixture.ViewModel.TargetStatusText);
        Assert.Equal(InstallerInstallMode.GodotLocal, fixture.Gateway.LastOptions?.Mode);
    }

    /// <summary>
    /// 验证 Unity Git 模式隐藏源目录、显示默认 URL，并向 Application 提交 Git 选项。
    /// </summary>
    [Fact]
    public async Task UnityGitModeUsesGitUrlWithoutLocalSource()
    {
        using InstallerViewModelFixture fixture = InstallerViewModelFixture.CreateUnity();
        await fixture.ViewModel.InitializeAsync();

        fixture.ViewModel.IsUnityGitSelected = true;
        await fixture.ViewModel.RefreshPlanAsync();

        Assert.False(fixture.ViewModel.IsSourcePathVisible);
        Assert.True(fixture.ViewModel.IsGitUrlVisible);
        Assert.Equal(DefaultGitUrl, fixture.ViewModel.GitUrl);
        Assert.Equal(InstallerInstallMode.UnityGit, fixture.Gateway.LastOptions?.Mode);
        Assert.Null(fixture.Gateway.LastOptions?.SourcePackageRoot);
    }

    /// <summary>
    /// 验证原生目录选择结果回填文本框，并触发最新目标的自动检测。
    /// </summary>
    [Fact]
    public async Task FolderPickerResultsUpdateEditablePaths()
    {
        using InstallerViewModelFixture fixture = InstallerViewModelFixture.CreateUnity();
        fixture.FolderPicker.SourceResult = fixture.SourceRoot + "-next";
        fixture.FolderPicker.TargetResult = fixture.TargetRoot;

        await fixture.ViewModel.PickSourceAsync();
        await fixture.ViewModel.PickTargetAsync();

        Assert.Equal(fixture.SourceRoot + "-next", fixture.ViewModel.SourcePackageRoot);
        Assert.Equal(fixture.TargetRoot, fixture.ViewModel.TargetProjectRoot);
        Assert.Equal(2, fixture.FolderPicker.RequestCount);
    }

    /// <summary>
    /// 验证安装进度和成功状态进入日志，清空命令只清理当前显示内容。
    /// </summary>
    [Fact]
    public async Task InstallAsyncProjectsProgressAndSupportsClearingLog()
    {
        using InstallerViewModelFixture fixture = InstallerViewModelFixture.CreateUnity();
        await fixture.ViewModel.InitializeAsync();

        await fixture.ViewModel.InstallCommand.ExecuteAsync();

        Assert.Equal("安装完成", fixture.ViewModel.SessionStatusText);
        Assert.Equal(100, fixture.ViewModel.ProgressValue);
        Assert.NotEmpty(fixture.ViewModel.LogEntries);

        fixture.ViewModel.ClearLogCommand.Execute(null);

        Assert.Empty(fixture.ViewModel.LogEntries);
    }

    /// <summary>
    /// 验证成功后以引擎、模式、平台、目标和校验证据生成可见摘要，避免用户只能从滚动日志判断结果。
    /// </summary>
    [Fact]
    public async Task InstallAsyncShowsCompletionSummary()
    {
        using InstallerViewModelFixture fixture = InstallerViewModelFixture.CreateUnity();
        await fixture.ViewModel.InitializeAsync();

        var visibleProperty = typeof(InstallerShellViewModel).GetProperty("IsCompletionSummaryVisible");
        var summaryProperty = typeof(InstallerShellViewModel).GetProperty("CompletionSummaryText");
        Assert.NotNull(visibleProperty);
        Assert.NotNull(summaryProperty);

        await fixture.ViewModel.InstallCommand.ExecuteAsync();

        Assert.True((bool)visibleProperty!.GetValue(fixture.ViewModel)!);
        var summary = Assert.IsType<string>(summaryProperty!.GetValue(fixture.ViewModel));
        Assert.Contains("Unity", summary);
        Assert.Contains("Unity 本地包", summary);
        Assert.Contains(fixture.PackageTarget, summary);
        Assert.Contains("校验证据", summary);
    }

    /// <summary>
    /// 验证自动检测不会反复展开完整动作日志，只有用户显式点击预览时才展示计划详情。
    /// </summary>
    [Fact]
    public async Task ExplicitPreviewExpandsPlanDetailsAfterSilentDetection()
    {
        using InstallerViewModelFixture fixture = InstallerViewModelFixture.CreateUnity();

        await fixture.ViewModel.InitializeAsync();

        Assert.DoesNotContain(
            fixture.ViewModel.LogEntries,
            static entry => entry.Message.StartsWith("计划:", StringComparison.Ordinal));

        await fixture.ViewModel.PreviewCommand.ExecuteAsync();

        Assert.Contains(
            fixture.ViewModel.LogEntries,
            static entry => entry.Message.StartsWith("计划:", StringComparison.Ordinal));
        Assert.Contains(
            fixture.ViewModel.LogEntries,
            static entry => entry.Message.StartsWith("InstallPackage:", StringComparison.Ordinal));
    }

    /// <summary>
    /// 验证 unmanaged legacy 默认冲突，用户明确确认后重试才切换接管策略。
    /// </summary>
    [Fact]
    public async Task RetryUsesConfirmedLegacyTakeoverPolicy()
    {
        using InstallerViewModelFixture fixture = InstallerViewModelFixture.CreateGodot();
        fixture.Gateway.RejectUnconfirmedLegacy = true;

        await fixture.ViewModel.InitializeAsync();

        Assert.True(fixture.ViewModel.IsTakeoverConfirmationVisible);
        Assert.True(fixture.ViewModel.CanRetry);
        Assert.True(fixture.ViewModel.IsOutcomeDetailsVisible);
        Assert.Contains("legacy.cs", fixture.ViewModel.OutcomeDetailsText, StringComparison.Ordinal);

        fixture.ViewModel.ConfirmLegacyTakeover = true;
        await fixture.ViewModel.RetryCommand.ExecuteAsync();

        Assert.Equal(InstallerLegacyPackagePolicy.TakeOverConfirmed, fixture.Gateway.LastOptions?.LegacyPackagePolicy);
        Assert.Equal("计划已就绪", fixture.ViewModel.SessionStatusText);
        Assert.False(fixture.ViewModel.IsOutcomeDetailsVisible);
    }

    /// <summary>
    /// 验证事务失败时页面直接显示回滚结论和诊断证据，而不是只留下通用失败状态。
    /// </summary>
    [Fact]
    public async Task InstallFailureShowsRollbackAndEvidenceDetails()
    {
        using InstallerViewModelFixture fixture = InstallerViewModelFixture.CreateUnity();
        fixture.Gateway.ExecutionFailure = new InstallerExecutionException(
            "写入失败。",
            rollbackSucceeded: true,
            new[] { "diagnostics/installer-failure.json" },
            new IOException("simulated"));
        await fixture.ViewModel.InitializeAsync();

        await fixture.ViewModel.InstallCommand.ExecuteAsync();

        Assert.Equal("安装失败", fixture.ViewModel.SessionStatusText);
        Assert.True(fixture.ViewModel.IsOutcomeDetailsVisible);
        Assert.Contains("回滚成功", fixture.ViewModel.OutcomeDetailsText, StringComparison.Ordinal);
        Assert.Contains("diagnostics/installer-failure.json", fixture.ViewModel.OutcomeDetailsText, StringComparison.Ordinal);
    }

    /// <summary>
    /// 验证 Godot 选项变更后，旧计划会在防抖窗口内立即失去执行资格，直到最新输入重新生成计划。
    /// </summary>
    [Fact]
    public async Task GodotInputChangeDisablesOldPlanBeforeDebouncedRefresh()
    {
        ControlledInstallerDetectionDelay delay = new();
        using InstallerViewModelFixture fixture = InstallerViewModelFixture.CreateGodot(delay);

        var initialization = fixture.ViewModel.InitializeAsync();
        await delay.WaitForNextAsync();
        delay.ReleaseLatest();
        await initialization;
        Assert.True(fixture.ViewModel.InstallCommand.CanExecute(null));

        fixture.ViewModel.EnableGodotPlugin = false;
        await delay.WaitForNextAsync();

        Assert.False(fixture.ViewModel.InstallCommand.CanExecute(null));
        await fixture.ViewModel.InstallCommand.ExecuteAsync();
        Assert.Equal(0, fixture.Gateway.ExecuteCount);

        var latestRefresh = fixture.ViewModel.RefreshPlanAsync();
        await delay.WaitForNextAsync();
        delay.ReleaseLatest();
        await latestRefresh;

        Assert.True(fixture.ViewModel.InstallCommand.CanExecute(null));
        Assert.False(fixture.Gateway.LastOptions!.GodotOptions!.EnablePlugin);
    }

    /// <summary>
    /// 验证 Godot Runtime 缓存失配时，Installer 会自动执行源码 bootstrap 并在同一窗口重新生成计划。
    /// </summary>
    [Fact]
    public async Task GodotRuntimeCacheFailureBootstrapsAndReplansInSameInstaller()
    {
        using InstallerViewModelFixture fixture = InstallerViewModelFixture.CreateGodot();
        fixture.Gateway.PlanningFailure = RuntimeCacheBootstrapRequirement.Create(
            "Godot 安装需要先构建项目 Runtime 缓存。");
        fixture.Gateway.PlanningFailuresRemaining = 1;

        await fixture.ViewModel.InitializeAsync();

        Assert.Equal(fixture.SourceRoot, fixture.GodotRuntimeBootstrapper.SourcePackageRoot);
        Assert.Equal(fixture.TargetRoot, fixture.GodotRuntimeBootstrapper.TargetProjectRoot);
        Assert.Equal(1, fixture.GodotRuntimeBootstrapper.BootstrapCount);
        Assert.False(fixture.ViewModel.IsGodotRuntimeBootstrapVisible);
        Assert.True(fixture.ViewModel.InstallCommand.CanExecute(null));
        Assert.Equal("计划已就绪", fixture.ViewModel.SessionStatusText);
    }

    /// <summary>
    /// 验证自动构建 Godot Runtime 期间不把前置缓存失败显示成红色安装失败。
    /// </summary>
    [Fact]
    public async Task GodotRuntimeBootstrapHidesTransientFailureDetailsWhileRunning()
    {
        using InstallerViewModelFixture fixture = InstallerViewModelFixture.CreateGodot();
        fixture.Gateway.PlanningFailure = RuntimeCacheBootstrapRequirement.Create(
            "Godot 安装需要先构建项目 Runtime 缓存。");
        fixture.Gateway.PlanningFailuresRemaining = 1;
        fixture.GodotRuntimeBootstrapper.BootstrapGate = new(TaskCreationOptions.RunContinuationsAsynchronously);

        var initialization = fixture.ViewModel.InitializeAsync();
        await fixture.GodotRuntimeBootstrapper.BootstrapStarted.Task;

        Assert.False(fixture.ViewModel.IsOutcomeDetailsVisible);
        Assert.Equal("正在为 Godot 自动构建当前平台 Runtime", fixture.ViewModel.SessionStatusText);

        fixture.GodotRuntimeBootstrapper.BootstrapGate.SetResult();
        await initialization;
        Assert.Equal("计划已就绪", fixture.ViewModel.SessionStatusText);
    }

    /// <summary>
    /// 验证自动 Runtime bootstrap 失败时不伪造计划，并保留手动恢复按钮。
    /// </summary>
    [Fact]
    public async Task GodotRuntimeBootstrapFailureKeepsManualRecoveryVisible()
    {
        using InstallerViewModelFixture fixture = InstallerViewModelFixture.CreateGodot();
        fixture.Gateway.PlanningFailure = RuntimeCacheBootstrapRequirement.Create(
            "Godot 安装需要先构建项目 Runtime 缓存。");
        fixture.Gateway.PlanningFailuresRemaining = 1;
        fixture.GodotRuntimeBootstrapper.BootstrapFailure = new InvalidOperationException("simulated bootstrap failure");

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.ViewModel.InitializeAsync());

        Assert.True(fixture.ViewModel.IsGodotRuntimeBootstrapVisible);
        Assert.False(fixture.ViewModel.InstallCommand.CanExecute(null));
        Assert.True(fixture.ViewModel.IsOutcomeDetailsVisible);
        Assert.Contains(
            fixture.ViewModel.LogEntries,
            entry => entry.Message.Contains("正在从选定 YokiFrame 源码包构建", StringComparison.Ordinal));
    }
}
