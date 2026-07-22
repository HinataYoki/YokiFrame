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
using YokiFrame.Tooling.Application.Models.ResKit;
using YokiFrame.Workbench.Avalonia.ViewModels;
using YokiFrame.Workbench.Avalonia.Views;
using YokiFrame.Workbench.Avalonia.Views.Pages;

namespace YokiFrame.Workbench.Avalonia.Tests;

/// <summary>覆盖 ResKit 页面筛选、稳定选择、XAML 契约和 Headless 渲染。</summary>
public sealed class ResKitPageViewModelTests
{
    /// <summary>验证等价资源身份刷新复用列表项和当前选择。</summary>
    [Fact]
    public void PeriodicRefreshPreservesResourceRowAndSelectionIdentity()
    {
        ResKitPageViewModel viewModel = new();
        viewModel.ApplyPeriodicState(CreateState(1L, 2));
        viewModel.SelectedResource = viewModel.Resources[1];
        var selected = viewModel.SelectedResource;

        viewModel.ApplyPeriodicState(CreateState(2L, 4));

        Assert.Same(selected, viewModel.SelectedResource);
        Assert.Equal(4, viewModel.SelectedLeaseCount);
    }

    /// <summary>验证搜索匹配路径、类型和 Provider。</summary>
    [Fact]
    public void SearchFiltersResourcesWithoutLosingStableSelection()
    {
        ResKitPageViewModel viewModel = new();
        viewModel.ApplyPeriodicState(CreateState(1L, 2));

        viewModel.SearchText = "AudioClip";

        Assert.Equal("Audio/Hit", Assert.Single(viewModel.Resources).Path);
        Assert.Equal("Audio/Hit", viewModel.SelectedPath);
    }

    /// <summary>验证周期状态携带的来源预览会立即显示，不需要先发送详情命令。</summary>
    [Fact]
    public void PeriodicStateShowsTrackedSourcePreviewImmediately()
    {
        ResKitPageViewModel viewModel = new();

        viewModel.ApplyPeriodicState(CreateState(7L, 2, true));
        viewModel.SelectedResource = viewModel.Resources[1];

        var source = Assert.Single(viewModel.Sources);
        Assert.Equal("Assets/Audio/AudioLoader.cs", source.FilePath);
        Assert.Contains("1 / 2", viewModel.SourceStatusText, StringComparison.Ordinal);
    }

    /// <summary>验证详情命令因资源竞态失败时保留实时状态中的来源预览。</summary>
    [Fact]
    public async Task DetailFailureKeepsTrackedSourcePreview()
    {
        ResKitPageViewModel viewModel = new(
            (_, _, _, _) => Task.FromException<WorkbenchResKitResourceDetail>(
                new InvalidOperationException("resource changed")),
            null,
            null,
            null);
        viewModel.ApplyPeriodicState(CreateState(7L, 2, true));
        viewModel.SelectedResource = viewModel.Resources[1];

        await viewModel.LoadSourcesCommand.ExecuteAsync();

        Assert.Single(viewModel.Sources);
        Assert.Contains("已保留实时预览", viewModel.SourceStatusText, StringComparison.Ordinal);
    }

    /// <summary>验证较旧的详情结果不会覆盖周期状态中已经观察到的来源预览。</summary>
    [Fact]
    public async Task OlderDetailDoesNotOverwriteNewerPeriodicState()
    {
        WorkbenchResKitResource staleResource = new(
            "Audio/Hit\u001fUnityEngine.AudioClip",
            "Audio/Hit",
            "UnityEngine.AudioClip",
            "Ready",
            1,
            "Unity.Resources",
            3L,
            1,
            new[]
            {
                new WorkbenchResKitLoadSource(
                    "StaleLoader", "Assets/Stale.cs", 9, 1, false, true)
            },
            1,
            false);
        ResKitPageViewModel viewModel = new(
            (_, _, _, _) => Task.FromResult(new WorkbenchResKitResourceDetail(6L, staleResource)),
            null,
            null,
            null);
        viewModel.ApplyPeriodicState(CreateState(7L, 2, true));
        viewModel.SelectedResource = viewModel.Resources[1];

        await viewModel.LoadSourcesCommand.ExecuteAsync();

        var preview = Assert.Single(viewModel.Sources);
        Assert.Equal("Assets/Audio/AudioLoader.cs", preview.FilePath);
        Assert.Contains("当前状态已更新至 v7", viewModel.SourceStatusText, StringComparison.Ordinal);
    }

