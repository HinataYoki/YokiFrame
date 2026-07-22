using System.ComponentModel;
using Avalonia.Controls;
using YokiFrame.Workbench.Avalonia.ViewModels;

namespace YokiFrame.Workbench.Avalonia.Views.Pages;

/// <summary>承载 TableKit 任务工作区和按状态展开的控制台抽屉。</summary>
public partial class TableKitPageView : UserControl
{
    private const double COLLAPSED_CONSOLE_HEIGHT = 36D;
    private const double MIN_EXPANDED_CONSOLE_HEIGHT = 170D;
    private const double DEFAULT_EXPANDED_CONSOLE_HEIGHT = 240D;
    private const double MAX_EXPANDED_CONSOLE_HEIGHT = 320D;
    private TableKitPageViewModel? mObservedViewModel;
    private double mExpandedConsoleHeight = DEFAULT_EXPANDED_CONSOLE_HEIGHT;

    /// <summary>初始化页面并应用默认收起的控制台状态。</summary>
    public TableKitPageView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        ApplyConsoleLayout(false);
    }

    /// <summary>切换 ViewModel 订阅，确保抽屉状态与页面状态保持一致。</summary>
    /// <param name="sender">TableKit 页面。</param>
    /// <param name="eventArgs">DataContext 变化事件参数。</param>
    private void OnDataContextChanged(object? sender, EventArgs eventArgs)
    {
        if (mObservedViewModel != null) mObservedViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        mObservedViewModel = DataContext as TableKitPageViewModel;
        if (mObservedViewModel != null) mObservedViewModel.PropertyChanged += OnViewModelPropertyChanged;
        ApplyConsoleLayout(mObservedViewModel?.IsConsoleExpanded == true);
    }

    /// <summary>响应控制台展开属性变化并重新分配底部高度。</summary>
    /// <param name="sender">当前 TableKit ViewModel。</param>
    /// <param name="args">发生变化的绑定属性。</param>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(TableKitPageViewModel.IsConsoleExpanded))
        {
            ApplyConsoleLayout(mObservedViewModel?.IsConsoleExpanded == true);
        }
    }

    /// <summary>在 36px 摘要条和 170-320px 可拖拽日志区之间切换。</summary>
    /// <param name="isExpanded">是否显示控制台日志正文。</param>
    private void ApplyConsoleLayout(bool isExpanded)
    {
        PreserveExpandedConsoleHeight(isExpanded);
        RowDefinition splitterRow = TableKitRootLayout.RowDefinitions[2];
        RowDefinition consoleRow = TableKitRootLayout.RowDefinitions[3];
        splitterRow.Height = new GridLength(isExpanded ? 5D : 0D);
        consoleRow.MinHeight = isExpanded ? MIN_EXPANDED_CONSOLE_HEIGHT : COLLAPSED_CONSOLE_HEIGHT;
        consoleRow.MaxHeight = isExpanded ? MAX_EXPANDED_CONSOLE_HEIGHT : COLLAPSED_CONSOLE_HEIGHT;
        consoleRow.Height = new GridLength(isExpanded ? mExpandedConsoleHeight : COLLAPSED_CONSOLE_HEIGHT);
        TableKitConsoleSplitter.IsVisible = isExpanded;
        TableKitConsoleSplitter.IsEnabled = isExpanded;
        TableKitConsoleList.IsVisible = isExpanded;
    }

    /// <summary>收起前保存用户拖拽后的控制台高度，后续展开时恢复。</summary>
    /// <param name="isExpanded">目标状态是否为展开。</param>
    private void PreserveExpandedConsoleHeight(bool isExpanded)
    {
        if (isExpanded || TableKitConsolePanel.Bounds.Height < MIN_EXPANDED_CONSOLE_HEIGHT) return;
        mExpandedConsoleHeight = Math.Clamp(
            TableKitConsolePanel.Bounds.Height,
            MIN_EXPANDED_CONSOLE_HEIGHT,
            MAX_EXPANDED_CONSOLE_HEIGHT);
    }
}
