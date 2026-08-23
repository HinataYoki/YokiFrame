using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using YokiFrame.Workbench.Avalonia.ViewModels;

namespace YokiFrame.Workbench.Avalonia.Views;

/// <summary>
/// Installer 模式的 XAML UserControl。
/// </summary>
public sealed partial class InstallerShellView : UserControl
{
    private ObservableCollection<InstallerLogLine>? mObservedLogs;

    /// <summary>
    /// 创建 Installer Shell 视图并加载 XAML。
    /// </summary>
    public InstallerShellView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        DetachedFromVisualTree += OnDetachedFromVisualTree;
    }

    /// <summary>
    /// 创建 Installer Shell 视图并绑定指定 ViewModel。
    /// </summary>
    /// <param name="viewModel">Installer Shell ViewModel。</param>
    public InstallerShellView(InstallerShellViewModel viewModel)
        : this()
    {
        DataContext = viewModel;
    }

    /// <summary>
    /// 在 ViewModel 变化时切换日志集合订阅，避免旧页面继续持有视图引用。
    /// </summary>
    /// <param name="sender">Installer 视图。</param>
    /// <param name="eventArgs">DataContext 变化事件参数。</param>
    private void OnDataContextChanged(object? sender, EventArgs eventArgs)
    {
        if (mObservedLogs != null)
        {
            mObservedLogs.CollectionChanged -= OnLogCollectionChanged;
        }

        mObservedLogs = (DataContext as InstallerShellViewModel)?.LogEntries;
        if (mObservedLogs != null)
        {
            mObservedLogs.CollectionChanged += OnLogCollectionChanged;
        }
    }

    /// <summary>
    /// 日志追加后调度滚动到末行，保持与旧版 Installer 一致的自动滚底体验。
    /// </summary>
    /// <param name="sender">日志集合。</param>
    /// <param name="eventArgs">集合变化信息。</param>
    private void OnLogCollectionChanged(object? sender, NotifyCollectionChangedEventArgs eventArgs)
    {
        Dispatcher.UIThread.Post(ScrollToLatestLog);
    }

    /// <summary>
    /// 把日志列表滚动到最后一项；清空或视图已释放时不执行。
    /// </summary>
    private void ScrollToLatestLog()
    {
        if (mObservedLogs == null || mObservedLogs.Count == 0)
        {
            return;
        }

        InstallerLogList.ScrollIntoView(mObservedLogs[^1]);
    }

    /// <summary>
    /// 视图从窗口树移除时释放当前 Installer ViewModel，解除静态语言服务和会话事件订阅。
    /// </summary>
    /// <param name="sender">Installer 视图。</param>
    /// <param name="eventArgs">视觉树分离事件参数。</param>
    private void OnDetachedFromVisualTree(object? sender, EventArgs eventArgs)
    {
        if (DataContext is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    /// <summary>
    /// 关闭无原生边框 Installer 窗口，保持旧版右上角关闭入口可用。
    /// </summary>
    /// <param name="sender">关闭按钮。</param>
    /// <param name="eventArgs">点击事件。</param>
    private void OnCloseInstallerButtonClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (TopLevel.GetTopLevel(this) is Window window)
        {
            window.Close();
        }

        eventArgs.Handled = true;
    }
}
