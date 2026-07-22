namespace YokiFrame.Tooling.Application.Models.ProjectModel;

/// <summary>
/// 描述 Project Model 读取或刷新过程中可供 AI/Workbench 处理的问题。
/// </summary>
public sealed class ProjectModelIssue
{
    /// <summary>创建结构化 Project Model 问题。</summary>
    public ProjectModelIssue(
        string code,
        string severity,
        string message,
        string suggestion,
        IReadOnlyList<string> evidencePaths)
    {
        Code = code;
        Severity = severity;
        Message = message;
        Suggestion = suggestion;
        EvidencePaths = evidencePaths.ToArray();
    }

    /// <summary>获取稳定问题码。</summary>
    public string Code { get; }

    /// <summary>获取 Warning、Error 或 Info 严重度。</summary>
    public string Severity { get; }

    /// <summary>获取问题说明。</summary>
    public string Message { get; }

    /// <summary>获取恢复建议。</summary>
    public string Suggestion { get; }

    /// <summary>获取支持该问题的证据路径。</summary>
    public IReadOnlyList<string> EvidencePaths { get; }
}
