using System.Text;
using System.Text.Json;
using YokiFrame.Tooling.Application.Documentation.Internal;

namespace YokiFrame.Tooling.Application.Documentation;

/// <summary>
/// 提供 Workbench、CLI 和未来 AI 入口共用的用户 API 文档目录、读取与搜索用例。
/// </summary>
public sealed class OfflineDocumentationService
{
    private const int SEARCH_SNIPPET_CONTEXT = 72;
    private const int SEARCH_SNIPPET_MAX_LENGTH = 180;

    private readonly DocumentationPathPolicy mPathPolicy;
    private readonly IDocumentationApiIndexSource mApiIndexSource;
    private readonly object mSnapshotLock = new();
    private DocumentationSnapshot? mSnapshot;

    /// <summary>
    /// 使用明确的 YokiFrame 包根创建离线文档服务，不假设 Unity 或 Godot 项目布局。
    /// </summary>
    /// <param name="packageRoot">由入口层解析并传入的 YokiFrame 包根。</param>
    /// <param name="apiIndexSource">未来 XML API 索引器实现的数据入口；为空时使用空索引。</param>
    public OfflineDocumentationService(
        string packageRoot,
        IDocumentationApiIndexSource? apiIndexSource = null)
    {
        mPathPolicy = new DocumentationPathPolicy(packageRoot);
        mApiIndexSource = apiIndexSource ?? new EmptyDocumentationApiIndexSource();
    }

    /// <summary>
    /// 扫描面向用户的 Documentation~/Api 与 Documentation~/Guides，并返回当前包版本、文档与 API 条目快照。
    /// </summary>
    /// <returns>离线文档目录。</returns>
    public DocumentationCatalog GetIndex()
    {
        return GetOrCreateSnapshot().Catalog;
    }

    /// <summary>
    /// 使当前离线文档快照失效；下一次索引或搜索请求会重新扫描并解析受控文档。
    /// </summary>
    public void RefreshIndex()
    {
        lock (mSnapshotLock)
        {
            mSnapshot = null;
        }
    }

    /// <summary>
    /// 从受控根安全读取 Markdown，并返回目录锚点和可复制代码块。
    /// </summary>
    /// <param name="relativePath">相对 YokiFrame 包根的 Markdown 路径。</param>
    /// <returns>解析后的离线文档。</returns>
    public DocumentationDocument ReadDocument(string relativePath)
    {
        var location = mPathPolicy.ResolveDocument(relativePath);
        lock (mSnapshotLock)
        {
            if (mSnapshot != null
                && mSnapshot.Documents.TryGetValue(location.RelativePath, out var cachedDocument))
            {
                return cachedDocument;
            }
        }

        var markdown = mPathPolicy.ReadMarkdown(location);
        return MarkdownDocumentParser.Parse(ReadPackageVersion(), location, markdown);
    }

