using System.Text.Json;
using System.Text.Json.Serialization;
using YokiFrame.Protocol.Common;

namespace YokiFrame.Protocol.ProjectModel;

/// <summary>
/// 表示 `.yokiframe/project/architecture.json` 中的程序集边界、目录所有权和架构不变量。
/// </summary>
public sealed class ProjectArchitectureDocument
{
    /// <summary>获取或设置 schema 版本。</summary>
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; set; } = ProjectModelContract.SCHEMA_VERSION;

    /// <summary>获取或设置文档 kind。</summary>
    [JsonPropertyName("kind")]
    public string Kind { get; set; } = ProjectModelContract.ARCHITECTURE_KIND;

    /// <summary>获取或设置五文件原子代次标识。</summary>
    [JsonPropertyName("modelGeneration")]
    public string ModelGeneration { get; set; } = string.Empty;

    /// <summary>获取或设置模型稳定标识。</summary>
    [JsonPropertyName("modelId")]
    public string ModelId { get; set; } = string.Empty;

    /// <summary>获取或设置 UTC 生成时间。</summary>
    [JsonPropertyName("generatedAtUtc")]
    public string GeneratedAtUtc { get; set; } = string.Empty;

    /// <summary>获取或设置架构 profile 标识。</summary>
    [JsonPropertyName("profile")]
    public string Profile { get; set; } = string.Empty;

    /// <summary>获取或设置 Core、Tool、Adapter 和工具链编译边界。</summary>
    [JsonPropertyName("boundaries")]
    public List<ProjectArchitectureBoundary> Boundaries { get; set; } = new();

    /// <summary>获取或设置项目路径的 owner 与写入策略。</summary>
    [JsonPropertyName("ownership")]
    public List<ProjectPathOwnership> Ownership { get; set; } = new();

    /// <summary>获取或设置生成时评估的架构不变量。</summary>
    [JsonPropertyName("invariants")]
    public List<ProjectArchitectureInvariant> Invariants { get; set; } = new();

    /// <summary>
    /// 从 JSON 文本反序列化架构文档。
    /// </summary>
    /// <param name="json">architecture.json 内容。</param>
    /// <returns>解析后的架构文档。</returns>
    public static ProjectArchitectureDocument FromJson(string json)
    {
        return JsonSerializer.Deserialize(json, YokiFrameProtocolJsonContext.Default.ProjectArchitectureDocument)
            ?? new ProjectArchitectureDocument();
    }

    /// <summary>
    /// 将架构文档序列化为 compact JSON。
    /// </summary>
    /// <returns>compact JSON 文本。</returns>
    public string ToJson()
    {
        return JsonSerializer.Serialize(this, YokiFrameProtocolJsonContext.Default.ProjectArchitectureDocument);
    }
}

/// <summary>
/// 描述一个源码模块的角色、根路径和编译边界。
/// </summary>
public sealed class ProjectArchitectureBoundary
{
    /// <summary>获取或设置边界稳定标识。</summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>获取或设置 Core、Tool、Adapter 或 Tooling 角色。</summary>
    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    /// <summary>获取或设置项目相对根路径。</summary>
    [JsonPropertyName("root")]
    public string Root { get; set; } = string.Empty;

    /// <summary>获取或设置 asmdef、csproj 或 package 等编译边界。</summary>
    [JsonPropertyName("compilationBoundary")]
    public string CompilationBoundary { get; set; } = string.Empty;

    /// <summary>获取或设置可选宿主类型；跨引擎边界保持为空。</summary>
    [JsonPropertyName("engineKind")]
    public string EngineKind { get; set; } = string.Empty;

    /// <summary>获取或设置允许依赖的边界标识。</summary>
    [JsonPropertyName("dependencies")]
    public List<string> Dependencies { get; set; } = new();
}

/// <summary>
/// 描述项目路径的 owner 和 AI 可执行写入策略。
/// </summary>
public sealed class ProjectPathOwnership
{
    /// <summary>获取或设置项目相对路径或受控 glob。</summary>
    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    /// <summary>获取或设置 Framework、Installer、Engine 或 User owner。</summary>
    [JsonPropertyName("owner")]
    public string Owner { get; set; } = string.Empty;

    /// <summary>获取或设置 ReadOnly、Managed 或 PlanRequired 访问策略。</summary>
    [JsonPropertyName("access")]
    public string Access { get; set; } = string.Empty;
}

/// <summary>
/// 描述一个可验证的架构约束及其生成时证据。
/// </summary>
public sealed class ProjectArchitectureInvariant
{
    /// <summary>获取或设置稳定约束码。</summary>
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    /// <summary>获取或设置约束说明。</summary>
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>获取或设置 Warning 或 Error 严重度。</summary>
    [JsonPropertyName("severity")]
    public string Severity { get; set; } = string.Empty;

    /// <summary>获取或设置生成时是否满足该约束。</summary>
    [JsonPropertyName("satisfied")]
    public bool Satisfied { get; set; }

    /// <summary>获取或设置支持判断的项目相对证据路径。</summary>
    [JsonPropertyName("evidencePaths")]
    public List<string> EvidencePaths { get; set; } = new();
}
