using YokiFrame.Client.FileBridge.Diagnostics;

namespace YokiFrame.Tooling.Application.Models;

/// <summary>
/// 描述 Workbench/CLI doctor 的一次只读诊断结果。
/// </summary>
public sealed class WorkbenchDoctorReport
{
    /// <summary>
    /// 创建 doctor 诊断报告。
    /// </summary>
    /// <param name="engineId">目标 engine 标识。</param>
    /// <param name="generatedAtUtc">诊断生成时间。</param>
    /// <param name="issues">诊断问题列表。</param>
    /// <param name="status">FileBridge 状态快照。</param>
    public WorkbenchDoctorReport(
        string engineId,
        DateTimeOffset generatedAtUtc,
        IReadOnlyList<WorkbenchDoctorIssue> issues,
        FileBridgeStatus status)
    {
        EngineId = engineId;
        GeneratedAtUtc = generatedAtUtc;
        Issues = issues;
        Status = status;
    }

    /// <summary>
    /// 获取目标 engine 标识。
    /// </summary>
    public string EngineId { get; }

    /// <summary>
    /// 获取诊断生成时间。
    /// </summary>
    public DateTimeOffset GeneratedAtUtc { get; }

    /// <summary>
    /// 获取诊断等级；当前只区分 Healthy 和 Warning。
    /// </summary>
    public string Level => Issues.Count == 0 ? "Healthy" : "Warning";

    /// <summary>
    /// 获取诊断问题数量。
    /// </summary>
    public int IssueCount => Issues.Count;

    /// <summary>
    /// 获取诊断问题列表。
    /// </summary>
    public IReadOnlyList<WorkbenchDoctorIssue> Issues { get; }

    /// <summary>
    /// 获取 FileBridge 状态快照。
    /// </summary>
    public FileBridgeStatus Status { get; }
}
