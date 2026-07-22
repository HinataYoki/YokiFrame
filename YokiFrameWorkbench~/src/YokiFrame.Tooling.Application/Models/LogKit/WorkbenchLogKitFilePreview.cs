namespace YokiFrame.Tooling.Application.Models.LogKit;

/// <summary>描述一次显式日志文件尾部预览结果。</summary>
public sealed record WorkbenchLogKitFilePreview
{
    /// <summary>创建文件预览；仅由 Application 命令用例使用。</summary>
    internal WorkbenchLogKitFilePreview(
        string kind, string path, string fileName, bool exists, long sizeBytes,
        string modifiedUtc, int lineCount, bool truncated, string content,
        string errorMessage, string transport, IReadOnlyList<string> evidencePaths)
    {
        Kind = kind;
        Path = path;
        FileName = fileName;
        Exists = exists;
        SizeBytes = sizeBytes;
        ModifiedUtc = modifiedUtc;
        LineCount = lineCount;
        Truncated = truncated;
        Content = content;
        ErrorMessage = errorMessage;
        Transport = transport;
        EvidencePaths = evidencePaths;
    }

    /// <summary>获取 editor 或 player 来源。</summary>
    public string Kind { get; }
    /// <summary>获取文件绝对路径。</summary>
    public string Path { get; }
    /// <summary>获取文件名。</summary>
    public string FileName { get; }
    /// <summary>获取文件是否存在。</summary>
    public bool Exists { get; }
    /// <summary>获取文件大小。</summary>
    public long SizeBytes { get; }
    /// <summary>获取最后修改 UTC 文本。</summary>
    public string ModifiedUtc { get; }
    /// <summary>获取预览行数。</summary>
    public int LineCount { get; }
    /// <summary>获取正文是否为尾部裁剪结果。</summary>
    public bool Truncated { get; }
    /// <summary>获取有界尾部正文。</summary>
    public string Content { get; }
    /// <summary>获取文件不存在、权限或读取错误。</summary>
    public string ErrorMessage { get; }
    /// <summary>获取实际命令传输。</summary>
    public string Transport { get; }
    /// <summary>获取命令和响应证据。</summary>
    public IReadOnlyList<string> EvidencePaths { get; }
}
