namespace YokiFrame.Protocol.Telemetry.SharedMemory;

/// <summary>
/// 表示 Shared Memory telemetry 帧读取结果。
/// </summary>
public sealed class SharedMemoryTelemetryFrameReadResult
{
    /// <summary>
    /// 创建帧读取结果。
    /// </summary>
    /// <param name="status">读取状态。</param>
    /// <param name="header">已解析 header；header 无法读取时为 null。</param>
    /// <param name="payloadJson">已接受 payload JSON；失败时为空。</param>
    /// <param name="message">面向诊断的失败或成功说明。</param>
    public SharedMemoryTelemetryFrameReadResult(
        SharedMemoryTelemetryFrameStatus status,
        SharedMemoryTelemetryFrameHeader? header,
        string payloadJson,
        string message)
    {
        Status = status;
        Header = header;
        PayloadJson = payloadJson;
        Message = message;
    }

    /// <summary>
    /// 获取读取状态。
    /// </summary>
    public SharedMemoryTelemetryFrameStatus Status { get; }

    /// <summary>
    /// 获取已解析 header；header 无法读取时为 null。
    /// </summary>
    public SharedMemoryTelemetryFrameHeader? Header { get; }

    /// <summary>
    /// 获取已接受 payload JSON；失败时为空。
    /// </summary>
    public string PayloadJson { get; }

    /// <summary>
    /// 获取诊断消息。
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// 获取当前结果是否为可使用 telemetry 帧。
    /// </summary>
    public bool IsAccepted => Status == SharedMemoryTelemetryFrameStatus.Accepted;
}
