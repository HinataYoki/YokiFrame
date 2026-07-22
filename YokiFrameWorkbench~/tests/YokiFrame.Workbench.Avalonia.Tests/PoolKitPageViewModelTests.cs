using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Layout;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using YokiFrame.Tooling.Application.Models.PoolKit;
using YokiFrame.Workbench.Avalonia.ViewModels;
using YokiFrame.Workbench.Avalonia.ViewModels.PoolKit;
using YokiFrame.Workbench.Avalonia.Views;
using YokiFrame.Workbench.Avalonia.Views.Pages;

namespace YokiFrame.Workbench.Avalonia.Tests;

/// <summary>覆盖 PoolKit 页面筛选、稳定选择、XAML 契约和 Headless 渲染。</summary>
public sealed class PoolKitPageViewModelTests
{
    /// <summary>验证等价身份刷新复用列表项和当前选择。</summary>
    [Fact]
    public void PeriodicRefreshPreservesPoolRowAndSelectionIdentity()
    {
        PoolKitPageViewModel viewModel = new();
        viewModel.ApplyPeriodicState(CreateState(1L, 2, 1));
        viewModel.SelectedPool = viewModel.Pools[1];
        var selected = viewModel.SelectedPool;

        viewModel.ApplyPeriodicState(CreateState(2L, 3, 2));

        Assert.Same(selected, viewModel.SelectedPool);
        Assert.Equal(3, viewModel.SelectedActiveCount);
        Assert.Equal(2, viewModel.Events.Count);
    }

    /// <summary>验证搜索匹配对象池名称、完整类型和健康状态。</summary>
    [Fact]
    public void SearchFiltersPoolsWithoutLosingStableSelection()
    {
        PoolKitPageViewModel viewModel = new();
        viewModel.ApplyPeriodicState(CreateState(1L, 2, 1));

        viewModel.SearchText = "Panel";

        Assert.Equal("PanelHandler", Assert.Single(viewModel.Pools).Name);
        Assert.Equal("PanelHandler", viewModel.SelectedName);
    }

    /// <summary>验证泄漏检查会清除隐藏候选的搜索条件，并把详情定位到首个候选池。</summary>
    [Fact]
    public async Task LeakCheckFocusesFirstCandidateAndReportsPoolName()
    {
        WorkbenchPoolKitState checkedState = CreateState(2L, 2, 1);
        PoolKitPageViewModel viewModel = new(
            null,
            (_, _) => Task.FromResult(checkedState),
            null,
            null);
        viewModel.ApplyPeriodicState(CreateState(1L, 1, 1));
        viewModel.SearchText = "AudioSource";

        await viewModel.CheckLeaksCommand.ExecuteAsync();

        Assert.Equal(string.Empty, viewModel.SearchText);
        Assert.Equal("PanelHandler", viewModel.SelectedName);
        Assert.True(viewModel.SelectedIsLeakCandidate);
        Assert.Contains("已定位到 PanelHandler", viewModel.OperationStatusText, StringComparison.Ordinal);
    }

    /// <summary>验证借出对象源码位置通过注入的宿主边界打开，并反馈明确成功状态。</summary>
    [Fact]
    public async Task ActiveObjectSourceLocationUsesHostOpenBoundary()
    {
        string openedPath = string.Empty;
        int openedLine = 0;
        PoolKitPageViewModel viewModel = new(
            null,
            null,
            null,
            (filePath, line) =>
            {
                openedPath = filePath;
                openedLine = line;
                return Task.CompletedTask;
            });
        viewModel.ApplyPeriodicState(CreateState(1L, 1, 1));
        viewModel.SelectedPool = viewModel.Pools.Single(static item => item.Name == "PanelHandler");
        PoolKitObjectListItemViewModel source = Assert.Single(viewModel.SelectedActiveObjects);

        await Assert.IsType<AsyncRelayCommand>(source.OpenCommand).ExecuteAsync();

        Assert.Equal("Assets/Test.cs", openedPath);
        Assert.Equal(10, openedLine);
        Assert.Equal("已打开 Test.cs:10", viewModel.OperationStatusText);
    }

