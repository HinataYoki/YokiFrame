using System.Text.Json;
using System.Text.Json.Serialization;
using YokiFrame.Protocol.Common;

namespace YokiFrame.Protocol.ProjectModel;

/// <summary>
/// 表示 `.yokiframe/project/capabilities.json` 中由包声明的静态 Kit 能力。
/// </summary>
public sealed class ProjectCapabilitiesDocument
{
    /// <summary>获取或设置 schema 版本。</summary>
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; set; } = ProjectModelContract.SCHEMA_VERSION;

    /// <summary>获取或设置文档 kind。</summary>
    [JsonPropertyName("kind")]
    public string Kind { get; set; } = ProjectModelContract.CAPABILITIES_KIND;

    /// <summary>获取或设置五文件原子代次标识。</summary>
    [JsonPropertyName("modelGeneration")]
    public string ModelGeneration { get; set; } = string.Empty;

    /// <summary>获取或设置模型稳定标识。</summary>
    [JsonPropertyName("modelId")]
    public string ModelId { get; set; } = string.Empty;

    /// <summary>获取或设置 UTC 生成时间。</summary>
    [JsonPropertyName("generatedAtUtc")]
    public string GeneratedAtUtc { get; set; } = string.Empty;

    /// <summary>获取或设置参与能力投影的包内 descriptor 路径。</summary>
    [JsonPropertyName("sourcePaths")]
    public List<string> SourcePaths { get; set; } = new();

    /// <summary>获取或设置本次静态投影覆盖的宿主类型。</summary>
    [JsonPropertyName("engineKinds")]
    public List<string> EngineKinds { get; set; } = new();

    /// <summary>获取或设置已安装且有机器可读声明的 Kit。</summary>
    [JsonPropertyName("kits")]
    public List<ProjectCapabilityKit> Kits { get; set; } = new();

    /// <summary>
    /// 从 JSON 文本反序列化静态能力文档。
    /// </summary>
    /// <param name="json">capabilities.json 内容。</param>
    /// <returns>解析后的能力文档。</returns>
    public static ProjectCapabilitiesDocument FromJson(string json)
    {
        return JsonSerializer.Deserialize(json, YokiFrameProtocolJsonContext.Default.ProjectCapabilitiesDocument)
            ?? new ProjectCapabilitiesDocument();
    }

    /// <summary>
    /// 将静态能力文档序列化为 compact JSON。
    /// </summary>
    /// <returns>compact JSON 文本。</returns>
    public string ToJson()
    {
        return JsonSerializer.Serialize(this, YokiFrameProtocolJsonContext.Default.ProjectCapabilitiesDocument);
    }
}

/// <summary>
/// 表示包内单个 `capability.json` descriptor 的独立 schema。
/// </summary>
public sealed class ProjectCapabilityDescriptor
{
    /// <summary>获取或设置 descriptor schema 版本。</summary>
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; set; } = ProjectModelContract.SCHEMA_VERSION;

    /// <summary>获取或设置 descriptor kind。</summary>
    [JsonPropertyName("kind")]
    public string Kind { get; set; } = ProjectModelContract.CAPABILITY_DESCRIPTOR_KIND;

    /// <summary>获取或设置该 descriptor 声明的 Kit。</summary>
    [JsonPropertyName("kit")]
    public ProjectCapabilityKit Kit { get; set; } = new();

    /// <summary>
    /// 从 JSON 文本反序列化 package-side capability descriptor。
    /// </summary>
    /// <param name="json">descriptor JSON 内容。</param>
    /// <returns>解析后的 descriptor。</returns>
    public static ProjectCapabilityDescriptor FromJson(string json)
    {
        return JsonSerializer.Deserialize(json, YokiFrameProtocolJsonContext.Default.ProjectCapabilityDescriptor)
            ?? new ProjectCapabilityDescriptor();
    }

    /// <summary>
    /// 将 package-side capability descriptor 序列化为 compact JSON。
    /// </summary>
    /// <returns>compact JSON 文本。</returns>
    public string ToJson()
    {
        return JsonSerializer.Serialize(this, YokiFrameProtocolJsonContext.Default.ProjectCapabilityDescriptor);
    }
}

