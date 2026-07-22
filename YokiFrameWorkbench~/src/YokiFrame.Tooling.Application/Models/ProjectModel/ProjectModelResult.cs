using YokiFrame.Protocol.ProjectModel;

namespace YokiFrame.Tooling.Application.Models.ProjectModel;

/// <summary>
/// 统一返回 Project Model status/refresh 的状态、bundle、问题和证据。
/// </summary>
public sealed class ProjectModelResult
{
    /// <summary>创建 Project Model 结果。</summary>
    public ProjectModelResult(
        string state,
        bool changed,
        ProjectModelBundle? bundle,
        IReadOnlyList<ProjectModelIssue> issues,
        IReadOnlyList<string> evidencePaths)
    {
        State = state;
        Changed = changed;
        Bundle = bundle;
        Issues = issues.ToArray();
        EvidencePaths = evidencePaths.ToArray();
    }

    /// <summary>获取 Ready、Missing、Stale、Partial 或 Blocked 状态。</summary>
    public string State { get; }

    /// <summary>获取本次 refresh 是否实际提交了新 generation。</summary>
    public bool Changed { get; }

    /// <summary>获取通过一致性校验的五文件 bundle。</summary>
    public ProjectModelBundle? Bundle { get; }

    /// <summary>获取结构化问题。</summary>
    public IReadOnlyList<ProjectModelIssue> Issues { get; }

    /// <summary>获取所有相关证据路径。</summary>
    public IReadOnlyList<string> EvidencePaths { get; }

    /// <summary>获取当前结果是否满足严格自动化 gate。</summary>
    public bool IsReady => string.Equals(State, "Ready", StringComparison.Ordinal);
}
