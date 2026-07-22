namespace YokiFrame.Workbench.Avalonia.Tests;

/// <summary>
/// 锁定新版 Avalonia Installer 与旧版安装器一致的用户可见功能契约。
/// </summary>
public sealed class InstallerParityContractTests
{
    /// <summary>
    /// 验证安装页面提供源目录、目标目录、Unity 两种模式和 Godot 两项配置。
    /// </summary>
    [Fact]
    public void InstallerShellExposesAllInstallationChoices()
    {
        var xaml = ReadSourceFile("Views", "InstallerShellView.axaml");

        Assert.Contains("installer.source.pick", xaml);
        Assert.Contains("installer.target.pick", xaml);
        Assert.Contains("本地包", xaml);
        Assert.Contains("Git 包", xaml);
        Assert.Contains("Git URL", xaml);
        Assert.Contains("维护 project.godot 中的 YokiFrame 设置", xaml);
        Assert.Contains("登记并启用 YokiFrame 编辑器插件", xaml);
    }

    /// <summary>
    /// 验证目录选择、预览、安装和日志清理均绑定命令，不保留静态禁用占位按钮。
    /// </summary>
    [Fact]
    public void InstallerShellBindsEveryPrimaryWorkflowCommand()
    {
        var xaml = ReadSourceFile("Views", "InstallerShellView.axaml");

        Assert.Contains("PickSourceCommand", xaml);
        Assert.Contains("PickTargetCommand", xaml);
        Assert.Contains("PreviewCommand", xaml);
        Assert.Contains("InstallCommand", xaml);
        Assert.Contains("BootstrapGodotRuntimeCommand", xaml);
        Assert.Contains("ClearLogCommand", xaml);
        Assert.DoesNotContain("IsEnabled=\"False\"", xaml);
    }

    /// <summary>
    /// 验证安装器显示阶段进度、可滚动日志和无障碍实时状态，支持错误后重试判断。
    /// </summary>
    [Fact]
    public void InstallerShellShowsProgressLogsAndLiveStatus()
    {
        var xaml = ReadSourceFile("Views", "InstallerShellView.axaml");

        Assert.Contains("<ProgressBar", xaml);
        Assert.Contains("LogEntries", xaml);
        Assert.Contains("AutomationProperties.LiveSetting=\"Polite\"", xaml);
        Assert.Contains("CanRetry", xaml);
        Assert.Contains("IsGodotRuntimeBootstrapVisible", xaml);
        Assert.Contains("installer.godot.bootstrap", xaml);
        Assert.Contains("OutcomeDetailsText", xaml);
    }

    /// <summary>
    /// 验证窗口只组合 Application 安装会话，不再直接在 Avalonia 层构建 Core 计划。
    /// </summary>
    [Fact]
    public void InstallerWindowDelegatesWorkflowToApplicationLayer()
    {
        var source = ReadSourceFile("InstallerWindow.cs");

        Assert.Contains("InstallerSessionService", source);
        Assert.DoesNotContain("new InstallPlanBuilder", source);
    }

    /// <summary>
    /// 验证 Installer 支持最低视口以上缩放，并使用 Avalonia 原生标题栏角色提供拖拽和关闭入口。
    /// </summary>
    [Fact]
    public void InstallerWindowUsesNativeTitleBarDragRegion()
    {
        var windowSource = ReadSourceFile("InstallerWindow.cs");
        var viewSource = ReadSourceFile("Views", "InstallerShellView.axaml");

        Assert.Contains("MinWidth = 900", windowSource);
        Assert.Contains("MinHeight = 680", windowSource);
        Assert.Contains("CanResize = true", windowSource);
        Assert.Contains("WindowDecorations = WindowDecorations.BorderOnly", windowSource);
        Assert.Contains("ExtendClientAreaToDecorationsHint = true", windowSource);
        Assert.Contains("ExtendClientAreaTitleBarHeightHint = INSTALLER_TITLE_BAR_HEIGHT", windowSource);
        Assert.Contains("WindowStartupLocation = WindowStartupLocation.CenterScreen", windowSource);
        Assert.Contains("OnCloseInstallerButtonClick", viewSource);
        Assert.Contains("chrome:WindowDecorationProperties.ElementRole=\"TitleBar\"", viewSource);
        Assert.Contains("chrome:WindowDecorationProperties.ElementRole=\"CloseButton\"", viewSource);
        Assert.DoesNotContain("OnInstallerHeaderPointerPressed", viewSource);
    }

    /// <summary>
    /// 验证 Unity 安装模式使用等宽分段控件，并移除原生单选圆点。
    /// </summary>
    [Fact]
    public void InstallerModeSegmentsStretchAcrossTheirGridCells()
    {
        var xaml = ReadSourceFile("Views", "InstallerShellView.axaml");
        var inputStyles = ReadSourceFile("Styles", "Inputs.axaml");

        Assert.Contains("ColumnDefinitions=\"*,*\"", xaml);
        Assert.Contains("RadioButton.install-mode-option", inputStyles);
        Assert.Contains("<Setter Property=\"HorizontalAlignment\" Value=\"Stretch\" />", inputStyles);
        Assert.Contains("<ControlTemplate>", inputStyles);
        Assert.Contains("segment-left", xaml);
        Assert.Contains("segment-right", xaml);
    }

    /// <summary>
    /// 验证目录选择使用 Avalonia 跨平台 StorageProvider，不回退到 Windows 专属对话框。
    /// </summary>
    [Fact]
    public void InstallerFolderPickerUsesAvaloniaStorageProvider()
    {
        var source = ReadSourceFile("Services", "AvaloniaInstallerFolderPicker.cs");

        Assert.Contains("IStorageProvider", source);
        Assert.Contains("OpenFolderPickerAsync", source);
        Assert.DoesNotContain("System.Windows.Forms", source);
        Assert.DoesNotContain("Microsoft.Win32", source);
    }

    /// <summary>
    /// 从源码树或测试输出树向上定位 Avalonia 项目文件。
    /// </summary>
    /// <param name="pathParts">相对于 Avalonia 项目根的路径片段。</param>
    /// <returns>目标文件完整文本。</returns>
    private static string ReadSourceFile(params string[] pathParts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            var directCandidate = Path.Combine(
                new[] { directory.FullName, "src", "YokiFrame.Workbench.Avalonia" }.Concat(pathParts).ToArray());
            if (File.Exists(directCandidate))
            {
                return File.ReadAllText(directCandidate);
            }

            var workspaceCandidate = Path.Combine(
                new[]
                {
                    directory.FullName,
                    "Assets",
                    "YokiFrame",
                    "YokiFrameWorkbench~",
                    "src",
                    "YokiFrame.Workbench.Avalonia"
                }.Concat(pathParts).ToArray());
            if (File.Exists(workspaceCandidate))
            {
                return File.ReadAllText(workspaceCandidate);
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("无法定位 Avalonia Installer 源文件: " + string.Join('/', pathParts));
    }
}
