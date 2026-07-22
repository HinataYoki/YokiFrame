using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using YokiFrame.Workbench.Avalonia.Services;
using YokiFrame.Workbench.Avalonia.ViewModels;
using YokiFrame.Workbench.Avalonia.Views;
using YokiFrame.Tooling.Application.Services;

namespace YokiFrame.Workbench.Avalonia.Tests;

/// <summary>
/// 覆盖 Workbench 窗口位置和尺寸持久化，避免每次 Ctrl+E 都回到默认大小或异常位置。
/// </summary>
public sealed class WindowStateStoreTests
{
    /// <summary>
    /// 验证没有历史状态时使用默认尺寸并居中打开。
    /// </summary>
    [Fact]
    public void LoadReturnsCenteredDefaultWhenStateFileIsMissing()
    {
        var store = new WindowStateStore(CreateProjectRoot());

        var placement = store.Load(
            WorkbenchWindow.DefaultWindowWidth,
            WorkbenchWindow.DefaultWindowHeight,
            WorkbenchWindow.DefaultWindowStartupLocation,
            CreateWorkAreas());

        Assert.Equal(WorkbenchWindow.DefaultWindowWidth, placement.Width);
        Assert.Equal(WorkbenchWindow.DefaultWindowHeight, placement.Height);
        Assert.Equal(WindowStartupLocation.CenterScreen, placement.StartupLocation);
        Assert.Null(placement.Position);
    }

    /// <summary>
    /// 验证关闭前保存的 normal 窗口矩形会在下次打开时恢复。
    /// </summary>
    [Fact]
    public void SaveAndLoadRoundtripsWindowBounds()
    {
        var projectRoot = CreateProjectRoot();
        var store = new WindowStateStore(projectRoot);

        store.Save(new PixelPoint(120, 160), 1440, 900, WindowState.Normal, "LogKit");
        var placement = store.Load(
            WorkbenchWindow.DefaultWindowWidth,
            WorkbenchWindow.DefaultWindowHeight,
            WorkbenchWindow.DefaultWindowStartupLocation,
            CreateWorkAreas());

        Assert.Equal(1440, placement.Width);
        Assert.Equal(900, placement.Height);
        Assert.Equal(new PixelPoint(120, 160), placement.Position);
        Assert.Equal(WindowStartupLocation.Manual, placement.StartupLocation);
        Assert.Equal("LogKit", store.LoadSelectedPage());
        Assert.True(File.Exists(Path.Combine(projectRoot, ".yokiframe", "workbench", "window-state.json")));
    }

    /// <summary>
    /// 验证明显离屏的历史位置会被丢弃，避免用户更换显示器后窗口打开到不可见区域。
    /// </summary>
    [Fact]
    public void LoadFallsBackToCenteredDefaultWhenSavedPositionIsOffscreen()
    {
        var store = new WindowStateStore(CreateProjectRoot());

        store.Save(new PixelPoint(-9000, -9000), 1440, 900, WindowState.Normal, "FsmKit");
        var placement = store.Load(
            WorkbenchWindow.DefaultWindowWidth,
            WorkbenchWindow.DefaultWindowHeight,
            WorkbenchWindow.DefaultWindowStartupLocation,
            CreateWorkAreas());

        Assert.Equal(WorkbenchWindow.DefaultWindowWidth, placement.Width);
        Assert.Equal(WorkbenchWindow.DefaultWindowHeight, placement.Height);
        Assert.Equal(WindowStartupLocation.CenterScreen, placement.StartupLocation);
        Assert.Null(placement.Position);
    }

    /// <summary>
    /// 验证最大化状态不会覆盖可恢复 normal 窗口矩形。
    /// </summary>
    [Fact]
    public void SaveIgnoresMaximizedState()
    {
        var projectRoot = CreateProjectRoot();
        var store = new WindowStateStore(projectRoot);

        store.Save(new PixelPoint(100, 100), 1400, 860, WindowState.Normal, "FsmKit");
        store.Save(new PixelPoint(0, 0), 1920, 1080, WindowState.Maximized, "EventKit");
        var placement = store.Load(
            WorkbenchWindow.DefaultWindowWidth,
            WorkbenchWindow.DefaultWindowHeight,
            WorkbenchWindow.DefaultWindowStartupLocation,
            CreateWorkAreas());

        Assert.Equal(1400, placement.Width);
        Assert.Equal(860, placement.Height);
        Assert.Equal(new PixelPoint(100, 100), placement.Position);
        Assert.Equal("EventKit", store.LoadSelectedPage());
    }

