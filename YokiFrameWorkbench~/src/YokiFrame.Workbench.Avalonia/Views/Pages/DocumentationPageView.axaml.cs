using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Styling;
using System.Text.Json;
using System.Text.Json.Serialization;
using YokiFrame.Workbench.Avalonia.Diagnostics;
using YokiFrame.Tooling.Application.Documentation;
using YokiFrame.Workbench.Avalonia.ViewModels;

namespace YokiFrame.Workbench.Avalonia.Views.Pages;

/// <summary>
/// 包内离线文档阅读页面的 XAML 宿主。
/// </summary>
public sealed partial class DocumentationPageView : UserControl
{
    private const string DOCUMENTATION_ASSEMBLY_NAME = "YokiFrame.Workbench.Avalonia";
    private const string YOKIFRAME_DIRECTORY_NAME = ".yokiframe";
    private const string WORKBENCH_DIRECTORY_NAME = "workbench";
    private const string WEBVIEW2_DIRECTORY_NAME = "webview2";

    private DocumentationPageViewModel? mViewModel;
    private bool mNavigationStarted;
    private bool mWebViewLoaded;

    /// <summary>
    /// 创建页面并加载编译后的 XAML。
    /// </summary>
    public DocumentationPageView()
    {
        InitializeComponent();
        DocumentationWebView.EnvironmentRequested += OnWebViewEnvironmentRequested;
        ActualThemeVariantChanged += OnActualThemeVariantChanged;
    }

    /// <summary>
    /// 在原生 WebView 创建环境前把 Windows WebView2 的可变浏览器数据移出受管 Runtime 目录。
    /// 每个项目使用独立目录，避免浏览器缓存污染 Installer 所有权清单或跨项目共享状态。
    /// </summary>
    /// <param name="sender">请求环境的原生 WebView。</param>
    /// <param name="args">平台专属环境创建参数。</param>
    private void OnWebViewEnvironmentRequested(object? sender, WebViewEnvironmentRequestedEventArgs args)
    {
        if (args is not WindowsWebView2EnvironmentRequestedEventArgs windowsArguments)
        {
            return;
        }

        var userDataFolder = ResolveWebView2UserDataFolder(global::YokiFrame.Workbench.Avalonia.Program.StartupOptions);
        if (string.IsNullOrWhiteSpace(userDataFolder))
        {
            return;
        }

        Directory.CreateDirectory(userDataFolder);
        windowsArguments.UserDataFolder = userDataFolder;
    }

    /// <summary>
    /// 计算当前 Workbench 项目的 WebView2 可变数据目录，设计期或未知启动上下文不配置路径。
    /// </summary>
    /// <param name="startupOptions">Avalonia 启动时解析出的项目上下文。</param>
    /// <returns>项目级 WebView2 数据目录；无法确定项目时返回 null。</returns>
    internal static string? ResolveWebView2UserDataFolder(ToolStartupOptions? startupOptions)
    {
        if (startupOptions == null || string.IsNullOrWhiteSpace(startupOptions.ProjectRoot))
        {
            return null;
        }

        return Path.Combine(
            startupOptions.ProjectRoot,
            YOKIFRAME_DIRECTORY_NAME,
            WORKBENCH_DIRECTORY_NAME,
            WEBVIEW2_DIRECTORY_NAME);
    }

    /// <summary>
    /// 在页面进入视觉树后绑定文档状态，并加载内嵌的 WebView 文档 HTML/CSS/JS 资源。
    /// </summary>
    /// <param name="sender">页面控件。</param>
    /// <param name="args">视觉树挂载事件参数。</param>
    private async void OnLoaded(object? sender, RoutedEventArgs args)
    {
        try
        {
            if (mViewModel == null)
            {
                mViewModel = DataContext as DocumentationPageViewModel;
                if (mViewModel != null)
                {
                    mViewModel.PropertyChanged += OnDocumentationPropertyChanged;
                }
            }

            if (mNavigationStarted)
            {
                return;
            }

            mNavigationStarted = true;
            DocumentationWebView.NavigateToString(await BuildDocumentHtmlAsync());
        }
        catch (Exception exception)
        {
            mNavigationStarted = false;
            ReportWebViewFailure("加载文档", exception);
        }
    }

    /// <summary>
    /// 标记 NativeWebView 已完成导航，并把当前目录和正文投影到浏览器端。
    /// </summary>
    /// <param name="sender">NativeWebView 控件。</param>
    /// <param name="args">导航完成事件参数。</param>
    private async void OnNavigationCompleted(object? sender, global::Avalonia.Controls.WebViewNavigationCompletedEventArgs args)
    {
        try
        {
            if (!args.IsSuccess)
            {
                mWebViewLoaded = false;
                mNavigationStarted = false;
                return;
            }

            mWebViewLoaded = true;
            await PushThemeAsync();
            await PushCatalogAsync();
            await PushCurrentDocumentAsync();
        }
        catch (Exception exception)
        {
            mWebViewLoaded = false;
            mNavigationStarted = false;
            ReportWebViewFailure("同步文档", exception);
        }
    }

