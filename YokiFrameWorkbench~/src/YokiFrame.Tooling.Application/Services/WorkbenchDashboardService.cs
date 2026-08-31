using YokiFrame.Client.FileBridge.Diagnostics;
using YokiFrame.Protocol.FileBridge;
using YokiFrame.Tooling.Application.Engines;
using YokiFrame.Tooling.Application.Models;

namespace YokiFrame.Tooling.Application.Services;

/// <summary>
/// 为 Avalonia Workbench 聚合 FileBridge 首屏数据。
/// </summary>
public sealed partial class WorkbenchDashboardService : IDisposable
{
    private const int COMMAND_TIMEOUT_MS = 10000;
    private static readonly TimeSpan HeartbeatStaleThreshold = EngineSelectionService.HeartbeatStaleThreshold;

    /// <summary>
    /// 读取 Workbench 首屏需要的 engine、bridge、snapshot 和 harness 状态。
    /// </summary>
    /// <param name="engineId">目标 engine；为空时只自动选择唯一在线 engine。</param>
    /// <returns>聚合后的 dashboard 状态。</returns>
    public WorkbenchDashboardState LoadDashboard(string engineId)
    {
        List<string> errors = new();
        var generatedAtUtc = DateTimeOffset.UtcNow;
        var engineSession = mEngineSessionCoordinator.Read(engineId, generatedAtUtc);
        var engines = engineSession.Engines;
        var engineSelection = engineSession.Selection;
        errors.AddRange(engineSession.Diagnostics.Select(FormatEngineSessionDiagnostic));
        var harnessSummary = ReadHarnessSummary(errors);
        if (!engineSelection.IsSelected)
        {
            return CreatePendingSelectionState(
                generatedAtUtc,
                engines,
                engineSelection,
                engineSession,
                harnessSummary,
                errors);
        }

        var selectedEngineId = engineSelection.SelectedEngineId;
        var bridgeStatus = ReadBridgeStatus(selectedEngineId, errors);
        if (bridgeStatus != null
            && engineSession.Heartbeats.TryGetValue(selectedEngineId, out var sessionHeartbeat))
        {
            bridgeStatus.Heartbeat = sessionHeartbeat;
        }

        var bridgeHealth = CreateBridgeHealth(selectedEngineId, engines, bridgeStatus, generatedAtUtc);
        var selectedRegistry = engines.FirstOrDefault(
            entry => string.Equals(entry.EngineId, selectedEngineId, StringComparison.Ordinal));
        var doctorReport = bridgeStatus == null
            ? null
            : mDoctorService.AnalyzeStatus(selectedRegistry, selectedEngineId, bridgeStatus, generatedAtUtc);
        var snapshots = ReadInitialSnapshots(
            selectedEngineId,
            bridgeHealth,
            SupportsTelemetry(selectedRegistry));
        var projections = WorkbenchDashboardKitProjections.ProjectAll(
            selectedEngineId, bridgeHealth, snapshots);

        return new WorkbenchDashboardState(
            mClient.Paths.ProjectRoot,
            generatedAtUtc,
            engines,
            engineSelection,
            bridgeStatus,
            bridgeHealth,
            doctorReport,
            snapshots,
            harnessSummary,
            errors,
            projections,
            engineSession);
    }

    /// <summary>
    /// 把应用层会话诊断转换为 Dashboard 的稳定错误文本；详细证据保留在快照中。
    /// </summary>
    /// <param name="diagnostic">应用层会话诊断。</param>
    /// <returns>Dashboard 错误摘要。</returns>
    private static string FormatEngineSessionDiagnostic(EngineSessionDiagnostic diagnostic)
    {
        var engineSuffix = string.IsNullOrWhiteSpace(diagnostic.EngineId)
            ? string.Empty
            : " [" + diagnostic.EngineId + "]";
        return "engine session " + diagnostic.Code + engineSuffix + ": " + diagnostic.Message;
    }

