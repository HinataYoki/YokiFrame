using YokiFrame.Client.FastChannel.IO;
using YokiFrame.Protocol.FastChannel;

namespace YokiFrame.Client.FastChannel;

/// <summary>
/// 表示已完成 FastChannel Hello/HelloAck 校验的单一传输连接，并串行化请求响应操作。
/// </summary>
public sealed class FastChannelConnection : IAsyncDisposable
{
    private const int DISPOSE_WAIT_MS = 500;
    private readonly Stream mStream;
    private readonly SemaphoreSlim mRequestGate = new(1, 1);
    private readonly CancellationTokenSource mLifetimeCancellation = new();
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
    public async Task<YokiFrameFastChannelFrame> RequestAsync(YokiFrameFastChannelFrame request, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        using var requestCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            mLifetimeCancellation.Token);
        await mRequestGate.WaitAsync(requestCancellation.Token).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            await FastChannelFrameStream.WriteAsync(mStream, request, requestCancellation.Token).ConfigureAwait(false);
            return await FastChannelFrameStream.ReadAsync(mStream, requestCancellation.Token).ConfigureAwait(false);
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

        mLifetimeCancellation.Cancel();
        // 先关闭流，主动打断不响应的 ReadAsync；随后再等待请求 finally 释放闸门。
        await mStream.DisposeAsync().ConfigureAwait(false);
        var finalizeTask = FinalizeDisposeAsync();
        if (await Task.WhenAny(finalizeTask, Task.Delay(DISPOSE_WAIT_MS)).ConfigureAwait(false)
            != finalizeTask)
        {
            _ = ObserveDisposeCompletionAsync(finalizeTask);
            return;
        }

        await finalizeTask.ConfigureAwait(false);
    }

    /// <summary>
    /// 等待当前请求离开闸门后释放同步原语和生命周期取消源。
    /// </summary>
    private async Task FinalizeDisposeAsync()
    {
        await mRequestGate.WaitAsync().ConfigureAwait(false);
        try
        {
            // 仅用于确认没有请求仍在 finally 中访问闸门。
        }
        finally
        {
            mRequestGate.Release();
            mRequestGate.Dispose();
            mLifetimeCancellation.Dispose();
        }
    }

    /// <summary>
    /// 观察超时后继续运行的释放任务，避免把后台异常变成未观察任务。
    /// </summary>
    /// <param name="disposeTask">等待请求结束的释放任务。</param>
    private static async Task ObserveDisposeCompletionAsync(Task disposeTask)
    {
        try
        {
            await disposeTask.ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Dispose 已经关闭底层流；后台收口失败不应重新抛到已返回的调用方。
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
