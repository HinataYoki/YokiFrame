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
        IEnumerable<string>? evidencePaths = null)
    {
        if (response.ProtocolVersion == envelope.ProtocolVersion
            && string.Equals(response.RequestId, envelope.RequestId, StringComparison.Ordinal)
            && string.Equals(response.EngineId, envelope.EngineId, StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(response.Status))
        {
            return response;
        }

        throw new YokiFrameProtocolException(new YokiFrameError(
            errorCode,
            message,
            suggestion,
            evidencePaths));
    }
}
