using YokiFrame;
using YokiFrame.Client.FileBridge.Diagnostics;
using YokiFrame.Tooling.Application.Models;

namespace YokiFrame.Workbench.Avalonia.ViewModels;

/// <summary>
/// 负责把 dashboard 状态投影为框架总览页的可扫读卡片。
/// </summary>
public sealed partial class WorkbenchShellViewModel
{
    /// <summary>
    /// 创建顶部摘要卡片。
    /// </summary>
    /// <param name="state">dashboard 状态；为空时使用占位数据。</param>
    /// <returns>摘要卡片。</returns>
    private static IReadOnlyList<WorkbenchMetricCard> CreateSummaryCards(WorkbenchDashboardState? state)
    {
        if (state == null)
        {
            return new[]
            {
                new WorkbenchMetricCard("连接", "等待数据", "FileBridge"),
                new WorkbenchMetricCard("引擎", "等待发现", "0 registry"),
                new WorkbenchMetricCard("队列", "0 / 0", "results 0 / deadletter 0"),
                new WorkbenchMetricCard("最近问题", "--", "等待桥接状态")
            };
        }

        var health = state.BridgeHealth;
        return new[]
        {
            new WorkbenchMetricCard("连接", health.RequiresReconnect ? "等待连接" : "已连接", CreateConnectionDetail(state), isPositive: !health.RequiresReconnect),
            new WorkbenchMetricCard("引擎", CreateSelectedEngineHeadline(state), CreateEngineRegistryDetail(state)),
            new WorkbenchMetricCard("队列", CreateCompactQueueHeadline(state), CreateResultQueueDetail(state)),
            new WorkbenchMetricCard("最近问题", CreateRecentErrorHeadline(state), CreateRecentErrorDetail(state.BridgeStatus), isPositive: IsRecentIssueEmpty(state))
        };
    }

    /// <summary>
    /// 创建引擎和 FileBridge 状态卡片。
    /// </summary>
    /// <param name="state">dashboard 状态；为空时使用占位数据。</param>
    /// <returns>状态卡片。</returns>
    private static IReadOnlyList<WorkbenchMetricCard> CreateEngineCards(WorkbenchDashboardState? state)
    {
        var health = state?.BridgeHealth;
        var status = state?.BridgeStatus;
        return new[]
        {
            new WorkbenchMetricCard("心跳", CreateHeartbeatText(health), CreateHeartbeatDetail(health), isPositive: health?.RequiresReconnect == false),
            new WorkbenchMetricCard("命令", CreateCommandPathText(status), "engine-scoped"),
            new WorkbenchMetricCard("事件", "JSONL", CreateProtocolStorageDetail(status)),
            new WorkbenchMetricCard("背压", CreateBackpressureHeadline(status), CreateBackpressureDetail(status), isPositive: status?.BackpressureActive != true)
        };
    }

    /// <summary>
    /// 创建实时数据区域使用的 snapshot 状态卡片。
    /// </summary>
    /// <param name="state">dashboard 状态；为空时使用占位数据。</param>
    /// <returns>snapshot 状态卡片。</returns>
    private static IReadOnlyList<WorkbenchMetricCard> CreateSnapshotCards(WorkbenchDashboardState? state)
    {
        if (state == null)
        {
            return new[]
            {
                new WorkbenchMetricCard("实时数据", "waiting", "等待 dashboard")
            };
        }

        if (state.Snapshots.Count == 0)
        {
            return new[]
            {
                new WorkbenchMetricCard("实时数据", "0/0", "没有注册 snapshot")
            };
        }

        return state.Snapshots
            .Select(static snapshot => new WorkbenchMetricCard(
                snapshot.Kit,
                CreateSnapshotSourceText(snapshot),
                CreateSnapshotDetailText(snapshot),
                isPositive: snapshot.Exists,
                isAccent: IsTelemetrySource(snapshot.Source)))
            .ToArray();
    }

