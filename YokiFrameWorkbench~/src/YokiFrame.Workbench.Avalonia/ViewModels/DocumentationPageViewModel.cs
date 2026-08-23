using System.Windows.Input;
using YokiFrame.Tooling.Application.Documentation;
using YokiFrame.Workbench.Avalonia.Services;

namespace YokiFrame.Workbench.Avalonia.ViewModels;

/// <summary>
/// 把包内离线文档目录、正文、页内导航和代码复制投影为专用页面状态。
/// </summary>
public sealed class DocumentationPageViewModel : ViewModelBase
{
    private readonly OfflineDocumentationService? mDocumentationService;
    private readonly Func<string, Task>? mCopyTextAsync;
    private readonly string mInitializationError;
    private DocumentationCatalog? mCatalog;
    private IReadOnlyList<DocumentationIndexEntry> mDocuments = Array.Empty<DocumentationIndexEntry>();
    private IReadOnlyList<DocumentationSearchResult> mSearchResults = Array.Empty<DocumentationSearchResult>();
    private IReadOnlyList<DocumentationHeading> mTableOfContents = Array.Empty<DocumentationHeading>();
    private IReadOnlyList<DocumentationCodeBlock> mCodeBlocks = Array.Empty<DocumentationCodeBlock>();
    private IReadOnlyList<DocumentationBlock> mBlocks = Array.Empty<DocumentationBlock>();
    private DocumentationIndexEntry? mSelectedDocument;
    private DocumentationCodeBlock? mSelectedCodeBlock;
    private string mSearchText = string.Empty;
    private string mPackageVersion = GetString(UnknownVersionKey, "未知");
    private string mMarkdownText = GetString(MarkdownPlaceholderKey, "选择一篇文档开始阅读。");
    private string mStatusText = GetString(NotLoadedKey, "尚未加载离线文档。");
    private int mLoadStarted;
    private int mDocumentLoadVersion;
    private int mDisposed;
    /// <summary>当前状态文本是否处于“尚未加载离线文档”占位；仅占位随语言切换重投影。</summary>
    private bool mIsWaitingStatus = true;

    /// <summary>按当前语言重新投影未加载状态的占位文本；已加载内容为 payload 数据不变。</summary>
    private void OnCultureChanged()
    {
        // 目录尚未加载时，版本、正文与状态均为占位，随语言重投影。
        if (mCatalog == null)
        {
            PackageVersion = GetString(UnknownVersionKey, "未知");
            MarkdownText = GetString(MarkdownPlaceholderKey, "选择一篇文档开始阅读。");
            if (mIsWaitingStatus)
            {
                StatusText = GetString(NotLoadedKey, "尚未加载离线文档。");
            }
        }
    }

    /// <summary>写入状态文本并维护“尚未加载”占位标记。</summary>
    /// <param name="text">新的状态文本。</param>
    /// <param name="isWaitingStatus">是否为尚未加载的占位状态。</param>
    private void SetStatus(string text, bool isWaitingStatus = false)
    {
        mIsWaitingStatus = isWaitingStatus;
        StatusText = text;
    }

    /// <summary>从当前语言资源读取 Documentation 文案，保留测试与无资源环境的中文兜底。</summary>
    private static string GetString(string key, string fallback)
    {
        return WorkbenchI18nService.Instance.GetString(key, fallback);
    }

    /// <summary>未知版本占位资源 key。</summary>
    private const string UnknownVersionKey = "String.Documentation.UnknownVersion";

    /// <summary>正文占位资源 key。</summary>
    private const string MarkdownPlaceholderKey = "String.Documentation.MarkdownPlaceholder";

    /// <summary>尚未加载状态占位资源 key。</summary>
    private const string NotLoadedKey = "String.Documentation.NotLoaded";

    /// <summary>无文档服务提示资源 key。</summary>
    private const string NoServiceKey = "String.Documentation.NoService";

    /// <summary>正在扫描提示资源 key。</summary>
    private const string ScanningKey = "String.Documentation.Scanning";

    /// <summary>目录加载失败模板资源 key。</summary>
    private const string LoadFailedTemplateKey = "String.Documentation.LoadFailedTemplate";

    /// <summary>搜索不可用提示资源 key。</summary>
    private const string SearchUnavailableKey = "String.Documentation.SearchUnavailable";

