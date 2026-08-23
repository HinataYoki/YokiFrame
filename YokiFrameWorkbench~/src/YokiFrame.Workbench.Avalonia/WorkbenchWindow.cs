using Avalonia.Controls;
using Avalonia.Threading;
using YokiFrame.Workbench.Avalonia.Diagnostics;
using YokiFrame.Workbench.Avalonia.Platform;
using YokiFrame.Workbench.Avalonia.Services;
using YokiFrame.Workbench.Avalonia.ViewModels;
using YokiFrame.Workbench.Avalonia.Views;
using YokiFrame.Tooling.Application.Models;
using YokiFrame.Tooling.Application.Packages;
using YokiFrame.Tooling.Application.Services;

namespace YokiFrame.Workbench.Avalonia;

/// <summary>
/// Workbench 主窗口，负责承载导航和首批页面。
/// </summary>
public sealed partial class WorkbenchWindow : Window
{
    /// <summary>
    /// 获取 Workbench 默认窗口宽度，匹配 Tauri 工作台宽屏布局。
    /// </summary>
    public const double DefaultWindowWidth = 1700;

    /// <summary>
    /// 获取 Workbench 默认窗口高度，匹配 Tauri 工作台宽屏布局。
    /// </summary>
    public const double DefaultWindowHeight = 1060;

    /// <summary>
    /// 获取 Workbench 是否默认显示在任务栏；引擎唤起时隐藏，避免像独立应用抢占任务栏。
    /// </summary>
    public const bool DefaultShowInTaskbar = false;

    /// <summary>
    /// 获取 Workbench 是否默认允许最小化；引擎子窗口模式下不提供最小化按钮。
    /// </summary>
    public const bool DefaultCanMinimize = false;

    /// <summary>
    /// 获取 Workbench 是否默认允许用户调整原生窗口大小；嵌入引擎后由宿主布局控制尺寸。
    /// </summary>
    public const bool DefaultCanResize = true;

    /// <summary>
    /// 获取 Workbench 默认窗口装饰；只保留边框并由 Shell 自绘标题栏按钮，避免系统标题和按钮冲突。
    /// </summary>
    public const WindowDecorations DefaultWindowDecorations = WindowDecorations.BorderOnly;

    /// <summary>
    /// 获取 Workbench 默认原生窗口标题；沉浸式标题栏下留空，避免系统标题压住左上角品牌区。
    /// </summary>
    public const string DefaultWindowTitle = "";

    /// <summary>
    /// 获取 Workbench 是否默认把内容扩展到系统装饰区，形成沉浸式标题栏。
    /// </summary>
    public const bool DefaultExtendClientAreaToDecorations = true;

    /// <summary>
    /// 获取 Workbench 默认窗口启动位置；没有历史状态时居中显示。
    /// </summary>
    public const WindowStartupLocation DefaultWindowStartupLocation = WindowStartupLocation.CenterScreen;

    private static readonly TimeSpan FileRefreshInterval = TimeSpan.FromSeconds(1);
    private readonly WorkbenchDashboardService mDashboardService;
    private readonly WorkbenchShellViewModel mShellViewModel;
    private readonly DispatcherTimer mRefreshTimer = new();
    private readonly IntPtr mParentWindowHandle;
    private readonly WindowStateStore? mWindowStateStore;
    private readonly WorkbenchActivationCoordinator? mActivationCoordinator;
    private readonly EngineLifecycleMonitor? mLifecycleMonitor;
    private readonly WorkbenchSession mSession = new();
    private readonly TelemetryRefreshPolicy mTelemetryRefreshPolicy = new(FileRefreshInterval);
    private WorkbenchDashboardState? mCurrentState;
    private volatile bool mIsClosed;
    private bool mClosePersistenceInFlight;
    private bool mClosePersistenceCompleted;
    private Task? mWindowShutdownTask;
    private string mSelectedEngineId = string.Empty;

    /// <summary>
    /// 创建 Workbench 主窗口。
    /// </summary>
    /// <param name="dashboardService">Tooling.Application dashboard 服务。</param>
    public WorkbenchWindow(WorkbenchDashboardService dashboardService)
        : this(dashboardService, IntPtr.Zero)
    {
    }

