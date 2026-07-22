using System.Text;
using System.Text.RegularExpressions;

namespace YokiFrame.Tooling.Application.Documentation.Internal;

/// <summary>
/// 以无第三方依赖的首版规则提取 Markdown 标题、目录锚点和围栏代码块。
/// </summary>
internal static class MarkdownDocumentParser
{
    private static readonly Regex sInlineLinkRegex = new(@"\[([^\]]+)\]\([^\)]+\)", RegexOptions.CultureInvariant);
    private static readonly Regex sInlineMarkerRegex = new(@"[`*_~]", RegexOptions.CultureInvariant);
    private static readonly Regex sClosingHashesRegex = new(@"\s+#+$", RegexOptions.CultureInvariant);

    /// <summary>
    /// 把已安全读取的 Markdown 转换为应用层只读模型。
    /// </summary>
    /// <param name="packageVersion">当前包版本。</param>
    /// <param name="location">已校验文档位置。</param>
    /// <param name="markdown">Markdown 正文。</param>
    /// <returns>文档只读模型。</returns>
    internal static DocumentationDocument Parse(
        string packageVersion,
        DocumentationFileLocation location,
        string markdown)
    {
        var normalizedMarkdown = NormalizeLineEndings(markdown);
        var headings = new List<DocumentationHeading>();
        var codeBlocks = new List<DocumentationCodeBlock>();
        var blocks = new List<DocumentationBlock>();
        ParseLines(normalizedMarkdown.Split('\n'), headings, codeBlocks);
        ParseRenderBlocks(normalizedMarkdown.Split('\n'), headings, codeBlocks, blocks);
        var title = headings.FirstOrDefault(static heading => heading.Level == 1)?.Title
            ?? Path.GetFileNameWithoutExtension(location.FullPath);
        return new DocumentationDocument(
            packageVersion,
            title,
            location.RelativePath,
            normalizedMarkdown,
            headings,
            codeBlocks,
            blocks);
    }

    /// <summary>
    /// 将 Markdown 转为 Workbench 可直接模板化的标题、段落、列表和表格块。
    /// </summary>
    private static void ParseRenderBlocks(
        IReadOnlyList<string> lines,
        IReadOnlyList<DocumentationHeading> headings,
        IReadOnlyList<DocumentationCodeBlock> codeBlocks,
        ICollection<DocumentationBlock> blocks)
    {
        var headingIndex = 0;
        var codeIndex = 0;
        var order = 0;
        var paragraph = new List<string>();
        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];
            if (line.TrimStart().StartsWith("```", StringComparison.Ordinal))
            {
                FlushParagraph(paragraph, blocks, ref order);
                if (codeIndex < codeBlocks.Count)
                {
                    blocks.Add(new DocumentationCodeBlockBlock { Order = order++, CodeBlock = codeBlocks[codeIndex++] });
                }

                while (index + 1 < lines.Count && !lines[index + 1].TrimStart().StartsWith("```", StringComparison.Ordinal))
                {
                    index++;
                }

                continue;
            }

            if (TryParseHeading(line, out _, out _))
            {
                FlushParagraph(paragraph, blocks, ref order);
                var heading = headings[headingIndex++];
                blocks.Add(new DocumentationHeadingBlock
                {
                    Order = order++,
                    Level = heading.Level,
                    Title = heading.Title,
                    Anchor = heading.Anchor
                });
                continue;
            }

            if (IsTableSeparator(line) && paragraph.Count > 0)
            {
                var header = paragraph[^1];
                paragraph.RemoveAt(paragraph.Count - 1);
                FlushParagraph(paragraph, blocks, ref order);
                var rows = new List<DocumentationTableRow>
                {
                    new() { IsHeader = true, Cells = SplitTableRow(header) }
                };
                while (index + 1 < lines.Count && IsTableRow(lines[index + 1]))
                {
                    index++;
                    if (!IsTableSeparator(lines[index]))
                    {
                        rows.Add(new DocumentationTableRow { Cells = SplitTableRow(lines[index]) });
                    }
                }

                blocks.Add(new DocumentationTableBlock { Order = order++, Rows = rows });
                continue;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                FlushParagraph(paragraph, blocks, ref order);
                continue;
            }

            if (IsListLine(line))
            {
                FlushParagraph(paragraph, blocks, ref order);
                blocks.Add(new DocumentationParagraphBlock
                {
                    Order = order++,
                    Text = line.TrimStart()[2..].Trim(),
                    IsListItem = true
                });
                continue;
            }