    /// <summary>目录未就绪提示资源 key。</summary>
    private const string CatalogNotReadyKey = "String.Documentation.CatalogNotReady";

    /// <summary>搜索无结果提示资源 key。</summary>
    private const string SearchNoMatchKey = "String.Documentation.SearchNoMatch";

    /// <summary>搜索完成模板资源 key。</summary>
    private const string SearchDoneTemplateKey = "String.Documentation.SearchDoneTemplate";

    /// <summary>搜索失败模板资源 key。</summary>
    private const string SearchFailedTemplateKey = "String.Documentation.SearchFailedTemplate";

    /// <summary>文档读取失败模板资源 key。</summary>
    private const string ReadFailedTemplateKey = "String.Documentation.ReadFailedTemplate";

    /// <summary>代码复制成功提示资源 key。</summary>
    private const string CopiedKey = "String.Documentation.Copied";

    /// <summary>复制失败模板资源 key。</summary>
    private const string CopyFailedTemplateKey = "String.Documentation.CopyFailedTemplate";

    /// <summary>目录加载完成模板资源 key。</summary>
    private const string LoadedTemplateKey = "String.Documentation.LoadedTemplate";

    /// <summary>
    /// 创建离线文档页面状态；服务为空时保留可诊断的不可用页面。
    /// </summary>
    /// <param name="sourcePackageRoot">启动入口解析出的真实 YokiFrame 包根。</param>
    /// <param name="documentationService">只从受控包内根读取文档的 Application 服务。</param>
    /// <param name="copyTextAsync">平台剪贴板写入回调。</param>
    /// <param name="initializationError">文档服务创建失败时的可显示原因。</param>
    public DocumentationPageViewModel(
        string sourcePackageRoot,
        OfflineDocumentationService? documentationService = null,
        Func<string, Task>? copyTextAsync = null,
        string initializationError = "")
    {
        SourcePackageRoot = sourcePackageRoot ?? string.Empty;
        mDocumentationService = documentationService;
        mCopyTextAsync = copyTextAsync;
        mInitializationError = initializationError ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(mInitializationError))
        {
            // 初始化失败信息是可诊断结果，清除占位标记。
            SetStatus(mInitializationError);
        }
        // 订阅全局语言切换；对应解除订阅在 Dispose，由 WorkbenchWindow 关闭流程统一调用。
        WorkbenchI18nService.Instance.CultureChanged += OnCultureChanged;
        SearchCommand = new AsyncRelayCommand(SearchAsync, CanSearch);
        ReloadCommand = new AsyncRelayCommand(ReloadAsync);
        CopyCodeCommand = new AsyncRelayCommand(CopySelectedCodeAsync, CanCopySelectedCode);
    }

    /// <summary>获取启动入口解析出的真实 YokiFrame 包根。</summary>
    public string SourcePackageRoot { get; }

    /// <summary>获取当前 package.json 版本。</summary>
    public string PackageVersion { get => mPackageVersion; private set => SetProperty(ref mPackageVersion, value); }

    /// <summary>获取经过本地筛选的文档目录。</summary>
    public IReadOnlyList<DocumentationIndexEntry> Documents
    {
        get => mDocuments;
        private set => SetProperty(ref mDocuments, value);
    }

    /// <summary>获取全文与 API 搜索结果。</summary>
    public IReadOnlyList<DocumentationSearchResult> SearchResults
    {
        get => mSearchResults;
        private set => SetProperty(ref mSearchResults, value);
    }

    /// <summary>获取或设置目录筛选和全文搜索关键词。</summary>
    public string SearchText
    {
        get => mSearchText;
        set
        {
            if (SetProperty(ref mSearchText, value ?? string.Empty))
            {
                SearchResults = Array.Empty<DocumentationSearchResult>();
                ApplyCatalogFilter();
                SearchCommand.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>获取或设置当前阅读的文档条目。</summary>
    public DocumentationIndexEntry? SelectedDocument
    {
        get => mSelectedDocument;
        set
        {
            if (SetProperty(ref mSelectedDocument, value) && value != null)
            {
                if (Volatile.Read(ref mDisposed) == 0)
                {
                    _ = LoadDocumentAsync(value.RelativePath);
                }
            }
        }
    }

    /// <summary>
    /// 从文档目录或正文内部链接选择目标页面；目标被关键词筛选隐藏时恢复完整目录后再加载。
    /// </summary>
    /// <param name="relativePath">由受控 WebView 传入的包内 Markdown 相对路径。</param>
    public void SelectDocument(string? relativePath)
    {
        if (mCatalog == null || string.IsNullOrWhiteSpace(relativePath))
        {
            return;
        }

        var target = mCatalog.Documents.FirstOrDefault(document => string.Equals(
            document.RelativePath,
            relativePath,
            StringComparison.Ordinal));
        if (target == null)
        {
            return;
        }

        if (!Documents.Any(document => string.Equals(
                document.RelativePath,
                target.RelativePath,
                StringComparison.Ordinal)))
        {
            SearchText = string.Empty;
        }

        SelectedDocument = target;
    }

    /// <summary>获取当前文档的页内标题目录。</summary>
    public IReadOnlyList<DocumentationHeading> TableOfContents
    {
        get => mTableOfContents;
        private set => SetProperty(ref mTableOfContents, value);
    }

    /// <summary>获取当前文档的完整 Markdown 正文。</summary>
    public string MarkdownText { get => mMarkdownText; private set => SetProperty(ref mMarkdownText, value); }

    /// <summary>获取当前文档内的代码块列表。</summary>
    public IReadOnlyList<DocumentationCodeBlock> CodeBlocks
    {
        get => mCodeBlocks;
        private set => SetProperty(ref mCodeBlocks, value);
    }

    /// <summary>获取供页面模板渲染的 Markdown 正文块。</summary>
    public IReadOnlyList<DocumentationBlock> Blocks
    {
        get => mBlocks;
        private set => SetProperty(ref mBlocks, value);
    }

    /// <summary>获取或设置准备复制的代码块。</summary>
    public DocumentationCodeBlock? SelectedCodeBlock
    {
        get => mSelectedCodeBlock;
        set
        {
            if (SetProperty(ref mSelectedCodeBlock, value))
            {
                CopyCodeCommand.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>获取文档加载、搜索或复制状态。</summary>
    public string StatusText { get => mStatusText; private set => SetProperty(ref mStatusText, value); }

    /// <summary>
    /// 接收文档宿主视图的原生边界错误，并复用页面状态显示给用户。
    /// </summary>
    /// <param name="message">可显示的错误消息。</param>
    internal void ReportViewError(string message)
    {
        StatusText = message;
    }

    /// <summary>获取显式执行全文和 API 搜索的命令。</summary>
    public AsyncRelayCommand SearchCommand { get; }

    /// <summary>获取重新扫描包内 Markdown 文档的命令。</summary>
    public AsyncRelayCommand ReloadCommand { get; }

    /// <summary>获取把当前代码块写入系统剪贴板的命令。</summary>
    public AsyncRelayCommand CopyCodeCommand { get; }

    /// <summary>
    /// 首次进入 Docs 页面时异步加载目录，后续调用复用已有状态。
    /// </summary>
    /// <returns>目录加载完成任务。</returns>
    public async Task EnsureLoadedAsync()
    {
        if (Volatile.Read(ref mDisposed) != 0)
        {
            return;
        }

        if (mCatalog != null || Interlocked.CompareExchange(ref mLoadStarted, 1, 0) != 0)
        {
            return;
        }

        if (mDocumentationService == null)
        {
            if (string.IsNullOrWhiteSpace(mInitializationError))
            {
                SetStatus(GetString(NoServiceKey, "当前启动入口没有可用的包内文档服务。"));
            }

            return;
        }

        try
        {
            SetStatus(GetString(ScanningKey, "正在扫描包内离线文档…"));
            mCatalog = await Task.Run(mDocumentationService.GetIndex);
            ApplyLoadedCatalog(mCatalog);
        }
        catch (Exception exception)
        {
            SetStatus(string.Format(GetString(LoadFailedTemplateKey, "文档目录加载失败: {0}"), exception.Message));
            Interlocked.Exchange(ref mLoadStarted, 0);
        }
    }

    /// <summary>
    /// 在 Application 服务中执行全文和 API 搜索，避免 View 自行扫描文件。
    /// </summary>
    /// <returns>搜索完成任务。</returns>
    private async Task SearchAsync()
    {
        if (mDocumentationService == null)
        {
            SetStatus(GetString(SearchUnavailableKey, "文档搜索不可用。"));
            return;
        }

        var query = SearchText.Trim();
        if (query.Length == 0)
        {
            ApplyCatalogFilter();
            return;
        }

        if (mCatalog == null)
        {
            await EnsureLoadedAsync();
        }

        if (mCatalog == null)
        {
            SetStatus(GetString(CatalogNotReadyKey, "文档目录尚未准备完成。"));
            return;
        }

        try
        {
            var results = await Task.Run(() => mDocumentationService.Search(query));
            if (!string.Equals(query, SearchText.Trim(), StringComparison.Ordinal))
            {
                return;
            }

            SearchResults = results;
            ApplyFullTextSearchResults();
            SetStatus(Documents.Count == 0
                ? GetString(SearchNoMatchKey, "未找到匹配的包内文档。")
                : string.Format(GetString(SearchDoneTemplateKey, "搜索完成，找到 {0} 篇文档。"), Documents.Count));
        }
        catch (Exception exception)
        {
            SetStatus(string.Format(GetString(SearchFailedTemplateKey, "文档搜索失败: {0}"), exception.Message));
        }
    }

    /// <summary>
    /// 清理目录和当前文档缓存后重新扫描受控 Markdown 根。
    /// </summary>
    private async Task ReloadAsync()
    {
        mCatalog = null;
        mSelectedDocument = null;
        Interlocked.Exchange(ref mLoadStarted, 0);
        Interlocked.Increment(ref mDocumentLoadVersion);
        Documents = Array.Empty<DocumentationIndexEntry>();
        SearchResults = Array.Empty<DocumentationSearchResult>();
        Blocks = Array.Empty<DocumentationBlock>();
        TableOfContents = Array.Empty<DocumentationHeading>();
        CodeBlocks = Array.Empty<DocumentationCodeBlock>();
        await EnsureLoadedAsync();
    }

    /// <summary>
    /// 从 Application 读取选中文档并更新正文、目录和代码块。
    /// </summary>
    /// <param name="relativePath">相对包根的受控 Markdown 路径。</param>
    /// <returns>文档读取完成任务。</returns>
    private async Task LoadDocumentAsync(string relativePath)
    {
        if (mDocumentationService == null || Volatile.Read(ref mDisposed) != 0)
        {
            return;
        }

        var loadVersion = Interlocked.Increment(ref mDocumentLoadVersion);
        try
        {
            var document = await Task.Run(() => mDocumentationService.ReadDocument(relativePath));
            if (Volatile.Read(ref mDisposed) == 0
                && loadVersion == Volatile.Read(ref mDocumentLoadVersion))
            {
                ApplyDocument(document);
            }
        }
        catch (Exception exception)
        {
            if (Volatile.Read(ref mDisposed) == 0
                && loadVersion == Volatile.Read(ref mDocumentLoadVersion))
            {
                SetStatus(string.Format(GetString(ReadFailedTemplateKey, "文档读取失败: {0}"), exception.Message));
            }
        }
    }

    /// <summary>
    /// 使关闭窗口前已经启动的文档读取结果失效，阻止后台任务继续修改页面状态。
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref mDisposed, 1) != 0)
        {
            return;
        }

        WorkbenchI18nService.Instance.CultureChanged -= OnCultureChanged;
        Interlocked.Increment(ref mDocumentLoadVersion);
        Interlocked.Exchange(ref mLoadStarted, 0);
    }

    /// <summary>
    /// 把当前代码块交给平台剪贴板边界。
    /// </summary>
    /// <returns>复制完成任务。</returns>
    private async Task CopySelectedCodeAsync()
    {
        if (SelectedCodeBlock == null || mCopyTextAsync == null)
        {
            return;
        }

        try
        {
            await mCopyTextAsync(SelectedCodeBlock.CopyText);
            SetStatus(GetString(CopiedKey, "代码已复制。"));
        }
        catch (Exception exception)
        {
            SetStatus(string.Format(GetString(CopyFailedTemplateKey, "复制失败: {0}"), exception.Message));
        }
    }

    /// <summary>
    /// 判断当前是否存在可复制代码和平台剪贴板入口。
    /// </summary>
    /// <returns>允许复制时返回 true。</returns>
    private bool CanCopySelectedCode()
    {
        return SelectedCodeBlock != null && mCopyTextAsync != null;
    }

    /// <summary>
    /// 判断标题栏搜索是否具备非空关键词，空输入会自动恢复完整文档目录。
    /// </summary>
    /// <returns>存在可检索关键词时返回 true。</returns>
    private bool CanSearch()
    {
        return !string.IsNullOrWhiteSpace(SearchText);
    }

    /// <summary>
    /// 应用首次目录快照并默认选择第一篇文档。
    /// </summary>
    /// <param name="catalog">Application 文档目录。</param>
    private void ApplyLoadedCatalog(DocumentationCatalog catalog)
    {
        PackageVersion = catalog.PackageVersion;
        ApplyCatalogFilter();
        SetStatus(string.Format(GetString(LoadedTemplateKey, "已加载 {0} 篇用户文档。"), catalog.NavigationDocuments.Count));
        if (SelectedDocument == null && Documents.Count > 0)
        {
            SelectedDocument = Documents[0];
        }
    }

    /// <summary>
    /// 使用目录元数据即时筛选文档列表，不在每次按键时读取正文。
    /// </summary>
    private void ApplyCatalogFilter()
    {
        if (mCatalog == null)
        {
            Documents = Array.Empty<DocumentationIndexEntry>();
            return;
        }

        var query = SearchText.Trim();
        Documents = query.Length == 0
            ? mCatalog.NavigationDocuments
            : mCatalog.NavigationDocuments.Where(entry => Contains(entry.Title, query)
                || Contains(entry.RelativePath, query)
                || entry.Keywords.Any(keyword => Contains(keyword.Text, query))).ToArray();
        EnsureSelectedDocumentIsVisible();
    }

    /// <summary>
    /// 把全文命中的 Markdown 文档按搜索排序投影为导航目录，不把 API 索引项伪装成可阅读页面。
    /// </summary>
    private void ApplyFullTextSearchResults()
    {
        if (mCatalog == null)
        {
            Documents = Array.Empty<DocumentationIndexEntry>();
            return;
        }

        Dictionary<string, DocumentationIndexEntry> entriesByPath = new(StringComparer.Ordinal);
        foreach (var entry in mCatalog.NavigationDocuments)
        {
            entriesByPath[entry.RelativePath] = entry;
        }

        HashSet<string> selectedPaths = new(StringComparer.Ordinal);
        List<DocumentationIndexEntry> documents = new();
        foreach (var result in SearchResults)
        {
            if (result.ItemKind != DocumentationSearchItemKind.Document
                || !selectedPaths.Add(result.RelativePath)
                || !entriesByPath.TryGetValue(result.RelativePath, out var entry))
            {
                continue;
            }

            documents.Add(entry);
        }

        Documents = documents;
        EnsureSelectedDocumentIsVisible();
    }

    /// <summary>
    /// 当前选中文档不在筛选结果中时切换到首个命中，空结果则保留当前正文供用户修改关键词。
    /// </summary>
    private void EnsureSelectedDocumentIsVisible()
    {
        if (Documents.Count == 0
            || (SelectedDocument != null
                && Documents.Any(entry => string.Equals(
                    entry.RelativePath,
                    SelectedDocument.RelativePath,
                    StringComparison.Ordinal))))
        {
            return;
        }

        SelectedDocument = Documents[0];
    }

    /// <summary>
    /// 应用一篇解析完成的 Markdown 文档。
    /// </summary>
    /// <param name="document">Application 文档内容。</param>
    private void ApplyDocument(DocumentationDocument document)
    {
        MarkdownText = document.Markdown;
        TableOfContents = document.Headings;
        CodeBlocks = document.CodeBlocks;
        Blocks = document.Blocks;
        SelectedCodeBlock = CodeBlocks.FirstOrDefault();
        StatusText = document.Title + " · " + document.RelativePath;
    }

    /// <summary>
    /// 执行不区分大小写的包含匹配。
    /// </summary>
    private static bool Contains(string value, string query)
    {
        return value.Contains(query, StringComparison.OrdinalIgnoreCase);
    }
}
