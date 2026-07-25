using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using YokiFrame.Tooling.Application.Models.TableKit;

namespace YokiFrame.Tooling.Application.Services.TableKit;

/// <summary>在 Luban 成功后直接生成项目侧 TableKit 代码和宿主程序集边界。</summary>
internal sealed partial class TableKitCodeGenerationService
{
    private const string LEGACY_MANIFEST_FILE_NAME = "tablekit.manifest.json";
    private const string GENERATED_HELPER_MARKER = "// Source: Luban bean mapper constructor.";
    private const string LEGACY_GENERATED_HELPER_MARKER = "// 由 YokiFrame Workbench TableKit 直接生成，请在 External/ 中放置用户自定义转换代码。";

    /// <summary>生成当前 C# target 的门面、加载契约、可选辅助代码和宿主项目文件。</summary>
    /// <param name="options">Workbench 页面选项。</param>
    /// <param name="contract">已解析的 Luban target 契约。</param>
    /// <returns>本次直接生成并保留的项目文件。</returns>
    public IReadOnlyList<string> Generate(TableKitOptions options, TableKitContract contract)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(contract);
        Directory.CreateDirectory(contract.OutputCodeDirectory);
        DeleteLegacyManifest(contract.OutputCodeDirectory);
        if (!contract.CodeTarget.StartsWith("cs-", StringComparison.OrdinalIgnoreCase))
        {
            return Array.Empty<string>();
        }

