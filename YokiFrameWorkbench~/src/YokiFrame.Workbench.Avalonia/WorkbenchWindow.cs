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
    private WorkbenchDashboardState? mCurrentState;
    private volatile bool mIsClosed;
    private bool mIsDashboardRefreshInFlight;
    private bool mDashboardRefreshPending;
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
        WorkbenchStartupTrace.Mark("window.after-shell-view-model");
        mShellViewModel.FsmKitPage.SelectedInstanceIdChanged += OnFsmTelemetrySelectionChanged;
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
    /// 窗口关闭前解除宿主 owner 关系，避免 Win32 在销毁 owned window 时把其它任务栏窗口拉到前台。
    /// </summary>
    /// <param name="sender">事件来源。</param>
    /// <param name="eventArgs">关闭事件参数。</param>
    private void OnClosing(object? sender, WindowClosingEventArgs eventArgs)
    {
        WorkbenchStartupTrace.Mark("window.closing");
        mIsClosed = true;
        mShellViewModel.UIKitPage.PersistEditorSettingsOnClose();
        if (mActivationCoordinator != null)
        {
            mActivationCoordinator.ActivationRequested -= OnActivationRequested;
        }

        mShellViewModel.TableKitPage.TryPersistConfiguration();
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
        StopTelemetryNotificationListener();
        StopFsmTelemetryPolling();
        mShellViewModel.EventKitPage.Dispose();
        mShellViewModel.FsmKitPage.SelectedInstanceIdChanged -= OnFsmTelemetrySelectionChanged;
        mShellViewModel.FsmKitPage.Dispose();
        mShellViewModel.LogKitPage.Dispose();
        mShellViewModel.PoolKitPage.Dispose();
        mShellViewModel.ResKitPage.Dispose();
        mShellViewModel.ActionKitPage.Dispose();
        mShellViewModel.AudioKitPage.Dispose();
        mShellViewModel.SaveKitPage.Dispose();
        mShellViewModel.RuntimeUpdate.Dispose();
        if (mLifecycleMonitor != null)
        {
            mLifecycleMonitor.Changed -= OnEngineLifecycleChanged;
            mLifecycleMonitor.Dispose();
        }

        mDashboardService.Dispose();
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
        QueueDashboardRefresh();
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

        Dispatcher.UIThread.Post(QueueDashboardRefresh);
    }

    /// <summary>
    /// 把 dashboard 刷新排入后台线程，避免文件桥扫描阻塞窗口首帧和交互。
    /// </summary>
    private void QueueDashboardRefresh()
    {
        if (mIsClosed)
        {
            return;
        }

        if (mIsDashboardRefreshInFlight)
        {
            mDashboardRefreshPending = true;
            return;
        }

        mDashboardRefreshPending = false;
        mIsDashboardRefreshInFlight = true;
        _ = RefreshDashboardAsync(mSelectedEngineId);
    }

    /// <summary>
    /// 在后台读取 dashboard，再回到 UI 线程提交 ViewModel 更新。
    /// </summary>
    /// <param name="engineId">本轮刷新使用的 engine 标识。</param>
    /// <returns>异步操作。</returns>
    private async Task RefreshDashboardAsync(string engineId)
    {
        try
        {
            var state = await Task.Run(() => mDashboardService.LoadDashboard(engineId))
                .ConfigureAwait(false);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (string.Equals(engineId, mSelectedEngineId, StringComparison.Ordinal))
                {
                    ApplyDashboardState(state);
                }
            });
        }
        catch (Exception exception)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (!mIsClosed)
                {
                    mShellViewModel.ShowTransientError(exception.Message);
                }
            });
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                mIsDashboardRefreshInFlight = false;
                QueuePendingDashboardRefresh();
            });
        }
    }

    /// <summary>
    /// 在当前后台读取结束后执行合并的最后一次刷新，确保切换 engine 的请求不会被丢弃。
    /// </summary>
    private void QueuePendingDashboardRefresh()
    {
        if (!mDashboardRefreshPending || mIsClosed)
        {
            return;
        }

        mDashboardRefreshPending = false;
        QueueDashboardRefresh();
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
    /// <param name="action">System action 名称。</param>
    /// <returns>异步操作。</returns>
    private async Task SendCommandAsync(string kit, string action)
    {
        mShellViewModel.ShowCommandInFlight(kit, action);
        var selectedEngineId = mSelectedEngineId;
        var result = await Task.Run(() => mDashboardService.SendCommandAsync(selectedEngineId, kit, action, CancellationToken.None))
            .ConfigureAwait(false);
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            mShellViewModel.ShowCommandResult(result);
            QueueDashboardRefresh();
        });
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
