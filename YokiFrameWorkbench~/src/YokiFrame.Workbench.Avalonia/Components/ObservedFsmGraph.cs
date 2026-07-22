using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using YokiFrame.Workbench.Avalonia.ViewModels.FsmKit;

namespace YokiFrame.Workbench.Avalonia.Components;

/// <summary>
/// 以单个自绘 Control 渲染 FsmKit 已观测转换图，并提供滚轮、按钮缩放和拖拽平移。
/// </summary>
public sealed partial class ObservedFsmGraph : Control
{
    private const double MIN_ZOOM = 0.35;
    private const double MAX_ZOOM = 1.8;
    private const double ZOOM_STEP = 0.12;
    private ObservedFsmGraphModel mModel = ObservedFsmGraphModel.Empty;
    private PointerDragState? mPointerDragState;
    private Drawing mRenderSnapshot = new DrawingGroup();

    /// <summary>定义原子图快照的轻量可绑定属性；该属性不参与样式或动画。</summary>
    public static readonly DirectProperty<ObservedFsmGraph, ObservedFsmGraphModel> ModelProperty =
        AvaloniaProperty.RegisterDirect<ObservedFsmGraph, ObservedFsmGraphModel>(
            nameof(Model),
            static control => control.Model,
            static (control, value) => control.Model = value);

    /// <summary>定义图缩放比例的可绑定属性。</summary>
    public static readonly StyledProperty<double> ZoomProperty =
        AvaloniaProperty.Register<ObservedFsmGraph, double>(nameof(Zoom), 1.0);

    /// <summary>定义画布背景画刷。</summary>
    public static readonly StyledProperty<IBrush?> BackgroundProperty =
        AvaloniaProperty.Register<ObservedFsmGraph, IBrush?>(nameof(Background));

    /// <summary>定义历史边、箭头的常规画刷。</summary>
    public static readonly StyledProperty<IBrush?> EdgeBrushProperty =
        AvaloniaProperty.Register<ObservedFsmGraph, IBrush?>(nameof(EdgeBrush));

    /// <summary>定义最近转换和当前节点的强调画刷。</summary>
    public static readonly StyledProperty<IBrush?> AccentBrushProperty =
        AvaloniaProperty.Register<ObservedFsmGraph, IBrush?>(nameof(AccentBrush));

    /// <summary>定义普通节点背景画刷。</summary>
    public static readonly StyledProperty<IBrush?> NodeBackgroundProperty =
        AvaloniaProperty.Register<ObservedFsmGraph, IBrush?>(nameof(NodeBackground));

    /// <summary>定义当前节点背景画刷。</summary>
    public static readonly StyledProperty<IBrush?> CurrentNodeBackgroundProperty =
        AvaloniaProperty.Register<ObservedFsmGraph, IBrush?>(nameof(CurrentNodeBackground));

    /// <summary>定义普通节点和标签边框画刷。</summary>
    public static readonly StyledProperty<IBrush?> BorderBrushProperty =
        AvaloniaProperty.Register<ObservedFsmGraph, IBrush?>(nameof(BorderBrush));

    /// <summary>定义复合节点的强边框画刷。</summary>
    public static readonly StyledProperty<IBrush?> StrongBorderBrushProperty =
        AvaloniaProperty.Register<ObservedFsmGraph, IBrush?>(nameof(StrongBorderBrush));

    /// <summary>定义节点标题文字画刷。</summary>
    public static readonly StyledProperty<IBrush?> TextBrushProperty =
        AvaloniaProperty.Register<ObservedFsmGraph, IBrush?>(nameof(TextBrush));

    /// <summary>定义节点辅助信息和边计数文字画刷。</summary>
    public static readonly StyledProperty<IBrush?> MutedTextBrushProperty =
        AvaloniaProperty.Register<ObservedFsmGraph, IBrush?>(nameof(MutedTextBrush));

    /// <summary>注册缩放与主题画刷变化处理；模型变化由 DirectProperty setter 主动重绘。</summary>
    static ObservedFsmGraph()
    {
        ZoomProperty.Changed.AddClassHandler<ObservedFsmGraph>(static (control, args) =>
        {
            control.ApplyZoom((double)args.NewValue!);
            control.ZoomChanged?.Invoke(control, EventArgs.Empty);
        });
        AffectsRender<ObservedFsmGraph>(
            BackgroundProperty,
            EdgeBrushProperty,
            AccentBrushProperty,
            NodeBackgroundProperty,
            CurrentNodeBackgroundProperty,
            BorderBrushProperty,
            StrongBorderBrushProperty,
            TextBrushProperty,
            MutedTextBrushProperty);
    }

