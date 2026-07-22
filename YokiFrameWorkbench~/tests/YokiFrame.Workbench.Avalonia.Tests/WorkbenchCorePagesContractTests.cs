using Avalonia.Controls;
using Avalonia.Threading;
using YokiFrame.Tooling.Application.Services;
using YokiFrame.Workbench.Avalonia.ViewModels;
using YokiFrame.Workbench.Avalonia.Views;

namespace YokiFrame.Workbench.Avalonia.Tests;

/// <summary>
/// 覆盖 Workbench 文档页面的降级边界和离线阅读器信息架构。
/// </summary>
public sealed class WorkbenchCorePagesContractTests
{
    /// <summary>
    /// 验证无效 source 只让 Docs 降级，不阻断 Framework 和 Workbench 窗口构造。
    /// </summary>
    [Fact]
    public async Task InvalidDocumentationSourceDoesNotBlockWorkbenchWindow()
    {
        InstallerHeadlessTestApplication.EnsureInitialized();
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var projectRoot = Path.Combine(
                Path.GetTempPath(),
                "yokiframe-invalid-docs-tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(projectRoot);
            var invalidSource = Path.Combine(projectRoot, "missing-package");
            var options = new ToolStartupOptions(
                ToolStartupMode.Workbench,
                projectRoot,
                invalidSource,
                projectRoot);
            WorkbenchWindow? window = null;

            var exception = Record.Exception(() =>
            {
                window = new WorkbenchWindow(
                    new WorkbenchDashboardService(projectRoot),
                    options);
            });

            Assert.Null(exception);
            Assert.NotNull(window);
            var shell = Assert.IsType<WorkbenchShellView>(window.Content);
            var viewModel = Assert.IsType<WorkbenchShellViewModel>(shell.DataContext);
            Assert.Equal("Framework", viewModel.SelectedPage);
            Assert.Contains("不存在", viewModel.DocumentationPage.StatusText);
            window.Close();
        });
    }

    /// <summary>
    /// 验证 Docs 页面使用专用离线阅读器，并且公共页头不重复展示路径、状态和版本元数据。
    /// </summary>
    [Fact]
    public void DocumentationPageUsesDedicatedOfflineReaderLayout()
    {
        var xaml = WorkbenchContractTestFiles.ReadSource("Views", "Pages", "DocumentationPageView.axaml");
        var shellXaml = WorkbenchContractTestFiles.ReadSource("Views", "WorkbenchShellView.axaml");

        Assert.DoesNotContain("DocumentationPage.SourcePackageRoot", shellXaml);
        Assert.DoesNotContain("DocumentationPage.StatusText", shellXaml);
        Assert.DoesNotContain("DocumentationPage.PackageVersion", shellXaml);
        Assert.Contains("NativeWebView", xaml);
        Assert.Contains("workbench.docs.webview", xaml);
        Assert.Contains("WebMessageReceived", xaml);
        Assert.Contains("NavigationCompleted", xaml);
    }
}
