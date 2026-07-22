#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System;
using System.Buffers.Binary;

namespace YokiFrame
{
    /// <summary>
    /// 表示 FastChannel v1 的固定 12 字节大端 framing header；该类型不执行 IO 或 JSON 解析。
    /// </summary>
    public readonly struct YokiFrameFastChannelFrameHeader
    {
        /// <summary>
        /// 创建完整 FastChannel header；调用侧仍需由协议层校验 magic、版本和长度。
        /// </summary>
        /// <param name="magic">固定协议魔数。</param>
        /// <param name="protocolVersion">固定协议版本。</param>
        /// <param name="messageKind">当前 frame 的消息类型。</param>
        /// <param name="flags">当前 frame 的保留 flags。</param>
        /// <param name="payloadLength">UTF-8 payload 字节数。</param>
        public YokiFrameFastChannelFrameHeader(
            uint magic,
            ushort protocolVersion,
            YokiFrameFastChannelMessageKind messageKind,
            byte flags,
            int payloadLength)
        {
            Magic = magic;
            ProtocolVersion = protocolVersion;
            MessageKind = messageKind;
            Flags = flags;
            PayloadLength = payloadLength;
        }

        /// <summary>
        /// 获取 header 声明的协议魔数。
        /// </summary>
        public uint Magic { get; }

        /// <summary>
        /// 获取 header 声明的协议版本。
        /// </summary>
        public ushort ProtocolVersion { get; }

        /// <summary>
        /// 获取 header 声明的消息类型。
        /// </summary>
        public YokiFrameFastChannelMessageKind MessageKind { get; }

        /// <summary>
        /// 获取 header 中保留给后续兼容的 flags。
        /// </summary>
        public byte Flags { get; }

        /// <summary>
        /// 获取 header 声明的 UTF-8 payload 字节数。
        /// </summary>
        public int PayloadLength { get; }

        /// <summary>
        /// 从固定 header 字节读取字段，不负责协议语义校验。
        /// </summary>
        /// <param name="headerBytes">至少包含 12 字节的 header 缓冲区。</param>
        /// <returns>按大端字节序读取的 header。</returns>
        public static YokiFrameFastChannelFrameHeader Read(ReadOnlySpan<byte> headerBytes)
        {
            if (headerBytes.Length < YokiFrameFastChannelContract.HEADER_SIZE)
            {
                throw new ArgumentException("FastChannel header buffer is too small.", nameof(headerBytes));
            }

            return new YokiFrameFastChannelFrameHeader(
                BinaryPrimitives.ReadUInt32BigEndian(headerBytes.Slice(YokiFrameFastChannelContract.MAGIC_OFFSET, 4)),
                BinaryPrimitives.ReadUInt16BigEndian(headerBytes.Slice(YokiFrameFastChannelContract.PROTOCOL_VERSION_OFFSET, 2)),
                (YokiFrameFastChannelMessageKind)headerBytes[YokiFrameFastChannelContract.MESSAGE_KIND_OFFSET],
                headerBytes[YokiFrameFastChannelContract.FLAGS_OFFSET],
                BinaryPrimitives.ReadInt32BigEndian(headerBytes.Slice(YokiFrameFastChannelContract.PAYLOAD_LENGTH_OFFSET, 4)));
        }

        /// <summary>
        /// 按固定大端布局写入 header；调用方必须提供至少 12 字节的目标缓冲区。
        /// </summary>
        /// <param name="headerBytes">目标 header 缓冲区。</param>
        public void WriteTo(Span<byte> headerBytes)
        {
            if (headerBytes.Length < YokiFrameFastChannelContract.HEADER_SIZE)
            {
                throw new ArgumentException("FastChannel header buffer is too small.", nameof(headerBytes));
            }

            BinaryPrimitives.WriteUInt32BigEndian(headerBytes.Slice(YokiFrameFastChannelContract.MAGIC_OFFSET, 4), Magic);
            BinaryPrimitives.WriteUInt16BigEndian(headerBytes.Slice(YokiFrameFastChannelContract.PROTOCOL_VERSION_OFFSET, 2), ProtocolVersion);
            headerBytes[YokiFrameFastChannelContract.MESSAGE_KIND_OFFSET] = (byte)MessageKind;
            headerBytes[YokiFrameFastChannelContract.FLAGS_OFFSET] = Flags;
            BinaryPrimitives.WriteInt32BigEndian(
                headerBytes.Slice(YokiFrameFastChannelContract.PAYLOAD_LENGTH_OFFSET, 4),
                PayloadLength);
        }
    }
}
#endif
