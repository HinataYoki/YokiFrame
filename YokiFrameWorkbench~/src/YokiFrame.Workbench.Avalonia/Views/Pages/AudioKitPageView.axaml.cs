using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

using YokiFrame.Workbench.Avalonia.ViewModels;

namespace YokiFrame.Workbench.Avalonia.Views.Pages;

/// <summary>承载 AudioKit Bus 观察、播放详情和稳定索引抽屉。</summary>
public partial class AudioKitPageView : UserControl
{
    private const double WIDE_LAYOUT_WIDTH = 1240D;
    private const double COLLAPSED_DRAWER_HEIGHT = 32D;
    private const double MIN_EXPANDED_DRAWER_HEIGHT = 180D;
    private const double DEFAULT_EXPANDED_DRAWER_HEIGHT = 220D;
    private const double MAX_EXPANDED_DRAWER_HEIGHT = 300D;
    private double mExpandedDrawerHeight = DEFAULT_EXPANDED_DRAWER_HEIGHT;
    private bool mIsWideLayout;

    /// <summary>初始化页面并先应用紧凑布局，避免首次布局时出现横向溢出。</summary>
    public AudioKitPageView()
    {
        InitializeComponent();
        SizeChanged += OnSizeChanged;
        SetResponsiveLayout(false);
        SetIndexDrawerExpanded(false);
    }

    /// <summary>按页面实际可用宽度切换详情区和 Bus 区规格，字号保持不变。</summary>
    /// <param name="sender">触发布局变化的 AudioKit 页面。</param>
    /// <param name="args">包含最新页面尺寸的事件参数。</param>
    private void OnSizeChanged(object? sender, SizeChangedEventArgs args)
    {
        SetResponsiveLayout(args.NewSize.Width >= WIDE_LAYOUT_WIDTH);
    }

    /// <summary>切换窄窗口 Tab 详情与宽屏双栏详情，并为 Bus 区设置稳定高度预算。</summary>
    /// <param name="isWide">页面宽度是否足以同时展示 Voice 与 History。</param>
    private void SetResponsiveLayout(bool isWide)
    {
        mIsWideLayout = isWide;
        Classes.Set("audiokit-wide", isWide);
        Classes.Set("audiokit-compact", !isWide);
        NarrowDetailTabs.IsVisible = !isWide;
        WideDetailLayout.IsVisible = isWide;
        ApplyMainAreaHeightBudget(IndexDrawerToggleButton.IsChecked == true);

        if (IndexDrawerToggleButton.IsChecked == true)
        {
            RootLayout.RowDefinitions[6].Height = new GridLength(
                isWide
                    ? Math.Clamp(mExpandedDrawerHeight, MIN_EXPANDED_DRAWER_HEIGHT, MAX_EXPANDED_DRAWER_HEIGHT)
                    : MIN_EXPANDED_DRAWER_HEIGHT);
        }
    }

    /// <summary>为 Bus 区和详情区分配高度，紧凑窗口展开抽屉时仍保留可用明细。</summary>
    /// <param name="isDrawerExpanded">稳定索引抽屉是否占用主视口高度。</param>
    private void ApplyMainAreaHeightBudget(bool isDrawerExpanded)
    {
        bool useCompactExpandedBudget = isDrawerExpanded && !mIsWideLayout;
        RowDefinition busRow = RootLayout.RowDefinitions[2];
        busRow.MinHeight = 160D;
        busRow.MaxHeight = 320D;
        busRow.Height = new GridLength(
            useCompactExpandedBudget ? 180D : mIsWideLayout ? 240D : 220D);
        RootLayout.RowDefinitions[4].MinHeight = useCompactExpandedBudget ? 110D : 180D;
    }

    /// <summary>响应索引抽屉标题条的展开/收起操作。</summary>
    /// <param name="sender">索引抽屉切换按钮。</param>
    /// <param name="args">按钮状态变化事件参数。</param>
    private void OnIndexDrawerToggleChanged(object? sender, RoutedEventArgs args)
    {
        SetIndexDrawerExpanded(IndexDrawerToggleButton.IsChecked == true);
    }

    /// <summary>在 32px 标题条和 180-300px 可调整配置区之间切换。</summary>
    /// <param name="isExpanded">是否显示配置表单与扫描预览。</param>
    private void SetIndexDrawerExpanded(bool isExpanded)
    {
        if (!isExpanded && IndexDrawerPanel.Bounds.Height >= MIN_EXPANDED_DRAWER_HEIGHT)
        {
            mExpandedDrawerHeight = Math.Clamp(
                IndexDrawerPanel.Bounds.Height,
                MIN_EXPANDED_DRAWER_HEIGHT,
                MAX_EXPANDED_DRAWER_HEIGHT);
        }

        Classes.Set("audiokit-drawer-expanded", isExpanded);
        IndexDrawerContent.IsVisible = isExpanded;
        IndexDrawerSplitter.IsVisible = isExpanded;
        ApplyMainAreaHeightBudget(isExpanded);

        RowDefinition splitterRow = RootLayout.RowDefinitions[5];
        RowDefinition drawerRow = RootLayout.RowDefinitions[6];
        splitterRow.Height = new GridLength(isExpanded ? 5D : 0D);
        drawerRow.MinHeight = isExpanded ? MIN_EXPANDED_DRAWER_HEIGHT : COLLAPSED_DRAWER_HEIGHT;
        drawerRow.MaxHeight = isExpanded ? MAX_EXPANDED_DRAWER_HEIGHT : COLLAPSED_DRAWER_HEIGHT;
        drawerRow.Height = new GridLength(isExpanded
            ? mIsWideLayout
                ? Math.Clamp(mExpandedDrawerHeight, MIN_EXPANDED_DRAWER_HEIGHT, MAX_EXPANDED_DRAWER_HEIGHT)
                : MIN_EXPANDED_DRAWER_HEIGHT
            : COLLAPSED_DRAWER_HEIGHT);

    }

    /// <summary>在用户完成任一索引字段编辑后自动保存当前项目配置。</summary>
    /// <param name="sender">失去焦点的索引输入控件。</param>
    /// <param name="args">失去焦点事件参数。</param>
    private void OnIndexSettingLostFocus(object? sender, RoutedEventArgs args)
    {
        if (DataContext is AudioKitPageViewModel viewModel)
        {
            _ = viewModel.SaveIndexSettingsAsync();
        }
    }
}
