using YokiFrame.Protocol.ProjectModel;

namespace YokiFrame.Protocol.Tests.ProjectModel;

/// <summary>
/// 覆盖 Project Model 五文件 DTO 与 aggregate bundle 的 source-generated JSON roundtrip。
/// </summary>
public sealed class ProjectModelRoundTripTests
{
    /// <summary>
    /// 验证 bundle roundtrip 保留同代五文件、宿主版本和深层能力字段。
    /// </summary>
    [Fact]
    public void BundleRoundTripPreservesAllProjectModelDocuments()
    {
        var bundle = CreateBundle();

        var restored = ProjectModelBundle.FromJson(bundle.ToJson());

        Assert.Equal("generation-1", restored.Manifest.ModelGeneration);
        Assert.Equal("6000.7.0a1", restored.Manifest.Project.EngineVersion);
        Assert.Equal("generation-1", restored.Architecture.ModelGeneration);
        Assert.Equal("generation-1", restored.Capabilities.ModelGeneration);
        Assert.Equal("generation-1", restored.Dependencies.ModelGeneration);
        Assert.Equal("generation-1", restored.ValidationProfile.ModelGeneration);
        Assert.Equal("YokiFrame", Assert.Single(restored.Architecture.Boundaries).Id);
        Assert.Equal("ping", Assert.Single(Assert.Single(restored.Capabilities.Kits).Commands).Action);
        Assert.Equal("Unity", Assert.Single(restored.Dependencies.Dependencies).Id);
        Assert.Equal("unity-compile", Assert.Single(restored.ValidationProfile.Gates).Id);
    }

    /// <summary>
    /// 验证每个持久文档的公开序列化入口均可独立 roundtrip，并保持固定 kind。
    /// </summary>
    [Fact]
    public void PersistentDocumentsRoundTripThroughTheirSourceGeneratedEntryPoints()
    {
        var bundle = CreateBundle();

        var manifest = ProjectModelManifest.FromJson(bundle.Manifest.ToJson());
        var architecture = ProjectArchitectureDocument.FromJson(bundle.Architecture.ToJson());
        var capabilities = ProjectCapabilitiesDocument.FromJson(bundle.Capabilities.ToJson());
        var dependencies = ProjectDependenciesDocument.FromJson(bundle.Dependencies.ToJson());
        var validation = ProjectValidationProfileDocument.FromJson(bundle.ValidationProfile.ToJson());

        Assert.Equal(ProjectModelContract.PROJECT_MODEL_KIND, manifest.Kind);
        Assert.Equal(ProjectModelContract.ARCHITECTURE_KIND, architecture.Kind);
        Assert.Equal(ProjectModelContract.CAPABILITIES_KIND, capabilities.Kind);
        Assert.Equal(ProjectModelContract.DEPENDENCIES_KIND, dependencies.Kind);
        Assert.Equal(ProjectModelContract.VALIDATION_PROFILE_KIND, validation.Kind);
        Assert.Contains("\"engineVersion\":\"6000.7.0a1\"", bundle.Manifest.ToJson(), StringComparison.Ordinal);
    }

    /// <summary>
    /// 创建覆盖 manifest、架构、能力、依赖和验证策略的最小同代 bundle。
    /// </summary>
    /// <returns>可用于 roundtrip 的完整 bundle。</returns>
    private static ProjectModelBundle CreateBundle()
    {
        const string generation = "generation-1";
        const string modelId = "model-1";
        const string generatedAtUtc = "2026-07-12T12:00:00.0000000Z";
        return new ProjectModelBundle
        {
            Manifest = CreateManifest(generation, modelId, generatedAtUtc),
            Architecture = CreateArchitecture(generation, modelId, generatedAtUtc),
            Capabilities = CreateCapabilities(generation, modelId, generatedAtUtc),
            Dependencies = CreateDependencies(generation, modelId, generatedAtUtc),
            ValidationProfile = CreateValidationProfile(generation, modelId, generatedAtUtc)
        };
    }

    /// <summary>
    /// 创建包含宿主版本、包身份和叶文档引用的 manifest。
    /// </summary>
    /// <param name="generation">模型代次。</param>
    /// <param name="modelId">模型标识。</param>
    /// <param name="generatedAtUtc">生成时间。</param>
    /// <returns>Project Model manifest。</returns>
    private static ProjectModelManifest CreateManifest(string generation, string modelId, string generatedAtUtc)
    {
        return new ProjectModelManifest
        {
            ModelGeneration = generation,
            ModelId = modelId,
            InputHash = "input-hash",
            GeneratedAtUtc = generatedAtUtc,
            Project = new ProjectModelProject
            {
                Id = "project-1",
                Name = "YokiFrame",
                Kind = "Unity",
                Root = ".",
                EngineKinds = new List<string> { "Unity" },
                EngineVersion = "6000.7.0a1",
                Platforms = new List<string> { "WindowsEditor" }
            },
            Package = new ProjectModelPackage
            {
                Name = "com.hinatayoki.yokiframe",
                Version = "2.0.0-preview",
                Root = "Assets/YokiFrame",
                Source = "embedded"
            },
            Documents = new List<ProjectModelDocumentReference>
            {
                new()
                {
                    Kind = ProjectModelContract.ARCHITECTURE_KIND,
                    Path = ProjectModelContract.ARCHITECTURE_FILE_NAME,
                    ContentHash = "architecture-hash"
                }
            },
            Sources = new List<ProjectModelSource>
            {
                new() { Kind = "package", Path = "Assets/YokiFrame/package.json", ContentHash = "package-hash" }
            }
        };
    }

