using YokiFrame;
using YokiFrame.Protocol.Results;

namespace YokiFrame.Client.FastChannel.IO;

/// <summary>
/// Client 侧唯一的 FastChannel framing 边界：直接使用共享 Core 帧类型读写 Stream，
/// 并把 Core framing 异常统一转换为工具协议异常，供 Application/CLI 的回退与错误白名单识别。
/// </summary>
internal static class FastChannelFrameStream
{
    /// <summary>读取一个完整 Core 帧并保留共享帧类型。</summary>
    /// <param name="stream">已建立连接的可读 Stream。</param>
    /// <param name="cancellationToken">读取取消令牌。</param>
    public static async Task<YokiFrameFastChannelFrame> ReadAsync(Stream stream, CancellationToken cancellationToken)
    {
        try
        {
            return await YokiFrameFastChannelFrameStream.ReadAsync(stream, cancellationToken).ConfigureAwait(false);
        }
        catch (YokiFrameFastChannelProtocolException exception)
        {
            throw ConvertException(exception);
        }
    }

    /// <summary>将一个完整 Core 帧编码、写入并刷新到当前 Stream。</summary>
    /// <param name="stream">已建立连接的可写 Stream。</param>
    /// <param name="frame">待写入的共享协议消息。</param>
    /// <param name="cancellationToken">写入取消令牌。</param>
    public static async Task WriteAsync(Stream stream, YokiFrameFastChannelFrame frame, CancellationToken cancellationToken)
    {
        try
        {
            await YokiFrameFastChannelFrameStream.WriteAsync(stream, frame, cancellationToken).ConfigureAwait(false);
        }
        catch (YokiFrameFastChannelProtocolException exception)
        {
            throw ConvertException(exception);
        }
    }

    /// <summary>将 Core framing 异常映射为 Client、Application 和 CLI 统一使用的标准错误。</summary>
    private static YokiFrameProtocolException ConvertException(YokiFrameFastChannelProtocolException exception)
    {
        return new YokiFrameProtocolException(new YokiFrameError(
            exception.Code,
            exception.Message,
            exception.Suggestion));
    }
}
