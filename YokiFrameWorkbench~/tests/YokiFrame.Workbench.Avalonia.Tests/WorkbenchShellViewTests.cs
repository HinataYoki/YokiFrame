using YokiFrame.Workbench.Avalonia.Views;
using YokiFrame.Workbench.Avalonia.ViewModels;
using System.Reflection;

namespace YokiFrame.Workbench.Avalonia.Tests;

/// <summary>
/// 覆盖 Workbench Shell 的导航契约，避免页面切片与 UI 导航漂移。
/// </summary>
public sealed partial class WorkbenchShellViewTests
{
    /// <summary>
    /// 验证框架页作为默认入口，并且导航只暴露已有真实能力的页面。
    /// </summary>
    [Fact]
    public void PageNamesOnlyExposeImplementedWorkbenchPages()
    {
        Assert.Equal("Framework", WorkbenchShellViewModel.DefaultPageName);
        Assert.Equal(
            new[] { "Framework", "Doctor", "Docs", "EventKit", "FsmKit", "LogKit", "PoolKit", "ResKit", "ActionKit", "AudioKit", "SpatialKit", "UIKit", "TableKit", "LocalizationKit", "SaveKit" },
            WorkbenchShellViewModel.PageNames);
        Assert.DoesNotContain("Architecture", WorkbenchShellViewModel.PageNames);
        Assert.Contains("EventKit", WorkbenchShellViewModel.PageNames);
        Assert.Contains("LogKit", WorkbenchShellViewModel.PageNames);
        Assert.Contains("PoolKit", WorkbenchShellViewModel.PageNames);
        Assert.Contains("ResKit", WorkbenchShellViewModel.PageNames);
        Assert.Contains("ActionKit", WorkbenchShellViewModel.PageNames);
        Assert.Contains("AudioKit", WorkbenchShellViewModel.PageNames);
        Assert.Contains("UIKit", WorkbenchShellViewModel.PageNames);
        Assert.Contains("TableKit", WorkbenchShellViewModel.PageNames);
        Assert.Contains("LocalizationKit", WorkbenchShellViewModel.PageNames);
        Assert.Contains("SaveKit", WorkbenchShellViewModel.PageNames);
    }

    /// <summary>
    /// 验证 Workbench Shell 已改为 XAML UserControl，而不是代码直接拼装 Grid。
    /// </summary>
    [Fact]
    public void WorkbenchShellViewUsesXamlUserControl()
    {
        Assert.True(typeof(global::Avalonia.Controls.UserControl).IsAssignableFrom(typeof(WorkbenchShellView)));
    }

    /// <summary>
    /// 验证 Workbench 默认使用接近 Tauri 参考图的宽屏工作台尺寸。
    /// </summary>
    [Fact]
    public void WorkbenchWindowDefaultsMatchTauriWorkbenchFrame()
    {
        Assert.Equal(1700, WorkbenchWindow.DefaultWindowWidth);
        Assert.Equal(1060, WorkbenchWindow.DefaultWindowHeight);
    }

    /// <summary>
    /// 验证 Workbench 默认按沉浸式工具窗口策略显示，不进入任务栏且不显示最小化。
    /// </summary>
    [Fact]
    public void WorkbenchWindowUsesEngineChildWindowFramePolicy()
    {
        Assert.False(WorkbenchWindow.DefaultShowInTaskbar);
        Assert.False(WorkbenchWindow.DefaultCanMinimize);
        Assert.True(WorkbenchWindow.DefaultCanResize);
        Assert.True(WorkbenchWindow.DefaultExtendClientAreaToDecorations);
        Assert.Equal(global::Avalonia.Controls.WindowDecorations.BorderOnly, WorkbenchWindow.DefaultWindowDecorations);
    }

    /// <summary>
    /// 验证 Workbench 不再绘制原生标题文本，避免与左上角品牌区重叠。
    /// </summary>
    [Fact]
    public void WorkbenchWindowSuppressesNativeTitleText()
    {
        var titleField = typeof(WorkbenchWindow).GetField("DefaultWindowTitle", BindingFlags.Public | BindingFlags.Static);

        Assert.NotNull(titleField);
        Assert.Equal(string.Empty, titleField.GetRawConstantValue());
    }

