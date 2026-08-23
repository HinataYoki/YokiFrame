using YokiFrame.Workbench.Avalonia.Services;
using YokiFrame.Workbench.Avalonia.ViewModels;
using Xunit;

namespace YokiFrame.Workbench.Avalonia.Tests;

/// <summary>验证 LogKit 页面的占位、能力说明与筛选选项会随语言切换刷新。</summary>
public sealed class LogKitI18nTests
{
    /// <summary>语言切换应重投影未连接占位与计算文本，且不改变等级哨兵值。</summary>
    /// <remarks>必须先初始化 Headless UI 线程：SetCulture 只有在 UI 线程上才会同步触发
    /// CultureChanged，否则重投影被 Post 到无人泵的 Dispatcher 队列。</remarks>
    [Fact]
    public async Task LogPage_ReprojectsPlaceholdersAndComputedTextsOnCultureChange()
    {
        InstallerHeadlessTestApplication.EnsureInitialized();
        await global::Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            WorkbenchI18nService service = WorkbenchI18nService.Instance;
            service.SetCulture("zh-CN");
            LogKitPageViewModel viewModel = new();
            try
            {
                // 未连接 Runtime 时显示等待/未安装占位。
                Assert.Equal("等待数据", viewModel.Source);
                Assert.Equal("未安装", viewModel.LoggerName);
                Assert.Equal("已停用", viewModel.RuntimeStatusText);
                Assert.Equal("保存", viewModel.SaveSettingsButtonText);
                Assert.Contains("全部", viewModel.HistoryLevelOptions);
                Assert.Equal(LogKitPageViewModel.HISTORY_LEVEL_ALL, viewModel.SelectedHistoryLevel);

                service.SetCulture("en-US");

                Assert.Equal("Waiting for data", viewModel.Source);
                Assert.Equal("Not installed", viewModel.LoggerName);
                Assert.Equal("Disabled", viewModel.RuntimeStatusText);
                Assert.Equal("Save", viewModel.SaveSettingsButtonText);
                Assert.Contains("All", viewModel.HistoryLevelOptions);
                // 等级哨兵值不随语言变化，保证筛选语义稳定。
                Assert.Equal(LogKitPageViewModel.HISTORY_LEVEL_ALL, viewModel.SelectedHistoryLevel);
            }
            finally
            {
                viewModel.Dispose();
                service.SetCulture("zh-CN");
            }
        });
    }
}
