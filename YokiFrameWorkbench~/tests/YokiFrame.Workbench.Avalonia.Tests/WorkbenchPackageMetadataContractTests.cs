using Avalonia.Threading;
using YokiFrame.Tooling.Application.Packages;
using YokiFrame.Workbench.Avalonia.Services;
using YokiFrame.Workbench.Avalonia.ViewModels;

namespace YokiFrame.Workbench.Avalonia.Tests;

/// <summary>
/// 覆盖包元数据向侧栏版本和 GitHub 外部链接命令的投影契约。
/// </summary>
public sealed class WorkbenchPackageMetadataContractTests
{
    /// <summary>
    /// 验证侧栏版本来自注入的 package.json 元数据，每次点击都转发真实仓库 URI。
    /// </summary>
    [Fact]
    public async Task ShellProjectsPackageMetadataAndOpensRepository()
    {
        var openedUris = new List<Uri>();
        var metadata = new YokiFramePackageMetadata(
            "2.0.0-test",
            new Uri("https://github.com/HinataYoki/YokiFrame"));
        using var viewModel = new WorkbenchShellViewModel(
            () => { },
            _ => { },
            (_, _) => Task.CompletedTask,
            metadata,
            uri =>
            {
                openedUris.Add(uri);
                return Task.CompletedTask;
            });

        await viewModel.OpenRepositoryCommand.ExecuteAsync();
        await viewModel.OpenRepositoryCommand.ExecuteAsync();

        Assert.Equal("v2.0.0-test", viewModel.VersionText);
        Assert.Equal(metadata.RepositoryUri.AbsoluteUri, viewModel.RepositoryUrl);
        Assert.Equal(new[] { metadata.RepositoryUri, metadata.RepositoryUri }, openedUris);
    }

    /// <summary>
    /// 验证未注入包元数据时不显示硬编码版本，也不启用外部链接。中文占位必须在固定文化下断言。
    /// </summary>
    [Fact]
    public void ShellWithoutPackageMetadataUsesSafeFallback()
    {
        InstallerHeadlessTestApplication.EnsureInitialized();
        Dispatcher.UIThread.Invoke(static () => WorkbenchI18nService.Instance.SetCulture("zh-CN"));
        WorkbenchShellViewModel? viewModel = null;
        try
        {
            viewModel = new WorkbenchShellViewModel(() => { }, _ => { }, (_, _) => Task.CompletedTask);
            Assert.Equal("版本未知", viewModel.VersionText);
            Assert.Equal(string.Empty, viewModel.RepositoryUrl);
            Assert.False(viewModel.OpenRepositoryCommand.CanExecute(null));
        }
        finally
        {
            viewModel?.Dispose();
            Dispatcher.UIThread.Invoke(static () => WorkbenchI18nService.Instance.SetCulture("zh-CN"));
        }
    }
}
