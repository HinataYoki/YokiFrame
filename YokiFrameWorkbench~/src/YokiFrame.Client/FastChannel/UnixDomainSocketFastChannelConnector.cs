using System.Net.Sockets;
using YokiFrame.Protocol.FastChannel;
using YokiFrame.Protocol.Results;

namespace YokiFrame.Client.FastChannel;

/// <summary>
/// 建立 macOS、Linux 或支持 AF_UNIX 平台的 Unix Domain Socket FastChannel 传输，并完成强制身份握手。
/// </summary>
public static class UnixDomainSocketFastChannelConnector
{
    /// <summary>
    /// 预留安全余量后的最大 socket 路径字符数，避免超过常见 `sun_path` 上限。
    /// </summary>
    public const int MAX_SOCKET_PATH_LENGTH = 100;

    /// <summary>
    /// 连接 Unix Domain Socket 并完成 Hello/HelloAck 校验；连接或握手失败时会释放 Socket。
    /// </summary>
    /// <param name="endpoint">来自当前 engine registry 的启用 Unix socket endpoint。</param>
    /// <param name="connectTimeout">连接 server 的最长等待时间。</param>
    /// <param name="cancellationToken">外部取消令牌。</param>
    /// <returns>已完成身份校验的连接对象。</returns>
    public static async Task<FastChannelConnection> ConnectAsync(
        FastChannelEndpoint endpoint,
        TimeSpan connectTimeout,
        CancellationToken cancellationToken)
    {
        FastChannelConnectorUtilities.ValidateEndpoint(endpoint, FastChannelTransport.UnixDomainSocket, connectTimeout);
        ValidateSocketPath(endpoint.Endpoint);
        var stream = await ConnectSocketAsync(endpoint.Endpoint, connectTimeout, cancellationToken).ConfigureAwait(false);
        return await FastChannelConnectorUtilities.CompleteHandshakeAsync(endpoint, stream, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 使用超时和外部取消令牌连接 Unix Domain Socket，并将本机连接失败转换为标准协议错误。
    /// </summary>
    /// <param name="socketPath">已校验的绝对 socket 路径。</param>
    /// <param name="connectTimeout">连接超时时间。</param>
    /// <param name="cancellationToken">外部取消令牌。</param>
    /// <returns>拥有 Socket 所有权的双向 NetworkStream。</returns>
    private static async Task<NetworkStream> ConnectSocketAsync(
        string socketPath,
        TimeSpan connectTimeout,
        CancellationToken cancellationToken)
    {
        var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(connectTimeout);
        try
        {
            await socket.ConnectAsync(new UnixDomainSocketEndPoint(socketPath), timeoutSource.Token).ConfigureAwait(false);
            return new NetworkStream(socket, true);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            socket.Dispose();
            throw FastChannelConnectorUtilities.CreateProtocolException(
                "FastChannelConnectTimeout",
                "FastChannel Unix Domain Socket did not accept a connection before the timeout.",
                "Refresh engine registry, then retry or use FileBridge fallback.");
        }
        catch (OperationCanceledException)
        {
            socket.Dispose();
            throw;
        }
        catch (YokiFrameProtocolException)
        {
            socket.Dispose();
            throw;
        }
        catch (Exception exception)
        {
            socket.Dispose();
            throw FastChannelConnectorUtilities.CreateProtocolException(
                "FastChannelConnectFailed",
                "FastChannel Unix Domain Socket connection failed: " + exception.Message,
                "Refresh engine registry, then retry or use FileBridge fallback.");
        }
    }

    /// <summary>
    /// 拒绝相对、过长或空白 socket 路径；Client 不删除路径，陈旧 socket 的清理由 Host 负责。
    /// </summary>
    /// <param name="socketPath">registry 声明的 socket 路径。</param>
    private static void ValidateSocketPath(string socketPath)
    {
        if (string.IsNullOrWhiteSpace(socketPath)
            || !Path.IsPathFullyQualified(socketPath)
            || socketPath.Length > MAX_SOCKET_PATH_LENGTH)
        {
            throw FastChannelConnectorUtilities.CreateProtocolException(
                "FastChannelEndpointInvalid",
                "FastChannel Unix Domain Socket path must be an absolute path within the supported length limit.",
                "Publish a short socket path from the engine adapter and refresh engine registry.");
        }
    }
}
