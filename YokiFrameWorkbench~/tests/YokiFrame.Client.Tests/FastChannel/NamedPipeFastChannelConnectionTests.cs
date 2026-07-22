using System.IO.Pipes;
using YokiFrame.Client.FastChannel;
using YokiFrame.Client.FastChannel.IO;
using YokiFrame.Protocol.FastChannel;
using YokiFrame.Protocol.Results;

namespace YokiFrame.Client.Tests.FastChannel;

/// <summary>
/// 覆盖 Windows Named Pipe FastChannel 的实际握手、分段读取和请求响应。
/// </summary>
public sealed class NamedPipeFastChannelConnectionTests
{
    /// <summary>
    /// 验证 Client 能连接同一用户的 Named Pipe，完成身份握手并读取 server 的响应 frame。
    /// </summary>
    [Fact]
    public async Task ConnectAndRequestRoundtripThroughNamedPipe()
    {
        var endpoint = FastChannelEndpoint.CreateNamedPipe(
            "unity-editor",
            "session-a",
            42,
            CreatePipeName());
        using var cancellationSource = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var serverTask = ServeOneRequestAsync(endpoint, false, cancellationSource.Token);

        await using var connection = await NamedPipeFastChannelConnector.ConnectAsync(
            endpoint,
            TimeSpan.FromSeconds(2),
            cancellationSource.Token);
        var response = await connection.RequestAsync(
            new FastChannelFrame(YokiFrameFastChannelMessageKind.Command, 0, "{\"requestId\":\"request-a\"}"),
            cancellationSource.Token);

        Assert.Equal(YokiFrameFastChannelMessageKind.Response, response.Kind);
        Assert.Equal("{\"requestId\":\"request-a\",\"message\":\"pong\"}", response.PayloadJson);
        await serverTask;
    }

    /// <summary>
    /// 验证 Host 返回不同 generation 的 HelloAck 时 Client 拒绝连接，避免 lifecycle 重建后复用旧通道。
    /// </summary>
    [Fact]
    public async Task ConnectRejectsMismatchedHelloAck()
    {
        var endpoint = FastChannelEndpoint.CreateNamedPipe(
            "unity-editor",
            "session-a",
            42,
            CreatePipeName());
        using var cancellationSource = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var serverTask = ServeOneRequestAsync(endpoint, true, cancellationSource.Token);

        var exception = await Assert.ThrowsAsync<YokiFrameProtocolException>(() =>
            NamedPipeFastChannelConnector.ConnectAsync(
                endpoint,
                TimeSpan.FromSeconds(2),
                cancellationSource.Token));

        Assert.Equal("FastChannelHandshakeMismatch", exception.Error.Code);
        await serverTask;
    }

    /// <summary>
    /// 验证 stream reader 能在底层多次 partial read 后组合出一个完整 FastChannel frame。
    /// </summary>
    [Fact]
    public async Task FrameStreamReaderAssemblesPartialReads()
    {
        var expected = new FastChannelFrame(YokiFrameFastChannelMessageKind.Hello, 3, "{\"engineId\":\"unity-editor\"}");
        await using var stream = new PartialReadStream(FastChannelFrameCodec.Encode(expected), 3);

        var actual = await FastChannelFrameStream.ReadAsync(stream, CancellationToken.None);

        Assert.Equal(expected.Kind, actual.Kind);
        Assert.Equal(expected.Flags, actual.Flags);
        Assert.Equal(expected.PayloadJson, actual.PayloadJson);
    }

