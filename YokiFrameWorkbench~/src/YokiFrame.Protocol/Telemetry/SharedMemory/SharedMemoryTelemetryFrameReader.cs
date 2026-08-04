using System.Text;
using YokiFrame;

namespace YokiFrame.Protocol.Telemetry.SharedMemory;

/// <summary>
/// 读取和校验 Shared Memory telemetry v1 帧。
/// </summary>
public static class SharedMemoryTelemetryFrameReader
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    /// <summary>
    /// 默认 payload 上限，避免 Workbench 刷新循环接受异常大帧。
    /// </summary>
    public const int DEFAULT_MAX_PAYLOAD_BYTES = YokiFrameSharedMemoryTelemetryContract.DEFAULT_MAX_PAYLOAD_BYTES;

    /// <summary>
    /// 从同一份稳定帧快照读取 telemetry；适用于测试和已复制出的内存段。
    /// </summary>
    /// <param name="frameBytes">包含 header 和 payload 的帧字节。</param>
    /// <param name="expectedGeneration">期望 generation；为 null 时不校验。</param>
    /// <param name="maxPayloadBytes">允许读取的 payload 最大字节数。</param>
    /// <param name="expectedEngineIdHash">期望 engineIdHash；为 null 时不校验。</param>
    /// <returns>帧读取结果。</returns>
    public static SharedMemoryTelemetryFrameReadResult ReadCommittedFrame(
        ReadOnlySpan<byte> frameBytes,
        long? expectedGeneration,
        int maxPayloadBytes = DEFAULT_MAX_PAYLOAD_BYTES,
        ulong? expectedEngineIdHash = null)
    {
        if (frameBytes.Length < SharedMemoryTelemetryFrameHeader.HEADER_SIZE)
        {
            return Failure(SharedMemoryTelemetryFrameStatus.BufferTooSmall, null, "Frame buffer is smaller than header.");
        }

        var headerBytes = frameBytes[..SharedMemoryTelemetryFrameHeader.HEADER_SIZE];
        var payloadBytes = frameBytes[YokiFrameSharedMemoryTelemetryContract.PAYLOAD_OFFSET..];
        return ReadFrame(
            headerBytes,
            payloadBytes,
            headerBytes,
            expectedGeneration,
            maxPayloadBytes,
            expectedEngineIdHash);
    }

    /// <summary>
    /// 使用前后两次 header 快照和 payload 快照读取 telemetry，检测并发写入造成的半写帧。
    /// </summary>
    /// <param name="firstHeaderBytes">读取 payload 前的 header 快照。</param>
    /// <param name="payloadBytes">payload 快照。</param>
    /// <param name="secondHeaderBytes">读取 payload 后的 header 快照。</param>
    /// <param name="expectedGeneration">期望 generation；为 null 时不校验。</param>
    /// <param name="maxPayloadBytes">允许读取的 payload 最大字节数。</param>
    /// <param name="expectedEngineIdHash">期望 engineIdHash；为 null 时不校验。</param>
    /// <returns>帧读取结果。</returns>
    public static SharedMemoryTelemetryFrameReadResult ReadFrame(
        ReadOnlySpan<byte> firstHeaderBytes,
        ReadOnlySpan<byte> payloadBytes,
        ReadOnlySpan<byte> secondHeaderBytes,
        long? expectedGeneration,
        int maxPayloadBytes = DEFAULT_MAX_PAYLOAD_BYTES,
        ulong? expectedEngineIdHash = null)
    {
        if (!TryReadHeaders(firstHeaderBytes, secondHeaderBytes, out var firstHeader, out var secondHeader, out var failure))
        {
            return failure;
        }

        var firstFailure = ValidateHeader(
            firstHeader,
            expectedGeneration,
            maxPayloadBytes,
            expectedEngineIdHash);
        if (firstFailure != null)
        {
            return firstFailure;
        }

        if (!AreHeadersStable(firstHeader, secondHeader))
        {
            return Failure(SharedMemoryTelemetryFrameStatus.HalfWrite, firstHeader, "Header changed while payload was being read.");
        }

        if (payloadBytes.Length < firstHeader.PayloadLength)
        {
            return Failure(SharedMemoryTelemetryFrameStatus.BufferTooSmall, firstHeader, "Payload buffer is smaller than payloadLength.");
        }

        var payload = payloadBytes[..firstHeader.PayloadLength];
        if (SharedMemoryTelemetryCrc32.Compute(payload) != firstHeader.PayloadCrc32)
        {
            return Failure(SharedMemoryTelemetryFrameStatus.CrcMismatch, firstHeader, "Payload CRC32 does not match header.");
        }

        try
        {
            return new SharedMemoryTelemetryFrameReadResult(
                SharedMemoryTelemetryFrameStatus.Accepted,
                firstHeader,
                StrictUtf8.GetString(payload),
                "Telemetry frame accepted.");
        }
        catch (DecoderFallbackException)
        {
            return Failure(
                SharedMemoryTelemetryFrameStatus.InvalidUtf8,
                firstHeader,
                "Telemetry payload is not valid UTF-8.");
        }
    }

    /// <summary>
    /// 读取前后两份 header，并处理 header 缓冲区不足的情况。
    /// </summary>
    /// <param name="firstHeaderBytes">第一次 header 快照。</param>
    /// <param name="secondHeaderBytes">第二次 header 快照。</param>
    /// <param name="firstHeader">解析出的第一次 header。</param>
    /// <param name="secondHeader">解析出的第二次 header。</param>
    /// <param name="failure">失败结果。</param>
    /// <returns>两份 header 都可读取时返回 true。</returns>
    private static bool TryReadHeaders(
        ReadOnlySpan<byte> firstHeaderBytes,
        ReadOnlySpan<byte> secondHeaderBytes,
        out SharedMemoryTelemetryFrameHeader firstHeader,
        out SharedMemoryTelemetryFrameHeader secondHeader,
        out SharedMemoryTelemetryFrameReadResult failure)
    {
        firstHeader = null!;
        secondHeader = null!;
        failure = null!;
        if (firstHeaderBytes.Length < SharedMemoryTelemetryFrameHeader.HEADER_SIZE
            || secondHeaderBytes.Length < SharedMemoryTelemetryFrameHeader.HEADER_SIZE)
        {
            failure = Failure(SharedMemoryTelemetryFrameStatus.BufferTooSmall, null, "Header buffer is too small.");
            return false;
        }

        firstHeader = SharedMemoryTelemetryFrameHeader.Read(firstHeaderBytes);
        secondHeader = SharedMemoryTelemetryFrameHeader.Read(secondHeaderBytes);
        return true;
    }

    /// <summary>
    /// 校验单份 header 是否满足 reader 基础约束。
    /// </summary>
    /// <param name="header">待校验 header。</param>
    /// <param name="expectedGeneration">期望 generation。</param>
    /// <param name="maxPayloadBytes">payload 最大字节数。</param>
    /// <param name="expectedEngineIdHash">期望 engineIdHash。</param>
    /// <returns>失败结果；通过时返回 null。</returns>
    private static SharedMemoryTelemetryFrameReadResult? ValidateHeader(
        SharedMemoryTelemetryFrameHeader header,
        long? expectedGeneration,
        int maxPayloadBytes,
        ulong? expectedEngineIdHash)
    {
        if (header.Magic != SharedMemoryTelemetryFrameHeader.MAGIC)
        {
            return Failure(SharedMemoryTelemetryFrameStatus.InvalidMagic, header, "Telemetry magic does not match.");
        }

        if (header.ProtocolVersion != SharedMemoryTelemetryFrameHeader.PROTOCOL_VERSION)
        {
            return Failure(SharedMemoryTelemetryFrameStatus.UnsupportedVersion, header, "Telemetry protocolVersion is unsupported.");
        }

        if (header.WriteState != SharedMemoryTelemetryWriteState.Committed)
        {
            return Failure(SharedMemoryTelemetryFrameStatus.Writing, header, "Telemetry frame is not committed.");
        }

        if (expectedEngineIdHash.HasValue && header.EngineIdHash != expectedEngineIdHash.Value)
        {
            return Failure(
                SharedMemoryTelemetryFrameStatus.EngineIdHashMismatch,
                header,
                "Telemetry engineIdHash does not match the requested engine.");
        }

        if (expectedGeneration.HasValue && header.Generation != expectedGeneration.Value)
        {
            return Failure(SharedMemoryTelemetryFrameStatus.GenerationMismatch, header, "Telemetry generation does not match engine registry.");
        }

        return ValidatePayloadLength(header, maxPayloadBytes);
    }

    /// <summary>
    /// 校验 payload 长度是否处于 reader 允许范围。
    /// </summary>
    /// <param name="header">待校验 header。</param>
    /// <param name="maxPayloadBytes">payload 最大字节数。</param>
    /// <returns>失败结果；通过时返回 null。</returns>
    private static SharedMemoryTelemetryFrameReadResult? ValidatePayloadLength(
        SharedMemoryTelemetryFrameHeader header,
        int maxPayloadBytes)
    {
        if (header.PayloadLength < 0 || header.PayloadLength > maxPayloadBytes)
        {
            return Failure(SharedMemoryTelemetryFrameStatus.PayloadTooLarge, header, "Telemetry payloadLength exceeds reader limit.");
        }

        return null;
    }

    /// <summary>
    /// 判断读取 payload 前后的关键 header 字段是否稳定。
    /// </summary>
    /// <param name="firstHeader">第一次 header 快照。</param>
    /// <param name="secondHeader">第二次 header 快照。</param>
    /// <returns>关键字段一致时返回 true。</returns>
    private static bool AreHeadersStable(
        SharedMemoryTelemetryFrameHeader firstHeader,
        SharedMemoryTelemetryFrameHeader secondHeader)
    {
        return firstHeader.Magic == secondHeader.Magic
            && firstHeader.ProtocolVersion == secondHeader.ProtocolVersion
            && firstHeader.EngineIdHash == secondHeader.EngineIdHash
            && firstHeader.Generation == secondHeader.Generation
            && firstHeader.Sequence == secondHeader.Sequence
            && firstHeader.WrittenAtUtcTicks == secondHeader.WrittenAtUtcTicks
            && firstHeader.PayloadLength == secondHeader.PayloadLength
            && firstHeader.PayloadCrc32 == secondHeader.PayloadCrc32
            && firstHeader.WriteState == secondHeader.WriteState;
    }

    /// <summary>
    /// 创建失败读取结果。
    /// </summary>
    /// <param name="status">失败状态。</param>
    /// <param name="header">相关 header。</param>
    /// <param name="message">诊断消息。</param>
    /// <returns>失败读取结果。</returns>
    private static SharedMemoryTelemetryFrameReadResult Failure(
        SharedMemoryTelemetryFrameStatus status,
        SharedMemoryTelemetryFrameHeader? header,
        string message)
    {
        return new SharedMemoryTelemetryFrameReadResult(status, header, string.Empty, message);
    }
}
