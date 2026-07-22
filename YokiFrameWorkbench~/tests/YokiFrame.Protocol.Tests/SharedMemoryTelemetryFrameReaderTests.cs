using System.Text;
using YokiFrame.Protocol.Telemetry.SharedMemory;

namespace YokiFrame.Protocol.Tests;

/// <summary>
/// 覆盖 Shared Memory telemetry v1 帧读取规则。
/// </summary>
public sealed class SharedMemoryTelemetryFrameReaderTests
{
    private const ulong ENGINE_ID_HASH = 0x1122334455667788UL;
    private const long GENERATION = 42L;

    /// <summary>
    /// 验证已提交且 CRC 正确的帧可以被读取。
    /// </summary>
    [Fact]
    public void CommittedFrameCanBeRead()
    {
        var frame = CreateFrame("{\"kit\":\"System\"}", GENERATION, 7L, SharedMemoryTelemetryWriteState.Committed);
        var result = SharedMemoryTelemetryFrameReader.ReadCommittedFrame(frame, GENERATION);

        Assert.True(result.IsAccepted);
        Assert.Equal(SharedMemoryTelemetryFrameStatus.Accepted, result.Status);
        Assert.Equal("{\"kit\":\"System\"}", result.PayloadJson);
        Assert.Equal(7L, result.Header!.Sequence);
    }

    /// <summary>
    /// 验证 writer 正在写入时 reader 会跳过当前帧。
    /// </summary>
    [Fact]
    public void WritingFrameIsSkipped()
    {
        var frame = CreateFrame("{}", GENERATION, 8L, SharedMemoryTelemetryWriteState.Writing);
        var result = SharedMemoryTelemetryFrameReader.ReadCommittedFrame(frame, GENERATION);

        Assert.False(result.IsAccepted);
        Assert.Equal(SharedMemoryTelemetryFrameStatus.Writing, result.Status);
    }

    /// <summary>
    /// 验证前后 header 序号变化时会被识别为半写帧。
    /// </summary>
    [Fact]
    public void ChangedSecondHeaderIsHalfWrite()
    {
        var firstFrame = CreateFrame("{}", GENERATION, 9L, SharedMemoryTelemetryWriteState.Committed);
        var secondFrame = CreateFrame("{}", GENERATION, 10L, SharedMemoryTelemetryWriteState.Committed);
        var result = SharedMemoryTelemetryFrameReader.ReadFrame(
            firstFrame.AsSpan(0, SharedMemoryTelemetryFrameHeader.HEADER_SIZE),
            firstFrame.AsSpan(SharedMemoryTelemetryFrameHeader.HEADER_SIZE),
            secondFrame.AsSpan(0, SharedMemoryTelemetryFrameHeader.HEADER_SIZE),
            GENERATION);

        Assert.False(result.IsAccepted);
        Assert.Equal(SharedMemoryTelemetryFrameStatus.HalfWrite, result.Status);
    }

    /// <summary>
    /// 验证 sequence 相同但写入时间变化时仍会识别为半写帧，避免接受混合 header。
    /// </summary>
    [Fact]
    public void ChangedWrittenTimestampIsHalfWrite()
    {
        var firstFrame = CreateFrame("{}", GENERATION, 10L, SharedMemoryTelemetryWriteState.Committed, 100L);
        var secondFrame = CreateFrame("{}", GENERATION, 10L, SharedMemoryTelemetryWriteState.Committed, 101L);
        var result = SharedMemoryTelemetryFrameReader.ReadFrame(
            firstFrame.AsSpan(0, SharedMemoryTelemetryFrameHeader.HEADER_SIZE),
            firstFrame.AsSpan(SharedMemoryTelemetryFrameHeader.HEADER_SIZE),
            secondFrame.AsSpan(0, SharedMemoryTelemetryFrameHeader.HEADER_SIZE),
            GENERATION);

        Assert.False(result.IsAccepted);
        Assert.Equal(SharedMemoryTelemetryFrameStatus.HalfWrite, result.Status);
    }

    /// <summary>
    /// 验证 generation 与 registry 不一致时会触发 remap/fallback 信号。
    /// </summary>
    [Fact]
    public void GenerationMismatchIsRejected()
    {
        var frame = CreateFrame("{}", GENERATION + 1L, 11L, SharedMemoryTelemetryWriteState.Committed);
        var result = SharedMemoryTelemetryFrameReader.ReadCommittedFrame(frame, GENERATION);

        Assert.False(result.IsAccepted);
        Assert.Equal(SharedMemoryTelemetryFrameStatus.GenerationMismatch, result.Status);
    }

    /// <summary>验证请求 engine 的稳定哈希与 frame 不一致时拒绝归属。</summary>
    [Fact]
    public void EngineIdHashMismatchIsRejected()
    {
        var frame = CreateFrame("{}", GENERATION, 11L, SharedMemoryTelemetryWriteState.Committed);
        var result = SharedMemoryTelemetryFrameReader.ReadCommittedFrame(
            frame,
            GENERATION,
            SharedMemoryTelemetryFrameReader.DEFAULT_MAX_PAYLOAD_BYTES,
            ENGINE_ID_HASH + 1UL);

        Assert.False(result.IsAccepted);
        Assert.Equal(SharedMemoryTelemetryFrameStatus.EngineIdHashMismatch, result.Status);
    }

