using System.Text.Json;
using System.Text.Json.Serialization;
using YokiFrame.Protocol.Common;

namespace YokiFrame.Protocol.ProjectModel;

/// <summary>
/// 表示 `.yokiframe/project/project-model.json`，并通过文档引用提交同一代 Project Model。
/// </summary>
public sealed class ProjectModelManifest
{
    /// <summary>获取或设置 schema 版本。</summary>
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; set; } = ProjectModelContract.SCHEMA_VERSION;

    /// <summary>获取或设置文档 kind。</summary>
    [JsonPropertyName("kind")]
    public string Kind { get; set; } = ProjectModelContract.PROJECT_MODEL_KIND;

    /// <summary>获取或设置五文件原子代次标识。</summary>
    [JsonPropertyName("modelGeneration")]
    public string ModelGeneration { get; set; } = string.Empty;

    /// <summary>获取或设置模型稳定标识。</summary>
    [JsonPropertyName("modelId")]
    public string ModelId { get; set; } = string.Empty;

    /// <summary>获取或设置生成输入的内容哈希。</summary>
    [JsonPropertyName("inputHash")]
    public string InputHash { get; set; } = string.Empty;

    /// <summary>获取或设置 UTC 生成时间。</summary>
    [JsonPropertyName("generatedAtUtc")]
    public string GeneratedAtUtc { get; set; } = string.Empty;

    /// <summary>获取或设置项目身份与宿主摘要。</summary>
    [JsonPropertyName("project")]
    public ProjectModelProject Project { get; set; } = new();

    /// <summary>获取或设置 YokiFrame 包摘要。</summary>
    [JsonPropertyName("package")]
    public ProjectModelPackage Package { get; set; } = new();

    /// <summary>获取或设置四个叶文档的路径与内容哈希。</summary>
    [JsonPropertyName("documents")]
    public List<ProjectModelDocumentReference> Documents { get; set; } = new();

    /// <summary>获取或设置生成本模型所使用的事实来源。</summary>
    [JsonPropertyName("sources")]
    public List<ProjectModelSource> Sources { get; set; } = new();

    /// <summary>
    /// 从 JSON 文本反序列化 Project Model manifest。
    /// </summary>
    /// <param name="json">manifest JSON 内容。</param>
    /// <returns>解析后的 manifest。</returns>
    public static ProjectModelManifest FromJson(string json)
    {
        return JsonSerializer.Deserialize(json, YokiFrameProtocolJsonContext.Default.ProjectModelManifest)
            ?? new ProjectModelManifest();
    }

    /// <summary>
    /// 将 manifest 序列化为 compact JSON。
    /// </summary>
    /// <returns>compact JSON 文本。</returns>
    public string ToJson()
    {
        return JsonSerializer.Serialize(this, YokiFrameProtocolJsonContext.Default.ProjectModelManifest);
    }
}

/// <summary>
/// 描述 Project Model 绑定的项目身份、宿主类型和目标平台。
/// </summary>
public sealed class ProjectModelProject
{
    /// <summary>获取或设置项目稳定标识。</summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>获取或设置项目显示名称。</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>获取或设置 Unity、Godot 或其它受支持项目类型。</summary>
    [JsonPropertyName("kind")]
    public string Kind { get; set; } = string.Empty;

    /// <summary>获取或设置相对项目根；默认使用 `.`，禁止写入开发机绝对路径。</summary>
    [JsonPropertyName("root")]
    public string Root { get; set; } = ".";

    /// <summary>获取或设置项目声明的宿主类型。</summary>
    [JsonPropertyName("engineKinds")]
    public List<string> EngineKinds { get; set; } = new();

    /// <summary>获取或设置项目当前宿主版本，例如 Unity Editor 或 Godot 版本。</summary>
    [JsonPropertyName("engineVersion")]
    public string EngineVersion { get; set; } = string.Empty;

    /// <summary>获取或设置目标平台标识。</summary>
    [JsonPropertyName("platforms")]
    public List<string> Platforms { get; set; } = new();
}

/// <summary>
/// 描述当前项目使用的 YokiFrame 包身份和来源。
/// </summary>
public sealed class ProjectModelPackage
{
    /// <summary>获取或设置包名。</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>获取或设置包版本。</summary>
    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    /// <summary>获取或设置项目相对包根。</summary>
    [JsonPropertyName("root")]
    public string Root { get; set; } = string.Empty;

    /// <summary>获取或设置 embedded、git 或 local 等安装来源。</summary>
    [JsonPropertyName("source")]
    public string Source { get; set; } = string.Empty;
}

/// <summary>
/// 描述 manifest 引用的单个叶文档及其完整性信息。
/// </summary>
public sealed class ProjectModelDocumentReference
{
    /// <summary>获取或设置叶文档 kind。</summary>
    [JsonPropertyName("kind")]
    public string Kind { get; set; } = string.Empty;

    /// <summary>获取或设置 `.yokiframe/project` 内的相对路径。</summary>
    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    /// <summary>获取或设置叶文档 schema 版本。</summary>
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; set; } = ProjectModelContract.SCHEMA_VERSION;

    /// <summary>获取或设置叶文档 canonical UTF-8 内容哈希。</summary>
    [JsonPropertyName("contentHash")]
    public string ContentHash { get; set; } = string.Empty;
}

/// <summary>
/// 描述参与模型生成的一个输入事实及其哈希。
/// </summary>
public sealed class ProjectModelSource
{
    /// <summary>获取或设置来源类型。</summary>
    [JsonPropertyName("kind")]
    public string Kind { get; set; } = string.Empty;

    /// <summary>获取或设置项目或包内相对来源路径。</summary>
    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    /// <summary>获取或设置来源内容哈希。</summary>
    [JsonPropertyName("contentHash")]
    public string ContentHash { get; set; } = string.Empty;
}
