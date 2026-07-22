using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using YokiFrame.Workbench.Avalonia.Components;
using YokiFrame.Workbench.Avalonia.ViewModels;
using YokiFrame.Workbench.Avalonia.ViewModels.FsmKit;
using YokiFrame.Workbench.Avalonia.Views;

namespace YokiFrame.Workbench.Avalonia.Tests;

/// <summary>
/// 覆盖 FsmKit 工作区布局、观测图几何、缩放能力和真实 Headless 渲染。
/// </summary>
public sealed class FsmKitGraphContractTests
{
    /// <summary>
    /// 验证 FsmKit 页面复用其它页面的两行紧凑标题栏，不展示重复宿主信息或刷新控件。
    /// </summary>
    [Fact]
    public void FsmKitPageUsesSharedCompactHeader()
    {
        var xaml = WorkbenchContractTestFiles.ReadSource("Views", "Pages", "FsmKitPageView.axaml");
        var shellXaml = WorkbenchContractTestFiles.ReadSource("Views", "WorkbenchShellView.axaml");
        var windowSource = WorkbenchContractTestFiles.ReadSource("WorkbenchWindow.cs");
        var telemetrySource = WorkbenchContractTestFiles.ReadSource("WorkbenchWindow.FsmTelemetry.cs");

        Assert.Contains("CurrentPageTitle", shellXaml);
        Assert.Contains("CurrentPageDescription", shellXaml);
        Assert.Contains("StaleReason", xaml);
        Assert.DoesNotContain("FsmKitPage.EngineId", shellXaml);
        Assert.DoesNotContain("FsmKitPage.SessionId", shellXaml);
        Assert.DoesNotContain("实时状态", shellXaml);
        Assert.DoesNotContain("workbench.fsm.pause", shellXaml);
        Assert.Contains("SharedMemoryRefreshInterval = TimeSpan.FromMilliseconds(100)", telemetrySource);
        Assert.Contains("FileRefreshInterval = TimeSpan.FromSeconds(1)", windowSource);
        Assert.Contains("PollFsmKitTelemetry", telemetrySource);
    }

    /// <summary>
    /// 验证 FsmKit 页面使用左侧实例列表、大尺寸状态流图和右侧转换历史。
    /// </summary>
    [Fact]
    public void FsmKitPageUsesTypedRuntimeWorkspace()
    {
        var xaml = WorkbenchContractTestFiles.ReadSource("Views", "Pages", "FsmKitPageView.axaml");
        var shellXaml = WorkbenchContractTestFiles.ReadSource("Views", "WorkbenchShellView.axaml");
        var graphSource = WorkbenchContractTestFiles.ReadSource("Components", "ObservedFsmGraph.cs");
        var graphRenderingSource = WorkbenchContractTestFiles.ReadSource(
            "Components", "ObservedFsmGraph.Rendering.cs");
        var graphStyles = WorkbenchContractTestFiles.ReadSource("Styles", "FsmKit.axaml");

        Assert.Contains("转换历史", xaml);
        Assert.Contains("ColumnDefinitions=\"252,*,300\"", xaml);
        Assert.Contains("SearchText", xaml);
        Assert.Contains("AutomationProperties.Name=\"搜索活动状态机\"", xaml);
        Assert.Contains("AutomationProperties.Name=\"活动状态机列表\"", xaml);
        Assert.Contains("SelectedMachineName", xaml);
        Assert.Contains("MachineState", xaml);
        Assert.Contains("CurrentState", xaml);
        Assert.Contains("ToolTip.Tip=\"{CompiledBinding InstanceId}\"", xaml);
        Assert.DoesNotContain("Text=\"{CompiledBinding InstanceId}\"", xaml);
        Assert.DoesNotContain("FsmKitPage.DataChannelText", shellXaml);
        Assert.Contains("ListBoxItem:selected /template/ ContentPresenter", graphStyles);
        Assert.Contains("Transitions", xaml);
        Assert.Contains("已观测转换图", xaml);
        Assert.Contains("Model=\"{CompiledBinding GraphModel}\"", xaml);
        Assert.Contains("AutomationProperties.Name=\"已观测状态转换图\"", xaml);
        Assert.Contains("AutomationProperties.LiveSetting=\"Polite\"", xaml);
        Assert.Contains("OnGraphZoomOut", xaml);
        Assert.Contains("OnGraphZoomIn", xaml);
        Assert.Contains("OnGraphFit", xaml);
        Assert.Contains("HandlePointerPressed", graphSource);
        Assert.Contains("HandlePointerMoved", graphSource);
        Assert.Contains("scrollViewer.Offset", graphSource);
        Assert.Contains("DashStyle", graphRenderingSource);
        Assert.Contains("DrawArrowHead", graphRenderingSource);
        Assert.Contains("DrawGeometry", graphRenderingSource);
        Assert.Contains("components|ObservedFsmGraph", graphStyles);
        Assert.Contains("AccentBrush", graphStyles);
        Assert.DoesNotContain("Children.Add", graphSource);
        Assert.Contains("EmptyStateTitle", xaml);
        Assert.DoesNotContain("<TabControl", xaml);
        Assert.DoesNotContain("<TabItem Header=\"诊断\"", xaml);
        Assert.DoesNotContain("<TabItem Header=\"API\"", xaml);
        Assert.DoesNotContain("原始数据", xaml);
        Assert.DoesNotContain("RawPayload", xaml);
        Assert.DoesNotContain("EvidencePaths", xaml);
        Assert.DoesNotContain("ChangeStateCommand", xaml);
        Assert.DoesNotContain("事件洞察", xaml);
        Assert.DoesNotContain("StateTree", xaml);
        Assert.DoesNotContain("StateEvents", xaml);
    }

