using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using YokiFrame.Client;
using YokiFrame.Client.ProjectModel;
using YokiFrame.Protocol.ProjectModel;
using YokiFrame.Protocol.Results;
using YokiFrame.Tooling.Application.Models.ProjectModel;

namespace YokiFrame.Tooling.Application.ProjectModel;

/// <summary>
/// Project Model 的唯一应用层 owner；统一服务 CLI、Workbench、Installer 和 Editor invoker。
/// </summary>
public sealed class ProjectModelService
{
    private readonly IYokiFrameClient mClient;
    private readonly ProjectModelFileStore mStore;
    private readonly TimeProvider mTimeProvider;

    /// <summary>使用 Client 和系统时间创建 Project Model service。</summary>
    public ProjectModelService(IYokiFrameClient client)
        : this(client, TimeProvider.System)
    {
    }

    /// <summary>使用可控时间源创建 Project Model service，便于测试 generation/freshness。</summary>
    public ProjectModelService(IYokiFrameClient client, TimeProvider timeProvider)
    {
        mClient = client ?? throw new ArgumentNullException(nameof(client));
        mStore = new ProjectModelFileStore(client.Paths);
        mTimeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <summary>
    /// 只读取已提交 bundle，并可选扫描当前输入 hash 判断模型是否过期。
    /// </summary>
    /// <param name="checkFreshness">是否重新读取项目权威输入文件。</param>
    /// <returns>带状态和问题的模型结果。</returns>
    public ProjectModelResult Inspect(bool checkFreshness = true)
    {
        ProjectModelBundle bundle;
        try
        {
            bundle = mStore.Read();
        }
        catch (YokiFrameProtocolException exception)
        {
            return CreateStoreFailure(exception);
        }

        if (!checkFreshness)
        {
            return CreateResult("Ready", false, bundle, Array.Empty<ProjectModelIssue>(), mStore.GetEvidencePaths());
        }

        try
        {
            var snapshot = new ProjectModelSourceScanner(mClient).Scan(string.Empty);
            var inputHash = ProjectModelInputHash.Compute(snapshot);
            if (!string.Equals(inputHash, bundle.Manifest.InputHash, StringComparison.OrdinalIgnoreCase))
            {
                var issue = new ProjectModelIssue(
                    "ProjectModelStale",
                    "Warning",
                    "Project Model input facts changed since the last refresh.",
                    "Run yoki project refresh before planning an AI change.",
                    CreateInputEvidence(snapshot));
                return CreateResult("Stale", false, bundle, new[] { issue }, mStore.GetEvidencePaths().Concat(CreateInputEvidence(snapshot)).ToArray());
            }

            if (!MatchesGeneratedProjection(bundle, snapshot, inputHash))
            {
                var evidence = mStore.GetEvidencePaths().Concat(CreateInputEvidence(snapshot)).ToArray();
                var issue = new ProjectModelIssue(
                    "ProjectModelGeneratedContentMismatch",
                    "Error",
                    "Project Model documents do not match the deterministic projection of the current project facts.",
                    "Run yoki project refresh to replace the untrusted Project Model generation.",
                    mStore.GetEvidencePaths());
                return CreateResult("Blocked", false, bundle, new[] { issue }, evidence);
            }

            return CreateResult("Ready", false, bundle, Array.Empty<ProjectModelIssue>(), mStore.GetEvidencePaths());
        }
        catch (YokiFrameProtocolException exception)
        {
            var issue = new ProjectModelIssue(
                exception.Error.Code,
                "Warning",
                exception.Error.Message,
                exception.Error.Suggestion,
                exception.Error.EvidencePaths);
            return CreateResult("Partial", false, bundle, new[] { issue }, mStore.GetEvidencePaths().Concat(exception.Error.EvidencePaths).ToArray());
        }
    }

    /// <summary>
    /// 扫描当前项目并提交新的五文件 generation；输入未变化时保持 last-known-good bundle 不重写。
    /// </summary>
    /// <param name="packageRootHint">可选已解析包根路径。</param>
    /// <returns>刷新后的 Project Model 结果。</returns>
    public ProjectModelResult Refresh(string packageRootHint = "")
    {
        var snapshot = new ProjectModelSourceScanner(mClient).Scan(packageRootHint);
        var inputHash = ProjectModelInputHash.Compute(snapshot);
        var existing = TryReadExisting();
        if (existing != null
            && string.Equals(existing.Manifest.InputHash, inputHash, StringComparison.OrdinalIgnoreCase)
            && MatchesGeneratedProjection(existing, snapshot, inputHash))
        {
            var existingIssues = new List<ProjectModelIssue>();
            TryRefreshHarness(existing, existingIssues);
            return CreateResult(
                existingIssues.Count == 0 ? "Ready" : "Partial",
                false,
                existing,
                existingIssues,
                mStore.GetEvidencePaths().Append(mClient.Paths.GetHarnessCapabilitiesPath()).ToArray());
        }

        var now = mTimeProvider.GetUtcNow().ToUniversalTime();
        var generation = CreateGeneration(existing, now);
        var modelId = ComputeModelId(inputHash, generation);
        var bundle = ProjectModelDocumentFactory.CreateBundle(snapshot, generation, modelId, now.ToString("O"));
        mStore.Commit(bundle);
        var committed = mStore.Read();
        var issues = new List<ProjectModelIssue>();
        TryRefreshHarness(committed, issues);
        var evidence = mStore.GetEvidencePaths().Concat(CreateInputEvidence(snapshot)).ToArray();
        return CreateResult(issues.Count == 0 ? "Ready" : "Partial", true, committed, issues, evidence);
    }

    /// <summary>
    /// 重建当前 generation 的确定性投影，并确认五个 DTO 与磁盘 bundle 完全一致。
    /// </summary>
    /// <param name="bundle">已经通过 Client 文件完整性校验的 bundle。</param>
    /// <param name="snapshot">当前项目权威输入快照。</param>
    /// <param name="inputHash">当前输入快照的稳定 hash。</param>
    /// <returns>模型身份和五文件 canonical JSON 都可信时返回 true。</returns>
    private static bool MatchesGeneratedProjection(
        ProjectModelBundle bundle,
        ProjectModelSourceSnapshot snapshot,
        string inputHash)
    {
        var expectedModelId = ComputeModelId(inputHash, bundle.Manifest.ModelGeneration);
        if (!string.Equals(bundle.Manifest.ModelId, expectedModelId, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var expected = ProjectModelDocumentFactory.CreateBundle(
            snapshot,
            bundle.Manifest.ModelGeneration,
            bundle.Manifest.ModelId,
            bundle.Manifest.GeneratedAtUtc);
        expected.Manifest.Documents = CreateExpectedDocumentReferences(expected);
        return BundleJsonEquals(bundle, expected);
    }

    /// <summary>按 Project Model store 的固定顺序创建四个叶文档引用。</summary>
    /// <param name="bundle">生成器重建的期望 bundle。</param>
    /// <returns>可直接参与 manifest canonical 比较的文档引用。</returns>
    private static List<ProjectModelDocumentReference> CreateExpectedDocumentReferences(ProjectModelBundle bundle)
    {
        return new List<ProjectModelDocumentReference>
        {
            CreateExpectedDocumentReference(ProjectModelContract.ARCHITECTURE_FILE_NAME, ProjectModelContract.ARCHITECTURE_KIND, bundle.Architecture.ToJson()),
            CreateExpectedDocumentReference(ProjectModelContract.CAPABILITIES_FILE_NAME, ProjectModelContract.CAPABILITIES_KIND, bundle.Capabilities.ToJson()),
            CreateExpectedDocumentReference(ProjectModelContract.DEPENDENCIES_FILE_NAME, ProjectModelContract.DEPENDENCIES_KIND, bundle.Dependencies.ToJson()),
            CreateExpectedDocumentReference(ProjectModelContract.VALIDATION_PROFILE_FILE_NAME, ProjectModelContract.VALIDATION_PROFILE_KIND, bundle.ValidationProfile.ToJson())
        };
    }

    /// <summary>为单个期望叶文档创建路径、kind、schema 和内容 hash。</summary>
    /// <param name="path">固定叶文件名。</param>
    /// <param name="kind">固定文档 kind。</param>
    /// <param name="json">叶文档 canonical JSON。</param>
    /// <returns>与 Client store 持久化格式一致的引用。</returns>
    private static ProjectModelDocumentReference CreateExpectedDocumentReference(string path, string kind, string json)
    {
        return new ProjectModelDocumentReference
        {
            Path = path,
            Kind = kind,
            SchemaVersion = ProjectModelContract.SCHEMA_VERSION,
            ContentHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant()
        };
    }

    /// <summary>逐文档比较 canonical JSON，避免语义 DTO 被同步篡改 hash 后继续视为 Ready。</summary>
    /// <param name="actual">磁盘读取结果。</param>
    /// <param name="expected">从当前事实重建的期望结果。</param>
    /// <returns>五个文档逐字一致时返回 true。</returns>
    private static bool BundleJsonEquals(ProjectModelBundle actual, ProjectModelBundle expected)
    {
        return string.Equals(NormalizeManifestForTrust(actual.Manifest), NormalizeManifestForTrust(expected.Manifest), StringComparison.Ordinal)
            && string.Equals(actual.Architecture.ToJson(), expected.Architecture.ToJson(), StringComparison.Ordinal)
            && string.Equals(actual.Capabilities.ToJson(), expected.Capabilities.ToJson(), StringComparison.Ordinal)
            && string.Equals(actual.Dependencies.ToJson(), expected.Dependencies.ToJson(), StringComparison.Ordinal)
            && string.Equals(actual.ValidationProfile.ToJson(), expected.ValidationProfile.ToJson(), StringComparison.Ordinal);
    }

    /// <summary>比较 manifest 的生成身份和输入来源，leaf refs 由 Client store 单独校验。</summary>
    /// <param name="manifest">待比较的 manifest。</param>
    /// <returns>去除可由 Client 重算的 documents 引用后的 canonical JSON。</returns>
    private static string NormalizeManifestForTrust(ProjectModelManifest manifest)
    {
        var root = JsonNode.Parse(manifest.ToJson())!.AsObject();
        root.Remove("documents");
        return root.ToJsonString();
    }

    /// <summary>读取已有 bundle；缺失或损坏时返回 null，由 refresh 重新生成。</summary>
    private ProjectModelBundle? TryReadExisting()
    {
        try
        {
            return mStore.Read();
        }
        catch (YokiFrameProtocolException)
        {
            return null;
        }
    }

    /// <summary>为新 generation 生成单调字符串。</summary>
    private static string CreateGeneration(ProjectModelBundle? existing, DateTimeOffset now)
    {
        var candidate = now.UtcTicks;
        if (existing != null && long.TryParse(existing.Manifest.ModelGeneration, out var previous))
        {
            candidate = Math.Max(candidate, previous + 1L);
        }

        return candidate.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>把 input hash 和 generation 绑定为本次模型稳定 ID。</summary>
    private static string ComputeModelId(string inputHash, string generation)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(inputHash + "\0" + generation))).ToLowerInvariant();
    }

    /// <summary>刷新最小静态 harness projection；动态 registry/session/command 不写入。</summary>
    private void TryRefreshHarness(ProjectModelBundle bundle, ICollection<ProjectModelIssue> issues)
    {
        try
        {
            var root = CreateHarnessJson(bundle);
            var path = mClient.Paths.GetHarnessCapabilitiesPath();
            WriteHarnessAtomically(path, root);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            issues.Add(new ProjectModelIssue(
                "HarnessRefreshFailed",
                "Warning",
                "Static harness projection could not be refreshed after Project Model commit.",
                "Retry yoki project refresh; the committed Project Model remains the last-known-good source.",
                new[] { mClient.Paths.GetHarnessCapabilitiesPath() }));
        }
    }

    /// <summary>创建不含动态宿主状态的 harness bootstrap JSON。</summary>
    private JsonObject CreateHarnessJson(ProjectModelBundle bundle)
    {
        var package = bundle.Manifest.Package;
        var capabilities = bundle.Capabilities;
        var packageRoot = TryResolvePackageRoot(package.Root);
        var runtimeCache = new ProjectRuntimeCacheReader().Read(mClient.Paths.ProjectRoot, packageRoot);
        var runtimeRoot = ToProjectRelativePath(runtimeCache.RuntimeRoot);
        var cliPath = ToProjectRelativePath(runtimeCache.CliPath);
        return new JsonObject
        {
            ["schemaVersion"] = ProjectModelContract.SCHEMA_VERSION,
            ["generatedAtUtc"] = bundle.Manifest.GeneratedAtUtc,
            ["package"] = new JsonObject
            {
                ["name"] = package.Name,
                ["version"] = package.Version,
                ["packageRoot"] = package.Root
            },
            ["projectModel"] = new JsonObject
            {
                ["path"] = ".yokiframe/project/project-model.json",
                ["modelGeneration"] = bundle.Manifest.ModelGeneration,
                ["modelId"] = bundle.Manifest.ModelId
            },
            ["cli"] = new JsonObject
            {
                ["available"] = runtimeCache.IsCliAvailable,
                ["path"] = cliPath,
                ["runtimeRoot"] = runtimeRoot,
                ["runtimeIdentifier"] = runtimeCache.RuntimeIdentifier,
                ["version"] = "0.1.0-preview"
            },
            ["workbench"] = new JsonObject
            {
                ["kind"] = "Avalonia",
                ["available"] = runtimeCache.IsWorkbenchAvailable,
                ["runtimeRoot"] = runtimeRoot
            },
            ["protocol"] = new JsonObject
            {
                ["fileBridgeVersion"] = YokiFrameFileBridgeContract.PROTOCOL_VERSION,
                ["sharedMemoryTelemetryVersion"] = YokiFrameSharedMemoryTelemetryContract.PROTOCOL_VERSION,
                ["fastChannelVersion"] = YokiFrameFastChannelContract.PROTOCOL_VERSION
            },
            ["engines"] = new JsonObject { ["knownKinds"] = new JsonArray(capabilities.EngineKinds.Select(static kind => JsonValue.Create(kind)).ToArray()) },
            ["kits"] = new JsonObject
            {
                ["snapshots"] = new JsonArray(capabilities.Kits.Where(kit => kit.SnapshotNames.Count > 0).Select(kit => JsonValue.Create(kit.Kit)).ToArray()),
                ["commands"] = new JsonArray(capabilities.Kits.Where(kit => kit.CommandCatalogDeclared).Select(kit => JsonValue.Create(kit.Kit)).ToArray())
            }
        };
    }

    /// <summary>将模型中的项目相对包根解析为受项目边界约束的绝对路径；越界输入视为不可用 Runtime。</summary>
    private string TryResolvePackageRoot(string relativePackageRoot)
    {
        if (string.IsNullOrWhiteSpace(relativePackageRoot))
        {
            return string.Empty;
        }

        var projectRoot = Path.GetFullPath(mClient.Paths.ProjectRoot);
        var packageRoot = Path.GetFullPath(Path.Combine(
            projectRoot,
            relativePackageRoot.Replace('/', Path.DirectorySeparatorChar)));
        var relativePath = Path.GetRelativePath(projectRoot, packageRoot);
        return Path.IsPathRooted(relativePath)
            || relativePath == ".."
            || relativePath.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            ? string.Empty
            : packageRoot;
    }

    /// <summary>把 Runtime 缓存完整路径转换为项目相对正斜杠文本；缺失路径保持空以表达未 bootstrap。</summary>
    private string ToProjectRelativePath(string path)
    {
        return string.IsNullOrWhiteSpace(path)
            ? string.Empty
            : Path.GetRelativePath(mClient.Paths.ProjectRoot, path).Replace(Path.DirectorySeparatorChar, '/');
    }

    /// <summary>使用同目录临时文件、WriteThrough 和原子替换提交 harness bootstrap。</summary>
    private static void WriteHarnessAtomically(string path, JsonObject root)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tempPath = path + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            var json = root.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }) + System.Environment.NewLine;
            var bytes = new UTF8Encoding(false).GetBytes(json);
            using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(flushToDisk: true);
            }

            File.Move(tempPath, path, true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    /// <summary>从扫描快照提取输入证据，供 stale/refresh 结果复用。</summary>
    private static IReadOnlyList<string> CreateInputEvidence(ProjectModelSourceSnapshot snapshot)
    {
        return snapshot.SourceFiles.Select(static source => source.AbsolutePath).ToArray();
    }

    /// <summary>把 store 异常转换为可供 CLI/Workbench 消费的状态结果。</summary>
    private ProjectModelResult CreateStoreFailure(YokiFrameProtocolException exception)
    {
        var state = exception.Error.Code == "ProjectModelMissing" ? "Missing" : "Blocked";
        var issue = new ProjectModelIssue(
            exception.Error.Code,
            "Error",
            exception.Error.Message,
            exception.Error.Suggestion,
            exception.Error.EvidencePaths);
        return CreateResult(state, false, null, new[] { issue }, mStore.GetEvidencePaths().Concat(exception.Error.EvidencePaths).ToArray());
    }

    /// <summary>构造统一结果并去重证据路径。</summary>
    private static ProjectModelResult CreateResult(
        string state,
        bool changed,
        ProjectModelBundle? bundle,
        IReadOnlyList<ProjectModelIssue> issues,
        IReadOnlyList<string> evidencePaths)
    {
        return new ProjectModelResult(
            state,
            changed,
            bundle,
            issues,
            evidencePaths.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
    }
}
