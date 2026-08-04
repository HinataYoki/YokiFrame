using YokiFrame.Protocol.FileBridge;

namespace YokiFrame.Client;

/// <summary>
/// 提供可选的 FastChannel 只读命令能力；不支持该优化的传输无需伪造默认实现。
/// </summary>
public interface IFastChannelCommandTransport
{
    /// <summary>判断当前 registry 是否声明只读 FastChannel 能力。</summary>
    bool CanSendFastChannelReadOnlyCommand(string engineId, string kit, string action)
    {
        return false;
    }

    /// <summary>失效指定 engine 的 FastChannel 连接缓存。</summary>
    Task InvalidateFastChannelConnectionsAsync(string engineId)
    {
        return Task.CompletedTask;
    }

    /// <summary>通过 FastChannel 发送通用只读命令。</summary>
    /// <param name="engineId">目标 engine。</param>
    /// <param name="kit">目标 Kit。</param>
    /// <param name="action">目标只读 action。</param>
    /// <param name="payloadJson">查询 payload JSON。</param>
    /// <param name="source">审计来源。</param>
    /// <param name="timeoutMs">本地 FastChannel 操作期限；线上信封会单独遵守协议超时范围。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>Host 返回的 terminal response。</returns>
    Task<CommandResponse> SendFastChannelReadOnlyCommandAsync(
        string engineId,
        string kit,
        string action,
        string payloadJson,
        string source,
        int timeoutMs,
        CancellationToken cancellationToken)
    {
        throw new NotSupportedException("The FastChannel capability is unavailable.");
    }

    /// <summary>通过 FastChannel 发送只读 System 命令。</summary>
    /// <param name="engineId">目标 engine。</param>
    /// <param name="action">目标只读 System action。</param>
    /// <param name="source">审计来源。</param>
    /// <param name="timeoutMs">本地 FastChannel 操作期限；线上信封会单独遵守协议超时范围。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>Host 返回的 terminal response。</returns>
    Task<CommandResponse> SendFastChannelReadOnlySystemCommandAsync(
        string engineId,
        string action,
        string source,
        int timeoutMs,
        CancellationToken cancellationToken);
}
