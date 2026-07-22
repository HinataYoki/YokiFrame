namespace YokiFrame.Tooling.Application.Documentation.Internal;

/// <summary>
/// 在尚未接入 XML API 索引器时提供稳定的空数据来源。
/// </summary>
internal sealed class EmptyDocumentationApiIndexSource : IDocumentationApiIndexSource
{
    /// <summary>
    /// 返回空 API 索引，保留未来 XML 投影器的替换入口。
    /// </summary>
    /// <returns>空条目集合。</returns>
    public IReadOnlyList<DocumentationApiIndexEntry> ReadEntries()
    {
        return Array.Empty<DocumentationApiIndexEntry>();
    }
}
