using System.Text;
using System.Text.Json;
using YokiFrame.Client.FileBridge.IO;
using YokiFrame.Protocol.ProjectModel;
using YokiFrame.Protocol.Results;

namespace YokiFrame.Client.ProjectModel;

/// <summary>
/// 承载 Project Model 文档序列化、结构校验和完整性验证。
/// </summary>
public sealed partial class ProjectModelFileStore
{
    /// <summary>
    /// 验证提交 bundle 的五文件 schema、kind、generation 和 modelId 一致性。
    /// </summary>
    /// <param name="bundle">待提交 bundle。</param>
    private void ValidateBundleForCommit(ProjectModelBundle bundle)
    {
        if (bundle.Manifest == null
            || bundle.Architecture == null
            || bundle.Capabilities == null
            || bundle.Dependencies == null
            || bundle.ValidationProfile == null)
        {
            throw CreateStoreException("ProjectModelInvalid", "Project Model bundle contains a null document.", GetEvidencePaths());
        }

        ValidateHeader(bundle.Manifest.SchemaVersion, bundle.Manifest.Kind, bundle.Manifest.ModelGeneration, bundle.Manifest.ModelId, ProjectModelContract.PROJECT_MODEL_KIND);
        ValidateHeader(bundle.Architecture.SchemaVersion, bundle.Architecture.Kind, bundle.Architecture.ModelGeneration, bundle.Architecture.ModelId, ProjectModelContract.ARCHITECTURE_KIND);
        ValidateHeader(bundle.Capabilities.SchemaVersion, bundle.Capabilities.Kind, bundle.Capabilities.ModelGeneration, bundle.Capabilities.ModelId, ProjectModelContract.CAPABILITIES_KIND);
        ValidateHeader(bundle.Dependencies.SchemaVersion, bundle.Dependencies.Kind, bundle.Dependencies.ModelGeneration, bundle.Dependencies.ModelId, ProjectModelContract.DEPENDENCIES_KIND);
        ValidateHeader(bundle.ValidationProfile.SchemaVersion, bundle.ValidationProfile.Kind, bundle.ValidationProfile.ModelGeneration, bundle.ValidationProfile.ModelId, ProjectModelContract.VALIDATION_PROFILE_KIND);
        EnsureSharedIdentity(bundle.Manifest.ModelGeneration, bundle.Manifest.ModelId, bundle.Architecture.ModelGeneration, bundle.Architecture.ModelId);
        EnsureSharedIdentity(bundle.Manifest.ModelGeneration, bundle.Manifest.ModelId, bundle.Capabilities.ModelGeneration, bundle.Capabilities.ModelId);
        EnsureSharedIdentity(bundle.Manifest.ModelGeneration, bundle.Manifest.ModelId, bundle.Dependencies.ModelGeneration, bundle.Dependencies.ModelId);
        EnsureSharedIdentity(bundle.Manifest.ModelGeneration, bundle.Manifest.ModelId, bundle.ValidationProfile.ModelGeneration, bundle.ValidationProfile.ModelId);
    }

