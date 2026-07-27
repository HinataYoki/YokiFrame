using System.Text.RegularExpressions;

namespace YokiFrame.Workbench.Avalonia.Tests;

/// <summary>
/// 文档 WebView 与 Avalonia 宿主之间的主题和底色契约测试。
/// </summary>
public sealed class DocumentationWebThemeContractTests
{
    /// <summary>
    /// 验证原生 WebView 后方使用 Workbench 面板色，避免创建和导航阶段露出异色矩形。
    /// </summary>
    [Fact]
    public void DocumentationWebViewUsesWorkbenchPanelSurface()
    {
        var xaml = ReadWorkbenchFile("Views", "Pages", "DocumentationPageView.axaml");

        Assert.Contains("Background=\"{DynamicResource Brush.Surface.Panel}\"", xaml);
        Assert.Contains("NativeWebView", xaml);
    }

    /// <summary>
    /// 验证文档页面从 Avalonia 主题资源同步主题和宿主底色，不再固定为暗色页面。
    /// </summary>
    [Fact]
    public void DocumentationWebViewSynchronizesThemeAndHostSurface()
    {
        var source = ReadWorkbenchFile("Views", "Pages", "DocumentationPageView.axaml.cs");
        var script = ReadWorkbenchFile("Assets", "DocumentationWeb", "workbench-docs.js");
        var styles = ReadWorkbenchFile("Assets", "DocumentationWeb", "docs.css");

        Assert.Contains("ActualThemeVariantChanged += OnActualThemeVariantChanged", source);
        Assert.Contains("TryFindResource(\"Brush.Surface.Panel\"", source);
        Assert.Contains("yokiDocs.setTheme(", source);
        Assert.DoesNotContain("<html data-theme=\\\"dark\\\">", source);
        Assert.Contains("setTheme(value)", script);
        Assert.Contains("document.documentElement.dataset.theme = theme", script);
        Assert.Contains("--host-surface", script);
        Assert.Contains("background: var(--host-surface, var(--surface-1))", styles);
    }

    /// <summary>
    /// 验证所有异步 WebView 事件都把原生边界异常投影到页面状态和启动诊断。
    /// </summary>
    [Fact]
    public void DocumentationWebViewAsyncEventsContainFailureBoundaries()
    {
        var source = ReadWorkbenchFile("Views", "Pages", "DocumentationPageView.axaml.cs");

        Assert.Equal(4, Regex.Matches(source, "catch \\(Exception exception\\)").Count);
        Assert.Contains("ReportWebViewFailure(\"加载文档\"", source);
        Assert.Contains("ReportWebViewFailure(\"同步文档\"", source);
        Assert.Contains("ReportWebViewFailure(\"同步主题\"", source);
        Assert.Contains("ReportWebViewFailure(\"同步文档状态\"", source);
        Assert.Contains("mViewModel?.ReportViewError", source);
        Assert.Contains("WorkbenchStartupTrace.Mark", source);
    }

    /// <summary>
    /// 验证 WebView 的 catalog、document 与 theme payload 都使用 Native AOT JSON 元数据，不回退反射序列化。
    /// </summary>
    [Fact]
    public void DocumentationWebViewUsesNativeAotJsonMetadata()
    {
        var source = ReadWorkbenchFile("Views", "Pages", "DocumentationPageView.axaml.cs");

        Assert.Contains("DocumentationWebJsonContext.Default.DocumentationWebCatalogPayload", source);
        Assert.Contains("DocumentationWebJsonContext.Default.DocumentationWebDocumentPayload", source);
        Assert.Contains("DocumentationWebJsonContext.Default.DocumentationWebThemePayload", source);
        Assert.Contains("PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase", source);
        Assert.DoesNotContain("JsonSerializer.Serialize(catalog)", source);
        Assert.DoesNotContain("JsonSerializer.Serialize(document)", source);
        Assert.DoesNotContain("JsonSerializer.Serialize(theme)", source);
    }

