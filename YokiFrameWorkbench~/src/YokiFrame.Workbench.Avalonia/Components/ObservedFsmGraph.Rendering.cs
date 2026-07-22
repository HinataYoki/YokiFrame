using System.Globalization;
using Avalonia;
using Avalonia.Media;
using YokiFrame.Workbench.Avalonia.ViewModels.FsmKit;

namespace YokiFrame.Workbench.Avalonia.Components;

/// <summary>承载 ObservedFsmGraph 的保留式渲染快照实现。</summary>
public sealed partial class ObservedFsmGraph
{
    private static readonly DashStyle sLatestEdgeDash = new(new[] { 6.0, 4.0 }, 0.0);
    private static readonly Typeface sTitleTypeface = new(FontFamily.Default, FontStyle.Normal, FontWeight.SemiBold);
    private static readonly Typeface sBodyTypeface = new(FontFamily.Default, FontStyle.Normal, FontWeight.Normal);

    /// <summary>直接回放当前渲染快照，避免每次 Render 重新创建边、节点和文字对象。</summary>
    /// <param name="context">Avalonia 当前绘制上下文。</param>
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        mRenderSnapshot.Draw(context);
    }

    /// <summary>在画布尺寸或主题资源变化后同步重建快照。</summary>
    /// <param name="change">当前 Avalonia 属性变更。</param>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == BoundsProperty)
        {
            RebuildRenderSnapshot();
            InvalidateVisual();
            return;
        }

        if (IsRenderResourceProperty(change.Property))
        {
            RebuildRenderSnapshot();
        }
    }

    /// <summary>判断属性是否会改变渲染快照持有的主题资源。</summary>
    /// <param name="property">待检查的 Avalonia 属性。</param>
    /// <returns>属性变化是否要求重建快照。</returns>
    private static bool IsRenderResourceProperty(AvaloniaProperty property)
    {
        return property == BackgroundProperty
            || property == EdgeBrushProperty
            || property == AccentBrushProperty
            || property == NodeBackgroundProperty
            || property == CurrentNodeBackgroundProperty
            || property == BorderBrushProperty
            || property == StrongBorderBrushProperty
            || property == TextBrushProperty
            || property == MutedTextBrushProperty;
    }

    /// <summary>以当前原子模型、尺寸、缩放和主题资源替换完整渲染快照。</summary>
    private void RebuildRenderSnapshot()
    {
        if (mModel == default)
        {
            return;
        }

        var model = mModel;
        RenderResources resources = new(this);
        DrawingGroup snapshot = new();
        using (var context = snapshot.Open())
        {
            context.DrawRectangle(resources.Background, null, new Rect(Bounds.Size));
            using (context.PushTransform(Matrix.CreateScale(Zoom, Zoom)))
            {
                DrawEdges(context, model, resources);
                DrawNodes(context, model, resources);
            }
        }

        mRenderSnapshot = snapshot;
        RenderSnapshotRevision++;
    }

    /// <summary>按模型顺序写入全部转换边、方向箭头和聚合次数。</summary>
    /// <param name="context">快照录制上下文。</param>
    /// <param name="model">本次快照绑定的原子模型。</param>
    /// <param name="resources">本次快照复用的主题资源。</param>
    private static void DrawEdges(
        DrawingContext context,
        ObservedFsmGraphModel model,
        RenderResources resources)
    {
        for (var index = 0; index < model.Edges.Count; index++)
        {
            DrawEdge(context, model.Edges[index], resources);
        }
    }

    /// <summary>向快照写入单条直线、曲线或自环，并突出最近一次转换。</summary>
    private static void DrawEdge(
        DrawingContext context,
        ObservedFsmGraphEdge edge,
        RenderResources resources)
    {
        var pen = edge.IsLatest ? resources.LatestEdgePen : resources.EdgePen;
        var brush = edge.IsLatest ? resources.AccentBrush : resources.EdgeBrush;
        using (context.PushOpacity(edge.IsLatest ? 1.0 : 0.82))
        {
            if (edge.IsCurved)
            {
                context.DrawGeometry(null, pen, CreateCurve(edge));
            }
            else
            {
                context.DrawLine(pen, new Point(edge.StartX, edge.StartY), new Point(edge.EndX, edge.EndY));
            }

            DrawArrowHead(context, edge, brush);
            DrawEdgeLabel(context, edge, resources);
        }
    }

    /// <summary>创建边的三次 Bezier 几何；模型已经预先计算全部控制点。</summary>
    private static StreamGeometry CreateCurve(ObservedFsmGraphEdge edge)
    {
        StreamGeometry geometry = new();
        using (var geometryContext = geometry.Open())
        {
            geometryContext.BeginFigure(new Point(edge.StartX, edge.StartY), false);
            geometryContext.CubicBezierTo(
                new Point(edge.ControlPointX, edge.ControlPointY),
                new Point(edge.ControlPoint2X, edge.ControlPoint2Y),
                new Point(edge.EndX, edge.EndY));
        }

        return geometry;
    }

    /// <summary>沿边终点切线写入单一实心箭头，避免翼线形成视觉折角。</summary>
    private static void DrawArrowHead(DrawingContext context, ObservedFsmGraphEdge edge, IBrush brush)
    {
        var tip = new Point(edge.EndX, edge.EndY);
        var basePoint = new Point(
            tip.X - Math.Cos(edge.ArrowAngle) * 13.0,
            tip.Y - Math.Sin(edge.ArrowAngle) * 13.0);
        var perpendicularX = -Math.Sin(edge.ArrowAngle) * 5.0;
        var perpendicularY = Math.Cos(edge.ArrowAngle) * 5.0;
        StreamGeometry geometry = new();
        using (var geometryContext = geometry.Open())
        {
            geometryContext.BeginFigure(tip, true);
            geometryContext.LineTo(new Point(basePoint.X + perpendicularX, basePoint.Y + perpendicularY));
            geometryContext.LineTo(new Point(basePoint.X - perpendicularX, basePoint.Y - perpendicularY));
            geometryContext.EndFigure(true);
        }

        context.DrawGeometry(brush, null, geometry);
    }

    /// <summary>写入边的聚合次数标签，并保持与原布局相同的中心位置。</summary>
    private static void DrawEdgeLabel(
        DrawingContext context,
        ObservedFsmGraphEdge edge,
        RenderResources resources)
    {
        var labelRect = new Rect(edge.LabelLeft - 18.0, edge.LabelTop - 12.0, 36.0, 24.0);
        context.DrawRectangle(resources.NodeBackground, resources.BorderPen, new RoundedRect(labelRect, 10.0));
        var text = CreateText(
            edge.Count + " 次", sTitleTypeface, 10.0, resources.MutedTextBrush, 34.0);
        context.DrawText(text, new Point(
            labelRect.X + (labelRect.Width - text.Width) / 2.0,
            labelRect.Y + (labelRect.Height - text.Height) / 2.0));
    }

    /// <summary>按模型顺序写入状态节点，使当前状态始终位于边的上层。</summary>
    /// <param name="context">快照录制上下文。</param>
    /// <param name="model">本次快照绑定的原子模型。</param>
    /// <param name="resources">本次快照复用的主题资源。</param>
    private static void DrawNodes(
        DrawingContext context,
        ObservedFsmGraphModel model,
        RenderResources resources)
    {
        for (var index = 0; index < model.Nodes.Count; index++)
        {
            DrawNode(context, model.Nodes[index], resources);
        }
    }

    /// <summary>向快照写入单个状态节点的背景、边框、名称和辅助信息。</summary>
    private static void DrawNode(
        DrawingContext context,
        ObservedFsmGraphNode node,
        RenderResources resources)
    {
        var nodeRect = new Rect(
            node.Left,
            node.Top,
            ObservedFsmGraphMetrics.NODE_WIDTH,
            ObservedFsmGraphMetrics.NODE_HEIGHT);
        var fill = node.IsCurrent ? resources.CurrentNodeBackground : resources.NodeBackground;
        var pen = node.IsCurrent
            ? resources.AccentBorderPen
            : node.IsComposite ? resources.StrongBorderPen : resources.BorderPen;
        context.DrawRectangle(fill, pen, new RoundedRect(nodeRect, 7.0));

        var textWidth = ObservedFsmGraphMetrics.NODE_WIDTH - 20.0;
        var title = CreateText(node.Name, sTitleTypeface, 13.0, resources.TextBrush, textWidth);
        var metadata = CreateText(
            CreateNodeMetadata(node), sBodyTypeface, 10.0, resources.MutedTextBrush, textWidth);
        context.DrawText(title, new Point(node.Left + 10.0, node.Top + 7.0));
        context.DrawText(metadata, new Point(node.Left + 10.0, node.Top + 31.0));
    }

    /// <summary>创建单行省略的格式化文本，仅在渲染快照重建时执行。</summary>
    private static FormattedText CreateText(
        string value,
        Typeface typeface,
        double fontSize,
        IBrush brush,
        double maxWidth)
    {
        FormattedText text = new(
            value,
            CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            typeface,
            fontSize,
            brush)
        {
            MaxLineCount = 1,
            MaxTextWidth = maxWidth,
            Trimming = TextTrimming.CharacterEllipsis
        };
        return text;
    }

    /// <summary>创建节点的紧凑辅助文本，仅在模型变化后重建快照时分配。</summary>
    private static string CreateNodeMetadata(ObservedFsmGraphNode node)
    {
        var role = node.IsCurrent ? "当前状态 · " : string.Empty;
        if (node.IsComposite)
        {
            role += " · 复合";
        }

        return role + "进入 " + node.EntryCount + " 次";
    }

    /// <summary>保存一次快照共享的画刷和画笔，避免按边、按节点重复创建 Pen。</summary>
    private readonly struct RenderResources
    {
        /// <summary>从控件当前主题属性创建一次快照资源。</summary>
        public RenderResources(ObservedFsmGraph graph)
        {
            Background = graph.Background ?? Brushes.Transparent;
            EdgeBrush = graph.EdgeBrush ?? Brushes.Gray;
            AccentBrush = graph.AccentBrush ?? Brushes.DodgerBlue;
            NodeBackground = graph.NodeBackground ?? Brushes.Transparent;
            CurrentNodeBackground = graph.CurrentNodeBackground ?? Brushes.Transparent;
            TextBrush = graph.TextBrush ?? Brushes.White;
            MutedTextBrush = graph.MutedTextBrush ?? Brushes.Gray;
            var borderBrush = graph.BorderBrush ?? Brushes.Gray;
            var strongBorderBrush = graph.StrongBorderBrush ?? Brushes.DarkGray;
            EdgePen = CreateEdgePen(EdgeBrush, 2.0, null);
            LatestEdgePen = CreateEdgePen(AccentBrush, 3.0, sLatestEdgeDash);
            BorderPen = new Pen(borderBrush, 1.0);
            StrongBorderPen = new Pen(strongBorderBrush, 1.0);
            AccentBorderPen = new Pen(AccentBrush, 1.0);
        }

        /// <summary>获取画布背景画刷。</summary>
        public IBrush Background { get; }

        /// <summary>获取普通边画刷。</summary>
        public IBrush EdgeBrush { get; }

        /// <summary>获取强调边和当前节点画刷。</summary>
        public IBrush AccentBrush { get; }

        /// <summary>获取普通节点和标签背景画刷。</summary>
        public IBrush NodeBackground { get; }

        /// <summary>获取当前节点背景画刷。</summary>
        public IBrush CurrentNodeBackground { get; }

        /// <summary>获取节点标题文字画刷。</summary>
        public IBrush TextBrush { get; }

        /// <summary>获取辅助文字画刷。</summary>
        public IBrush MutedTextBrush { get; }

        /// <summary>获取普通转换边画笔。</summary>
        public Pen EdgePen { get; }

        /// <summary>获取最近转换边画笔。</summary>
        public Pen LatestEdgePen { get; }

        /// <summary>获取普通节点边框画笔。</summary>
        public Pen BorderPen { get; }

        /// <summary>获取复合节点边框画笔。</summary>
        public Pen StrongBorderPen { get; }

        /// <summary>获取当前节点边框画笔。</summary>
        public Pen AccentBorderPen { get; }

        /// <summary>创建具有统一端点和连接样式的边画笔。</summary>
        private static Pen CreateEdgePen(IBrush brush, double thickness, IDashStyle? dashStyle)
        {
            return new Pen(
                brush,
                thickness,
                dashStyle,
                PenLineCap.Round,
                PenLineJoin.Round,
                10.0);
        }
    }
}