            paragraph.Add(line.Trim());
        }

        FlushParagraph(paragraph, blocks, ref order);
    }

    /// <summary>把连续普通行合并为单个段落或列表块。</summary>
    private static void FlushParagraph(List<string> lines, ICollection<DocumentationBlock> blocks, ref int order)
    {
        if (lines.Count == 0)
        {
            return;
        }

        var text = string.Join(" ", lines).Trim();
        blocks.Add(new DocumentationParagraphBlock
        {
            Order = order++,
            Text = text,
            IsListItem = false
        });
        lines.Clear();
    }

    /// <summary>判断单行无序或有序列表项。</summary>
    private static bool IsListLine(string line)
    {
        var trimmed = line.TrimStart();
        return trimmed.StartsWith("- ", StringComparison.Ordinal)
            || trimmed.StartsWith("* ", StringComparison.Ordinal)
            || Regex.IsMatch(trimmed, @"^\d+\. ");
    }

    /// <summary>判断 Markdown 表格行。</summary>
    private static bool IsTableRow(string line) => line.TrimStart().StartsWith("|", StringComparison.Ordinal);

    /// <summary>判断 Markdown 表头分隔行。</summary>
    private static bool IsTableSeparator(string line)
    {
        return IsTableRow(line) && line.Replace("|", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace(":", string.Empty, StringComparison.Ordinal)
            .Trim().Length == 0;
    }

    /// <summary>拆分 Markdown 表格单元格并去除边界空白。</summary>
    private static IReadOnlyList<string> SplitTableRow(string line)
    {
        return line.Trim().Trim('|').Split('|').Select(static cell => cell.Trim()).ToArray();
    }

    /// <summary>
    /// 单次扫描 Markdown 行，避免把代码块内的井号误识别为标题。
    /// </summary>
    /// <param name="lines">Markdown 行。</param>
    /// <param name="headings">标题结果集合。</param>
    /// <param name="codeBlocks">代码块结果集合。</param>
    private static void ParseLines(
        IReadOnlyList<string> lines,
        ICollection<DocumentationHeading> headings,
        ICollection<DocumentationCodeBlock> codeBlocks)
    {
        var anchors = new Dictionary<string, int>(StringComparer.Ordinal);
        var codeState = new CodeFenceState();
        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];
            if (TryHandleFence(line, index, codeState, codeBlocks))
            {
                continue;
            }

            if (codeState.IsOpen)
            {
                codeState.Lines.Add(line);
                continue;
            }

            AddHeading(line, headings, anchors);
        }

        CloseUnterminatedFence(lines.Count, codeState, codeBlocks);
    }

    /// <summary>
    /// 处理代码围栏的开启或关闭，并在关闭时产出复制文本。
    /// </summary>
    /// <param name="line">当前 Markdown 行。</param>
    /// <param name="lineIndex">当前零基行号。</param>
    /// <param name="state">围栏扫描状态。</param>
    /// <param name="codeBlocks">代码块结果集合。</param>
    /// <returns>当前行是否为围栏。</returns>
    private static bool TryHandleFence(
        string line,
        int lineIndex,
        CodeFenceState state,
        ICollection<DocumentationCodeBlock> codeBlocks)
    {
        var trimmed = line.TrimStart();
        if (!trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            return false;
        }

        if (!state.IsOpen)
        {
            state.Open(trimmed[3..].Trim(), lineIndex + 2);
            return true;
        }

        codeBlocks.Add(state.Close(codeBlocks.Count, lineIndex));
        return true;
    }

    /// <summary>
    /// 将未闭合围栏按文档末尾结束，确保 UI 仍可复制正文。
    /// </summary>
    /// <param name="lineCount">文档总行数。</param>
    /// <param name="state">围栏扫描状态。</param>
    /// <param name="codeBlocks">代码块结果集合。</param>
    private static void CloseUnterminatedFence(
        int lineCount,
        CodeFenceState state,
        ICollection<DocumentationCodeBlock> codeBlocks)
    {
        if (state.IsOpen)
        {
            codeBlocks.Add(state.Close(codeBlocks.Count, lineCount));
        }
    }

    /// <summary>
    /// 识别 ATX 标题并分配重复标题的稳定后缀锚点。
    /// </summary>
    /// <param name="line">当前 Markdown 行。</param>
    /// <param name="headings">标题结果集合。</param>
    /// <param name="anchorCounts">基础锚点出现次数。</param>
    private static void AddHeading(
        string line,
        ICollection<DocumentationHeading> headings,
        IDictionary<string, int> anchorCounts)
    {
        if (!TryParseHeading(line, out var level, out var title))
        {
            return;
        }

        var baseAnchor = CreateAnchor(title);
        anchorCounts.TryGetValue(baseAnchor, out var duplicateCount);
        anchorCounts[baseAnchor] = duplicateCount + 1;
        var anchor = duplicateCount == 0 ? baseAnchor : $"{baseAnchor}-{duplicateCount}";
        headings.Add(new DocumentationHeading(level, title, anchor));
    }

    /// <summary>
    /// 解析一至六级 ATX 标题并移除常见行内 Markdown 标记。
    /// </summary>
    /// <param name="line">当前 Markdown 行。</param>
    /// <param name="level">成功时返回标题层级。</param>
    /// <param name="title">成功时返回纯文本标题。</param>
    /// <returns>当前行是否为有效标题。</returns>
    private static bool TryParseHeading(string line, out int level, out string title)
    {
        level = 0;
        title = string.Empty;
        var trimmed = line.TrimStart();
        while (level < trimmed.Length && level < 6 && trimmed[level] == '#')
        {
            level++;
        }

        if (level == 0 || level >= trimmed.Length || !char.IsWhiteSpace(trimmed[level]))
        {
            return false;
        }

        var rawTitle = sClosingHashesRegex.Replace(trimmed[(level + 1)..].Trim(), string.Empty).Trim();
        title = StripInlineMarkdown(rawTitle);
        return title.Length > 0;
    }

    /// <summary>
    /// 去除链接目标和轻量行内标记，保留目录需要的可读文本。
    /// </summary>
    /// <param name="value">Markdown 标题正文。</param>
    /// <returns>纯文本标题。</returns>
    private static string StripInlineMarkdown(string value)
    {
        var withoutLinks = sInlineLinkRegex.Replace(value, "$1");
        return sInlineMarkerRegex.Replace(withoutLinks, string.Empty).Trim();
    }

    /// <summary>
    /// 生成不依赖 UI 框架的稳定页内锚点，并保留中文字符。
    /// </summary>
    /// <param name="title">纯文本标题。</param>
    /// <returns>基础锚点。</returns>
    private static string CreateAnchor(string title)
    {
        var builder = new StringBuilder(title.Length);
        var pendingDash = false;
        foreach (var character in title.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(character) || character == '_')
            {
                AppendPendingDash(builder, ref pendingDash);
                builder.Append(character);
            }
            else if (char.IsWhiteSpace(character) || character == '-')
            {
                pendingDash = builder.Length > 0;
            }
        }

        return builder.Length == 0 ? "section" : builder.ToString();
    }

    /// <summary>
    /// 仅在锚点已有正文时追加一个折叠后的连字符。
    /// </summary>
    /// <param name="builder">锚点缓冲区。</param>
    /// <param name="pendingDash">是否等待追加连字符。</param>
    private static void AppendPendingDash(StringBuilder builder, ref bool pendingDash)
    {
        if (pendingDash && builder.Length > 0 && builder[^1] != '-')
        {
            builder.Append('-');
        }

        pendingDash = false;
    }

    /// <summary>
    /// 统一换行符，保证代码复制文本在不同宿主上稳定。
    /// </summary>
    /// <param name="markdown">原始 Markdown。</param>
    /// <returns>使用换行符 LF 的正文。</returns>
    private static string NormalizeLineEndings(string markdown)
    {
        return markdown.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
    }

    /// <summary>
    /// 保存单个围栏代码块的扫描状态。
    /// </summary>
    private sealed class CodeFenceState
    {
        /// <summary>
        /// 获取当前是否位于代码围栏内。
        /// </summary>
        internal bool IsOpen { get; private set; }

        /// <summary>
        /// 获取当前代码块语言。
        /// </summary>
        internal string Language { get; private set; } = string.Empty;

        /// <summary>
        /// 获取代码正文的一基起始行。
        /// </summary>
        internal int StartLine { get; private set; }

        /// <summary>
        /// 获取当前代码正文行。
        /// </summary>
        internal List<string> Lines { get; } = new();

        /// <summary>
        /// 开启新代码围栏并清空上一次状态。
        /// </summary>
        /// <param name="language">围栏声明语言。</param>
        /// <param name="startLine">代码正文一基起始行。</param>
        internal void Open(string language, int startLine)
        {
            IsOpen = true;
            Language = language;
            StartLine = startLine;
            Lines.Clear();
        }

        /// <summary>
        /// 关闭围栏并返回不含围栏的代码块模型。
        /// </summary>
        /// <param name="index">文档内代码块序号。</param>
        /// <param name="endLine">代码正文一基结束行。</param>
        /// <returns>代码块只读模型。</returns>
        internal DocumentationCodeBlock Close(int index, int endLine)
        {
            var codeBlock = new DocumentationCodeBlock(
                index,
                Language,
                string.Join('\n', Lines),
                StartLine,
                Math.Max(StartLine - 1, endLine));
            IsOpen = false;
            Language = string.Empty;
            StartLine = 0;
            Lines.Clear();
            return codeBlock;
        }
    }
}