    /// <summary>
    /// 验证文档三栏优先保障正文宽度，并在较窄视口依次收起页内导航和文档目录。
    /// </summary>
    [Fact]
    public void DocumentationReaderUsesResponsiveColumnLayout()
    {
        var styles = ReadWorkbenchFile("Assets", "DocumentationWeb", "docs.css");
        var tokens = ReadWorkbenchFile("Assets", "DocumentationWeb", "tokens.css");

        Assert.Contains("--doc-nav-width: 236px", tokens);
        Assert.Contains("--doc-toc-width: 208px", tokens);
        Assert.Contains("var(--doc-nav-width)", styles);
        Assert.Contains("var(--doc-toc-width)", styles);
        Assert.Contains("scrollbar-gutter: stable", styles);
        Assert.Contains("@media (max-width: 1319px)", styles);
        Assert.Contains("@media (max-width: 1039px)", styles);
        Assert.Contains(".doc-toc", styles);
        Assert.Contains(".doc-nav", styles);
        Assert.DoesNotContain("linear-gradient", styles);
    }

    /// <summary>
    /// 验证长文正文使用独立字号、舒展行高和受控阅读宽度，表格与长代码只在局部处理溢出。
    /// </summary>
    [Fact]
    public void DocumentationReaderPrioritizesLongFormReadability()
    {
        var tokens = ReadWorkbenchFile("Assets", "DocumentationWeb", "tokens.css");
        var styles = ReadWorkbenchFile("Assets", "DocumentationWeb", "docs.css");
        var markdown = ReadWorkbenchFile("Assets", "DocumentationWeb", "markdown.css");

        Assert.Contains("--doc-reading-width: 860px", tokens);
        Assert.Contains("--fs-doc-body: 15px", tokens);
        Assert.Contains("--lh-doc-body: 1.78", tokens);
        Assert.Contains("max-width: var(--doc-reading-width)", styles);
        Assert.Contains("font-size: var(--fs-doc-body)", markdown);
        Assert.Contains("line-height: var(--lh-doc-body)", markdown);
        Assert.Contains("overflow-wrap: anywhere", markdown);
        Assert.Contains(".md-table", markdown);
        Assert.Contains("overflow-x: auto", markdown);
        Assert.Contains("outline: 2px solid var(--primary-focus)", markdown);
    }

    /// <summary>
    /// 验证扁平阅读布局仍保留三栏、章节、API 和页内导航的语义色彩，不退化为纯 Markdown 外观。
    /// </summary>
    [Fact]
    public void DocumentationReaderKeepsSemanticColorWayfinding()
    {
        var tokens = ReadWorkbenchFile("Assets", "DocumentationWeb", "tokens.css");
        var styles = ReadWorkbenchFile("Assets", "DocumentationWeb", "docs.css");
        var markdown = ReadWorkbenchFile("Assets", "DocumentationWeb", "markdown.css");

        Assert.Contains("--doc-command-soft:", tokens);
        Assert.Contains("border-top: 2px solid var(--doc-core)", styles);
        Assert.Contains("border-top: 2px solid var(--primary)", styles);
        Assert.Contains("border-top: 2px solid var(--doc-command)", styles);
        Assert.Contains("background: var(--doc-command-soft)", styles);
        Assert.Contains(".md-h2::before", markdown);
        Assert.Contains(".md-h3::before", markdown);
        Assert.Contains("background: var(--primary-soft)", markdown);
        Assert.Contains("color: var(--primary-hover)", markdown);
    }

    /// <summary>
    /// 验证嵌入式文档页面拦截右键菜单，避免暴露浏览器刷新等宿主无关命令。
    /// </summary>
    [Fact]
    public void DocumentationReaderDisablesBrowserContextMenu()
    {
        var script = ReadWorkbenchFile("Assets", "DocumentationWeb", "workbench-docs.js");

        Assert.Contains("document.addEventListener('contextmenu'", script);
        Assert.Contains("event.preventDefault();", script);
    }

