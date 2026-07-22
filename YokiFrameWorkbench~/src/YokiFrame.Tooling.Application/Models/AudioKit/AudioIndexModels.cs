namespace YokiFrame.Tooling.Application.Models.AudioKit;

/// <summary>定义 AudioKit 音频索引扫描和生成输入。</summary>
public sealed record AudioIndexRequest(
    string ProjectRoot,
    string ScanFolder,
    string OutputPath,
    string ManifestPath,
    string NamespaceName,
    string ClassName,
    int StartId);

/// <summary>描述稳定 ID、常量名和项目相对音频路径。</summary>
public sealed record AudioIndexEntry(
    int Id,
    string ConstantName,
    string Name,
    string Path,
    string FolderCategory);

/// <summary>描述一次索引扫描或生成结果。</summary>
public sealed record AudioIndexResult(
    IReadOnlyList<AudioIndexEntry> Entries,
    string GeneratedFile,
    string ManifestFile,
    bool ManifestChanged);