    /// <summary>
    /// 验证 Workbench 打开窗口时不在 UI 线程同步读取 dashboard，避免 Ctrl+E 体感被文件 IO 阻塞。
    /// </summary>
    [Fact]
    public void WorkbenchWindowDefersInitialDashboardRefreshUntilAfterFirstPaint()
    {
        var source = ReadWorkbenchWindowSource();
        var openedMethod = source.Substring(source.IndexOf("private void OnOpened", StringComparison.Ordinal));
        openedMethod = openedMethod.Substring(0, openedMethod.IndexOf("private void AttachToParentWindowIfRequested", StringComparison.Ordinal));

        Assert.DoesNotContain("RefreshDashboard();", openedMethod);
        Assert.Contains("AttachToParentWindowIfRequested();", openedMethod);
        Assert.Contains("QueueDashboardRefresh();", openedMethod);
        Assert.Contains("Task.Run", source);
    }

    /// <summary>
    /// 验证窗口打开时不在 engine 尚未选择前发送命令目录请求。
    /// </summary>
    [Fact]
    public void WorkbenchWindowWaitsForEngineSelectionBeforeCommandCatalogRefresh()
    {
        var source = ReadWorkbenchWindowSource();
        var openedMethod = source.Substring(source.IndexOf("private void OnOpened", StringComparison.Ordinal));
        openedMethod = openedMethod.Substring(0, openedMethod.IndexOf("private void AttachToParentWindowIfRequested", StringComparison.Ordinal));

        Assert.DoesNotContain("QueueCommandCatalogRefresh();", openedMethod);
        Assert.DoesNotContain("RefreshCommandCatalogAsync", source);
    }

    /// <summary>
    /// 验证窗口关键生命周期写入启动 trace，便于判断冷启动时间是否花在窗口创建或宿主绑定阶段。
    /// </summary>
    [Fact]
    public void WorkbenchWindowWritesStartupTraceMilestones()
    {
        var source = ReadWorkbenchWindowSource();

        Assert.Contains("WorkbenchStartupTrace.Mark(\"window.ctor.enter\")", source);
        Assert.Contains("WorkbenchStartupTrace.Mark(\"window.opened\")", source);
        Assert.Contains("WorkbenchStartupTrace.Mark(\"window.closing\")", source);
    }

    /// <summary>
    /// 验证自绘标题栏只提供最大化和关闭按钮，不再出现最小化或全屏入口。
    /// </summary>
    [Fact]
    public void WorkbenchShellChromeUsesOnlyMaximizeAndCloseButtons()
    {
        var xaml = ReadAppTitleBarXaml();

        Assert.Contains("ElementRole=\"MaximizeButton\"", xaml);
        Assert.Contains("ElementRole=\"CloseButton\"", xaml);
        Assert.DoesNotContain("ElementRole=\"MinimizeButton\"", xaml);
        Assert.DoesNotContain("ElementRole=\"FullScreenButton\"", xaml);
    }

    /// <summary>
    /// 验证自绘标题栏按钮绑定显式 Click 处理，避免只设置 chrome 角色后点击没有窗口动作。
    /// </summary>
    [Fact]
    public void WorkbenchShellChromeButtonsBindExplicitClickHandlers()
    {
        var xaml = ReadAppTitleBarXaml();

        Assert.Contains("Click=\"OnMaximizeWindowButtonClick\"", xaml);
        Assert.Contains("Click=\"OnCloseWindowButtonClick\"", xaml);
    }

    /// <summary>
    /// 验证标题栏使用品牌图片和统一矢量图标资源，避免 Unicode 字符在不同字体下显示错位。
    /// </summary>
    [Fact]
    public void AppTitleBarUsesBrandImageAndSharedIconResources()
    {
        var appXaml = ReadWorkbenchAppXaml();
        var titleBarXaml = ReadAppTitleBarXaml();
        var shellXaml = ReadWorkbenchShellViewXaml();

        Assert.Contains("Resources/Icons.axaml", appXaml);
        Assert.Contains("Assets/Brand/yoki.png", shellXaml);
        Assert.DoesNotContain("Assets/Brand/yoki.png", titleBarXaml);
        Assert.Contains("Icon.Sun", titleBarXaml);
        Assert.Contains("Icon.Moon", titleBarXaml);
        Assert.Contains("Icon.Maximize", titleBarXaml);
        Assert.Contains("Icon.Close", titleBarXaml);
        Assert.DoesNotContain("Text=\"◇\"", titleBarXaml);
        Assert.DoesNotContain("Content=\"☼\"", titleBarXaml);
        Assert.DoesNotContain("Content=\"□\"", titleBarXaml);
        Assert.DoesNotContain("Content=\"×\"", titleBarXaml);
    }

