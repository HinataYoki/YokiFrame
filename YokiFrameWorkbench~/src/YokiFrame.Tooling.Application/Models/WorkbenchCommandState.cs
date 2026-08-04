using YokiFrame.Protocol.Results;

namespace YokiFrame.Tooling.Application.Models;

/// <summary>
/// 描述 Workbench 发送 System 命令后的响应状态。
/// </summary>
public sealed class WorkbenchCommandState
{
    /// <summary>
    /// 创建命令响应状态。
    /// </summary>
    /// <param name="action">命令 action。</param>
    /// <param name="ok">命令是否成功得到响应。</param>
    /// <param name="status">Runtime response 状态。</param>
    /// <param name="resultJson">业务结果 JSON。</param>
    /// <param name="errorMessage">失败说明。</param>
    public WorkbenchCommandState(string action, bool ok, string status, string resultJson, string errorMessage)
        : this("System", action, ok, status, resultJson, errorMessage)
    {
    }

    /// <summary>
    /// 创建命令响应状态。
    /// </summary>
    /// <param name="kit">命令 Kit。</param>
    /// <param name="action">命令 action。</param>
    /// <param name="ok">命令是否成功得到响应。</param>
    /// <param name="status">Runtime response 状态。</param>
    /// <param name="resultJson">业务结果 JSON。</param>
    /// <param name="errorMessage">失败说明。</param>
    public WorkbenchCommandState(string kit, string action, bool ok, string status, string resultJson, string errorMessage)
        : this(
            kit,
            action,
            ok,
            status,
            resultJson,
            errorMessage,
            null,
            string.Empty,
            string.Empty,
            CommandEvidence.Empty("Command result was created by a legacy caller without transport evidence."))
    {
    }

    /// <summary>
    /// 创建带宿主身份、请求标识和证据的 Workbench 命令状态。
    /// </summary>
    /// <param name="kit">命令 Kit。</param>
    /// <param name="action">命令 action。</param>
    /// <param name="ok">命令是否成功得到响应。</param>
    /// <param name="status">Runtime response 状态。</param>
    /// <param name="resultJson">业务结果 JSON。</param>
    /// <param name="errorMessage">失败说明。</param>
    /// <param name="targetIdentity">命令目标宿主身份。</param>
    /// <param name="requestId">请求标识。</param>
    /// <param name="transport">实际使用的传输。</param>
    /// <param name="evidence">transport-specific 证据。</param>
    public WorkbenchCommandState(
        string kit,
        string action,
        bool ok,
        string status,
        string resultJson,
        string errorMessage,
        HostIdentity? targetIdentity,
        string requestId,
        string transport,
        CommandEvidence evidence)
        : this(
            kit,
            action,
            ok,
            status,
            resultJson,
            errorMessage,
            ok ? CommandOutcomeState.Succeeded : CommandOutcomeState.Failed,
            targetIdentity,
            requestId,
            transport,
            evidence)
    {
    }

    /// <summary>
    /// 创建带显式结果状态、宿主身份、请求标识和证据的 Workbench 命令状态。
    /// </summary>
    /// <param name="kit">命令 Kit。</param>
    /// <param name="action">命令 action。</param>
    /// <param name="ok">是否收到成功 terminal response。</param>
    /// <param name="status">Runtime response 状态或本地状态。</param>
    /// <param name="resultJson">业务结果 JSON。</param>
    /// <param name="errorMessage">失败或不确定说明。</param>
    /// <param name="outcome">跨传输统一结果状态。</param>
    /// <param name="targetIdentity">命令目标宿主身份。</param>
    /// <param name="requestId">请求标识。</param>
    /// <param name="transport">实际使用的传输。</param>
    /// <param name="evidence">transport-specific 证据。</param>
    public WorkbenchCommandState(
        string kit,
        string action,
        bool ok,
        string status,
        string resultJson,
        string errorMessage,
        CommandOutcomeState outcome,
        HostIdentity? targetIdentity,
        string requestId,
        string transport,
        CommandEvidence evidence)
    {
        Kit = kit ?? string.Empty;
        Action = action ?? string.Empty;
        Ok = ok;
        Status = status ?? string.Empty;
        ResultJson = resultJson ?? string.Empty;
        ErrorMessage = errorMessage ?? string.Empty;
        TargetIdentity = targetIdentity;
        RequestId = requestId ?? string.Empty;
        Transport = transport ?? string.Empty;
        Evidence = evidence ?? CommandEvidence.Empty("Command result did not provide evidence.");
        Outcome = outcome;
    }

    /// <summary>
    /// 获取命令 Kit。
    /// </summary>
    public string Kit { get; }

    /// <summary>
    /// 获取命令 action。
    /// </summary>
    public string Action { get; }

    /// <summary>
    /// 获取命令是否成功得到 terminal response。
    /// </summary>
    public bool Ok { get; }

    /// <summary>
    /// 获取 Runtime response 状态。
    /// </summary>
    public string Status { get; }

    /// <summary>
    /// 获取业务结果 JSON。
    /// </summary>
    public string ResultJson { get; }

    /// <summary>
    /// 获取失败说明。
    /// </summary>
    public string ErrorMessage { get; }

    /// <summary>获取命令目标宿主身份。</summary>
    public HostIdentity? TargetIdentity { get; }

    /// <summary>获取请求标识。</summary>
    public string RequestId { get; }

    /// <summary>获取实际使用的传输。</summary>
    public string Transport { get; }

    /// <summary>获取 transport-specific 证据。</summary>
    public CommandEvidence Evidence { get; }

    /// <summary>
    /// 获取跨传输统一结果状态；Unknown 不得被调用方自动重放为变更命令。
    /// </summary>
    public CommandOutcomeState Outcome { get; }
}