    /// <summary>验证页面使用双栏主从、对象标签与事件抽屉，且样式不硬编码颜色。</summary>
    [Fact]
    public void PageContractUsesMasterDetailTabsAndEventDrawer()
    {
        string xaml = WorkbenchContractTestFiles.ReadSource("Views", "Pages", "PoolKitPageView.axaml");
        string shell = WorkbenchContractTestFiles.ReadSource("Views", "WorkbenchShellView.axaml");
        string styles = WorkbenchContractTestFiles.ReadSource("Styles", "PoolKit.axaml");

        Assert.Contains("Width=\"320\" MinWidth=\"280\" MaxWidth=\"420\"", xaml, StringComparison.Ordinal);
        Assert.Contains("PoolMasterPanel", xaml, StringComparison.Ordinal);
        Assert.Contains("PoolDetailPanel", xaml, StringComparison.Ordinal);
        Assert.Contains("GridSplitter", xaml, StringComparison.Ordinal);
        Assert.Contains("ObjectTabs", xaml, StringComparison.Ordinal);
        Assert.Contains("EventDrawer", xaml, StringComparison.Ordinal);
        Assert.Contains("VirtualizingStackPanel", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("HorizontalScrollBarVisibility=\"Auto\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("UniformGrid", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Classes=\"kit-stat poolkit-stat", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"当前对象池还没有事件记录\"", xaml, StringComparison.Ordinal);
        Assert.Contains("TextWrapping=\"Wrap\"", xaml, StringComparison.Ordinal);
        Assert.Contains("workbench.poolkit.search", xaml, StringComparison.Ordinal);
        Assert.Contains("SelectedActiveObjects", xaml, StringComparison.Ordinal);
        Assert.Contains("PoolKitObjectListItemViewModel", xaml, StringComparison.Ordinal);
        Assert.Contains("Classes=\"ghost poolkit-source-link\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Padding=\"0,0,12,0\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Width=\"3*\" MinWidth=\"220\"", xaml, StringComparison.Ordinal);
        Assert.Contains("HorizontalAlignment=\"Right\"", xaml, StringComparison.Ordinal);
        Assert.Contains("PoolKitPage.ToggleTrackingCommand", shell, StringComparison.Ordinal);
        Assert.Contains("PoolKitPage.ToggleLocationCommand", shell, StringComparison.Ordinal);
        Assert.Contains("PoolKitPage.CheckLeaksCommand", shell, StringComparison.Ordinal);
        Assert.Contains("PoolKitPage.ClearHistoryCommand", shell, StringComparison.Ordinal);
        Assert.Contains("PoolKitPage.TrackingEnabled", shell, StringComparison.Ordinal);
        Assert.Contains("PoolKitPage.StackTraceEnabled", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("FontSize.Micro", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("FontSize.Xs", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("#", styles, StringComparison.Ordinal);
    }

    /// <summary>验证最小与常用窗口下双栏工作区无横向滚动、字号可读并保存视觉证据。</summary>
    [Theory]
    [InlineData(1700, 1060)]
    [InlineData(1280, 820)]
    public async Task PageRendersAdaptiveMasterDetailWithoutHorizontalOverflow(double width, double height)
    {
        InstallerHeadlessTestApplication.EnsureInitialized();
        await Dispatcher.UIThread.InvokeAsync(() => AssertAdaptiveLayout(width, height));
    }

    /// <summary>验证真实 PoolKit 页面非空渲染，等价帧像素稳定且指标变化可见。</summary>
    [Fact]
    public async Task PageRendersStablePixelsForEquivalentFrames()
    {
        InstallerHeadlessTestApplication.EnsureInitialized();
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            PoolKitPageViewModel viewModel = new();
            viewModel.ApplyPeriodicState(CreateState(1L, 2, 1));
            PoolKitPageView view = new() { DataContext = viewModel };
            Window window = new() { Width = 1360, Height = 760, Content = view };
            try
            {
                window.Show();
                Dispatcher.UIThread.RunJobs();
                string firstHash = CaptureFrameHash(window);
                viewModel.ApplyPeriodicState(CreateState(1L, 2, 1));
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(firstHash, CaptureFrameHash(window));

                viewModel.ApplyPeriodicState(CreateState(2L, 3, 2));
                Dispatcher.UIThread.RunJobs();
                Assert.NotEqual(firstHash, CaptureFrameHash(window));
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>在真实 Workbench Shell 中检查 PoolKit 主从比例、虚拟化密度和滚动方向。</summary>
    private static void AssertAdaptiveLayout(double width, double height)
    {
        WorkbenchShellViewModel viewModel = new(() => { }, _ => { }, (_, _) => Task.CompletedTask)
        {
            SelectedPage = "PoolKit"
        };
        viewModel.PoolKitPage.ApplyPeriodicState(CreateDenseState());
        Window window = new()
        {
            Width = width,
            Height = height,
            Content = new WorkbenchShellView(viewModel)
        };
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            PoolKitPageView page = Assert.Single(
                window.GetVisualDescendants().OfType<PoolKitPageView>(),
                static item => item.IsVisible);
            Border? master = page.FindControl<Border>("PoolMasterPanel");
            Border? detail = page.FindControl<Border>("PoolDetailPanel");
            ListBox? poolList = page.FindControl<ListBox>("PoolList");
            Assert.NotNull(master);
            Assert.NotNull(detail);
            Assert.NotNull(poolList);
            Assert.InRange(master.Bounds.Width, 279d, 421d);
            Assert.True(detail.Bounds.Width > 500d);
            Assert.True(master.Bounds.Right <= detail.Bounds.Left);
            Assert.True(page.Bounds.Width <= window.ClientSize.Width);
            Assert.True(poolList.GetVisualDescendants().OfType<ListBoxItem>().Count(static item => item.IsVisible) >= 8);
            ScrollBar[] horizontalScrollBars = page.GetVisualDescendants()
                .OfType<ScrollBar>()
                .Where(static item => item.IsVisible && item.Orientation == Orientation.Horizontal)
                .ToArray();
            Assert.Empty(horizontalScrollBars);
            TextBlock[] visibleText = page.GetVisualDescendants()
                .OfType<TextBlock>()
                .Where(static item => item.IsVisible)
                .ToArray();
            Assert.NotEmpty(visibleText);
            Assert.All(visibleText, static item => Assert.True(item.FontSize >= 12d));
            SaveFrame(window, width, height);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>创建足量对象池、对象和事件，用于两档窗口的真实布局验收。</summary>
    private static WorkbenchPoolKitState CreateDenseState()
    {
        WorkbenchPoolKitPool[] pools = Enumerable.Range(0, 16)
            .Select(static index => CreatePool(
                "RuntimePool" + index.ToString("00"),
                "YokiFrame.Runtime.Sample.ObjectPool`1",
                index == 0 ? 18 : index % 5 + 1,
                index % 4 + 2))
            .ToArray();
        WorkbenchPoolKitEvent[] events = Enumerable.Range(0, 18)
            .Select(index => new WorkbenchPoolKitEvent(
                index % 5 == 0 ? "Forced" : (index % 2 == 0 ? "Return" : "Spawn"),
                18.5 + index,
                pools[0].Name,
                "RuntimeObject-" + index,
                "Assets/Runtime/PoolCaller.cs",
                40 + index))
            .ToArray();
        object dataSource = CreateInternalDataSource(
            "unity-editor", "dense-pool-session", 9L, "PlayMode",
            DateTimeOffset.Parse("2026-07-19T08:00:00Z"), "telemetry", string.Empty,
            new[] { "Global\\YokiFrame.PoolKit" }, string.Empty, "{}");
        int totalActive = pools.Sum(static item => item.ActiveCount);
        int totalInactive = pools.Sum(static item => item.InactiveCount);
        WorkbenchPoolKitSuspectedLeak[] leaks = pools
            .Take(2)
            .Select(static item => new WorkbenchPoolKitSuspectedLeak(item.Name, item.ActiveCount, item.PeakCount))
            .ToArray();
        return CreateInternal<WorkbenchPoolKitState>(
            dataSource,
            8L,
            new WorkbenchPoolKitStats(
                pools.Length,
                totalActive + totalInactive,
                totalActive,
                totalInactive,
                pools.Sum(static item => item.PeakCount),
                true,
                true,
                true,
                events.Length),
            pools,
            events,
            new WorkbenchPoolKitLeakReport(leaks, leaks.Length, true),
            pools.Length,
            events.Length,
            false,
            false);
    }

    /// <summary>创建包含两个对象池和当前池事件的强类型测试状态。</summary>
    private static WorkbenchPoolKitState CreateState(long version, int activeCount, int eventCount)
    {
        WorkbenchPoolKitPool[] pools =
        {
            CreatePool("AudioSource", "YokiFrame.AudioSource", 1, 0),
            CreatePool("PanelHandler", "YokiFrame.PanelHandler", activeCount, 1)
        };
        WorkbenchPoolKitEvent[] events = Enumerable.Range(0, eventCount)
            .Select(index => new WorkbenchPoolKitEvent(
                "Spawn", 16.85 + index, "PanelHandler", "Panel-" + index,
                "Assets/UI/Panel.cs", 18 + index))
            .ToArray();
        object dataSource = CreateInternalDataSource(
            "unity-editor", "pool-session", 8L, "PlayMode",
            DateTimeOffset.Parse("2026-07-16T08:00:00Z"), "telemetry", string.Empty,
            new[] { "Global\\YokiFrame.PoolKit" }, string.Empty, "{}");
        return CreateInternal<WorkbenchPoolKitState>(
            dataSource,
            version,
            new WorkbenchPoolKitStats(2, 4 + activeCount, 1 + activeCount, 3, 5, true, false, true, eventCount),
            pools,
            events,
            new WorkbenchPoolKitLeakReport(new[] { new WorkbenchPoolKitSuspectedLeak("PanelHandler", activeCount, 4) }, 1, true),
            2,
            eventCount,
            false,
            false);
    }

    /// <summary>创建单个对象池测试 read model。</summary>
    private static WorkbenchPoolKitPool CreatePool(string name, string typeName, int activeCount, int inactiveCount)
    {
        WorkbenchPoolKitObject[] active = Enumerable.Range(0, activeCount)
            .Select(index => new WorkbenchPoolKitObject("Object-" + index, 10 + index, "Assets/Test.cs", 10 + index))
            .ToArray();
        WorkbenchPoolKitObject[] inactive = Enumerable.Range(0, inactiveCount)
            .Select(index => new WorkbenchPoolKitObject("Idle-" + index, 0, string.Empty, 0))
            .ToArray();
        int total = activeCount + inactiveCount;
        return new WorkbenchPoolKitPool(
            name + "\u001f" + typeName + "\u001f0",
            name, typeName, total, activeCount, inactiveCount, Math.Max(total, 4), 20,
            total > 0 ? (double)activeCount / total : 0d,
            "Normal", activeCount, false, inactiveCount, false, active, inactive);
    }

    /// <summary>通过反射创建 Application 内部 PoolKit 数据源。</summary>
    private static object CreateInternalDataSource(params object[] arguments)
    {
        Type type = typeof(WorkbenchPoolKitState).Assembly.GetType(
            "YokiFrame.Tooling.Application.Models.PoolKit.WorkbenchPoolKitDataSource", true)!;
        object? instance = Activator.CreateInstance(
            type,
            System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.NonPublic,
            null,
            arguments,
            null);
        return Assert.IsAssignableFrom<object>(instance);
    }

    /// <summary>调用 Application 模型的内部构造方法。</summary>
    private static T CreateInternal<T>(params object[] arguments)
    {
        object? instance = Activator.CreateInstance(
            typeof(T),
            System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.NonPublic,
            null,
            arguments,
            null);
        return Assert.IsType<T>(instance);
    }

    /// <summary>捕获完整 Headless 帧并计算稳定像素哈希。</summary>
    private static string CaptureFrameHash(Window window)
    {
        using WriteableBitmap? frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        using var framebuffer = frame.Lock();
        int byteCount = checked(framebuffer.RowBytes * framebuffer.Size.Height);
        byte[] pixels = new byte[byteCount];
        Marshal.Copy(framebuffer.Address, pixels, 0, byteCount);
        Assert.Contains(pixels, static value => value != 0);
        return Convert.ToHexString(SHA256.HashData(pixels));
    }

    /// <summary>保存两档 PoolKit Headless 截图并拒绝空白输出。</summary>
    private static void SaveFrame(Window window, double width, double height)
    {
        using WriteableBitmap? frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        string outputDirectory = Path.Combine(
            WorkbenchContractTestFiles.FindWorkbenchRoot(),
            ".artifacts",
            "screenshots",
            "workbench");
        Directory.CreateDirectory(outputDirectory);
        string fileName = "poolkit-master-detail-" + (int)width + "x" + (int)height + ".png";
        using FileStream stream = new(
            Path.Combine(outputDirectory, fileName),
            FileMode.Create,
            FileAccess.Write,
            FileShare.Read);
        frame.Save(stream);
        Assert.True(stream.Length > 1024, "PoolKit Headless 截图内容为空或异常小。");
    }
}
