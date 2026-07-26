using System.Text.Json.Serialization;
using YokiFrame.Packaging.Models;

namespace YokiFrame.Packaging.Services;

/// <summary>为 Runtime manifest 和当前指针提供 Native AOT 可用的 JSON 元数据。</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true,
    WriteIndented = true)]
[JsonSerializable(typeof(RuntimeManifest))]
[JsonSerializable(typeof(RuntimeCachePointer))]
internal sealed partial class RuntimePackagingJsonContext : JsonSerializerContext
{
}
