using System.Text.RegularExpressions;

namespace YokiFrame.Tooling.Application.Documentation.Internal;

/// <summary>
/// 从人工 Markdown 中提取首版 Kit、类型、方法和错误码搜索词。
/// </summary>
internal static class DocumentationKeywordExtractor
{
    private static readonly Regex sKitRegex = new(
        @"\b[A-Z][A-Za-z0-9_]*Kit\b",
        RegexOptions.CultureInvariant);
    private static readonly Regex sMethodRegex = new(
        @"\b[A-Z][A-Za-z0-9_]*(?=\s*\()",
        RegexOptions.CultureInvariant);
    private static readonly Regex sTypeRegex = new(
        @"\bI?[A-Z][A-Za-z0-9_]*(?:<[^>`\r\n]+>)?",
        RegexOptions.CultureInvariant);
    private static readonly Regex sErrorCodeRegex = new(
        @"\b[A-Z][A-Za-z0-9_]*(?:Unknown|Invalid|Mismatch|NotFound|TooLarge|Busy|Timeout|Failed|Failure|Error|Exception|Denied|Conflict|Unsupported|Incomplete)[A-Za-z0-9_]*\b|\b(?:Unknown|Invalid|Mismatch|NotFound|TooLarge|Busy|Timeout|Failed|Failure|Error|Exception|Denied|Conflict|Unsupported|Incomplete)[A-Z][A-Za-z0-9_]*\b",
        RegexOptions.CultureInvariant);

    /// <summary>
    /// 提取并去重 Markdown 中的四类搜索关键词。
    /// </summary>
    /// <param name="markdown">Markdown 正文。</param>
    /// <returns>按分类和文本排序的关键词。</returns>
    internal static IReadOnlyList<DocumentationKeyword> Extract(string markdown)
    {
        var keywords = new Dictionary<string, DocumentationKeyword>(StringComparer.OrdinalIgnoreCase);
        AddMatches(keywords, markdown, sKitRegex, DocumentationKeywordKind.Kit);
        AddMatches(keywords, markdown, sTypeRegex, DocumentationKeywordKind.Type);
        AddMatches(keywords, markdown, sMethodRegex, DocumentationKeywordKind.Method);
        AddMatches(keywords, markdown, sErrorCodeRegex, DocumentationKeywordKind.ErrorCode);
        return keywords.Values
            .OrderBy(static item => item.Kind)
            .ThenBy(static item => item.Text, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>
    /// 把单类正则命中写入带分类的去重字典。
    /// </summary>
    /// <param name="keywords">关键词去重字典。</param>
    /// <param name="markdown">Markdown 正文。</param>
    /// <param name="regex">当前分类提取规则。</param>
    /// <param name="kind">关键词分类。</param>
    private static void AddMatches(
        IDictionary<string, DocumentationKeyword> keywords,
        string markdown,
        Regex regex,
        DocumentationKeywordKind kind)
    {
        foreach (Match match in regex.Matches(markdown))
        {
            var text = NormalizeKeyword(match.Value);
            if (text.Length == 0)
            {
                continue;
            }

            keywords[$"{kind}:{text}"] = new DocumentationKeyword(text, kind);
        }
    }

    /// <summary>
    /// 移除类型泛型实参，使类型搜索可稳定命中声明名。
    /// </summary>
    /// <param name="value">正则命中文本。</param>
    /// <returns>规范化关键词。</returns>
    private static string NormalizeKeyword(string value)
    {
        var genericStart = value.IndexOf('<');
        return (genericStart >= 0 ? value[..genericStart] : value).Trim();
    }
}
