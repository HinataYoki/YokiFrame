using System.IO;
using YokiFrame.Tooling.Application.Services.LocalizationKit;
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
            string previousCulture = service.CurrentCultureName;
            string projectRoot = Path.Combine(Path.GetTempPath(), "yokiframe-localization-i18n-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(projectRoot);
            LocalizationKitPageViewModel? viewModel = null;
            try
            {
                service.SetCulture("zh-CN");
                viewModel = new LocalizationKitPageViewModel(projectRoot, new LocalizationKitApplicationService());
                Assert.Equal("等待刷新", viewModel.StatusText);
                Assert.Equal("全部", viewModel.LanguageOptions[0]);
                Assert.False(viewModel.HasActiveFilters);

                service.SetCulture("en-US");

                Assert.Equal("Waiting for refresh", viewModel.StatusText);
                Assert.Equal("All", viewModel.LanguageOptions[0]);
                Assert.False(viewModel.HasActiveFilters);
                string selectedLanguage = viewModel.SelectedLanguage.Trim();
                Assert.True(
                    string.Equals(selectedLanguage, "all", StringComparison.OrdinalIgnoreCase)
                    || selectedLanguage == "全部");
            }
            finally
            {
                viewModel?.Dispose();
                service.SetCulture(previousCulture);
                DeleteTemporaryRoot(projectRoot);
            }
        });
    }

    /// <summary>删除临时项目目录。清理失败只记录，不覆盖断言异常。</summary>
    /// <param name="projectRoot">临时项目根目录。</param>
    private static void DeleteTemporaryRoot(string projectRoot)
    {
        try
        {
            if (Directory.Exists(projectRoot))
            {
                Directory.Delete(projectRoot, true);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine(exception);
        }
    }
}