    /// <summary>
    /// 验证标题栏右侧按钮顺序为状态、主题切换、语言、最大化、关闭。
    /// </summary>
    [Fact]
    public void AppTitleBarKeepsRequestedToolbarOrder()
    {
        var xaml = ReadAppTitleBarXaml();
        var statusIndex = xaml.IndexOf("ConnectionBadgeText", StringComparison.Ordinal);
        var themeIndex = xaml.IndexOf("OnToggleThemeButtonClick", StringComparison.Ordinal);
        var languageIndex = xaml.IndexOf("CultureOptions", StringComparison.Ordinal);
        var maximizeIndex = xaml.IndexOf("OnMaximizeWindowButtonClick", StringComparison.Ordinal);
        var closeIndex = xaml.IndexOf("OnCloseWindowButtonClick", StringComparison.Ordinal);

        Assert.True(statusIndex >= 0);
        Assert.True(statusIndex < themeIndex);
        Assert.True(themeIndex < languageIndex);
        Assert.True(languageIndex < maximizeIndex);
        Assert.True(maximizeIndex < closeIndex);
    }

    /// <summary>
    /// 验证语言下拉绑定到语言选项，而不是误用命令桥的命令分组。
    /// </summary>
    [Fact]
    public void AppTitleBarLanguageSelectorUsesCultureOptions()
    {
        var xaml = ReadAppTitleBarXaml();
        var viewModel = new WorkbenchShellViewModel(() => { }, _ => { }, _ => Task.CompletedTask);

        Assert.Contains("ItemsSource=\"{CompiledBinding CultureOptions}\"", xaml);
        Assert.DoesNotContain("ItemsSource=\"{CompiledBinding CommandGroups}\"", xaml);
        Assert.Contains("中文", viewModel.CultureOptions);
        Assert.Contains("English", viewModel.CultureOptions);
        Assert.Equal("中文", viewModel.CultureText);
    }

    /// <summary>
    /// 验证 Workbench 窗口构造时设置应用图标，任务栏和窗口左上角都能使用同一品牌资产。
    /// </summary>
    [Fact]
    public void WorkbenchWindowsUseYokiBrandIcon()
    {
        var workbenchSource = ReadWorkbenchWindowSource();
        var installerSource = ReadInstallerWindowSource();

        Assert.Contains("BrandIconLoader.ApplyTo(this)", workbenchSource);
        Assert.Contains("BrandIconLoader.ApplyTo(this)", installerSource);
        Assert.Contains("catch (Exception exception)", installerSource);
        Assert.Contains("mShellViewModel.ShowLocalError", installerSource);
    }

    /// <summary>
    /// 验证文档 Enter 搜索事件把命令异常投影到现有 Workbench 错误状态。
    /// </summary>
    [Fact]
    public void DocumentationSearchEventContainsAsyncFailureBoundary()
    {
        var source = ReadWorkbenchShellViewSource();

        Assert.Contains("catch (Exception exception)", source);
        Assert.Contains("viewModel.ShowTransientError(\"文档搜索失败:", source);
    }

    /// <summary>
    /// 验证 WorkbenchApp 统一挂载资源和样式，避免每个页面各自维护色板、按钮和面板样式。
    /// </summary>
    [Fact]
    public void WorkbenchAppLoadsSharedDesignSystem()
    {
        var source = ReadWorkbenchAppSource();
        var xaml = ReadWorkbenchAppXaml();

        Assert.Contains("AvaloniaXamlLoader.Load(this)", source);
        Assert.DoesNotContain("new ResourceInclude", source);
        Assert.DoesNotContain("new StyleInclude", source);
        Assert.Contains("Resources/Colors.axaml", xaml);
        Assert.Contains("Resources/Typography.axaml", xaml);
        Assert.Contains("Styles/Buttons.axaml", xaml);
        Assert.Contains("Styles/Panels.axaml", xaml);
        Assert.Contains("Styles/Terminal.axaml", xaml);
    }

