using YokiFrame.Protocol.Telemetry.SharedMemory;
using YokiFrame.Tooling.Application.Models;
using YokiFrame.Tooling.Application.Models.FsmKit;

namespace YokiFrame.Tooling.Application.Services;

/// <summary>承载 FsmKit 高频 Shared Memory 读取用例。</summary>
public sealed partial class WorkbenchDashboardService
{
    /// <summary>
    /// 只读取 FsmKit Shared Memory 最新帧，不访问 snapshot 或发送命令，供无游标调用方使用。
    /// </summary>
    /// <param name="engineId">当前选中 engine。</param>
    /// <param name="bridgeHealth">周期 dashboard 已确认的宿主身份。</param>
    /// <param name="selectedInstanceId">页面当前选择；非空时只读取该实例命名段。</param>
    /// <returns>已接受的 FsmKit telemetry；目标 segment 不可用时返回空。</returns>
    public WorkbenchFsmKitState? ReadFsmKitTelemetry(
        string engineId,
        WorkbenchBridgeHealth bridgeHealth,
        string selectedInstanceId)
    {
        return PollFsmKitTelemetry(
            engineId,
            bridgeHealth,
            selectedInstanceId,
            long.MinValue).State;
    }

    /// <summary>
    /// 只读取晚于游标的 FsmKit Shared Memory；有选择时直接读取命名实例段，不先解析总览。
    /// </summary>
    /// <param name="engineId">当前选中 engine。</param>
    /// <param name="bridgeHealth">周期 dashboard 已确认的宿主身份。</param>
    /// <param name="selectedInstanceId">页面当前选择；非空时禁止回落总览详情。</param>
    /// <param name="afterSequence">最后接受的帧序号。</param>
    /// <returns>明确区分 accepted、unchanged、retryable、unavailable 和 rejected 的轮询结果。</returns>
    public WorkbenchFsmKitTelemetryReadResult PollFsmKitTelemetry(
        string engineId,
        WorkbenchBridgeHealth bridgeHealth,
        string selectedInstanceId,
        long afterSequence)
    {
        if (string.IsNullOrWhiteSpace(engineId)
            || bridgeHealth.State != WorkbenchBridgeConnectionState.Online
            || bridgeHealth.Generation <= 0L)
        {
            return WorkbenchFsmKitTelemetryReadResult.Unavailable(
                "FsmKit telemetry requires an online engine with a valid generation.");
        }

        var name = string.IsNullOrWhiteSpace(selectedInstanceId)
            ? "state"
            : selectedInstanceId;
        return TryReadFsmKitTelemetry(
            engineId,
            name,
            bridgeHealth,
            afterSequence);
    }

    /// <summary>读取并解析一个晚于游标的 FsmKit latest frame，不执行文件或命令回落。</summary>
    /// <param name="engineId">当前选中 engine。</param>
    /// <param name="name">标准 state 或稳定 instanceId。</param>
    /// <param name="bridgeHealth">周期 dashboard 已确认的宿主身份。</param>
    /// <param name="afterSequence">最后接受的帧序号。</param>
    /// <returns>区分新帧、未变化、命名段不可用和内容被拒绝的读取结果。</returns>
    private WorkbenchFsmKitTelemetryReadResult TryReadFsmKitTelemetry(
        string engineId,
        string name,
        WorkbenchBridgeHealth bridgeHealth,
        long afterSequence)
    {
        var telemetry = mClient.ReadTelemetryIfChanged(
            engineId,
            FSM_KIT,
            name,
            bridgeHealth.Generation,
            SharedMemoryTelemetryFrameReader.DEFAULT_MAX_PAYLOAD_BYTES,
            afterSequence);
        if (telemetry == null)
        {
            return WorkbenchFsmKitTelemetryReadResult.Unchanged();
        }

        if (!telemetry.IsAccepted)
        {
            return MapRejectedFsmTelemetryRead(telemetry);
        }

        var acceptedHeader = telemetry.Header
            ?? throw new InvalidOperationException("Accepted telemetry must contain a header.");

        WorkbenchFsmKitState state;
        try
        {
            state = ParseFsmKitTelemetry(engineId, name, bridgeHealth, telemetry);
        }
        catch (Exception exception)
        {
            return WorkbenchFsmKitTelemetryReadResult.RejectedWithTrustedCursor(
                acceptedHeader,
                "FsmKit telemetry payload could not be parsed: " + exception.Message);
        }

        if (!string.Equals(name, "state", StringComparison.Ordinal)
            && !string.Equals(state.Selected?.InstanceId, name, StringComparison.Ordinal))
        {
            return WorkbenchFsmKitTelemetryReadResult.RejectedWithTrustedCursor(
                acceptedHeader,
                "FsmKit named telemetry selected.instanceId does not match the segment name.");
        }

        return WorkbenchFsmKitTelemetryReadResult.Accepted(state, acceptedHeader);
    }

    /// <summary>映射底层未接受状态；协议坏帧不得暴露尚未经过 parser 信任的 header 游标。</summary>
    /// <param name="telemetry">底层 Shared Memory 读取结果。</param>
    /// <returns>瞬态、不可用或不可信拒绝结果。</returns>
    private static WorkbenchFsmKitTelemetryReadResult MapRejectedFsmTelemetryRead(
        SharedMemoryTelemetryFrameReadResult telemetry)
    {
        if (telemetry.Status is SharedMemoryTelemetryFrameStatus.Writing
            or SharedMemoryTelemetryFrameStatus.HalfWrite)
        {
            return WorkbenchFsmKitTelemetryReadResult.Retryable(telemetry.Message);
        }

        if (telemetry.Header == null
            || telemetry.Status is SharedMemoryTelemetryFrameStatus.Unavailable
                or SharedMemoryTelemetryFrameStatus.GenerationMismatch)
        {
            return WorkbenchFsmKitTelemetryReadResult.Unavailable(telemetry.Message);
        }

        return WorkbenchFsmKitTelemetryReadResult.Rejected(telemetry.Message);
    }

    /// <summary>把已校验 payload 投影为强类型状态，并保留实际 Shared Memory segment 证据。</summary>
    /// <param name="engineId">当前选中 engine。</param>
    /// <param name="name">标准 state 或稳定 instanceId。</param>
    /// <param name="bridgeHealth">周期 dashboard 已确认的宿主身份。</param>
    /// <param name="telemetry">已接受且包含 header 的读取结果。</param>
    /// <returns>FsmKit 强类型状态。</returns>
    private WorkbenchFsmKitState ParseFsmKitTelemetry(
        string engineId,
        string name,
        WorkbenchBridgeHealth bridgeHealth,
        SharedMemoryTelemetryFrameReadResult telemetry)
    {
        WorkbenchFsmKitDataSource dataSource = new(
            engineId,
            bridgeHealth.SessionId,
            bridgeHealth.Generation,
            bridgeHealth.Mode,
            ReadTelemetryUpdatedAtUtc(telemetry.Header) ?? DateTimeOffset.MinValue,
            "telemetry",
            string.Empty,
            new[] { SharedMemoryTelemetrySegmentName.Create(mClient.Paths.ProjectRoot, engineId, FSM_KIT, name) },
            string.Empty,
            telemetry.PayloadJson);
        return WorkbenchFsmKitStateParser.Parse(dataSource);
    }
}
