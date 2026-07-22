using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Styling;

namespace YokiFrame.Workbench.Avalonia.Components;

/// <summary>
/// Workbench 顶部自绘标题栏，负责品牌区、连接状态和窗口控制按钮。
/// </summary>
public sealed partial class AppTitleBar : UserControl
{
    /// <summary>
    /// 创建标题栏组件并加载 XAML。
    /// </summary>
    public AppTitleBar()
    {
        InitializeComponent();
        UpdateThemeToggleIcon();
    }

    /// <summary>
    /// 处理主题切换按钮点击，在暗色和亮色主题之间切换。
    /// </summary>
    /// <param name="sender">事件来源。</param>
    /// <param name="eventArgs">点击事件参数。</param>
    private void OnToggleThemeButtonClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (Application.Current == null)
        {
            return;
        }

        Application.Current.RequestedThemeVariant = Application.Current.RequestedThemeVariant == ThemeVariant.Dark
            ? ThemeVariant.Light
            : ThemeVariant.Dark;
        UpdateThemeToggleIcon();
        eventArgs.Handled = true;
    }

    /// <summary>
    /// 处理自绘最大化按钮点击，在 BorderOnly 标题栏下显式切换宿主窗口状态。
    /// </summary>
    /// <param name="sender">事件来源。</param>
    /// <param name="eventArgs">点击事件参数。</param>
    private void OnMaximizeWindowButtonClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (TopLevel.GetTopLevel(this) is not Window window)
        {
            return;
        }

        window.WindowState = window.WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
        eventArgs.Handled = true;
    }

    /// <summary>
    /// 处理自绘关闭按钮点击，在 BorderOnly 标题栏下显式关闭宿主窗口。
    /// </summary>
    /// <param name="sender">事件来源。</param>
    /// <param name="eventArgs">点击事件参数。</param>
    private void OnCloseWindowButtonClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (TopLevel.GetTopLevel(this) is not Window window)
        {
            return;
        }

        window.Close();
        eventArgs.Handled = true;
    }

    /// <summary>
    /// 根据当前主题刷新切换按钮图标，暗色主题显示太阳，亮色主题显示月亮。
    /// </summary>
    private void UpdateThemeToggleIcon()
    {
        if (ThemeSunIcon == null || ThemeMoonIcon == null || Application.Current == null)
        {
            return;
        }

        var isDarkTheme = Application.Current.RequestedThemeVariant == ThemeVariant.Dark;
        ThemeSunIcon.IsVisible = isDarkTheme;
        ThemeMoonIcon.IsVisible = !isDarkTheme;
    }
}
