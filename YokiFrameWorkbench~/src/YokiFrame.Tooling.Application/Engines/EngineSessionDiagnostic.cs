namespace YokiFrame.Tooling.Application.Engines;

/// <summary>
/// 描述引擎会话发现过程中的局部诊断；单个 engine 的问题不应丢弃其它有效条目。
/// </summary>
public sealed class EngineSessionDiagnostic
{
    /// <summary>
    /// 创建引擎会话诊断。
    /// </summary>
    /// <param name="code">稳定诊断代码。</param>
    /// <param name="message">面向工具调用方的诊断说明。</param>
    /// <param name="engineId">关联 engine；目录级问题可以为空。</param>
    /// <param name="evidencePaths">可复查的文件证据。</param>
    public EngineSessionDiagnostic(
        string code,
        string message,
        string? engineId,
        IReadOnlyList<string> evidencePaths)
    {
        Code = code;
        Message = message;
        EngineId = engineId ?? string.Empty;
        EvidencePaths = evidencePaths.ToArray();
    }

    /// <summary>获取稳定诊断代码。</summary>
    public string Code { get; }

    /// <summary>获取诊断说明。</summary>
    public string Message { get; }

    /// <summary>获取关联 engine 标识。</summary>
    public string EngineId { get; }

    /// <summary>获取诊断证据路径。</summary>
    public IReadOnlyList<string> EvidencePaths { get; }
}
