using YokiFrame.Client.FastChannel.IO;
using YokiFrame.Protocol.FastChannel;

namespace YokiFrame.Client.FastChannel;

/// <summary>
/// 表示已完成 FastChannel Hello/HelloAck 校验的单一传输连接，并串行化请求响应操作。
/// </summary>
public sealed class FastChannelConnection : IAsyncDisposable
{
    private readonly Stream mStream;
    private readonly SemaphoreSlim mRequestGate = new(1, 1);
    private int mDisposed;

    /// <summary>
    /// 使用已经建立且完成握手的 Stream 创建连接对象；仅 transport connector 可以调用。
    /// </summary>
    /// <param name="endpoint">本次连接对应的 registry endpoint。</param>
    /// <param name="stream">已连接的双向传输流。</param>
    internal FastChannelConnection(FastChannelEndpoint endpoint, Stream stream)
    {
        Endpoint = endpoint;
        mStream = stream;
    }

    /// <summary>
    /// 获取建连时验证过的 endpoint；registry session 或 generation 改变后调用侧必须丢弃当前实例。
    /// </summary>
    public FastChannelEndpoint Endpoint { get; }

    /// <summary>
    /// 串行发送一条已完成业务层校验的请求并读取紧随其后的一个响应 frame。
    /// </summary>
    /// <param name="request">待发送的协议 frame。</param>
    /// <param name="cancellationToken">调用侧取消令牌。</param>
    /// <returns>Host 返回的下一条完整 response 或 error frame。</returns>
    public async Task<FastChannelFrame> RequestAsync(FastChannelFrame request, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        await mRequestGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            await FastChannelFrameStream.WriteAsync(mStream, request, cancellationToken).ConfigureAwait(false);
            return await FastChannelFrameStream.ReadAsync(mStream, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            mRequestGate.Release();
        }
    }

    /// <summary>
    /// 完成 Hello/HelloAck 校验后才将连接交给调用侧；校验失败由 connector 负责释放 transport。
    /// </summary>
    /// <param name="cancellationToken">握手取消令牌。</param>
    /// <returns>握手完成后的异步任务。</returns>
    internal async Task PerformHandshakeAsync(CancellationToken cancellationToken)
    {
        await FastChannelFrameStream.WriteAsync(
            mStream,
            FastChannelHandshake.CreateHello(Endpoint),
            cancellationToken).ConfigureAwait(false);
        var acknowledgement = await FastChannelFrameStream.ReadAsync(mStream, cancellationToken).ConfigureAwait(false);
        FastChannelHandshake.EnsureHelloAckMatchesEndpoint(acknowledgement, Endpoint);
    }

    /// <summary>
    /// 关闭底层 Stream；正在进行的请求会先完成或失败，后续请求会被拒绝。
    /// </summary>
    /// <returns>资源释放完成后的异步值任务。</returns>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref mDisposed, 1) != 0)
        {
            return;
        }

        await mRequestGate.WaitAsync().ConfigureAwait(false);
        try
        {
            await mStream.DisposeAsync().ConfigureAwait(false);
        }
        finally
        {
            mRequestGate.Release();
            mRequestGate.Dispose();
        }
    }

    /// <summary>
    /// 拒绝在已经释放的连接上继续读写，避免背景重连与 UI 请求共享失效 Stream。
    /// </summary>
    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref mDisposed) == 0)
        {
            return;
        }

        throw new ObjectDisposedException(nameof(FastChannelConnection));
    }
}
