using YokiFrame.Tooling.Application.Models;
using YokiFrame.Workbench.Avalonia.ViewModels;

namespace YokiFrame.Workbench.Avalonia.Pages;

/// <summary>
/// 把 Tooling.Application dashboard read model 投影为 Workbench 详情页段落。
/// </summary>
internal static class WorkbenchPageSectionProjector
{
    /// <summary>
    /// 创建 Framework 页对应的系统状态段落，供模块契约和未来复用使用。
    /// </summary>
    /// <param name="state">dashboard 状态。</param>
    /// <returns>系统状态段落。</returns>
    internal static IReadOnlyList<WorkbenchDisplaySection> CreateFrameworkSections(WorkbenchDashboardState state)
    {
        return new[]
        {
            new WorkbenchDisplaySection("Project", state.ProjectRoot),
            new WorkbenchDisplaySection("Engines", string.Join(", ", state.Engines.Select(static engine => engine.EngineId))),
            new WorkbenchDisplaySection("Harness", state.HarnessSummary),
            new WorkbenchDisplaySection("Bridge Health", CreateBridgeHealthText(state.BridgeHealth)),
            new WorkbenchDisplaySection("Bridge Queues", CreateBridgeQueueText(state)),
            new WorkbenchDisplaySection("Evidence", string.Join(Environment.NewLine, state.BridgeHealth.EvidencePaths)),
            new WorkbenchDisplaySection("Errors", state.ErrorMessages.Count == 0 ? "none" : string.Join(Environment.NewLine, state.ErrorMessages))
        };
    }

    /// <summary>
    /// 创建 Doctor 页段落；报告不可用时保留恢复建议。
    /// </summary>
    /// <param name="state">dashboard 状态。</param>
    /// <returns>Doctor 段落。</returns>
    internal static IReadOnlyList<WorkbenchDisplaySection> CreateDoctorSections(WorkbenchDashboardState state)
    {
        var report = state.DoctorReport;
        if (report == null)
        {
            return new[]
            {
                new WorkbenchDisplaySection("Status", "unavailable"),
                new WorkbenchDisplaySection("Suggestion", state.BridgeHealth.Suggestion)
            };
        }

        return new[]
        {
            new WorkbenchDisplaySection("Level", report.Level),
            new WorkbenchDisplaySection("Issues", CreateDoctorIssueText(report.Issues)),
            new WorkbenchDisplaySection("Bridge Queues", CreateBridgeQueueText(state)),
            new WorkbenchDisplaySection("Heartbeat", CreateBridgeHealthText(state.BridgeHealth)),
            new WorkbenchDisplaySection("Generated", report.GeneratedAtUtc.ToLocalTime().ToString("HH:mm:ss"))
        };
    }

    /// <summary>
    /// 创建指定 Kit 的状态段落，只读取 Dashboard 已聚合的 snapshot，不触发额外 IO。
    /// </summary>
    /// <param name="state">dashboard 状态。</param>
    /// <param name="kit">目标 Kit 名称。</param>
    /// <returns>Kit 状态段落。</returns>
    internal static IReadOnlyList<WorkbenchDisplaySection> CreateKitSections(
        WorkbenchDashboardState state,
        string kit)
    {
        var snapshot = state.Snapshots.FirstOrDefault(item => item.Kit == kit);
        if (snapshot == null)
        {
            return new[]
            {
                new WorkbenchDisplaySection("Kit", kit),
                new WorkbenchDisplaySection("Snapshot", "missing")
            };
        }

        return new[]
        {
            new WorkbenchDisplaySection("Kit", kit),
            new WorkbenchDisplaySection("Source", snapshot.Source),
            new WorkbenchDisplaySection("Path", snapshot.Path),
            new WorkbenchDisplaySection("Status", snapshot.Exists ? "available" : "missing"),
            new WorkbenchDisplaySection("Data", snapshot.Exists ? snapshot.PayloadPreview : snapshot.ErrorMessage)
        };
    }

    /// <summary>
    /// 创建 Documentation 页的实际项目路径和 harness 状态段落。
    /// </summary>
    /// <param name="state">dashboard 状态。</param>
    /// <returns>文档路径段落。</returns>
    internal static IReadOnlyList<WorkbenchDisplaySection> CreateDocumentationSections(WorkbenchDashboardState state)
    {
        return new[]
        {
            new WorkbenchDisplaySection("Project", state.ProjectRoot),
            new WorkbenchDisplaySection("Documentation", Path.Combine(state.ProjectRoot, "Assets", "YokiFrame", "Documentation~")),
            new WorkbenchDisplaySection("Workbench", Path.Combine(state.ProjectRoot, "Assets", "YokiFrame", "YokiFrameWorkbench~")),
            new WorkbenchDisplaySection("Harness", state.HarnessSummary)
        };
    }

    /// <summary>
    /// 创建 FileBridge 健康摘要。
    /// </summary>
    /// <param name="health">FileBridge 健康状态。</param>
    /// <returns>可显示摘要。</returns>
    private static string CreateBridgeHealthText(WorkbenchBridgeHealth health)
    {
        var age = health.HeartbeatAgeSeconds.HasValue ? health.HeartbeatAgeSeconds.Value + "s" : "missing";
        return health.State
            + " | " + health.Message
            + " | heartbeatAge=" + age
            + " | threshold=" + health.StaleThresholdSeconds + "s"
            + " | session=" + CreateOptionalText(health.SessionId)
            + " | generation=" + health.Generation
            + " | sequence=" + health.Sequence
            + " | suggestion=" + health.Suggestion;
    }

    /// <summary>
    /// 创建 FileBridge 队列摘要。
    /// </summary>
    /// <param name="state">dashboard 状态。</param>
    /// <returns>可显示摘要。</returns>
    private static string CreateBridgeQueueText(WorkbenchDashboardState state)
    {
        var status = state.BridgeStatus;
        if (status == null)
        {
            return "unavailable";
        }

        return "pending=" + status.PendingCount
            + ", processing=" + status.ProcessingCount
            + ", archive=" + status.ArchiveCount
            + ", deadletter=" + status.DeadletterCount
            + ", results=" + status.ResultCount;
    }

    /// <summary>
    /// 创建 Doctor issue 摘要文本。
    /// </summary>
    /// <param name="issues">诊断 issue 列表。</param>
    /// <returns>可显示摘要。</returns>
    private static string CreateDoctorIssueText(IReadOnlyList<WorkbenchDoctorIssue> issues)
    {
        if (issues.Count == 0)
        {
            return "none";
        }

        return string.Join(Environment.NewLine, issues.Select(static issue => issue.Code
            + " | " + issue.Message
            + " | suggestion=" + issue.Suggestion
            + " | evidence=" + string.Join(", ", issue.EvidencePaths)));
    }

    /// <summary>
    /// 把空文本转换为统一占位，避免状态段落出现空字段。
    /// </summary>
    /// <param name="value">待显示文本。</param>
    /// <returns>可显示文本。</returns>
    private static string CreateOptionalText(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "unknown" : value;
    }
}