        TableKitProjectKind projectKind = DetectProjectKind(options.ProjectRoot);
        List<string> files = GenerateRuntimeFiles(options, contract);
        if (projectKind == TableKitProjectKind.Unity)
        {
            GenerateUnityBoundary(options, contract, files);
        }
        else
        {
            GenerateGodotBoundary(options, contract, files);
        }
        return files;
    }

    /// <summary>生成两端共用的 Runtime C# 文件，并保持用户 External 目录不受影响。</summary>
    /// <param name="options">包含运行时路径和辅助文件开关的页面选项。</param>
    /// <param name="contract">包含实际 manager 与输出根的生成契约。</param>
    /// <returns>生成文件列表。</returns>
    private static List<string> GenerateRuntimeFiles(
        TableKitOptions options,
        TableKitContract contract)
    {
        List<string> files = new();
        AddGeneratedFile(files, contract.OutputCodeDirectory, "ITableDataLoader.cs", BuildLoaderSource());
        AddGeneratedFile(files, contract.OutputCodeDirectory, "TableKit.cs", BuildFacadeSource(options, contract));
        IReadOnlyList<IGrouping<(string Namespace, string TypeName), TableKitExternalTypeMapping>> helperGroups =
            contract.GenerateExternalTypeUtil
                ? contract.ExternalTypeMappings
                    .GroupBy(static mapping => (Namespace: mapping.HelperNamespace, TypeName: mapping.HelperTypeName))
                    .OrderBy(static group => group.Key.Namespace, StringComparer.Ordinal)
                    .ThenBy(static group => group.Key.TypeName, StringComparer.Ordinal)
                    .ToArray()
                : Array.Empty<IGrouping<(string Namespace, string TypeName), TableKitExternalTypeMapping>>();
        EnsureUniqueHelperFileNames(helperGroups);
        HashSet<string> activeHelperFiles = helperGroups
            .Select(static group => group.Key.TypeName + ".cs")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        DeleteStaleGeneratedHelpers(contract.OutputCodeDirectory, activeHelperFiles);
        foreach (IGrouping<(string Namespace, string TypeName), TableKitExternalTypeMapping> helperGroup in helperGroups)
        {
            AddGeneratedFile(
                files,
                contract.OutputCodeDirectory,
                helperGroup.Key.TypeName + ".cs",
                BuildExternalTypeHelperSource(helperGroup.Key.Namespace, helperGroup.Key.TypeName, helperGroup.ToArray()));
        }
        return files;
    }

    /// <summary>拒绝不同命名空间生成同名 helper 文件，避免后写入覆盖先写入。</summary>
    /// <param name="helperGroups">按命名空间和类型分组的 mapper。</param>
    private static void EnsureUniqueHelperFileNames(
        IReadOnlyList<IGrouping<(string Namespace, string TypeName), TableKitExternalTypeMapping>> helperGroups)
    {
        string? duplicate = helperGroups
            .GroupBy(static group => group.Key.TypeName, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(static group => group.Count() > 1)
            ?.Key;
        if (duplicate != null)
        {
            throw new InvalidDataException("Luban mapper 包含不同命名空间的同名 helper，无法写入同一 TableKit 根: " + duplicate);
        }
    }

    /// <summary>删除已不再由当前 mapper 使用的自动生成 helper，并保留用户自定义文件。</summary>
    /// <param name="outputDirectory">TableKit 代码根。</param>
    /// <param name="activeFileNames">本次仍会生成的 helper 文件名。</param>
    private static void DeleteStaleGeneratedHelpers(string outputDirectory, IReadOnlySet<string> activeFileNames)
    {
        foreach (string path in Directory.EnumerateFiles(outputDirectory, "*.cs", SearchOption.TopDirectoryOnly))
        {
            if (activeFileNames.Contains(Path.GetFileName(path))) continue;
            string content = File.ReadAllText(path);
            if (!content.Contains(GENERATED_HELPER_MARKER, StringComparison.Ordinal)
                && !content.Contains(LEGACY_GENERATED_HELPER_MARKER, StringComparison.Ordinal))
            {
                continue;
            }
            DeleteFileIfExists(path);
            DeleteFileIfExists(path + ".meta");
        }
    }

    /// <summary>提交一个生成文件并把绝对路径加入操作结果。</summary>
    /// <param name="files">接收生成文件路径的列表。</param>
    /// <param name="outputDirectory">TableKit 代码根。</param>
    /// <param name="fileName">生成文件名。</param>
    /// <param name="content">使用固定 LF 的完整内容。</param>
    private static void AddGeneratedFile(
        List<string> files,
        string outputDirectory,
        string fileName,
        string content)
    {
        string path = Path.Combine(outputDirectory, fileName);
        WriteAtomically(path, content);
        files.Add(path);
    }

    /// <summary>通过项目事实识别 Unity 或 Godot，拒绝把程序集文件生成到未知宿主。</summary>
    /// <param name="projectRoot">规范化前的宿主项目根。</param>
    /// <returns>唯一识别出的项目类型。</returns>
    private static TableKitProjectKind DetectProjectKind(string projectRoot)
    {
        string root = Path.GetFullPath(projectRoot);
        bool isUnity = File.Exists(Path.Combine(root, "ProjectSettings", "ProjectVersion.txt"));
        bool isGodot = File.Exists(Path.Combine(root, "project.godot"));
        if (isUnity == isGodot)
        {
            throw new InvalidDataException("TableKit 无法唯一识别 Unity 或 Godot .NET 项目。");
        }
        return isUnity ? TableKitProjectKind.Unity : TableKitProjectKind.Godot;
    }

    /// <summary>清理旧版中转 manifest 及其 Unity meta，不触碰其它用户文件。</summary>
    /// <param name="outputDirectory">TableKit 代码根。</param>
    private static void DeleteLegacyManifest(string outputDirectory)
    {
        string manifestPath = Path.Combine(outputDirectory, LEGACY_MANIFEST_FILE_NAME);
        DeleteFileIfExists(manifestPath);
        DeleteFileIfExists(manifestPath + ".meta");
    }

    /// <summary>删除明确归属生成器的单个旧文件。</summary>
    /// <param name="path">待删除文件绝对路径。</param>
    private static void DeleteFileIfExists(string path)
    {
        if (File.Exists(path)) File.Delete(path);
    }

    /// <summary>以 UTF-8、同目录临时文件和原子替换提交完整生成文本。</summary>
    /// <param name="path">目标文件绝对路径。</param>
    /// <param name="content">完整文件内容。</param>
    private static void WriteAtomically(string path, string content)
    {
        string normalized = NormalizeGeneratedText(content);
        if (File.Exists(path) && string.Equals(File.ReadAllText(path), normalized, StringComparison.Ordinal)) return;
        string temporaryPath = path + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            using (FileStream stream = new(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (StreamWriter writer = new(stream, new UTF8Encoding(false)))
            {
                writer.Write(normalized);
                writer.Flush();
                stream.Flush(true);
            }
            if (File.Exists(path)) File.Replace(temporaryPath, path, null);
            else File.Move(temporaryPath, path);
        }
        finally
        {
            DeleteFileIfExists(temporaryPath);
        }
    }

    /// <summary>把生成文本统一为 LF 和末尾单换行。</summary>
    /// <param name="content">原始生成文本。</param>
    /// <returns>可稳定比较和提交的文本。</returns>
    private static string NormalizeGeneratedText(string content)
    {
        return content.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').TrimEnd('\n') + "\n";
    }

    /// <summary>转义生成 C# 字符串字面量中的控制字符、反斜杠和引号。</summary>
    /// <param name="value">原始配置文本。</param>
    /// <returns>可写入双引号字面量的内容。</returns>
    private static string Escape(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        string serialized = JsonSerializer.Serialize(value, TableKitCodeGenerationJsonContext.Default.String);
        return serialized[1..^1];
    }

    /// <summary>表示 TableKit 生成器支持的宿主项目类型。</summary>
    private enum TableKitProjectKind
    {
        Unity,
        Godot
    }
}

/// <summary>为 TableKit 生成器提供 Native AOT 可用的字符串 JSON 元数据。</summary>
[JsonSourceGenerationOptions]
[JsonSerializable(typeof(string))]
internal sealed partial class TableKitCodeGenerationJsonContext : JsonSerializerContext
{
}
