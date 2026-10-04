using System.Windows.Input;
using YokiFrame.Tooling.Application.Documentation;
using YokiFrame.Workbench.Avalonia.Services;

namespace YokiFrame.Workbench.Avalonia.ViewModels;

/// <summary>承载文档目录过滤、搜索结果和正文投影。</summary>
public sealed partial class DocumentationPageViewModel
{
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
}
