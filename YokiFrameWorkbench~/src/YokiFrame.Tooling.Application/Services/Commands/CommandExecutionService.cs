using YokiFrame.Client;
using YokiFrame.Protocol.FileBridge;
using YokiFrame.Protocol.Results;
using YokiFrame.Tooling.Application.Engines;
using YokiFrame.Tooling.Application.Models;

namespace YokiFrame.Tooling.Application.Services;

/// <summary>
/// 为 Workbench、CLI 和 AI 自动化统一选择 FastChannel 或可靠 FileBridge。
/// </summary>
public sealed class CommandExecutionService
{
    private const string SYSTEM_KIT = "System";
    private const string FAST_CHANNEL_TRANSPORT = "fast-channel";
    private const string FILE_BRIDGE_TRANSPORT = "file-bridge";
    private readonly IYokiFrameClient mClient;
    private readonly EngineSelectionService mEngineSelectionService;

    /// <summary>
    /// 使用统一 Client 创建命令执行用例，入口层无需理解具体传输协议。
    /// </summary>
    /// <param name="client">统一 YokiFrame Client。</param>
    public CommandExecutionService(IYokiFrameClient client)
    {
        mClient = client;
        mEngineSelectionService = new EngineSelectionService(client);
    }

    /// <summary>
    /// 发送命令并返回实际传输、证据路径和 terminal response。
    /// 当前 endpoint 明确声明的 ReadOnly command 会尝试一次 FastChannel。
    /// </summary>
    /// <param name="requestedEngineId">显式 engine 标识；为空时只自动选择唯一在线 engine。</param>
    /// <param name="kit">目标 Kit；空值归一化为 System。</param>
    /// <param name="action">目标 action。</param>
    /// <param name="payloadJson">命令 payload JSON。</param>
    /// <param name="source">审计来源。</param>
    /// <param name="timeoutMs">命令超时毫秒数。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>实际传输与宿主响应。</returns>
    public async Task<CommandExecutionResult> ExecuteAsync(
        string requestedEngineId,
        string kit,
        string action,
        string payloadJson,
        string source,
        int timeoutMs,
        CancellationToken cancellationToken)
    {
        var selectedEngineId = mEngineSelectionService.Resolve(requestedEngineId, DateTimeOffset.UtcNow);
        var selectedKit = string.IsNullOrWhiteSpace(kit) ? SYSTEM_KIT : kit;
        if (mClient.CanSendFastChannelReadOnlyCommand(selectedEngineId, selectedKit, action))
        {
            var fastChannelResponse = await TrySendFastChannelCommandAsync(
                selectedEngineId,
                selectedKit,
                action,
                payloadJson,
                source,
                timeoutMs,
                cancellationToken).ConfigureAwait(false);
            if (fastChannelResponse != null)
            {
                return new CommandExecutionResult(
                    FAST_CHANNEL_TRANSPORT,
                    string.Empty,
                    string.Empty,
                    fastChannelResponse);
            }
        }

        var fileBridgeResult = await mClient.SendCommandAsync(
            selectedEngineId,
            selectedKit,
            action,
            payloadJson,
            source,
            timeoutMs,
            cancellationToken).ConfigureAwait(false);
        return new CommandExecutionResult(
            FILE_BRIDGE_TRANSPORT,
            fileBridgeResult.CommandPath,
            fileBridgeResult.ResponsePath,
            fileBridgeResult.Response);
    }

    /// <summary>
    /// 尝试通过 FastChannel 发送白名单只读命令；可恢复故障返回 null，由调用方只回退一次 FileBridge。
    /// </summary>
    /// <param name="engineId">已选择的 engine。</param>
    /// <param name="action">白名单 System action。</param>
    /// <param name="source">审计来源。</param>
    /// <param name="timeoutMs">命令超时毫秒数。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>成功响应；优化层不可用时返回 null。</returns>
    private async Task<CommandResponse?> TrySendFastChannelCommandAsync(
        string engineId,
        string kit,
        string action,
        string payloadJson,
        string source,
        int timeoutMs,
        CancellationToken cancellationToken)
    {
        try
        {
            return await mClient.SendFastChannelReadOnlyCommandAsync(
                engineId,
                kit,
                action,
                payloadJson,
                source,
                timeoutMs,
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (IsRecoverableFastChannelFailure(exception))
        {
            return null;
        }
    }

    /// <summary>
    /// 判断异常是否属于可选 FastChannel 的连接、协议或对象生命周期故障。
    /// </summary>
    /// <param name="exception">FastChannel 调用异常。</param>
    /// <returns>可以回退 FileBridge 时返回 true。</returns>
    private static bool IsRecoverableFastChannelFailure(Exception exception)
    {
        return exception is YokiFrameProtocolException
            or IOException
            or TimeoutException
            or ObjectDisposedException;
    }

}