    /// <summary>
    /// 创建队列摘要文本。
    /// </summary>
    /// <param name="state">dashboard 状态。</param>
    /// <returns>队列摘要。</returns>
    private static string CreateQueueHeadline(WorkbenchDashboardState? state)
    {
        var status = state?.BridgeStatus;
        return (status?.PendingCount ?? 0) + " pending / " + (status?.ProcessingCount ?? 0) + " processing";
    }

    /// <summary>
    /// 创建紧凑队列摘要文本。
    /// </summary>
    /// <param name="state">dashboard 状态。</param>
    /// <returns>紧凑队列摘要。</returns>
    private static string CreateCompactQueueHeadline(WorkbenchDashboardState? state)
    {
        var status = state?.BridgeStatus;
        return (status?.PendingCount ?? 0) + " / " + (status?.ProcessingCount ?? 0);
    }

    /// <summary>
    /// 创建命令归档数量说明，放在队列卡片详情中。
    /// </summary>
    /// <param name="status">FileBridge 状态。</param>
    /// <returns>命令归档数量说明。</returns>
    private static string CreateArchiveDetail(FileBridgeStatus? status)
    {
        return "archive " + (status?.ArchiveCount ?? 0);
    }

    /// <summary>
    /// 创建结果和死信数量摘要，放在顶部队列卡片详情中。
    /// </summary>
    /// <param name="state">dashboard 状态。</param>
    /// <returns>结果队列摘要。</returns>
    private static string CreateResultQueueDetail(WorkbenchDashboardState? state)
    {
        var status = state?.BridgeStatus;
        return "results " + (status?.ResultCount ?? 0) + " / deadletter " + (status?.DeadletterCount ?? 0);
    }

    /// <summary>
    /// 创建 snapshot 可用数量摘要。
    /// </summary>
    /// <param name="state">dashboard 状态。</param>
    /// <returns>snapshot 可用数量。</returns>
    private static string CreateSnapshotAvailabilityHeadline(WorkbenchDashboardState state)
    {
        return state.Snapshots.Count(static snapshot => snapshot.Exists) + "/" + state.Snapshots.Count + " available";
    }

    /// <summary>
    /// 创建 snapshot 来源摘要，帮助用户判断当前是实时源还是文件快照。
    /// </summary>
    /// <param name="state">dashboard 状态。</param>
    /// <returns>snapshot 来源摘要。</returns>
    private static string CreateSnapshotSourceDetail(WorkbenchDashboardState state)
    {
        if (state.Snapshots.Count == 0)
        {
            return "没有注册 snapshot";
        }

        return string.Join(" / ", state.Snapshots.Select(static snapshot => snapshot.Source).Distinct(StringComparer.Ordinal));
    }

    /// <summary>
    /// 创建 engine registry 摘要。
    /// </summary>
    /// <param name="state">dashboard 状态。</param>
    /// <returns>registry 数量摘要。</returns>
    private static string CreateEngineRegistryDetail(WorkbenchDashboardState? state)
    {
        if (state == null)
        {
            return "0 registry entries";
        }

        var registry = state.Engines.FirstOrDefault(engine => engine.EngineId == state.SelectedEngineId);
        if (registry == null)
        {
            return state.Engines.Count + " registry entries";
        }

        var engine = string.IsNullOrWhiteSpace(registry.Engine) ? "engine" : registry.Engine;
        var version = string.IsNullOrWhiteSpace(registry.Version) ? "unknown" : registry.Version;
        return state.Engines.Count + " registry · " + engine + " " + version;
    }

    /// <summary>
    /// 创建当前 engine 的紧凑标题；等待用户选择时显示明确占位。
    /// </summary>
    /// <param name="state">dashboard 状态。</param>
    /// <returns>engine 标题。</returns>
    private static string CreateSelectedEngineHeadline(WorkbenchDashboardState state)
    {
        return string.IsNullOrWhiteSpace(state.SelectedEngineId)
            ? "等待选择"
            : state.SelectedEngineId;
    }

