using System.Text.Json;
using System.Text.Json.Serialization;
using YokiFrame;
using YokiFrame.Protocol.Common;
using YokiFrame.Protocol.Results;

namespace YokiFrame.Protocol.FastChannel;

/// <summary>
/// 表示 FastChannel Hello 与 HelloAck 共用的 endpoint 身份；三项字段必须同时匹配才可复用连接。
/// </summary>
public sealed class FastChannelSessionIdentity
{
    /// <summary>
    /// 获取或设置目标 engine 标识。
    /// </summary>
    [JsonPropertyName("engineId")]
    public string EngineId { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置宿主进程会话标识。
    /// </summary>
    [JsonPropertyName("sessionId")]
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置宿主当前 generation。
    /// </summary>
    [JsonPropertyName("generation")]
    public long Generation { get; set; }
}

/// <summary>
/// 负责 FastChannel Hello/HelloAck 的 payload 编解码和 endpoint 身份匹配，不管理具体传输连接。
/// </summary>
public static class FastChannelHandshake
{
    /// <summary>
    /// 根据 registry endpoint 创建 Client 发起的 Hello frame。
    /// </summary>
    /// <param name="endpoint">Client 本轮读取到的启用 endpoint。</param>
    /// <returns>携带 endpoint 身份的 Hello frame。</returns>
    public static YokiFrameFastChannelFrame CreateHello(FastChannelEndpoint endpoint)
    {
        return CreateFrame(YokiFrameFastChannelMessageKind.Hello, endpoint);
    }

    /// <summary>
    /// 根据当前 Host endpoint 创建成功确认用的 HelloAck frame。
    /// </summary>
    /// <param name="endpoint">Host 当前启用的 endpoint。</param>
    /// <returns>携带 endpoint 身份的 HelloAck frame。</returns>
    public static YokiFrameFastChannelFrame CreateHelloAck(FastChannelEndpoint endpoint)
    {
        return CreateFrame(YokiFrameFastChannelMessageKind.HelloAck, endpoint);
    }

    /// <summary>
    /// 读取并校验 Client Hello 的身份 payload。
    /// </summary>
    /// <param name="frame">已通过 framing 校验的 Hello frame。</param>
    /// <returns>已校验的 Client endpoint 身份。</returns>
    public static FastChannelSessionIdentity ReadHello(YokiFrameFastChannelFrame frame)
    {
        return ReadFrameIdentity(frame, YokiFrameFastChannelMessageKind.Hello);
    }

    /// <summary>
    /// 读取并校验 Host HelloAck 的身份 payload。
    /// </summary>
    /// <param name="frame">已通过 framing 校验的 HelloAck frame。</param>
    /// <returns>已校验的 Host endpoint 身份。</returns>
    public static FastChannelSessionIdentity ReadHelloAck(YokiFrameFastChannelFrame frame)
    {
        return ReadFrameIdentity(frame, YokiFrameFastChannelMessageKind.HelloAck);
    }

    /// <summary>
    /// 确认 Host HelloAck 仍对应 Client 读取的 registry endpoint，防止 session 或 generation 切换后误用旧连接。
    /// </summary>
    /// <param name="acknowledgement">Host 返回的 HelloAck frame。</param>
    /// <param name="expectedEndpoint">Client 建连前读取到的 endpoint。</param>
    public static void EnsureHelloAckMatchesEndpoint(
        YokiFrameFastChannelFrame acknowledgement,
        FastChannelEndpoint expectedEndpoint)
    {
        var identity = ReadHelloAck(acknowledgement);
        EnsureIdentityMatchesEndpoint(identity, expectedEndpoint);
    }

    /// <summary>
    /// 确认 Client Hello 与当前 Host endpoint 一致，避免旧 Client 在 lifecycle 重建后继续发送命令。
    /// </summary>
    /// <param name="hello">Client 发起的 Hello frame。</param>
    /// <param name="expectedEndpoint">Host 当前启用的 endpoint。</param>
    public static void EnsureHelloMatchesEndpoint(YokiFrameFastChannelFrame hello, FastChannelEndpoint expectedEndpoint)
    {
        var identity = ReadHello(hello);
        EnsureIdentityMatchesEndpoint(identity, expectedEndpoint);
    }

    /// <summary>
    /// 创建指定类型的握手 frame，并统一校验 endpoint 是否可用于 FastChannel。
    /// </summary>
    /// <param name="messageKind">Hello 或 HelloAck 消息类型。</param>
    /// <param name="endpoint">当前 endpoint 描述。</param>
    /// <returns>包含 compact JSON payload 的握手 frame。</returns>
    private static YokiFrameFastChannelFrame CreateFrame(
        YokiFrameFastChannelMessageKind messageKind,
        FastChannelEndpoint endpoint)
    {
        var identity = CreateIdentity(endpoint);
        var payloadJson = JsonSerializer.Serialize(identity, YokiFrameProtocolJsonContext.Default.FastChannelSessionIdentity);
        return new YokiFrameFastChannelFrame(messageKind, 0, payloadJson);
    }

