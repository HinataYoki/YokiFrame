using System.Text.Json;
using System.Text.Json.Serialization;
using YokiFrame.Protocol.Common;

namespace YokiFrame.Protocol.ProjectModel;

/// <summary>
/// 表示 `.yokiframe/project/validation-profile.json` 中可删除后重建的有效验证策略投影。
/// </summary>
public sealed class ProjectValidationProfileDocument
{
    /// <summary>获取或设置 schema 版本。</summary>
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; set; } = ProjectModelContract.SCHEMA_VERSION;

    /// <summary>获取或设置文档 kind。</summary>
    [JsonPropertyName("kind")]
    public string Kind { get; set; } = ProjectModelContract.VALIDATION_PROFILE_KIND;

    /// <summary>获取或设置五文件原子代次标识。</summary>
    [JsonPropertyName("modelGeneration")]
    public string ModelGeneration { get; set; } = string.Empty;

    /// <summary>获取或设置模型稳定标识。</summary>
    [JsonPropertyName("modelId")]
    public string ModelId { get; set; } = string.Empty;

    /// <summary>获取或设置 UTC 生成时间。</summary>
    [JsonPropertyName("generatedAtUtc")]
    public string GeneratedAtUtc { get; set; } = string.Empty;

    /// <summary>获取或设置验证 profile 标识。</summary>
    [JsonPropertyName("profile")]
    public string Profile { get; set; } = string.Empty;

    /// <summary>获取或设置默认验证 gate。</summary>
    [JsonPropertyName("gates")]
    public List<ProjectValidationGate> Gates { get; set; } = new();

    /// <summary>获取或设置 evidence 必需类型和保留策略。</summary>
    [JsonPropertyName("evidencePolicy")]
    public ProjectEvidencePolicy EvidencePolicy { get; set; } = new();

    /// <summary>
    /// 从 JSON 文本反序列化验证策略文档。
    /// </summary>
    /// <param name="json">validation-profile.json 内容。</param>
    /// <returns>解析后的验证策略文档。</returns>
    public static ProjectValidationProfileDocument FromJson(string json)
    {
        return JsonSerializer.Deserialize(json, YokiFrameProtocolJsonContext.Default.ProjectValidationProfileDocument)
            ?? new ProjectValidationProfileDocument();
    }

    /// <summary>
    /// 将验证策略文档序列化为 compact JSON。
    /// </summary>
    /// <returns>compact JSON 文本。</returns>
    public string ToJson()
    {
        return JsonSerializer.Serialize(this, YokiFrameProtocolJsonContext.Default.ProjectValidationProfileDocument);
    }
}

/// <summary>
/// 描述一个默认验证 gate 的适用宿主、超时和证据要求。
/// </summary>
public sealed class ProjectValidationGate
{
    /// <summary>获取或设置 gate 稳定标识。</summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>获取或设置 gate 类型。</summary>
    [JsonPropertyName("kind")]
    public string Kind { get; set; } = string.Empty;

    /// <summary>获取或设置该 gate 是否为通过工作流所必需。</summary>
    [JsonPropertyName("required")]
    public bool Required { get; set; }

    /// <summary>获取或设置 gate 适用的宿主类型。</summary>
    [JsonPropertyName("engineKinds")]
    public List<string> EngineKinds { get; set; } = new();

    /// <summary>获取或设置最大等待毫秒数。</summary>
    [JsonPropertyName("timeoutMs")]
    public int TimeoutMs { get; set; }

    /// <summary>获取或设置失败后的最大重试次数。</summary>
    [JsonPropertyName("retryCount")]
    public int RetryCount { get; set; }

    /// <summary>获取或设置 gate 要求的 evidence 类型。</summary>
    [JsonPropertyName("evidence")]
    public List<string> Evidence { get; set; } = new();
}

/// <summary>
/// 描述工作流 evidence 的必需类型、保留时间和可靠存储要求。
/// </summary>
public sealed class ProjectEvidencePolicy
{
    /// <summary>获取或设置所有通过状态都必须具备的 evidence 类型。</summary>
    [JsonPropertyName("requiredTypes")]
    public List<string> RequiredTypes { get; set; } = new();

    /// <summary>获取或设置 evidence 默认保留天数。</summary>
    [JsonPropertyName("retentionDays")]
    public int RetentionDays { get; set; }

    /// <summary>获取或设置是否要求 FileBridge 或 artifact 文件持久证据。</summary>
    [JsonPropertyName("persistentEvidenceRequired")]
    public bool PersistentEvidenceRequired { get; set; }

    /// <summary>获取或设置单个 artifact 允许的最大字节数。</summary>
    [JsonPropertyName("maxArtifactBytes")]
    public long MaxArtifactBytes { get; set; }
}
