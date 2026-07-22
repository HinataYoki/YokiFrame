using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using YokiFrame;
using YokiFrame.Protocol.Common;
using YokiFrame.Protocol.Results;
using YokiFrame.Protocol.Validation;

namespace YokiFrame.Protocol.FileBridge;

/// <summary>
/// 表示写入 `.yokiframe/engines/&lt;engineId&gt;/commands` 的 FileBridge 命令信封。
/// </summary>
public sealed class CommandEnvelope
{
    /// <summary>
    /// Runtime CommandPolicy 允许的最短等待时间。
    /// </summary>
    public const int COMMAND_TIMEOUT_MIN_MS = YokiFrameFileBridgeContract.COMMAND_TIMEOUT_MIN_MS;

    /// <summary>
    /// Runtime CommandPolicy 允许的最长等待时间。
    /// </summary>
    public const int COMMAND_TIMEOUT_MAX_MS = YokiFrameFileBridgeContract.COMMAND_TIMEOUT_MAX_MS;

    /// <summary>
    /// 单个命令 payload JSON 的最大 UTF-8 字节数。
    /// </summary>
    public const int PAYLOAD_MAX_BYTES = YokiFrameFileBridgeContract.PAYLOAD_MAX_BYTES;

    /// <summary>
    /// 单个命令文件的最大 UTF-8 字节数。
    /// </summary>
    public const int COMMAND_FILE_MAX_BYTES = YokiFrameFileBridgeContract.COMMAND_FILE_MAX_BYTES;

    /// <summary>
    /// 获取或设置协议版本。
    /// </summary>
    [JsonPropertyName("protocolVersion")]
    public int ProtocolVersion { get; set; }

