using YokiFrame.Tooling.Application.Models.SpatialKit;
using YokiFrame.Workbench.Avalonia.Services;
using YokiFrame.Workbench.Avalonia.ViewModels;
using Xunit;

namespace YokiFrame.Workbench.Avalonia.Tests;

/// <summary>验证 SpatialKit 页面的占位与健康摘要会随语言切换刷新。</summary>
public sealed class SpatialKitI18nTests
{
    /// <summary>语言切换应重投影未连接占位与已应用密度的健康文案。</summary>
    /// <remarks>必须先初始化 Headless UI 线程：SetCulture 只有在 UI 线程上才会同步触发
    /// CultureChanged，否则重投影被 Post 到无人泵的 Dispatcher 队列。</remarks>
    [Fact]
    public async Task SpatialPage_ReprojectsPlaceholdersAndHealthOnCultureChange()
    {
        InstallerHeadlessTestApplication.EnsureInitialized();
        await global::Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            WorkbenchI18nService service = WorkbenchI18nService.Instance;
            service.SetCulture("zh-CN");
            SpatialKitPageViewModel viewModel = new();
            try
            {
                // 未连接 Runtime 时显示等待占位。
                Assert.Equal("等待数据", viewModel.Source);
                Assert.Equal("等待密度数据", viewModel.DensitySummaryText);
                Assert.Equal("等待数据", viewModel.HealthText);

                // 切换语言后占位随语言重投影；哨兵状态（未曾应用密度）保持不变。
                service.SetCulture("en-US");

                Assert.Equal("Waiting for data", viewModel.Source);
                Assert.Equal("Waiting for density data", viewModel.DensitySummaryText);
                Assert.Equal("Waiting for data", viewModel.HealthText);
            }
            finally
            {
                viewModel.Dispose();
                service.SetCulture("zh-CN");
            }
        });
    }
}
