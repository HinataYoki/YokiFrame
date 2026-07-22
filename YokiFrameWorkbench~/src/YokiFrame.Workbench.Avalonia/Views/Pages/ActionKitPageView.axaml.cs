using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace YokiFrame.Workbench.Avalonia.Views.Pages;

/// <summary>承载 ActionKit 活动树与终态诊断专用布局。</summary>
public partial class ActionKitPageView : UserControl
{
    private const double WIDE_LAYOUT_WIDTH = 1240D;
    private const double COLLAPSED_DRAWER_HEIGHT = 32D;
    private const double MIN_EXPANDED_DRAWER_HEIGHT = 180D;
    private const double DEFAULT_EXPANDED_DRAWER_HEIGHT = 220D;
    private const double MAX_EXPANDED_DRAWER_HEIGHT = 300D;
    private double mExpandedDrawerHeight = DEFAULT_EXPANDED_DRAWER_HEIGHT;

    /// <summary>初始化 ActionKit 页面组件。</summary>
    public ActionKitPageView()
    {
        InitializeComponent();
        SizeChanged += OnSizeChanged;
        SetWideLayout(false);
        SetDrawerExpanded(false);
    }

    /// <summary>按页面实际可用宽度切换常驻 Inspector，保证紧凑窗口优先展示执行树。</summary>
    /// <param name="sender">触发布局变化的 ActionKit 页面。</param>
    /// <param name="args">包含最新页面尺寸的事件参数。</param>
    private void OnSizeChanged(object? sender, SizeChangedEventArgs args)
    {
        SetWideLayout(args.NewSize.Width >= WIDE_LAYOUT_WIDTH);
    }

    /// <summary>响应抽屉切换按钮并在折叠前保留用户调整后的展开高度。</summary>
    /// <param name="sender">抽屉切换按钮。</param>
    /// <param name="args">按钮选中状态变化事件。</param>
    private void OnDrawerToggleChanged(object? sender, RoutedEventArgs args)
    {
        SetDrawerExpanded(DrawerToggleButton.IsChecked == true);
    }

    /// <summary>应用紧凑或宽屏列结构，并在节点详情迁移后修正当前抽屉页签。</summary>
    /// <param name="isWide">是否达到可常驻 Inspector 的页面宽度。</param>
    private void SetWideLayout(bool isWide)
    {
        Classes.Set("actionkit-wide", isWide);
        Classes.Set("actionkit-compact", !isWide);
        ColumnDefinition inspectorSplitterColumn = ActionWorkspace.ColumnDefinitions[3];
        ColumnDefinition inspectorColumn = ActionWorkspace.ColumnDefinitions[4];
        inspectorSplitterColumn.Width = new GridLength(isWide ? 8D : 0D);
        inspectorColumn.MinWidth = isWide ? 240D : 0D;
        inspectorColumn.MaxWidth = isWide ? 360D : 0D;
        inspectorColumn.Width = new GridLength(isWide ? 280D : 0D);
        InspectorSplitter.IsVisible = isWide;
        InspectorPanel.IsVisible = isWide;
        CompactInspectorTab.IsVisible = !isWide;
        if (isWide && ReferenceEquals(DrawerTabs.SelectedItem, CompactInspectorTab))
        {
            DrawerTabs.SelectedItem = StackTraceTab;
        }
    }

    /// <summary>在 32px 页签条和 180-300px 可拖拽诊断区之间切换。</summary>
    /// <param name="isExpanded">是否展开诊断抽屉。</param>
    private void SetDrawerExpanded(bool isExpanded)
    {
        if (!isExpanded && DrawerPanel.Bounds.Height >= MIN_EXPANDED_DRAWER_HEIGHT)
        {
            mExpandedDrawerHeight = Math.Clamp(
                DrawerPanel.Bounds.Height,
                MIN_EXPANDED_DRAWER_HEIGHT,
                MAX_EXPANDED_DRAWER_HEIGHT);
        }

        Classes.Set("actionkit-drawer-expanded", isExpanded);
        RowDefinition drawerSplitterRow = RootLayout.RowDefinitions[2];
        RowDefinition drawerRow = RootLayout.RowDefinitions[3];
        drawerSplitterRow.Height = new GridLength(isExpanded ? 5D : 0D);
        drawerRow.MinHeight = isExpanded ? MIN_EXPANDED_DRAWER_HEIGHT : COLLAPSED_DRAWER_HEIGHT;
        drawerRow.MaxHeight = isExpanded ? MAX_EXPANDED_DRAWER_HEIGHT : COLLAPSED_DRAWER_HEIGHT;
        drawerRow.Height = new GridLength(
            isExpanded
                ? Math.Clamp(mExpandedDrawerHeight, MIN_EXPANDED_DRAWER_HEIGHT, MAX_EXPANDED_DRAWER_HEIGHT)
                : COLLAPSED_DRAWER_HEIGHT);
    }
}
