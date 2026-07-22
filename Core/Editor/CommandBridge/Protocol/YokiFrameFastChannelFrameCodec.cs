#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System;
using System.Text;

namespace YokiFrame
{
    /// <summary>
    /// 负责 FastChannel v1 的固定帧编解码与无状态输入校验，不持有 Stream、Socket、JSON 库或宿主生命周期。
    /// </summary>
    public static class YokiFrameFastChannelFrameCodec
    {
        private static readonly Encoding sUtf8 = new UTF8Encoding(false, true);

        /// <summary>
        /// 将 FastChannel frame 编码为固定大端 header 和严格 UTF-8 payload。
        /// </summary>
        /// <param name="frame">待编码的协议消息。</param>
        /// <returns>可直接写入任意本机传输的完整 frame 字节。</returns>
        public static byte[] Encode(YokiFrameFastChannelFrame frame)
        {
            if (frame == null)
            {
                throw new ArgumentNullException(nameof(frame));
            }

            EnsureMessageKind(frame.MessageKind);
            var payloadBytes = EncodePayload(frame.PayloadJson);
            var bytes = new byte[YokiFrameFastChannelContract.HEADER_SIZE + payloadBytes.Length];
            var header = new YokiFrameFastChannelFrameHeader(
                YokiFrameFastChannelContract.MAGIC,
                YokiFrameFastChannelContract.PROTOCOL_VERSION,
                frame.MessageKind,
                frame.Flags,
                payloadBytes.Length);

            header.WriteTo(bytes.AsSpan(0, YokiFrameFastChannelContract.HEADER_SIZE));
            payloadBytes.CopyTo(bytes.AsSpan(YokiFrameFastChannelContract.HEADER_SIZE));
            return bytes;
        }

        /// <summary>
        /// 解码单个完整 FastChannel frame，并拒绝截断、尾随数据、未知版本与非法 UTF-8。
        /// </summary>
        /// <param name="frameBytes">恰好包含一个完整 frame 的字节。</param>
        /// <returns>已校验的消息对象。</returns>
        public static YokiFrameFastChannelFrame Decode(ReadOnlySpan<byte> frameBytes)
        {
            var header = ReadValidatedHeader(frameBytes);
            var expectedLength = YokiFrameFastChannelContract.HEADER_SIZE + header.PayloadLength;
            if (frameBytes.Length < expectedLength)
            {
                throw CreateProtocolException(
                    "FastChannelFrameTruncated",
                    "FastChannel frame payload is shorter than its declared length.",
                    "Continue reading the current frame before decoding it.");
            }

            if (frameBytes.Length > expectedLength)
            {
                throw CreateProtocolException(
                    "FastChannelFrameTrailingBytes",
                    "FastChannel decoder received bytes from more than one frame.",
                    "Split frames using the fixed header payloadLength before decoding.");
            }

            return new YokiFrameFastChannelFrame(
                header.MessageKind,
                header.Flags,
                DecodePayload(frameBytes.Slice(YokiFrameFastChannelContract.HEADER_SIZE, header.PayloadLength)));
        }

        /// <summary>
        /// 读取并校验固定 header，使 stream transport 能在分配 payload 缓冲区前拒绝异常长度。
        /// </summary>
        /// <param name="headerBytes">至少包含固定 12 字节 header 的缓冲区。</param>
        /// <returns>已通过 magic、版本、消息类型和长度检查的 header。</returns>
        public static YokiFrameFastChannelFrameHeader ReadValidatedHeader(ReadOnlySpan<byte> headerBytes)
        {
            if (headerBytes.Length < YokiFrameFastChannelContract.HEADER_SIZE)
            {
                throw CreateProtocolException(
                    "FastChannelFrameTooSmall",
                    "FastChannel frame is smaller than the fixed header.",
                    "Read the complete 12-byte FastChannel header before decoding.");
            }

            var header = YokiFrameFastChannelFrameHeader.Read(headerBytes);
            ValidateHeader(header);
            return header;
        }

