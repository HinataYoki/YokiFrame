using System.IO.Pipes;
using YokiFrame;
using YokiFrame.Protocol.FastChannel;

namespace YokiFrame.Client.FastChannel;

/// <summary>
/// 建立 Windows Named Pipe FastChannel 传输，并在返回连接前完成强制身份握手。
/// </summary>
public static class NamedPipeFastChannelConnector
{
    /// <summary>
    /// 连接 Windows Named Pipe 并完成 Hello/HelloAck 校验；连接或握手失败时会释放 Pipe。
    /// </summary>
    /// <param name="endpoint">来自当前 engine registry 的启用 Named Pipe endpoint。</param>
    /// <param name="connectTimeout">连接 server 的最长等待时间。</param>
    /// <param name="cancellationToken">外部取消令牌。</param>
    /// <returns>已完成身份校验的连接对象。</returns>
    public static async Task<FastChannelConnection> ConnectAsync(
        FastChannelEndpoint endpoint,
        TimeSpan connectTimeout,
        CancellationToken cancellationToken)
    {
        FastChannelConnectorUtilities.ValidateEndpoint(endpoint, FastChannelTransport.NamedPipe, connectTimeout);
        ValidatePipeName(endpoint.Endpoint);
        var pipe = await ConnectPipeAsync(endpoint, connectTimeout, cancellationToken).ConfigureAwait(false);
        return await FastChannelConnectorUtilities.CompleteHandshakeAsync(endpoint, pipe, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 创建当前用户限定的异步 Pipe 并把连接超时转换为稳定协议错误。
    /// </summary>
    /// <param name="endpoint">已通过基础校验的 endpoint。</param>
    /// <param name="connectTimeout">连接超时时间。</param>
    /// <param name="cancellationToken">外部取消令牌。</param>
    /// <returns>已连接的 Pipe。</returns>
    private static async Task<NamedPipeClientStream> ConnectPipeAsync(
        FastChannelEndpoint endpoint,
        TimeSpan connectTimeout,
        CancellationToken cancellationToken)
    {
        var pipe = new NamedPipeClientStream(
            ".",
            endpoint.Endpoint,
            PipeDirection.InOut,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(connectTimeout);
        try
        {
            await pipe.ConnectAsync(timeoutSource.Token).ConfigureAwait(false);
            return pipe;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            pipe.Dispose();
            throw FastChannelConnectorUtilities.CreateProtocolException(
                "FastChannelConnectTimeout",
                "FastChannel Named Pipe did not accept a connection before the timeout.",
                "Refresh engine registry, then retry or use FileBridge fallback.");
        }
        catch (OperationCanceledException)
        {
            pipe.Dispose();
            throw;
        }
        catch (YokiFrame.Protocol.Results.YokiFrameProtocolException)
        {
            pipe.Dispose();
            throw;
        }
        catch (Exception exception)
        {
            pipe.Dispose();
            throw FastChannelConnectorUtilities.CreateProtocolException(
                "FastChannelConnectFailed",
                "FastChannel Named Pipe connection failed: " + exception.Message,
                "Refresh engine registry, then retry or use FileBridge fallback.");
        }
    }

    /// <summary>
    /// 校验 Named Pipe 名称满足共享 SafeId 白名单，避免 registry 值进入本机 Pipe 路径。
    /// </summary>
    /// <param name="pipeName">registry 声明的 Pipe 名称。</param>
    private static void ValidatePipeName(string pipeName)
    {
        if (!YokiFrameSafeIdContract.IsSafeId(pipeName))
        {
            throw FastChannelConnectorUtilities.CreateProtocolException(
                "FastChannelEndpointInvalid",
                "FastChannel Named Pipe name is invalid.",
                "Refresh engine registry and publish a SafeId-compatible Pipe name.");
        }
    }
}