    /// <summary>
    /// 创建 Workbench 主窗口，并按启动选项决定是否尝试挂载到宿主窗口。
    /// </summary>
    /// <param name="dashboardService">Tooling.Application dashboard 服务。</param>
    /// <param name="startupOptions">Workbench 启动选项。</param>
    public WorkbenchWindow(WorkbenchDashboardService dashboardService, ToolStartupOptions startupOptions)
        : this(dashboardService, startupOptions, null)
    {
    }

    /// <summary>
    /// 创建 Workbench 主窗口，并订阅同项目后续进程的激活请求。
    /// </summary>
    /// <param name="dashboardService">Tooling.Application dashboard 服务。</param>
    /// <param name="startupOptions">Workbench 启动选项。</param>
    /// <param name="activationCoordinator">当前项目 owner 的激活协调器；降级启动时为空。</param>
    public WorkbenchWindow(
        WorkbenchDashboardService dashboardService,
        ToolStartupOptions startupOptions,
        WorkbenchActivationCoordinator? activationCoordinator)
        : this(
            dashboardService,
            startupOptions.ParentWindowHandle,
            new WindowStateStore(startupOptions.ProjectRoot),
            activationCoordinator,
            startupOptions.ProjectRoot,
            startupOptions.SourcePackageRoot)
    {
    }

    /// <summary>
    /// 初始化 Workbench 主窗口的页面、刷新计时器和宿主窗口句柄。
    /// </summary>
    /// <param name="dashboardService">Tooling.Application dashboard 服务。</param>
    /// <param name="parentWindowHandle">宿主窗口原生句柄；为 0 时不尝试嵌入。</param>
    private WorkbenchWindow(WorkbenchDashboardService dashboardService, IntPtr parentWindowHandle)
        : this(dashboardService, parentWindowHandle, null, null, string.Empty, string.Empty)
    {
    }

    /// <summary>
    /// 初始化 Workbench 主窗口的页面、刷新计时器、宿主窗口句柄和窗口状态存储。
    /// </summary>
    /// <param name="dashboardService">Tooling.Application dashboard 服务。</param>
    /// <param name="parentWindowHandle">宿主窗口原生句柄；为 0 时不尝试嵌入。</param>
    /// <param name="windowStateStore">窗口状态存储；为空时只使用默认居中策略。</param>
    /// <param name="activationCoordinator">项目级激活协调器；为空时窗口不接收外部激活。</param>
    /// <param name="projectRoot">当前宿主项目根；用于创建 Application 工作流服务。</param>
    /// <param name="sourcePackageRoot">启动入口解析出的真实 YokiFrame 包根。</param>
    private WorkbenchWindow(
        WorkbenchDashboardService dashboardService,
        IntPtr parentWindowHandle,
        WindowStateStore? windowStateStore,
        WorkbenchActivationCoordinator? activationCoordinator,
        string projectRoot,
        string sourcePackageRoot)
    {
        WorkbenchStartupTrace.Mark("window.ctor.enter");
        mDashboardService = dashboardService;
        mParentWindowHandle = parentWindowHandle;
        mWindowStateStore = windowStateStore;
        mActivationCoordinator = activationCoordinator;
        WorkbenchStartupTrace.Mark("window.before-shell-view-model");
        mShellViewModel = CreateShellViewModel(projectRoot, sourcePackageRoot);
        mShellViewModel.SetTaskTracker(mSession.Track);
        WorkbenchStartupTrace.Mark("window.after-shell-view-model");
        mShellViewModel.FsmKitPage.SelectedInstanceIdChanged += OnFsmTelemetrySelectionChanged;
        // 遥测通道只捕获窗口引用，实际状态在轮询时才读取，可安全在构造期创建。
        mEventKitTelemetryChannel = new EventKitTelemetryChannel(this);
        mLogKitTelemetryChannel = new LogKitTelemetryChannel(this);
        mFsmTelemetryChannel = new FsmKitTelemetryChannel(this);
        mLifecycleMonitor = string.IsNullOrWhiteSpace(projectRoot)
            ? null
            : dashboardService.CreateLifecycleMonitor();
        if (mLifecycleMonitor != null)
        {
            mLifecycleMonitor.Changed += OnEngineLifecycleChanged;
        }
        Title = DefaultWindowTitle;
        Width = DefaultWindowWidth;
        Height = DefaultWindowHeight;
        MinWidth = 1280;
        MinHeight = 820;
        ShowInTaskbar = DefaultShowInTaskbar;
        CanMinimize = DefaultCanMinimize;
        CanResize = DefaultCanResize;
        WindowDecorations = DefaultWindowDecorations;
        WindowStartupLocation = DefaultWindowStartupLocation;
        ExtendClientAreaToDecorationsHint = DefaultExtendClientAreaToDecorations;
        ExtendClientAreaTitleBarHeightHint = 48;
        BrandIconLoader.ApplyTo(this);
        ApplySavedPage();
        WorkbenchStartupTrace.Mark("window.before-shell-view");
        Content = new WorkbenchShellView(mShellViewModel);
        WorkbenchStartupTrace.Mark("window.after-shell-view");
        ApplySavedWindowPlacement();
        mRefreshTimer.Interval = FileRefreshInterval;
        mRefreshTimer.Tick += OnRefreshTimerTick;
        Opened += OnOpened;
        Closing += OnClosing;
        Closed += OnClosed;
        if (mActivationCoordinator != null)
        {
            mActivationCoordinator.ActivationRequested += OnActivationRequested;
        }

        WorkbenchStartupTrace.Mark("window.ctor.exit");
    }

