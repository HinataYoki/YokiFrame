using YokiFrame;

namespace YokiFrame.Protocol.Telemetry.SharedMemory;

/// <summary>
/// 表示 Shared Memory telemetry 帧当前写入状态，reader 只接受已提交帧。
/// </summary>
public enum SharedMemoryTelemetryWriteState
{
    /// <summary>
    /// 未写入或保留状态，reader 会把它视为不可用帧。
    /// </summary>
    Empty = YokiFrameSharedMemoryTelemetryContract.WRITE_STATE_EMPTY,

    /// <summary>
    /// writer 正在更新 header 或 payload，reader 必须跳过本帧。
    /// </summary>
    Writing = YokiFrameSharedMemoryTelemetryContract.WRITE_STATE_WRITING,

    /// <summary>
    /// writer 已完成 payload、CRC 和 header 提交，reader 可以继续校验。
    /// </summary>
    Committed = YokiFrameSharedMemoryTelemetryContract.WRITE_STATE_COMMITTED
}
