using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using YokiFrame.Workbench.Avalonia.Diagnostics;
using YokiFrame.Tooling.Application.Services;

namespace YokiFrame.Workbench.Avalonia;

/// <summary>
/// 配置 Workbench 应用样式和主窗口。
/// </summary>
public sealed partial class WorkbenchApp : Application
{
    /// <summary>
    /// 获取 Workbench 默认主题；固定暗色以匹配 Tauri 调试工作台视觉基线。
    /// </summary>
    public static ThemeVariant DefaultThemeVariant => ThemeVariant.Dark;

    /// <summary>
    /// 初始化 Avalonia 主题和全局样式。
    /// </summary>
    public override void Initialize()
    {
        WorkbenchStartupTrace.Mark("app.initialize.enter");
        try
        {
            AvaloniaXamlLoader.Load(this);
            RequestedThemeVariant = DefaultThemeVariant;
        }
        finally
        {
            WorkbenchStartupTrace.Mark("app.initialize.exit");
        }
    }

    /// <summary>
    /// 在桌面生命周期中创建主窗口并注入 Tooling.Application 服务。
    /// </summary>
    public override void OnFrameworkInitializationCompleted()
    {
        WorkbenchStartupTrace.Mark("app.framework.enter");
        try
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var options = Program.StartupOptions
                    ?? ToolStartupOptions.FromArgs(
                        desktop.Args ?? Array.Empty<string>(),
                        Directory.GetCurrentDirectory(),
                        AppContext.BaseDirectory);
                desktop.ShutdownMode = global::Avalonia.Controls.ShutdownMode.OnMainWindowClose;
                desktop.MainWindow = options.Mode == ToolStartupMode.Workbench
                    ? new WorkbenchWindow(
                        new WorkbenchDashboardService(options.ProjectRoot),
                        options,
                        Program.ActivationCoordinator)
                    : new InstallerWindow(options);
            }

            base.OnFrameworkInitializationCompleted();
        }
        finally
        {
            WorkbenchStartupTrace.Mark("app.framework.exit");
        }
    }
}
