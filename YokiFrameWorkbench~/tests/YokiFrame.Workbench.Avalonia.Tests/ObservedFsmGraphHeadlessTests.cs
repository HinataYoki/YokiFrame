using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using YokiFrame.Workbench.Avalonia.Components;
using YokiFrame.Workbench.Avalonia.ViewModels.FsmKit;

namespace YokiFrame.Workbench.Avalonia.Tests;

/// <summary>
/// 覆盖 FsmKit 观测图模型的不可变语义，以及单控件绘制的原子刷新约束。
/// </summary>
public sealed class ObservedFsmGraphHeadlessTests
{
    /// <summary>
    /// 验证模型会持有集合副本，并能识别引用不同但节点、边和尺寸完全相同的快照。
    /// </summary>
    [Fact]
    public void GraphModelOwnsCollectionsAndUsesVisualSemanticEquality()
    {
        List<ObservedFsmGraphNode> nodes = new() { CreateNode("Idle", isCurrent: true) };
        List<ObservedFsmGraphEdge> edges = new() { CreateEdge(isLatest: true) };
        ObservedFsmGraphModel model = new(nodes, edges, 640.0, 480.0);
        ObservedFsmGraphModel equivalent = new(
            new[] { CreateNode("Idle", isCurrent: true) },
            new[] { CreateEdge(isLatest: true) },
            640.0,
            480.0);

        nodes.Clear();
        edges.Clear();

        Assert.Single(model.Nodes);
        Assert.Single(model.Edges);
        Assert.True(model.SemanticallyEquals(equivalent));
        Assert.Equal(model.GetHashCode(), equivalent.GetHashCode());
        Assert.False(model.SemanticallyEquals(new ObservedFsmGraphModel(
            new[] { CreateNode("Idle", isCurrent: false) },
            equivalent.Edges,
            640.0,
            480.0)));
        Assert.False(model.SemanticallyEquals(new ObservedFsmGraphModel(
            equivalent.Nodes,
            new[] { CreateEdge(isLatest: false) },
            640.0,
            480.0)));
        Assert.False(model.SemanticallyEquals(new ObservedFsmGraphModel(
            equivalent.Nodes,
            equivalent.Edges,
            641.0,
            480.0)));
    }

    /// <summary>
    /// 验证首次模型只产生一次绘制提交，附着窗口和等价模型均不会重复失效。
    /// </summary>
    [Fact]
    public async Task EquivalentModelAndVisualAttachmentDoNotAdvanceRevision()
    {
        InstallerHeadlessTestApplication.EnsureInitialized();
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var graph = new ObservedFsmGraph { Model = CreateModel(isCurrent: true, isLatest: true) };
            var firstModel = graph.Model;
            Window window = new() { Width = 800, Height = 600, Content = graph };
            try
            {
                Assert.Equal(1, GetVisualRevision(graph));
                window.Show();
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(1, GetVisualRevision(graph));
                var snapshotRevision = GetRenderSnapshotRevision(graph);

                graph.Model = CreateModel(isCurrent: true, isLatest: true);
                graph.InvalidateVisual();
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(1, GetVisualRevision(graph));
                Assert.Equal(snapshotRevision, GetRenderSnapshotRevision(graph));
                Assert.Same(firstModel, graph.Model);

                DrawingGroup replay = new();
                using (var context = replay.Open())
                {
                    graph.Render(context);
                }

                Assert.Equal(snapshotRevision, GetRenderSnapshotRevision(graph));
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>
    /// 验证一份语义变化模型只使单个绘制控件提交一次，并同步节点、边和画布尺寸。
    /// </summary>
    [Fact]
    public async Task ChangedModelPerformsOneAtomicVisualCommit()
    {
        InstallerHeadlessTestApplication.EnsureInitialized();
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var graph = new ObservedFsmGraph { Model = CreateModel(isCurrent: true, isLatest: false) };
            var previousRevision = GetVisualRevision(graph);
            var previousSnapshotRevision = GetRenderSnapshotRevision(graph);
            var changed = CreateModel(isCurrent: false, isLatest: true, width: 720.0, height: 540.0);

            graph.Model = changed;

            Assert.Equal(previousRevision + 1, GetVisualRevision(graph));
            Assert.Equal(previousSnapshotRevision + 1, GetRenderSnapshotRevision(graph));
            Assert.Same(changed, graph.Model);
            Assert.Equal(720.0, graph.Width);
            Assert.Equal(540.0, graph.Height);
            Assert.Single(graph.Model.Nodes);
            Assert.Single(graph.Model.Edges);
        });
    }

