using YokiFrame.Workbench.Avalonia.Services;
using YokiFrame.Workbench.Avalonia.ViewModels;
using Xunit;

namespace YokiFrame.Workbench.Avalonia.Tests;

/// <summary>验证 ResKit 页面的占位与动态文案会随语言切换刷新。</summary>
public sealed class ResKitI18nTests
{
    /// <summary>语言切换应重投影未连接占位、按钮文本和来源空闲提示。</summary>
    /// <remarks>必须先初始化 Headless UI 线程：SetCulture 只有在 UI 线程上才会同步触发
    /// CultureChanged，否则重投影被 Post 到无人泵的 Dispatcher 队列。</remarks>
    [Fact]
    public async Task ResPage_ReprojectsPlaceholdersOnCultureChange()
    {
        InstallerHeadlessTestApplication.EnsureInitialized();
        await global::Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            WorkbenchI18nService service = WorkbenchI18nService.Instance;
            service.SetCulture("zh-CN");
            ResKitPageViewModel viewModel = new();
            try
            {
                Assert.Equal("等待数据", viewModel.Source);
                Assert.Equal("启用定位", viewModel.TrackingButtonText);
                Assert.Contains("选择资源后", viewModel.SourceStatusText);

                service.SetCulture("en-US");

                Assert.Equal("Waiting for data", viewModel.Source);
                Assert.Equal("Enable location", viewModel.TrackingButtonText);
                Assert.Contains("Select a resource", viewModel.SourceStatusText);
            }
            finally
            {
                viewModel.Dispose();
                service.SetCulture("zh-CN");
            }
        });
    }
}
