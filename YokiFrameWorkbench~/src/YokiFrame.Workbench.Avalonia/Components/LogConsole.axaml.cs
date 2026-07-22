using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using YokiFrame.Workbench.Avalonia.ViewModels;

namespace YokiFrame.Workbench.Avalonia.Components;

/// <summary>
/// Workbench 运行日志组件，统一终端外观和日志行展示。
/// </summary>
public sealed partial class LogConsole : UserControl
{
    /// <summary>
    /// 创建日志控制台组件并加载 XAML。
    /// </summary>
    public LogConsole()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 把当前运行日志复制到系统剪贴板；控件尚未挂载窗口时安全跳过。
    /// </summary>
    /// <param name="sender">事件来源。</param>
    /// <param name="e">路由事件参数。</param>
    private async void OnCopyLogsButtonClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (DataContext is not WorkbenchShellViewModel viewModel)
            {
                return;
            }

            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard == null)
            {
                return;
            }

            await clipboard.SetTextAsync(viewModel.CreateLogClipboardText());
        }
        catch (Exception exception)
        {
            // 剪贴板是平台边界，窗口关闭或权限变化时失败不能冒泡到 Avalonia 事件循环。
            if (DataContext is WorkbenchShellViewModel viewModel)
            {
                viewModel.ShowTransientError("复制日志失败: " + exception.Message);
            }
        }
    }
}
