using Avalonia.Controls;
using Avalonia.Interactivity;
using YokiFrame.Workbench.Avalonia.Components;

namespace YokiFrame.Workbench.Avalonia.Views.Pages;

/// <summary>
/// FsmKit 只读诊断页面的 XAML 宿主。
/// </summary>
public sealed partial class FsmKitPageView : UserControl
{
    /// <summary>
    /// 创建页面并加载编译后的 XAML。
    /// </summary>
    public FsmKitPageView()
    {
        InitializeComponent();
        ObservedGraph.ZoomChanged += HandleGraphZoomChanged;
    }

    /// <summary>处理图控件缩放变化并刷新百分比文本。</summary>
    private void HandleGraphZoomChanged(object? sender, EventArgs eventArgs)
    {
        GraphZoomLabel.Text = Math.Round(ObservedGraph.Zoom * 100.0) + "%";
    }

    /// <summary>响应缩小按钮。</summary>
    private void OnGraphZoomOut(object? sender, RoutedEventArgs eventArgs)
    {
        ObservedGraph.ZoomOut();
    }

    /// <summary>响应放大按钮。</summary>
    private void OnGraphZoomIn(object? sender, RoutedEventArgs eventArgs)
    {
        ObservedGraph.ZoomIn();
    }

    /// <summary>响应适应视口按钮。</summary>
    private void OnGraphFit(object? sender, RoutedEventArgs eventArgs)
    {
        ObservedGraph.FitToViewport();
    }
}
