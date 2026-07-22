using YokiFrame;

namespace YokiFrame.Protocol.FastChannel;

/// <summary>
/// 表示已完成 framing 验证的 FastChannel 消息；payload 保持 UTF-8 JSON 文本，具体 DTO 由上层消息类型解析。
/// </summary>
public sealed class FastChannelFrame
{
    /// <summary>
    /// 创建 FastChannel 消息；编码器会在写入前校验消息类型、UTF-8 和 payload 容量。
    /// </summary>
    /// <param name="kind">消息类型。</param>
    /// <param name="flags">协议预留 flags。</param>
    /// <param name="payloadJson">UTF-8 JSON payload；为空时写入空文本。</param>
    public FastChannelFrame(YokiFrameFastChannelMessageKind kind, byte flags, string payloadJson)
    {
        Kind = kind;
        Flags = flags;
        PayloadJson = payloadJson ?? string.Empty;
    }

    /// <summary>
    /// 获取当前消息的固定协议类型。
    /// </summary>
    public YokiFrameFastChannelMessageKind Kind { get; }

    /// <summary>
    /// 获取当前消息的保留 flags。
    /// </summary>
    public byte Flags { get; }

    /// <summary>
    /// 获取当前消息的 UTF-8 JSON payload 文本。
    /// </summary>
    public string PayloadJson { get; }
}
