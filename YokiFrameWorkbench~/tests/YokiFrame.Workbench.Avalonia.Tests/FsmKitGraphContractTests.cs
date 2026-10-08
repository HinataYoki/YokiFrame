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
    /// 验证状态节点按圆环等距分布，边使用节点边界而不是节点中心连接。
    /// </summary>
    [Fact]
    public void ApplyPeriodicState_多个状态_节点等距且边连接边界()
    {
        using FsmKitPageViewModel viewModel = new();
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
    public void Zoom_自由缩放_限制在百分之三十五到一百八十并更新宽度()
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
    public void ApplyPeriodicState_双向边和自环_分离曲线并保留自环()
    {
        using FsmKitPageViewModel viewModel = new();
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
    /// 验证 FsmKit 专页进入真实视觉树，且渲染帧非空。
    /// 截图只进入内存流，窗口关闭后释放 Shell 持有的静态订阅。
    /// </summary>
    [Fact]
    public async Task Show_FsmKit专页_进入视觉树且渲染帧非空()
    {
        InstallerHeadlessTestApplication.EnsureInitialized();
        await Dispatcher.UIThread.InvokeAsync(AssertFsmKitShellFrame);
    }

    /// <summary>
    /// 在 UI 线程显示 FsmKit 专页，断言布局和渲染帧后关闭窗口并释放 Shell。
    /// </summary>
    private static void AssertFsmKitShellFrame()
    {
        WorkbenchShellViewModel shell = CreateFsmKitShell();
        Window window = CreateFsmKitWindow(shell);
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            AssertFsmKitPageLayout(window);
            AssertRenderedFrameIsNotEmpty(window);
        }
        finally
        {
            window.Close();
            DisposeShell(shell);
        }
    }

    /// <summary>
    /// 创建已选中 FsmKit 并写入真实详情的 Headless Shell。
    /// </summary>
    /// <returns>尚未显示、由调用方负责释放的 Shell。</returns>
    private static WorkbenchShellViewModel CreateFsmKitShell()
    {
        WorkbenchShellViewModel viewModel = new(() => { }, _ => { }, (_, _) => Task.CompletedTask)
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
        return viewModel;
    }

    /// <summary>
    /// 创建承载指定 Shell 的 Headless 窗口。
    /// </summary>
    /// <param name="viewModel">已经写入 FsmKit 详情的 Shell。</param>
    /// <returns>尚未显示、可由测试控制生命周期的窗口。</returns>
    private static Window CreateFsmKitWindow(WorkbenchShellViewModel viewModel)
    {
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
    /// 把已布局窗口的渲染帧写入内存流，确认内容超过空图阈值。
    /// 不落盘，避免多个测试共享同一 PNG 路径。
    /// </summary>
    /// <param name="window">已经完成布局的 Workbench 窗口。</param>
    private static void AssertRenderedFrameIsNotEmpty(Window window)
    {
        using WriteableBitmap? frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        using MemoryStream stream = new();
        frame.Save(stream);
        Assert.True(stream.Length > 1024, "FsmKit Headless 截图内容为空或异常小。");
    }

    /// <summary>
    /// 释放 Shell 装配的页面和自身订阅。必须在窗口关闭后、创建 Shell 的 UI 线程调用。
    /// </summary>
    /// <param name="shell">测试创建的 Workbench Shell。</param>
    private static void DisposeShell(WorkbenchShellViewModel shell)
    {
        shell.EventKitPage.Dispose();
        shell.FsmKitPage.Dispose();
        shell.LogKitPage.Dispose();
        shell.PoolKitPage.Dispose();
        shell.ResKitPage.Dispose();
        shell.ActionKitPage.Dispose();
        shell.AudioKitPage.Dispose();
        shell.SpatialKitPage.Dispose();
        shell.UIKitPage.Dispose();
        shell.TableKitPage.Dispose();
        shell.LocalizationKitPage.Dispose();
        shell.SaveKitPage.Dispose();
        shell.DocumentationPage.Dispose();
        shell.RuntimeUpdate.Dispose();
        shell.Dispose();
    }
}