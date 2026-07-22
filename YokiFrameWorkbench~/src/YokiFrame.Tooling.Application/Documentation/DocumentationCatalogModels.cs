namespace YokiFrame.Tooling.Application.Documentation;

/// <summary>
/// 标识离线 Markdown 所属的受控文档根。
/// </summary>
public enum DocumentationSourceKind
{
    /// <summary>
    /// 包根下 Documentation~/Api 与 Documentation~/Guides 中的公开 Workbench 文档。
    /// </summary>
    PackageDocumentation,

    /// <summary>
    /// 历史源码侧文档分类；当前公开文档策略不会生成该分类。
    /// </summary>
    WorkbenchDocumentation,
}

/// <summary>
/// 标识文档与 API 索引中的可搜索语义。
/// </summary>
public enum DocumentationKeywordKind
{
    /// <summary>
    /// YokiFrame Kit 入口。
    /// </summary>
    Kit,

    /// <summary>
    /// C# 类型或接口。
    /// </summary>
    Type,

    /// <summary>
    /// C# 方法入口。
    /// </summary>
    Method,

    /// <summary>
    /// 协议或应用错误码。
    /// </summary>
    ErrorCode,
}

/// <summary>
/// 标识搜索结果来自人工 Markdown 还是可插拔 API 索引。
/// </summary>
public enum DocumentationSearchItemKind
{
    /// <summary>
    /// 人工维护的 Markdown 文档。
    /// </summary>
    Document,

    /// <summary>
    /// 外部 API 索引来源提供的符号。
    /// </summary>
    ApiSymbol,
}

/// <summary>
/// 描述离线文档目录及当前包版本。
/// </summary>
public sealed class DocumentationCatalog
{
    /// <summary>
    /// 创建离线文档目录快照。
    /// </summary>
    /// <param name="packageVersion">package.json 中的当前版本。</param>
    /// <param name="documents">受控根内的 Markdown 条目。</param>
    /// <param name="apiEntries">可插拔 API 索引条目。</param>
    public DocumentationCatalog(
        string packageVersion,
        IReadOnlyList<DocumentationIndexEntry> documents,
        IReadOnlyList<DocumentationApiIndexEntry> apiEntries)
    {
        PackageVersion = packageVersion;
        Documents = documents.ToArray();
        ApiEntries = apiEntries.ToArray();
    }

    /// <summary>
    /// 获取 package.json 中的当前包版本。
    /// </summary>
    public string PackageVersion { get; }

    /// <summary>
    /// 获取受控 Markdown 文档目录快照。
    /// </summary>
    public IReadOnlyList<DocumentationIndexEntry> Documents { get; }

    /// <summary>
    /// 获取 API 索引来源返回的条目快照。
    /// </summary>
    public IReadOnlyList<DocumentationApiIndexEntry> ApiEntries { get; }
}

/// <summary>
/// 描述一个受控 Markdown 文档的目录条目。
/// </summary>
public sealed class DocumentationIndexEntry
{
    /// <summary>
    /// 创建 Markdown 文档目录条目。
    /// </summary>
    /// <param name="title">文档标题。</param>
    /// <param name="relativePath">相对 YokiFrame 包根的稳定路径。</param>
    /// <param name="sourceKind">文档所属受控根。</param>
    /// <param name="keywords">从正文提取的首版搜索关键词。</param>
    public DocumentationIndexEntry(
        string title,
        string relativePath,
        DocumentationSourceKind sourceKind,
        IReadOnlyList<DocumentationKeyword> keywords)
    {
        Title = title;
        RelativePath = relativePath;
        SourceKind = sourceKind;
        Keywords = keywords.ToArray();
    }

    /// <summary>
    /// 获取文档标题。
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// 获取相对 YokiFrame 包根的稳定路径。
    /// </summary>
    public string RelativePath { get; }

    /// <summary>
    /// 获取文档所属受控根。
    /// </summary>
    public DocumentationSourceKind SourceKind { get; }

    /// <summary>
    /// 获取从正文提取的首版搜索关键词。
    /// </summary>
    public IReadOnlyList<DocumentationKeyword> Keywords { get; }

    /// <summary>获取 Workbench 文档目录分组。</summary>
    public string Group
    {
        get => ResolveGroup(RelativePath, SourceKind);
    }

