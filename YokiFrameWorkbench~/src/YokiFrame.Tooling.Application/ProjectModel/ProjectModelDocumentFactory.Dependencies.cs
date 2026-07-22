using YokiFrame.Protocol.ProjectModel;

namespace YokiFrame.Tooling.Application.ProjectModel;

/// <summary>
/// 生成依赖事实和可选 Integration 的保守状态，不把未观测的 DLL 当成已安装。
/// </summary>
internal static partial class ProjectModelDocumentFactory
{
    private static readonly (string Id, string Define, string[] Tokens)[] sOptionalIntegrations =
    {
        ("UniTask", "YOKIFRAME_UNITASK_SUPPORT", new[] { "unitask" }),
        ("YooAsset", "YOKIFRAME_YOOASSET_SUPPORT", new[] { "yooasset" }),
        ("DOTween", "YOKIFRAME_DOTWEEN_SUPPORT", new[] { "dotween" }),
        ("Luban", "YOKIFRAME_LUBAN_SUPPORT", new[] { "luban" }),
        ("Nino", "YOKIFRAME_NINO_SUPPORT", new[] { "nino" }),
        ("ZString", "YOKIFRAME_ZSTRING_SUPPORT", new[] { "zstring" }),
        ("InputSystem", "YOKIFRAME_INPUTSYSTEM_SUPPORT", new[] { "inputsystem", "input system" })
    };

    /// <summary>创建项目依赖文档。</summary>
    private static ProjectDependenciesDocument CreateDependencies(
        ProjectModelSourceSnapshot snapshot,
        string generation,
        string modelId,
        string generatedAtUtc)
    {
        var sourcePath = snapshot.Dependencies.FirstOrDefault()?.SourcePath ?? string.Empty;
        var sourceHash = snapshot.SourceFiles.FirstOrDefault(source => source.RelativePath == sourcePath)?.Sha256 ?? string.Empty;
        return new ProjectDependenciesDocument
        {
            ModelGeneration = generation,
            ModelId = modelId,
            GeneratedAtUtc = generatedAtUtc,
            SourcePath = sourcePath,
            SourceHash = sourceHash,
            Dependencies = snapshot.Dependencies
                .Select(CreateDependency)
                .OrderBy(static dependency => dependency.Kind, StringComparer.Ordinal)
                .ThenBy(static dependency => dependency.Id, StringComparer.Ordinal)
                .ToList(),
            OptionalIntegrations = CreateOptionalIntegrations(snapshot)
        };
    }

    /// <summary>把扫描依赖转换为 Project Model 的可验证依赖边。</summary>
    private static ProjectDependency CreateDependency(ProjectDependencyFact fact)
    {
        return new ProjectDependency
        {
            Id = fact.Name,
            Kind = fact.Kind,
            Version = fact.Reference,
            State = string.IsNullOrWhiteSpace(fact.Name) ? "Invalid" : "Available",
            From = "project",
            To = fact.Name,
            SourcePath = fact.SourcePath
        };
    }

    /// <summary>只在依赖文本中出现明确 token 时标记 Integration Detected。</summary>
    private static List<ProjectOptionalIntegration> CreateOptionalIntegrations(ProjectModelSourceSnapshot snapshot)
    {
        var values = snapshot.Dependencies
            .SelectMany(static dependency => new[] { dependency.Name, dependency.Reference })
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value.ToLowerInvariant())
            .ToArray();
        return sOptionalIntegrations
            .Select(integration => new ProjectOptionalIntegration
            {
                Id = integration.Id,
                Define = integration.Define,
                State = integration.Tokens.Any(token => values.Any(value => value.Contains(token, StringComparison.Ordinal)))
                    ? "Detected"
                    : "Unknown",
                Version = string.Empty,
                EvidencePaths = integration.Tokens.Any(token => values.Any(value => value.Contains(token, StringComparison.Ordinal)))
                    ? snapshot.Dependencies
                        .Where(dependency => integration.Tokens.Any(token => (dependency.Name + dependency.Reference).Contains(token, StringComparison.OrdinalIgnoreCase)))
                        .Select(static dependency => dependency.SourcePath)
                        .Distinct(StringComparer.Ordinal)
                        .ToList()
                    : new List<string>()
            })
            .ToList();
    }
}
