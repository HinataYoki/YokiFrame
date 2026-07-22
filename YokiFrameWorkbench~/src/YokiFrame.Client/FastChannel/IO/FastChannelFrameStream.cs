using YokiFrame;
using YokiFrame.Protocol.FastChannel;
using YokiFrame.Protocol.Results;

namespace YokiFrame.Client.FastChannel.IO;

/// <summary>
/// 为 .NET Client 暴露 Core Runtime Stream framing facade，并转换为统一的工具协议异常。
/// </summary>
public static class FastChannelFrameStream
{
    /// <summary>
    /// 读取一个完整 Core frame 并映射为 Client 使用的工具侧消息。
    /// </summary>
    /// <param name="stream">已建立连接的可读 Stream。</param>
    /// <param name="cancellationToken">读取取消令牌。</param>
    /// <returns>已完成 framing 和 UTF-8 校验的消息。</returns>
    public static async Task<FastChannelFrame> ReadAsync(Stream stream, CancellationToken cancellationToken)
    {
        try
        {
            var frame = await YokiFrameFastChannelFrameStream.ReadAsync(stream, cancellationToken).ConfigureAwait(false);
            return new FastChannelFrame(frame.MessageKind, frame.Flags, frame.PayloadJson);
        }
        catch (YokiFrameFastChannelProtocolException exception)
        {
            throw ConvertException(exception);
        }
    }

    /// <summary>
    /// 将工具侧消息映射为 Core frame 后写入当前 Stream。
    /// </summary>
    /// <param name="stream">已建立连接的可写 Stream。</param>
    /// <param name="frame">待写入的协议消息。</param>
    /// <param name="cancellationToken">写入取消令牌。</param>
    /// <returns>写入完成后的异步任务。</returns>
    public static async Task WriteAsync(Stream stream, FastChannelFrame frame, CancellationToken cancellationToken)
    {
        if (frame == null)
        {
            throw new ArgumentNullException(nameof(frame));
        }

        try
        {
            await YokiFrameFastChannelFrameStream.WriteAsync(
                stream,
                new YokiFrameFastChannelFrame(frame.Kind, frame.Flags, frame.PayloadJson),
                cancellationToken).ConfigureAwait(false);
        }
        catch (YokiFrameFastChannelProtocolException exception)
        {
            throw ConvertException(exception);
        }
    }

    /// <summary>
    /// 将 Core framing 异常映射为 Client、Application 和 CLI 统一使用的标准错误。
    /// </summary>
    /// <param name="exception">Core stream 返回的跨宿主协议异常。</param>
    /// <returns>工具侧标准协议异常。</returns>
    private static YokiFrameProtocolException ConvertException(YokiFrameFastChannelProtocolException exception)
    {
        return new YokiFrameProtocolException(new YokiFrameError(
            exception.Code,
            exception.Message,
            exception.Suggestion));
    }
}
