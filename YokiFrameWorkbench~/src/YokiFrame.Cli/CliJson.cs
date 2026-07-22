using System.Text.Json;
using System.Text.Json.Serialization;
using YokiFrame.Protocol.FastChannel;
using YokiFrame.Protocol.FileBridge;
using YokiFrame.Protocol.ProjectModel;
using YokiFrame.Tooling.Application.Models.Capabilities;
using YokiFrame.Tooling.Application.Models;
using YokiFrame.Tooling.Application.Models.ProjectModel;
using YokiFrame.Tooling.Application.Models.AudioKit;
using YokiFrame.Tooling.Application.Models.LocalizationKit;

namespace YokiFrame.Cli;

/// <summary>
/// 提供 CLI 输出专用 JSON 配置，避免把应用模型加入 Protocol 的序列化上下文。
/// </summary>
internal static class CliJson
{
    /// <summary>
    /// 获取 compact CLI JSON 配置；该配置只用于 stdout 和 stderr，不用于协议文件。
    /// </summary>
    public static JsonSerializerOptions CompactOptions { get; } = CreateOptions();

    /// <summary>
    /// 创建 Native AOT 友好的 CLI JSON 配置。
    /// </summary>
    /// <returns>CLI 专用 JSON 配置。</returns>
    private static JsonSerializerOptions CreateOptions()
    {
        return new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
            TypeInfoResolver = CliJsonContext.Default
        };
    }
}

/// <summary>
/// 为 CLI 实际输出的协议 DTO 和应用读模型生成 JSON 元数据。
/// </summary>
[JsonSourceGenerationOptions(
    GenerationMode = JsonSourceGenerationMode.Metadata,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = false)]
[JsonSerializable(typeof(CommandResponse))]
[JsonSerializable(typeof(EngineRegistryEntry))]
[JsonSerializable(typeof(FastChannelEndpoint))]
[JsonSerializable(typeof(List<FastChannelEndpoint>))]
[JsonSerializable(typeof(WorkbenchDoctorIssue[]))]
[JsonSerializable(typeof(CapabilityCatalog))]
[JsonSerializable(typeof(CapabilityCatalogProject))]
[JsonSerializable(typeof(CapabilityCatalogEngine))]
[JsonSerializable(typeof(CapabilityCatalogCommandSet))]
[JsonSerializable(typeof(CapabilityCatalogCommand))]
[JsonSerializable(typeof(CapabilityCatalogKit))]
[JsonSerializable(typeof(CapabilityCatalogIssue))]
[JsonSerializable(typeof(CapabilityCatalogSource))]
[JsonSerializable(typeof(ProjectModelBundle))]
[JsonSerializable(typeof(ProjectModelManifest))]
[JsonSerializable(typeof(List<ProjectCapabilityKit>))]
[JsonSerializable(typeof(ProjectModelIssue[]))]
[JsonSerializable(typeof(AudioIndexEntry[]))]
[JsonSerializable(typeof(LocalizationEntryRecord[]))]
[JsonSerializable(typeof(LocalizationEntryRecord))]
[JsonSerializable(typeof(LocalizationLanguageRecord[]))]
[JsonSerializable(typeof(LocalizationLanguageRecord))]
[JsonSerializable(typeof(LocalizationCatalog))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(Dictionary<string, IReadOnlyDictionary<string, string>>))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(string[]))]
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(Dictionary<string, JsonElement>))]
internal sealed partial class CliJsonContext : JsonSerializerContext
{
}
