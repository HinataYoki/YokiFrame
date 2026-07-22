using YokiFrame;
using YokiFrame.Protocol.Results;

namespace YokiFrame.Protocol.FastChannel;

/// <summary>
/// 为 .NET 工具侧暴露 FastChannel framing facade；实际字节编解码只委托 Core Runtime codec。
/// </summary>
public static class FastChannelFrameCodec
{
    /// <summary>
    /// 将工具侧 frame 委托给 Core Runtime codec 编码，并转换为标准工具协议异常。
    /// </summary>
    /// <param name="frame">待编码的工具侧消息。</param>
    /// <returns>可直接写入本机传输的完整 frame 字节。</returns>
    public static byte[] Encode(FastChannelFrame frame)
    {
        if (frame == null)
        {
            throw new ArgumentNullException(nameof(frame));
        }

        try
        {
            return YokiFrameFastChannelFrameCodec.Encode(new YokiFrameFastChannelFrame(
                frame.Kind,
                frame.Flags,
                frame.PayloadJson));
        }
        catch (YokiFrameFastChannelProtocolException exception)
        {
            throw ConvertException(exception);
        }
    }

    /// <summary>
    /// 通过 Core Runtime codec 解码完整 frame，并映射回工具侧消息类型。
    /// </summary>
    /// <param name="frameBytes">恰好包含一个完整 frame 的字节。</param>
    /// <returns>已校验的工具侧消息。</returns>
    public static FastChannelFrame Decode(ReadOnlySpan<byte> frameBytes)
    {
        try
        {
            var frame = YokiFrameFastChannelFrameCodec.Decode(frameBytes);
            return new FastChannelFrame(frame.MessageKind, frame.Flags, frame.PayloadJson);
        }
        catch (YokiFrameFastChannelProtocolException exception)
        {
            throw ConvertException(exception);
        }
    }

    /// <summary>
    /// 读取并校验固定 header，使 Client transport 能在分配 payload 缓冲区前拒绝异常长度。
    /// </summary>
    /// <param name="headerBytes">至少包含固定 12 字节 header 的缓冲区。</param>
    /// <returns>已通过 Core framing 校验的 header。</returns>
    public static YokiFrameFastChannelFrameHeader ReadValidatedHeader(ReadOnlySpan<byte> headerBytes)
    {
        try
        {
            return YokiFrameFastChannelFrameCodec.ReadValidatedHeader(headerBytes);
        }
        catch (YokiFrameFastChannelProtocolException exception)
        {
            throw ConvertException(exception);
        }
    }

    /// <summary>
    /// 将 Core Runtime framing 异常映射为 CLI、Application 和 Client 统一使用的标准错误。
    /// </summary>
    /// <param name="exception">Core codec 返回的跨宿主协议异常。</param>
    /// <returns>工具侧标准协议异常。</returns>
    private static YokiFrameProtocolException ConvertException(YokiFrameFastChannelProtocolException exception)
    {
        return new YokiFrameProtocolException(new YokiFrameError(
            exception.Code,
            exception.Message,
            exception.Suggestion));
    }
}
