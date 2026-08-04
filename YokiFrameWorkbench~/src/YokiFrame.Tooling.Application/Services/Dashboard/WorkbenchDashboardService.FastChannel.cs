using YokiFrame.Protocol.Results;
using YokiFrame.Tooling.Application.Models;

namespace YokiFrame.Tooling.Application.Services;

/// <summary>
/// 承载 Workbench 命令的 FastChannel 优先选择和可靠 FileBridge 回退。
/// </summary>
public sealed partial class WorkbenchDashboardService
{
    private const string SYSTEM_KIT = "System";
    private const string WORKBENCH_SOURCE = "workbench";

    /// <summary>
    /// 发送指定 Kit/action 命令并转换为 Workbench 可显示的响应状态；只有首版允许的两个无副作用 System 命令会尝试 FastChannel。
    /// </summary>
    /// <param name="engineId">目标 engine。</param>
    /// <param name="kit">目标 Kit。</param>
    /// <param name="action">目标 action。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>命令响应状态。</returns>
    public async Task<WorkbenchCommandState> SendCommandAsync(
        string engineId,
        string kit,
        string action,
        CancellationToken cancellationToken)
    {
        return await SendCommandAsync(
            engineId,
            kit,
            action,
            "{}",
            cancellationToken,
            null).ConfigureAwait(false);
    }

    /// <summary>发送带显式 payload 的命令；只有 Registry 声明的只读命令才尝试 FastChannel。</summary>
    /// <param name="engineId">目标 engine。</param>
    /// <param name="kit">目标 Kit。</param>
    /// <param name="action">目标 action。</param>
    /// <param name="payloadJson">已由 Application 用例校验的 payload JSON。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>命令响应状态。</returns>
    public async Task<WorkbenchCommandState> SendCommandAsync(
        string engineId,
        string kit,
        string action,
        string payloadJson,
        CancellationToken cancellationToken)
    {
        return await SendCommandAsync(
            engineId,
            kit,
            action,
            payloadJson,
            cancellationToken,
            null).ConfigureAwait(false);
    }

    /// <summary>
    /// 发送命令并要求响应前后都属于指定宿主代次。
    /// </summary>
    /// <param name="engineId">目标 engine。</param>
    /// <param name="kit">目标 Kit。</param>
    /// <param name="action">目标 action。</param>
    /// <param name="payloadJson">已校验的 payload JSON。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <param name="expectedIdentity">发送前观察到的宿主身份。</param>
    /// <returns>带身份和证据的命令状态。</returns>
    public async Task<WorkbenchCommandState> SendCommandAsync(
        string engineId,
        string kit,
        string action,
        string payloadJson,
        CancellationToken cancellationToken,
        HostIdentity? expectedIdentity)
    {
        var selectedKit = string.IsNullOrWhiteSpace(kit) ? SYSTEM_KIT : kit;
        try
        {
            var result = await mCommandExecutionService.ExecuteWithIdentityAsync(
                engineId,
                selectedKit,
                action,
                payloadJson,
                WORKBENCH_SOURCE,
                COMMAND_TIMEOUT_MS,
                cancellationToken,
                expectedIdentity).ConfigureAwait(false);
            var response = result.Response;
            var succeeded = string.Equals(response.Status, "Success", StringComparison.OrdinalIgnoreCase);
            return new WorkbenchCommandState(
                selectedKit,
                action,
                succeeded,
                response.Status,
                response.ResultJson,
                succeeded
                    ? string.Empty
                    : string.IsNullOrWhiteSpace(response.ErrorMessage)
                        ? response.ErrorCode
                        : response.ErrorMessage,
                succeeded ? CommandOutcomeState.Succeeded : CommandOutcomeState.Failed,
                result.TargetIdentity,
                result.RequestId,
                result.Transport,
                result.Evidence);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (YokiFrameProtocolException exception)
        {
            return new WorkbenchCommandState(
                selectedKit,
                action,
                false,
                IsUnknownCommandOutcome(exception) ? "Unknown" : "Error",
                string.Empty,
                exception.Error.Message,
                IsUnknownCommandOutcome(exception)
                    ? CommandOutcomeState.Unknown
                    : CommandOutcomeState.Failed,
                expectedIdentity,
                string.Empty,
                string.Empty,
                CommandEvidence.Empty(exception.Error.Code));
        }
        catch (Exception exception)
        {
            return new WorkbenchCommandState(
                selectedKit,
                action,
                false,
                "Error",
                string.Empty,
                exception.Message,
                expectedIdentity,
                string.Empty,
                string.Empty,
                CommandEvidence.Empty(exception.GetType().Name));
        }
    }

    /// <summary>
    /// 判断本地等待期限结束是否使 Runtime 执行结果变得不可确定。
    /// </summary>
    /// <param name="exception">协议层异常。</param>
    /// <returns>没有 terminal response 的命令超时时返回 true。</returns>
    private static bool IsUnknownCommandOutcome(YokiFrameProtocolException exception)
    {
        return string.Equals(exception.Error.Code, "CommandTimeout", StringComparison.Ordinal)
            || string.Equals(exception.Error.Code, "FastChannelCommandTimeout", StringComparison.Ordinal);
    }

}
