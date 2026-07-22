using Avalonia.Controls;
using Avalonia.Styling;
using YokiFrame.Workbench.Avalonia.Platform;
using YokiFrame.Workbench.Avalonia.Services;
using YokiFrame.Workbench.Avalonia.ViewModels;
using YokiFrame.Workbench.Avalonia.Views;
using YokiFrame.Tooling.Application.Installer;

namespace YokiFrame.Workbench.Avalonia;

/// <summary>
/// Installer 模式主窗口，负责组合 Application 会话和跨平台目录选择服务。
/// </summary>
public sealed class InstallerWindow : Window
{
    private const double INSTALLER_TITLE_BAR_HEIGHT = 72;

    private readonly InstallerShellViewModel mShellViewModel;

    /// <summary>
    /// 创建 Installer 模式主窗口。
    /// </summary>
    /// <param name="startupOptions">启动默认路径。</param>
    public InstallerWindow(ToolStartupOptions startupOptions)
    {
        InstallerSessionService session = new(new InstallerCoreWorkflowGateway());
        mShellViewModel = new InstallerShellViewModel(
            startupOptions,
            session,
            new InstallerTargetDetectionService(),
            new InstallerInputDetectionService(TimeSpan.FromMilliseconds(300)),
            new AvaloniaInstallerFolderPicker(() => StorageProvider));
        Title = "YokiFrame Installer";
        RequestedThemeVariant = ThemeVariant.Light;
        Width = 1080;
        Height = 760;
        MinWidth = 900;
        MinHeight = 680;
        CanResize = true;
        CanMaximize = true;
        WindowDecorations = WindowDecorations.BorderOnly;
        ExtendClientAreaToDecorationsHint = true;
        ExtendClientAreaTitleBarHeightHint = INSTALLER_TITLE_BAR_HEIGHT;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        BrandIconLoader.ApplyTo(this);
        Content = new InstallerShellView(mShellViewModel);
        Opened += OnOpened;
    }

    /// <summary>
    /// 窗口首次显示后启动路径检测，确保 StorageProvider 和 UI 上下文均已就绪。
    /// </summary>
    /// <param name="sender">窗口实例。</param>
    /// <param name="eventArgs">窗口打开事件参数。</param>
    private async void OnOpened(object? sender, EventArgs eventArgs)
    {
        try
        {
            await mShellViewModel.InitializeAsync();
        }
        catch (Exception exception)
        {
            // 窗口事件没有 Task 返回边界，初始化异常只能在这里收口，避免终止 UI 线程。
            mShellViewModel.ShowLocalError(exception.Message);
        }
    }
}