    /// <summary>
    /// 在人工 Markdown 与可插拔 API 条目中搜索 Kit、类型、方法和错误码关键词。
    /// </summary>
    /// <param name="keyword">不区分大小写的关键词。</param>
    /// <returns>带命中语义和上下文摘要的结果。</returns>
    public IReadOnlyList<DocumentationSearchResult> Search(string? keyword)
    {
        var query = keyword?.Trim() ?? string.Empty;
        if (query.Length == 0)
        {
            return Array.Empty<DocumentationSearchResult>();
        }

        var snapshot = GetOrCreateSnapshot();
        var results = new List<DocumentationSearchResult>();
        AddDocumentResults(results, snapshot, query);
        AddApiResults(results, snapshot.Catalog.ApiEntries, query);
        return results
            .OrderBy(result => GetResultRank(result, query))
            .ThenBy(static result => result.Title, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>
    /// 获取现有不可变快照，或在锁内完成一次完整扫描后发布新快照。
    /// </summary>
    /// <returns>可供索引和搜索共同复用的文档快照。</returns>
    private DocumentationSnapshot GetOrCreateSnapshot()
    {
        lock (mSnapshotLock)
        {
            mSnapshot ??= CreateSnapshot();
            return mSnapshot;
        }
    }

    /// <summary>
    /// 单次读取版本、用户 API/指南 Markdown 与 API 来源，并建立目录和已解析正文的一致快照。
    /// </summary>
    /// <returns>新建的不可变文档快照。</returns>
    private DocumentationSnapshot CreateSnapshot()
    {
        var packageVersion = ReadPackageVersion();
        var parsedEntries = new List<DocumentationIndexEntry>();
        var documents = new Dictionary<string, DocumentationDocument>(StringComparer.Ordinal);
        foreach (var location in mPathPolicy.EnumerateMarkdownFiles())
        {
            var markdown = mPathPolicy.ReadMarkdown(location);
            var document = MarkdownDocumentParser.Parse(packageVersion, location, markdown);
            var keywords = DocumentationKeywordExtractor.Extract(document.Markdown);
            parsedEntries.Add(new DocumentationIndexEntry(
                document.Title,
                location.RelativePath,
                location.SourceKind,
                keywords));
            documents.Add(location.RelativePath, document);
        }

        var entries = new List<DocumentationIndexEntry>(parsedEntries.Count);
        string previousGroup = string.Empty;
        foreach (var parsedEntry in parsedEntries
                     .OrderBy(static entry => DocumentationIndexEntry.GetNavigationGroupOrder(entry.Group))
                     .ThenBy(static entry => entry.RelativePath, StringComparer.OrdinalIgnoreCase))
        {
            var entry = new DocumentationIndexEntry(
                parsedEntry.Title,
                parsedEntry.RelativePath,
                parsedEntry.SourceKind,
                parsedEntry.Keywords)
            {
                IsGroupStart = !string.Equals(previousGroup, parsedEntry.Group, StringComparison.Ordinal),
            };
            previousGroup = entry.Group;
            entries.Add(entry);
        }

        var catalog = new DocumentationCatalog(packageVersion, entries, ReadApiEntries());
        return new DocumentationSnapshot(catalog, documents);
    }

    /// <summary>
    /// 读取并过滤 API 来源条目，避免空符号污染目录与搜索。
    /// </summary>
    /// <returns>API 索引条目快照。</returns>
    private IReadOnlyList<DocumentationApiIndexEntry> ReadApiEntries()
    {
        var entries = mApiIndexSource.ReadEntries() ?? Array.Empty<DocumentationApiIndexEntry>();
        return entries
            .Where(static entry => entry != null && !string.IsNullOrWhiteSpace(entry.Name))
            .OrderBy(static entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>
    /// 搜索 Markdown 标题、路径、正文和已分类关键词。
    /// </summary>
    /// <param name="results">统一结果集合。</param>
    /// <param name="snapshot">包含目录条目与已解析正文的同代快照。</param>
    /// <param name="query">已规范化搜索词。</param>
    private static void AddDocumentResults(
        ICollection<DocumentationSearchResult> results,
        DocumentationSnapshot snapshot,
        string query)
    {
        foreach (var entry in snapshot.Catalog.NavigationDocuments)
        {
            var document = snapshot.Documents[entry.RelativePath];
            if (!ContainsQuery(entry.Title, query)
                && !ContainsQuery(entry.RelativePath, query)
                && !ContainsQuery(document.Markdown, query))
            {
                continue;
            }

            var kinds = entry.Keywords
                .Where(item => ContainsQuery(item.Text, query))
                .Select(static item => item.Kind)
                .Distinct()
                .ToArray();
            results.Add(new DocumentationSearchResult(
                DocumentationSearchItemKind.Document,
                entry.Title,
                entry.RelativePath,
                CreateSnippet(entry.Title + "\n" + document.Markdown, query),
                kinds));
        }
    }

    /// <summary>
    /// 搜索 API 条目的符号名、声明类型、摘要和来源路径。
    /// </summary>
    /// <param name="results">统一结果集合。</param>
    /// <param name="entries">API 索引条目。</param>
    /// <param name="query">已规范化搜索词。</param>
    private static void AddApiResults(
        ICollection<DocumentationSearchResult> results,
        IReadOnlyList<DocumentationApiIndexEntry> entries,
        string query)
    {
        foreach (var entry in entries)
        {
            var searchText = string.Join(
                '\n',
                entry.Name,
                entry.DeclaringType,
                entry.Summary,
                entry.RelativePath);
            if (!ContainsQuery(searchText, query))
            {
                continue;
            }

            results.Add(new DocumentationSearchResult(
                DocumentationSearchItemKind.ApiSymbol,
                entry.Name,
                entry.RelativePath,
                CreateSnippet(searchText, query),
                new[] { entry.Kind }));
        }
    }

    /// <summary>
    /// 从 package.json 读取当前版本，避免 UI 或 CLI 硬编码包版本。
    /// </summary>
    /// <returns>非空版本文本。</returns>
    private string ReadPackageVersion()
    {
        var packagePath = Path.Combine(mPathPolicy.PackageRoot, "package.json");
        if (!File.Exists(packagePath))
        {
            throw new FileNotFoundException("YokiFrame package.json 不存在。", packagePath);
        }

        try
        {
            using var stream = File.OpenRead(packagePath);
            using var document = JsonDocument.Parse(stream);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("version", out var versionElement))
            {
                throw new InvalidDataException($"YokiFrame package.json 缺少 version: {packagePath}");
            }

            if (versionElement.ValueKind != JsonValueKind.String)
            {
                throw new InvalidDataException($"YokiFrame package.json version 必须是字符串: {packagePath}");
            }

            var version = versionElement.GetString()?.Trim();
            return !string.IsNullOrWhiteSpace(version)
                ? version
                : throw new InvalidDataException($"YokiFrame package.json version 不能为空: {packagePath}");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"YokiFrame package.json JSON 无效: {packagePath}", exception);
        }
    }

    /// <summary>
    /// 创建保留命中词且适合列表展示的紧凑摘要。
    /// </summary>
    /// <param name="source">待截取文本。</param>
    /// <param name="query">命中关键词。</param>
    /// <returns>紧凑上下文摘要。</returns>
    private static string CreateSnippet(string source, string query)
    {
        var matchIndex = source.IndexOf(query, StringComparison.OrdinalIgnoreCase);
        if (matchIndex < 0)
        {
            return CollapseWhitespace(source[..Math.Min(source.Length, SEARCH_SNIPPET_MAX_LENGTH)]);
        }

        var start = Math.Max(0, matchIndex - SEARCH_SNIPPET_CONTEXT);
        var available = source.Length - start;
        var length = Math.Min(available, SEARCH_SNIPPET_MAX_LENGTH);
        var prefix = start > 0 ? "…" : string.Empty;
        var suffix = start + length < source.Length ? "…" : string.Empty;
        return prefix + CollapseWhitespace(source.Substring(start, length)) + suffix;
    }

    /// <summary>
    /// 把换行和连续空白压缩为单空格，避免搜索列表出现破碎布局。
    /// </summary>
    /// <param name="value">原始摘要文本。</param>
    /// <returns>紧凑摘要。</returns>
    private static string CollapseWhitespace(string value)
    {
        var builder = new StringBuilder(value.Length);
        var pendingSpace = false;
        foreach (var character in value)
        {
            if (char.IsWhiteSpace(character))
            {
                pendingSpace = builder.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(character);
        }

        return builder.ToString();
    }

    /// <summary>
    /// 执行不区分大小写的首版包含匹配。
    /// </summary>
    /// <param name="source">待搜索文本。</param>
    /// <param name="query">关键词。</param>
    /// <returns>是否包含关键词。</returns>
    private static bool ContainsQuery(string source, string query)
    {
        return source.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 让标题完全匹配优先，其次按文档和 API 的稳定顺序显示。
    /// </summary>
    /// <param name="result">搜索结果。</param>
    /// <param name="query">搜索词。</param>
    /// <returns>升序排序权重。</returns>
    private static int GetResultRank(DocumentationSearchResult result, string query)
    {
        if (result.Title.Equals(query, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        return result.ItemKind == DocumentationSearchItemKind.Document ? 1 : 2;
    }

    /// <summary>
    /// 保存同一次扫描得到的目录和已解析正文，避免搜索阶段重复执行全文 IO 与解析。
    /// </summary>
    private sealed class DocumentationSnapshot
    {
        /// <summary>
        /// 创建只在服务内部发布且后续不再修改的文档快照。
        /// </summary>
        /// <param name="catalog">目录与 API 条目。</param>
        /// <param name="documents">按包内相对路径索引的已解析正文。</param>
        internal DocumentationSnapshot(
            DocumentationCatalog catalog,
            IDictionary<string, DocumentationDocument> documents)
        {
            Catalog = catalog;
            Documents = new Dictionary<string, DocumentationDocument>(documents, StringComparer.Ordinal);
        }

        /// <summary>
        /// 获取当前扫描对应的目录。
        /// </summary>
        internal DocumentationCatalog Catalog { get; }

        /// <summary>
        /// 获取当前扫描中已经完成解析的正文映射。
        /// </summary>
        internal IReadOnlyDictionary<string, DocumentationDocument> Documents { get; }
    }
}
