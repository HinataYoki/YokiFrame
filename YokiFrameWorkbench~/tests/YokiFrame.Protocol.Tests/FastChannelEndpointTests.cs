using YokiFrame;
using YokiFrame.Protocol.FastChannel;

namespace YokiFrame.Protocol.Tests;

/// <summary>
/// 覆盖 FastChannel endpoint 的序列化和重连判断契约。
/// </summary>
public sealed class FastChannelEndpointTests
{
    /// <summary>
    /// 验证 Named Pipe endpoint 可以用 compact JSON roundtrip，并保留 FileBridge fallback。
    /// </summary>
    [Fact]
    public void NamedPipeEndpointRoundtripKeepsFallback()
    {
        var endpoint = FastChannelEndpoint.CreateNamedPipe(
            "unity-editor",
            "session-a",
            12,
            "YokiFrame.FastChannel.unity-editor");
        endpoint.ReadOnlyCommands.Add("System/list_commands");
        endpoint.ReadOnlyCommands.Add("FsmKit/get_state");

        var roundtrip = FastChannelEndpoint.FromJson(endpoint.ToJson());

        Assert.Equal(YokiFrameFastChannelContract.PROTOCOL_VERSION, roundtrip.ProtocolVersion);
        Assert.True(roundtrip.Enabled);
        Assert.Equal("unity-editor", roundtrip.EngineId);
        Assert.Equal("session-a", roundtrip.SessionId);
        Assert.Equal(12, roundtrip.Generation);
        Assert.Equal(FastChannelTransport.NamedPipe, roundtrip.Transport);
        Assert.Equal("YokiFrame.FastChannel.unity-editor", roundtrip.Endpoint);
        Assert.Equal(FastChannelEndpoint.FILEBRIDGE_FALLBACK, roundtrip.Fallback);
        Assert.True(roundtrip.SupportsReadOnlyCommand("System", "list_commands"));
        Assert.True(roundtrip.SupportsReadOnlyCommand("FsmKit", "get_state"));
        Assert.False(roundtrip.SupportsReadOnlyCommand("System", "refresh_snapshots"));
    }

    /// <summary>
    /// 验证 Workbench 可通过 sessionId / generation 变化判断旧 FastChannel 连接需要自动重连。
    /// </summary>
    [Fact]
    public void EndpointRequiresReconnectWhenGenerationChanges()
    {
        var endpoint = FastChannelEndpoint.CreateUnixDomainSocket(
            "godot-editor",
            "session-a",
            7,
            "/tmp/yokiframe-godot-editor.sock");

        Assert.False(endpoint.RequiresReconnect("session-a", 7));
        Assert.True(endpoint.RequiresReconnect("session-a", 8));
        Assert.True(endpoint.RequiresReconnect("session-b", 7));
    }

    /// <summary>
    /// 验证禁用 endpoint 不会触发重连，调用侧应直接使用 FileBridge fallback。
    /// </summary>
    [Fact]
    public void DisabledEndpointDoesNotRequireReconnect()
    {
        var endpoint = FastChannelEndpoint.Disabled("unity-editor", "session-a", 1);

        Assert.False(endpoint.Enabled);
        Assert.Equal(FastChannelTransport.None, endpoint.Transport);
        Assert.False(endpoint.RequiresReconnect("session-b", 2));
        Assert.Equal(FastChannelEndpoint.FILEBRIDGE_FALLBACK, endpoint.Fallback);
    }
}
