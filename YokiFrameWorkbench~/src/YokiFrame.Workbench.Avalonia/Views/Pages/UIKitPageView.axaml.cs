using Avalonia;
using Avalonia.Controls;

namespace YokiFrame.Workbench.Avalonia.Views.Pages;

/// <summary>Unity UIKit Runtime 只读诊断页面视图。</summary>
public sealed partial class UIKitPageView : UserControl
{
    private const double WIDE_LAYOUT_WIDTH = 1120D;

    /// <summary>初始化 UIKit 页面并使用紧凑布局作为测量前的安全默认值。</summary>
    public UIKitPageView()
    {
        InitializeComponent();
        ApplyLayout(false);
    }

    /// <summary>按页面实际可用宽度切换三栏和上下布局，不缩放文字。</summary>
    /// <param name="sender">触发布局变化的 UIKit 页面。</param>
    /// <param name="args">包含最新页面尺寸的事件参数。</param>
    private void OnPageSizeChanged(object? sender, SizeChangedEventArgs args)
    {
        ApplyLayout(args.NewSize.Width >= WIDE_LAYOUT_WIDTH);
    }

    /// <summary>切换宽屏和紧凑视觉树，两个布局共享同一个 ViewModel 选择状态。</summary>
    /// <param name="isWide">是否使用常驻三栏布局。</param>
    private void ApplyLayout(bool isWide)
    {
        UIKitWideLayout.IsVisible = isWide;
        UIKitCompactLayout.IsVisible = !isWide;
        Classes.Set("uikit-wide", isWide);
        Classes.Set("uikit-compact", !isWide);
    }
}
