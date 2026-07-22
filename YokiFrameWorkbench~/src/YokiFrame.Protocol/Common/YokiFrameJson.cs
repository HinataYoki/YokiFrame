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
    public static JsonSerializerOptions CompactOptions { get; } = CreateOptions(false);

    /// <summary>
    /// 获取缩进 JSON 输出配置；该配置仅用于人工可读文件或调试输出。
    /// </summary>
    public static JsonSerializerOptions PrettyOptions { get; } = CreateOptions(true);

    /// <summary>
    /// 按缩进需求创建 JSON 配置，并统一大小写和容错读取策略。
    /// </summary>
    /// <param name="writeIndented">是否输出缩进格式。</param>
    /// <returns>可复用的 JSON 序列化配置。</returns>
    private static JsonSerializerOptions CreateOptions(bool writeIndented)
    {
        return new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = writeIndented,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            TypeInfoResolver = YokiFrameProtocolJsonContext.Default
        };
    }
}
