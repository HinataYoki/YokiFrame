using YokiFrame.Workbench.Avalonia.Services;
using YokiFrame.Workbench.Avalonia.ViewModels;
using Xunit;

namespace YokiFrame.Workbench.Avalonia.Tests;

/// <summary>验证 Documentation 页面的未加载占位会随语言切换刷新。</summary>
public sealed class DocumentationI18nTests
{
    /// <summary>语言切换应重投影版本、正文与状态占位；已加载内容不受影响。</summary>
    /// <remarks>必须先初始化 Headless UI 线程：SetCulture 只有在 UI 线程上才会同步触发
    /// CultureChanged，否则重投影被 Post 到无人泵的 Dispatcher 队列。</remarks>
    [Fact]
    public async Task DocsPage_ReprojectsPlaceholdersOnCultureChange()
    {
        InstallerHeadlessTestApplication.EnsureInitialized();
        await global::Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            WorkbenchI18nService service = WorkbenchI18nService.Instance;
            service.SetCulture("zh-CN");
            DocumentationPageViewModel viewModel = new("pkg-root", null, null, "");
            try
            {
                Assert.Equal("未知", viewModel.PackageVersion);
                Assert.Equal("选择一篇文档开始阅读。", viewModel.MarkdownText);
                Assert.Equal("尚未加载离线文档。", viewModel.StatusText);

                service.SetCulture("en-US");

                Assert.Equal("Unknown", viewModel.PackageVersion);
                Assert.Equal("Select a document to start reading.", viewModel.MarkdownText);
                Assert.Equal("Offline documents not loaded yet.", viewModel.StatusText);
            }
            finally
            {
                viewModel.Dispose();
                service.SetCulture("zh-CN");
            }
        });
    }
}