    /// <summary>
    /// 获取或设置目标 engine。
    /// </summary>
    [JsonPropertyName("engineId")]
    public string EngineId { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置命令来源，例如 cli、workbench 或 codex。
    /// </summary>
    [JsonPropertyName("source")]
    public string Source { get; set; } = "cli";

    /// <summary>
    /// 获取或设置创建时间。
    /// </summary>
    [JsonPropertyName("createdAtUtc")]
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// 获取或设置请求标识。
    /// </summary>
    [JsonPropertyName("requestId")]
    public string RequestId { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置目标 Kit。
    /// </summary>
    [JsonPropertyName("kit")]
    public string Kit { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置目标 action。
    /// </summary>
    [JsonPropertyName("action")]
    public string Action { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置 payload JSON 字符串。
    /// </summary>
    [JsonPropertyName("payloadJson")]
    public string PayloadJson { get; set; } = "{}";

    /// <summary>
    /// 获取或设置等待超时毫秒数。
    /// </summary>
    [JsonPropertyName("timeoutMs")]
    public int TimeoutMs { get; set; } = 10000;

    /// <summary>
    /// 创建并校验命令信封。
    /// </summary>
    /// <param name="engineId">目标 engine。</param>
    /// <param name="source">命令来源。</param>
    /// <param name="requestId">请求标识。</param>
    /// <param name="kit">目标 Kit。</param>
    /// <param name="action">目标 action。</param>
    /// <param name="payloadJson">payload JSON 字符串。</param>
    /// <param name="timeoutMs">等待超时毫秒数。</param>
    /// <returns>已校验的命令信封。</returns>
    public static CommandEnvelope Create(
        string engineId,
        string source,
        string requestId,
        string kit,
        string action,
        string payloadJson,
        int timeoutMs)
    {
        return new CommandEnvelope
        {
            ProtocolVersion = YokiFrameFileBridgeContract.PROTOCOL_VERSION,
            EngineId = SafeIdValidator.EnsureSafeId(engineId, nameof(engineId)),
            Source = SafeIdValidator.EnsureSafeId(source, nameof(source)),
            RequestId = SafeIdValidator.EnsureSafeId(requestId, nameof(requestId)),
            Kit = SafeIdValidator.EnsureSafeId(kit, nameof(kit)),
            Action = SafeIdValidator.EnsureSafeId(action, nameof(action)),
            PayloadJson = EnsurePayloadJson(payloadJson),
            TimeoutMs = EnsureTimeout(timeoutMs),
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
    }

    /// <summary>
    /// 从 JSON 文本反序列化命令信封。
    /// </summary>
    /// <param name="json">命令 JSON 内容。</param>
    /// <returns>解析后的命令信封。</returns>
    public static CommandEnvelope FromJson(string json)
    {
        return JsonSerializer.Deserialize(json, YokiFrameProtocolJsonContext.Default.CommandEnvelope)
            ?? new CommandEnvelope();
    }

    /// <summary>
    /// 将命令信封序列化为 compact JSON。
    /// </summary>
    /// <returns>compact JSON 文本。</returns>
    public string ToJson()
    {
        var json = JsonSerializer.Serialize(this, YokiFrameProtocolJsonContext.Default.CommandEnvelope);
        EnsureCommandJsonSize(json);
        return json;
    }

    /// <summary>
    /// 校验 payload 是合法 JSON，避免 Runtime 侧收到无法解析的信封。
    /// </summary>
    /// <param name="payloadJson">待检查 payload。</param>
    /// <returns>合法 payload JSON。</returns>
    private static string EnsurePayloadJson(string payloadJson)
    {
        var normalizedPayload = string.IsNullOrWhiteSpace(payloadJson) ? "{}" : payloadJson;
        try
        {
            using var _ = JsonDocument.Parse(normalizedPayload);
            EnsurePayloadSize(normalizedPayload);
            return normalizedPayload;
        }
        catch (JsonException exception)
        {
            throw new YokiFrameProtocolException(new YokiFrameError(
                "InvalidPayloadJson",
                $"Payload must be valid JSON: {exception.Message}",
                "Pass --payload with an object such as \"{}\".",
                Array.Empty<string>()));
        }
    }

    /// <summary>
    /// 校验 payload 字节数，防止命令通道被大 JSON 拖垮。
    /// </summary>
    /// <param name="payloadJson">已确认语法合法的 payload JSON。</param>
    private static void EnsurePayloadSize(string payloadJson)
    {
        if (Encoding.UTF8.GetByteCount(payloadJson) <= PAYLOAD_MAX_BYTES)
        {
            return;
        }

        throw new YokiFrameProtocolException(new YokiFrameError(
            "PayloadTooLarge",
            $"Payload must not exceed {PAYLOAD_MAX_BYTES} UTF-8 bytes.",
            "Reduce --payload size or move large data into a snapshot/asset file.",
            Array.Empty<string>()));
    }

    /// <summary>
    /// 校验序列化后的命令文件大小，避免元数据和 payload 组合后越过 Runtime 上限。
    /// </summary>
    /// <param name="commandJson">已序列化的命令 JSON。</param>
    private static void EnsureCommandJsonSize(string commandJson)
    {
        if (Encoding.UTF8.GetByteCount(commandJson) <= COMMAND_FILE_MAX_BYTES)
        {
            return;
        }

        throw new YokiFrameProtocolException(new YokiFrameError(
            "CommandTooLarge",
            $"Command file must not exceed {COMMAND_FILE_MAX_BYTES} UTF-8 bytes.",
            "Shorten command metadata or move large data into a snapshot/asset file.",
            Array.Empty<string>()));
    }

    /// <summary>
    /// 校验 timeout 处于 Runtime CommandPolicy 允许范围内。
    /// </summary>
    /// <param name="timeoutMs">待检查超时毫秒数。</param>
    /// <returns>合法超时毫秒数。</returns>
    private static int EnsureTimeout(int timeoutMs)
    {
        if (timeoutMs is >= COMMAND_TIMEOUT_MIN_MS and <= COMMAND_TIMEOUT_MAX_MS)
        {
            return timeoutMs;
        }

        throw new YokiFrameProtocolException(new YokiFrameError(
            "InvalidTimeout",
            $"Command timeout must be between {COMMAND_TIMEOUT_MIN_MS} and {COMMAND_TIMEOUT_MAX_MS} milliseconds.",
            "Pass --timeout with a value in the Runtime CommandPolicy range.",
            Array.Empty<string>()));
    }
}
