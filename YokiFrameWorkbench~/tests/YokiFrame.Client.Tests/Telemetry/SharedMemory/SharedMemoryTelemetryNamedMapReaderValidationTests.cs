using System.IO.MemoryMappedFiles;
using YokiFrame;
using YokiFrame.Client.Telemetry.SharedMemory;
using YokiFrame.Protocol.Telemetry.SharedMemory;

namespace YokiFrame.Client.Tests.Telemetry.SharedMemory;

/// <summary>验证 named map reader 在复制 payload 前完成全部 header 拒绝判定。</summary>
public sealed class SharedMemoryTelemetryNamedMapReaderValidationTests
{
    private const int MAX_PAYLOAD_BYTES = 1024;
    private const ulong EXPECTED_ENGINE_ID_HASH = 0x1234567890ABCDEFUL;
    private const long EXPECTED_GENERATION = 42L;
    private const long SEQUENCE = 7L;
    private const long WRITTEN_AT_UTC_TICKS = 638880000000000000L;

    /// <summary>验证错误身份、协议和 payload 上限均在进入 payload 读取前被拒绝，同时保留准确状态。</summary>
    /// <param name="headerKind">待构造的错误 header 类型。</param>
    /// <param name="expectedStatus">统一 frame reader 应返回的诊断状态。</param>
    [Theory]
    [InlineData(HeaderKind.InvalidMagic, SharedMemoryTelemetryFrameStatus.InvalidMagic)]
    [InlineData(HeaderKind.UnsupportedVersion, SharedMemoryTelemetryFrameStatus.UnsupportedVersion)]
    [InlineData(HeaderKind.WrongEngine, SharedMemoryTelemetryFrameStatus.EngineIdHashMismatch)]
    [InlineData(HeaderKind.WrongGeneration, SharedMemoryTelemetryFrameStatus.GenerationMismatch)]
    [InlineData(HeaderKind.OversizedPayload, SharedMemoryTelemetryFrameStatus.PayloadTooLarge)]
    public void RejectedHeaderDoesNotEnterPayloadReader(
        HeaderKind headerKind,
        SharedMemoryTelemetryFrameStatus expectedStatus)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var result = ReadFrame(headerKind, false, out var payloadReadAttemptCount);

