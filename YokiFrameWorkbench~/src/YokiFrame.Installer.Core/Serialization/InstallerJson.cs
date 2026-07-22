using System.Text.Json;
using System.Text.Json.Serialization;
using YokiFrame.Installer.Core.Models;

namespace YokiFrame.Installer.Core.Serialization;

/// <summary>
/// 提供 Installer 持久化文件的 Native AOT 友好 JSON 配置。
/// </summary>
internal static class InstallerJson
{
    /// <summary>
    /// 获取稳定、可审阅的 Installer JSON 配置。
    /// </summary>
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    /// <summary>
    /// 创建使用 source-generated metadata 的 JSON 配置。
    /// </summary>
    /// <returns>Installer JSON 配置。</returns>
    private static JsonSerializerOptions CreateOptions()
    {
        return new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            TypeInfoResolver = InstallerJsonContext.Default
        };
    }
}

/// <summary>
/// 为 owner manifest 生成 Native AOT 序列化元数据。
/// </summary>
[JsonSourceGenerationOptions(
    GenerationMode = JsonSourceGenerationMode.Metadata,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true)]
[JsonSerializable(typeof(PackageOwnerManifest))]
[JsonSerializable(typeof(PackageOwnerFile))]
[JsonSerializable(typeof(List<PackageOwnerFile>))]
internal sealed partial class InstallerJsonContext : JsonSerializerContext
{
}