    /// <summary>
    /// 序列化四个叶文档；manifest 在调用方完成 refs 后单独最后写入。
    /// </summary>
    /// <param name="bundle">已通过结构校验的 bundle。</param>
    /// <returns>按固定文件名索引的叶文档字节。</returns>
    private static IReadOnlyDictionary<string, byte[]> SerializeLeaves(ProjectModelBundle bundle)
    {
        return new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            [ProjectModelContract.ARCHITECTURE_FILE_NAME] = SerializeUtf8(bundle.Architecture.ToJson()),
            [ProjectModelContract.CAPABILITIES_FILE_NAME] = SerializeUtf8(bundle.Capabilities.ToJson()),
            [ProjectModelContract.DEPENDENCIES_FILE_NAME] = SerializeUtf8(bundle.Dependencies.ToJson()),
            [ProjectModelContract.VALIDATION_PROFILE_FILE_NAME] = SerializeUtf8(bundle.ValidationProfile.ToJson())
        };
    }

    /// <summary>
    /// 按固定顺序把四个叶文档写入 staging，manifest 不在此阶段出现。
    /// </summary>
    /// <param name="stagingPath">同卷 staging 目录。</param>
    /// <param name="leafBytes">叶文档字节集合。</param>
    private static void WriteStagedLeaves(string stagingPath, IReadOnlyDictionary<string, byte[]> leafBytes)
    {
        foreach (var fileName in ProjectModelContract.FILE_NAMES.Skip(1))
        {
            WriteStagedFile(Path.Combine(stagingPath, fileName), leafBytes[fileName]);
        }
    }

    /// <summary>
    /// 使用 CreateNew、WriteThrough 和 Flush(true) 写入单个 staging 文件。
    /// </summary>
    /// <param name="path">staging 文件路径。</param>
    /// <param name="bytes">完整 UTF-8 文件字节。</param>
    private static void WriteStagedFile(string path, byte[] bytes)
    {
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough);
        stream.Write(bytes, 0, bytes.Length);
        stream.Flush(flushToDisk: true);
    }

    /// <summary>
    /// 校验 manifest 的根字段和四个叶文档引用，拒绝重复、未知或越界路径。
    /// </summary>
    /// <param name="manifest">待校验 manifest。</param>
    /// <param name="bundleRoot">bundle 目录，用于证据路径。</param>
    private void ValidateManifest(ProjectModelManifest manifest, string bundleRoot)
    {
        ValidateHeader(manifest.SchemaVersion, manifest.Kind, manifest.ModelGeneration, manifest.ModelId, ProjectModelContract.PROJECT_MODEL_KIND);
        if (manifest.Documents == null || manifest.Documents.Count != 4)
        {
            throw CreateStoreException("ProjectModelInvalid", "Project Model manifest must contain exactly four leaf references.", new[] { Path.Combine(bundleRoot, ProjectModelContract.PROJECT_MODEL_FILE_NAME) });
        }

        var seenPaths = new HashSet<string>(StringComparer.Ordinal);
        var seenKinds = new HashSet<string>(StringComparer.Ordinal);
        var expectedKinds = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [ProjectModelContract.ARCHITECTURE_FILE_NAME] = ProjectModelContract.ARCHITECTURE_KIND,
            [ProjectModelContract.CAPABILITIES_FILE_NAME] = ProjectModelContract.CAPABILITIES_KIND,
            [ProjectModelContract.DEPENDENCIES_FILE_NAME] = ProjectModelContract.DEPENDENCIES_KIND,
            [ProjectModelContract.VALIDATION_PROFILE_FILE_NAME] = ProjectModelContract.VALIDATION_PROFILE_KIND
        };

        foreach (var reference in manifest.Documents)
        {
            if (reference == null
                || string.IsNullOrWhiteSpace(reference.Path)
                || !expectedKinds.TryGetValue(reference.Path, out var expectedKind)
                || !string.Equals(reference.Kind, expectedKind, StringComparison.Ordinal)
                || reference.SchemaVersion != ProjectModelContract.SCHEMA_VERSION
                || !IsSha256(reference.ContentHash)
                || !seenPaths.Add(reference.Path)
                || !seenKinds.Add(reference.Kind))
            {
                throw CreateStoreException("ProjectModelInvalid", "Project Model manifest contains an invalid or duplicate document reference.", new[] { Path.Combine(bundleRoot, ProjectModelContract.PROJECT_MODEL_FILE_NAME) });
            }
        }

        if (!expectedKinds.Keys.All(seenPaths.Contains))
        {
            throw CreateStoreException("ProjectModelInvalid", "Project Model manifest does not reference every fixed leaf document.", new[] { Path.Combine(bundleRoot, ProjectModelContract.PROJECT_MODEL_FILE_NAME) });
        }
    }

    /// <summary>
    /// 校验单个文档的 schema、kind、generation 和 modelId 字段。
    /// </summary>
    /// <param name="schemaVersion">文档 schema 版本。</param>
    /// <param name="kind">文档 kind。</param>
    /// <param name="modelGeneration">文档代次。</param>
    /// <param name="modelId">模型稳定标识。</param>
    /// <param name="expectedKind">期望 kind。</param>
    private void ValidateHeader(int schemaVersion, string kind, string modelGeneration, string modelId, string expectedKind)
    {
        if (schemaVersion != ProjectModelContract.SCHEMA_VERSION
            || !string.Equals(kind, expectedKind, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(modelGeneration)
            || string.IsNullOrWhiteSpace(modelId))
        {
            throw CreateStoreException(
                "ProjectModelInvalid",
                "Project Model document header is invalid for kind " + expectedKind + ".",
                GetEvidencePaths());
        }
    }

    /// <summary>
    /// 确认两个文档使用同一个 modelGeneration 和 modelId。
    /// </summary>
    /// <param name="expectedGeneration">期望代次。</param>
    /// <param name="expectedModelId">期望模型标识。</param>
    /// <param name="actualGeneration">实际代次。</param>
    /// <param name="actualModelId">实际模型标识。</param>
    private void EnsureSharedIdentity(string expectedGeneration, string expectedModelId, string actualGeneration, string actualModelId)
    {
        if (!string.Equals(expectedGeneration, actualGeneration, StringComparison.Ordinal)
            || !string.Equals(expectedModelId, actualModelId, StringComparison.Ordinal))
        {
            throw CreateStoreException(
                "ProjectModelGenerationMismatch",
                "Project Model documents do not share modelGeneration and modelId.",
                GetEvidencePaths());
        }
    }

    /// <summary>
    /// 判断字符串是否为固定长度的十六进制 SHA-256 摘要。
    /// </summary>
    /// <param name="value">待校验摘要。</param>
    /// <returns>格式正确时返回 true。</returns>
    private static bool IsSha256(string? value)
    {
        return value is { Length: 64 } && value.All(static character => Uri.IsHexDigit(character));
    }

    /// <summary>
    /// 根据实际叶文件字节创建固定顺序的 manifest refs。
    /// </summary>
    /// <param name="leafBytes">四个叶文件字节。</param>
    /// <returns>携带相对路径、kind、schema 和 SHA-256 的 refs。</returns>
    private static List<ProjectModelDocumentReference> CreateDocumentReferences(IReadOnlyDictionary<string, byte[]> leafBytes)
    {
        return new List<ProjectModelDocumentReference>
        {
            CreateReference(ProjectModelContract.ARCHITECTURE_FILE_NAME, ProjectModelContract.ARCHITECTURE_KIND, leafBytes),
            CreateReference(ProjectModelContract.CAPABILITIES_FILE_NAME, ProjectModelContract.CAPABILITIES_KIND, leafBytes),
            CreateReference(ProjectModelContract.DEPENDENCIES_FILE_NAME, ProjectModelContract.DEPENDENCIES_KIND, leafBytes),
            CreateReference(ProjectModelContract.VALIDATION_PROFILE_FILE_NAME, ProjectModelContract.VALIDATION_PROFILE_KIND, leafBytes)
        };
    }

    /// <summary>
    /// 创建单个叶文档引用，hash 直接来自待提交 UTF-8 字节。
    /// </summary>
    /// <param name="fileName">固定叶文件名。</param>
    /// <param name="kind">固定文档 kind。</param>
    /// <param name="leafBytes">叶文件字节集合。</param>
    /// <returns>完整文档引用。</returns>
    private static ProjectModelDocumentReference CreateReference(string fileName, string kind, IReadOnlyDictionary<string, byte[]> leafBytes)
    {
        return new ProjectModelDocumentReference
        {
            Kind = kind,
            Path = fileName,
            SchemaVersion = ProjectModelContract.SCHEMA_VERSION,
            ContentHash = ComputeHash(leafBytes[fileName])
        };
    }

    /// <summary>
    /// 读取并校验 architecture.json。
    /// </summary>
    /// <param name="bundleRoot">bundle 目录。</param>
    /// <param name="manifest">已验证 manifest。</param>
    /// <returns>架构文档。</returns>
    private ProjectArchitectureDocument ReadArchitecture(string bundleRoot, ProjectModelManifest manifest)
    {
        return ReadLeaf(bundleRoot, manifest, ProjectModelContract.ARCHITECTURE_FILE_NAME, ProjectModelContract.ARCHITECTURE_KIND, ProjectArchitectureDocument.FromJson,
            static document => document.SchemaVersion, static document => document.Kind, static document => document.ModelGeneration, static document => document.ModelId);
    }

    /// <summary>
    /// 读取并校验 capabilities.json。
    /// </summary>
    /// <param name="bundleRoot">bundle 目录。</param>
    /// <param name="manifest">已验证 manifest。</param>
    /// <returns>静态能力文档。</returns>
    private ProjectCapabilitiesDocument ReadCapabilities(string bundleRoot, ProjectModelManifest manifest)
    {
        return ReadLeaf(bundleRoot, manifest, ProjectModelContract.CAPABILITIES_FILE_NAME, ProjectModelContract.CAPABILITIES_KIND, ProjectCapabilitiesDocument.FromJson,
            static document => document.SchemaVersion, static document => document.Kind, static document => document.ModelGeneration, static document => document.ModelId);
    }

    /// <summary>
    /// 读取并校验 dependencies.json。
    /// </summary>
    /// <param name="bundleRoot">bundle 目录。</param>
    /// <param name="manifest">已验证 manifest。</param>
    /// <returns>依赖文档。</returns>
    private ProjectDependenciesDocument ReadDependencies(string bundleRoot, ProjectModelManifest manifest)
    {
        return ReadLeaf(bundleRoot, manifest, ProjectModelContract.DEPENDENCIES_FILE_NAME, ProjectModelContract.DEPENDENCIES_KIND, ProjectDependenciesDocument.FromJson,
            static document => document.SchemaVersion, static document => document.Kind, static document => document.ModelGeneration, static document => document.ModelId);
    }

    /// <summary>
    /// 读取并校验 validation-profile.json。
    /// </summary>
    /// <param name="bundleRoot">bundle 目录。</param>
    /// <param name="manifest">已验证 manifest。</param>
    /// <returns>验证策略文档。</returns>
    private ProjectValidationProfileDocument ReadValidationProfile(string bundleRoot, ProjectModelManifest manifest)
    {
        return ReadLeaf(bundleRoot, manifest, ProjectModelContract.VALIDATION_PROFILE_FILE_NAME, ProjectModelContract.VALIDATION_PROFILE_KIND, ProjectValidationProfileDocument.FromJson,
            static document => document.SchemaVersion, static document => document.Kind, static document => document.ModelGeneration, static document => document.ModelId);
    }

    /// <summary>
    /// 读取单个叶文件并验证 manifest ref、UTF-8、schema、kind、generation、modelId 与 hash。
    /// </summary>
    /// <typeparam name="TDocument">叶文档类型。</typeparam>
    /// <param name="bundleRoot">bundle 目录。</param>
    /// <param name="manifest">已验证 manifest。</param>
    /// <param name="fileName">固定叶文件名。</param>
    /// <param name="kind">固定叶 kind。</param>
    /// <param name="parser">协议 DTO 解析器。</param>
    /// <param name="schemaSelector">schema 字段选择器。</param>
    /// <param name="kindSelector">kind 字段选择器。</param>
    /// <param name="generationSelector">generation 字段选择器。</param>
    /// <param name="modelIdSelector">modelId 字段选择器。</param>
    /// <returns>经过完整性校验的叶文档。</returns>
    private TDocument ReadLeaf<TDocument>(string bundleRoot, ProjectModelManifest manifest, string fileName, string kind, Func<string, TDocument> parser,
        Func<TDocument, int> schemaSelector, Func<TDocument, string> kindSelector, Func<TDocument, string> generationSelector, Func<TDocument, string> modelIdSelector)
    {
        var path = Path.Combine(bundleRoot, fileName);
        var bytes = ReadRequiredFile(path);
        var reference = manifest.Documents.Single(item => string.Equals(item.Path, fileName, StringComparison.Ordinal));
        if (!string.Equals(reference.ContentHash, ComputeHash(bytes), StringComparison.OrdinalIgnoreCase))
        {
            throw CreateStoreException("ProjectModelHashMismatch", "Project Model leaf hash does not match its manifest reference: " + fileName, new[] { path, mPaths.ProjectModelManifestPath });
        }

        TDocument document;
        try
        {
            document = parser(sStrictUtf8.GetString(bytes));
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            throw CreateStoreException("ProjectModelInvalid", "Project Model leaf JSON is invalid: " + fileName + ". " + exception.Message, new[] { path });
        }

        ValidateHeader(schemaSelector(document), kindSelector(document), generationSelector(document), modelIdSelector(document), kind);
        EnsureSharedIdentity(manifest.ModelGeneration, manifest.ModelId, generationSelector(document), modelIdSelector(document));
        return document;
    }

    /// <summary>
    /// 解析 manifest JSON，并将语法损坏转换为带 evidence 的协议异常。
    /// </summary>
    /// <param name="bytes">manifest UTF-8 字节。</param>
    /// <param name="path">manifest 路径。</param>
    /// <returns>解析后的 manifest。</returns>
    private ProjectModelManifest ParseManifest(byte[] bytes, string path)
    {
        try
        {
            return ProjectModelManifest.FromJson(sStrictUtf8.GetString(bytes));
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            throw CreateStoreException("ProjectModelInvalid", "Project Model manifest JSON is invalid: " + exception.Message, new[] { path });
        }
    }

    /// <summary>
    /// 读取固定文件并拒绝缺失、重解析点、BOM 或非法 UTF-8。
    /// </summary>
    /// <param name="path">固定模型文件路径。</param>
    /// <returns>完整 UTF-8 文件字节。</returns>
    private byte[] ReadRequiredFile(string path)
    {
        EnsureNoReparsePoint(path);
        if (!File.Exists(path))
        {
            throw CreateStoreException("ProjectModelMissing", "Project Model file is missing: " + Path.GetFileName(path), new[] { path });
        }

        try
        {
            var bytes = File.ReadAllBytes(path);
            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            {
                throw CreateStoreException("ProjectModelEncodingInvalid", "Project Model files must be UTF-8 without a BOM.", new[] { path });
            }

            _ = sStrictUtf8.GetString(bytes);
            return bytes;
        }
        catch (YokiFrameProtocolException)
        {
            throw;
        }
        catch (Exception exception) when (exception is DecoderFallbackException or IOException or UnauthorizedAccessException)
        {
            throw CreateStoreException("ProjectModelReadFailed", "Project Model file cannot be read: " + exception.Message, new[] { path });
        }
    }

    /// <summary>
    /// 检查目录中只存在五个受管文件，防止目录替换静默删除未知用户内容。
    /// </summary>
    /// <param name="bundleRoot">现有 bundle 目录。</param>
    private static void EnsureManagedDirectory(string bundleRoot)
    {
        foreach (var path in Directory.EnumerateFileSystemEntries(bundleRoot, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(bundleRoot, path).Replace('\\', '/');
            if (Directory.Exists(path) || !ProjectModelContract.FILE_NAMES.Contains(relativePath, StringComparer.Ordinal))
            {
                throw new YokiFrameProtocolException(new YokiFrameError(
                    "ProjectModelDirectoryConflict",
                    "Project Model directory contains an unmanaged entry: " + relativePath,
                    "Move the unmanaged entry away before refreshing the Project Model.",
                    new[] { bundleRoot, path }));
            }
        }
    }

    /// <summary>
    /// 拒绝目录或文件重解析点，避免 Project Model 写入跟随链接逃逸项目根。
    /// </summary>
    /// <param name="path">待检查路径。</param>
    private static void EnsureNoReparsePoint(string path)
    {
        if (!File.Exists(path) && !Directory.Exists(path))
        {
            return;
        }

        var attributes = File.GetAttributes(path);
        if (attributes.HasFlag(FileAttributes.ReparsePoint))
        {
            throw new YokiFrameProtocolException(new YokiFrameError(
                "ProjectModelReparsePoint",
                "Project Model path must not be a symbolic link or reparse point: " + path,
                "Remove the reparse point and retry the Project Model refresh.",
                new[] { path }));
        }
    }

    /// <summary>
    /// 创建统一的 Project Model 协议异常并附带固定 evidence。
    /// </summary>
    /// <param name="code">稳定错误码。</param>
    /// <param name="message">错误说明。</param>
    /// <param name="extraEvidence">额外 staging、backup 或目标证据。</param>
    /// <returns>可由 CLI/Application 直接消费的协议异常。</returns>
    private YokiFrameProtocolException CreateStoreException(string code, string message, IEnumerable<string> extraEvidence)
    {
        var evidence = GetEvidencePaths().Concat(extraEvidence).Distinct(StringComparer.OrdinalIgnoreCase);
        return new YokiFrameProtocolException(new YokiFrameError(code, message, "Inspect the Project Model evidence and regenerate the bundle.", evidence));
    }
}