    /// <summary>
    /// 验证状态节点按圆环等距分布，边使用节点边界而不是节点中心连接。
    /// </summary>
    [Fact]
    public void FsmKitGraphUsesCircularNodeLayoutAndBoundaryEdges()
    {
        FsmKitPageViewModel viewModel = new();
        viewModel.ApplyPeriodicState(FsmKitContractTestData.CreateState(
            "chosen-instance",
            "snapshot",
            "{}",
            "F:/Project/fsm-state.json",
            "Ready"));

        Assert.Equal(3, viewModel.GraphModel.Nodes.Count);
        var centerX = viewModel.GraphModel.CanvasWidth / 2.0;
        var centerY = viewModel.GraphModel.CanvasHeight / 2.0;
        var radii = viewModel.GraphModel.Nodes
            .Select(node => Math.Sqrt(
                Math.Pow(node.CenterX - centerX, 2) + Math.Pow(node.CenterY - centerY, 2)))
            .ToArray();
        Assert.All(radii, radius => Assert.Equal(radii[0], radius, precision: 5));
        var edge = Assert.Single(viewModel.GraphModel.Edges);
        var source = viewModel.GraphModel.Nodes.Single(node => node.Name == edge.From);
        var target = viewModel.GraphModel.Nodes.Single(node => node.Name == edge.To);
        Assert.True(Math.Abs(source.CenterX - edge.StartX) + Math.Abs(source.CenterY - edge.StartY) > 1.0);
        Assert.True(Math.Abs(target.CenterX - edge.EndX) + Math.Abs(target.CenterY - edge.EndY) > 1.0);
    }

    /// <summary>
    /// 验证图控件支持 35% 到 180% 的缩放范围，并同步外层滚动尺寸。
    /// </summary>
    [Fact]
    public void ObservedFsmGraphSupportsFreeZoom()
    {
        ObservedFsmGraph graph = new()
        {
            Model = new ObservedFsmGraphModel(
                Array.Empty<ObservedFsmGraphNode>(),
                Array.Empty<ObservedFsmGraphEdge>(),
                600,
                400)
        };

        graph.ZoomOut();
        Assert.Equal(528, graph.Width, precision: 5);
        Assert.Equal(0.88, graph.Zoom, precision: 5);
        graph.Zoom = 10;
        Assert.Equal(1.8, graph.Zoom, precision: 5);
        graph.Zoom = 0;
        Assert.Equal(0.35, graph.Zoom, precision: 5);
    }

