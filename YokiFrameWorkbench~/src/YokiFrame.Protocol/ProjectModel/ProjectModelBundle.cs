using System.Text.Json;
using System.Text.Json.Serialization;
using YokiFrame.Protocol.Common;

namespace YokiFrame.Protocol.ProjectModel;

/// <summary>
/// 聚合一次经过 generation 与 hash 校验的 Project Model 五文件，供 Client 和 Application 传递一致快照。
/// </summary>
public sealed class ProjectModelBundle
{
    /// <summary>获取或设置提交四个叶文档的 manifest。</summary>
    [JsonPropertyName("manifest")]
    public ProjectModelManifest Manifest { get; set; } = new();

    /// <summary>获取或设置架构文档。</summary>
    [JsonPropertyName("architecture")]
    public ProjectArchitectureDocument Architecture { get; set; } = new();

    /// <summary>获取或设置静态能力文档。</summary>
    [JsonPropertyName("capabilities")]
    public ProjectCapabilitiesDocument Capabilities { get; set; } = new();

    /// <summary>获取或设置依赖文档。</summary>
    [JsonPropertyName("dependencies")]
    public ProjectDependenciesDocument Dependencies { get; set; } = new();

    /// <summary>获取或设置验证策略文档。</summary>
    [JsonPropertyName("validationProfile")]
    public ProjectValidationProfileDocument ValidationProfile { get; set; } = new();

    /// <summary>
    /// 获取或设置验证文档的简短语义别名；该别名不重复写入 aggregate JSON。等价于 <see cref="ValidationProfile"/>。
    /// </summary>
    [JsonIgnore]
    [Obsolete("Use ValidationProfile instead.")]
    public ProjectValidationProfileDocument Validation
    {
        get => ValidationProfile;
        set => ValidationProfile = value ?? new ProjectValidationProfileDocument();
    }

    /// <summary>
    /// 从 JSON 文本反序列化聚合 Project Model；该格式仅用于内存边界和测试，不替代五个持久文件。
    /// </summary>
    /// <param name="json">bundle JSON 内容。</param>
    /// <returns>解析后的 Project Model bundle。</returns>
    public static ProjectModelBundle FromJson(string json)
    {
        return JsonSerializer.Deserialize(json, YokiFrameProtocolJsonContext.Default.ProjectModelBundle)
            ?? new ProjectModelBundle();
    }

    /// <summary>
    /// 将聚合 Project Model 序列化为 compact JSON；持久化仍应由 Client 分别提交五个文件。
    /// </summary>
    /// <returns>compact JSON 文本。</returns>
    public string ToJson()
    {
        return JsonSerializer.Serialize(this, YokiFrameProtocolJsonContext.Default.ProjectModelBundle);
    }
}
