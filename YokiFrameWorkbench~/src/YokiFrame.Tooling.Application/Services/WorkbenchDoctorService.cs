using YokiFrame;
using YokiFrame.Client;
using YokiFrame.Client.FileBridge.Diagnostics;
using YokiFrame.Protocol.FileBridge;
using YokiFrame.Tooling.Application.Engines;
using YokiFrame.Tooling.Application.Models;

namespace YokiFrame.Tooling.Application.Services;

/// <summary>
/// 提供 Workbench 和 CLI 共用的 FileBridge 只读诊断能力。
/// </summary>
public sealed class WorkbenchDoctorService
{
    private readonly IYokiFrameClient mClient;
    private readonly EngineSelectionService mEngineSelectionService;

    /// <summary>
    /// 获取 heartbeat stale 判定阈值。
    /// </summary>
    public static TimeSpan HeartbeatStaleThreshold => EngineSelectionService.HeartbeatStaleThreshold;

    /// <summary>
    /// 使用项目根目录创建 doctor 服务。
    /// </summary>
    /// <param name="projectRoot">Unity/Godot 项目根目录。</param>
    public WorkbenchDoctorService(string projectRoot)
        : this(new YokiFrameClient(projectRoot))
    {
    }

    /// <summary>
    /// 使用可替换 Client 创建 doctor 服务，供 CLI、Workbench 和测试共享同一用例。
    /// </summary>
    /// <param name="client">统一 YokiFrame Client。</param>
    public WorkbenchDoctorService(IYokiFrameClient client)
    {
        mClient = client;
        mEngineSelectionService = new EngineSelectionService(client);
    }

    /// <summary>
    /// 分析指定 engine 的 FileBridge 状态，不发送 command，也不修改任何文件。
    /// </summary>
    /// <param name="engineId">目标 engine；为空时使用默认 Unity Editor。</param>
    /// <returns>doctor 诊断报告。</returns>
    public WorkbenchDoctorReport Analyze(string engineId)
    {
        var nowUtc = DateTimeOffset.UtcNow;
        var selectedEngineId = mEngineSelectionService.Resolve(engineId, nowUtc);
        var status = mClient.ReadBridgeStatus(selectedEngineId);
        var registry = FindEngineRegistry(selectedEngineId);
        return AnalyzeStatus(registry, selectedEngineId, status, nowUtc);
    }

    /// <summary>
    /// 基于调用方已读取的 FileBridge 状态生成诊断报告，避免 Workbench 刷新时重复读取状态文件。
    /// </summary>
    /// <param name="engineId">目标 engine。</param>
    /// <param name="status">FileBridge 状态。</param>
    /// <param name="nowUtc">诊断生成时间。</param>
    /// <returns>doctor 诊断报告。</returns>
    public WorkbenchDoctorReport AnalyzeStatus(string engineId, FileBridgeStatus status, DateTimeOffset nowUtc)
    {
        return AnalyzeStatus(null, engineId, status, nowUtc);
    }

    /// <summary>
    /// 基于调用方已读取的 registry 与 FileBridge 状态生成诊断报告，避免 Dashboard 刷新时重复扫描 engine 目录。
    /// </summary>
    /// <param name="registry">当前 engine registry；不可用时为 null。</param>
    /// <param name="engineId">目标 engine。</param>
    /// <param name="status">FileBridge 状态。</param>
    /// <param name="nowUtc">诊断生成时间。</param>
    /// <returns>包含 host identity 一致性结果的诊断报告。</returns>
    internal WorkbenchDoctorReport AnalyzeStatus(
        EngineRegistryEntry? registry,
        string engineId,
        FileBridgeStatus status,
        DateTimeOffset nowUtc)
    {
        var issues = CreateIssues(registry, status, nowUtc);
        return new WorkbenchDoctorReport(engineId, nowUtc, issues, status);
    }