        /// <summary>
        /// 将 UTF-16 文本编码为严格 UTF-8，并在分配完整 frame 前执行容量限制。
        /// </summary>
        /// <param name="payloadJson">待写入的 payload 文本。</param>
        /// <returns>已验证的 UTF-8 字节。</returns>
        private static byte[] EncodePayload(string payloadJson)
        {
            try
            {
                var payloadBytes = sUtf8.GetBytes(payloadJson ?? string.Empty);
                EnsurePayloadLength(payloadBytes.Length);
                return payloadBytes;
            }
            catch (EncoderFallbackException exception)
            {
                throw CreateProtocolException(
                    "FastChannelInvalidUtf8",
                    "FastChannel payload contains an invalid UTF-16 sequence: " + exception.Message,
                    "Provide a valid Unicode JSON payload.");
            }
        }

        /// <summary>
        /// 将 payload 字节解码为严格 UTF-8 文本，拒绝替换字符掩盖的损坏传输数据。
        /// </summary>
        /// <param name="payloadBytes">已通过长度校验的 payload 字节。</param>
        /// <returns>解码后的 JSON 文本。</returns>
        private static string DecodePayload(ReadOnlySpan<byte> payloadBytes)
        {
            try
            {
                return sUtf8.GetString(payloadBytes);
            }
            catch (DecoderFallbackException exception)
            {
                throw CreateProtocolException(
                    "FastChannelInvalidUtf8",
                    "FastChannel payload is not valid UTF-8: " + exception.Message,
                    "Close the channel and reconnect with a compatible host.");
            }
        }

        /// <summary>
        /// 校验固定 header 的魔数、版本、消息类型和 payload 长度。
        /// </summary>
        /// <param name="header">刚从字节流读取的 header。</param>
        private static void ValidateHeader(YokiFrameFastChannelFrameHeader header)
        {
            if (header.Magic != YokiFrameFastChannelContract.MAGIC)
            {
                throw CreateProtocolException(
                    "FastChannelInvalidMagic",
                    "FastChannel frame magic does not match YFCH.",
                    "Verify that the selected endpoint is a YokiFrame FastChannel host.");
            }

            if (header.ProtocolVersion != YokiFrameFastChannelContract.PROTOCOL_VERSION)
            {
                throw CreateProtocolException(
                    "FastChannelUnsupportedVersion",
                    "FastChannel protocolVersion is not supported.",
                    "Refresh engine registry and reconnect using a compatible Workbench version.");
            }

            EnsureMessageKind(header.MessageKind);
            EnsurePayloadLength(header.PayloadLength);
        }

        /// <summary>
        /// 校验 message kind 属于当前 FastChannel v1 的固定集合。
        /// </summary>
        /// <param name="messageKind">待校验的消息类型。</param>
        private static void EnsureMessageKind(YokiFrameFastChannelMessageKind messageKind)
        {
            if (YokiFrameFastChannelContract.IsKnownMessageKind(messageKind))
            {
                return;
            }

            throw CreateProtocolException(
                "FastChannelUnknownMessageKind",
                "FastChannel message kind is not supported by protocol v1.",
                "Refresh the client or reconnect to a compatible host.");
        }

        /// <summary>
        /// 校验 payload 长度在单帧容量范围内，避免非受信 endpoint 触发大内存分配。
        /// </summary>
        /// <param name="payloadLength">header 或编码器给出的 payload 字节数。</param>
        private static void EnsurePayloadLength(int payloadLength)
        {
            if (payloadLength >= 0 && payloadLength <= YokiFrameFastChannelContract.MAX_PAYLOAD_BYTES)
            {
                return;
            }

            throw CreateProtocolException(
                "FastChannelPayloadTooLarge",
                "FastChannel payload exceeds the v1 frame limit.",
                "Use FileBridge snapshots for large data or split the request into smaller reads.");
        }

        /// <summary>
        /// 创建 Adapter 与工具侧都可识别的 FastChannel framing 异常。
        /// </summary>
        /// <param name="code">稳定错误码。</param>
        /// <param name="message">面向调用侧的错误说明。</param>
        /// <param name="suggestion">恢复建议。</param>
        /// <returns>跨宿主协议异常。</returns>
        private static YokiFrameFastChannelProtocolException CreateProtocolException(
            string code,
            string message,
            string suggestion)
        {
            return new YokiFrameFastChannelProtocolException(code, message, suggestion);
        }
    }
}
#endif
