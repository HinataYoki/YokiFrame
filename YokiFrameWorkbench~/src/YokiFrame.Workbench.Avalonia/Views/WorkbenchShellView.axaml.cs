using Avalonia.Controls;
using Avalonia.Input;
using YokiFrame.Workbench.Avalonia.ViewModels;

namespace YokiFrame.Workbench.Avalonia.Views;

/// <summary>
/// Workbench Shell 的 XAML UserControl。
/// </summary>
public sealed partial class WorkbenchShellView : UserControl
{
    /// <summary>
    /// 创建 Workbench Shell 视图并加载 XAML。
    /// </summary>
    public WorkbenchShellView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 创建 Workbench Shell 视图并绑定指定 ViewModel。
    /// </summary>
    /// <param name="viewModel">Workbench Shell ViewModel。</param>
    public WorkbenchShellView(WorkbenchShellViewModel viewModel)
        : this()
    {
        DataContext = viewModel;
    }

    /// <summary>
    /// 在文档标题栏搜索框按下 Enter 时执行当前关键词搜索，避免用户额外移动到图标按钮。
    /// </summary>
    /// <param name="sender">触发输入事件的搜索框。</param>
    /// <param name="args">当前键盘输入参数。</param>
    private async void OnDocumentationSearchKeyDown(object? sender, KeyEventArgs args)
    {
        if (args.Key != Key.Enter || DataContext is not WorkbenchShellViewModel viewModel)
        {
            return;
        }

        try
        {
            if (!viewModel.DocumentationPage.SearchCommand.CanExecute(null))
            {
                return;
            }

            args.Handled = true;
            await viewModel.DocumentationPage.SearchCommand.ExecuteAsync();
        }
        catch (Exception exception)
        {
            viewModel.ShowTransientError("文档搜索失败: " + exception.Message);
        }
    }
}