    /// <summary>
    /// 窗口打开后刷新首屏数据，避免构造阶段阻塞 UI 创建。
    /// </summary>
    /// <param name="sender">事件来源。</param>
    /// <param name="eventArgs">事件参数。</param>
    private void OnOpened(object? sender, EventArgs eventArgs)
    {
        WorkbenchStartupTrace.Mark("window.opened");
        AttachToParentWindowIfRequested();
        ActivateExistingWindow();
        _ = mShellViewModel.RuntimeUpdate.StartCheckAsync();
        mRefreshTimer.Start();
        StartFsmTelemetryPolling();
        QueueDashboardRefresh();
    }

    /// <summary>
    /// 在窗口拿到原生句柄后尝试挂载到引擎窗口；失败时保持普通窗口，避免阻断 Workbench 使用。
    /// </summary>
    private void AttachToParentWindowIfRequested()
    {
        if (mParentWindowHandle == IntPtr.Zero)
        {
            return;
        }

        WindowsWorkbenchWindowHost.TryAttach(this, mParentWindowHandle);
    }

    /// <summary>
    /// 关闭前异步提交页面草稿；先取消当前关闭，避免 UI 线程同步等待项目配置锁。
    /// </summary>
    /// <param name="sender">事件来源。</param>
    /// <param name="eventArgs">关闭事件参数。</param>
    private async void OnClosing(object? sender, WindowClosingEventArgs eventArgs)
    {
        WorkbenchStartupTrace.Mark("window.closing");

        // Avalonia Closing 事件运行在 UI 线程，配置 Store 的同步包装不能在这里等待。
        if (!mClosePersistenceCompleted)
        {
            eventArgs.Cancel = true;
            if (mClosePersistenceInFlight)
            {
                return;
            }

            mClosePersistenceInFlight = true;
            mIsClosed = true;
            mSession.Cancel();
            try
            {
                await Task.Run(async () =>
                {
                    await mShellViewModel.UIKitPage.PersistEditorSettingsOnCloseAsync();
                    mShellViewModel.LocalizationKitPage.PersistLubanWorkspaceSettingsOnClose();
                    await mShellViewModel.AudioKitPage.PersistIndexSettingsOnCloseAsync();
                    mShellViewModel.TableKitPage.TryPersistConfiguration();
                }).ConfigureAwait(true);
            }
            catch (Exception exception)
            {
                WorkbenchStartupTrace.Mark("window.close-persistence.failed." + exception.GetType().Name);
            }
            finally
            {
                mClosePersistenceInFlight = false;
                mClosePersistenceCompleted = true;
                Dispatcher.UIThread.Post(Close);
            }

            return;
        }

        mIsClosed = true;
        try
        {
            await EnsureWindowShutdownAsync().ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            WorkbenchStartupTrace.Mark("window.session-shutdown.failed." + exception.GetType().Name);
        }

        if (mActivationCoordinator != null)
        {
            mActivationCoordinator.ActivationRequested -= OnActivationRequested;
        }

        SaveWindowState();
        if (mParentWindowHandle == IntPtr.Zero)
        {
            return;
        }

        WindowsWorkbenchWindowHost.TryDetach(this);
    }

