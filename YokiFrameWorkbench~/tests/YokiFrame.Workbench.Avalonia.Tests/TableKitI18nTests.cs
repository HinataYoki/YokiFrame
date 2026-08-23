using YokiFrame.Workbench.Avalonia.Services;
using YokiFrame.Workbench.Avalonia.ViewModels;
using Xunit;

namespace YokiFrame.Workbench.Avalonia.Tests;

/// <summary>验证 TableKit 页面的占位与计算摘要会随语言切换刷新。</summary>
public sealed class TableKitI18nTests
{
    /// <summary>语言切换应重投影预览、控制台与收起/展开按钮等计算文本。</summary>
    /// <remarks>必须先初始化 Headless UI 线程：SetCulture 只有在 UI 线程上才会同步触发
    /// CultureChanged，否则重投影被 Post 到无人泵的 Dispatcher 队列。</remarks>
    [Fact]
    public async Task TablePage_ReprojectsComputedTextsOnCultureChange()
    {
        InstallerHeadlessTestApplication.EnsureInitialized();
        await global::Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            WorkbenchI18nService service = WorkbenchI18nService.Instance;
            service.SetCulture("zh-CN");
            TableKitPageViewModel viewModel = new(
                System.IO.Directory.GetCurrentDirectory(),
                new YokiFrame.Tooling.Application.Services.TableKit.TableKitApplicationService());
            try
            {
                Assert.Equal("等待验证", viewModel.PreviewCountText);
                Assert.Equal("等待操作", viewModel.ConsoleCountText);
                Assert.Equal("展开", viewModel.ConsoleToggleText);
                Assert.False(viewModel.IsConsoleExpanded);

                service.SetCulture("en-US");

                Assert.Equal("Waiting for validation", viewModel.PreviewCountText);
                Assert.Equal("Waiting for operation", viewModel.ConsoleCountText);
                Assert.Equal("Expand", viewModel.ConsoleToggleText);

                viewModel.IsConsoleExpanded = true;
                Assert.Equal("Collapse", viewModel.ConsoleToggleText);
            }
            finally
            {
                viewModel.Dispose();
                service.SetCulture("zh-CN");
            }
        });
    }
}
