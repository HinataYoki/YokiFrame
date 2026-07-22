using System.Text.Json;
using System.Text.Json.Serialization;
using YokiFrame.Protocol.FastChannel;
using YokiFrame.Protocol.FileBridge;
using YokiFrame.Protocol.ProjectModel;

namespace YokiFrame.Protocol.Common;

/// <summary>
/// 为 YokiFrame Protocol 的 AOT 发布提供 JSON 元数据，避免运行时反射序列化。
/// </summary>
[JsonSourceGenerationOptions(
    GenerationMode = JsonSourceGenerationMode.Metadata,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true,
    WriteIndented = false)]
[JsonSerializable(typeof(CommandEnvelope))]
[JsonSerializable(typeof(CommandResponse))]
[JsonSerializable(typeof(EngineRegistryEntry))]
[JsonSerializable(typeof(FastChannelEndpoint))]
[JsonSerializable(typeof(FastChannelSessionIdentity))]
[JsonSerializable(typeof(ProjectModelManifest))]
[JsonSerializable(typeof(ProjectArchitectureDocument))]
[JsonSerializable(typeof(ProjectCapabilitiesDocument))]
[JsonSerializable(typeof(ProjectCapabilityDescriptor))]
[JsonSerializable(typeof(ProjectDependenciesDocument))]
[JsonSerializable(typeof(ProjectValidationProfileDocument))]
[JsonSerializable(typeof(ProjectModelBundle))]
[JsonSerializable(typeof(List<FastChannelEndpoint>))]
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(Dictionary<string, JsonElement>))]
internal sealed partial class YokiFrameProtocolJsonContext : JsonSerializerContext
{
}