    /// <summary>
    /// 验证超过 reader 上限的 payload 会被拒绝，避免刷新循环接受异常大帧。
    /// </summary>
    [Fact]
    public void PayloadTooLargeIsRejected()
    {
        var frame = CreateFrame("{\"long\":true}", GENERATION, 12L, SharedMemoryTelemetryWriteState.Committed);
        var result = SharedMemoryTelemetryFrameReader.ReadCommittedFrame(frame, GENERATION, 4);

        Assert.False(result.IsAccepted);
        Assert.Equal(SharedMemoryTelemetryFrameStatus.PayloadTooLarge, result.Status);
    }

    /// <summary>
    /// 验证 payload 损坏时 CRC 校验会拒绝该帧。
    /// </summary>
    [Fact]
    public void CrcMismatchIsRejected()
    {
        var frame = CreateFrame("{\"ok\":true}", GENERATION, 13L, SharedMemoryTelemetryWriteState.Committed);
        frame[^1] ^= 0x01;
        var result = SharedMemoryTelemetryFrameReader.ReadCommittedFrame(frame, GENERATION);

        Assert.False(result.IsAccepted);
        Assert.Equal(SharedMemoryTelemetryFrameStatus.CrcMismatch, result.Status);
    }

    /// <summary>
    /// 验证同一 engine/Kit 在不同项目中使用不同 Named Map，避免多个 Unity 项目串写实时状态。
    /// </summary>
    [Fact]
    public void SegmentNameSeparatesProjectRoots()
    {
        var first = SharedMemoryTelemetrySegmentName.Create("F:/ProjectA", "unity-editor", "Architecture", "state");
        var second = SharedMemoryTelemetrySegmentName.Create("F:/ProjectB", "unity-editor", "Architecture", "state");

        Assert.NotEqual(first, second);
    }

    /// <summary>
    /// 验证项目作用域忽略 Windows 路径大小写、分隔符和末尾斜杠差异。
    /// </summary>
    [Fact]
    public void SegmentNameNormalizesEquivalentProjectRoots()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var first = SharedMemoryTelemetrySegmentName.Create("F:\\YokiFrame", "unity-editor", "Architecture", "state");
        var second = SharedMemoryTelemetrySegmentName.Create("f:/yokiframe/", "unity-editor", "Architecture", "state");

        Assert.Equal(first, second);
    }

    /// <summary>
    /// 验证非 ASCII 项目路径使用完整 UTF-8 字节参与作用域，不能因 UTF-16 低字节相同而串用 segment。
    /// </summary>
    [Fact]
    public void SegmentNameSeparatesUnicodeProjectRoots()
    {
        var first = SharedMemoryTelemetrySegmentName.Create("F:/Project-\u4e00", "unity-editor", "Architecture", "state");
        var second = SharedMemoryTelemetrySegmentName.Create("F:/Project-\u4f00", "unity-editor", "Architecture", "state");

        Assert.NotEqual(first, second);
    }

    /// <summary>
    /// 验证大小写敏感平台不会把两个真实目录折叠为相同项目作用域。
    /// </summary>
    [Fact]
    public void SegmentNamePreservesProjectRootCaseOnPosix()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var first = SharedMemoryTelemetrySegmentName.Create("/work/Project", "unity-editor", "Architecture", "state");
        var second = SharedMemoryTelemetrySegmentName.Create("/work/project", "unity-editor", "Architecture", "state");

        Assert.NotEqual(first, second);
    }

    /// <summary>
    /// 创建测试用 telemetry 帧。
    /// </summary>
    /// <param name="payloadJson">payload JSON 文本。</param>
    /// <param name="generation">engine generation。</param>
    /// <param name="sequence">帧序号。</param>
    /// <param name="writeState">写入状态。</param>
    /// <param name="writtenAtUtcTicks">写入时间；为空时使用当前 UTC ticks。</param>
    /// <returns>完整帧字节。</returns>
    private static byte[] CreateFrame(
        string payloadJson,
        long generation,
        long sequence,
        SharedMemoryTelemetryWriteState writeState,
        long? writtenAtUtcTicks = null)
    {
        var payload = Encoding.UTF8.GetBytes(payloadJson);
        var frame = new byte[SharedMemoryTelemetryFrameHeader.HEADER_SIZE + payload.Length];
        var header = new SharedMemoryTelemetryFrameHeader(
            SharedMemoryTelemetryFrameHeader.MAGIC,
            SharedMemoryTelemetryFrameHeader.PROTOCOL_VERSION,
            ENGINE_ID_HASH,
            generation,
            sequence,
            writtenAtUtcTicks ?? DateTimeOffset.UtcNow.UtcTicks,
            payload.Length,
            SharedMemoryTelemetryCrc32.Compute(payload),
            writeState);

        header.WriteTo(frame.AsSpan(0, SharedMemoryTelemetryFrameHeader.HEADER_SIZE));
        payload.CopyTo(frame.AsSpan(SharedMemoryTelemetryFrameHeader.HEADER_SIZE));
        return frame;
    }
}