    /// <summary>
    /// 验证双向转换分离为曲线，自环保留独立回环几何。
    /// </summary>
    [Fact]
    public void FsmKitGraphSeparatesReverseEdgesAndKeepsSelfLoops()
    {
        FsmKitPageViewModel viewModel = new();
        viewModel.ApplyPeriodicState(FsmKitContractTestData.CreateState(
            "chosen-instance",
            "snapshot",
            "{}",
            "F:/Project/fsm-state.json",
            "Ready",
            transitions:
            [
                ("Start", "Ready"),
                ("Ready", "Start"),
                ("Ready", "Ready")
            ]));

        Assert.Equal(3, viewModel.GraphModel.Edges.Count);
        Assert.All(
            viewModel.GraphModel.Edges.Where(edge => !edge.IsSelfLoop),
            edge => Assert.True(edge.IsCurved));
        var selfLoop = Assert.Single(viewModel.GraphModel.Edges, edge => edge.IsSelfLoop);
        Assert.True(selfLoop.IsCurved);
        Assert.True(selfLoop.IsLatest);
    }

    /// <summary>
    /// 通过 Headless 真实加载 Shell，验证 FsmKit 专页进入视觉树而非通用 JSON 页面。
    /// </summary>
    [Fact]
    public async Task FsmKitPageRendersInsideHeadlessShell()
    {
        InstallerHeadlessTestApplication.EnsureInitialized();
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var window = CreateFsmKitWindow();
            try
            {
                window.Show();
                Dispatcher.UIThread.RunJobs();
                AssertFsmKitPageLayout(window);
                SaveFsmKitFrame(window);
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>
    /// 创建带真实 FsmKit 数据的 Headless Shell 窗口。
    /// </summary>
    /// <returns>尚未显示、可由测试控制生命周期的窗口。</returns>
    private static Window CreateFsmKitWindow()
    {
        var viewModel = new WorkbenchShellViewModel(() => { }, _ => { }, (_, _) => Task.CompletedTask)
        {
            SelectedPage = "FsmKit"
        };
        viewModel.FsmKitPage.ApplyPeriodicState(FsmKitContractTestData.CreateState(
            "chosen-instance",
            "snapshot",
            "{\"active\":true}",
            "F:/Project/fsm-state.json",
            "Ready",
            transitions:
            [
                ("Start", "Ready"),
                ("Ready", "Start"),
                ("Ready", "ChosenState")
            ]));
        return new Window
        {
            Width = 1600,
            Height = 980,
            Content = new WorkbenchShellView(viewModel)
        };
    }

    /// <summary>
    /// 断言真实视觉树中的页面、公共页头和观测图均完成有效布局。
    /// </summary>
    /// <param name="window">已经显示并完成布局的 Headless 窗口。</param>
    private static void AssertFsmKitPageLayout(Window window)
    {
        var page = window.GetVisualDescendants()
            .OfType<Control>()
            .FirstOrDefault(control => string.Equals(
                AutomationProperties.GetAutomationId(control),
                "workbench.fsm.page",
                StringComparison.Ordinal));

        Assert.NotNull(page);
        Assert.True(page.Bounds.Width > 0);
        Assert.True(page.Bounds.Height > 0);
        var pageHeader = Assert.Single(
            window.GetVisualDescendants().OfType<Border>(),
            static border => border.Classes.Contains("page-header"));
        Assert.InRange(pageHeader.Bounds.Height, 44, 80);
        var graph = Assert.Single(window.GetVisualDescendants().OfType<ObservedFsmGraph>());
        Assert.NotEmpty(graph.Model.Nodes);
        Assert.NotEmpty(graph.Model.Edges);
        Assert.True(graph.Model.CanvasWidth >= 520);
        Assert.True(graph.Model.CanvasHeight >= 420);
        Assert.Equal(graph.Model.CanvasWidth, graph.Width, precision: 5);
        Assert.Equal(graph.Model.CanvasHeight, graph.Height, precision: 5);
    }

    /// <summary>
    /// 保存含真实节点和边的 FsmKit Headless 渲染帧，供视觉回归和人工审阅使用。
    /// </summary>
    /// <param name="window">已经完成布局的 Workbench 窗口。</param>
    private static void SaveFsmKitFrame(Window window)
    {
        using WriteableBitmap? frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        var root = WorkbenchContractTestFiles.FindWorkbenchRoot();
        var outputDirectory = Path.Combine(root, ".artifacts", "screenshots", "fsmkit");
        Directory.CreateDirectory(outputDirectory);
        var outputPath = Path.Combine(outputDirectory, "observed-graph-1600x980.png");
        using FileStream stream = new(outputPath, FileMode.Create, FileAccess.Write, FileShare.Read);
        frame.Save(stream);
        Assert.True(stream.Length > 1024, "FsmKit Headless 截图内容为空或异常小。");
    }
}
