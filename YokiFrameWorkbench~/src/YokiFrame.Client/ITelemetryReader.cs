using YokiFrame.Client.Telemetry.SharedMemory;
using YokiFrame.Protocol.Telemetry.SharedMemory;

namespace YokiFrame.Client;

/// <summary>
/// 提供 Shared Memory latest-state telemetry 的窄读取端口。
/// </summary>
public interface ITelemetryReader
{
    /// <summary>读取指定 telemetry 目标的最新帧。</summary>
    SharedMemoryTelemetryFrameReadResult ReadTelemetry(
        string engineId,
        string kit,
        string name,
        long? expectedGeneration,
        int maxPayloadBytes);

    /// <summary>读取晚于指定序号的新帧；未变化时返回空。</summary>
    SharedMemoryTelemetryFrameReadResult? ReadTelemetryIfChanged(
        string engineId,
        string kit,
        string name,
        long? expectedGeneration,
        int maxPayloadBytes,
        long afterSequence);

    /// <summary>尝试打开指定 engine 的 telemetry 变化通知 listener。</summary>
    SharedMemoryTelemetryNotificationListener? CreateTelemetryNotificationListener(string engineId)
    {
        return null;
    }
}