    /// <summary>
    /// 验证 Workbench Shell 已拆分为公共组件，后续页面复刻时不用继续堆同一个大 XAML。
    /// </summary>
    [Fact]
    public void WorkbenchShellUsesSharedComponents()
    {
        var xaml = ReadWorkbenchShellViewXaml();

        Assert.Contains("components:AppTitleBar", xaml);
        Assert.Contains("components:SideNavigation", xaml);
        Assert.Contains("components:MetricCard", xaml);
        Assert.Contains("components:LogConsole", xaml);
    }

    /// <summary>
    /// 验证 Windows owner 绑定不会把 Workbench 改成子窗口，只调整任务栏显示语义。
    /// </summary>
    [Fact]
    public void WindowsHostOwnerStyleKeepsMovableTopLevelFrame()
    {
        var currentStyle = WS_POPUP | WS_CAPTION | WS_THICKFRAME | WS_SYSMENU | WS_MINIMIZEBOX | WS_MAXIMIZEBOX;
        var nextStyle = InvokeCreateOwnedWindowStyle(currentStyle);
        var nextExStyle = InvokeCreateOwnedToolWindowExStyle(WS_EX_APPWINDOW);

        Assert.Equal(0, nextStyle & WS_CHILD);
        Assert.Equal(WS_POPUP, nextStyle & WS_POPUP);
        Assert.Equal(WS_CAPTION, nextStyle & WS_CAPTION);
        Assert.Equal(WS_THICKFRAME, nextStyle & WS_THICKFRAME);
        Assert.Equal(WS_SYSMENU, nextStyle & WS_SYSMENU);
        Assert.Equal(0, nextStyle & WS_MINIMIZEBOX);
        Assert.Equal(WS_MAXIMIZEBOX, nextStyle & WS_MAXIMIZEBOX);
        Assert.Equal(0, nextExStyle & WS_EX_APPWINDOW);
        Assert.Equal(WS_EX_TOOLWINDOW, nextExStyle & WS_EX_TOOLWINDOW);
    }

    /// <summary>
    /// 验证 Workbench 关闭前会先解除 Windows owner 关系，避免关闭时系统把 owner 链路中的窗口拉到前台。
    /// </summary>
    [Fact]
    public void WorkbenchWindowDetachesWindowsOwnerBeforeClose()
    {
        var source = ReadWorkbenchWindowSource();

        Assert.Contains("Closing += OnClosing;", source);
        Assert.Contains("WindowsWorkbenchWindowHost.TryDetach(this);", source);
        Assert.True(source.IndexOf("Closing += OnClosing;", StringComparison.Ordinal) < source.IndexOf("Closed += OnClosed;", StringComparison.Ordinal));
    }

    /// <summary>
    /// 验证 Workbench 关闭时无条件保存 TableKit 草稿，且保存发生在窗口布局状态之前。
    /// </summary>
    [Fact]
    public void WorkbenchWindowPersistsTableKitConfigurationBeforeClose()
    {
        var source = ReadWorkbenchWindowSource();
        var closingStart = source.IndexOf("private void OnClosing", StringComparison.Ordinal);
        var closedStart = source.IndexOf("private void OnClosed", StringComparison.Ordinal);
        var closingBody = source[closingStart..closedStart];
        const string persistCall = "mShellViewModel.TableKitPage.TryPersistConfiguration();";

        Assert.Contains(persistCall, closingBody);
        Assert.DoesNotContain("if (mWindowStateStore != null) " + persistCall, closingBody);
        Assert.True(
            closingBody.IndexOf(persistCall, StringComparison.Ordinal)
            < closingBody.IndexOf("SaveWindowState();", StringComparison.Ordinal));
    }

    /// <summary>
    /// 验证 Windows host 提供关闭前 owner 解绑入口，避免窗口销毁时仍保留 GWLP_HWNDPARENT。
    /// </summary>
    [Fact]
    public void WindowsHostCanDetachOwnedWindowParent()
    {
        var source = ReadWindowsHostSource();

        Assert.Contains("public static bool TryDetach", source);
        Assert.Contains("GetHiddenOwnerWindow()", source);
        Assert.Contains("sHiddenOwnerWindow", source);
        Assert.Contains("WS_EX_TOOLWINDOW", source);
        Assert.DoesNotContain("GWLP_HWNDPARENT, IntPtr.Zero", source);
        Assert.DoesNotContain("DestroyWindow", source);
    }