/// <summary>
/// 描述一个 Kit 的静态状态、数据面、命令面和验证配方。
/// </summary>
public sealed class ProjectCapabilityKit
{
    /// <summary>获取或设置 Kit 标识。</summary>
    [JsonPropertyName("kit")]
    public string Kit { get; set; } = string.Empty;

    /// <summary>获取或设置 Available、Partial、Unavailable 或 Disabled 状态。</summary>
    [JsonPropertyName("state")]
    public string State { get; set; } = string.Empty;

    /// <summary>获取或设置 Core、Tool 或 Harness 角色。</summary>
    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    /// <summary>获取或设置该 Kit 声明的 snapshot 名称。</summary>
    [JsonPropertyName("snapshotNames")]
    public List<string> SnapshotNames { get; set; } = new();

    /// <summary>获取或设置该 Kit 声明的 telemetry 名称。</summary>
    [JsonPropertyName("telemetryNames")]
    public List<string> TelemetryNames { get; set; } = new();

    /// <summary>获取或设置宿主是否通过 System/list_commands 声明该 Kit 命令。</summary>
    [JsonPropertyName("commandCatalogDeclared")]
    public bool CommandCatalogDeclared { get; set; }

    /// <summary>获取或设置静态命令描述。</summary>
    [JsonPropertyName("commands")]
    public List<ProjectCapabilityCommand> Commands { get; set; } = new();

    /// <summary>获取或设置命令引用的验证配方。</summary>
    [JsonPropertyName("verifyRecipes")]
    public List<ProjectCapabilityVerifyRecipe> VerifyRecipes { get; set; } = new();

    /// <summary>获取或设置该声明对应的包内实现来源路径。</summary>
    [JsonPropertyName("sourcePath")]
    public string SourcePath { get; set; } = string.Empty;

    /// <summary>获取或设置实现来源的 SHA-256 内容哈希。</summary>
    [JsonPropertyName("sourceHash")]
    public string SourceHash { get; set; } = string.Empty;
}

/// <summary>
/// 描述一个可调用 action 的风险、宿主范围、前置条件和验证方式。
/// </summary>
public sealed class ProjectCapabilityCommand
{
    /// <summary>获取或设置 action 标识。</summary>
    [JsonPropertyName("action")]
    public string Action { get; set; } = string.Empty;

    /// <summary>获取或设置 ReadOnly、Maintenance、UserAction 或 Dangerous 类型。</summary>
    [JsonPropertyName("kind")]
    public string Kind { get; set; } = string.Empty;

    /// <summary>获取或设置真实实现该 action 的宿主类型。</summary>
    [JsonPropertyName("engineKinds")]
    public List<string> EngineKinds { get; set; } = new();

    /// <summary>获取或设置可观察副作用。</summary>
    [JsonPropertyName("sideEffects")]
    public List<string> SideEffects { get; set; } = new();

    /// <summary>获取或设置执行前必须满足的条件。</summary>
    [JsonPropertyName("preconditions")]
    public List<string> Preconditions { get; set; } = new();

    /// <summary>获取或设置验证配方标识。</summary>
    [JsonPropertyName("verifyRecipe")]
    public string VerifyRecipe { get; set; } = string.Empty;
}

/// <summary>
/// 描述 capability command 完成后应执行的 gate 和证据要求。
/// </summary>
public sealed class ProjectCapabilityVerifyRecipe
{
    /// <summary>获取或设置配方稳定标识。</summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>获取或设置配方职责说明。</summary>
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>获取或设置必须通过的 gate 标识。</summary>
    [JsonPropertyName("gates")]
    public List<string> Gates { get; set; } = new();

    /// <summary>获取或设置必须保留的 evidence 类型。</summary>
    [JsonPropertyName("evidence")]
    public List<string> Evidence { get; set; } = new();
}
