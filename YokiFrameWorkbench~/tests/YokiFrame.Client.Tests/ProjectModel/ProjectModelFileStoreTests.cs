using System.Security.Cryptography;
using System.Text;
using YokiFrame.Client.FileBridge;
using YokiFrame.Client.ProjectModel;
using YokiFrame.Protocol.ProjectModel;
using YokiFrame.Protocol.Results;

namespace YokiFrame.Client.Tests.ProjectModel;

/// <summary>
/// 覆盖 Project Model 五文件持久化、完整性校验和替换提交。
/// </summary>
public sealed class ProjectModelFileStoreTests : IDisposable
{
    private static readonly UTF8Encoding sUtf8 = new(encoderShouldEmitUTF8Identifier: false);
    private readonly string mProjectRoot = Path.Combine(
        Path.GetTempPath(),
        "yokiframe-project-model-store-tests",
        Guid.NewGuid().ToString("N"));

    /// <summary>
    /// 验证 Project Model 所有路径均固定在 `.yokiframe/project`，锁文件位于目录外。
    /// </summary>
    [Fact]
    public void PathsExposeOnlyFixedProjectModelLocations()
    {
        var paths = new YokiFramePaths(mProjectRoot);
        var yokiFrameRoot = Path.Combine(Path.GetFullPath(mProjectRoot), ".yokiframe");
        var projectModelRoot = Path.Combine(yokiFrameRoot, "project");

        Assert.Equal(projectModelRoot, paths.ProjectModelRoot);
        Assert.Equal(Path.Combine(yokiFrameRoot, "project-model.lock"), paths.ProjectModelLockPath);
        Assert.Equal(Path.Combine(projectModelRoot, "project-model.json"), paths.ProjectModelManifestPath);
        Assert.Equal(Path.Combine(projectModelRoot, "architecture.json"), paths.ProjectArchitecturePath);
        Assert.Equal(Path.Combine(projectModelRoot, "capabilities.json"), paths.ProjectCapabilitiesPath);
        Assert.Equal(Path.Combine(projectModelRoot, "dependencies.json"), paths.ProjectDependenciesPath);
        Assert.Equal(Path.Combine(projectModelRoot, "validation-profile.json"), paths.ProjectValidationProfilePath);
    }

    /// <summary>
    /// 验证尚未生成 Project Model 时返回稳定的 Missing 错误，而不是底层文件异常。
    /// </summary>
    [Fact]
    public void ReadMissingBundleReturnsProjectModelMissing()
    {
        var paths = new YokiFramePaths(mProjectRoot);
        var store = new ProjectModelFileStore(paths);

        var exception = Assert.Throws<YokiFrameProtocolException>(() => store.Read());

        Assert.Equal("ProjectModelMissing", exception.Error.Code);
        Assert.Contains(paths.ProjectModelManifestPath, exception.Error.EvidencePaths);
    }

    /// <summary>
    /// 验证提交会写出五个固定文件、生成真实内容哈希，并可完整读回。
    /// </summary>
    [Fact]
    public void CommitRoundTripPersistsFiveFilesAndHashes()
    {
        var paths = new YokiFramePaths(mProjectRoot);
        var store = new ProjectModelFileStore(paths);

        store.Commit(CreateBundle("generation-1", "model-1"));
        var restored = store.Read();

        Assert.Equal("generation-1", restored.Manifest.ModelGeneration);
        Assert.Equal("Core", Assert.Single(restored.Architecture.Boundaries).Role);
        Assert.Equal("System", Assert.Single(restored.Capabilities.Kits).Kit);
        Assert.Equal("Unity", Assert.Single(restored.Dependencies.Dependencies).Id);
        Assert.Equal("unity-compile", Assert.Single(restored.ValidationProfile.Gates).Id);
        Assert.Equal(4, restored.Manifest.Documents.Count);
        Assert.Equal(ProjectModelContract.FILE_NAMES.Order(), GetStoredFileNames(paths));

        foreach (var reference in restored.Manifest.Documents)
        {
            var bytes = File.ReadAllBytes(Path.Combine(paths.ProjectModelRoot, reference.Path));
            Assert.Equal(ComputeHash(bytes), reference.ContentHash);
        }
    }

