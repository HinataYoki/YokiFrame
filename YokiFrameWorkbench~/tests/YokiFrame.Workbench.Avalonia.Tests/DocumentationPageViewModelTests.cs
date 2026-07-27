using YokiFrame.Tooling.Application.Documentation;
using YokiFrame.Workbench.Avalonia.ViewModels;

namespace YokiFrame.Workbench.Avalonia.Tests;

/// <summary>
/// 覆盖文档标题栏关键词搜索与 WebView 目录投影所依赖的 ViewModel 行为。
/// </summary>
public sealed class DocumentationPageViewModelTests
{
    /// <summary>
    /// 验证 WebView 宿主异常复用现有文档状态，不需要额外的错误状态模型。
    /// </summary>
    [Fact]
    public void ReportViewErrorProjectsNativeFailureToStatusText()
    {
        DocumentationPageViewModel viewModel = new(string.Empty);

        viewModel.ReportViewError("文档视图加载失败: test");

        Assert.Equal("文档视图加载失败: test", viewModel.StatusText);
    }

    /// <summary>
    /// 验证关键词仅存在于 Markdown 正文时，全文搜索仍会将对应文档重新投影到导航目录。
    /// </summary>
    [Fact]
    public async Task FullTextSearchAddsBodyMatchesToNavigationDocuments()
    {
        var packageRoot = Path.Combine(Path.GetTempPath(), "yokiframe-docs-search-" + Guid.NewGuid().ToString("N"));
        try
        {
            WritePackageFile(packageRoot, "package.json", "{\"name\":\"com.hinatayoki.yokiframe\",\"version\":\"2.0.0-test\"}");
            WritePackageFile(
                packageRoot,
                "Documentation~/Api/02-Core/FsmKit.md",
                "# FsmKit 指南\n\n这是只存在于正文中的山河契约。\n");
            WritePackageFile(
                packageRoot,
                "Documentation~/Api/03-Tool/AudioKit.md",
                "# AudioKit 指南\n\n音频总线说明。\n");
            DocumentationPageViewModel viewModel = new(
                packageRoot,
                new OfflineDocumentationService(packageRoot));

            await viewModel.EnsureLoadedAsync();
            viewModel.SearchText = "山河契约";
            Assert.Empty(viewModel.Documents);

            await viewModel.SearchCommand.ExecuteAsync();

            var document = Assert.Single(viewModel.Documents);
            Assert.Equal("FsmKit 指南", document.Title);
            Assert.Single(viewModel.SearchResults, static result => result.ItemKind == DocumentationSearchItemKind.Document);

            viewModel.SearchText = string.Empty;
            Assert.Equal(2, viewModel.Documents.Count);
            Assert.Empty(viewModel.SearchResults);
        }
        finally
        {
            if (Directory.Exists(packageRoot))
            {
                Directory.Delete(packageRoot, recursive: true);
            }
        }
    }

    /// <summary>
    /// 验证正文内部链接可以选择被当前关键词筛选隐藏的文档，并恢复完整导航目录。
    /// </summary>
    [Fact]
    public async Task SelectDocumentRestoresFullCatalogForInternalDocumentLinks()
    {
        var packageRoot = Path.Combine(Path.GetTempPath(), "yokiframe-docs-link-" + Guid.NewGuid().ToString("N"));
        try
        {
            WritePackageFile(packageRoot, "package.json", "{\"name\":\"com.hinatayoki.yokiframe\",\"version\":\"2.0.0-test\"}");
            WritePackageFile(packageRoot, "Documentation~/Api/02-Core/FsmKit.md", "# FsmKit\n");
            WritePackageFile(packageRoot, "Documentation~/Api/03-Tool/AudioKit.md", "# AudioKit\n");
            DocumentationPageViewModel viewModel = new(
                packageRoot,
                new OfflineDocumentationService(packageRoot));

            await viewModel.EnsureLoadedAsync();
            viewModel.SearchText = "AudioKit";
            Assert.Single(viewModel.Documents);

            viewModel.SelectDocument("Documentation~/Api/02-Core/FsmKit.md");

            Assert.Equal(string.Empty, viewModel.SearchText);
            Assert.Equal(2, viewModel.Documents.Count);
            Assert.Equal("FsmKit", viewModel.SelectedDocument?.Title);
        }
        finally
        {
            if (Directory.Exists(packageRoot))
            {
                Directory.Delete(packageRoot, recursive: true);
            }
        }
    }

    /// <summary>
    /// 验证 Guides 可由正文链接选择，但不会进入左侧目录或全文搜索。
    /// </summary>
    [Fact]
    public async Task GuidesRemainReachableButStayOutOfNavigation()
    {
        var packageRoot = Path.Combine(Path.GetTempPath(), "yokiframe-docs-guides-" + Guid.NewGuid().ToString("N"));
        try
        {
            WritePackageFile(packageRoot, "package.json", "{\"name\":\"com.hinatayoki.yokiframe\",\"version\":\"2.0.0-test\"}");
            WritePackageFile(packageRoot, "Documentation~/Api/02-Core/FsmKit.md", "# FsmKit\n");
            WritePackageFile(
                packageRoot,
                "Documentation~/Guides/Deep-Reference.md",
                "# 深度指南\n\nGuideOnlyToken\n");
            DocumentationPageViewModel viewModel = new(
                packageRoot,
                new OfflineDocumentationService(packageRoot));

            await viewModel.EnsureLoadedAsync();

            Assert.Single(viewModel.Documents);
            Assert.DoesNotContain(
                viewModel.Documents,
                static document => document.RelativePath.Contains("/Guides/", StringComparison.OrdinalIgnoreCase));

            viewModel.SearchText = "GuideOnlyToken";
            await viewModel.SearchCommand.ExecuteAsync();

            Assert.Empty(viewModel.Documents);
            Assert.DoesNotContain(
                viewModel.SearchResults,
                static result => result.RelativePath.EndsWith("/Guides/Deep-Reference.md", StringComparison.Ordinal));

            viewModel.SelectDocument("Documentation~/Guides/Deep-Reference.md");

            Assert.Equal("深度指南", viewModel.SelectedDocument?.Title);
            Assert.DoesNotContain(
                viewModel.Documents,
                static document => document.RelativePath.Contains("/Guides/", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            if (Directory.Exists(packageRoot))
            {
                Directory.Delete(packageRoot, recursive: true);
            }
        }
    }

    /// <summary>
    /// 向临时包根写入一个文件，并保证其父目录已经存在。
    /// </summary>
    /// <param name="packageRoot">测试用 YokiFrame 包根。</param>
    /// <param name="relativePath">相对包根的文件路径。</param>
    /// <param name="content">待写入的 UTF-8 文本。</param>
    private static void WritePackageFile(string packageRoot, string relativePath, string content)
    {
        var path = Path.Combine(packageRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }
}
