namespace YokiFrame.Tooling.Application.Documentation.Internal;

/// <summary>
/// 保存已通过受控根校验的 Markdown 物理路径和公开相对路径。
/// </summary>
internal sealed class DocumentationFileLocation
{
    /// <summary>
    /// 创建已验证文档位置。
    /// </summary>
    /// <param name="fullPath">Markdown 绝对路径。</param>
    /// <param name="relativePath">相对 YokiFrame 包根的稳定路径。</param>
    /// <param name="sourceKind">所属受控根。</param>
    internal DocumentationFileLocation(
        string fullPath,
        string relativePath,
        DocumentationSourceKind sourceKind)
    {
        FullPath = fullPath;
        RelativePath = relativePath;
        SourceKind = sourceKind;
    }

    /// <summary>
    /// 获取 Markdown 绝对路径。
    /// </summary>
    internal string FullPath { get; }

    /// <summary>
    /// 获取相对 YokiFrame 包根的稳定路径。
    /// </summary>
    internal string RelativePath { get; }

    /// <summary>
    /// 获取所属受控根。
    /// </summary>
    internal DocumentationSourceKind SourceKind { get; }
}
