using System.Buffers.Binary;
using System.Text;
using YokiFrame;
using YokiFrame.Protocol.FastChannel;
using YokiFrame.Protocol.Results;

namespace YokiFrame.Protocol.Tests;

/// <summary>
/// 覆盖 FastChannel v1 固定帧格式、字节序和输入边界。
/// </summary>
public sealed class FastChannelFrameCodecTests
{
    /// <summary>
    /// 验证编码器写入固定 12 字节大端 header，并可无损还原 UTF-8 payload。
    /// </summary>
    [Fact]
    public void EncodeWritesBigEndianHeaderAndRoundtripsPayload()
    {
        const byte flags = 0xA5;
        const string payloadJson = "{\"engineId\":\"unity-editor\"}";
        var frame = new FastChannelFrame(YokiFrameFastChannelMessageKind.Hello, flags, payloadJson);

        var bytes = FastChannelFrameCodec.Encode(frame);
        var payloadLength = Encoding.UTF8.GetByteCount(payloadJson);
        var decoded = FastChannelFrameCodec.Decode(bytes);

        Assert.Equal(YokiFrameFastChannelContract.HEADER_SIZE + payloadLength, bytes.Length);
        Assert.Equal((byte)'Y', bytes[0]);
        Assert.Equal((byte)'F', bytes[1]);
        Assert.Equal((byte)'C', bytes[2]);
        Assert.Equal((byte)'H', bytes[3]);
        Assert.Equal(0, bytes[4]);
        Assert.Equal(1, bytes[5]);
        Assert.Equal((byte)YokiFrameFastChannelMessageKind.Hello, bytes[6]);
        Assert.Equal(flags, bytes[7]);
        Assert.Equal(payloadLength, BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(8, 4)));
        Assert.Equal(YokiFrameFastChannelMessageKind.Hello, decoded.Kind);
        Assert.Equal(flags, decoded.Flags);
        Assert.Equal(payloadJson, decoded.PayloadJson);
    }

    /// <summary>
    /// 验证不完整帧不会被当作合法消息，避免粘包读取器接受截断数据。
    /// </summary>
    [Fact]
    public void DecodeRejectsTruncatedPayload()
    {
        var bytes = CreateHeader(YokiFrameFastChannelMessageKind.Hello, 0, 2);
        var exception = Assert.Throws<YokiFrameProtocolException>(() => FastChannelFrameCodec.Decode(bytes));

        Assert.Equal("FastChannelFrameTruncated", exception.Error.Code);
    }

    /// <summary>
    /// 验证未知 message kind 被拒绝，防止新版本消息被旧宿主错误执行。
    /// </summary>
    [Fact]
    public void DecodeRejectsUnknownMessageKind()
    {
        var bytes = CreateHeader((YokiFrameFastChannelMessageKind)99, 0, 0);
        var exception = Assert.Throws<YokiFrameProtocolException>(() => FastChannelFrameCodec.Decode(bytes));

        Assert.Equal("FastChannelUnknownMessageKind", exception.Error.Code);
    }

    /// <summary>
    /// 验证声明超过总帧上限的 payload 会在分配前被拒绝。
    /// </summary>
    [Fact]
    public void DecodeRejectsPayloadExceedingFrameLimit()
    {
        var bytes = CreateHeader(
            YokiFrameFastChannelMessageKind.Command,
            0,
            YokiFrameFastChannelContract.MAX_PAYLOAD_BYTES + 1);
        var exception = Assert.Throws<YokiFrameProtocolException>(() => FastChannelFrameCodec.Decode(bytes));

        Assert.Equal("FastChannelPayloadTooLarge", exception.Error.Code);
    }

    /// <summary>
    /// 验证 payload 不是严格 UTF-8 时被拒绝，保证后续 JSON 解码不会接收替换字符。
    /// </summary>
    [Fact]
    public void DecodeRejectsInvalidUtf8Payload()
    {
        var bytes = new byte[YokiFrameFastChannelContract.HEADER_SIZE + 1];
        CreateHeader(YokiFrameFastChannelMessageKind.Hello, 0, 1).CopyTo(bytes, 0);
        bytes[^1] = 0xFF;

        var exception = Assert.Throws<YokiFrameProtocolException>(() => FastChannelFrameCodec.Decode(bytes));

        Assert.Equal("FastChannelInvalidUtf8", exception.Error.Code);
    }

    /// <summary>
    /// 创建只含 FastChannel v1 header 的测试字节，用于构造损坏或截断输入。
    /// </summary>
    /// <param name="kind">要写入的 message kind。</param>
    /// <param name="flags">要写入的 flags。</param>
    /// <param name="payloadLength">header 声明的 payload 长度。</param>
    /// <returns>固定长度 header 字节。</returns>
    private static byte[] CreateHeader(YokiFrameFastChannelMessageKind kind, byte flags, int payloadLength)
    {
        var bytes = new byte[YokiFrameFastChannelContract.HEADER_SIZE];
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(0, 4), YokiFrameFastChannelContract.MAGIC);
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(4, 2), YokiFrameFastChannelContract.PROTOCOL_VERSION);
        bytes[6] = (byte)kind;
        bytes[7] = flags;
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(8, 4), payloadLength);
        return bytes;
    }
}