        Assert.NotNull(result);
        Assert.Equal(expectedStatus, result.Status);
        Assert.Equal(0, payloadReadAttemptCount);
    }

    /// <summary>验证测试探针只在合法 header 确实进入 payload 复制路径时记录一次。</summary>
    [Fact]
    public void AcceptedHeaderEntersPayloadReaderOnce()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var result = ReadFrame(HeaderKind.Valid, false, out var payloadReadAttemptCount);

        Assert.NotNull(result);
        Assert.Equal(SharedMemoryTelemetryFrameStatus.Accepted, result.Status);
        Assert.Equal(1, payloadReadAttemptCount);
    }

    /// <summary>验证合法 header 仍复制 payload 并执行 CRC 校验，没有被前置校验错误短路。</summary>
    [Fact]
    public void CrcMismatchStillReadsPayloadOnce()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var result = ReadFrame(HeaderKind.Valid, true, out var payloadReadAttemptCount);

        Assert.NotNull(result);
        Assert.Equal(SharedMemoryTelemetryFrameStatus.CrcMismatch, result.Status);
        Assert.Equal(1, payloadReadAttemptCount);
    }

    /// <summary>把指定测试帧写入独立映射，并通过内部探针返回 payload 读取入口次数。</summary>
    /// <param name="headerKind">测试 header 类型。</param>
    /// <param name="invalidCrc">是否故意写入错误 CRC。</param>
    /// <param name="payloadReadAttemptCount">本次读取进入 payload 路径的次数。</param>
    /// <returns>named map reader 的统一读取结果。</returns>
    private static SharedMemoryTelemetryFrameReadResult? ReadFrame(
        HeaderKind headerKind,
        bool invalidCrc,
        out int payloadReadAttemptCount)
    {
        var frame = CreateFrame(headerKind, invalidCrc);
        var segmentName = "YokiFrame.NamedMapReader.Tests." + Guid.NewGuid().ToString("N");
        using var memoryMap = MemoryMappedFile.CreateNew(
            segmentName, frame.Length, MemoryMappedFileAccess.ReadWrite);
        using var accessor = memoryMap.CreateViewAccessor(
            0, frame.Length, MemoryMappedFileAccess.ReadWrite);
        accessor.WriteArray(0, frame, 0, frame.Length);
        accessor.Flush();
        var probe = new PayloadReadAttemptProbe();
        var headerSize = SharedMemoryTelemetryFrameHeader.HEADER_SIZE;
        var result = SharedMemoryTelemetryNamedMapReader.Read(
            accessor,
            EXPECTED_GENERATION,
            EXPECTED_ENGINE_ID_HASH,
            MAX_PAYLOAD_BYTES,
            null,
            new byte[headerSize],
            new byte[headerSize],
            probe.RecordAttempt);
        payloadReadAttemptCount = probe.AttemptCount;
        return result;
    }

    /// <summary>创建 payload 内容固定、仅按测试类型改写目标 header 字段的完整帧。</summary>
    /// <param name="headerKind">测试 header 类型。</param>
    /// <param name="invalidCrc">是否故意写入错误 CRC。</param>
    /// <returns>可写入 named map 的完整帧字节。</returns>
    private static byte[] CreateFrame(HeaderKind headerKind, bool invalidCrc)
    {
        var payload = new byte[MAX_PAYLOAD_BYTES];
        Array.Fill(payload, (byte)0x2A);
        var payloadCrc32 = SharedMemoryTelemetryCrc32.Compute(payload);
        var payloadLength = headerKind == HeaderKind.OversizedPayload
            ? MAX_PAYLOAD_BYTES + 1
            : payload.Length;
        var magic = headerKind == HeaderKind.InvalidMagic
            ? SharedMemoryTelemetryFrameHeader.MAGIC + 1U
            : SharedMemoryTelemetryFrameHeader.MAGIC;
        var protocolVersion = headerKind == HeaderKind.UnsupportedVersion
            ? SharedMemoryTelemetryFrameHeader.PROTOCOL_VERSION + 1
            : SharedMemoryTelemetryFrameHeader.PROTOCOL_VERSION;
        var engineIdHash = headerKind == HeaderKind.WrongEngine
            ? EXPECTED_ENGINE_ID_HASH + 1UL
            : EXPECTED_ENGINE_ID_HASH;
        var generation = headerKind == HeaderKind.WrongGeneration
            ? EXPECTED_GENERATION + 1L
            : EXPECTED_GENERATION;
        SharedMemoryTelemetryFrameHeader header = new(
            magic,
            protocolVersion,
            engineIdHash,
            generation,
            SEQUENCE,
            WRITTEN_AT_UTC_TICKS,
            payloadLength,
            invalidCrc ? payloadCrc32 + 1U : payloadCrc32,
            SharedMemoryTelemetryWriteState.Committed);
        var frame = new byte[SharedMemoryTelemetryFrameHeader.HEADER_SIZE + payload.Length];
        header.WriteTo(frame.AsSpan(0, SharedMemoryTelemetryFrameHeader.HEADER_SIZE));
        payload.CopyTo(frame.AsSpan(YokiFrameSharedMemoryTelemetryContract.PAYLOAD_OFFSET));
        return frame;
    }

    /// <summary>列出前置校验需要覆盖的合法与拒绝 header 形态。</summary>
    public enum HeaderKind
    {
        /// <summary>全部字段合法。</summary>
        Valid,

        /// <summary>magic 不属于当前协议。</summary>
        InvalidMagic,

        /// <summary>协议版本不受支持。</summary>
        UnsupportedVersion,

        /// <summary>engineIdHash 不属于请求目标。</summary>
        WrongEngine,

        /// <summary>generation 不属于当前宿主代次。</summary>
        WrongGeneration,

        /// <summary>payloadLength 超过读取上限。</summary>
        OversizedPayload
    }

    /// <summary>记录单次读取是否进入 payload 分配与复制入口。</summary>
    private sealed class PayloadReadAttemptProbe
    {
        /// <summary>获取 payload 读取入口累计调用次数。</summary>
        public int AttemptCount { get; private set; }

        /// <summary>在 payload 分配前记录一次读取尝试，供测试断言坏 header 不触达此处。</summary>
        public void RecordAttempt()
        {
            AttemptCount++;
        }
    }
}