    /// <summary>
    /// 窗口关闭时停止定时刷新，避免关闭后继续访问 UI 控件。
    /// </summary>
    /// <param name="sender">事件来源。</param>
    /// <param name="eventArgs">事件参数。</param>
    private void OnClosed(object? sender, EventArgs eventArgs)
    {
        mIsClosed = true;
        mRefreshTimer.Stop();
        mShellViewModel.FsmKitPage.SelectedInstanceIdChanged -= OnFsmTelemetrySelectionChanged;
        mActivationCoordinator?.ActivationRequested -= OnActivationRequested;
        _ = EnsureWindowShutdownAsync();
    }

    /// <summary>
    /// 创建并复用唯一窗口关闭任务，确保 Closing 与 Closed 不会重复释放同一组资源。
    /// </summary>
    /// <returns>窗口后台资源全部停止后的任务。</returns>
    private Task EnsureWindowShutdownAsync()
    {
        if (mWindowShutdownTask != null)
        {
            return mWindowShutdownTask;
        }

        mTelemetryRefreshPolicy.Reset();
        Task notificationShutdown = StopTelemetryNotificationListener();
        Task fsmShutdown = StopFsmTelemetryPolling();
        mWindowShutdownTask = CompleteWindowShutdownAsync(notificationShutdown, fsmShutdown);
        return mWindowShutdownTask;
    }

    /// <summary>
    /// 兜底等待窗口关闭时仍在运行的任务，再释放通知信号和 Dashboard Client。
    /// </summary>
    /// <param name="notificationShutdown">通知 listener 的停止任务。</param>
    /// <param name="fsmShutdown">Fsm telemetry 的停止任务。</param>
    private async Task CompleteWindowShutdownAsync(
        Task notificationShutdown,
        Task fsmShutdown)
    {
        try
        {
            await mSession.DisposeAsync().ConfigureAwait(false);
            await Task.WhenAll(notificationShutdown, fsmShutdown).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            WorkbenchStartupTrace.Mark("window.shutdown.failed." + exception.GetType().Name);
        }
        finally
        {
            DisposePageViewModels();
            mTelemetryRefreshSignal.Dispose();
            mDashboardService.Dispose();
        }
    }

    /// <summary>
    /// 在后台刷新、通知和命令任务全部结束后释放页面与生命周期监视器。
    /// </summary>
    private void DisposePageViewModels()
    {
        mShellViewModel.EventKitPage.Dispose();
        mShellViewModel.FsmKitPage.Dispose();
        mShellViewModel.LogKitPage.Dispose();
        mShellViewModel.PoolKitPage.Dispose();
        mShellViewModel.ResKitPage.Dispose();
        mShellViewModel.ActionKitPage.Dispose();
        mShellViewModel.AudioKitPage.Dispose();
        mShellViewModel.UIKitPage.Dispose();
        mShellViewModel.SaveKitPage.Dispose();
        mShellViewModel.SpatialKitPage.Dispose();
        mShellViewModel.TableKitPage.Dispose();
        mShellViewModel.DocumentationPage.Dispose();
        mShellViewModel.LocalizationKitPage.Dispose();
        mShellViewModel.RuntimeUpdate.Dispose();
        mShellViewModel.Dispose();
        if (mLifecycleMonitor != null)
        {
            mLifecycleMonitor.Changed -= OnEngineLifecycleChanged;
            mLifecycleMonitor.Dispose();
        }
    }

