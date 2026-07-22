#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
namespace YokiFrame
{
    /// <summary>
    /// 表示通过 FastChannel 固定 framing 传输的纯 C# 消息；payload 保持 UTF-8 JSON 文本，不依赖具体 JSON 库。
    /// </summary>
    public sealed class YokiFrameFastChannelFrame
    {
        /// <summary>
        /// 创建 FastChannel 消息；编码器会在写入前校验消息类型、UTF-8 和 payload 容量。
        /// </summary>
        /// <param name="messageKind">当前消息类型。</param>
        /// <param name="flags">协议预留 flags。</param>
        /// <param name="payloadJson">UTF-8 JSON payload；为空时写入空文本。</param>
        public YokiFrameFastChannelFrame(
            YokiFrameFastChannelMessageKind messageKind,
            byte flags,
            string payloadJson)
        {
            MessageKind = messageKind;
            Flags = flags;
            PayloadJson = payloadJson ?? string.Empty;
        }

        /// <summary>
        /// 获取当前消息的固定协议类型。
        /// </summary>
        public YokiFrameFastChannelMessageKind MessageKind { get; private set; }

        /// <summary>
        /// 获取当前消息的保留 flags。
        /// </summary>
        public byte Flags { get; private set; }

        /// <summary>
        /// 获取当前消息的 UTF-8 JSON payload 文本。
        /// </summary>
        public string PayloadJson { get; private set; }
    }
}
#endif