    /// <summary>
    /// 验证页内导航使用无链接按钮，避免 WebView 悬停时显示 about:blank 锚点状态条。
    /// </summary>
    [Fact]
    public void DocumentationTableOfContentsDoesNotExposeBrowserLinkPreview()
    {
        var script = ReadWorkbenchFile("Assets", "DocumentationWeb", "workbench-docs.js");
        var styles = ReadWorkbenchFile("Assets", "DocumentationWeb", "docs.css");

        Assert.Contains("<button type=\"button\" class=\"doc-toc-item", script);
        Assert.Contains("data-target=", script);
        Assert.Contains("link.dataset.target", script);
        Assert.DoesNotContain("href=\"#${esc(item.id)}\"", script);
        Assert.Contains("background: transparent", styles);
        Assert.Contains("cursor: pointer", styles);
    }

    /// <summary>
    /// 验证用户文档导航把 Kit 主页面收口为纯 Kit 名称；专题指南不属于左侧目录。
    /// </summary>
    [Fact]
    public void DocumentationNavigationUsesCompactUserFacingTitles()
    {
        var script = ReadWorkbenchFile("Assets", "DocumentationWeb", "workbench-docs.js");

        Assert.Contains("title.match(/^([a-z][a-z0-9]*kit)", script);
        Assert.Contains("return kitTitle[1]", script);
        Assert.Contains("function isNavigationDocument(document)", script);
        Assert.Contains("/Api/00-GettingStarted/FrameworkOverview.md", script);
        Assert.Contains("for (const document of navigationDocuments())", script);
        Assert.DoesNotContain("isGuide", script);
        Assert.Contains("return '框架概览'", script);
        Assert.DoesNotContain("Architecture_Avalonia_CSharp_Workbench", script);
    }

    /// <summary>
    /// 验证正文中的包内 Markdown 链接不会让 WebView 直接导航，而是交给受控文档选择流程。
    /// </summary>
    [Fact]
    public void DocumentationLinksStayInsideWorkbench()
    {
        var script = ReadWorkbenchFile("Assets", "DocumentationWeb", "workbench-docs.js");
        var source = ReadWorkbenchFile("Views", "Pages", "DocumentationPageView.axaml.cs");

        Assert.Contains("function resolveInternalMarkdownPath(href)", script);
        Assert.Contains("function bindInternalDocumentLinks()", script);
        Assert.Contains("event.preventDefault();", script);
        Assert.Contains("post({ type: 'select-document', relativePath: targetPath });", script);
        Assert.Contains("bindInternalDocumentLinks();", script);
        Assert.Contains("mViewModel.SelectDocument(path.GetString());", source);
    }

    /// <summary>
    /// 验证标题卡片只承载开篇 H1 与紧随导语，并从正文移除这两部分以避免重复展示。
    /// </summary>
    [Fact]
    public void DocumentationHeroConsumesLeadingTitleAndIntroduction()
    {
        var script = ReadWorkbenchFile("Assets", "DocumentationWeb", "workbench-docs.js");

        Assert.Contains("function documentPresentation(markdown)", script);
        Assert.Contains("bodyMarkdown: lines.slice(bodyIndex).join('\\n')", script);
        Assert.Contains("renderWithHeadings(presentation.bodyMarkdown)", script);
        Assert.Contains("presentation.summary.length > 0", script);
        Assert.DoesNotContain("function documentSummary(document)", script);
        Assert.DoesNotContain("documentSummary(activeDocument)", script);
    }