    /// <summary>
    /// 响应 Workbench 主题变化，把当前主题和真实面板底色同步给原生 WebView 内的文档页面。
    /// </summary>
    /// <param name="sender">发生主题变化的文档页面。</param>
    /// <param name="args">主题变化事件参数。</param>
    private async void OnActualThemeVariantChanged(object? sender, EventArgs args)
    {
        try
        {
            await PushThemeAsync();
        }
        catch (Exception exception)
        {
            ReportWebViewFailure("同步主题", exception);
        }
    }

    /// <summary>
    /// 响应 ViewModel 的目录或正文变化，保持 WebView 与离线文档服务同代更新。
    /// </summary>
    /// <param name="sender">文档页面状态。</param>
    /// <param name="args">属性变化事件参数。</param>
    private async void OnDocumentationPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
    {
        try
        {
            if (!mWebViewLoaded)
            {
                return;
            }

            if (args.PropertyName is nameof(DocumentationPageViewModel.Documents)
                or nameof(DocumentationPageViewModel.PackageVersion))
            {
                await PushCatalogAsync();
                await PushCurrentDocumentAsync();
            }

            if (args.PropertyName is nameof(DocumentationPageViewModel.MarkdownText)
                or nameof(DocumentationPageViewModel.SelectedDocument))
            {
                await PushCurrentDocumentAsync();
            }
        }
        catch (Exception exception)
        {
            ReportWebViewFailure("同步文档状态", exception);
        }
    }

    /// <summary>
    /// 将 WebView 原生边界异常投影到文档状态和启动诊断，不让事件回调终止 Avalonia UI 线程。
    /// </summary>
    /// <param name="operation">失败的 WebView 操作。</param>
    /// <param name="exception">原生边界异常。</param>
    private void ReportWebViewFailure(string operation, Exception exception)
    {
        mViewModel?.ReportViewError(operation + "失败: " + exception.Message);
        WorkbenchStartupTrace.Mark("documentation.webview.failed." + operation + "." + exception.GetType().Name);
    }

    /// <summary>
    /// 接收浏览器端的文档选择消息，复用 ViewModel 的受控路径读取和版本控制。
    /// </summary>
    /// <param name="sender">NativeWebView 控件。</param>
    /// <param name="args">WebView 消息事件参数。</param>
    private void OnWebMessageReceived(object? sender, global::Avalonia.Controls.WebMessageReceivedEventArgs args)
    {
        if (mViewModel == null || string.IsNullOrWhiteSpace(args.Body))
        {
            return;
        }

        try
        {
            using var message = JsonDocument.Parse(args.Body);
            var root = message.RootElement;
            if (!root.TryGetProperty("type", out var type)
                || type.GetString() != "select-document"
                || !root.TryGetProperty("relativePath", out var path)
                || path.ValueKind != JsonValueKind.String)
            {
                return;
            }

            mViewModel.SelectDocument(path.GetString());
        }
        catch (JsonException)
        {
            // WebView 消息属于 UI 输入；非法载荷不进入文档服务，也不影响当前阅读状态。
        }
    }

    /// <summary>
    /// 把当前文档目录序列化后交给 WebView 内嵌文档脚本构建左侧导航。
    /// </summary>
    private async Task PushCatalogAsync()
    {
        if (!mWebViewLoaded || mViewModel == null)
        {
            return;
        }

        DocumentationWebCatalogPayload catalog = new(
            mViewModel.PackageVersion,
            mViewModel.SearchText,
            mViewModel.SearchResults
                .Where(static result => result.ItemKind == DocumentationSearchItemKind.Document)
                .Select(static result => new DocumentationWebSearchResultPayload(
                    result.RelativePath,
                    result.Snippet))
                .ToArray(),
            mViewModel.Documents.Select(static document => new DocumentationWebEntryPayload(
                document.Title,
                document.RelativePath,
                document.Group)).ToArray());
        var json = JsonSerializer.Serialize(
            catalog,
            DocumentationWebJsonContext.Default.DocumentationWebCatalogPayload);
        await InvokeScriptAsync("yokiDocs.setCatalog(" + json + ");");
    }

    /// <summary>
    /// 把当前正文和目录条目序列化后交给 WebView 内嵌 Markdown 渲染器。
    /// </summary>
    private async Task PushCurrentDocumentAsync()
    {
        if (!mWebViewLoaded || mViewModel?.SelectedDocument == null)
        {
            return;
        }

        DocumentationWebDocumentPayload document = new(
            mViewModel.SelectedDocument.Title,
            mViewModel.SelectedDocument.RelativePath,
            mViewModel.SelectedDocument.Group,
            mViewModel.MarkdownText);
        var json = JsonSerializer.Serialize(
            document,
            DocumentationWebJsonContext.Default.DocumentationWebDocumentPayload);
        await InvokeScriptAsync("yokiDocs.setDocument(" + json + ");");
    }

