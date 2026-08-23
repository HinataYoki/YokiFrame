using YokiFrame.Workbench.Avalonia.Views;
using YokiFrame.Workbench.Avalonia.ViewModels;

namespace YokiFrame.Workbench.Avalonia.Tests;

/// <summary>
/// 覆盖 Installer 模式的 XAML 与 ViewModel 分层契约。
/// </summary>
public sealed class InstallerShellViewTests
{
    /// <summary>
    /// 验证 Installer Shell 已改为 XAML UserControl。
    /// </summary>
    [Fact]
    public void InstallerShellViewUsesXamlUserControl()
    {
        Assert.True(typeof(global::Avalonia.Controls.UserControl).IsAssignableFrom(typeof(InstallerShellView)));
    }

    /// <summary>
    /// 验证 Installer Shell 使用步骤轨道、主工作面、变更审阅和底部命令栏的新工作台布局。
    /// </summary>
    [Fact]
    public void InstallerShellViewDefinesReadableLightInstallerLayout()
    {
        var xaml = ReadInstallerShellViewXaml();

        Assert.Contains("Classes=\"installer-rail\"", xaml);
        Assert.Contains("Classes=\"installer-workspace\"", xaml);
        Assert.Contains("Classes=\"installer-review-pane\"", xaml);
        Assert.Contains("Classes=\"installer-footer\"", xaml);
        Assert.Contains("Classes=\"installer-log-header\"", xaml);
        Assert.Contains("String.Installer.Title", xaml);
        Assert.Contains("String.Installer.ProjectAndSource", xaml);
        Assert.Contains("String.Installer.ReviewTitle", xaml);
        Assert.Contains("String.Installer.SourceDirectory", xaml);
        Assert.Contains("String.Installer.TargetProject", xaml);
        Assert.Contains("String.Installer.LogTitle", xaml);
        Assert.Contains("String.Installer.PreviewPlan", xaml);
        Assert.Contains("String.Installer.InstallOrUpdate", xaml);
        Assert.Contains("String.Installer.BuildRuntime", xaml);
    }

    /// <summary>
    /// 验证 InstallerWindow 显式使用浅色主题，防止 WorkbenchApp 的暗色默认主题污染安装器控件模板。
    /// </summary>
    [Fact]
    public void InstallerWindowRequestsLightThemeVariant()
    {
        var source = ReadInstallerWindowSource();

        Assert.Contains("RequestedThemeVariant = ThemeVariant.Light", source);
    }

    /// <summary>
    /// 验证 Installer Shell 复用全局设计系统资源，视图自身不硬编码颜色。
    /// </summary>
    [Fact]
    public void InstallerShellUsesSharedDesignSystemStyles()
    {
        var xaml = ReadInstallerShellViewXaml();

        Assert.DoesNotContain("InstallBackgroundBrush", xaml);
        Assert.DoesNotContain("InstallSurfaceBrush", xaml);
        Assert.DoesNotContain("Color=\"#", xaml);
        Assert.Contains("Brush.Surface.Panel", xaml);
        Assert.Contains("Classes=\"installer-rail\"", xaml);
        Assert.Contains("Classes=\"installer-review-pane\"", xaml);
        Assert.Contains("Classes=\"primary\"", xaml);
        Assert.Contains("Classes=\"terminal\"", xaml);
    }

    /// <summary>
    /// 从当前测试目录向上查找 Installer Shell XAML，用于验证安装器布局和本地样式契约。
    /// </summary>
    /// <returns>Installer Shell XAML 文本。</returns>
    private static string ReadInstallerShellViewXaml()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            foreach (var candidate in CreateInstallerShellViewXamlCandidates(directory.FullName))
            {
                if (File.Exists(candidate))
                {
                    return File.ReadAllText(candidate);
                }
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("无法定位 InstallerShellView.axaml。");
    }

    /// <summary>
    /// 从当前测试目录向上查找 InstallerWindow 源码，用于验证窗口主题契约。
    /// </summary>
    /// <returns>InstallerWindow 源码文本。</returns>
    private static string ReadInstallerWindowSource()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            foreach (var candidate in CreateInstallerWindowSourceCandidates(directory.FullName))
            {
                if (File.Exists(candidate))
                {
                    return File.ReadAllText(candidate);
                }
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("无法定位 InstallerWindow.cs。");
    }

    /// <summary>
    /// 生成源码树和测试输出树下可能存在的 Installer Shell XAML 路径。
    /// </summary>
    /// <param name="directory">当前向上探测的目录。</param>
    /// <returns>候选 XAML 路径。</returns>
    private static IEnumerable<string> CreateInstallerShellViewXamlCandidates(string directory)
    {
        yield return Path.Combine(
            directory,
            "src",
            "YokiFrame.Workbench.Avalonia",
            "Views",
            "InstallerShellView.axaml");
        yield return Path.Combine(
            directory,
            "Assets",
            "YokiFrame",
            "YokiFrameWorkbench~",
            "src",
            "YokiFrame.Workbench.Avalonia",
            "Views",
            "InstallerShellView.axaml");
    }

    /// <summary>
    /// 生成源码树和测试输出树下可能存在的 InstallerWindow 源码路径。
    /// </summary>
    /// <param name="directory">当前向上探测的目录。</param>
    /// <returns>候选源码路径。</returns>
    private static IEnumerable<string> CreateInstallerWindowSourceCandidates(string directory)
    {
        yield return Path.Combine(
            directory,
            "src",
            "YokiFrame.Workbench.Avalonia",
            "InstallerWindow.cs");
        yield return Path.Combine(
            directory,
            "Assets",
            "YokiFrame",
            "YokiFrameWorkbench~",
            "src",
            "YokiFrame.Workbench.Avalonia",
            "InstallerWindow.cs");
    }
}