    /// <summary>
    /// 验证文档页面在紧凑标题栏提供关键词输入、显式搜索和 Enter 提交，并将空结果传达给阅读器。
    /// </summary>
    [Fact]
    public void DocumentationHeaderProvidesKeywordSearch()
    {
        var shell = ReadWorkbenchFile("Views", "WorkbenchShellView.axaml");
        var source = ReadWorkbenchFile("Views", "WorkbenchShellView.axaml.cs");
        var icons = ReadWorkbenchFile("Resources", "Icons.axaml");
        var script = ReadWorkbenchFile("Assets", "DocumentationWeb", "workbench-docs.js");
        var styles = ReadWorkbenchFile("Assets", "DocumentationWeb", "docs.css");
        var page = ReadWorkbenchFile("Views", "Pages", "DocumentationPageView.axaml.cs");

        Assert.Contains("IsVisible=\"{CompiledBinding IsDocumentationPage}\"", shell);
        Assert.Contains("DocumentationPage.SearchText, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged", shell);
        Assert.Contains("DocumentationPage.SearchCommand", shell);
        Assert.Contains("KeyDown=\"OnDocumentationSearchKeyDown\"", shell);
        Assert.Contains("workbench.docs.search", shell);
        Assert.Contains("OnDocumentationSearchKeyDown", source);
        Assert.Contains("Icon.Search", icons);
        Assert.Contains("mViewModel.SearchText", page);
        Assert.Contains("doc-nav-empty", script);
        Assert.Contains("doc-empty-article", script);
        Assert.Contains("doc-nav-empty", styles);
        Assert.Contains("doc-empty-article", styles);
    }

    /// <summary>
    /// 验证全文命中会把摘要投影到导航，并在正文中安全高亮后定位首个命中。
    /// </summary>
    [Fact]
    public void DocumentationSearchPresentsSnippetsAndLocatesFullTextMatches()
    {
        var page = ReadWorkbenchFile("Views", "Pages", "DocumentationPageView.axaml.cs");
        var script = ReadWorkbenchFile("Assets", "DocumentationWeb", "workbench-docs.js");
        var styles = ReadWorkbenchFile("Assets", "DocumentationWeb", "docs.css");

        Assert.Contains("mViewModel.SearchResults", page);
        Assert.Contains("result.Snippet", page);
        Assert.Contains("function documentSearchSnippet(document)", script);
        Assert.Contains("doc-nav-item-snippet", script);
        Assert.Contains("function highlightSearchMatches(root, query)", script);
        Assert.Contains("document.createTreeWalker", script);
        Assert.Contains("document.createElement('mark')", script);
        Assert.Contains("match.textContent =", script);
        Assert.Contains("function scrollToSearchMatch(match)", script);
        Assert.Contains("scrollToSearchMatch(firstMatch)", script);
        Assert.Contains(".doc-nav-item-snippet", styles);
        Assert.Contains(".doc-search-match", styles);
    }

    /// <summary>
    /// 验证文档目录为每类 Kit 使用独立 SVG 图标和主题语义色。
    /// </summary>
    [Fact]
    public void DocumentationNavigationUsesTauriKitIcons()
    {
        var script = ReadWorkbenchFile("Assets", "DocumentationWeb", "workbench-docs.js");
        var styles = ReadWorkbenchFile("Assets", "DocumentationWeb", "docs.css");

        Assert.Contains("event: '<svg", script);
        Assert.Contains("codegen: '<svg", script);
        Assert.Contains("inspector: '<svg", script);
        Assert.Contains("log: '<svg", script);
        Assert.Contains("pool: '<svg", script);
        Assert.Contains("singleton: '<svg", script);
        Assert.Contains("localization: '<svg", script);
        Assert.Contains("table: '<svg", script);
        Assert.Contains("toolclass: '<svg", script);
        Assert.Contains("identity.includes('codegenkit')", script);
        Assert.Contains("identity.includes('inspectorkit')", script);
        Assert.Contains("identity.includes('eventkit')", script);
        Assert.Contains("identity.includes('logkit')", script);
        Assert.Contains("identity.includes('toolclass')", script);
        Assert.Contains("identity.includes('uikit')", script);
        Assert.Contains("data-doc-icon-tone=\"eventkit\"", styles);
        Assert.Contains("data-doc-icon-tone=\"uikit\"", styles);
        Assert.Contains("data-doc-icon-tone=\"codegenkit\"", styles);
        Assert.Contains("data-doc-icon-tone=\"inspectorkit\"", styles);
        Assert.Contains("data-doc-icon-tone=\"toolclass\"", styles);
    }