    /// <summary>
    /// 从指定消息类型的 frame 读取 endpoint 身份，并将损坏 JSON 转换为标准协议错误。
    /// </summary>
    /// <param name="frame">已完成 framing 解码的消息。</param>
    /// <param name="expectedKind">当前调用允许的握手消息类型。</param>
    /// <returns>已校验的 endpoint 身份。</returns>
    private static FastChannelSessionIdentity ReadFrameIdentity(
        YokiFrameFastChannelFrame frame,
        YokiFrameFastChannelMessageKind expectedKind)
    {
        if (frame == null)
        {
            throw new ArgumentNullException(nameof(frame));
        }

        if (frame.MessageKind != expectedKind)
        {
            throw CreateProtocolException(
                "FastChannelHandshakeKindMismatch",
                "FastChannel handshake frame has an unexpected message kind.",
                "Close the connection and restart the FastChannel handshake.");
        }

        try
        {
            var identity = JsonSerializer.Deserialize(
                frame.PayloadJson,
                YokiFrameProtocolJsonContext.Default.FastChannelSessionIdentity);
            if (identity == null)
            {
                throw CreateProtocolException(
                    "FastChannelHandshakeInvalidJson",
                    "FastChannel handshake payload is empty.",
                    "Reconnect to a compatible YokiFrame host.");
            }

            EnsureIdentity(identity);
            return identity;
        }
        catch (JsonException exception)
        {
            throw CreateProtocolException(
                "FastChannelHandshakeInvalidJson",
                "FastChannel handshake payload is not valid JSON: " + exception.Message,
                "Reconnect to a compatible YokiFrame host.");
        }
    }

    /// <summary>
    /// 从 endpoint 提取身份字段，并拒绝 disabled、旧协议或损坏 endpoint 描述。
    /// </summary>
    /// <param name="endpoint">待使用的 registry endpoint。</param>
    /// <returns>经过格式校验的身份对象。</returns>
    private static FastChannelSessionIdentity CreateIdentity(FastChannelEndpoint endpoint)
    {
        if (endpoint == null)
        {
            throw new ArgumentNullException(nameof(endpoint));
        }

        if (!endpoint.Enabled)
        {
            throw CreateProtocolException(
                "FastChannelEndpointDisabled",
                "FastChannel endpoint is disabled and must use FileBridge fallback.",
                "Refresh engine registry or use FileBridge directly.");
        }

        if (endpoint.ProtocolVersion != YokiFrameFastChannelContract.PROTOCOL_VERSION)
        {
            throw CreateProtocolException(
                "FastChannelEndpointVersionMismatch",
                "FastChannel endpoint protocolVersion does not match the framing contract.",
                "Refresh engine registry and update the incompatible client or host.");
        }

        var identity = new FastChannelSessionIdentity
        {
            EngineId = endpoint.EngineId,
            SessionId = endpoint.SessionId,
            Generation = endpoint.Generation
        };
        EnsureIdentity(identity);
        return identity;
    }

    /// <summary>
    /// 校验握手身份和预期 endpoint 的三项字段完全一致。
    /// </summary>
    /// <param name="identity">从握手 payload 读取到的身份。</param>
    /// <param name="expectedEndpoint">当前 registry 或 Host endpoint。</param>
    private static void EnsureIdentityMatchesEndpoint(
        FastChannelSessionIdentity identity,
        FastChannelEndpoint expectedEndpoint)
    {
        var expectedIdentity = CreateIdentity(expectedEndpoint);
        if (string.Equals(identity.EngineId, expectedIdentity.EngineId, StringComparison.Ordinal)
            && string.Equals(identity.SessionId, expectedIdentity.SessionId, StringComparison.Ordinal)
            && identity.Generation == expectedIdentity.Generation)
        {
            return;
        }

        throw CreateProtocolException(
            "FastChannelHandshakeMismatch",
            "FastChannel handshake identity no longer matches the active engine session.",
            "Discard the connection, refresh engine registry, and reconnect or use FileBridge fallback.");
    }

    /// <summary>
    /// 校验 engine、session 与 generation 可安全表示当前 FastChannel 生命周期。
    /// </summary>
    /// <param name="identity">待校验的 endpoint 身份。</param>
    private static void EnsureIdentity(FastChannelSessionIdentity identity)
    {
        if (!YokiFrameSafeIdContract.IsSafeId(identity.EngineId)
            || !YokiFrameSafeIdContract.IsSafeId(identity.SessionId)
            || identity.Generation <= 0L)
        {
            throw CreateProtocolException(
                "FastChannelHandshakeInvalidIdentity",
                "FastChannel handshake identity contains an invalid engine, session, or generation.",
                "Refresh engine registry and reconnect after the host publishes a valid endpoint.");
        }
    }

    /// <summary>
    /// 创建可由 Client、Application 和 CLI 统一处理的 FastChannel 协议异常。
    /// </summary>
    /// <param name="code">稳定错误码。</param>
    /// <param name="message">面向调用侧的错误说明。</param>
    /// <param name="suggestion">恢复建议。</param>
    /// <returns>标准协议异常。</returns>
    private static YokiFrameProtocolException CreateProtocolException(string code, string message, string suggestion)
    {
        return new YokiFrameProtocolException(new YokiFrameError(code, message, suggestion));
    }
}