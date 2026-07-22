using YokiFrame.Tooling.Application.Packages;
using YokiFrame.Workbench.Avalonia.ViewModels;

namespace YokiFrame.Workbench.Avalonia.Tests;

/// <summary>
/// 覆盖包元数据向侧栏版本和 GitHub 外部链接命令的投影契约。
/// </summary>
public sealed class WorkbenchPackageMetadataContractTests
{
    /// <summary>
    /// 验证侧栏版本来自注入的 package.json 元数据，点击命令会转交真实仓库 URI。
    /// </summary>
    [Fact]
    public async Task ShellProjectsPackageMetadataAndOpensRepository()
    {
        Uri? openedUri = null;
        var metadata = new YokiFramePackageMetadata(
            "2.0.0-test",
            new Uri("https://github.com/HinataYoki/YokiFrame"));
        var viewModel = new WorkbenchShellViewModel(
            () => { },
            _ => { },
            (_, _) => Task.CompletedTask,
            metadata,
            uri =>
            {
                openedUri = uri;
                return Task.CompletedTask;
            });

        await viewModel.OpenRepositoryCommand.ExecuteAsync();

        Assert.Equal("v2.0.0-test", viewModel.VersionText);
        Assert.Equal(metadata.RepositoryUri.AbsoluteUri, viewModel.RepositoryUrl);
        Assert.Equal(metadata.RepositoryUri, openedUri);
    }

    /// <summary>
    /// 验证未注入包元数据的设计时 Shell 不显示硬编码版本，也不会启用外部链接。
    /// </summary>
    [Fact]
    public void ShellWithoutPackageMetadataUsesSafeFallback()
    {
        var viewModel = new WorkbenchShellViewModel(() => { }, _ => { }, (_, _) => Task.CompletedTask);

        Assert.Equal("版本未知", viewModel.VersionText);
        Assert.Equal(string.Empty, viewModel.RepositoryUrl);
        Assert.False(viewModel.OpenRepositoryCommand.CanExecute(null));
    }
}