    /// <summary>
    /// 验证文档树定义的四个独立图标键均可由 Avalonia 导航解析，避免视觉资源只在单端存在。
    /// </summary>
    [Fact]
    public void DocumentationIconKeysRemainAvailableToAvaloniaNavigation()
    {
        var resources = ReadWorkbenchFile("Resources", "Icons.axaml");
        var source = ReadWorkbenchFile("Components", "NavigationIcon.axaml.cs");

        Assert.Contains("Icon.Navigation.CodeGenKit", resources);
        Assert.Contains("Icon.Navigation.InspectorKit", resources);
        Assert.Contains("Icon.Navigation.LogKit", resources);
        Assert.Contains("Icon.Navigation.ToolClass", resources);
        Assert.Contains("Icon.Navigation.SingletonKit", resources);
        Assert.Contains("Icon.Navigation.LocalizationKit", resources);
        Assert.Contains("Icon.Navigation.SceneKit", resources);
        Assert.Contains("\"codegenkit\" => \"CodeGenKit\"", source);
        Assert.Contains("\"inspectorkit\" => \"InspectorKit\"", source);
        Assert.Contains("\"logkit\" => \"LogKit\"", source);
        Assert.Contains("\"toolclass\" => \"ToolClass\"", source);
        Assert.Contains("\"singleton\" or \"singletonkit\" => \"SingletonKit\"", source);
        Assert.Contains("\"localization\" or \"localizationkit\" => \"LocalizationKit\"", source);
        Assert.Contains("\"scene\" or \"scenekit\" => \"SceneKit\"", source);
    }

    /// <summary>
    /// 验证 Avalonia 左侧导航的浅深色图标颜色逐项跟随文档树 token，避免两端维护成独立色板。
    /// </summary>
    /// <param name="resourceSuffix">Avalonia 图标资源后缀。</param>
    /// <param name="cssVariable">文档树对应的 CSS token 名称。</param>
    [Theory]
    [InlineData("Framework", "--kit-icon-framework")]
    [InlineData("Docs", "--kit-icon-docs")]
    [InlineData("CodeGenKit", "--kit-icon-codegenkit")]
    [InlineData("InspectorKit", "--kit-icon-inspectorkit")]
    [InlineData("EventKit", "--kit-icon-eventkit")]
    [InlineData("Fsm", "--kit-icon-fsmkit")]
    [InlineData("LogKit", "--kit-icon-logkit")]
    [InlineData("PoolKit", "--kit-icon-poolkit")]
    [InlineData("ResKit", "--kit-icon-reskit")]
    [InlineData("SingletonKit", "--kit-icon-singletonkit")]
    [InlineData("ActionKit", "--kit-icon-actionkit")]
    [InlineData("AudioKit", "--kit-icon-audiokit")]
    [InlineData("LocalizationKit", "--kit-icon-localizationkit")]
    [InlineData("SaveKit", "--kit-icon-savekit")]
    [InlineData("SceneKit", "--kit-icon-scenekit")]
    [InlineData("SpatialKit", "--kit-icon-spatialkit")]
    [InlineData("TableKit", "--kit-icon-tablekit")]
    [InlineData("UIKit", "--kit-icon-uikit")]
    [InlineData("ToolClass", "--kit-icon-toolclass")]
    public void AvaloniaNavigationIconColorsMatchDocumentationTokens(string resourceSuffix, string cssVariable)
    {
        var tokens = ReadWorkbenchFile("Assets", "DocumentationWeb", "tokens.css");
        var colors = ReadWorkbenchFile("Resources", "Colors.axaml");

        Assert.Equal(
            ReadCssColor(tokens, ":root", cssVariable),
            ReadAvaloniaIconColor(colors, "Light", resourceSuffix));
        Assert.Equal(
            ReadCssColor(tokens, "html[data-theme=\"dark\"]", cssVariable),
            ReadAvaloniaIconColor(colors, "Dark", resourceSuffix));
    }

