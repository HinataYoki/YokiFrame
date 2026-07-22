using YokiFrame;

namespace YokiFrame.Protocol.Telemetry.SharedMemory;

/// <summary>
/// 提供 Shared Memory telemetry payload 使用的 IEEE CRC32 校验。
/// </summary>
public static class SharedMemoryTelemetryCrc32
{
    /// <summary>
    /// 计算 payload 的 CRC32，用于发现半写帧或损坏 payload。
    /// </summary>
    /// <param name="payload">待校验 payload 字节。</param>
    /// <returns>IEEE CRC32 校验值。</returns>
    public static uint Compute(ReadOnlySpan<byte> payload)
    {
        return YokiFrameSharedMemoryTelemetryCrc32.Compute(payload);
    }
}
