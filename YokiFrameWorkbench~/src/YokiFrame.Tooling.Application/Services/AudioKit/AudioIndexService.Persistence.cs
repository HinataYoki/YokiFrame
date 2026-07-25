using System.Text.Json;
using System.Text.Json.Serialization;
using YokiFrame.Tooling.Application.Models.AudioKit;

namespace YokiFrame.Tooling.Application.Services.AudioKit;

/// <summary>承载 AudioKit 索引 manifest 验证和原子持久化。</summary>
public sealed partial class AudioIndexService
{
    /// <summary>读取可选 manifest；不存在时创建空分配表。</summary>
    private static AudioIndexManifest ReadManifest(string manifestPath)
    {
        if (!File.Exists(manifestPath)) return new AudioIndexManifest();
        try
        {
            AudioIndexManifest? manifest = JsonSerializer.Deserialize(
                File.ReadAllText(manifestPath), AudioIndexJsonContext.Default.AudioIndexManifest);
            if (manifest == null || manifest.SchemaVersion != 1 || manifest.Assignments == null)
                throw new InvalidDataException("AudioKit index manifest requires schemaVersion 1.");
            return manifest;
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("AudioKit index manifest is invalid JSON: " + manifestPath, exception);
        }
    }

    /// <summary>拒绝非正 ID、非规范路径和重复 ID。</summary>
    private static void ValidateManifestAssignments(AudioIndexManifest manifest)
    {
        HashSet<int> ids = new();
        foreach ((string path, int id) in manifest.Assignments)
        {
            if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path)
                || path.Split('/', '\\').Any(static segment => segment == ".."))
                throw new InvalidDataException("AudioKit manifest contains an invalid project-relative path.");
            if (id <= 0 || !ids.Add(id))
                throw new InvalidDataException("AudioKit manifest contains an invalid or duplicate ID: " + id);
        }
    }

    /// <summary>判断本轮 active 条目是否需要更新 manifest。</summary>
    private static bool HasManifestChanges(
        IReadOnlyList<AudioIndexEntry> entries,
        AudioIndexManifest manifest)
    {
        for (var index = 0; index < entries.Count; index++)
        {
            AudioIndexEntry entry = entries[index];
            if (!manifest.Assignments.TryGetValue(entry.Path, out int id) || id != entry.Id) return true;
        }
        return false;
    }

    /// <summary>合并 active 路径分配，同时保留已移除路径的历史 ID。</summary>
    private static void MergeAssignments(
        AudioIndexManifest manifest,
        IReadOnlyList<AudioIndexEntry> entries)
    {
        for (var index = 0; index < entries.Count; index++)
            manifest.Assignments[entries[index].Path] = entries[index].Id;
    }

    /// <summary>按路径稳定排序后原子写入 manifest。</summary>
    private static void WriteManifest(string manifestPath, AudioIndexManifest manifest)
    {
        AudioIndexManifest stable = new()
        {
            Assignments = manifest.Assignments
                .OrderBy(static pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.OrdinalIgnoreCase)
        };
        WriteAtomically(
            manifestPath,
            JsonSerializer.Serialize(stable, AudioIndexJsonContext.Default.AudioIndexManifest)
                + System.Environment.NewLine);
    }

    /// <summary>先提交生成映射，再提交稳定账本；账本失败时恢复映射，避免只推进 ID 分配。</summary>
    private static void WriteGeneratedOutputs(
        string outputPath,
        string source,
        string manifestPath,
        AudioIndexManifest manifest)
    {
        TextFileBackup outputBackup = CaptureTextFile(outputPath, "AudioKit output");
        var sourceWritten = false;
        try
        {
            WriteAtomically(outputPath, source);
            sourceWritten = true;
            WriteManifest(manifestPath, manifest);
        }
        catch (Exception exception)
        {
            if (!sourceWritten) throw;
            try
            {
                RestoreTextFile(outputPath, outputBackup);
            }
            catch (Exception rollbackException)
            {
                throw new IOException(
                    "AudioKit index generation failed and could not restore the generated source.",
                    new AggregateException(exception, rollbackException));
            }

            throw;
        }
    }

    /// <summary>读取生成文件的原始文本；目录目标直接拒绝，避免后续回滚破坏目录。</summary>
    private static TextFileBackup CaptureTextFile(string path, string description)
    {
        if (Directory.Exists(path))
        {
            throw new IOException(description + " path is a directory: " + path);
        }

        return File.Exists(path)
            ? new TextFileBackup(true, File.ReadAllText(path))
            : new TextFileBackup(false, string.Empty);
    }

    /// <summary>恢复生成前的源文件状态；原本不存在时只删除本次已写入的普通文件。</summary>
    private static void RestoreTextFile(string path, TextFileBackup backup)
    {
        if (backup.Exists)
        {
            WriteAtomically(path, backup.Content);
        }
        else if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    /// <summary>在目标目录写临时文件并以同卷替换，失败时清理临时文件。</summary>
    private static void WriteAtomically(string targetPath, string content)
    {
        string? directory = Path.GetDirectoryName(targetPath);
        if (string.IsNullOrEmpty(directory)) throw new InvalidDataException("AudioKit output has no parent directory.");
        Directory.CreateDirectory(directory);
        string temporaryPath = Path.Combine(directory, "." + Path.GetFileName(targetPath) + "." + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            File.WriteAllText(temporaryPath, content);
            File.Move(temporaryPath, targetPath, true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    /// <summary>解析项目内路径并拒绝越界结果。</summary>
    private static string ResolveInside(string projectRoot, string value, string name)
    {
        string configured = RequireText(value, name);
        string candidate = Path.GetFullPath(Path.IsPathRooted(configured)
            ? configured
            : Path.Combine(projectRoot, configured));
        EnsureInside(projectRoot, candidate, name);
        return candidate;
    }

    /// <summary>使用相对路径语义确认候选路径位于项目根。</summary>
    private static void EnsureInside(string projectRoot, string candidate, string name)
    {
        string relative = Path.GetRelativePath(projectRoot, candidate);
        if (Path.IsPathRooted(relative) || relative == ".."
            || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new InvalidDataException("AudioKit " + name + " must stay inside the project root.");
    }

    /// <summary>把平台路径分隔符转换为发布稳定正斜杠。</summary>
    private static string NormalizePath(string path) => path.Replace('\\', '/');

    private sealed class AudioIndexManifest
    {
        public int SchemaVersion { get; set; } = 1;
        public Dictionary<string, int> Assignments { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>为 AudioKit manifest 提供 Native AOT 可用的 JSON 元数据。</summary>
    [JsonSourceGenerationOptions(
        PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
        WriteIndented = true)]
    [JsonSerializable(typeof(AudioIndexManifest))]
    private sealed partial class AudioIndexJsonContext : JsonSerializerContext
    {
    }

    /// <summary>保存一次生成事务开始前的文本文件状态，供 manifest 提交失败时恢复。</summary>
    private sealed record TextFileBackup(bool Exists, string Content);
}
