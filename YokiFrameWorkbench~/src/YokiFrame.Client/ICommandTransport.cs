using YokiFrame.Client.Commands;
using YokiFrame.Protocol.FileBridge;

namespace YokiFrame.Client;

/// <summary>
/// 提供可靠命令和可选快速通道命令的窄传输端口。
/// </summary>
public interface ICommandTransport
{
    /// <summary>按 requestId 查询 FileBridge 请求状态。</summary>
    CommandRequestStatus ReadCommandStatus(string engineId, string requestId)
    {
        throw new NotSupportedException("The command transport does not implement status inspection.");
    }

    /// <summary>通过 FileBridge 发送命令并等待 terminal response。</summary>
    Task<CommandSendResult> SendCommandAsync(
        string engineId,
        string kit,
        string action,
        string payloadJson,
        string source,
        int timeoutMs,
        CancellationToken cancellationToken);
}