    /// <summary>
    /// 验证 Workbench 强制使用暗色主题，避免跟随系统浅色主题回到默认控件外观。
    /// </summary>
    [Fact]
    public void WorkbenchAppUsesDarkThemeByDefault()
    {
        Assert.Equal(global::Avalonia.Styling.ThemeVariant.Dark, WorkbenchApp.DefaultThemeVariant);
    }

    /// <summary>
    /// 验证发布版不启用 Avalonia Trace 日志，避免冷启动时初始化无用诊断输出。
    /// </summary>
    [Fact]
    public void ProgramKeepsAvaloniaTraceLoggingDebugOnly()
    {
        var source = ReadProgramSource();

        Assert.Contains("#if DEBUG", source);
        Assert.Contains(".LogToTrace()", source);
    }

    /// <summary>
    /// 验证 Windows 发布路径使用 Win32 / Skia 专用初始化，避免每次冷启动都走跨平台探测分支。
    /// </summary>
    [Fact]
    public void ProgramUsesWindowsSpecificAvaloniaStartupOnWindows()
    {
        var source = ReadProgramSource();

        Assert.Contains("OperatingSystem.IsWindows()", source);
        Assert.Contains(".UseWin32()", source);
        Assert.Contains(".UseSkia()", source);
        Assert.Contains(".UseHarfBuzz()", source);
        Assert.Contains(".UsePlatformDetect()", source);
    }

    /// <summary>
    /// 验证 Workbench 进程内写入启动打点，方便区分 CLR / Avalonia / 窗口创建各阶段耗时。
    /// </summary>
    [Fact]
    public void ProgramWritesStartupTraceForColdStartDiagnosis()
    {
        var source = ReadProgramSource();

        Assert.Contains("WorkbenchStartupTrace", source);
        Assert.Contains("Mark(\"main.enter\")", source);
        Assert.Contains("Mark(\"main.after-lifetime\")", source);
    }

    /// <summary>
    /// 验证 Workbench 模式在 Avalonia 初始化前重定向重复进程，并由主窗口恢复和激活已有实例。
    /// </summary>
    [Fact]
    public void WorkbenchStartupRedirectsDuplicateProjectAndActivatesPrimaryWindow()
    {
        var programSource = ReadProgramSource();
        var appSource = ReadWorkbenchAppSource();
        var windowSource = ReadWorkbenchWindowSource();

        Assert.Contains("WorkbenchActivationCoordinator.Start", programSource);
        Assert.Contains("ActivationRedirected", programSource);
        Assert.Contains("Program.StartupOptions", appSource);
        Assert.Contains("Program.ActivationCoordinator", appSource);
        Assert.Contains("ActivationRequested += OnActivationRequested", windowSource);
        Assert.Contains("WindowState = WindowState.Normal", windowSource);
        Assert.Contains("Activate()", windowSource);
    }

    /// <summary>
    /// 验证桌面生命周期按主窗口关闭退出，避免关闭后残留隐藏窗口或进程影响任务栏焦点。
    /// </summary>
    [Fact]
    public void WorkbenchAppShutsDownWhenMainWindowCloses()
    {
        var source = ReadWorkbenchAppSource();

        Assert.Contains("ShutdownMode.OnMainWindowClose", source);
    }

    /// <summary>
    /// 验证 Workbench Shell ViewModel 负责页面选择状态，便于 XAML 编译绑定。
    /// </summary>
    [Fact]
    public void WorkbenchShellViewModelTracksSelectedPage()
    {
        var viewModel = new WorkbenchShellViewModel(() => { }, _ => { }, _ => Task.CompletedTask);

        viewModel.SelectedPage = "FsmKit";

        Assert.Equal("FsmKit", viewModel.SelectedPage);
        Assert.Equal("FsmKit", viewModel.CurrentPageTitle);
    }

