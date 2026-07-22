using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Layout;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using YokiFrame.Tooling.Application.Models.SpatialKit;
using YokiFrame.Workbench.Avalonia.ViewModels;
using YokiFrame.Workbench.Avalonia.Views;
using YokiFrame.Workbench.Avalonia.Views.Pages;

namespace YokiFrame.Workbench.Avalonia.Tests;

/// <summary>验证 SpatialKit 页面状态、主从布局和双尺寸 Headless 视觉结果。</summary>
public sealed class SpatialKitPageViewModelTests
{
    /// <summary>验证页面保留索引选择并把 bin 转换为固定数量的热力图单元。</summary>
    [Fact]
    public void ApplyPeriodicState_PopulatesDensityCellsAndHealth()
    {
        WorkbenchSpatialKitState state = Parse("{\"schemaVersion\":1,\"version\":2,\"stats\":{\"activeIndexCount\":1,\"entityCount\":4,\"partitionCount\":2},\"indexes\":[{\"diagnosticsId\":\"q-1\",\"indexKind\":\"Quadtree\",\"entityTypeName\":\"Enemy\",\"count\":4,\"plane\":\"XZ\",\"maxDepth\":6,\"maxEntitiesPerNode\":4,\"partitionCount\":2,\"createdAtUtc\":\"2026-07-18T00:00:00Z\",\"density\":{\"diagnosticsId\":\"q-1\",\"indexKind\":\"Quadtree\",\"plane\":\"XZ\",\"resolution\":2,\"totalBins\":4,\"occupiedBins\":1,\"minCount\":4,\"meanCount\":1,\"p95Count\":1,\"maxCount\":4,\"bins\":[0,0,0,4],\"hotspots\":[{\"x\":1,\"y\":1,\"count\":4}]}}]}");
        SpatialKitPageViewModel viewModel = new();

        viewModel.ApplyPeriodicState(state);

        Assert.Single(viewModel.Indexes);
        Assert.Same(viewModel.Indexes[0], viewModel.SelectedIndex);
        Assert.Equal(4, viewModel.DensityCells.Count);
        Assert.Equal("存在明显热点分区，建议检查 cell size 或实体分布", viewModel.HealthText);
        Assert.Equal(1, viewModel.ActiveIndexCount);
        Assert.True(viewModel.HasDensity);
        Assert.Equal("1 / 4", viewModel.DensityOccupancyText);
        Assert.Equal("2 x 2", viewModel.DensityResolutionText);
        Assert.True(viewModel.HasHealthWarning);
    }

    /// <summary>验证选中无密度索引时只显示右侧局部空状态，不清空左侧实例列表。</summary>
    [Fact]
    public void SelectingIndexWithoutDensityShowsLocalEmptyState()
    {
        SpatialKitPageViewModel viewModel = new();
        viewModel.ApplyPeriodicState(CreateState(3, includeMissingDensity: true));

        viewModel.SelectedIndex = viewModel.Indexes[1];

        Assert.Equal(3, viewModel.Indexes.Count);
        Assert.True(viewModel.ShowDensityEmpty);
        Assert.False(viewModel.HasDensity);
        Assert.False(viewModel.ShowNoSelection);
        Assert.Equal("当前索引暂无密度数据", viewModel.HealthText);
    }

    /// <summary>验证同一宿主会话中的旧版本不会覆盖当前热力图。</summary>
    [Fact]
    public void ApplyPeriodicState_RejectsOlderVersion()
    {
        SpatialKitPageViewModel viewModel = new();
        viewModel.ApplyPeriodicState(Parse("{\"schemaVersion\":1,\"version\":3,\"stats\":{\"activeIndexCount\":1},\"indexes\":[]}"));
        viewModel.ApplyPeriodicState(Parse("{\"schemaVersion\":1,\"version\":2,\"stats\":{\"activeIndexCount\":9},\"indexes\":[]}"));

        Assert.Equal(1, viewModel.ActiveIndexCount);
    }