    /// <summary>
    /// 在后台创建单连接 Named Pipe host，校验 Hello 后发送 HelloAck 和固定 Response。
    /// </summary>
    /// <param name="endpoint">当前测试使用的 endpoint。</param>
    /// <param name="sendMismatchedAcknowledgement">是否故意发送 generation 不一致的确认。</param>
    /// <param name="cancellationToken">测试整体取消令牌。</param>
    /// <returns>server 停止后的异步任务。</returns>
    private static async Task ServeOneRequestAsync(
        FastChannelEndpoint endpoint,
        bool sendMismatchedAcknowledgement,
        CancellationToken cancellationToken)
    {
        await using var server = new NamedPipeServerStream(
            endpoint.Endpoint,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        await server.WaitForConnectionAsync(cancellationToken);
        var hello = await FastChannelFrameStream.ReadAsync(server, cancellationToken);
        FastChannelHandshake.EnsureHelloMatchesEndpoint(hello, endpoint);

        var acknowledgementEndpoint = sendMismatchedAcknowledgement
            ? FastChannelEndpoint.CreateNamedPipe(endpoint.EngineId, endpoint.SessionId, endpoint.Generation + 1, endpoint.Endpoint)
            : endpoint;
        await FastChannelFrameStream.WriteAsync(
            server,
            FastChannelHandshake.CreateHelloAck(acknowledgementEndpoint),
            cancellationToken);
        if (sendMismatchedAcknowledgement)
        {
            return;
        }

        var request = await FastChannelFrameStream.ReadAsync(server, cancellationToken);
        Assert.Equal(YokiFrameFastChannelMessageKind.Command, request.Kind);
        await FastChannelFrameStream.WriteAsync(
            server,
            new FastChannelFrame(
                YokiFrameFastChannelMessageKind.Response,
                0,
                "{\"requestId\":\"request-a\",\"message\":\"pong\"}"),
            cancellationToken);
    }

    /// <summary>
    /// 创建不与并行测试冲突的短 Named Pipe 名称。
    /// </summary>
    /// <returns>当前测试唯一的 pipe 名称。</returns>
    private static string CreatePipeName()
    {
        return "YokiFrame.FastChannel.Tests." + Guid.NewGuid().ToString("N");
    }

    /// <summary>
    /// 用受限单次读取大小模拟 Pipe 或 Socket 的 partial read 行为。
    /// </summary>
    private sealed class PartialReadStream : Stream
    {
        private readonly MemoryStream mInnerStream;
        private readonly int mMaxReadBytes;

        /// <summary>
        /// 使用固定最大读取片段创建只读内存流。
        /// </summary>
        /// <param name="bytes">需要分段输出的完整字节。</param>
        /// <param name="maxReadBytes">每次最多返回的字节数。</param>
        public PartialReadStream(byte[] bytes, int maxReadBytes)
        {
            mInnerStream = new MemoryStream(bytes, false);
            mMaxReadBytes = maxReadBytes;
        }

        /// <summary>
        /// 获取流是否支持读取。
        /// </summary>
        public override bool CanRead => true;

        /// <summary>
        /// 获取流是否支持定位。
        /// </summary>
        public override bool CanSeek => false;

        /// <summary>
        /// 获取流是否支持写入。
        /// </summary>
        public override bool CanWrite => false;

        /// <summary>
        /// 获取底层内存流长度。
        /// </summary>
        public override long Length => mInnerStream.Length;

        /// <summary>
        /// 获取或设置当前位置；测试流不允许外部定位。
        /// </summary>
        public override long Position
        {
            get => mInnerStream.Position;
            set => throw new NotSupportedException();
        }

        /// <summary>
        /// 刷新只读测试流；该操作无需写入任何数据。
        /// </summary>
        public override void Flush()
        {
        }

        /// <summary>
        /// 以受限片段同步读取数据。
        /// </summary>
        /// <param name="buffer">目标缓冲区。</param>
        /// <param name="offset">目标写入偏移。</param>
        /// <param name="count">调用侧请求的字节数。</param>
        /// <returns>实际返回的受限字节数。</returns>
        public override int Read(byte[] buffer, int offset, int count)
        {
            return mInnerStream.Read(buffer, offset, Math.Min(count, mMaxReadBytes));
        }

        /// <summary>
        /// 以受限片段异步读取数据，模拟真实传输不能保证一次读满缓冲区。
        /// </summary>
        /// <param name="buffer">目标缓冲区。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>实际读取字节数。</returns>
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            return mInnerStream.ReadAsync(buffer[..Math.Min(buffer.Length, mMaxReadBytes)], cancellationToken);
        }

        /// <summary>
        /// 拒绝测试流的定位操作。
        /// </summary>
        /// <param name="offset">目标偏移。</param>
        /// <param name="origin">偏移基准。</param>
        /// <returns>不会返回，始终抛出不支持异常。</returns>
        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// 拒绝调整只读测试流长度。
        /// </summary>
        /// <param name="value">目标长度。</param>
        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// 拒绝向只读测试流写入数据。
        /// </summary>
        /// <param name="buffer">待写入缓冲区。</param>
        /// <param name="offset">源读取偏移。</param>
        /// <param name="count">待写入字节数。</param>
        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// 释放底层内存流。
        /// </summary>
        /// <param name="disposing">是否正在释放托管资源。</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                mInnerStream.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
