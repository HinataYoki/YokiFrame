using YokiFrame.Protocol.FastChannel;
using YokiFrame.Protocol.Results;

namespace YokiFrame.Client.FastChannel;

/// <summary>
/// 集中 FastChannel 各本机 transport 共用的 endpoint 前置校验、握手完成和标准错误构造。
/// </summary>
internal static class FastChannelConnectorUtilities
{
    /// <summary>
    /// 校验 endpoint 已启用、传输类型匹配且连接超时为正数；具体 endpoint 文本由各 transport 继续校验。
    /// </summary>
    /// <param name="endpoint">来自当前 engine registry 的 endpoint。</param>
    /// <param name="expectedTransport">当前 connector 支持的 transport 类型。</param>
    /// <param name="connectTimeout">调用侧要求的连接超时。</param>
    public static void ValidateEndpoint(
        FastChannelEndpoint endpoint,
        string expectedTransport,
        TimeSpan connectTimeout)
    {
        if (endpoint == null)
        {
            throw new ArgumentNullException(nameof(endpoint));
        }

        if (!endpoint.Enabled || !string.Equals(endpoint.Transport, expectedTransport, StringComparison.Ordinal))
        {
            throw CreateProtocolException(
                "FastChannelEndpointUnsupported",
                "FastChannel endpoint is not enabled for the requested local transport.",
                "Select an enabled endpoint for this platform or use FileBridge fallback.");
        }

        if (connectTimeout <= TimeSpan.Zero)
        {
            throw CreateProtocolException(
                "FastChannelEndpointInvalid",
                "FastChannel connection timeout must be positive.",
                "Refresh engine registry and provide a positive connection timeout.");
        }
    }

    /// <summary>
    /// 在已连接 Stream 上执行强制 Hello/HelloAck；失败时释放连接及其底层 transport。
    /// </summary>
    /// <param name="endpoint">建连时读取到的 registry endpoint。</param>
    /// <param name="stream">已经建立的双向本机传输流。</param>
    /// <param name="cancellationToken">握手取消令牌。</param>
    /// <returns>已完成身份校验的连接对象。</returns>
    public static async Task<FastChannelConnection> CompleteHandshakeAsync(
        FastChannelEndpoint endpoint,
        Stream stream,
        CancellationToken cancellationToken)
    {
        var connection = new FastChannelConnection(endpoint, stream);
        try
        {
            await connection.PerformHandshakeAsync(cancellationToken).ConfigureAwait(false);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// 创建 Client transport 可直接上报的标准 FastChannel 错误。
    /// </summary>
    /// <param name="code">稳定错误码。</param>
    /// <param name="message">面向调用侧的错误说明。</param>
    /// <param name="suggestion">恢复建议。</param>
    /// <returns>标准协议异常。</returns>
    public static YokiFrameProtocolException CreateProtocolException(string code, string message, string suggestion)
    {
        return new YokiFrameProtocolException(new YokiFrameError(code, message, suggestion));
    }
}
