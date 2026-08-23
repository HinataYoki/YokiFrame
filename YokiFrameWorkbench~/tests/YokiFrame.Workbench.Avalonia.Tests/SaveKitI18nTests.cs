using YokiFrame.Workbench.Avalonia.Services;
using YokiFrame.Workbench.Avalonia.ViewModels;
using Xunit;

namespace YokiFrame.Workbench.Avalonia.Tests;

/// <summary>验证 SaveKit 页面的占位文本会随语言切换刷新且筛选哨兵保持稳定。</summary>
public sealed class SaveKitI18nTests
{
    /// <summary>语言切换应重投影未连接与等待配置占位，Runtime 占位同步刷新。</summary>
    /// <remarks>必须先初始化 Headless UI 线程：SetCulture 只有在 UI 线程上才会同步触发
    /// CultureChanged，否则重投影被 Post 到无人泵的 Dispatcher 队列。</remarks>
    [Fact]
    public async Task SavePage_ReprojectsPlaceholdersOnCultureChange()
    {
        InstallerHeadlessTestApplication.EnsureInitialized();
        await global::Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            WorkbenchI18nService service = WorkbenchI18nService.Instance;
            service.SetCulture("zh-CN");
            SaveKitPageViewModel viewModel = new();
            try
            {
                Assert.Equal("未连接", viewModel.EngineLabel);
                Assert.Equal("等待项目配置", viewModel.StatusText);
                Assert.Equal("未连接", viewModel.RuntimeStatusText);
                Assert.Equal(SaveKitPageViewModel.FILTER_ALL, viewModel.Filter);

                service.SetCulture("en-US");

                Assert.Equal("Not connected", viewModel.EngineLabel);
                Assert.Equal("Waiting for project settings", viewModel.StatusText);
                Assert.Equal("Not connected", viewModel.RuntimeStatusText);
                // 筛选哨兵值不随语言变化，保证筛选语义稳定。
                Assert.Equal(SaveKitPageViewModel.FILTER_ALL, viewModel.Filter);
                Assert.True(viewModel.IsAllFilter);
            }
            finally
            {
                viewModel.Dispose();
                service.SetCulture("zh-CN");
            }
        });
    }
}