    /// <summary>验证页面使用双栏主从、历史抽屉、按需详情和 Shell 显式动作。</summary>
    [Fact]
    public void PageContractUsesMasterDetailAndHistoryDrawer()
    {
        string xaml = WorkbenchContractTestFiles.ReadSource("Views", "Pages", "ResKitPageView.axaml");
        string shell = WorkbenchContractTestFiles.ReadSource("Views", "WorkbenchShellView.axaml");
        string styles = WorkbenchContractTestFiles.ReadSource("Styles", "ResKit.axaml");

        Assert.Contains("ResourceMasterPanel", xaml, StringComparison.Ordinal);
        Assert.Contains("ResourceDetailPanel", xaml, StringComparison.Ordinal);
        Assert.Contains("GridSplitter", xaml, StringComparison.Ordinal);
        Assert.Contains("HistoryDrawer", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("reskit-overview-strip", xaml, StringComparison.Ordinal);
        Assert.Contains("VirtualizingStackPanel", xaml, StringComparison.Ordinal);
        Assert.Contains("workbench.reskit.search", xaml, StringComparison.Ordinal);
        Assert.Contains("LoadSourcesCommand", xaml, StringComparison.Ordinal);
        Assert.Contains("ResKitPage.ToggleTrackingCommand", shell, StringComparison.Ordinal);
        Assert.Contains("ResKitPage.ClearHistoryCommand", shell, StringComparison.Ordinal);
        Assert.Contains("ResKitPage.TrackingEnabled", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("HorizontalScrollBarVisibility=\"Auto\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("UniformGrid", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("FontSize.Xs", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("FontSize.Micro", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Release", xaml, StringComparison.OrdinalIgnoreCase);
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

    /// <summary>验证等价帧保持选择与布局稳定，指标变化仍产生可见像素差异。</summary>
    [Fact]
    public async Task PageKeepsLayoutStableAndRendersMetricChanges()
    {
        InstallerHeadlessTestApplication.EnsureInitialized();
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            ResKitPageViewModel viewModel = new();
            viewModel.ApplyPeriodicState(CreateState(1L, 2));
            ResKitPageView view = new() { DataContext = viewModel };
            Window window = new() { Width = 1360, Height = 760, Content = view };
            try
            {
                window.Show();
                Dispatcher.UIThread.RunJobs();
                viewModel.ApplyPeriodicState(CreateState(1L, 2));
                Dispatcher.UIThread.RunJobs();
                string firstHash = CaptureFrameHash(window);
                Rect firstBounds = view.Bounds;
                var selected = viewModel.SelectedResource;
                viewModel.ApplyPeriodicState(CreateState(1L, 2));
                Dispatcher.UIThread.RunJobs();
                Assert.Same(selected, viewModel.SelectedResource);
                Assert.Equal(firstBounds, view.Bounds);
                viewModel.ApplyPeriodicState(CreateState(2L, 4));
                Dispatcher.UIThread.RunJobs();
                Assert.NotEqual(firstHash, CaptureFrameHash(window));
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>在真实 Workbench Shell 中检查 ResKit 主从比例、列表密度和滚动方向。</summary>
    private static void AssertAdaptiveLayout(double width, double height)
    {
        WorkbenchShellViewModel viewModel = new(() => { }, _ => { }, (_, _) => Task.CompletedTask)
        {
            SelectedPage = "ResKit"
        };
        viewModel.ResKitPage.ApplyPeriodicState(CreateDenseState());
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
            ResKitPageView page = Assert.Single(
                window.GetVisualDescendants().OfType<ResKitPageView>(),
                static item => item.IsVisible);
            Border? master = page.FindControl<Border>("ResourceMasterPanel");
            Border? detail = page.FindControl<Border>("ResourceDetailPanel");
            ListBox? resourceList = page.FindControl<ListBox>("ResourceList");
            Expander? history = page.FindControl<Expander>("HistoryDrawer");
            Assert.NotNull(master);
            Assert.NotNull(detail);
            Assert.NotNull(resourceList);
            Assert.NotNull(history);
            Assert.False(history.IsExpanded);
            Assert.InRange(master.Bounds.Width, 289d, 431d);
            Assert.True(detail.Bounds.Width > 500d);
            Assert.True(history.Bounds.Width > detail.Bounds.Width);
            Assert.True(master.Bounds.Right <= detail.Bounds.Left);
            Assert.True(page.Bounds.Width <= window.ClientSize.Width);
            Assert.True(resourceList.GetVisualDescendants().OfType<ListBoxItem>().Count(static item => item.IsVisible) >= 8);
            if (width >= 1600d)
            {
                history.IsExpanded = true;
                Dispatcher.UIThread.RunJobs();
            }
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

    /// <summary>创建足量资源和卸载历史，用于两档窗口的真实布局验收。</summary>
    private static WorkbenchResKitState CreateDenseState()
    {
        string[] typeNames =
        {
            "UnityEngine.AudioClip",
            "UnityEngine.Texture2D",
            "YokiFrame.Runtime.GameConfig",
            "UnityEngine.Material"
        };
        WorkbenchResKitResource[] resources = Enumerable.Range(0, 24)
            .Select(index => CreateResource(
                "Assets/Runtime/Feature" + (index % 4) + "/Resource-" + index.ToString("00"),
                typeNames[index % typeNames.Length],
                index % 6 + 1))
            .ToArray();
        WorkbenchResKitUnloadRecord[] history = Enumerable.Range(0, 16)
            .Select(index => new WorkbenchResKitUnloadRecord(
                "Assets/Released/Resource-" + index + "\u001f" + typeNames[index % typeNames.Length] + "\u001f" + index,
                "Assets/Released/Resource-" + index.ToString("00"),
                typeNames[index % typeNames.Length],
                "Unity.Resources",
                "2026-07-19T08:" + index.ToString("00") + ":14.9335149Z"))
            .ToArray();
        object source = CreateInternalDataSource(
            "unity-editor", "dense-res-session", 9L, "PlayMode",
            DateTimeOffset.Parse("2026-07-19T08:00:00Z"), "telemetry", string.Empty,
            new[] { "Global\\YokiFrame.ResKit" }, string.Empty, "{}");
        return CreateInternal<WorkbenchResKitState>(
            source,
            8L,
            new WorkbenchResKitProvider("Unity.Resources", 12L, true, true),
            new WorkbenchResKitStats(
                resources.Length,
                3,
                resources.Sum(static item => item.LeaseCount),
                1,
                true),
            resources,
            history,
            48,
            100,
            431L,
            true,
            true,
            string.Empty);
    }

    /// <summary>创建包含两个资源和一条历史的强类型测试状态。</summary>
    private static WorkbenchResKitState CreateState(
        long version,
        int audioLeaseCount,
        bool includeSourcePreview = false)
    {
        WorkbenchResKitResource[] resources =
        {
            CreateResource("Configs/Main", "YokiFrame.GameConfig", 1),
            CreateResource("Audio/Hit", "UnityEngine.AudioClip", audioLeaseCount, includeSourcePreview)
        };
        WorkbenchResKitUnloadRecord[] history =
        {
            new("Audio/Old\u001fUnityEngine.AudioClip\u001f2026-07-17T08:00:00Z\u001f0",
                "Audio/Old", "UnityEngine.AudioClip", "Unity.Resources", "2026-07-17T08:00:00Z")
        };
        object source = CreateInternalDataSource(
            "unity-editor", "res-session", 8L, "PlayMode",
            DateTimeOffset.Parse("2026-07-17T08:00:00Z"), "telemetry", string.Empty,
            new[] { "Global\\YokiFrame.ResKit" }, string.Empty, "{}");
        return CreateInternal<WorkbenchResKitState>(
            source, version,
            new WorkbenchResKitProvider("Unity.Resources", 3L, true, true),
            new WorkbenchResKitStats(2, 0, 1 + audioLeaseCount, 1, true),
            resources, history, 2, 1, 0L, false, false, string.Empty);
    }

    /// <summary>创建单个资源测试 read model。</summary>
    private static WorkbenchResKitResource CreateResource(
        string path,
        string typeName,
        int leaseCount,
        bool includeSourcePreview = false)
    {
        WorkbenchResKitLoadSource[] sources = includeSourcePreview
            ? new[]
            {
                new WorkbenchResKitLoadSource(
                    "AudioLoader.Play", "Assets/Audio/AudioLoader.cs", 42, 1, false, true)
            }
            : Array.Empty<WorkbenchResKitLoadSource>();
        return new WorkbenchResKitResource(
            path + "\u001f" + typeName, path, typeName, "Ready", leaseCount,
            "Unity.Resources", 3L, includeSourcePreview ? leaseCount : 1,
            sources, includeSourcePreview ? leaseCount : 0, includeSourcePreview && leaseCount > sources.Length);
    }

    /// <summary>通过反射创建 Application 内部 ResKit 数据源。</summary>
    private static object CreateInternalDataSource(params object[] arguments)
    {
        Type type = typeof(WorkbenchResKitState).Assembly.GetType(
            "YokiFrame.Tooling.Application.Models.ResKit.WorkbenchResKitDataSource", true)!;
        object? instance = Activator.CreateInstance(
            type,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic,
            null, arguments, null);
        return Assert.IsAssignableFrom<object>(instance);
    }

    /// <summary>调用 Application 模型的内部构造方法。</summary>
    private static T CreateInternal<T>(params object[] arguments)
    {
        object? instance = Activator.CreateInstance(
            typeof(T),
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic,
            null, arguments, null);
        return Assert.IsType<T>(instance);
    }

    /// <summary>捕获完整 Headless 帧并计算非空像素哈希。</summary>
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

    /// <summary>保存两档 ResKit Headless 截图并拒绝空白输出。</summary>
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
        string fileName = "reskit-master-detail-" + (int)width + "x" + (int)height + ".png";
        using FileStream stream = new(
            Path.Combine(outputDirectory, fileName),
            FileMode.Create,
            FileAccess.Write,
            FileShare.Read);
        frame.Save(stream);
        Assert.True(stream.Length > 1024, "ResKit Headless 截图内容为空或异常小。");
    }
}