    /// <summary>
    /// 验证叶文件在提交后被修改时，读取会在 JSON 解析前报告哈希不匹配。
    /// </summary>
    [Fact]
    public void ReadRejectsTamperedLeafHash()
    {
        var paths = new YokiFramePaths(mProjectRoot);
        var store = new ProjectModelFileStore(paths);
        store.Commit(CreateBundle("generation-1", "model-1"));

        File.AppendAllText(paths.ProjectArchitecturePath, " ", sUtf8);

        var exception = Assert.Throws<YokiFrameProtocolException>(() => store.Read());
        Assert.Equal("ProjectModelHashMismatch", exception.Error.Code);
    }

    /// <summary>
    /// 验证即使攻击者同步更新叶文件哈希，跨代文档仍会被一致性校验拒绝。
    /// </summary>
    [Fact]
    public void ReadRejectsMixedGenerationWithMatchingHash()
    {
        var paths = new YokiFramePaths(mProjectRoot);
        var store = new ProjectModelFileStore(paths);
        store.Commit(CreateBundle("generation-1", "model-1"));
        RewriteArchitectureGeneration(paths, "generation-2");

        var exception = Assert.Throws<YokiFrameProtocolException>(() => store.Read());

        Assert.Equal("ProjectModelGenerationMismatch", exception.Error.Code);
        Assert.Contains(paths.ProjectArchitecturePath, exception.Error.EvidencePaths);
    }

    /// <summary>
    /// 验证第二次提交完整替换旧 bundle，并清理同级 staging 与 backup 目录。
    /// </summary>
    [Fact]
    public void SecondCommitReplacesBundleWithoutTransactionResidue()
    {
        var paths = new YokiFramePaths(mProjectRoot);
        var store = new ProjectModelFileStore(paths);
        store.Commit(CreateBundle("generation-1", "model-1"));

        store.Commit(CreateBundle("generation-2", "model-2"));
        var restored = store.Read();

        Assert.Equal("generation-2", restored.Manifest.ModelGeneration);
        Assert.Equal("model-2", restored.Manifest.ModelId);
        Assert.DoesNotContain(
            Directory.EnumerateDirectories(paths.YokiFrameRoot),
            static path => Path.GetFileName(path).StartsWith(".project-staging-", StringComparison.Ordinal)
                || Path.GetFileName(path).StartsWith(".project-backup-", StringComparison.Ordinal));
    }

    /// <summary>
    /// 删除当前测试的唯一临时项目，避免锁文件和模型文件污染后续用例。
    /// </summary>
    public void Dispose()
    {
        if (Directory.Exists(mProjectRoot))
        {
            Directory.Delete(mProjectRoot, recursive: true);
        }
    }

