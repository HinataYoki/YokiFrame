using Avalonia.Controls;

namespace YokiFrame.Workbench.Avalonia.Views.Pages;

/// <summary>
/// 承载 EventKit Runtime 事件与活动时间线的只读页面。
/// </summary>
public sealed partial class EventKitPageView : UserControl
{
    private const double COMPACT_LAYOUT_WIDTH = 1200D;

    /// <summary>初始化 EventKit 页面组件。</summary>
    public EventKitPageView()
    {
        InitializeComponent();
        Classes.Add("eventkit-compact");
        SizeChanged += OnSizeChanged;
    }

    /// <summary>按页面实际可用宽度切换紧凑规格，保持字号不随窗口缩小。</summary>
    private void OnSizeChanged(object? sender, SizeChangedEventArgs args)
    {
        bool isCompact = args.NewSize.Width < COMPACT_LAYOUT_WIDTH;
        if (isCompact == Classes.Contains("eventkit-compact"))
        {
            return;
        }

        if (isCompact)
        {
            Classes.Add("eventkit-compact");
            return;
        }

        Classes.Remove("eventkit-compact");
    }
}