    /// <summary>
    /// 创建连接状态的短说明，避免把完整健康消息塞进总览卡片。
    /// </summary>
    /// <param name="state">dashboard 状态。</param>
    /// <returns>连接短说明。</returns>
    private static string CreateConnectionDetail(WorkbenchDashboardState state)
    {
        var mode = string.IsNullOrWhiteSpace(state.BridgeHealth.Mode) ? "unknown" : state.BridgeHealth.Mode;
        return mode + " · " + state.BridgeHealth.State;
    }

    /// <summary>
    /// 创建 heartbeat 辅助说明。
    /// </summary>
    /// <param name="health">FileBridge 健康状态。</param>
    /// <returns>heartbeat 辅助说明。</returns>
    private static string CreateHeartbeatDetail(WorkbenchBridgeHealth? health)
    {
        if (health == null)
        {
            return "status/heartbeat.json";
        }

        var age = health.HeartbeatAgeSeconds.HasValue ? health.HeartbeatAgeSeconds.Value + "s" : "missing";
        return "gen " + health.Generation + " · seq " + health.Sequence + " · age " + age;
    }

    /// <summary>
    /// 创建单个 snapshot 的来源文本。
    /// </summary>
    /// <param name="snapshot">snapshot 状态。</param>
    /// <returns>来源文本。</returns>
    private static string CreateSnapshotSourceText(WorkbenchSnapshotState snapshot)
    {
        return string.IsNullOrWhiteSpace(snapshot.Source) ? "snapshot" : snapshot.Source;
    }

    /// <summary>
    /// 创建单个 snapshot 的详情文本。
    /// </summary>
    /// <param name="snapshot">snapshot 状态。</param>
    /// <returns>详情文本。</returns>
    private static string CreateSnapshotDetailText(WorkbenchSnapshotState snapshot)
    {
        return snapshot.Exists ? "available" : "missing: " + snapshot.ErrorMessage;
    }

