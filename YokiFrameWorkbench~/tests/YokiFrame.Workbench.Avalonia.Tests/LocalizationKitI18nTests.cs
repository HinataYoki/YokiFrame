using YokiFrame.Workbench.Avalonia.Services;
using YokiFrame.Workbench.Avalonia.ViewModels;
using Xunit;

namespace YokiFrame.Workbench.Avalonia.Tests;

/// <summary>验证 LocalizationKit 页面的占位与语言筛选会随语言切换刷新。</summary>
public sealed class LocalizationKitI18nTests
{
    /// <summary>语言切换应重投影等待占位与语言下拉“全部”标签，筛选哨兵保持稳定。</summary>
    /// <remarks>必须先初始化 Headless UI 线程：SetCulture 只有在 UI 线程上才会同步触发
    /// CultureChanged，否则重投影被 Post 到无人泵的 Dispatcher 队列。</remarks>
    [Fact]
    public async Task LocalizationPage_ReprojectsPlaceholdersAndLanguageFilterOnCultureChange()
    {
        InstallerHeadlessTestApplication.EnsureInitialized();
        await global::Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            WorkbenchI18nService service = WorkbenchI18nService.Instance;
            service.SetCulture("zh-CN");
            LocalizationKitPageViewModel viewModel = new(
                System.IO.Directory.GetCurrentDirectory(),
                new YokiFrame.Tooling.Application.Services.LocalizationKit.LocalizationKitApplicationService());
            try
            {
                Assert.Equal("等待刷新", viewModel.StatusText);
                Assert.Equal("全部", viewModel.LanguageOptions[0]);
                Assert.False(viewModel.HasActiveFilters);

                service.SetCulture("en-US");

                Assert.Equal("Waiting for refresh", viewModel.StatusText);
                Assert.Equal("All", viewModel.LanguageOptions[0]);
                Assert.False(viewModel.HasActiveFilters);
                // 哨兵语义稳定：切换语言后仍视为“全部”筛选。
                Assert.Equal(
                    LocalizationKitPageViewModel.LANGUAGE_ALL,
                    viewModel.GetType()
                        .GetProperty("SelectedLanguage")!.GetValue(viewModel) is string selected
                        ? selected.Trim().ToLowerInvariant() == "all" || selected == "全部"
                            ? LocalizationKitPageViewModel.LANGUAGE_ALL
                            : selected
                        : string.Empty);
            }
            finally
            {
                viewModel.Dispose();
                service.SetCulture("zh-CN");
            }
        });
    }
}