    /// <summary>
    /// 根据受控路径解析 Workbench 左侧文档分组。
    /// </summary>
    /// <param name="relativePath">相对 YokiFrame 包根的稳定路径。</param>
    /// <param name="sourceKind">文档所属受控根。</param>
    /// <returns>Workbench 左侧文档导航分组。</returns>
    internal static string ResolveGroup(string relativePath, DocumentationSourceKind sourceKind)
    {
        if (relativePath.Contains("/Api/00-GettingStarted/", StringComparison.OrdinalIgnoreCase))
        {
            return "入门";
        }

        if (relativePath.Contains("/Guides/", StringComparison.OrdinalIgnoreCase))
        {
            return "工具链";
        }

        if (relativePath.Contains("/Api/01-Architecture/", StringComparison.OrdinalIgnoreCase)
            || relativePath.Contains("Architecture", StringComparison.OrdinalIgnoreCase))
        {
            return "架构";
        }

        if (relativePath.Contains("/Api/03-Tool/", StringComparison.OrdinalIgnoreCase))
        {
            return "Tool";
        }

        if (relativePath.Contains("/Api/04-Reference/", StringComparison.OrdinalIgnoreCase))
        {
            return "Reference";
        }

        return sourceKind == DocumentationSourceKind.WorkbenchDocumentation ? "Tool" : "Core";
    }

    /// <summary>
    /// 返回文档分组在左侧导航中的稳定顺序。
    /// </summary>
    /// <param name="group">由 <see cref="ResolveGroup"/> 解析出的文档分组。</param>
    /// <returns>越小越靠前的导航排序值。</returns>
    internal static int GetNavigationGroupOrder(string group)
    {
        return group switch
        {
            "入门" => 0,
            "架构" => 1,
            "工具链" => 2,
            "Core" => 3,
            "Tool" => 4,
            "Reference" => 5,
            _ => 6,
        };
    }

    /// <summary>
    /// 为同一分组内的文档提供稳定排序键。
    /// </summary>
    /// <param name="relativePath">相对 YokiFrame 包根的稳定路径。</param>
    /// <returns>用于同一文档分组内排序的稳定键。</returns>
    internal static string ResolveNavigationSortKey(string relativePath)
    {
        return relativePath;
    }

    /// <summary>获取该条目是否为目录分组的首项。</summary>
    public bool IsGroupStart { get; init; }
}

/// <summary>
/// 描述一个从 Markdown 或 API 索引得到的搜索关键词。
/// </summary>
public sealed class DocumentationKeyword
{
    /// <summary>
    /// 创建带语义分类的关键词。
    /// </summary>
    /// <param name="text">关键词文本。</param>
    /// <param name="kind">关键词分类。</param>
    public DocumentationKeyword(string text, DocumentationKeywordKind kind)
    {
        Text = text;
        Kind = kind;
    }

    /// <summary>
    /// 获取关键词文本。
    /// </summary>
    public string Text { get; }

    /// <summary>
    /// 获取关键词分类。
    /// </summary>
    public DocumentationKeywordKind Kind { get; }
}

/// <summary>
/// 描述由 XML 文档或其它未来来源投影出的单个 API 索引条目。
/// </summary>
public sealed class DocumentationApiIndexEntry
{
    /// <summary>
    /// 创建 API 索引条目；本模型只接收已投影数据，不负责生成 XML 文档。
    /// </summary>
    /// <param name="name">可搜索符号名。</param>
    /// <param name="kind">符号语义分类。</param>
    /// <param name="summary">可显示摘要。</param>
    /// <param name="declaringType">声明类型。</param>
    /// <param name="relativePath">索引来源的包内相对路径。</param>
    public DocumentationApiIndexEntry(
        string name,
        DocumentationKeywordKind kind,
        string summary,
        string declaringType,
        string relativePath)
    {
        Name = name;
        Kind = kind;
        Summary = summary;
        DeclaringType = declaringType;
        RelativePath = relativePath;
    }

    /// <summary>
    /// 获取可搜索符号名。
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 获取符号语义分类。
    /// </summary>
    public DocumentationKeywordKind Kind { get; }

    /// <summary>
    /// 获取可显示摘要。
    /// </summary>
    public string Summary { get; }

    /// <summary>
    /// 获取声明类型。
    /// </summary>
    public string DeclaringType { get; }

    /// <summary>
    /// 获取索引来源的包内相对路径。
    /// </summary>
    public string RelativePath { get; }
}

/// <summary>
/// 定义未来 XML API 索引器或其它离线符号来源与应用层之间的窄入口。
/// </summary>
public interface IDocumentationApiIndexSource
{
    /// <summary>
    /// 读取已投影的 API 索引条目；实现负责解析来源，应用层只消费只读模型。
    /// </summary>
    /// <returns>API 索引条目快照。</returns>
    IReadOnlyList<DocumentationApiIndexEntry> ReadEntries();
}