    /// <summary>
    /// 判断 snapshot 来源是否为实时 telemetry，用于在 UI 上做轻量强调。
    /// </summary>
    /// <param name="source">snapshot 来源。</param>
    /// <returns>来源为 telemetry 时返回 true。</returns>
    private static bool IsTelemetrySource(string source)
    {
        return string.Equals(source, "telemetry", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 创建最近错误摘要，优先使用宿主上报的 FileBridge 错误，再回落 dashboard 读取错误。
    /// </summary>
    /// <param name="state">dashboard 状态。</param>
    /// <returns>最近错误摘要。</returns>
    private static string CreateRecentErrorHeadline(WorkbenchDashboardState? state)
    {
        if (!string.IsNullOrWhiteSpace(state?.BridgeStatus?.LastError))
        {
            return state.BridgeStatus.LastError;
        }

        return state?.ErrorMessages.Count > 0 ? state.ErrorMessages[0] : "--";
    }

    /// <summary>
    /// 创建最近错误辅助说明；没有限流原因时显示无活动限制。
    /// </summary>
    /// <param name="status">FileBridge 状态。</param>
    /// <returns>最近错误辅助说明。</returns>
    private static string CreateRecentErrorDetail(FileBridgeStatus? status)
    {
        return string.IsNullOrWhiteSpace(status?.LastPollLimitReason)
            ? "无活动限制"
            : "limit " + status.LastPollLimitReason;
    }

    /// <summary>
    /// 判断当前是否没有需要首屏暴露的问题。
    /// </summary>
    /// <param name="state">dashboard 状态。</param>
    /// <returns>没有问题时返回 true。</returns>
    private static bool IsRecentIssueEmpty(WorkbenchDashboardState state)
    {
        return string.IsNullOrWhiteSpace(state.BridgeStatus?.LastError)
            && state.ErrorMessages.Count == 0
            && state.BridgeStatus?.BackpressureActive != true;
    }

    /// <summary>
    /// 创建 heartbeat 文件摘要。
    /// </summary>
    /// <param name="health">FileBridge 健康状态。</param>
    /// <returns>heartbeat 摘要。</returns>
    private static string CreateHeartbeatText(WorkbenchBridgeHealth? health)
    {
        if (health == null)
        {
            return "等待心跳";
        }

        return health.RequiresReconnect ? "已过期" : "新鲜";
    }

    /// <summary>
    /// 创建运行模式摘要。
    /// </summary>
    /// <param name="health">FileBridge 健康状态。</param>
    /// <returns>运行模式摘要。</returns>
    private static string CreateModeHeadline(WorkbenchBridgeHealth health)
    {
        return string.IsNullOrWhiteSpace(health.Mode) ? "unknown" : health.Mode;
    }

    /// <summary>
    /// 创建运行模式辅助说明。
    /// </summary>
    /// <param name="health">FileBridge 健康状态。</param>
    /// <returns>运行模式辅助说明。</returns>
    private static string CreateModeDetail(WorkbenchBridgeHealth health)
    {
        return string.IsNullOrWhiteSpace(health.SessionId) ? "session unknown" : "session " + health.SessionId;
    }

    /// <summary>
    /// 创建 heartbeat 文件路径摘要。
    /// </summary>
    /// <param name="status">FileBridge 状态。</param>
    /// <returns>heartbeat 文件路径。</returns>
    private static string CreateHeartbeatFileText(FileBridgeStatus? status)
    {
        var heartbeat = status?.Heartbeat;
        if (heartbeat != null && !string.IsNullOrWhiteSpace(heartbeat.Path))
        {
            return heartbeat.Path;
        }

        if (string.IsNullOrWhiteSpace(status?.EngineRoot))
        {
            return "status/heartbeat.json";
        }

        return Path.Combine(
            status.EngineRoot,
            YokiFrameFileBridgeLayout.STATUS_DIRECTORY,
            YokiFrameFileBridgeLayout.HEARTBEAT_FILE_NAME);
    }

    /// <summary>
    /// 创建命令目录路径摘要。
    /// </summary>
    /// <param name="status">FileBridge 状态。</param>
    /// <returns>命令目录路径。</returns>
    private static string CreateCommandPathText(FileBridgeStatus? status)
    {
        return string.IsNullOrWhiteSpace(status?.CommandsRoot) ? "commands" : Path.GetFileName(status.CommandsRoot);
    }

    /// <summary>
    /// 创建协议文件存储摘要，帮助定位事件流和历史结果是否堆积。
    /// </summary>
    /// <param name="status">FileBridge 状态。</param>
    /// <returns>协议文件存储摘要。</returns>
    private static string CreateProtocolStorageDetail(FileBridgeStatus? status)
    {
        if (status == null)
        {
            return "protocol 0 files / 0 bytes";
        }

        return "protocol " + status.ProtocolFileCount + " files / " + status.ProtocolBytes + " bytes";
    }

    /// <summary>
    /// 创建结果文件数量摘要。
    /// </summary>
    /// <param name="status">FileBridge 状态。</param>
    /// <returns>结果文件数量摘要。</returns>
    private static string CreateResultHeadline(FileBridgeStatus? status)
    {
        return (status?.ResultCount ?? 0).ToString();
    }

    /// <summary>
    /// 创建死信结果数量说明。
    /// </summary>
    /// <param name="status">FileBridge 状态。</param>
    /// <returns>死信结果数量说明。</returns>
    private static string CreateDeadletterDetail(FileBridgeStatus? status)
    {
        return "deadletter " + (status?.DeadletterCount ?? 0);
    }

    /// <summary>
    /// 创建背压状态摘要。
    /// </summary>
    /// <param name="status">FileBridge 状态。</param>
    /// <returns>背压状态摘要。</returns>
    private static string CreateBackpressureHeadline(FileBridgeStatus? status)
    {
        return status?.BackpressureActive == true ? "Active" : "Idle";
    }

    /// <summary>
    /// 创建背压计数说明。
    /// </summary>
    /// <param name="status">FileBridge 状态。</param>
    /// <returns>背压计数说明。</returns>
    private static string CreateBackpressureDetail(FileBridgeStatus? status)
    {
        return "BridgeBusy " + (status?.BridgeBusyCount ?? 0);
    }
}