    /// <summary>创建可缩放、可拖拽且不创建节点子控件的状态流图。</summary>
    public ObservedFsmGraph()
    {
        ClipToBounds = true;
        IsHitTestVisible = true;
        PointerWheelChanged += HandlePointerWheelChanged;
        PointerPressed += HandlePointerPressed;
        PointerMoved += HandlePointerMoved;
        PointerReleased += HandlePointerReleased;
        PointerCaptureLost += HandlePointerCaptureLost;
        UpdateCanvasSize();
        RebuildRenderSnapshot();
    }

    /// <summary>当缩放比例变化时通知页面更新百分比文本。</summary>
    public event EventHandler? ZoomChanged;

    /// <summary>获取或设置节点、边与画布尺寸属于同一代的不可变图模型。</summary>
    public ObservedFsmGraphModel Model { get => mModel; set => CommitModel(value); }

    /// <summary>获取控件实际提交过的模型版本，仅用于内部回归测试和刷新诊断。</summary>
    internal int VisualRevision { get; private set; }

    /// <summary>获取已构建的渲染快照版本，仅用于回归测试确认 Render 不重复创建绘制对象。</summary>
    internal int RenderSnapshotRevision { get; private set; }

    /// <summary>获取或设置图缩放比例，范围为 35% 到 180%。</summary>
    public double Zoom { get => GetValue(ZoomProperty); set => SetValue(ZoomProperty, Math.Clamp(value, MIN_ZOOM, MAX_ZOOM)); }

    /// <summary>获取或设置画布背景画刷。</summary>
    public IBrush? Background { get => GetValue(BackgroundProperty); set => SetValue(BackgroundProperty, value); }

    /// <summary>获取或设置普通历史边画刷。</summary>
    public IBrush? EdgeBrush { get => GetValue(EdgeBrushProperty); set => SetValue(EdgeBrushProperty, value); }

    /// <summary>获取或设置最近转换和当前节点强调画刷。</summary>
    public IBrush? AccentBrush { get => GetValue(AccentBrushProperty); set => SetValue(AccentBrushProperty, value); }

    /// <summary>获取或设置普通节点背景画刷。</summary>
    public IBrush? NodeBackground { get => GetValue(NodeBackgroundProperty); set => SetValue(NodeBackgroundProperty, value); }

    /// <summary>获取或设置当前节点背景画刷。</summary>
    public IBrush? CurrentNodeBackground { get => GetValue(CurrentNodeBackgroundProperty); set => SetValue(CurrentNodeBackgroundProperty, value); }

    /// <summary>获取或设置普通边框画刷。</summary>
    public IBrush? BorderBrush { get => GetValue(BorderBrushProperty); set => SetValue(BorderBrushProperty, value); }

    /// <summary>获取或设置复合节点强边框画刷。</summary>
    public IBrush? StrongBorderBrush { get => GetValue(StrongBorderBrushProperty); set => SetValue(StrongBorderBrushProperty, value); }

    /// <summary>获取或设置节点标题文字画刷。</summary>
    public IBrush? TextBrush { get => GetValue(TextBrushProperty); set => SetValue(TextBrushProperty, value); }

    /// <summary>获取或设置节点辅助文字画刷。</summary>
    public IBrush? MutedTextBrush { get => GetValue(MutedTextBrushProperty); set => SetValue(MutedTextBrushProperty, value); }

    /// <summary>放大图画布。</summary>
    public void ZoomIn() => Zoom += ZOOM_STEP;

    /// <summary>缩小图画布。</summary>
    public void ZoomOut() => Zoom -= ZOOM_STEP;

    /// <summary>按当前 ScrollViewer 可视区域适配完整图，最大不超过 100%。</summary>
    public void FitToViewport()
    {
        var scrollViewer = this.FindAncestorOfType<ScrollViewer>();
        if (scrollViewer == null || Model.CanvasWidth <= 0.0 || Model.CanvasHeight <= 0.0)
        {
            Zoom = 1.0;
            return;
        }

        var availableWidth = Math.Max(1.0, scrollViewer.Bounds.Width - 24.0);
        var availableHeight = Math.Max(1.0, scrollViewer.Bounds.Height - 24.0);
        Zoom = Math.Min(1.0, Math.Min(
            availableWidth / Model.CanvasWidth,
            availableHeight / Model.CanvasHeight));
    }

