using YokiFrame.Protocol.Telemetry.SharedMemory;

namespace YokiFrame.Tooling.Application.Tests;

/// <summary>
/// 为测试 Client 统一复刻 Shared Memory 游标顺序，避免各 fake 对新帧定义产生漂移。
/// </summary>
internal static class TelemetryFrameCursorTestHelper
{
    /// <summary>
    /// 按 session/generation 内单调 sequence 过滤完整读取结果；失败结果保留给应用层处理。
    /// </summary>
    /// <param name="frame">fake 完整读取产生的帧结果。</param>
    /// <param name="afterSequence">调用方最后接受的帧序号。</param>
    /// <returns>候选帧晚于游标或读取失败时返回原结果，否则返回空。</returns>
    internal static SharedMemoryTelemetryFrameReadResult? Filter(
        SharedMemoryTelemetryFrameReadResult frame,
        long afterSequence)
    {
        var header = frame.Header;
        if (!frame.IsAccepted || header == null)
        {
            return frame;
        }

        return header.Sequence > afterSequence ? frame : null;
    }
}
