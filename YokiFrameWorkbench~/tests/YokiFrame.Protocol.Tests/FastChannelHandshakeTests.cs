using YokiFrame.Protocol.FastChannel;
using YokiFrame.Protocol.Results;

namespace YokiFrame.Protocol.Tests;

/// <summary>
/// 覆盖 FastChannel v1 在连接建立前的 endpoint 身份握手契约。
/// </summary>
public sealed class FastChannelHandshakeTests
{
    /// <summary>
    /// 验证 Hello 和 HelloAck 都携带 endpoint 的 engine、session 和 generation，且可经 frame roundtrip 读取。
    /// </summary>
    [Fact]
    public void HandshakeFramesRoundtripEndpointIdentity()
    {
        var endpoint = FastChannelEndpoint.CreateNamedPipe(
            "unity-editor",
            "session-a",
            42,
            "YokiFrame.FastChannel.unity-editor");

        var hello = FastChannelHandshake.ReadHello(FastChannelFrameCodec.Decode(FastChannelFrameCodec.Encode(
            FastChannelHandshake.CreateHello(endpoint))));
        var acknowledgement = FastChannelHandshake.ReadHelloAck(FastChannelFrameCodec.Decode(FastChannelFrameCodec.Encode(
            FastChannelHandshake.CreateHelloAck(endpoint))));

        Assert.Equal(endpoint.EngineId, hello.EngineId);
        Assert.Equal(endpoint.SessionId, hello.SessionId);
        Assert.Equal(endpoint.Generation, hello.Generation);
        Assert.Equal(endpoint.EngineId, acknowledgement.EngineId);
        Assert.Equal(endpoint.SessionId, acknowledgement.SessionId);
        Assert.Equal(endpoint.Generation, acknowledgement.Generation);
    }

    /// <summary>
    /// 验证确认帧与 registry endpoint 的 generation 不一致时必须拒绝旧连接。
    /// </summary>
    [Fact]
    public void HelloAckRejectsChangedGeneration()
    {
        var expectedEndpoint = FastChannelEndpoint.CreateNamedPipe(
            "unity-editor",
            "session-a",
            42,
            "YokiFrame.FastChannel.unity-editor");
        var staleEndpoint = FastChannelEndpoint.CreateNamedPipe(
            "unity-editor",
            "session-a",
            43,
            "YokiFrame.FastChannel.unity-editor");
        var acknowledgement = FastChannelHandshake.CreateHelloAck(staleEndpoint);

        var exception = Assert.Throws<YokiFrameProtocolException>(() =>
            FastChannelHandshake.EnsureHelloAckMatchesEndpoint(acknowledgement, expectedEndpoint));

        Assert.Equal("FastChannelHandshakeMismatch", exception.Error.Code);
    }
}