    /// <summary>验证页面采用左侧索引主列表和右侧诊断热力图主从结构。</summary>
    [Fact]
    public void PageContractUsesSpatialMasterDetailWorkspace()
    {
        string xaml = WorkbenchContractTestFiles.ReadSource("Views", "Pages", "SpatialKitPageView.axaml");
        string styles = WorkbenchContractTestFiles.ReadSource("Styles", "SpatialKit.axaml");

        Assert.Contains("SpatialIndexPanel", xaml, StringComparison.Ordinal);
        Assert.Contains("SpatialDetailPanel", xaml, StringComparison.Ordinal);
        Assert.Contains("GridSplitter", xaml, StringComparison.Ordinal);
        Assert.Contains("VirtualizingStackPanel", xaml, StringComparison.Ordinal);
        Assert.Contains("HeatmapViewport", xaml, StringComparison.Ordinal);
        Assert.Contains("CompiledBinding PlaneBadge", xaml, StringComparison.Ordinal);
        Assert.Contains("CompiledBinding SelectedIndex.ProjectionDescription", xaml, StringComparison.Ordinal);
        Assert.Contains("ShowDensityEmpty", xaml, StringComparison.Ordinal);
        Assert.Contains("ScrollViewer.HorizontalScrollBarVisibility=\"Disabled\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("HorizontalScrollBarVisibility=\"Auto\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("FontSize.Xs", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("FontSize.Micro", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Viewbox", xaml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("#", styles, StringComparison.Ordinal);
    }

    /// <summary>验证两档窗口下主从比例、热力图方形约束和滚动方向均稳定。</summary>
    [Theory]
    [InlineData(1700, 1060)]
    [InlineData(1280, 820)]
    public async Task PageRendersAdaptiveMasterDetailWithoutHorizontalOverflow(double width, double height)
    {
        InstallerHeadlessTestApplication.EnsureInitialized();
        await Dispatcher.UIThread.InvokeAsync(() => AssertAdaptiveLayout(width, height));
    }

    /// <summary>验证空索引和 stale 无密度状态仍保持可读空状态，不产生横向溢出。</summary>
    [Fact]
    public async Task PageRendersEmptyAndStaleStatesWithReadableFallbacks()
    {
        InstallerHeadlessTestApplication.EnsureInitialized();
        await Dispatcher.UIThread.InvokeAsync(AssertFallbackStates);
    }

    /// <summary>将 JSON 包装成页面状态，并允许构造来源 stale 诊断。</summary>
    private static WorkbenchSpatialKitState Parse(string payload, string staleReason = "")
    {
        WorkbenchSpatialKitDataSource source = new(
            "unity", "session-1", 1L, "Editor", DateTimeOffset.UtcNow,
            "snapshot", string.Empty, Array.Empty<string>(), staleReason, payload);
        return WorkbenchSpatialKitStateParser.Parse(source);
    }

    /// <summary>在同一页面实例中验收空列表和 stale/无密度回退状态。</summary>
    private static void AssertFallbackStates()
    {
        WorkbenchShellViewModel viewModel = new(() => { }, _ => { }, (_, _) => Task.CompletedTask)
        {
            SelectedPage = "SpatialKit"
        };
        Window window = new()
        {
            Width = 1280,
            Height = 820,
            Content = new WorkbenchShellView(viewModel)
        };

        try
        {
            viewModel.SpatialKitPage.ApplyPeriodicState(Parse(
                "{\"schemaVersion\":1,\"version\":1,\"stats\":{\"activeIndexCount\":0},\"indexes\":[]}"));
            window.Show();
            Dispatcher.UIThread.RunJobs();
            SpatialKitPageView page = Assert.Single(
                window.GetVisualDescendants().OfType<SpatialKitPageView>(),
                static item => item.IsVisible);
            Grid? workspace = page.FindControl<Grid>("SpatialWorkspace");
            Assert.NotNull(workspace);
            Assert.False(workspace.IsVisible);
            Assert.Contains(
                page.GetVisualDescendants().OfType<TextBlock>(),
                static item => item.IsVisible && item.Text == "暂无运行中的 SpatialKit 索引");
            AssertNoHorizontalScrollBar(page);
            SaveFrame(window, 1280, 820, "empty");

            viewModel.SpatialKitPage.ApplyPeriodicState(Parse(
                "{\"schemaVersion\":1,\"version\":2,\"stats\":{\"activeIndexCount\":1},\"indexes\":[{\"diagnosticsId\":\"spatial-stale\",\"indexKind\":\"HashGrid\",\"entityTypeName\":\"Enemy\",\"count\":2,\"plane\":\"XZ\",\"cellSize\":2.5,\"partitionCount\":1}]}",
                "Telemetry generation changed; showing last accepted frame."));
            Dispatcher.UIThread.RunJobs();
            Assert.True(viewModel.SpatialKitPage.HasStaleReason);
            Assert.True(viewModel.SpatialKitPage.ShowDensityEmpty);
            Assert.Equal("session-1", viewModel.SpatialKitPage.SessionId);
            Assert.Equal(1L, viewModel.SpatialKitPage.Generation);
            Assert.Contains(
                page.GetVisualDescendants().OfType<TextBlock>(),
                static item => item.IsVisible && item.Text == "当前索引暂无密度数据");
            AssertNoHorizontalScrollBar(page);
            SaveFrame(window, 1280, 820, "stale-no-density");
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>在真实 Workbench Shell 中检查 SpatialKit 页面视觉树和布局约束。</summary>
    private static void AssertAdaptiveLayout(double width, double height)
    {
        WorkbenchShellViewModel viewModel = new(() => { }, _ => { }, (_, _) => Task.CompletedTask)
        {
            SelectedPage = "SpatialKit"
        };
        viewModel.SpatialKitPage.ApplyPeriodicState(CreateState(12));
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
            SpatialKitPageView page = Assert.Single(
                window.GetVisualDescendants().OfType<SpatialKitPageView>(),
                static item => item.IsVisible);
            Border? master = page.FindControl<Border>("SpatialIndexPanel");
            Border? detail = page.FindControl<Border>("SpatialDetailPanel");
            Border? heatmap = page.FindControl<Border>("HeatmapHost");
            ListBox? indexList = page.FindControl<ListBox>("SpatialIndexList");
            Assert.NotNull(master);
            Assert.NotNull(detail);
            Assert.NotNull(heatmap);
            Assert.NotNull(indexList);
            Assert.InRange(master.Bounds.Width, 270d, 390d);
            Assert.True(detail.Bounds.Width > master.Bounds.Width);
            Assert.True(heatmap.IsVisible);
            Assert.InRange(heatmap.Bounds.Width, 220d, detail.Bounds.Width);
            Assert.InRange(Math.Abs(heatmap.Bounds.Width - heatmap.Bounds.Height), 0d, 1d);
            Assert.True(indexList.GetVisualDescendants().OfType<ListBoxItem>().Count(static item => item.IsVisible) >= 6);
            Assert.True(page.Bounds.Width <= window.ClientSize.Width);

            AssertNoHorizontalScrollBar(page);

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

    /// <summary>断言页面没有可见横向滚动条，保证列表末列不会被覆盖。</summary>
    private static void AssertNoHorizontalScrollBar(SpatialKitPageView page)
    {
        ScrollBar[] horizontalScrollBars = page.GetVisualDescendants()
                .OfType<ScrollBar>()
                .Where(static item => item.IsVisible && item.Orientation == Orientation.Horizontal)
                .ToArray();
        Assert.Empty(horizontalScrollBars);
    }

    /// <summary>创建足量索引和密度单元，覆盖列表滚动与热力图主视觉。</summary>
    private static WorkbenchSpatialKitState CreateState(int indexCount, bool includeMissingDensity = false)
    {
        WorkbenchSpatialIndex[] indexes = Enumerable.Range(0, indexCount)
            .Select(index => CreateIndex(index, includeMissingDensity && index == 1))
            .ToArray();
        WorkbenchSpatialKitDataSource source = new(
            "unity-editor", "spatial-session", 4L, "PlayMode", DateTimeOffset.UtcNow,
            "telemetry", string.Empty, new[] { "Global\\YokiFrame.SpatialKit" }, string.Empty, "{}");
        return new WorkbenchSpatialKitState(
            source, 2L, indexCount, indexCount * 14, indexCount * 5, 2, indexCount - 2, 1,
            indexes, false);
    }

    /// <summary>创建单个空间索引及其固定分辨率密度投影。</summary>
    private static WorkbenchSpatialIndex CreateIndex(int index, bool omitDensity)
    {
        const int resolution = 16;
        int[] bins = Enumerable.Range(0, resolution * resolution)
            .Select(cell => cell % 19 == 0 ? index + 8 : (cell % 7 == 0 ? 1 : 0))
            .ToArray();
        int occupied = bins.Count(static count => count > 0);
        int maximum = bins.Max();
        WorkbenchSpatialDensity? density = omitDensity
            ? null
            : new WorkbenchSpatialDensity(
                "spatial-" + index, "Quadtree", "XZ", resolution,
                -32f, -32f, 32f, 32f, bins.Length, occupied, 0, 2, 3, maximum,
                bins, new[] { new WorkbenchSpatialHotspot(0, 0, maximum) });
        return new WorkbenchSpatialIndex(
            "spatial-" + index, index % 2 == 0 ? "Quadtree" : "HashGrid",
            "YokiFrame.Entities." + index, index * 14 + 6, index % 2 == 0 ? "XZ" : "XY",
            2.5f + index, 6, 4, index * 5 + 2, DateTimeOffset.UtcNow,
            new WorkbenchSpatialBounds2D(-32f, -32f, 64f, 64f), null, density);
    }

    /// <summary>保存 SpatialKit Headless 截图并拒绝空白输出。</summary>
    private static void SaveFrame(Window window, double width, double height, string? stateName = null)
    {
        using WriteableBitmap? frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        string outputDirectory = Path.Combine(
            WorkbenchContractTestFiles.FindWorkbenchRoot(), ".artifacts", "screenshots", "workbench");
        Directory.CreateDirectory(outputDirectory);
        string prefix = stateName == null ? "master-detail" : stateName;
        string fileName = "spatialkit-" + prefix + "-" + (int)width + "x" + (int)height + ".png";
        using FileStream stream = new(
            Path.Combine(outputDirectory, fileName), FileMode.Create, FileAccess.Write, FileShare.Read);
        frame.Save(stream);
        Assert.True(stream.Length > 1024, "SpatialKit Headless 截图内容为空或异常小。");
    }
}
