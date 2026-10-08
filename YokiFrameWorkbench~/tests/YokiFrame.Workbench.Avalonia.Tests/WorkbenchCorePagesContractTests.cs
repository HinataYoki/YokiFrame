using Avalonia.Threading;
using YokiFrame.Tooling.Application.Services;
using YokiFrame.Workbench.Avalonia.ViewModels;
using YokiFrame.Workbench.Avalonia.Views;

namespace YokiFrame.Workbench.Avalonia.Tests;

/// <summary>
/// 覆盖 Workbench 文档页面的降级边界。
/// </summary>
public sealed class WorkbenchCorePagesContractTests
{
    /// <summary>
    /// 验证无效文档源只让文档页进入包根不存在状态，不阻断 Workbench 窗口构造。
    /// </summary>
    [Fact]
    public async Task 构造窗口_无效文档源_不阻断且文档状态为包根不存在()
    {
        InstallerHeadlessTestApplication.EnsureInitialized();
        await Dispatcher.UIThread.InvokeAsync(AssertInvalidDocumentationSourceDoesNotBlockWindow);
    }

    /// <summary>
    /// 在 UI 线程构造窗口并断言精确文档降级状态，结束后关闭窗口并删除临时目录。
    /// </summary>
    private static void AssertInvalidDocumentationSourceDoesNotBlockWindow()
    {
        var projectRoot = Path.Combine(
            Path.GetTempPath(),
            "yokiframe-invalid-docs-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(projectRoot);
        WorkbenchWindow? window = null;
        try
        {
            var invalidSource = Path.Combine(projectRoot, "missing-package");
            var options = new ToolStartupOptions(
                ToolStartupMode.Workbench,
                projectRoot,
                invalidSource,
                projectRoot);
            var exception = Record.Exception(() =>
            {
                window = new WorkbenchWindow(new WorkbenchDashboardService(projectRoot), options);
            });

            Assert.Null(exception);
            Assert.NotNull(window);
            var shell = Assert.IsType<WorkbenchShellView>(window.Content);
            var viewModel = Assert.IsType<WorkbenchShellViewModel>(shell.DataContext);
            Assert.Equal("Framework", viewModel.SelectedPage);
            Assert.Equal(
                "文档服务不可用: YokiFrame 包根不存在: " + Path.GetFullPath(invalidSource),
                viewModel.DocumentationPage.StatusText);
        }
        finally
        {
            window?.Close();
            if (Directory.Exists(projectRoot))
            {
                Directory.Delete(projectRoot, recursive: true);
            }
        }
    }
}
