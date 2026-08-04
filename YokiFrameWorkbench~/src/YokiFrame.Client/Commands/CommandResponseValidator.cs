using YokiFrame.Protocol.FileBridge;
using YokiFrame.Protocol.Results;

namespace YokiFrame.Client.Commands;

/// <summary>
/// 统一验证 FileBridge 与 FastChannel 响应仍关联当前命令，避免旧响应或污染文件被错误接受。
/// </summary>
internal static class CommandResponseValidator
{
    /// <summary>
    /// 校验协议版本、请求、引擎和 terminal 状态，并保留调用传输层的稳定错误语义。
    /// </summary>
    /// <param name="response">待验证响应。</param>
    /// <param name="envelope">当前命令信封。</param>
    /// <param name="errorCode">当前传输层使用的错误码。</param>
    /// <param name="message">关联失败说明。</param>
    /// <param name="suggestion">调用方可执行的恢复建议。</param>
    /// <param name="evidencePaths">可选证据文件路径。</param>
    /// <returns>通过关联校验的原响应。</returns>
    internal static CommandResponse Validate(
        CommandResponse response,
        CommandEnvelope envelope,
        string errorCode,
        string message,
        string suggestion,
        IEnumerable<string>? evidencePaths = null,
        string? requestId = null,
        string? engineId = null,
        string? transport = null)
    {
        return Validate(
            response,
            envelope.ProtocolVersion,
            envelope.RequestId,
            envelope.EngineId,
            errorCode,
            message,
            suggestion,
            evidencePaths,
            requestId,
            engineId,
            transport);
    }

    /// <summary>
    /// 校验没有完整 envelope 的 terminal response，例如依据 results 文件查询请求状态。
    /// </summary>
    /// <param name="response">待验证响应。</param>
    /// <param name="expectedProtocolVersion">期望协议版本。</param>
    /// <param name="expectedRequestId">期望请求标识。</param>
    /// <param name="expectedEngineId">期望 engine 标识。</param>
    /// <param name="errorCode">当前传输层使用的错误码。</param>
    /// <param name="message">关联失败说明。</param>
    /// <param name="suggestion">调用方可执行的恢复建议。</param>
    /// <param name="evidencePaths">可选证据文件路径。</param>
    /// <param name="requestId">错误对象关联请求标识。</param>
    /// <param name="engineId">错误对象关联 engine 标识。</param>
    /// <param name="transport">错误对象关联传输标识。</param>
    /// <returns>通过关联校验的原响应。</returns>
    internal static CommandResponse Validate(
        CommandResponse response,
        int expectedProtocolVersion,
        string expectedRequestId,
        string expectedEngineId,
        string errorCode,
        string message,
        string suggestion,
        IEnumerable<string>? evidencePaths = null,
        string? requestId = null,
        string? engineId = null,
        string? transport = null)
    {
        if (response.ProtocolVersion == expectedProtocolVersion
            && string.Equals(response.RequestId, expectedRequestId, StringComparison.Ordinal)
            && string.Equals(response.EngineId, expectedEngineId, StringComparison.Ordinal)
            && IsTerminalStatus(response.Status))
        {
            return response;
        }

        throw new YokiFrameProtocolException(new YokiFrameError(
            errorCode,
            message,
            suggestion,
            evidencePaths,
            requestId ?? expectedRequestId,
            engineId ?? expectedEngineId,
            transport));
    }

    /// <summary>
    /// 判断状态是否属于当前 FileBridge/FastChannel 协议定义的 terminal 集合。
    /// </summary>
    /// <param name="status">响应状态文本。</param>
    /// <returns>Success 或 Error（大小写不敏感）时返回 true。</returns>
    internal static bool IsTerminalStatus(string? status)
    {
        return string.Equals(status, "Success", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "Error", StringComparison.OrdinalIgnoreCase);
    }
}
