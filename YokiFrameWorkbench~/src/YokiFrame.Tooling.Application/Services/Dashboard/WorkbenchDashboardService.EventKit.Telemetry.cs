using YokiFrame.Protocol.Telemetry.SharedMemory;
using YokiFrame.Tooling.Application.Models;
using YokiFrame.Tooling.Application.Models.EventKit;

namespace YokiFrame.Tooling.Application.Services;

/// <summary>承载 EventKit 高频 Shared Memory 读取用例。</summary>
public sealed partial class WorkbenchDashboardService
{
    /// <summary>只读取晚于游标的 EventKit/state，不访问文件或发送命令。</summary>
    public WorkbenchEventKitTelemetryReadResult PollEventKitTelemetry(
        string engineId,
        WorkbenchBridgeHealth bridgeHealth,
        long afterSequence)
    {
        if (string.IsNullOrWhiteSpace(engineId)
            || bridgeHealth.State != WorkbenchBridgeConnectionState.Online
            || bridgeHealth.Generation <= 0L)
        {
            return WorkbenchEventKitTelemetryReadResult.Unavailable(
                "EventKit telemetry requires an online engine with a valid generation.");
        }

        var telemetry = mClient.ReadTelemetryIfChanged(
            engineId,
            EVENT_KIT,
            "state",
            bridgeHealth.Generation,
            SharedMemoryTelemetryFrameReader.DEFAULT_MAX_PAYLOAD_BYTES,
            afterSequence);
        if (telemetry == null)
        {
            return WorkbenchEventKitTelemetryReadResult.Unchanged();
        }

        return telemetry.IsAccepted
            ? ParseAcceptedEventKitTelemetry(engineId, bridgeHealth, telemetry)
            : MapRejectedEventKitTelemetry(telemetry);
    }

    /// <summary>解析协议已接受的 EventKit 帧，并把 JSON 失败转成可信游标拒绝。</summary>
    private WorkbenchEventKitTelemetryReadResult ParseAcceptedEventKitTelemetry(
        string engineId,
        WorkbenchBridgeHealth bridgeHealth,
        SharedMemoryTelemetryFrameReadResult telemetry)
    {
        SharedMemoryTelemetryFrameHeader header = telemetry.Header
            ?? throw new InvalidOperationException("Accepted telemetry must contain a header.");
        try
        {
            WorkbenchEventKitDataSource dataSource = new(
                engineId,
                bridgeHealth.SessionId,
                bridgeHealth.Generation,
                bridgeHealth.Mode,
                ReadTelemetryUpdatedAtUtc(header) ?? DateTimeOffset.MinValue,
                "telemetry",
                new[]
                {
                    SharedMemoryTelemetrySegmentName.Create(
                        mClient.Paths.ProjectRoot,
                        engineId,
                        EVENT_KIT,
                        "state")
                },
                string.Empty,
                telemetry.PayloadJson);
            WorkbenchEventKitState state = WorkbenchEventKitStateParser.Parse(dataSource);
            return string.IsNullOrWhiteSpace(state.StaleReason)
                ? WorkbenchEventKitTelemetryReadResult.Accepted(state, header)
                : WorkbenchEventKitTelemetryReadResult.RejectedWithTrustedCursor(
                    header,
                    state.StaleReason);
        }
        catch (Exception exception)
        {
            return WorkbenchEventKitTelemetryReadResult.RejectedWithTrustedCursor(
                header,
                "EventKit telemetry payload could not be parsed: " + exception.Message);
        }
    }

    /// <summary>映射底层半写、不可用和协议拒绝状态。</summary>
    private static WorkbenchEventKitTelemetryReadResult MapRejectedEventKitTelemetry(
        SharedMemoryTelemetryFrameReadResult telemetry)
    {
        if (telemetry.Status is SharedMemoryTelemetryFrameStatus.Writing
            or SharedMemoryTelemetryFrameStatus.HalfWrite)
        {
            return WorkbenchEventKitTelemetryReadResult.Retryable(telemetry.Message);
        }

        if (telemetry.Header == null
            || telemetry.Status is SharedMemoryTelemetryFrameStatus.Unavailable
                or SharedMemoryTelemetryFrameStatus.GenerationMismatch)
        {
            return WorkbenchEventKitTelemetryReadResult.Unavailable(telemetry.Message);
        }

        return WorkbenchEventKitTelemetryReadResult.Rejected(telemetry.Message);
    }
}