    /// <summary>
    /// 创建包含 Core 边界、路径 owner 和宿主隔离不变量的架构文档。
    /// </summary>
    /// <param name="generation">模型代次。</param>
    /// <param name="modelId">模型标识。</param>
    /// <param name="generatedAtUtc">生成时间。</param>
    /// <returns>架构文档。</returns>
    private static ProjectArchitectureDocument CreateArchitecture(
        string generation,
        string modelId,
        string generatedAtUtc)
    {
        return new ProjectArchitectureDocument
        {
            ModelGeneration = generation,
            ModelId = modelId,
            GeneratedAtUtc = generatedAtUtc,
            Profile = "yokiframe-v1",
            Boundaries = new List<ProjectArchitectureBoundary>
            {
                new() { Id = "YokiFrame", Role = "Core", Root = "Assets/YokiFrame/Core/Runtime" }
            },
            Ownership = new List<ProjectPathOwnership>
            {
                new() { Path = "Assets/YokiFrame/**", Owner = "Framework", Access = "ReadOnly" }
            },
            Invariants = new List<ProjectArchitectureInvariant>
            {
                new() { Code = "CoreHostIndependent", Severity = "Error", Satisfied = true }
            }
        };
    }

    /// <summary>
    /// 创建包含 engine SDK 和可选 Integration 状态的依赖文档。
    /// </summary>
    /// <param name="generation">模型代次。</param>
    /// <param name="modelId">模型标识。</param>
    /// <param name="generatedAtUtc">生成时间。</param>
    /// <returns>依赖文档。</returns>
    private static ProjectDependenciesDocument CreateDependencies(
        string generation,
        string modelId,
        string generatedAtUtc)
    {
        return new ProjectDependenciesDocument
        {
            ModelGeneration = generation,
            ModelId = modelId,
            GeneratedAtUtc = generatedAtUtc,
            SourcePath = "Assets/YokiFrame/package.json",
            SourceHash = "package-hash",
            Dependencies = new List<ProjectDependency>
            {
                new() { Id = "Unity", Kind = "EngineSdk", Version = "6000.7.0a1", State = "Available" }
            },
            OptionalIntegrations = new List<ProjectOptionalIntegration>
            {
                new() { Id = "UniTask", State = "Missing", Define = "YOKIFRAME_UNITASK_SUPPORT" }
            }
        };
    }

    /// <summary>
    /// 创建包含 Unity 编译 gate 和持久证据要求的验证策略。
    /// </summary>
    /// <param name="generation">模型代次。</param>
    /// <param name="modelId">模型标识。</param>
    /// <param name="generatedAtUtc">生成时间。</param>
    /// <returns>验证策略文档。</returns>
    private static ProjectValidationProfileDocument CreateValidationProfile(
        string generation,
        string modelId,
        string generatedAtUtc)
    {
        return new ProjectValidationProfileDocument
        {
            ModelGeneration = generation,
            ModelId = modelId,
            GeneratedAtUtc = generatedAtUtc,
            Profile = "unity-default",
            Gates = new List<ProjectValidationGate>
            {
                new() { Id = "unity-compile", Kind = "Compile", Required = true, TimeoutMs = 120000 }
            },
            EvidencePolicy = new ProjectEvidencePolicy
            {
                RequiredTypes = new List<string> { "compile-result" },
                RetentionDays = 14,
                PersistentEvidenceRequired = true
            }
        };
    }

    /// <summary>
    /// 创建带一条只读 System 命令的静态能力文档。
    /// </summary>
    /// <param name="generation">模型代次。</param>
    /// <param name="modelId">模型标识。</param>
    /// <param name="generatedAtUtc">生成时间。</param>
    /// <returns>能力文档。</returns>
    private static ProjectCapabilitiesDocument CreateCapabilities(
        string generation,
        string modelId,
        string generatedAtUtc)
    {
        return new ProjectCapabilitiesDocument
        {
            ModelGeneration = generation,
            ModelId = modelId,
            GeneratedAtUtc = generatedAtUtc,
            SourcePaths = new List<string> { "Core/Editor/CommandBridge/Capabilities/System/capability.json" },
            EngineKinds = new List<string> { "Unity", "Godot" },
            Kits = new List<ProjectCapabilityKit>
            {
                new()
                {
                    Kit = "System",
                    State = "Available",
                    Role = "Core",
                    SnapshotNames = new List<string> { "state" },
                    TelemetryNames = new List<string> { "state" },
                    CommandCatalogDeclared = true,
                    Commands = new List<ProjectCapabilityCommand>
                    {
                        new()
                        {
                            Action = "ping",
                            Kind = "ReadOnly",
                            EngineKinds = new List<string> { "Unity", "Godot" },
                            Preconditions = new List<string> { "engine.reachable" },
                            VerifyRecipe = "system-ping"
                        }
                    },
                    VerifyRecipes = new List<ProjectCapabilityVerifyRecipe>
                    {
                        new() { Id = "system-ping", Gates = new List<string> { "runtime-response-success" } }
                    }
                }
            }
        };
    }
}