    /// <summary>
    /// 接收后台管道线程的激活请求，并切换到 Avalonia UI 线程处理窗口状态。
    /// </summary>
    /// <param name="sender">项目级激活协调器。</param>
    /// <param name="eventArgs">需要由可用窗口显式确认的激活请求。</param>
    private void OnActivationRequested(
        object? sender,
        WorkbenchActivationRequestEventArgs eventArgs)
    {
        WorkbenchStartupTrace.Mark("window.activation.requested");
        if (mIsClosed)
        {
            WorkbenchStartupTrace.Mark("window.activation.rejected.closed");
            return;
        }

        var activated = Dispatcher.UIThread.CheckAccess()
            ? ActivateExistingWindow()
            : Dispatcher.UIThread.InvokeAsync(ActivateExistingWindow).GetAwaiter().GetResult();
        if (!activated)
        {
            WorkbenchStartupTrace.Mark("window.activation.rejected.invisible");
            return;
        }

        eventArgs.Accept();
        WorkbenchStartupTrace.Mark("window.activation.acknowledged");
    }

    /// <summary>
    /// 恢复可能最小化的 Workbench，并请求操作系统把已有窗口带到前台。
    /// </summary>
    /// <returns>窗口已显示并完成平台前台恢复时返回 true。</returns>
    private bool ActivateExistingWindow()
    {
        if (mIsClosed)
        {
            return false;
        }

        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Show();
        Activate();
        return !OperatingSystem.IsWindows()
            || WindowsWorkbenchWindowHost.TryBringToFront(this);
    }

    /// <summary>
    /// 定时刷新 dashboard，只读取 FileBridge 状态和 snapshot，不发送命令。
    /// </summary>
    /// <param name="sender">事件来源。</param>
    /// <param name="eventArgs">事件参数。</param>
    private void OnRefreshTimerTick(object? sender, EventArgs eventArgs)
    {
        QueueDashboardRefresh(TelemetryRefreshTrigger.LowFrequencyDashboard);
    }

    /// <summary>
    /// 收到 registry/heartbeat 生命周期变化后立即刷新 dashboard；1 秒文件计时器继续作为兜底。
    /// </summary>
    private void OnEngineLifecycleChanged(object? sender, EngineLifecycleChangedEventArgs eventArgs)
    {
        if (mIsClosed)
        {
            return;
        }

        Dispatcher.UIThread.Post(() => QueueDashboardRefresh(TelemetryRefreshTrigger.EngineLifecycle));
    }

    /// <summary>
    /// 把 dashboard 刷新排入后台线程，避免文件桥扫描阻塞窗口首帧和交互。
    /// </summary>
    private void QueueDashboardRefresh()
    {
        QueueDashboardRefresh(TelemetryRefreshTrigger.ExplicitDashboard);
    }

    /// <summary>
    /// 按请求来源提交 Dashboard 刷新，并由策略合并或节流重复请求。
    /// </summary>
    /// <param name="trigger">触发本次请求的来源。</param>
    private void QueueDashboardRefresh(TelemetryRefreshTrigger trigger)
    {
        if (mIsClosed)
        {
            return;
        }

        TelemetryRefreshAction action = mTelemetryRefreshPolicy.Request(
            trigger,
            DateTimeOffset.UtcNow);
        if (action != TelemetryRefreshAction.StartDashboard)
        {
            return;
        }

        StartDashboardRefresh();
    }

    /// <summary>
    /// 启动已经由刷新策略批准的 Dashboard 读取任务。
    /// </summary>
    private void StartDashboardRefresh()
    {
        long refreshVersion = mSession.BeginRefresh();
        Task refreshTask = RefreshDashboardAsync(mSelectedEngineId, refreshVersion);
        mSession.Track(refreshTask);
    }

