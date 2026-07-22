namespace YokiFrame.Tooling.Application.Documentation;

/// <summary>
/// 描述一个已安全读取并解析的 Markdown 文档。
/// </summary>
public sealed class DocumentationDocument
{
    /// <summary>
    /// 创建可供 Workbench 和 CLI 消费的文档只读模型。
    /// </summary>
    /// <param name="packageVersion">当前包版本。</param>
    /// <param name="title">文档标题。</param>
    /// <param name="relativePath">相对 YokiFrame 包根的稳定路径。</param>
    /// <param name="markdown">完整 Markdown 正文。</param>
    /// <param name="headings">目录标题与锚点。</param>
    /// <param name="codeBlocks">可复制代码块。</param>
    public DocumentationDocument(
        string packageVersion,
        string title,
        string relativePath,
        string markdown,
        IReadOnlyList<DocumentationHeading> headings,
        IReadOnlyList<DocumentationCodeBlock> codeBlocks,
        IReadOnlyList<DocumentationBlock> blocks)
    {
        PackageVersion = packageVersion;
        Title = title;
        RelativePath = relativePath;
        Markdown = markdown;
        Headings = headings.ToArray();
        CodeBlocks = codeBlocks.ToArray();
        Blocks = blocks.ToArray();
    }

    /// <summary>
    /// 获取当前包版本。
    /// </summary>
    public string PackageVersion { get; }

    /// <summary>
    /// 获取文档标题。
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// 获取相对 YokiFrame 包根的稳定路径。
    /// </summary>
    public string RelativePath { get; }

    /// <summary>
    /// 获取完整 Markdown 正文。
    /// </summary>
    public string Markdown { get; }

    /// <summary>
    /// 获取目录标题与稳定锚点。
    /// </summary>
    public IReadOnlyList<DocumentationHeading> Headings { get; }

    /// <summary>
    /// 获取支持 UI 直接复制的代码块。
    /// </summary>
    public IReadOnlyList<DocumentationCodeBlock> CodeBlocks { get; }

    /// <summary>
    /// 获取供 Workbench 渲染的 Markdown 正文块。
    /// </summary>
    public IReadOnlyList<DocumentationBlock> Blocks { get; }
}

/// <summary>
/// 表示文档正文中的一个可渲染块。
/// </summary>
public abstract class DocumentationBlock
{
    /// <summary>
    /// 获取块在原文中的顺序。
    /// </summary>
    public int Order { get; init; }
}

/// <summary>
/// 表示文档标题块。
/// </summary>
public sealed class DocumentationHeadingBlock : DocumentationBlock
{
    /// <summary>获取标题层级。</summary>
    public int Level { get; init; }

    /// <summary>获取标题文本。</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>获取标题锚点。</summary>
    public string Anchor { get; init; } = string.Empty;
}

/// <summary>
/// 表示文档普通段落或列表块。
/// </summary>
public sealed class DocumentationParagraphBlock : DocumentationBlock
{
    /// <summary>获取段落文本。</summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>获取是否为列表项。</summary>
    public bool IsListItem { get; init; }
}

/// <summary>
/// 表示文档中的代码块正文。
/// </summary>
public sealed class DocumentationCodeBlockBlock : DocumentationBlock
{
    /// <summary>获取代码块模型。</summary>
    public DocumentationCodeBlock CodeBlock { get; init; } = null!;
}

/// <summary>
/// 表示文档表格块。
/// </summary>
public sealed class DocumentationTableBlock : DocumentationBlock
{
    /// <summary>获取表格行。</summary>
    public IReadOnlyList<DocumentationTableRow> Rows { get; init; } = Array.Empty<DocumentationTableRow>();
}

/// <summary>
/// 表示 Markdown 表格的一行。
/// </summary>
public sealed class DocumentationTableRow
{
    /// <summary>获取单元格文本。</summary>
    public IReadOnlyList<string> Cells { get; init; } = Array.Empty<string>();

    /// <summary>获取是否为表头。</summary>
    public bool IsHeader { get; init; }
}