    /// <summary>
    /// 验证 Workbench Shell ViewModel 已提供新版操作型总览需要的结构化布局数据。
    /// </summary>
    [Fact]
    public void WorkbenchShellViewModelProvidesOperationalOverviewData()
    {
        var viewModel = new WorkbenchShellViewModel(() => { }, _ => { }, _ => Task.CompletedTask);

        Assert.Contains(viewModel.NavigationGroups, group => group.Title == "Core");
        Assert.Contains(viewModel.NavigationGroups, group => group.Items.Any(item => item.PageName == "FsmKit"));
        Assert.Contains(viewModel.NavigationGroups, group => group.Items.Any(item => item.PageName == "EventKit"));
        Assert.True(viewModel.SummaryCards.Count >= 3);
        Assert.InRange(viewModel.EngineCards.Count, 1, 4);
        Assert.NotEmpty(viewModel.SnapshotCards);
        Assert.NotEmpty(viewModel.SkillOptions);
        Assert.Equal(
            new[] { "yokiframe", "yokiframe-cli", "yokiframe-workbench" },
            viewModel.SkillOptions.Select(static option => option.Name));
        Assert.Contains(viewModel.SkillOptions, static option => option.Name == "yokiframe-cli" && option.Label == "CLI 指南");
        Assert.Contains(viewModel.SkillOptions, static option => option.Name == "yokiframe-workbench" && option.Label == "工作台指南");
        Assert.DoesNotContain(viewModel.SkillOptions, static option => option.Name is "yokiframe-command-bridge" or "yokiframe-editor");
        Assert.NotEmpty(viewModel.SkillStatusCards);
        Assert.NotEmpty(viewModel.SkillTargets);
        Assert.NotEmpty(viewModel.LogLines);
    }

    /// <summary>
    /// 验证 Workbench 对外展示稳定的 FileBridge 名称，协议代际只由 protocolVersion 表达。
    /// </summary>
    [Fact]
    public void WorkbenchMetricCardsUseStableFileBridgeName()
    {
        var viewModel = new WorkbenchShellViewModel(() => { }, _ => { }, _ => Task.CompletedTask);
        var metricDetails = viewModel.SummaryCards
            .Concat(viewModel.EngineCards)
            .Concat(viewModel.SnapshotCards)
            .Select(static card => card.Detail);

        Assert.Contains("FileBridge", metricDetails);
        Assert.DoesNotContain("FileBridge v2", metricDetails);
    }

    /// <summary>
    /// 验证 Workbench 能从 System/list_commands 返回值更新快捷命令下拉，而不是固定写死几组命令。
    /// </summary>
    [Fact]
    public void WorkbenchShellViewModelUpdatesCommandOptionsFromCatalogJson()
    {
        var viewModel = new WorkbenchShellViewModel(() => { }, _ => { }, _ => Task.CompletedTask);
        var updateMethod = typeof(WorkbenchShellViewModel).GetMethod("UpdateCommandCatalogJson", BindingFlags.Instance | BindingFlags.Public);
        var catalogJson = "{\"kits\":[{\"kit\":\"System\",\"actions\":[{\"action\":\"ping\"},{\"action\":\"list_commands\"}]},{\"kit\":\"EventKit\",\"actions\":[{\"action\":\"get_workbench_snapshot\"}]}]}";

        Assert.NotNull(updateMethod);
        updateMethod.Invoke(viewModel, new object[] { catalogJson });

        Assert.Contains("System", viewModel.CommandGroups);
        Assert.Contains("EventKit", viewModel.CommandGroups);
        Assert.DoesNotContain("Bridge", viewModel.CommandGroups);
        viewModel.CommandGroup = "EventKit";
        Assert.Contains("get_workbench_snapshot", viewModel.CommandActions);
    }

    /// <summary>
    /// 验证命令桥卡片只提供链路验证按钮，并且每个按钮都绑定真实命令入口。
    /// </summary>
    [Fact]
    public void WorkbenchShellCommandBridgeUsesVerificationCommands()
    {
        var xaml = ReadWorkbenchShellViewXaml();

        Assert.Contains("Content=\"Ping\"", xaml);
        Assert.Contains("Command=\"{CompiledBinding PingCommand}\"", xaml);
        Assert.Contains("Content=\"状态\"", xaml);
        Assert.Contains("Command=\"{CompiledBinding BridgeStatusCommand}\"", xaml);
        Assert.Contains("Content=\"目录\"", xaml);
        Assert.Contains("Command=\"{CompiledBinding RefreshCommandCatalogCommand}\"", xaml);
        Assert.DoesNotContain("Content=\"发送\"", xaml);
    }
}
