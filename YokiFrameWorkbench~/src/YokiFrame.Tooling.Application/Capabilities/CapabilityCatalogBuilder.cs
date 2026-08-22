using System.Globalization;
using System.Text.Json.Nodes;
using YokiFrame.Client.FileBridge.Diagnostics;
using YokiFrame.Protocol.FileBridge;
using YokiFrame.Protocol.Validation;
using YokiFrame.Tooling.Application.Models;
using YokiFrame.Tooling.Application.Models.Capabilities;

namespace YokiFrame.Tooling.Application.Capabilities;

/// <summary>
/// 在单次读取过程中构建能力目录，确保静态声明与动态观测不会被无来源地合并。
/// </summary>
internal sealed partial class CapabilityCatalogBuilder
{
    private const int SCHEMA_VERSION = 1;
    private const string READ_ONLY_KIND = "ReadOnly";
    private const string MAINTENANCE_KIND = "Maintenance";
    private const string USER_ACTION_KIND = "UserAction";
    private const string DANGEROUS_KIND = "Dangerous";
    private readonly DateTimeOffset mGeneratedAtUtc;
    private readonly string mHarnessPath;
    private readonly List<CapabilityCatalogEngineBuilder> mEngines = new();
    private readonly Dictionary<string, KitBuilder> mKits = new(StringComparer.Ordinal);
    private readonly List<CapabilityCatalogIssue> mIssues = new();
    private readonly List<CapabilityCatalogSource> mSources = new();
    private readonly HashSet<string> mEvidencePaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> mDeclaredSnapshotKits = new(StringComparer.Ordinal);
    private readonly HashSet<string> mDeclaredCommandKits = new(StringComparer.Ordinal);
    private readonly HashSet<string> mDeclaredEngineKinds = new(StringComparer.Ordinal);
    private string mModelState = "Missing";
    private string mPackageName = string.Empty;
    private string mPackageVersion = string.Empty;
    private string mPackageRoot = string.Empty;
    private int mFileBridgeVersion;
    private int mTelemetryVersion;
    private int mFastChannelVersion;
    private int mErrorCount;
    private bool mHasDrift;

    /// <summary>
    /// 创建能力目录构建器。
    /// </summary>
    /// <param name="projectRoot">项目根路径。</param>
    /// <param name="harnessPath">静态 harness 路径。</param>
    /// <param name="generatedAtUtc">本次聚合时间。</param>
    public CapabilityCatalogBuilder(string projectRoot, string harnessPath, DateTimeOffset generatedAtUtc)
    {
        ProjectRoot = projectRoot;
        mHarnessPath = harnessPath;
        mGeneratedAtUtc = generatedAtUtc.ToUniversalTime();
    }

    /// <summary>获取当前项目根路径，供上层记录无效 registry 证据。</summary>
    public string ProjectRoot { get; }