    /// <summary>验证旧窗口状态没有页面字段时保持默认页面回退所需的空值。</summary>
    [Fact]
    public void LoadSelectedPageSupportsLegacyWindowState()
    {
        var projectRoot = CreateProjectRoot();
        var stateDirectory = Path.Combine(projectRoot, ".yokiframe", "workbench");
        Directory.CreateDirectory(stateDirectory);
        File.WriteAllText(
            Path.Combine(stateDirectory, "window-state.json"),
            "{\"x\":100,\"y\":100,\"width\":1400,\"height\":860}");

        var store = new WindowStateStore(projectRoot);

        Assert.Equal(string.Empty, store.LoadSelectedPage());
    }

    /// <summary>验证真实 WorkbenchWindow 构造时恢复同一项目上次关闭的页面。</summary>
    [Fact]
    public async Task WorkbenchWindowRestoresLastSelectedPage()
    {
        InstallerHeadlessTestApplication.EnsureInitialized();
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var projectRoot = CreateProjectRoot();
            var packageRoot = Path.Combine(projectRoot, "Assets", "YokiFrame");
            Directory.CreateDirectory(packageRoot);
            new WindowStateStore(projectRoot).Save(
                new PixelPoint(100, 100),
                1400,
                860,
                WindowState.Normal,
                "LogKit");
            var options = new ToolStartupOptions(
                ToolStartupMode.Workbench,
                projectRoot,
                packageRoot,
                projectRoot);
            WorkbenchWindow window = new(new WorkbenchDashboardService(projectRoot), options);
            try
            {
                window.Show();
                Dispatcher.UIThread.RunJobs();
                var shell = Assert.IsType<WorkbenchShellView>(window.Content);
                var viewModel = Assert.IsType<WorkbenchShellViewModel>(shell.DataContext);
                Assert.Equal("LogKit", viewModel.SelectedPage);
                Assert.True(viewModel.IsLogKitPage);
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>
    /// 验证窗口状态存储使用 source-generated JSON 元数据，避免 Native AOT 发布版运行时依赖反射序列化。
    /// </summary>
    [Fact]
    public void WindowStateStoreUsesNativeAotFriendlyJsonContext()
    {
        var source = File.ReadAllText(FindWindowStateStoreSourcePath());

        Assert.Contains("JsonSerializable(typeof(PersistedWindowState))", source);
        Assert.Contains("WindowStateJsonContext.Default.PersistedWindowState", source);
        Assert.DoesNotContain("JsonSerializerDefaults.Web", source);
    }

    /// <summary>
    /// 创建测试项目根目录。
    /// </summary>
    /// <returns>临时项目根目录。</returns>
    private static string CreateProjectRoot()
    {
        return Path.Combine(Path.GetTempPath(), "yokiframe-window-state-tests", Guid.NewGuid().ToString("N"));
    }

    /// <summary>
    /// 创建一块常见 1080p 工作区，用于验证位置可见性。
    /// </summary>
    /// <returns>工作区集合。</returns>
    private static IReadOnlyList<WindowWorkArea> CreateWorkAreas()
    {
        return new[]
        {
            new WindowWorkArea(new PixelRect(0, 0, 1920, 1040))
        };
    }

    /// <summary>
    /// 从测试输出目录向上查找窗口状态存储源码路径。
    /// </summary>
    /// <returns>窗口状态存储源码路径。</returns>
    private static string FindWindowStateStoreSourcePath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            var candidate = Path.Combine(
                directory.FullName,
                "Assets",
                "YokiFrame",
                "YokiFrameWorkbench~",
                "src",
                "YokiFrame.Workbench.Avalonia",
                "Services",
                "WindowStateStore.cs");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("无法定位 WindowStateStore.cs。");
    }
}