    /// <summary>
    /// 从指定 CSS 主题块读取某个图标颜色 token。
    /// </summary>
    /// <param name="tokens">完整 token 样式文本。</param>
    /// <param name="themeSelector">目标主题块的选择器。</param>
    /// <param name="cssVariable">目标 CSS token 名称。</param>
    /// <returns>标准化为大写的十六进制颜色。</returns>
    private static string ReadCssColor(string tokens, string themeSelector, string cssVariable)
    {
        var section = ReadSection(tokens, themeSelector, "}");
        var match = Regex.Match(section, Regex.Escape(cssVariable) + @":\s*(#[0-9a-fA-F]{6})\s*;");

        Assert.True(match.Success, "无法读取文档图标颜色 token: " + cssVariable);
        return match.Groups[1].Value.ToUpperInvariant();
    }

    /// <summary>
    /// 从指定 Avalonia 主题字典读取导航图标颜色。
    /// </summary>
    /// <param name="colors">完整 Avalonia 颜色资源文本。</param>
    /// <param name="themeName">目标主题字典名称。</param>
    /// <param name="resourceSuffix">目标图标资源后缀。</param>
    /// <returns>标准化为大写的十六进制颜色。</returns>
    private static string ReadAvaloniaIconColor(string colors, string themeName, string resourceSuffix)
    {
        var section = ReadSection(colors, "<ResourceDictionary x:Key=\"" + themeName + "\">", "</ResourceDictionary>");
        var pattern = "<SolidColorBrush x:Key=\"Brush\\.Icon\\." + Regex.Escape(resourceSuffix)
            + "\" Color=\"(#[0-9a-fA-F]{6})\" />";
        var match = Regex.Match(section, pattern);

        Assert.True(match.Success, "无法读取 Avalonia 导航图标颜色: " + resourceSuffix);
        return match.Groups[1].Value.ToUpperInvariant();
    }

    /// <summary>
    /// 截取从指定起始标记到首个结束标记之间的文本，用于轻量检查主题资源。
    /// </summary>
    /// <param name="content">待检查的完整文本。</param>
    /// <param name="startMarker">主题块起始标记。</param>
    /// <param name="endMarker">主题块结束标记。</param>
    /// <returns>包含起始标记与目标内容的主题文本。</returns>
    private static string ReadSection(string content, string startMarker, string endMarker)
    {
        var start = content.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.True(start >= 0, "无法定位主题资源起始标记: " + startMarker);

        var end = content.IndexOf(endMarker, start, StringComparison.Ordinal);
        Assert.True(end >= 0, "无法定位主题资源结束标记: " + endMarker);
        return content.Substring(start, end - start);
    }

    /// <summary>
    /// 从测试输出目录向上定位 Workbench 源文件，兼容 solution 与 Unity 工程两种运行位置。
    /// </summary>
    /// <param name="segments">Workbench Avalonia 项目内的相对路径片段。</param>
    /// <returns>指定源文件的完整文本。</returns>
    private static string ReadWorkbenchFile(params string[] segments)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            var sourceRoot = Directory.Exists(Path.Combine(directory.FullName, "src"))
                ? directory.FullName
                : Path.Combine(directory.FullName, "Assets", "YokiFrame", "YokiFrameWorkbench~");
            var path = Path.Combine(new[]
            {
                sourceRoot,
                "src",
                "YokiFrame.Workbench.Avalonia"
            }.Concat(segments).ToArray());
            if (File.Exists(path))
            {
                return File.ReadAllText(path);
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("无法定位 Workbench 页面文件: " + string.Join("/", segments));
    }

}
