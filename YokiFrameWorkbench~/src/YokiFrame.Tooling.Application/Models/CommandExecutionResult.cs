using YokiFrame.Protocol.FileBridge;
using YokiFrame.Protocol.Results;

namespace YokiFrame.Tooling.Application.Models;

/// <summary>
/// 描述共享命令用例最终选择的传输和 terminal response，供 Workbench 与 CLI 分别投影。
/// </summary>
public sealed class CommandExecutionResult
{
    /// <summary>
    /// 创建一次命令执行结果。
    /// </summary>
    /// <param name="transport">实际完成请求的传输标识。</param>
    /// <param name="commandPath">FileBridge command 证据路径；FastChannel 成功时为空。</param>
    /// <param name="responsePath">FileBridge response 证据路径；FastChannel 成功时为空。</param>
    /// <param name="response">宿主返回的 terminal response。</param>
    public CommandExecutionResult(
        string transport,
        string commandPath,
        string responsePath,
        CommandResponse response)
        : this(
            transport,
            commandPath,
            responsePath,
            response,
            null,
            response.RequestId,
            string.Equals(transport, "file-bridge", StringComparison.OrdinalIgnoreCase)
                ? CommandEvidence.FileBacked(commandPath, responsePath)
                : CommandEvidence.Ephemeral("FastChannel returned a terminal response without FileBridge file evidence."))
    {
    }

    /// <summary>
    /// 创建带目标身份、请求标识和统一证据模型的命令结果。
    /// </summary>
    /// <param name="transport">实际完成请求的传输标识。</param>
    /// <param name="commandPath">FileBridge command 证据路径。</param>
    /// <param name="responsePath">FileBridge response 证据路径。</param>
    /// <param name="response">宿主返回的 terminal response。</param>
    /// <param name="targetIdentity">发送时确认的宿主身份。</param>
    /// <param name="requestId">本次请求标识。</param>
    /// <param name="evidence">统一 transport-specific 证据。</param>
    public CommandExecutionResult(
        string transport,
        string commandPath,
        string responsePath,
        CommandResponse response,
        HostIdentity? targetIdentity,
        string requestId,
        CommandEvidence evidence)
    {
        Transport = transport;
        CommandPath = commandPath;
        ResponsePath = responsePath;
        Response = response;
        TargetIdentity = targetIdentity;
        RequestId = requestId ?? string.Empty;
        Evidence = evidence;
        Outcome = string.Equals(response.Status, "Success", StringComparison.OrdinalIgnoreCase)
            ? CommandOutcomeState.Succeeded
            : CommandOutcomeState.Failed;
    }

    /// <summary>
    /// 获取实际完成请求的传输标识。
    /// </summary>
    public string Transport { get; }

    /// <summary>
    /// 获取 FileBridge command 证据路径；FastChannel 成功时为空。
    /// </summary>
    public string CommandPath { get; }

    /// <summary>
    /// 获取 FileBridge response 证据路径；FastChannel 成功时为空。
    /// </summary>
    public string ResponsePath { get; }

    /// <summary>
    /// 获取宿主返回的 terminal response。
    /// </summary>
    public CommandResponse Response { get; }

    /// <summary>获取发送时确认的宿主身份；旧调用方未提供时为空。</summary>
    public HostIdentity? TargetIdentity { get; }

    /// <summary>获取本次请求标识。</summary>
    public string RequestId { get; }

    /// <summary>获取 transport-specific 证据。</summary>
    public CommandEvidence Evidence { get; }

    /// <summary>
    /// 获取 terminal response 表示的命令结果状态。
    /// </summary>
    public CommandOutcomeState Outcome { get; }
}
