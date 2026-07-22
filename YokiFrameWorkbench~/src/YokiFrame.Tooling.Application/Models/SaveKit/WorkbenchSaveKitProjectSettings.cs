namespace YokiFrame.Tooling.Application.Models.SaveKit;

/// <summary>SaveKit Workbench 当前项目配置和存档文件扫描结果。</summary>
public sealed record WorkbenchSaveKitProjectSettings(
    string EngineId,
    string EngineLabel,
    bool IsSupported,
    string ConfigPath,
    string Fingerprint,
    string StoragePath,
    string FileExtension,
    string ResolvedStoragePath,
    bool DirectoryExists,
    IReadOnlyList<WorkbenchSaveKitFile> Files,
    string StatusText)
{
    /// <summary>获取 Slot 文件数量。</summary>
    public int SlotCount => Files.Count(static file => file.Kind == "Slot");

    /// <summary>获取 Global 文件数量。</summary>
    public int GlobalCount => Files.Count(static file => file.Kind == "Global");

    /// <summary>获取全部文件数量。</summary>
    public int FileCount => Files.Count;
}
