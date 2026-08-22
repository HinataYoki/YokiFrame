using System.Text.Json;

namespace YokiFrame.Protocol.Common;

/// <summary>
/// 提供 YokiFrame 协议 DTO 与协议 JSON 节点使用的序列化配置。
/// </summary>
public static class YokiFrameJson
{
    /// <summary>
    /// 获取 compact 协议 JSON 配置；CLI 应用输出使用自己的 JSON context。
    /// </summary>
    public static JsonSerializerOptions CompactOptions { get; } = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        TypeInfoResolver = YokiFrameProtocolJsonContext.Default
    };
}