    /// <summary>
    /// 在后台读取 dashboard，再回到 UI 线程提交 ViewModel 更新。
    /// </summary>
    /// <param name="engineId">本轮刷新使用的 engine 标识。</param>
    /// <param name="refreshVersion">本轮刷新捕获的会话代次。</param>
    /// <returns>异步操作。</returns>
    private async Task RefreshDashboardAsync(string engineId, long refreshVersion)
    {
        CancellationToken cancellationToken = mSession.LifetimeToken;
        try
        {
            var state = await Task.Run(
                    () => mDashboardService.LoadDashboard(engineId),
                    cancellationToken)
                .ConfigureAwait(false);
            if (!mSession.IsCurrentRefresh(refreshVersion))
            {
                return;
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (mSession.IsCurrentRefresh(refreshVersion)
                    && string.Equals(engineId, mSelectedEngineId, StringComparison.Ordinal))
                {
                    ApplyDashboardState(state);
                }
            }, DispatcherPriority.Background, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            WorkbenchStartupTrace.Mark("dashboard.refresh.cancelled");
        }
        catch (Exception exception)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (!mIsClosed)
                {
                    mShellViewModel.ShowTransientError(exception.Message);
                }
            }, DispatcherPriority.Background, cancellationToken);
        }
        finally
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    TelemetryRefreshAction action = mTelemetryRefreshPolicy.CompleteDashboardRefresh(
                        DateTimeOffset.UtcNow);
                    if (action == TelemetryRefreshAction.StartDashboard && !mIsClosed)
                    {
                        StartDashboardRefresh();
                    }
                }, DispatcherPriority.Background, cancellationToken);
            }
        }
    }

    /// <summary>
    /// 在 UI 线程应用 dashboard 状态，集中维护当前 engine 和 ViewModel 投影。
    /// </summary>
    /// <param name="state">后台读取到的 dashboard 状态。</param>
    private void ApplyDashboardState(WorkbenchDashboardState state)
    {
        if (mIsClosed)
        {
            return;
        }

        mCurrentState = state;
        mSelectedEngineId = mCurrentState.SelectedEngineId;
        mLifecycleMonitor?.SetEngine(mSelectedEngineId);
        mShellViewModel.UpdateDashboard(mCurrentState);
        UpdateSharedMemoryRefreshMode(mCurrentState);
    }

    /// <summary>
    /// 发送 System 命令，并在响应返回后刷新 dashboard 状态。
    /// </summary>
    /// <param name="kit">命令 Kit。</param>
    /// <param name="action">System action 名称。</param>
    /// <returns>异步操作。</returns>
    private Task SendCommandAsync(string kit, string action)
    {
        Task task = SendCommandCoreAsync(kit, action);
        mSession.Track(task);
        return task;
    }

    /// <summary>
    /// 执行命令并在会话关闭或宿主代次变化时阻止旧结果提交到页面。
    /// </summary>
    /// <param name="kit">命令 Kit。</param>
    /// <param name="action">命令 action。</param>
    /// <returns>异步命令操作。</returns>
    private async Task SendCommandCoreAsync(string kit, string action)
    {
        var selectedEngineId = mSelectedEngineId;
        var expectedIdentity = mCurrentState?.CurrentHostIdentity;
        if (expectedIdentity == null || string.IsNullOrWhiteSpace(selectedEngineId))
        {
            mShellViewModel.ShowTransientError("当前宿主身份尚未收敛，命令未发送。");
            QueueDashboardRefresh();
            return;
        }

        mShellViewModel.ShowCommandInFlight(kit, action);
        CancellationToken cancellationToken = mSession.LifetimeToken;
        var result = await Task.Run(() => mDashboardService.SendCommandAsync(
                selectedEngineId,
                kit,
                action,
                "{}",
                cancellationToken,
                expectedIdentity),
                cancellationToken)
            .ConfigureAwait(false);
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (mIsClosed || cancellationToken.IsCancellationRequested)
            {
                return;
            }

            var currentIdentity = mCurrentState?.CurrentHostIdentity;
            if (currentIdentity == null || result.TargetIdentity != currentIdentity)
            {
                QueueDashboardRefresh();
                return;
            }

            mShellViewModel.ShowCommandResult(result);
            QueueDashboardRefresh();
        }, DispatcherPriority.Background, cancellationToken);
    }

    /// <summary>
    /// 切换当前 engine，并立即刷新 Workbench 首屏状态。
    /// </summary>
    /// <param name="engineId">新选中的 engine 标识。</param>
    private void ChangeEngine(string engineId)
    {
        if (string.IsNullOrWhiteSpace(engineId) || engineId == mSelectedEngineId)
        {
            return;
        }

        mSelectedEngineId = engineId;
        QueueDashboardRefresh();
    }
}
