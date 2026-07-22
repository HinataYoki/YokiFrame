using System.Text.Json;
using System.Text.Json.Serialization;
using YokiFrame.Protocol.Common;

namespace YokiFrame.Protocol.ProjectModel;

/// <summary>
/// 表示 `.yokiframe/project/dependencies.json` 中的必需依赖和可选 Integration 事实。
/// </summary>
public sealed class ProjectDependenciesDocument
{
    /// <summary>获取或设置 schema 版本。</summary>
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; set; } = ProjectModelContract.SCHEMA_VERSION;

    /// <summary>获取或设置文档 kind。</summary>
    [JsonPropertyName("kind")]
    public string Kind { get; set; } = ProjectModelContract.DEPENDENCIES_KIND;

    /// <summary>获取或设置五文件原子代次标识。</summary>
    [JsonPropertyName("modelGeneration")]
    public string ModelGeneration { get; set; } = string.Empty;

    /// <summary>获取或设置模型稳定标识。</summary>
    [JsonPropertyName("modelId")]
    public string ModelId { get; set; } = string.Empty;

    /// <summary>获取或设置 UTC 生成时间。</summary>
    [JsonPropertyName("generatedAtUtc")]
    public string GeneratedAtUtc { get; set; } = string.Empty;

    /// <summary>获取或设置主要依赖事实来源路径。</summary>
    [JsonPropertyName("sourcePath")]
    public string SourcePath { get; set; } = string.Empty;

    /// <summary>获取或设置主要来源内容哈希。</summary>
    [JsonPropertyName("sourceHash")]
    public string SourceHash { get; set; } = string.Empty;

    /// <summary>获取或设置 engine SDK、包和程序集依赖。</summary>
    [JsonPropertyName("dependencies")]
    public List<ProjectDependency> Dependencies { get; set; } = new();

    /// <summary>获取或设置 UniTask、YooAsset 等可选 Integration 状态。</summary>
    [JsonPropertyName("optionalIntegrations")]
    public List<ProjectOptionalIntegration> OptionalIntegrations { get; set; } = new();

    /// <summary>
    /// 从 JSON 文本反序列化依赖文档。
    /// </summary>
    /// <param name="json">dependencies.json 内容。</param>
    /// <returns>解析后的依赖文档。</returns>
    public static ProjectDependenciesDocument FromJson(string json)
    {
        return JsonSerializer.Deserialize(json, YokiFrameProtocolJsonContext.Default.ProjectDependenciesDocument)
            ?? new ProjectDependenciesDocument();
    }

    /// <summary>
    /// 将依赖文档序列化为 compact JSON。
    /// </summary>
    /// <returns>compact JSON 文本。</returns>
    public string ToJson()
    {
        return JsonSerializer.Serialize(this, YokiFrameProtocolJsonContext.Default.ProjectDependenciesDocument);
    }
}

/// <summary>
/// 描述一个必需 SDK、包或模块依赖。
/// </summary>
public sealed class ProjectDependency
{
    /// <summary>获取或设置依赖稳定标识。</summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>获取或设置 EngineSdk、Package、Assembly 或 Project 类型。</summary>
    [JsonPropertyName("kind")]
    public string Kind { get; set; } = string.Empty;

    /// <summary>获取或设置检测到或要求的版本。</summary>
    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    /// <summary>获取或设置 Available、Missing、Invalid 或 Unknown 状态。</summary>
    [JsonPropertyName("state")]
    public string State { get; set; } = string.Empty;

    /// <summary>获取或设置依赖来源模块。</summary>
    [JsonPropertyName("from")]
    public string From { get; set; } = string.Empty;

    /// <summary>获取或设置依赖目标模块。</summary>
    [JsonPropertyName("to")]
    public string To { get; set; } = string.Empty;

    /// <summary>获取或设置支持该事实的项目相对路径。</summary>
    [JsonPropertyName("sourcePath")]
    public string SourcePath { get; set; } = string.Empty;
}

/// <summary>
/// 描述一个可选 Integration 的检测状态和编译宏。
/// </summary>
public sealed class ProjectOptionalIntegration
{
    /// <summary>获取或设置 Integration 标识。</summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>获取或设置 Available、Missing、Invalid 或 Unknown 状态。</summary>
    [JsonPropertyName("state")]
    public string State { get; set; } = string.Empty;

    /// <summary>获取或设置由宿主维护的编译宏。</summary>
    [JsonPropertyName("define")]
    public string Define { get; set; } = string.Empty;

    /// <summary>获取或设置检测到的版本。</summary>
    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    /// <summary>获取或设置支持判断的项目相对证据路径。</summary>
    [JsonPropertyName("evidencePaths")]
    public List<string> EvidencePaths { get; set; } = new();
}
