using YokiFrame.Tooling.Application.Models.AudioKit;

namespace YokiFrame.Tooling.Application.Services.AudioKit;

/// <summary>扫描项目音频并通过持久化 manifest 分配稳定整数 ID。</summary>
public sealed partial class AudioIndexService
{
    private static readonly HashSet<string> sAudioExtensions = new(
        new[] { ".wav", ".mp3", ".ogg", ".aiff", ".aif", ".flac", ".m4a" },
        StringComparer.OrdinalIgnoreCase);

    /// <summary>扫描音频并预览稳定 ID，不修改项目文件。</summary>
    /// <param name="request">项目根、扫描范围和生成配置。</param>
    /// <returns>按项目相对路径稳定排序的索引结果。</returns>
    public AudioIndexResult Scan(AudioIndexRequest request)
    {
        ValidatedRequest validated = ValidateRequest(request);
        AudioIndexManifest manifest = ReadManifest(validated.ManifestPath);
        IReadOnlyList<AudioFile> files = ScanFiles(validated.ProjectRoot, validated.ScanRoot);
        IReadOnlyList<AudioIndexEntry> entries = AssignEntries(files, manifest, validated.StartId);
        return new AudioIndexResult(
            entries, validated.OutputPath, validated.ManifestPath,
            HasManifestChanges(entries, manifest));
    }

    /// <summary>扫描并原子写入稳定 manifest 与 C# ID/路径映射。</summary>
    /// <param name="request">项目根、扫描范围和生成配置。</param>
    /// <returns>已写入文件路径和最终条目。</returns>
    public AudioIndexResult Generate(AudioIndexRequest request)
    {
        ValidatedRequest validated = ValidateRequest(request);
        AudioIndexManifest manifest = ReadManifest(validated.ManifestPath);
        IReadOnlyList<AudioFile> files = ScanFiles(validated.ProjectRoot, validated.ScanRoot);
        IReadOnlyList<AudioIndexEntry> entries = AssignEntries(files, manifest, validated.StartId);
        if (entries.Count == 0) throw new InvalidDataException("AudioKit scan found no supported audio files.");
        bool manifestChanged = HasManifestChanges(entries, manifest);
        MergeAssignments(manifest, entries);
        string source = BuildSource(entries, validated.NamespaceName, validated.ClassName);
        WriteGeneratedOutputs(validated.OutputPath, source, validated.ManifestPath, manifest);
        return new AudioIndexResult(entries, validated.OutputPath, validated.ManifestPath, manifestChanged);
    }

    /// <summary>验证路径、标识符和起始 ID，并解析项目内绝对路径。</summary>
    private static ValidatedRequest ValidateRequest(AudioIndexRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        string projectRoot = Path.GetFullPath(RequireText(request.ProjectRoot, nameof(request.ProjectRoot)));
        if (!Directory.Exists(projectRoot)) throw new DirectoryNotFoundException("AudioKit project root does not exist: " + projectRoot);
        string scanRoot = ResolveInside(projectRoot, request.ScanFolder, nameof(request.ScanFolder));
        string outputPath = ResolveInside(projectRoot, request.OutputPath, nameof(request.OutputPath));
        string manifestPath = ResolveInside(projectRoot, request.ManifestPath, nameof(request.ManifestPath));
        if (!Directory.Exists(scanRoot)) throw new DirectoryNotFoundException("AudioKit scan folder does not exist: " + scanRoot);
        if (!string.Equals(Path.GetExtension(outputPath), ".cs", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("AudioKit output path must use the .cs extension.");
        if (!string.Equals(Path.GetExtension(manifestPath), ".json", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("AudioKit manifest path must use the .json extension.");
        if (request.StartId <= 0) throw new ArgumentOutOfRangeException(nameof(request.StartId));
        ValidateNamespace(request.NamespaceName);
        ValidateIdentifier(request.ClassName, nameof(request.ClassName));
        return new ValidatedRequest(projectRoot, scanRoot, outputPath, manifestPath,
            request.NamespaceName.Trim(), request.ClassName.Trim(), request.StartId);
    }

    /// <summary>递归扫描非符号链接音频文件并按规范化项目路径排序。</summary>
    private static IReadOnlyList<AudioFile> ScanFiles(string projectRoot, string scanRoot)
    {
        EnumerationOptions options = new()
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = false,
            AttributesToSkip = FileAttributes.ReparsePoint
        };
        List<AudioFile> files = new();
        foreach (string filePath in Directory.EnumerateFiles(scanRoot, "*", options))
        {
            if (!sAudioExtensions.Contains(Path.GetExtension(filePath))) continue;
            string fullPath = Path.GetFullPath(filePath);
            EnsureInside(projectRoot, fullPath, "audio file");
            string relativePath = NormalizePath(Path.GetRelativePath(projectRoot, fullPath));
            string scanRelative = NormalizePath(Path.GetRelativePath(scanRoot, fullPath));
            string category = ReadFolderCategory(scanRelative);
            files.Add(new AudioFile(relativePath, Path.GetFileNameWithoutExtension(fullPath), category));
        }
        files.Sort(static (left, right) => StringComparer.OrdinalIgnoreCase.Compare(left.Path, right.Path));
        return files;
    }

    /// <summary>保留 manifest 旧分配，并为新路径分配未使用的递增 ID。</summary>
    private static IReadOnlyList<AudioIndexEntry> AssignEntries(
        IReadOnlyList<AudioFile> files,
        AudioIndexManifest manifest,
        int startId)
    {
        ValidateManifestAssignments(manifest);
        HashSet<int> usedIds = new(manifest.Assignments.Values);
        Dictionary<string, string> constants = new(StringComparer.Ordinal);
        List<AudioIndexEntry> entries = new(files.Count);
        int nextId = startId;
        for (var index = 0; index < files.Count; index++)
        {
            AudioFile file = files[index];
            int id = manifest.Assignments.TryGetValue(file.Path, out int assigned)
                ? assigned
                : AllocateId(usedIds, ref nextId);
            string constantName = CreateConstantName(file.Name, file.FolderCategory);
            if (constants.TryGetValue(constantName, out string? previousPath))
                throw new InvalidDataException(
                    "AudioKit constant name collision " + constantName + ": " + previousPath + " and " + file.Path);
            constants.Add(constantName, file.Path);
            entries.Add(new AudioIndexEntry(id, constantName, file.Name, file.Path, file.FolderCategory));
        }
        return entries;
    }

    /// <summary>分配当前最小可用 ID 并推进游标。</summary>
    private static int AllocateId(HashSet<int> usedIds, ref int nextId)
    {
        while (usedIds.Contains(nextId)) nextId = checked(nextId + 1);
        int result = nextId;
        usedIds.Add(result);
        nextId = checked(nextId + 1);
        return result;
    }

    /// <summary>读取扫描根下第一级目录作为常量分类。</summary>
    private static string ReadFolderCategory(string scanRelativePath)
    {
        int separator = scanRelativePath.IndexOf('/');
        return separator <= 0 ? string.Empty : scanRelativePath[..separator];
    }

    /// <summary>要求配置文本非空并返回裁剪值。</summary>
    private static string RequireText(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("AudioKit " + name + " cannot be empty.", name);
        return value.Trim();
    }

    private sealed record AudioFile(string Path, string Name, string FolderCategory);
    private sealed record ValidatedRequest(
        string ProjectRoot,
        string ScanRoot,
        string OutputPath,
        string ManifestPath,
        string NamespaceName,
        string ClassName,
        int StartId);
}