    /// <summary>
    /// 发送 System 命令并转换为 Workbench 可显示的响应状态。
    /// </summary>
    /// <param name="engineId">目标 engine。</param>
    /// <param name="action">System action。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>命令响应状态。</returns>
    public async Task<WorkbenchCommandState> SendSystemCommandAsync(
        string engineId,
        string action,
        CancellationToken cancellationToken)
    {
        return await SendCommandAsync(engineId, "System", action, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 读取 FileBridge 队列和 heartbeat 状态；失败时记录错误。
    /// </summary>
    /// <param name="engineId">目标 engine。</param>
    /// <param name="errors">错误收集列表。</param>
    /// <returns>FileBridge 状态；失败时返回 null。</returns>
    private FileBridgeStatus? ReadBridgeStatus(string engineId, List<string> errors)
    {
        try
        {
            return mClient.ReadBridgeStatus(engineId);
        }
        catch (Exception exception)
        {
            errors.Add("bridge status: " + exception.Message);
            return null;
        }
    }

    /// <summary>
    /// 根据 registry 和 heartbeat 生成 Workbench 可直接展示的连接健康信息。
    /// </summary>
    /// <param name="engineId">当前选中 engine。</param>
    /// <param name="engines">已发现的 registry 条目。</param>
    /// <param name="bridgeStatus">FileBridge 队列和 heartbeat 状态。</param>
    /// <param name="nowUtc">本轮 dashboard 生成时间。</param>
    /// <returns>连接健康信息。</returns>
    private WorkbenchBridgeHealth CreateBridgeHealth(
        string engineId,
        IReadOnlyList<EngineRegistryEntry> engines,
        FileBridgeStatus? bridgeStatus,
        DateTimeOffset nowUtc)
    {
        var registry = engines.FirstOrDefault(entry => string.Equals(entry.EngineId, engineId, StringComparison.Ordinal));
        if (registry == null)
        {
            return CreateEngineUnregisteredHealth(engineId);
        }

        if (bridgeStatus == null)
        {
            return new WorkbenchBridgeHealth(
                WorkbenchBridgeConnectionState.Unavailable,
                "FileBridge status could not be read.",
                "Inspect the engine root and regenerate FileBridge state from the host adapter.",
                new[] { mClient.Paths.GetEngineRoot(engineId) },
                null,
                (long)HeartbeatStaleThreshold.TotalSeconds,
                registry.SessionId,
                registry.Generation,
                registry.Mode,
                0L);
        }

        if (bridgeStatus.Heartbeat == null)
        {
            return CreateHeartbeatMissingHealth(registry, bridgeStatus);
        }

        return CreateHeartbeatHealth(registry, bridgeStatus, nowUtc);
    }

    /// <summary>
    /// 创建 registry 缺失时的健康信息。
    /// </summary>
    /// <param name="engineId">当前选中 engine。</param>
    /// <returns>连接健康信息。</returns>
    private WorkbenchBridgeHealth CreateEngineUnregisteredHealth(string engineId)
    {
        return new WorkbenchBridgeHealth(
            WorkbenchBridgeConnectionState.EngineUnregistered,
            "Engine " + engineId + " is not registered.",
            "Start the target engine adapter or choose an engine from the registry.",
            new[] { mClient.Paths.EnginesRoot },
            null,
            (long)HeartbeatStaleThreshold.TotalSeconds,
            string.Empty,
            0L,
            string.Empty,
            0L);
    }

    /// <summary>
    /// 创建 heartbeat 文件缺失时的健康信息。
    /// </summary>
    /// <param name="registry">当前 engine registry。</param>
    /// <param name="bridgeStatus">FileBridge 队列状态。</param>
    /// <returns>连接健康信息。</returns>
    private WorkbenchBridgeHealth CreateHeartbeatMissingHealth(EngineRegistryEntry registry, FileBridgeStatus bridgeStatus)
    {
        return new WorkbenchBridgeHealth(
            WorkbenchBridgeConnectionState.HeartbeatMissing,
            "Heartbeat is missing for " + registry.EngineId + ".",
            "Wait for the adapter to write heartbeat, or restart the host adapter if it does not recover.",
            new[] { GetHeartbeatPath(registry.EngineId), bridgeStatus.EngineRoot },
            null,
            (long)HeartbeatStaleThreshold.TotalSeconds,
            registry.SessionId,
            registry.Generation,
            registry.Mode,
            0L);
    }

    /// <summary>
    /// 创建存在 heartbeat 时的健康信息。
    /// </summary>
    /// <param name="registry">当前 engine registry。</param>
    /// <param name="bridgeStatus">FileBridge 队列状态。</param>
    /// <param name="nowUtc">本轮 dashboard 生成时间。</param>
    /// <returns>连接健康信息。</returns>
    private static WorkbenchBridgeHealth CreateHeartbeatHealth(
        EngineRegistryEntry registry,
        FileBridgeStatus bridgeStatus,
        DateTimeOffset nowUtc)
    {
        var heartbeat = bridgeStatus.Heartbeat!;
        var ageSeconds = Math.Max(0, (long)(nowUtc.ToUniversalTime() - heartbeat.CreatedAtUtc.ToUniversalTime()).TotalSeconds);
        if (EngineHostIdentity.HasMismatch(registry, heartbeat))
        {
            return CreateIdentityMismatchHealth(registry, bridgeStatus, heartbeat, ageSeconds);
        }

        var isStale = heartbeat.IsStale(nowUtc, HeartbeatStaleThreshold);
        var state = isStale ? WorkbenchBridgeConnectionState.Stale : WorkbenchBridgeConnectionState.Online;
        var message = isStale
            ? "Heartbeat is stale for " + registry.EngineId + "."
            : "FileBridge is online for " + registry.EngineId + ".";
        var suggestion = isStale
            ? "Wait for the adapter to reconnect after Play Mode or Domain Reload, then refresh."
            : "No action needed.";
        return new WorkbenchBridgeHealth(
            state,
            message,
            suggestion,
            new[] { heartbeat.Path, bridgeStatus.EngineRoot },
            ageSeconds,
            (long)HeartbeatStaleThreshold.TotalSeconds,
            string.IsNullOrWhiteSpace(heartbeat.SessionId) ? registry.SessionId : heartbeat.SessionId,
            heartbeat.Generation != 0L ? heartbeat.Generation : registry.Generation,
            string.IsNullOrWhiteSpace(heartbeat.Mode) ? registry.Mode : heartbeat.Mode,
            heartbeat.Sequence);
    }

    /// <summary>
    /// 创建宿主身份尚未收敛时的暂时 stale 状态，并以最新 registry 身份阻止本轮读取旧 telemetry。
    /// </summary>
    /// <param name="registry">当前 engine registry。</param>
    /// <param name="bridgeStatus">FileBridge 队列与 heartbeat 状态。</param>
    /// <param name="heartbeat">尚未同步到 registry 的 heartbeat。</param>
    /// <param name="ageSeconds">heartbeat 当前年龄秒数。</param>
    /// <returns>等待下一轮刷新重新校验的健康信息。</returns>
    private static WorkbenchBridgeHealth CreateIdentityMismatchHealth(
        EngineRegistryEntry registry,
        FileBridgeStatus bridgeStatus,
        HeartbeatInfo heartbeat,
        long ageSeconds)
    {
        return new WorkbenchBridgeHealth(
            WorkbenchBridgeConnectionState.Stale,
            "Engine registry and heartbeat identities are temporarily inconsistent for " + registry.EngineId + ".",
            "Wait for the host heartbeat to publish the current session and generation, then refresh.",
            new[] { heartbeat.Path, bridgeStatus.EngineRoot },
            ageSeconds,
            (long)HeartbeatStaleThreshold.TotalSeconds,
            registry.SessionId,
            registry.Generation,
            registry.Mode,
            heartbeat.Sequence);
    }


}