    /// <summary>
    /// 读取静态 harness 摘要；原始 JSON 保留在目录中，动态命令不会回写该文件。
    /// </summary>
    /// <param name="node">harness JSON 节点。</param>
    public void ApplyHarness(JsonNode node)
    {
        if (node is not JsonObject root)
        {
            mSources.Add(new CapabilityCatalogSource("harness", mHarnessPath, "Invalid"));
            AddEvidence(mHarnessPath);
            AddIssue("HarnessInvalid", "Warning", "harness", "Harness capabilities root must be a JSON object.", "Regenerate the harness capabilities file.", new[] { mHarnessPath });
            return;
        }

        AddEvidence(mHarnessPath);
        var invalidFields = new List<string>();
        if (ReadInt32(root, "schemaVersion") != SCHEMA_VERSION)
        {
            invalidFields.Add("schemaVersion");
        }

        var package = ReadObject(root, "package");
        var harnessPackageName = ReadString(package, "name");
        var harnessPackageVersion = ReadString(package, "version");
        var harnessPackageRoot = ReadString(package, "packageRoot");
        if (string.IsNullOrWhiteSpace(harnessPackageRoot))
        {
            harnessPackageRoot = ReadString(package, "root");
        }

        if (string.IsNullOrWhiteSpace(harnessPackageName))
        {
            invalidFields.Add("package.name");
        }

        if (string.IsNullOrWhiteSpace(harnessPackageVersion))
        {
            invalidFields.Add("package.version");
        }

        if (string.IsNullOrWhiteSpace(harnessPackageRoot))
        {
            invalidFields.Add("package.packageRoot");
        }

        if (!mProjectModelBundleAvailable)
        {
            mPackageName = harnessPackageName;
            mPackageVersion = harnessPackageVersion;
            mPackageRoot = harnessPackageRoot;
        }

        var protocol = ReadObject(root, "protocol");
        mFileBridgeVersion = ReadInt32(protocol, "fileBridgeVersion");
        mTelemetryVersion = ReadInt32(protocol, "sharedMemoryTelemetryVersion");
        mFastChannelVersion = ReadInt32(protocol, "fastChannelVersion");
        if (mFileBridgeVersion <= 0)
        {
            invalidFields.Add("protocol.fileBridgeVersion");
        }

        if (mTelemetryVersion <= 0)
        {
            invalidFields.Add("protocol.sharedMemoryTelemetryVersion");
        }

        if (mFastChannelVersion <= 0)
        {
            invalidFields.Add("protocol.fastChannelVersion");
        }

        var generatedAtUtc = ReadString(root, "generatedAtUtc");
        if (string.IsNullOrWhiteSpace(generatedAtUtc)
            || !DateTimeOffset.TryParse(generatedAtUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _))
        {
            invalidFields.Add("generatedAtUtc");
        }

        if (invalidFields.Count > 0)
        {
            AddIssue(
                "HarnessInvalid",
                "Warning",
                "harness",
                "Harness capabilities is missing or has invalid required fields: " + string.Join(", ", invalidFields) + ".",
                "Regenerate the harness capabilities file from the owning Installer/Editor process.",
                new[] { mHarnessPath });
        }

        mSources.Add(new CapabilityCatalogSource(
            "harness",
            mHarnessPath,
            invalidFields.Count == 0 ? "Available" : "Invalid"));
        foreach (var kind in ReadStringArray(ReadObject(root, "engines"), "knownKinds"))
        {
            mDeclaredEngineKinds.Add(kind);
        }

        foreach (var kit in ReadStringArray(ReadObject(root, "kits"), "snapshots"))
        {
            mDeclaredSnapshotKits.Add(kit);
            GetKit(kit).ApplyHarnessSnapshotDeclaration();
        }

        foreach (var kit in ReadStringArray(ReadObject(root, "kits"), "commands"))
        {
            mDeclaredCommandKits.Add(kit);
            GetKit(kit).ApplyHarnessCommandDeclaration();
        }
    }

    /// <summary>
    /// 加入一个 registry/heartbeat engine，并计算当前宿主身份状态。
    /// </summary>
    /// <param name="entry">engine registry 条目。</param>
    /// <param name="heartbeat">对应 heartbeat。</param>
    /// <returns>可被实时命令目录更新的内部 engine 节点。</returns>
    public CapabilityCatalogEngineBuilder AddEngine(EngineRegistryEntry entry, HeartbeatInfo? heartbeat)
    {
        var safeEngineId = SafeIdValidator.EnsureSafeId(entry.EngineId, nameof(entry.EngineId));
        var heartbeatPath = Path.Combine(
            ProjectRoot,
            ".yokiframe",
            "engines",
            safeEngineId,
            YokiFrameFileBridgeLayout.STATUS_DIRECTORY,
            YokiFrameFileBridgeLayout.HEARTBEAT_FILE_NAME);
        mSources.Add(new CapabilityCatalogSource(
            "engine-registry",
            Path.Combine(ProjectRoot, ".yokiframe", "engines", safeEngineId, YokiFrameFileBridgeLayout.ENGINE_REGISTRY_FILE_NAME),
            "Available",
            entry.EngineId));
        AddEvidence(Path.Combine(ProjectRoot, ".yokiframe", "engines", safeEngineId, YokiFrameFileBridgeLayout.ENGINE_REGISTRY_FILE_NAME));

        var identityState = ResolveIdentityState(entry, heartbeat);
        var staleThreshold = Engines.EngineSelectionService.HeartbeatStaleThreshold;
        var isStale = heartbeat != null && heartbeat.IsStale(mGeneratedAtUtc, staleThreshold);
        if (heartbeat == null)
        {
            AddIssue("HeartbeatMissing", "Warning", entry.EngineId, "Engine heartbeat is missing.", "Start the engine adapter and refresh the catalog.", new[] { heartbeatPath });
            mSources.Add(new CapabilityCatalogSource("heartbeat", heartbeatPath, "Missing", entry.EngineId));
        }
        else
        {
            AddEvidence(heartbeat.Path);
            var heartbeatState = isStale ? "Stale" : "Available";
            mSources.Add(new CapabilityCatalogSource("heartbeat", heartbeat.Path, heartbeatState, entry.EngineId));
            if (isStale)
            {
                AddIssue("HeartbeatStale", "Warning", entry.EngineId, "Engine heartbeat is stale.", "Refresh the engine session before trusting runtime capabilities.", new[] { heartbeat.Path });
            }
        }

        if (identityState == "Mismatch")
        {
            AddIssue("EngineIdentityMismatch", "Warning", entry.EngineId, "Engine registry and heartbeat session/generation do not match.", "Wait for the engine session to settle, then refresh the catalog.", new[] { heartbeatPath });
        }
        else if (identityState == "Invalid")
        {
            AddIssue("EngineIdentityInvalid", "Warning", entry.EngineId, "Engine registry or heartbeat is missing a valid sessionId or generation.", "Refresh the engine adapter before trusting runtime capabilities.", new[] { heartbeatPath });
        }

        var engine = new CapabilityCatalogEngineBuilder(
            entry,
            heartbeat,
            identityState,
            heartbeat != null && !isStale && identityState == "Match");
        mEngines.Add(engine);
        return engine;
    }

