using System.Text.Json;
using System.Text.Json.Serialization;
using YokiFrame.Protocol.Common;

namespace YokiFrame.Protocol.FileBridge;

/// <summary>
/// 表示 Runtime 写入 results 目录的命令响应。
/// </summary>
public sealed class CommandResponse
{
    /// <summary>
    /// 获取或设置协议版本。
    /// </summary>
    [JsonPropertyName("protocolVersion")]
    public int ProtocolVersion { get; set; }

    /// <summary>
    /// 获取或设置请求标识。
    /// </summary>
    [JsonPropertyName("requestId")]
    public string RequestId { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置 engine 标识。
    /// </summary>
    [JsonPropertyName("engineId")]
    public string EngineId { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置响应状态。
    /// </summary>
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置结果 JSON 字符串。
    /// </summary>
    [JsonPropertyName("resultJson")]
    public string ResultJson { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置错误码。
    /// </summary>
    [JsonPropertyName("errorCode")]
    public string ErrorCode { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置错误消息。
    /// </summary>
    [JsonPropertyName("errorMessage")]
    public string ErrorMessage { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置完成时间。
    /// </summary>
    [JsonPropertyName("completedAtUtc")]
    public string CompletedAtUtc { get; set; } = string.Empty;

    /// <summary>
    /// 从 JSON 文本反序列化命令响应。
    /// </summary>
    /// <param name="json">响应 JSON 内容。</param>
    /// <returns>解析后的命令响应。</returns>
    public static CommandResponse FromJson(string json)
    {
        return JsonSerializer.Deserialize(json, YokiFrameProtocolJsonContext.Default.CommandResponse)
            ?? throw new JsonException("Command response JSON must contain an object, not null.");
    }
}
