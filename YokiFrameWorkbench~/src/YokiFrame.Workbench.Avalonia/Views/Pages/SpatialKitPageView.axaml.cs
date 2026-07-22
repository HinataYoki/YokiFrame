using Avalonia;
using Avalonia.Controls;

namespace YokiFrame.Workbench.Avalonia.Views.Pages;

/// <summary>SpatialKit Workbench 页面视图。</summary>
public sealed partial class SpatialKitPageView : UserControl
{
    private const double COMPACT_WIDTH = 1050D;

    /// <summary>创建 SpatialKit 页面并加载 XAML。</summary>
    public SpatialKitPageView()
    {
        InitializeComponent();
    }

    /// <summary>根据页面可用宽度切换紧凑样式，不缩放文字或功能控件。</summary>
    private void OnPageSizeChanged(object? sender, SizeChangedEventArgs args)
    {
        Classes.Set("spatialkit-compact", args.NewSize.Width < COMPACT_WIDTH);
    }

    /// <summary>保持热力图为方形，避免拉伸空间坐标并充分利用当前详情视口。</summary>
    private void OnHeatmapViewportSizeChanged(object? sender, SizeChangedEventArgs args)
    {
        double availableSize = Math.Floor(Math.Max(0D, Math.Min(args.NewSize.Width, args.NewSize.Height)));
        HeatmapHost.Width = availableSize;
        HeatmapHost.Height = availableSize;
    }
}
