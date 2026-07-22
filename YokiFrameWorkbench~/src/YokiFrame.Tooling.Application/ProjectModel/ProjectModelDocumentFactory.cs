using System.Security.Cryptography;
using System.Text;
using YokiFrame.Protocol.ProjectModel;

namespace YokiFrame.Tooling.Application.ProjectModel;

/// <summary>
/// 把扫描到的项目事实投影为 Project Model 五文件的内存 bundle。
/// </summary>
internal static partial class ProjectModelDocumentFactory
{
    /// <summary>
    /// 创建带统一 generation、modelId 和生成时间的五文件 bundle。
    /// </summary>
    public static ProjectModelBundle CreateBundle(
        ProjectModelSourceSnapshot snapshot,
        string generation,
        string modelId,
        string generatedAtUtc)
    {
        var bundle = new ProjectModelBundle
        {
            Manifest = CreateManifest(snapshot, generation, modelId, generatedAtUtc),
            Architecture = CreateArchitecture(snapshot, generation, modelId, generatedAtUtc),
            Capabilities = CreateCapabilities(snapshot, generation, modelId, generatedAtUtc),
            Dependencies = CreateDependencies(snapshot, generation, modelId, generatedAtUtc),
            ValidationProfile = CreateValidationProfile(snapshot, generation, modelId, generatedAtUtc)
        };
        return bundle;
    }

    /// <summary>创建 project-model.json 的项目、包和输入来源摘要。</summary>
    private static ProjectModelManifest CreateManifest(
        ProjectModelSourceSnapshot snapshot,
        string generation,
        string modelId,
        string generatedAtUtc)
    {
        return new ProjectModelManifest
        {
            ModelGeneration = generation,
            ModelId = modelId,
            InputHash = ProjectModelInputHash.Compute(snapshot),
            GeneratedAtUtc = generatedAtUtc,
            Project = CreateProjectIdentity(snapshot),
            Package = new ProjectModelPackage
            {
                Name = snapshot.PackageName,
                Version = snapshot.PackageVersion,
                Root = snapshot.PackageRelativeRoot,
                Source = snapshot.PackageSource
            },
            Sources = snapshot.SourceFiles
                .Select(source => new ProjectModelSource
                {
                    Kind = ResolveSourceKind(source.RelativePath),
                    Path = source.RelativePath,
                    ContentHash = source.Sha256
                })
                .OrderBy(source => source.Path, StringComparer.Ordinal)
                .ToList()
        };
    }

    /// <summary>创建不持久化绝对路径的项目身份。</summary>
    private static ProjectModelProject CreateProjectIdentity(ProjectModelSourceSnapshot snapshot)
    {
        return new ProjectModelProject
        {
            Id = ComputeProjectId(snapshot.ProjectRoot, snapshot.ProjectKind),
            Name = new DirectoryInfo(snapshot.ProjectRoot).Name,
            Kind = snapshot.ProjectKind,
            EngineVersion = snapshot.EngineVersion,
            Root = ".",
            EngineKinds = new[] { snapshot.ProjectKind }.ToList(),
            Platforms = new List<string>()
        };
    }

    /// <summary>使用本机路径仅计算稳定项目 ID，不把路径原文写入模型。</summary>
    private static string ComputeProjectId(string projectRoot, string projectKind)
    {
        var input = projectKind + "\0" + Path.GetFullPath(projectRoot).TrimEnd(Path.DirectorySeparatorChar);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input))).ToLowerInvariant();
    }

    /// <summary>根据源文件相对路径归类模型输入证据。</summary>
    private static string ResolveSourceKind(string relativePath)
    {
        if (relativePath.EndsWith("package.json", StringComparison.OrdinalIgnoreCase)) return "package";
        if (relativePath.EndsWith("tool-manifest.json", StringComparison.OrdinalIgnoreCase)) return "tool-manifest";
        if (relativePath.EndsWith("manifest.json", StringComparison.OrdinalIgnoreCase)) return "project-dependencies";
        if (relativePath.EndsWith("ProjectVersion.txt", StringComparison.OrdinalIgnoreCase)) return "unity-project";
        if (relativePath.EndsWith("project.godot", StringComparison.OrdinalIgnoreCase)) return "godot-project";
        if (relativePath.EndsWith("capability.json", StringComparison.OrdinalIgnoreCase)) return "capability-descriptor";
        return "project-source";
    }
}