    /// <summary>
    /// 根据 registry 与 FileBridge 状态生成诊断 issue；覆盖宿主身份切换、连接实时性和 deadletter 证据。
    /// </summary>
    /// <param name="registry">当前 engine registry；不可用时为 null。</param>
    /// <param name="status">FileBridge 状态。</param>
    /// <param name="nowUtc">当前 UTC 时间。</param>
    /// <returns>诊断 issue 列表。</returns>
    private IReadOnlyList<WorkbenchDoctorIssue> CreateIssues(
        EngineRegistryEntry? registry,
        FileBridgeStatus status,
        DateTimeOffset nowUtc)
    {
        List<WorkbenchDoctorIssue> issues = new();
        var heartbeat = status.Heartbeat;
        if (heartbeat == null)
        {
            issues.Add(CreateHeartbeatMissingIssue(status.EngineId));
        }
        else
        {
            if (registry != null && EngineHostIdentity.HasMismatch(registry, heartbeat))
            {
                issues.Add(CreateHostIdentityMismatchIssue(registry, status, heartbeat));
            }

            if (heartbeat.IsStale(nowUtc, HeartbeatStaleThreshold))
            {
                issues.Add(CreateHeartbeatStaleIssue(heartbeat.Path));
            }
        }

        if (status.DeadletterCount > 0)
        {
            issues.Add(CreateDeadletterIssue(status.CommandsRoot));
        }

        return issues;
    }

    /// <summary>
    /// 读取当前选中 engine 的 registry，用于 CLI Doctor 复用与 Dashboard 相同的宿主身份判断。
    /// </summary>
    /// <param name="engineId">已经通过安全校验的 engine 标识。</param>
    /// <returns>匹配的 registry；不存在时返回 null。</returns>
    private EngineRegistryEntry? FindEngineRegistry(string engineId)
    {
        return mClient.ReadEngineEntries().FirstOrDefault(
            entry => string.Equals(entry.EngineId, engineId, StringComparison.Ordinal));
    }

    /// <summary>
    /// 创建 registry 与 heartbeat 指向不同宿主实例时的诊断，并保留两个状态文件的排查位置。
    /// </summary>
    /// <param name="registry">当前 engine registry。</param>
    /// <param name="status">当前 FileBridge 状态。</param>
    /// <param name="heartbeat">尚未同步到 registry 的 heartbeat。</param>
    /// <returns>宿主身份切换诊断。</returns>
    private static WorkbenchDoctorIssue CreateHostIdentityMismatchIssue(
        EngineRegistryEntry registry,
        FileBridgeStatus status,
        HeartbeatInfo heartbeat)
    {
        return new WorkbenchDoctorIssue(
            "HostIdentityMismatch",
            "Engine registry and heartbeat refer to different host identities.",
            "Wait for the host heartbeat to publish the current session and generation, then refresh.",
            new[] { heartbeat.Path, status.EngineRoot });
    }

    /// <summary>
    /// 创建 heartbeat 缺失诊断。
    /// </summary>
    /// <param name="engineId">目标 engine。</param>
    /// <returns>诊断 issue。</returns>
    private WorkbenchDoctorIssue CreateHeartbeatMissingIssue(string engineId)
    {
        return new WorkbenchDoctorIssue(
            "HeartbeatMissing",
            "Engine heartbeat was not found.",
            "Start the engine adapter or verify the requested engine id.",
            new[] { mClient.Paths.GetHeartbeatPath(engineId) });
    }

    /// <summary>
    /// 创建 heartbeat 过期诊断。
    /// </summary>
    /// <param name="heartbeatPath">heartbeat 文件路径。</param>
    /// <returns>诊断 issue。</returns>
    private static WorkbenchDoctorIssue CreateHeartbeatStaleIssue(string heartbeatPath)
    {
        return new WorkbenchDoctorIssue(
            "HeartbeatStale",
            "Engine heartbeat is stale.",
            "Verify whether the engine adapter is running or wait for the next heartbeat.",
            new[] { heartbeatPath });
    }

    /// <summary>
    /// 创建 deadletter 存在诊断。
    /// </summary>
    /// <param name="commandsRoot">commands 根目录。</param>
    /// <returns>诊断 issue。</returns>
    private static WorkbenchDoctorIssue CreateDeadletterIssue(string commandsRoot)
    {
        return new WorkbenchDoctorIssue(
            "DeadletterPresent",
            "Deadletter evidence exists.",
            "Inspect deadletter files before sending more commands.",
            new[] { Path.Combine(commandsRoot, YokiFrameFileBridgeLayout.DEADLETTER_DIRECTORY) });
    }
}
