using System.Net.Sockets;
using YokiFrame.Client.FastChannel;
using YokiFrame.Client.FastChannel.IO;
using YokiFrame.Protocol.FastChannel;

namespace YokiFrame.Client.Tests.FastChannel;

/// <summary>
/// 覆盖 Unix Domain Socket FastChannel 的实际握手、请求响应与短路径约束。
/// </summary>
public sealed class UnixDomainSocketFastChannelConnectionTests
{
    /// <summary>
    /// 验证 Client 能通过本机 Unix Domain Socket 完成身份握手，并读取 Host 的响应 frame。
    /// </summary>
    [Fact]
    public async Task ConnectAndRequestRoundtripThroughUnixDomainSocket()
    {
        var socketPath = CreateSocketPath();
        var endpoint = FastChannelEndpoint.CreateUnixDomainSocket(
            "godot-runtime",
            "session-a",
            42,
            socketPath);
        using var cancellationSource = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var serverTask = ServeOneRequestAsync(endpoint, cancellationSource.Token);

        await using var connection = await UnixDomainSocketFastChannelConnector.ConnectAsync(
            endpoint,
            TimeSpan.FromSeconds(2),
            cancellationSource.Token);
        var response = await connection.RequestAsync(
            new YokiFrameFastChannelFrame(YokiFrameFastChannelMessageKind.Command, 0, "{\"requestId\":\"request-a\"}"),
            cancellationSource.Token);

        Assert.Equal(YokiFrameFastChannelMessageKind.Response, response.MessageKind);
        Assert.Equal("{\"requestId\":\"request-a\",\"message\":\"pong\"}", response.PayloadJson);
        await serverTask;
    }

    /// <summary>
    /// 验证过长 socket 路径会在连接前被拒绝，避免触发不同平台不一致的 AF_UNIX bind 或 connect 错误。
    /// </summary>
    [Fact]
    public async Task ConnectRejectsOverlongSocketPath()
    {
        var socketPath = Path.Combine(Path.GetTempPath(), new string('x', 180) + ".sock");
        var endpoint = FastChannelEndpoint.CreateUnixDomainSocket(
            "godot-runtime",
            "session-a",
            42,
            socketPath);

        var exception = await Assert.ThrowsAsync<YokiFrame.Protocol.Results.YokiFrameProtocolException>(() =>
            UnixDomainSocketFastChannelConnector.ConnectAsync(
                endpoint,
                TimeSpan.FromSeconds(1),
                CancellationToken.None));

        Assert.Equal("FastChannelEndpointInvalid", exception.Error.Code);
    }

    /// <summary>
    /// 在后台创建单连接 Unix Domain Socket host，校验 Hello 后发送 HelloAck 和固定 Response。
    /// </summary>
    /// <param name="endpoint">当前测试使用的 endpoint。</param>
    /// <param name="cancellationToken">测试整体取消令牌。</param>
    /// <returns>server 停止后的异步任务。</returns>
    private static async Task ServeOneRequestAsync(FastChannelEndpoint endpoint, CancellationToken cancellationToken)
    {
        using var listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        try
        {
            listener.Bind(new UnixDomainSocketEndPoint(endpoint.Endpoint));
            listener.Listen(1);
            using var acceptedSocket = await listener.AcceptAsync(cancellationToken);
            await using var stream = new NetworkStream(acceptedSocket, false);
            var hello = await FastChannelFrameStream.ReadAsync(stream, cancellationToken);
            FastChannelHandshake.EnsureHelloMatchesEndpoint(hello, endpoint);
            await FastChannelFrameStream.WriteAsync(
                stream,
                FastChannelHandshake.CreateHelloAck(endpoint),
                cancellationToken);
            var request = await FastChannelFrameStream.ReadAsync(stream, cancellationToken);
            Assert.Equal(YokiFrameFastChannelMessageKind.Command, request.MessageKind);
            await FastChannelFrameStream.WriteAsync(
                stream,
                new YokiFrameFastChannelFrame(
                    YokiFrameFastChannelMessageKind.Response,
                    0,
                    "{\"requestId\":\"request-a\",\"message\":\"pong\"}"),
                cancellationToken);
        }
        finally
        {
            if (File.Exists(endpoint.Endpoint))
            {
                File.Delete(endpoint.Endpoint);
            }
        }
    }

    /// <summary>
    /// 创建适合 AF_UNIX 短路径限制的临时 socket 路径。
    /// </summary>
    /// <returns>当前测试唯一的绝对 socket 路径。</returns>
    private static string CreateSocketPath()
    {
        return Path.Combine(Path.GetTempPath(), "yf-" + Guid.NewGuid().ToString("N") + ".sock");
    }
}
