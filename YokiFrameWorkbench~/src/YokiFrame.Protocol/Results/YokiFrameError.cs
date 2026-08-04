using System.Text.Json.Nodes;

namespace YokiFrame.Protocol.Results;

/// <summary>
/// 描述 CLI 和协议服务返回给 AI/脚本的标准错误信息。
/// </summary>
public sealed class YokiFrameError
{
    /// <summary>
    /// 创建标准错误对象；调用方必须提供可行动的修复建议和证据路径。
    /// </summary>
    /// <param name="code">稳定错误码，供脚本和测试断言。</param>
    /// <param name="message">面向用户的错误说明。</param>
    /// <param name="suggestion">建议的下一步处理方式。</param>
    /// <param name="evidencePaths">可用于复查的文件或目录路径。</param>
    /// <param name="requestId">关联请求标识；非请求类错误为空。</param>
    /// <param name="engineId">关联宿主标识；非宿主错误为空。</param>
    /// <param name="transport">产生错误的传输；未知时为空。</param>
    public YokiFrameError(
        string code,
        string message,
        string suggestion,
        IEnumerable<string>? evidencePaths = null,
        string? requestId = null,
        string? engineId = null,
        string? transport = null)
    {
        Code = string.IsNullOrWhiteSpace(code) ? "Unknown" : code;
        Message = string.IsNullOrWhiteSpace(message) ? "YokiFrame operation failed." : message;
        Suggestion = string.IsNullOrWhiteSpace(suggestion) ? "Inspect evidence paths and retry." : suggestion;
        EvidencePaths = evidencePaths?.Where(static path => !string.IsNullOrWhiteSpace(path)).ToArray()
            ?? Array.Empty<string>();
        RequestId = requestId ?? string.Empty;
        EngineId = engineId ?? string.Empty;
        Transport = transport ?? string.Empty;
    }

    /// <summary>
    /// 获取稳定错误码。
    /// </summary>
    public string Code { get; }

    /// <summary>
    /// 获取错误说明。
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// 获取建议动作。
    /// </summary>
    public string Suggestion { get; }

    /// <summary>
    /// 获取可复查的证据路径。
    /// </summary>
    public IReadOnlyList<string> EvidencePaths { get; }

    /// <summary>获取关联请求标识。</summary>
    public string RequestId { get; }

    /// <summary>获取关联宿主标识。</summary>
    public string EngineId { get; }

    /// <summary>获取产生错误的传输标识。</summary>
    public string Transport { get; }

    /// <summary>
    /// 转换为 compact CLI 输出使用的 JSON 节点。
    /// </summary>
    /// <returns>包含 code、message、suggestion 和 evidencePaths 的 JSON 对象。</returns>
    public JsonObject ToJson()
    {
        JsonArray evidencePaths = new();
        foreach (var path in EvidencePaths)
        {
            evidencePaths.Add(JsonValue.Create(path));
        }

        return new JsonObject
        {
            ["code"] = Code,
            ["message"] = Message,
            ["suggestion"] = Suggestion,
            ["evidencePaths"] = evidencePaths,
            ["requestId"] = string.IsNullOrEmpty(RequestId) ? null : RequestId,
            ["engineId"] = string.IsNullOrEmpty(EngineId) ? null : EngineId,
            ["transport"] = string.IsNullOrEmpty(Transport) ? null : Transport
        };
    }
}