    /// <summary>只在视觉语义变化时提交模型并使单个绘制 Control 失效。</summary>
    /// <param name="model">待提交的不可变图模型。</param>
    private void CommitModel(ObservedFsmGraphModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        if (mModel.SemanticallyEquals(model))
        {
            return;
        }

        SetAndRaise(ModelProperty, ref mModel, model);
        UpdateCanvasSize();
        VisualRevision++;
        RebuildRenderSnapshot();
        InvalidateVisual();
    }

    /// <summary>滚轮按固定步长缩放图画布。</summary>
    private void HandlePointerWheelChanged(object? sender, PointerWheelEventArgs eventArgs)
    {
        Zoom += eventArgs.Delta.Y > 0 ? ZOOM_STEP : -ZOOM_STEP;
        eventArgs.Handled = true;
    }

    /// <summary>按住鼠标左键开始拖拽，并使用稳定的 ScrollViewer 坐标系记录起点。</summary>
    private void HandlePointerPressed(object? sender, PointerPressedEventArgs eventArgs)
    {
        var scrollViewer = this.FindAncestorOfType<ScrollViewer>();
        if (scrollViewer == null)
        {
            return;
        }

        var point = eventArgs.GetCurrentPoint(scrollViewer);
        if (!point.Properties.IsLeftButtonPressed)
        {
            return;
        }

        mPointerDragState = new PointerDragState(scrollViewer, eventArgs.Pointer, point.Position, scrollViewer.Offset);
        eventArgs.Pointer.Capture(this);
        eventArgs.Handled = true;
    }

    /// <summary>在稳定视口坐标系中计算指针位移并更新滚动偏移。</summary>
    private void HandlePointerMoved(object? sender, PointerEventArgs eventArgs)
    {
        var dragState = mPointerDragState;
        if (dragState == null || !ReferenceEquals(dragState.Pointer, eventArgs.Pointer))
        {
            return;
        }

        var delta = eventArgs.GetPosition(dragState.ScrollViewer) - dragState.StartPosition;
        dragState.ScrollViewer.Offset = new Vector(
            Math.Max(0.0, dragState.StartOffset.X - delta.X),
            Math.Max(0.0, dragState.StartOffset.Y - delta.Y));
        eventArgs.Handled = true;
    }

    /// <summary>结束拖拽并释放指针捕获。</summary>
    private void HandlePointerReleased(object? sender, PointerReleasedEventArgs eventArgs)
    {
        if (mPointerDragState == null || !ReferenceEquals(mPointerDragState.Pointer, eventArgs.Pointer))
        {
            return;
        }

        eventArgs.Pointer.Capture(null);
        mPointerDragState = null;
        eventArgs.Handled = true;
    }

    /// <summary>指针意外失去捕获时清理拖拽状态。</summary>
    private void HandlePointerCaptureLost(object? sender, PointerCaptureLostEventArgs eventArgs)
    {
        mPointerDragState = null;
    }

    /// <summary>限制缩放范围，并同步滚动布局尺寸和绘制比例。</summary>
    private void ApplyZoom(double zoom)
    {
        var clampedZoom = Math.Clamp(zoom, MIN_ZOOM, MAX_ZOOM);
        if (Math.Abs(clampedZoom - zoom) > 0.0001)
        {
            SetCurrentValue(ZoomProperty, clampedZoom);
            return;
        }

        UpdateCanvasSize();
        RebuildRenderSnapshot();
        InvalidateVisual();
    }

    /// <summary>同步外层 ScrollViewer 观察到的缩放后画布尺寸。</summary>
    private void UpdateCanvasSize()
    {
        Width = Model.CanvasWidth * Zoom;
        Height = Model.CanvasHeight * Zoom;
    }

    /// <summary>保存一次拖拽使用的稳定视口、指针和起始偏移。</summary>
    private sealed class PointerDragState
    {
        /// <summary>创建拖拽状态。</summary>
        public PointerDragState(ScrollViewer scrollViewer, IPointer pointer, Point startPosition, Vector startOffset)
        {
            ScrollViewer = scrollViewer;
            Pointer = pointer;
            StartPosition = startPosition;
            StartOffset = startOffset;
        }

        /// <summary>获取拖拽开始时的稳定视口坐标系。</summary>
        public ScrollViewer ScrollViewer { get; }

        /// <summary>获取当前拖拽指针。</summary>
        public IPointer Pointer { get; }

        /// <summary>获取按下时的视口坐标。</summary>
        public Point StartPosition { get; }

        /// <summary>获取按下时的滚动偏移。</summary>
        public Vector StartOffset { get; }
    }
}
