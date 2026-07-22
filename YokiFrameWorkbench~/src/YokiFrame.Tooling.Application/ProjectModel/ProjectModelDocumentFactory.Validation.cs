using YokiFrame.Protocol.ProjectModel;

namespace YokiFrame.Tooling.Application.ProjectModel;

/// <summary>
/// 生成默认验证 gate 投影；它是可删除缓存，不替代未来项目规则的权威来源。
/// </summary>
internal static partial class ProjectModelDocumentFactory
{
    /// <summary>按 Unity/Godot 项目类型创建默认验证 profile。</summary>
    private static ProjectValidationProfileDocument CreateValidationProfile(
        ProjectModelSourceSnapshot snapshot,
        string generation,
        string modelId,
        string generatedAtUtc)
    {
        var compileKind = snapshot.ProjectKind == "Unity" ? "UnityCompile" : "GodotBuild";
        return new ProjectValidationProfileDocument
        {
            ModelGeneration = generation,
            ModelId = modelId,
            GeneratedAtUtc = generatedAtUtc,
            Profile = snapshot.ProjectKind.ToLowerInvariant() + "-default-v1",
            Gates = new List<ProjectValidationGate>
            {
                CreateGate("project-model-valid", "ProjectModel", true, new[] { snapshot.ProjectKind }, 30000, 0, new[] { "project-model" }),
                CreateGate("engine-reachable", "EngineReachable", true, new[] { snapshot.ProjectKind }, 15000, 1, new[] { "engine-registry", "heartbeat" }),
                CreateGate("compile", compileKind, true, new[] { snapshot.ProjectKind }, 300000, 1, new[] { "compile-log" }),
                CreateGate("console-errors", "ConsoleErrors", snapshot.ProjectKind == "Unity", new[] { "Unity" }, 30000, 0, new[] { "console-log" }),
                CreateGate("runtime-reachable", "RuntimeReachable", false, new[] { snapshot.ProjectKind }, 30000, 1, new[] { "runtime-status" }),
                CreateGate("evidence-required", "Evidence", true, new[] { snapshot.ProjectKind }, 30000, 0, new[] { "terminal-response", "artifact" })
            },
            EvidencePolicy = new ProjectEvidencePolicy
            {
                RequiredTypes = new List<string> { "project-model", "terminal-response", "compile-log" },
                RetentionDays = 30,
                PersistentEvidenceRequired = true,
                MaxArtifactBytes = 50L * 1024L * 1024L
            }
        };
    }

    /// <summary>创建单个默认 gate。</summary>
    private static ProjectValidationGate CreateGate(
        string id,
        string kind,
        bool required,
        IReadOnlyList<string> engineKinds,
        int timeoutMs,
        int retryCount,
        IReadOnlyList<string> evidence)
    {
        return new ProjectValidationGate
        {
            Id = id,
            Kind = kind,
            Required = required,
            EngineKinds = engineKinds.ToList(),
            TimeoutMs = timeoutMs,
            RetryCount = retryCount,
            Evidence = evidence.ToList()
        };
    }
}
