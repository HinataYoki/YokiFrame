namespace YokiFrame.Tooling.Application.Models;

/// <summary>
/// 描述 Workbench/CLI doctor 发现的一条诊断问题。
/// </summary>
public sealed class WorkbenchDoctorIssue
{
    /// <summary>
    /// 创建 doctor 诊断问题。
    /// </summary>
    /// <param name="code">稳定诊断码，供脚本和 UI 分类。</param>
    /// <param name="message">问题说明。</param>
    /// <param name="suggestion">建议处理动作。</param>
    /// <param name="evidencePaths">相关证据路径。</param>
    public WorkbenchDoctorIssue(
        string code,
        string message,
        string suggestion,
        IReadOnlyList<string> evidencePaths)
    {
        Code = code;
        Message = message;
        Suggestion = suggestion;
        EvidencePaths = evidencePaths;
    }

    /// <summary>
    /// 获取稳定诊断码。
    /// </summary>
    public string Code { get; }

    /// <summary>
    /// 获取问题说明。
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// 获取建议处理动作。
    /// </summary>
    public string Suggestion { get; }

    /// <summary>
    /// 获取相关证据路径。
    /// </summary>
    public IReadOnlyList<string> EvidencePaths { get; }
}
