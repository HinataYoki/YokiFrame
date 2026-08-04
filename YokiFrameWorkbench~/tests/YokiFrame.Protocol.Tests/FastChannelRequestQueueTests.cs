using YokiFrame;

namespace YokiFrame.Protocol.Tests;

/// <summary>
/// 覆盖 FastChannel 后台 listener 与宿主主线程之间的有界请求队列。
/// </summary>
public sealed class FastChannelRequestQueueTests
{
    /// <summary>
    /// 验证主线程处理器能完成后台请求，并将 response 异步交回等待中的 listener。
    /// </summary>
    [Fact]
    public async Task ProcessPendingCompletesQueuedRequest()
    {
        using var queue = new YokiFrameFastChannelRequestQueue(2);
        var request = new YokiFrameFastChannelFrame(YokiFrameFastChannelMessageKind.Command, 0, "{\"requestId\":\"request-a\"}");

        var accepted = queue.TryEnqueue(request, out var responseTask);
        var processed = queue.ProcessPending(static frame => new YokiFrameFastChannelFrame(
            YokiFrameFastChannelMessageKind.Response,
            frame.Flags,
            "{\"ok\":true}"));
        var response = await responseTask;

        Assert.True(accepted);
        Assert.Equal(1, processed);
        Assert.Equal(YokiFrameFastChannelMessageKind.Response, response.MessageKind);
        Assert.Equal("{\"ok\":true}", response.PayloadJson);
    }

    /// <summary>
    /// 验证队列到达容量上限时拒绝新请求，不让后台 socket 读取无限堆积到主线程。
    /// </summary>
    [Fact]
    public void TryEnqueueRejectsRequestWhenQueueIsFull()
    {
        using var queue = new YokiFrameFastChannelRequestQueue(1);
        var request = new YokiFrameFastChannelFrame(YokiFrameFastChannelMessageKind.Command, 0, "{}");

        var firstAccepted = queue.TryEnqueue(request, out _);
        var secondAccepted = queue.TryEnqueue(request, out _);

        Assert.True(firstAccepted);
        Assert.False(secondAccepted);
        Assert.Equal(1, queue.PendingCount);
    }

    /// <summary>
    /// 验证 Host 停止时会取消尚未进入主线程的请求，避免 listener 永久等待 response。
    /// </summary>
    [Fact]
    public async Task StopCancelsQueuedRequest()
    {
        using var queue = new YokiFrameFastChannelRequestQueue(1);
        var request = new YokiFrameFastChannelFrame(YokiFrameFastChannelMessageKind.Command, 0, "{}");
        Assert.True(queue.TryEnqueue(request, out var responseTask));

        queue.Stop();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await responseTask);
        Assert.Equal(0, queue.PendingCount);
    }

    /// <summary>
    /// 验证连接令牌取消后，已经排队但尚未进入主线程的请求不会再执行 response factory。
    /// </summary>
    [Fact]
    public async Task ConnectionCancellationSkipsQueuedRequest()
    {
        using var queue = new YokiFrameFastChannelRequestQueue(1);
        using var cancellationSource = new CancellationTokenSource();
        var request = new YokiFrameFastChannelFrame(YokiFrameFastChannelMessageKind.Command, 0, "{}");
        Assert.True(queue.TryEnqueue(request, cancellationSource.Token, out var responseTask));

        cancellationSource.Cancel();
        var processed = queue.ProcessPending(static _ =>
            throw new InvalidOperationException("已取消请求不应进入主线程处理器。"));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await responseTask);
        Assert.Equal(0, processed);
        Assert.Equal(0, queue.PendingCount);
    }
}