    /// <summary>
    /// 创建包含五个真实协议文档的最小同代 bundle。
    /// </summary>
    /// <param name="generation">五文件共享代次。</param>
    /// <param name="modelId">五文件共享模型标识。</param>
    /// <returns>可提交的完整 Project Model。</returns>
    private static ProjectModelBundle CreateBundle(string generation, string modelId)
    {
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
    /// 创建 Project Model 根 manifest；文档引用由 Store 按真实字节覆盖。
    /// </summary>
    /// <param name="generation">模型代次。</param>
    /// <param name="modelId">模型标识。</param>
    /// <param name="generatedAtUtc">生成时间。</param>
    /// <returns>待提交 manifest。</returns>
    private static ProjectModelManifest CreateManifest(string generation, string modelId, string generatedAtUtc)
    {
        return new ProjectModelManifest
        {
            ModelGeneration = generation,
            ModelId = modelId,
            InputHash = "input-hash-" + generation,
            GeneratedAtUtc = generatedAtUtc,
            Project = new ProjectModelProject
            {
                Id = "project-1",
                Name = "YokiFrame",
                Kind = "Unity",
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
            }
        };
    }

    /// <summary>
    /// 创建带 Core 编译边界的架构文档。
    /// </summary>
    /// <param name="generation">模型代次。</param>
    /// <param name="modelId">模型标识。</param>
    /// <param name="generatedAtUtc">生成时间。</param>
    /// <returns>架构文档。</returns>
    private static ProjectArchitectureDocument CreateArchitecture(string generation, string modelId, string generatedAtUtc)
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
            }
        };
    }

    /// <summary>
    /// 创建带 System Kit 的静态能力文档。
    /// </summary>
    /// <param name="generation">模型代次。</param>
    /// <param name="modelId">模型标识。</param>
    /// <param name="generatedAtUtc">生成时间。</param>
    /// <returns>能力文档。</returns>
    private static ProjectCapabilitiesDocument CreateCapabilities(string generation, string modelId, string generatedAtUtc)
    {
        return new ProjectCapabilitiesDocument
        {
            ModelGeneration = generation,
            ModelId = modelId,
            GeneratedAtUtc = generatedAtUtc,
            EngineKinds = new List<string> { "Unity" },
            Kits = new List<ProjectCapabilityKit>
            {
                new() { Kit = "System", State = "Available", Role = "Core" }
            }
        };
    }

    /// <summary>
    /// 创建带 Unity SDK 的依赖文档。
    /// </summary>
    /// <param name="generation">模型代次。</param>
    /// <param name="modelId">模型标识。</param>
    /// <param name="generatedAtUtc">生成时间。</param>
    /// <returns>依赖文档。</returns>
    private static ProjectDependenciesDocument CreateDependencies(string generation, string modelId, string generatedAtUtc)
    {
        return new ProjectDependenciesDocument
        {
            ModelGeneration = generation,
            ModelId = modelId,
            GeneratedAtUtc = generatedAtUtc,
            Dependencies = new List<ProjectDependency>
            {
                new() { Id = "Unity", Kind = "EngineSdk", Version = "6000.7.0a1", State = "Available" }
            }
        };
    }

    /// <summary>
    /// 创建带 Unity 编译 gate 的验证策略文档。
    /// </summary>
    /// <param name="generation">模型代次。</param>
    /// <param name="modelId">模型标识。</param>
    /// <param name="generatedAtUtc">生成时间。</param>
    /// <returns>验证策略文档。</returns>
    private static ProjectValidationProfileDocument CreateValidationProfile(string generation, string modelId, string generatedAtUtc)
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
            }
        };
    }

    /// <summary>
    /// 把 architecture 改成另一代，并同步 manifest 哈希以单独验证代次校验。
    /// </summary>
    /// <param name="paths">固定模型路径。</param>
    /// <param name="generation">要写入 architecture 的新代次。</param>
    private static void RewriteArchitectureGeneration(YokiFramePaths paths, string generation)
    {
        var architecture = ProjectArchitectureDocument.FromJson(File.ReadAllText(paths.ProjectArchitecturePath, sUtf8));
        architecture.ModelGeneration = generation;
        var architectureBytes = sUtf8.GetBytes(architecture.ToJson());
        File.WriteAllBytes(paths.ProjectArchitecturePath, architectureBytes);

        var manifest = ProjectModelManifest.FromJson(File.ReadAllText(paths.ProjectModelManifestPath, sUtf8));
        var reference = manifest.Documents.Single(static item => item.Path == ProjectModelContract.ARCHITECTURE_FILE_NAME);
        reference.ContentHash = ComputeHash(architectureBytes);
        File.WriteAllBytes(paths.ProjectModelManifestPath, sUtf8.GetBytes(manifest.ToJson()));
    }

    /// <summary>
    /// 返回正式 Project Model 目录中的文件名排序结果。
    /// </summary>
    /// <param name="paths">固定模型路径。</param>
    /// <returns>按 ordinal 排序的文件名。</returns>
    private static IReadOnlyList<string> GetStoredFileNames(YokiFramePaths paths)
    {
        return Directory.EnumerateFiles(paths.ProjectModelRoot)
            .Select(static path => Path.GetFileName(path))
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>
    /// 计算与 Store 一致的小写 SHA-256 十六进制摘要。
    /// </summary>
    /// <param name="bytes">完整文件字节。</param>
    /// <returns>小写摘要。</returns>
    private static string ComputeHash(byte[] bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }
}
