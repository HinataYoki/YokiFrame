namespace YokiFrame.Packaging.Models;

/// <summary>
/// 描述一个平台目录中的运行产物。
/// </summary>
public sealed class RuntimePlatformManifest
{
    /// <summary>
    /// 创建平台运行产物 manifest。
    /// </summary>
    /// <param name="platform">平台标识。</param>
    /// <param name="runtimeIdentifier">.NET runtime identifier。</param>
    /// <param name="sharedRuntime">GUI 和 CLI 是否共用运行时文件；Native AOT 的两个入口均可存在但此值为 false。</param>
    /// <param name="entrypoint">旧版 GUI 入口相对路径；保留给已有 Unity 启动器兼容。</param>
    /// <param name="guiEntry">Workbench GUI 入口相对路径。</param>
    /// <param name="cliEntry">轻量 CLI 入口相对路径；未发布 CLI 时为空。</param>
    /// <param name="fileCount">文件数量。</param>
    /// <param name="totalBytes">总大小。</param>
    /// <param name="files">文件记录。</param>
    public RuntimePlatformManifest(
        string platform,
        string runtimeIdentifier,
        bool sharedRuntime,
        string entrypoint,
        string guiEntry,
        string cliEntry,
        int fileCount,
        long totalBytes,
        IReadOnlyList<RuntimeManifestFile> files)
    {
        Platform = platform ?? string.Empty;
        RuntimeIdentifier = runtimeIdentifier ?? string.Empty;
        SharedRuntime = sharedRuntime;
        Entrypoint = entrypoint ?? string.Empty;
        GuiEntry = guiEntry ?? string.Empty;
        CliEntry = cliEntry ?? string.Empty;
        FileCount = fileCount;
        TotalBytes = totalBytes;
        Files = files ?? Array.Empty<RuntimeManifestFile>();
    }

    /// <summary>
    /// 获取平台标识。
    /// </summary>
    public string Platform { get; }

    /// <summary>
    /// 获取 .NET runtime identifier。
    /// </summary>
    public string RuntimeIdentifier { get; }

    /// <summary>
    /// 获取 GUI 和 CLI 是否共用运行时文件；false 不代表 CLI 缺席。
    /// </summary>
    public bool SharedRuntime { get; }

    /// <summary>
    /// 获取旧版入口文件相对路径；当前保持等于 GUI 入口，便于旧启动器兼容。
    /// </summary>
    public string Entrypoint { get; }

    /// <summary>
    /// 获取 Workbench GUI 入口相对路径。
    /// </summary>
    public string GuiEntry { get; }

    /// <summary>
    /// 获取轻量 CLI 入口相对路径。
    /// </summary>
    public string CliEntry { get; }

    /// <summary>
    /// 获取文件数量。
    /// </summary>
    public int FileCount { get; }

    /// <summary>
    /// 获取总大小。
    /// </summary>
    public long TotalBytes { get; }

    /// <summary>
    /// 获取文件记录。
    /// </summary>
    public IReadOnlyList<RuntimeManifestFile> Files { get; }
}
