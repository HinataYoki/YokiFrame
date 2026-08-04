namespace YokiFrame.Protocol.Telemetry.SharedMemory;

/// <summary>
/// 表示 Shared Memory telemetry 帧读取结果，Workbench 可据此决定使用 telemetry 还是回落 snapshot。
/// </summary>
public enum SharedMemoryTelemetryFrameStatus
{
    /// <summary>
    /// 帧已通过 header、generation、payload size 和 CRC 校验。
    /// </summary>
    Accepted,

    /// <summary>
    /// OS shared memory segment 不存在或当前平台不支持该通道。
    /// </summary>
    Unavailable,

    /// <summary>
    /// header 或 payload 缓冲区不足，不能安全读取。
    /// </summary>
    BufferTooSmall,

    /// <summary>
    /// magic 不匹配，当前内存段不是 YokiFrame telemetry 帧。
    /// </summary>
    InvalidMagic,

    /// <summary>
    /// protocolVersion 不受当前 reader 支持。
    /// </summary>
    UnsupportedVersion,

    /// <summary>
    /// writer 正在写入，reader 应跳过当前帧并等待下一次刷新。
    /// </summary>
    Writing,

    /// <summary>
    /// 两次读取到的 header 不一致，说明发生半写帧或并发更新。
    /// </summary>
    HalfWrite,

    /// <summary>
    /// 帧中的 engineIdHash 与请求 engine 不一致，不能把该段内容归属于当前宿主。
    /// </summary>
    EngineIdHashMismatch,

    /// <summary>
    /// 帧 generation 与当前 engine generation 不一致，需要重新映射或回落 registry。
    /// </summary>
    GenerationMismatch,

    /// <summary>
    /// payloadLength 为负数或超过 reader 允许上限。
    /// </summary>
    PayloadTooLarge,

    /// <summary>
    /// payload CRC32 校验失败，reader 不应使用该帧。
    /// </summary>
    CrcMismatch,

    /// <summary>
    /// payload 不是合法 UTF-8，reader 不应接受替换字符后的伪数据。
    /// </summary>
    InvalidUtf8
}