    /// <summary>
    /// 把 Avalonia 当前主题和面板色作为宿主视觉契约同步到浏览器端，避免原生矩形宿主露出异色边角。
    /// </summary>
    private async Task PushThemeAsync()
    {
        if (!mWebViewLoaded)
        {
            return;
        }

        DocumentationWebThemePayload theme = new(
            ResolveDocumentTheme(),
            ResolveHostSurfaceCssColor());
        var json = JsonSerializer.Serialize(
            theme,
            DocumentationWebJsonContext.Default.DocumentationWebThemePayload);
        await InvokeScriptAsync("yokiDocs.setTheme(" + json + ");");
    }

    /// <summary>
    /// 在 WebView 页面中执行一段脚本；导航尚未完成时静默跳过本次更新。
    /// </summary>
    /// <param name="script">要执行的 JavaScript 表达式。</param>
    private async Task InvokeScriptAsync(string script)
    {
        if (!mWebViewLoaded)
        {
            return;
        }

        await DocumentationWebView.InvokeScript(script);
    }

    /// <summary>
    /// 从 AvaloniaResource 读取内嵌文档资源并组装为单页 HTML，避免依赖文件系统路径。
    /// </summary>
    /// <returns>可由 NativeWebView 直接加载的 HTML。</returns>
    private async Task<string> BuildDocumentHtmlAsync()
    {
        var tokens = await ReadAssetAsync("tokens.css");
        var docs = await ReadAssetAsync("docs.css");
        var markdown = await ReadAssetAsync("markdown.css");
        var renderer = await ReadAssetAsync("markdown.js");
        var workbench = await ReadAssetAsync("workbench-docs.js");
        var theme = ResolveDocumentTheme();
        var hostSurface = ResolveHostSurfaceCssColor();
        return "<!doctype html><html data-theme=\"" + theme
            + "\" style=\"--host-surface:" + hostSurface + ";\"><head><meta charset=\"utf-8\"><style>"
            + tokens + docs + markdown
            + "</style></head><body><script>"
            + renderer + "\n" + workbench
            + "</script></body></html>";
    }

    /// <summary>
    /// 将 Avalonia 实际主题归一为文档 CSS 支持的 light 或 dark 标识。
    /// </summary>
    /// <returns>文档页面使用的主题名称。</returns>
    private string ResolveDocumentTheme()
    {
        return ActualThemeVariant == ThemeVariant.Light ? "light" : "dark";
    }

    /// <summary>
    /// 从当前主题资源读取 Workbench 面板真实颜色，保证 WebView 空白区域与外层面板无色差。
    /// </summary>
    /// <returns>浏览器可直接使用的 CSS 颜色；资源不可用时返回透明色。</returns>
    private string ResolveHostSurfaceCssColor()
    {
        if (!this.TryFindResource("Brush.Surface.Panel", ActualThemeVariant, out var resource)
            || resource is not ISolidColorBrush brush)
        {
            return "transparent";
        }

        var color = brush.Color;
        return color.A == byte.MaxValue
            ? $"#{color.R:X2}{color.G:X2}{color.B:X2}"
            : $"rgba({color.R},{color.G},{color.B},{color.A / 255d:0.###})";
    }

    /// <summary>
    /// 读取内嵌文档资源文本，并保留跨平台 AvaloniaResource 加载方式。
    /// </summary>
    /// <param name="fileName">DocumentationWeb 目录内的资源文件名。</param>
    /// <returns>UTF-8 文本内容。</returns>
    private static async Task<string> ReadAssetAsync(string fileName)
    {
        var uri = new Uri($"avares://{DOCUMENTATION_ASSEMBLY_NAME}/Assets/DocumentationWeb/{fileName}");
        await using var stream = AssetLoader.Open(uri);
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }

    /// <summary>描述 WebView 左侧目录与搜索结果的宿主 payload。</summary>
    private sealed record DocumentationWebCatalogPayload(
        string PackageVersion,
        string SearchQuery,
        DocumentationWebSearchResultPayload[] SearchResults,
        DocumentationWebEntryPayload[] Documents);

    /// <summary>描述 WebView 搜索结果摘要的宿主 payload。</summary>
    private sealed record DocumentationWebSearchResultPayload(string RelativePath, string Snippet);

    /// <summary>描述 WebView 目录中的单篇文档。</summary>
    private sealed record DocumentationWebEntryPayload(string Title, string RelativePath, string Group);

    /// <summary>描述 WebView 当前正文与 Markdown 内容。</summary>
    private sealed record DocumentationWebDocumentPayload(
        string Title,
        string RelativePath,
        string Group,
        string Markdown);

    /// <summary>描述 WebView 当前主题与宿主底色。</summary>
    private sealed record DocumentationWebThemePayload(string Theme, string HostSurface);

    /// <summary>为 Native AOT 文档页 payload 提供无反射 JSON 元数据。</summary>
    [JsonSourceGenerationOptions(
        GenerationMode = JsonSourceGenerationMode.Metadata,
        PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
    [JsonSerializable(typeof(DocumentationWebCatalogPayload))]
    [JsonSerializable(typeof(DocumentationWebDocumentPayload))]
    [JsonSerializable(typeof(DocumentationWebThemePayload))]
    private sealed partial class DocumentationWebJsonContext : JsonSerializerContext
    {
    }
}