/// <summary>
/// 描述 Markdown 标题及其页内目录锚点。
/// </summary>
public sealed class DocumentationHeading
{
    /// <summary>
    /// 创建 Markdown 目录项。
    /// </summary>
    /// <param name="level">标题层级，范围为 1 至 6。</param>
    /// <param name="title">去除 Markdown 标记后的标题。</param>
    /// <param name="anchor">文档内稳定锚点。</param>
    public DocumentationHeading(int level, string title, string anchor)
    {
        Level = level;
        Title = title;
        Anchor = anchor;
    }

    /// <summary>
    /// 获取标题层级。
    /// </summary>
    public int Level { get; }

    /// <summary>
    /// 获取纯文本标题。
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// 获取文档内稳定锚点。
    /// </summary>
    public string Anchor { get; }

    /// <summary>
    /// 获取带层级缩进提示的页内导航标题。
    /// </summary>
    public string DisplayTitle => new string(' ', Math.Max(0, Level - 2) * 2) + Title;
}

/// <summary>
/// 描述一个 Markdown 围栏代码块及其复制文本。
/// </summary>
public sealed class DocumentationCodeBlock
{
    /// <summary>
    /// 创建代码块只读模型。
    /// </summary>
    /// <param name="index">文档内从零开始的代码块序号。</param>
    /// <param name="language">围栏声明的语言。</param>
    /// <param name="code">不含围栏的代码正文。</param>
    /// <param name="startLine">代码正文起始行，使用一基行号。</param>
    /// <param name="endLine">代码正文结束行，使用一基行号。</param>
    public DocumentationCodeBlock(int index, string language, string code, int startLine, int endLine)
    {
        Index = index;
        Language = language;
        Code = code;
        StartLine = startLine;
        EndLine = endLine;
    }

    /// <summary>
    /// 获取文档内从零开始的代码块序号。
    /// </summary>
    public int Index { get; }

    /// <summary>
    /// 获取围栏声明的语言。
    /// </summary>
    public string Language { get; }

    /// <summary>
    /// 获取不含围栏的代码正文。
    /// </summary>
    public string Code { get; }

    /// <summary>
    /// 获取 UI 应写入剪贴板的完整文本。
    /// </summary>
    public string CopyText => Code;

    /// <summary>
    /// 获取代码正文的一基起始行号。
    /// </summary>
    public int StartLine { get; }

    /// <summary>
    /// 获取代码正文的一基结束行号。
    /// </summary>
    public int EndLine { get; }
}

/// <summary>
/// 描述一次文档或 API 关键词搜索命中。
/// </summary>
public sealed class DocumentationSearchResult
{
    /// <summary>
    /// 创建统一搜索结果。
    /// </summary>
    /// <param name="itemKind">结果来源类型。</param>
    /// <param name="title">结果标题或 API 符号名。</param>
    /// <param name="relativePath">相对包根或 API 索引来源的路径。</param>
    /// <param name="snippet">包含命中上下文的摘要。</param>
    /// <param name="matchedKeywordKinds">命中的 Kit、类型、方法或错误码分类。</param>
    public DocumentationSearchResult(
        DocumentationSearchItemKind itemKind,
        string title,
        string relativePath,
        string snippet,
        IReadOnlyList<DocumentationKeywordKind> matchedKeywordKinds)
    {
        ItemKind = itemKind;
        Title = title;
        RelativePath = relativePath;
        Snippet = snippet;
        MatchedKeywordKinds = matchedKeywordKinds.Distinct().ToArray();
    }

    /// <summary>
    /// 获取结果来源类型。
    /// </summary>
    public DocumentationSearchItemKind ItemKind { get; }

    /// <summary>
    /// 获取结果标题或 API 符号名。
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// 获取相对包根或 API 索引来源的路径。
    /// </summary>
    public string RelativePath { get; }

    /// <summary>
    /// 获取包含命中上下文的摘要。
    /// </summary>
    public string Snippet { get; }

    /// <summary>
    /// 获取命中的语义分类。
    /// </summary>
    public IReadOnlyList<DocumentationKeywordKind> MatchedKeywordKinds { get; }
}
