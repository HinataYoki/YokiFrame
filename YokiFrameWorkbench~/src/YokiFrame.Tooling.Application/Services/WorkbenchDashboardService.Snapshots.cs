using System.Text.Json;
using System.Text.Json.Nodes;
using YokiFrame.Client;
using YokiFrame.Protocol.Common;
using YokiFrame.Protocol.FileBridge;
using YokiFrame.Protocol.Results;
using YokiFrame.Protocol.Telemetry.SharedMemory;
using YokiFrame.Tooling.Application.Models;

namespace YokiFrame.Tooling.Application.Services;

/// <summary>读取 Dashboard 的 snapshot 与 Shared Memory telemetry 数据。</summary>
public sealed partial class WorkbenchDashboardService
{
    /// <summary>
    /// 读取 Phase 2 首批页面的 state 数据；只有 registry 声明 telemetry.read 时才优先读取 telemetry，失败后回落 snapshot。
    /// </summary>
    /// <param name="engineId">目标 engine。</param>
    /// <param name="bridgeHealth">当前 bridge 健康信息，用于获取 generation。</param>
    /// <param name="telemetryEnabled">当前 registry 是否明确允许读取 telemetry。</param>
    /// <returns>snapshot 状态列表。</returns>
    private IReadOnlyList<WorkbenchSnapshotState> ReadInitialSnapshots(
        string engineId,
        WorkbenchBridgeHealth bridgeHealth,
        bool telemetryEnabled)
    {
        List<WorkbenchSnapshotState> snapshots = new();
        snapshots.Add(ReadKitState(engineId, "System", "state", bridgeHealth, telemetryEnabled));
        foreach (var kit in WorkbenchRuntimeKitCatalog.SnapshotStateKits)
        {
            snapshots.Add(ReadKitState(
                engineId,
                kit,
                "state",
                bridgeHealth,
                telemetryEnabled && WorkbenchRuntimeKitCatalog.UsesTelemetryState(kit)));
        }

        return snapshots;
    }
    /// <summary>
    /// 读取 Kit/state；registry 声明实时能力且 bridge 在线时优先使用 Shared Memory Telemetry，其他情况直接回落 snapshot。
    /// </summary>
    /// <param name="engineId">目标 engine。</param>
    /// <param name="kit">Kit 名称。</param>
    /// <param name="name">state 名称。</param>
    /// <param name="bridgeHealth">当前 bridge 健康信息。</param>
    /// <param name="telemetryEnabled">当前 registry 是否明确允许读取 telemetry。</param>
    /// <returns>Kit/state 状态。</returns>
    private WorkbenchSnapshotState ReadKitState(
        string engineId,
        string kit,
        string name,
        WorkbenchBridgeHealth bridgeHealth,
        bool telemetryEnabled)
    {
        if (!telemetryEnabled
            || bridgeHealth.State != WorkbenchBridgeConnectionState.Online
            || bridgeHealth.Generation == 0L)
        {
            var staleReason = bridgeHealth.State == WorkbenchBridgeConnectionState.Online
                ? string.Empty
                : bridgeHealth.Message;
            return ReadSnapshot(engineId, kit, name, "snapshot", staleReason, bridgeHealth.Generation, null);
        }
        var generation = bridgeHealth.Generation != 0L ? bridgeHealth.Generation : (long?)null;
        var telemetry = mClient.ReadTelemetry(
            engineId,
            kit,
            name,
            generation,
            SharedMemoryTelemetryFrameReader.DEFAULT_MAX_PAYLOAD_BYTES);
        var telemetryEvidence = SharedMemoryTelemetrySegmentName.Create(mClient.Paths.ProjectRoot, engineId, kit, name);
        if (telemetry.IsAccepted)
        {
            return new WorkbenchSnapshotState(
                kit,
                name,
                telemetryEvidence,
                "telemetry",
                true,
                TrimText(telemetry.PayloadJson, 900),
                string.Empty,
                telemetry.PayloadJson,
                ReadTelemetryUpdatedAtUtc(telemetry.Header));
        }
        return ReadSnapshot(
            engineId,
            kit,
            name,
            "snapshot",
            telemetry.Message,
            bridgeHealth.Generation,
            new[] { telemetryEvidence });
    }
    /// <summary>
    /// 判断当前 registry 是否明确声明 Shared Memory Telemetry；缺失 capability 时不打开 map，避免把回退路径当作常规轮询。
    /// </summary>
    /// <param name="registry">当前选中 engine 的 registry；未注册时为空。</param>
    /// <returns>仅当 capability 列表包含精确的 telemetry.read 时返回 true。</returns>
    private static bool SupportsTelemetry(EngineRegistryEntry? registry)
    {
        return registry != null
            && registry.Capabilities.Any(static capability => string.Equals(
                capability,
                "telemetry.read",
                StringComparison.Ordinal));
    }
    /// <summary>
    /// 读取单个 snapshot 并标注数据源。
    /// </summary>
    /// <param name="engineId">目标 engine。</param>
    /// <param name="kit">Kit 名称。</param>
    /// <param name="name">snapshot 名称。</param>
    /// <param name="source">数据来源标识。</param>
    /// <param name="staleReason">回落到 snapshot 的原因。</param>
    /// <param name="expectedGeneration">当前 bridge 已确认的 generation；为 0 时只要求信封自身有效。</param>
    /// <param name="priorEvidencePaths">本次回落前已尝试的数据源证据。</param>
    /// <returns>snapshot 状态。</returns>
    private WorkbenchSnapshotState ReadSnapshot(
        string engineId,
        string kit,
        string name,
        string source,
        string staleReason,
        long expectedGeneration,
        IReadOnlyList<string>? priorEvidencePaths)
    {
        var path = mClient.Paths.GetSnapshotPath(engineId, kit, name);
        var evidencePaths = CreateSnapshotEvidencePaths(path, priorEvidencePaths);
        try
        {
            var node = mClient.ReadSnapshot(engineId, kit, name);
            var envelope = ReadSnapshotEnvelope(node, engineId, kit, name, expectedGeneration);
            var reason = CombineStaleReasons(staleReason, envelope.StaleReason);
            return new WorkbenchSnapshotState(
                kit,
                name,
                path,
                source,
                envelope.IsReadable,
                TrimText(envelope.PreviewJson, 900),
                envelope.IsReadable ? string.Empty : envelope.StaleReason,
                envelope.PayloadJson,
                envelope.UpdatedAtUtc,
                reason,
                evidencePaths);
        }
        catch (YokiFrameProtocolException exception)
        {
            var reason = CombineStaleReasons(staleReason, exception.Error.Message);
            return new WorkbenchSnapshotState(
                kit,
                name,
                path,
                source,
                false,
                string.Empty,
                exception.Error.Message,
                staleReason: reason,
                evidencePaths: evidencePaths);
        }
        catch (Exception exception)
        {
            var reason = CombineStaleReasons(staleReason, exception.Message);
            return new WorkbenchSnapshotState(
                kit,
                name,
                path,
                source,
                false,
                string.Empty,
                exception.Message,
                staleReason: reason,
                evidencePaths: evidencePaths);
        }
    }
    /// <summary>
    /// 读取 harness capabilities 并裁剪为首屏摘要。
    /// </summary>
    /// <param name="errors">错误收集列表。</param>
    /// <returns>harness 摘要。</returns>
    private string ReadHarnessSummary(List<string> errors)
    {
        try
        {
            var node = mClient.ReadHarnessCapabilities();
            return TrimText(node.ToJsonString(YokiFrameJson.CompactOptions), 700);
        }
        catch (Exception exception)
        {
            errors.Add("harness status: " + exception.Message);
            return "unavailable";
        }
    }
    /// <summary>
    /// 裁剪长文本，避免首屏状态区域被大 JSON 撑开。
    /// </summary>
    /// <param name="text">待裁剪文本。</param>
    /// <param name="maxLength">最大长度。</param>
    /// <returns>裁剪后的文本。</returns>
    private static string TrimText(string text, int maxLength)
    {
        if (text.Length <= maxLength)
        {
            return text;
        }
        return text[..maxLength] + "...";
    }
    /// <summary>
    /// 获取指定 engine 的 heartbeat 文件路径，供缺失状态作为证据路径展示。
    /// </summary>
    /// <param name="engineId">目标 engine。</param>
    /// <returns>heartbeat 文件路径。</returns>
    private string GetHeartbeatPath(string engineId)
    {
        return mClient.Paths.GetHeartbeatPath(engineId);
    }
}
