using YokiFrame.Protocol.Telemetry.SharedMemory;
using YokiFrame.Tooling.Application.Models;
using YokiFrame.Tooling.Application.Models.Telemetry;
using YokiFrame.Tooling.Application.Models.LogKit;

namespace YokiFrame.Tooling.Application.Services;

/// <summary>承载 LogKit 高频 Shared Memory 读取用例。</summary>
public sealed partial class WorkbenchDashboardService
{
    /// <summary>只读取晚于游标的 LogKit/state，不访问文件或发送命令。</summary>
    /// <param name="engineId">目标 engine。</param>
    /// <param name="bridgeHealth">当前已确认宿主身份。</param>
    /// <param name="afterSequence">调用方最后处理的 telemetry sequence。</param>
    /// <returns>新状态、无变化或明确拒绝类别。</returns>
    public WorkbenchTelemetryReadResult<WorkbenchLogKitState> PollLogKitTelemetry(
        string engineId,
        WorkbenchBridgeHealth bridgeHealth,
        long afterSequence)
    {
        if (string.IsNullOrWhiteSpace(engineId)
            || bridgeHealth.State != WorkbenchBridgeConnectionState.Online
            || string.IsNullOrWhiteSpace(bridgeHealth.SessionId)
            || bridgeHealth.Generation <= 0L)
        {
            return WorkbenchTelemetryReadResult<WorkbenchLogKitState>.Unavailable(
                "LogKit telemetry requires an online engine with a confirmed session and generation.");
        }

        var telemetry = mClient.ReadTelemetryIfChanged(
            engineId,
            LOG_KIT,
            "state",
            bridgeHealth.Generation,
            SharedMemoryTelemetryFrameReader.DEFAULT_MAX_PAYLOAD_BYTES,
            afterSequence);
        if (telemetry == null)
        {
            return WorkbenchTelemetryReadResult<WorkbenchLogKitState>.Unchanged();
        }

        return telemetry.IsAccepted
            ? ParseAcceptedLogKitTelemetry(engineId, bridgeHealth, telemetry)
            : MapRejectedLogKitTelemetry(telemetry);
    }

    /// <summary>解析协议已接受帧，并把 JSON 失败转换为可信游标拒绝。</summary>
    private WorkbenchTelemetryReadResult<WorkbenchLogKitState> ParseAcceptedLogKitTelemetry(
        string engineId,
        WorkbenchBridgeHealth bridgeHealth,
        SharedMemoryTelemetryFrameReadResult telemetry)
    {
        var header = telemetry.Header
            ?? throw new InvalidOperationException("Accepted telemetry must contain a header.");
        try
        {
            WorkbenchLogKitDataSource source = new(
                engineId,
                bridgeHealth.SessionId,
                bridgeHealth.Generation,
                bridgeHealth.Mode,
                ReadTelemetryUpdatedAtUtc(header) ?? DateTimeOffset.MinValue,
                "telemetry",
                string.Empty,
                new[]
                {
                    SharedMemoryTelemetrySegmentName.Create(
                        mClient.Paths.ProjectRoot,
                        engineId,
                        LOG_KIT,
                        "state")
                },
                string.Empty,
                telemetry.PayloadJson);
            var state = WorkbenchLogKitStateParser.Parse(source);
            return string.IsNullOrWhiteSpace(state.StaleReason)
                ? WorkbenchTelemetryReadResult<WorkbenchLogKitState>.Accepted(state, header)
                : WorkbenchTelemetryReadResult<WorkbenchLogKitState>.RejectedWithTrustedCursor(header, state.StaleReason);
        }
        catch (Exception exception)
        {
            return WorkbenchTelemetryReadResult<WorkbenchLogKitState>.RejectedWithTrustedCursor(
                header,
                "LogKit telemetry payload could not be parsed: " + exception.Message);
        }
    }

    /// <summary>映射半写、不可用和协议拒绝状态。</summary>
    private static WorkbenchTelemetryReadResult<WorkbenchLogKitState> MapRejectedLogKitTelemetry(
        SharedMemoryTelemetryFrameReadResult telemetry)
    {
        if (telemetry.Status is SharedMemoryTelemetryFrameStatus.Writing
            or SharedMemoryTelemetryFrameStatus.HalfWrite)
        {
            return WorkbenchTelemetryReadResult<WorkbenchLogKitState>.Retryable(telemetry.Message);
        }

        if (telemetry.Header == null
            || telemetry.Status is SharedMemoryTelemetryFrameStatus.Unavailable
                or SharedMemoryTelemetryFrameStatus.GenerationMismatch)
        {
            return WorkbenchTelemetryReadResult<WorkbenchLogKitState>.Unavailable(telemetry.Message);
        }

        return WorkbenchTelemetryReadResult<WorkbenchLogKitState>.Rejected(telemetry.Message);
    }
}