    /// <summary>
    /// 验证主题画刷和实际布局尺寸变化会重建快照，使缓存不会保留过期颜色或背景范围。
    /// </summary>
    [Fact]
    public async Task ThemeAndBoundsChangesRebuildRenderSnapshot()
    {
        InstallerHeadlessTestApplication.EnsureInitialized();
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var graph = new ObservedFsmGraph { Model = CreateModel(isCurrent: true, isLatest: true) };
            var initialRevision = GetRenderSnapshotRevision(graph);

            graph.AccentBrush = Brushes.Orange;

            Assert.Equal(initialRevision + 1, GetRenderSnapshotRevision(graph));
            var themedRevision = GetRenderSnapshotRevision(graph);
            graph.Arrange(new Rect(0.0, 0.0, 640.0, 480.0));
            Assert.True(GetRenderSnapshotRevision(graph) > themedRevision);
        });
    }

    /// <summary>
    /// 创建一份包含单节点、单转换边和显式画布尺寸的测试模型。
    /// </summary>
    /// <param name="isCurrent">节点是否处于当前状态。</param>
    /// <param name="isLatest">转换边是否为最近一次转换。</param>
    /// <param name="width">画布宽度。</param>
    /// <param name="height">画布高度。</param>
    /// <returns>内容确定的不可变测试模型。</returns>
    private static ObservedFsmGraphModel CreateModel(
        bool isCurrent,
        bool isLatest,
        double width = 640.0,
        double height = 480.0)
    {
        return new ObservedFsmGraphModel(
            new[] { CreateNode("Idle", isCurrent) },
            new[] { CreateEdge(isLatest) },
            width,
            height);
    }

    /// <summary>
    /// 创建业务信息和布局坐标固定的测试节点。
    /// </summary>
    /// <param name="name">节点名称。</param>
    /// <param name="isCurrent">节点是否处于当前状态。</param>
    /// <returns>可用于模型语义比较的节点。</returns>
    private static ObservedFsmGraphNode CreateNode(string name, bool isCurrent)
    {
        return new ObservedFsmGraphNode(
            name,
            isCurrent,
            isComposite: false,
            entryCount: 2L,
            orderIndex: 0,
            enumId: 1,
            left: 100.0,
            top: 80.0,
            centerX: 168.0,
            centerY: 109.0,
            angle: 0.0);
    }

    /// <summary>
    /// 创建业务信息和曲线几何固定的测试转换边。
    /// </summary>
    /// <param name="isLatest">转换边是否为最近一次转换。</param>
    /// <returns>可用于模型语义比较的转换边。</returns>
    private static ObservedFsmGraphEdge CreateEdge(bool isLatest)
    {
        return new ObservedFsmGraphEdge(
            "Idle",
            "Idle",
            count: 1,
            startX: 168.0,
            startY: 80.0,
            endX: 180.0,
            endY: 82.0,
            labelLeft: 168.0,
            labelTop: 48.0,
            isCurved: true,
            isSelfLoop: true,
            controlPointX: 140.0,
            controlPointY: 32.0,
            controlPoint2X: 196.0,
            controlPoint2Y: 34.0,
            arrowAngle: 0.9,
            isLatest: isLatest);
    }

    /// <summary>
    /// 读取控件内部视觉版本，避免为测试计数器扩大正式公共 API。
    /// </summary>
    /// <param name="graph">待检查的图控件。</param>
    /// <returns>控件已提交的视觉版本号。</returns>
    private static int GetVisualRevision(ObservedFsmGraph graph)
    {
        var property = typeof(ObservedFsmGraph).GetProperty(
            "VisualRevision",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(property);
        return Assert.IsType<int>(property.GetValue(graph));
    }

    /// <summary>
    /// 读取控件内部渲染快照版本，用于确认等价模型和重复 Render 不会重新分配绘制对象。
    /// </summary>
    /// <param name="graph">待检查的图控件。</param>
    /// <returns>控件已构建的渲染快照版本号。</returns>
    private static int GetRenderSnapshotRevision(ObservedFsmGraph graph)
    {
        var property = typeof(ObservedFsmGraph).GetProperty(
            "RenderSnapshotRevision",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(property);
        return Assert.IsType<int>(property.GetValue(graph));
    }
}