    /// <summary>
    /// 添加能力目录问题并累计严格模式所需的证据。
    /// </summary>
    /// <param name="code">稳定问题码。</param>
    /// <param name="severity">Warning 或 Error。</param>
    /// <param name="scope">问题范围。</param>
    /// <param name="message">问题说明。</param>
    /// <param name="suggestion">恢复建议。</param>
    /// <param name="evidencePaths">证据路径。</param>
    public void AddIssue(
        string code,
        string severity,
        string scope,
        string message,
        string suggestion,
        IReadOnlyList<string> evidencePaths)
    {
        mIssues.Add(new CapabilityCatalogIssue(code, severity, scope, message, suggestion, evidencePaths));
        if (string.Equals(severity, "Error", StringComparison.OrdinalIgnoreCase))
        {
            mErrorCount++;
        }

        foreach (var path in evidencePaths)
        {
            AddEvidence(path);
        }
    }

    /// <summary>
    /// 完成能力目录并按 schema 稳定排序所有列表。
    /// </summary>
    /// <returns>能力目录结果。</returns>
    public CapabilityCatalogResult Build()
    {
        var engines = mEngines
            .OrderBy(engine => engine.EngineId, StringComparer.Ordinal)
            .Select(engine => engine.ToModel())
            .ToArray();
        var kits = mKits.Values
            .OrderBy(kit => kit.Kit, StringComparer.Ordinal)
            .Select(kit => kit.ToModel())
            .ToArray();
        var state = ResolveState(engines.Length);
        var declaredEngineKinds = mProjectModelTrusted && mProjectModelBundleAvailable
            ? mProjectModelDeclaredEngineKinds
            : mDeclaredEngineKinds;
        var declaredKitIds = mProjectModelTrusted && mProjectModelBundleAvailable
            ? mProjectModelDeclaredKitIds
            : mDeclaredSnapshotKits.Concat(mDeclaredCommandKits).ToHashSet(StringComparer.Ordinal);
        var project = new CapabilityCatalogProject(
            mModelState,
            ProjectRoot,
            mPackageName,
            mPackageVersion,
            mPackageRoot,
            mFileBridgeVersion,
            mTelemetryVersion,
            mFastChannelVersion,
            declaredEngineKinds.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            declaredKitIds.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            mProjectModelId,
            mProjectModelGeneration,
            mProjectModelPath,
            mProjectModelInputHash);
        var sortedIssues = mIssues
            .OrderBy(issue => issue.Severity, StringComparer.Ordinal)
            .ThenBy(issue => issue.Scope, StringComparer.Ordinal)
            .ThenBy(issue => issue.Code, StringComparer.Ordinal)
            .ToArray();
        var sortedSources = mSources
            .OrderBy(source => source.Kind, StringComparer.Ordinal)
            .ThenBy(source => source.EngineId, StringComparer.Ordinal)
            .ThenBy(source => source.Path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var catalog = new CapabilityCatalog(
            SCHEMA_VERSION,
            mGeneratedAtUtc.ToString("O"),
            state,
            project,
            engines,
            kits,
            sortedIssues,
            sortedSources);
        return new CapabilityCatalogResult(
            state,
            catalog,
            mEvidencePaths.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray());
    }

    /// <summary>
    /// 计算目录总体状态；动态漂移优先于普通 Partial 警告。
    /// </summary>
    /// <param name="engineCount">有效 engine 数量。</param>
    /// <returns>Ready、Partial、Drifted 或 Blocked。</returns>
    private string ResolveState(int engineCount)
    {
        if (mErrorCount > 0 || mProjectModelBlocked)
        {
            return "Blocked";
        }

        if (mHasDrift || mProjectModelDrifted)
        {
            return "Drifted";
        }

        if (string.Equals(mModelState, "Missing", StringComparison.Ordinal)
            || string.Equals(mModelState, "Partial", StringComparison.Ordinal))
        {
            return "Partial";
        }

        if (engineCount == 0)
        {
            return "Blocked";
        }

        return mIssues.Count > 0 ? "Partial" : "Ready";
    }

}
