using System.IO.MemoryMappedFiles;
using System.Text;
using YokiFrame;
using YokiFrame.Protocol.Telemetry.SharedMemory;

namespace YokiFrame.Client.Tests.Telemetry.SharedMemory;

/// <summary>覆盖 Shared Memory reader 在稳定未变化 header 处跳过 payload 的高频路径。</summary>
public sealed class YokiFrameClientTelemetryCursorTests
{
    private const long GENERATION = 120L;
    private const long SEQUENCE = 44L;
    private const long WRITTEN_AT_UTC_TICKS = 638880000000000000L;

    /// <summary>验证已消费帧在 CRC 校验和 UTF-8 解码前返回空，避免 100ms 轮询重复复制 payload。</summary>
    [Fact]
    public void UnchangedCommittedHeaderSkipsPayloadRead()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var result = ReadFrameIfChanged("{\"kit\":\"FsmKit\"}", true, SEQUENCE);

        Assert.Null(result);
    }

    /// <summary>验证 header 较游标更新时仍执行完整 payload 校验，不会把损坏的新帧误判为未变化。</summary>
    [Fact]
    public void NewHeaderStillValidatesPayload()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var result = ReadFrameIfChanged("{\"kit\":\"FsmKit\"}", true, SEQUENCE - 1L);

        Assert.NotNull(result);
        Assert.Equal(SharedMemoryTelemetryFrameStatus.CrcMismatch, result.Status);
    }

    /// <summary>验证同一 generation 内 sequence 未前进时，即使系统时间前进也不会重复接受旧帧。</summary>
    [Fact]
    public void NewerTimestampDoesNotAcceptLowerSequence()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var result = ReadFrameIfChanged(
            "{\"kit\":\"FsmKit\"}",
            false,
            SEQUENCE,
            sequence: 1L,
            writtenAtUtcTicks: WRITTEN_AT_UTC_TICKS + 1L);

        Assert.Null(result);
    }

    /// <summary>验证 sequence 前进时即使系统时间回拨也会完整校验新帧。</summary>
    [Fact]
    public void OlderTimestampStillReadsHigherSequence()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var result = ReadFrameIfChanged(
            "{\"kit\":\"FsmKit\"}",
            true,
            SEQUENCE,
            sequence: SEQUENCE + 1L,
            writtenAtUtcTicks: WRITTEN_AT_UTC_TICKS - 1L);

        Assert.NotNull(result);
        Assert.Equal(SharedMemoryTelemetryFrameStatus.CrcMismatch, result.Status);
    }

    /// <summary>验证 frame 的 engineIdHash 不属于请求 engine 时不会接受伪归属 payload。</summary>
    [Fact]
    public void EngineIdHashMismatchIsRejected()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var result = ReadFrameIfChanged(
            "{\"kit\":\"FsmKit\"}", false, SEQUENCE, useWrongEngineHash: true);

        Assert.NotNull(result);
        Assert.Equal(SharedMemoryTelemetryFrameStatus.EngineIdHashMismatch, result.Status);
    }

    /// <summary>验证 Writing 与随后同 sequence 的 Committed 帧可连续读取，不把写入中状态当成已消费帧。</summary>
    [Fact]
    public void WritingFrameCanBeRetriedAfterCommit()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        const string payloadJson = "{\"kit\":\"FsmKit\"}";
        var engineId = "cursor-" + Guid.NewGuid().ToString("N");
        var projectRoot = CreateProjectRoot();
        var engineIdHash = YokiFrameSharedMemoryTelemetryEngineIdHash.Compute(engineId);
        var writingFrame = CreateFrame(
            payloadJson, false, engineIdHash, writeState: SharedMemoryTelemetryWriteState.Writing);
        var committedFrame = CreateFrame(payloadJson, false, engineIdHash);
        var segmentName = SharedMemoryTelemetrySegmentName.Create(projectRoot, engineId, "FsmKit", "state");
        using var memoryMap = MemoryMappedFile.CreateNew(
            segmentName, committedFrame.Length, MemoryMappedFileAccess.ReadWrite);
        using var accessor = memoryMap.CreateViewAccessor(0, committedFrame.Length, MemoryMappedFileAccess.Write);
        using YokiFrameClient client = new(projectRoot);

        accessor.WriteArray(0, writingFrame, 0, writingFrame.Length);
        var writing = client.ReadTelemetryIfChanged(
            engineId, "FsmKit", "state", GENERATION,
            SharedMemoryTelemetryFrameReader.DEFAULT_MAX_PAYLOAD_BYTES, SEQUENCE - 1L);
        accessor.WriteArray(0, committedFrame, 0, committedFrame.Length);
        var committed = client.ReadTelemetryIfChanged(
            engineId, "FsmKit", "state", GENERATION,
            SharedMemoryTelemetryFrameReader.DEFAULT_MAX_PAYLOAD_BYTES, SEQUENCE - 1L);

        Assert.Equal(SharedMemoryTelemetryFrameStatus.Writing, writing?.Status);
        Assert.True(committed?.IsAccepted);
    }

    /// <summary>验证预热后 1000 次稳定空闲轮询无托管分配，且只打开一次 map/accessor。</summary>
    [Fact]
    public void UnchangedPollingReusesOneMapLease()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var engineId = "lease-" + Guid.NewGuid().ToString("N");
        var projectRoot = CreateProjectRoot();
        var engineIdHash = YokiFrameSharedMemoryTelemetryEngineIdHash.Compute(engineId);
        var frame = CreateFrame("{\"kit\":\"FsmKit\"}", false, engineIdHash);
        var segmentName = SharedMemoryTelemetrySegmentName.Create(projectRoot, engineId, "FsmKit", "state");
        using var memoryMap = MemoryMappedFile.CreateNew(segmentName, frame.Length, MemoryMappedFileAccess.ReadWrite);
        using var accessor = memoryMap.CreateViewAccessor(0, frame.Length, MemoryMappedFileAccess.Write);
        accessor.WriteArray(0, frame, 0, frame.Length);
        using YokiFrameClient client = new(projectRoot);

        Assert.Null(client.ReadTelemetryIfChanged(
            engineId, "FsmKit", "state", GENERATION,
            SharedMemoryTelemetryFrameReader.DEFAULT_MAX_PAYLOAD_BYTES, SEQUENCE));
        var allUnchanged = true;
        for (var index = 0; index < 1000; index++)
        {
            var result = client.ReadTelemetryIfChanged(
                engineId, "FsmKit", "state", GENERATION,
                SharedMemoryTelemetryFrameReader.DEFAULT_MAX_PAYLOAD_BYTES, SEQUENCE);
            allUnchanged &= result == null;
        }

        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        for (var index = 0; index < 1000; index++)
        {
            var result = client.ReadTelemetryIfChanged(
                engineId, "FsmKit", "state", GENERATION,
                SharedMemoryTelemetryFrameReader.DEFAULT_MAX_PAYLOAD_BYTES, SEQUENCE);
            allUnchanged &= result == null;
        }
        var allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

        Assert.True(allUnchanged);
        Assert.Equal(0L, allocatedBytes);
        Assert.Equal(1, client.GetTelemetryMapOpenCount(engineId, "FsmKit", "state"));
        Assert.Equal(1, client.ActiveTelemetryLeaseCount);
    }

    /// <summary>验证并发读取同一目标时仍只创建一个 accessor，缓存与 lease 生命周期不会发生竞态。</summary>
    [Fact]
    public void ConcurrentPollingSharesOneMapLease()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var engineId = "concurrent-" + Guid.NewGuid().ToString("N");
        var projectRoot = CreateProjectRoot();
        var engineIdHash = YokiFrameSharedMemoryTelemetryEngineIdHash.Compute(engineId);
        var frame = CreateFrame("{\"kit\":\"FsmKit\"}", false, engineIdHash);
        var segmentName = SharedMemoryTelemetrySegmentName.Create(projectRoot, engineId, "FsmKit", "state");
        using var memoryMap = MemoryMappedFile.CreateNew(segmentName, frame.Length, MemoryMappedFileAccess.ReadWrite);
        using var accessor = memoryMap.CreateViewAccessor(0, frame.Length, MemoryMappedFileAccess.Write);
        accessor.WriteArray(0, frame, 0, frame.Length);
        using YokiFrameClient client = new(projectRoot);

        Parallel.For(0, 64, _ => Assert.Null(client.ReadTelemetryIfChanged(
            engineId, "FsmKit", "state", GENERATION,
            SharedMemoryTelemetryFrameReader.DEFAULT_MAX_PAYLOAD_BYTES, SEQUENCE)));

        Assert.Equal(1, client.GetTelemetryMapOpenCount(engineId, "FsmKit", "state"));
        Assert.Equal(1, client.ActiveTelemetryLeaseCount);
    }

    /// <summary>验证 generation 改变会先释放旧映射，再打开并接受新一代帧。</summary>
    [Fact]
    public void GenerationChangeReopensMapLease()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var engineId = "generation-" + Guid.NewGuid().ToString("N");
        var projectRoot = CreateProjectRoot();
        var engineIdHash = YokiFrameSharedMemoryTelemetryEngineIdHash.Compute(engineId);
        var firstFrame = CreateFrame("{\"generation\":120}", false, engineIdHash);
        var nextFrame = CreateFrame("{\"generation\":121}", false, engineIdHash, generation: GENERATION + 1L);
        var segmentName = SharedMemoryTelemetrySegmentName.Create(projectRoot, engineId, "FsmKit", "state");
        using var memoryMap = MemoryMappedFile.CreateNew(segmentName, nextFrame.Length, MemoryMappedFileAccess.ReadWrite);
        using var accessor = memoryMap.CreateViewAccessor(0, nextFrame.Length, MemoryMappedFileAccess.Write);
        using YokiFrameClient client = new(projectRoot);

        accessor.WriteArray(0, firstFrame, 0, firstFrame.Length);
        var first = client.ReadTelemetry(
            engineId, "FsmKit", "state", GENERATION,
            SharedMemoryTelemetryFrameReader.DEFAULT_MAX_PAYLOAD_BYTES);
        accessor.WriteArray(0, nextFrame, 0, nextFrame.Length);
        var next = client.ReadTelemetry(
            engineId, "FsmKit", "state", GENERATION + 1L,
            SharedMemoryTelemetryFrameReader.DEFAULT_MAX_PAYLOAD_BYTES);

        Assert.True(first.IsAccepted);
        Assert.True(next.IsAccepted);
        Assert.Equal(2, client.GetTelemetryMapOpenCount(engineId, "FsmKit", "state"));
        Assert.Equal(1, client.ActiveTelemetryLeaseCount);
    }

    /// <summary>验证稳定身份校验失败后立即关闭 accessor，避免坏 segment 被 100ms 轮询长期钉住。</summary>
    [Fact]
    public void IdentityFailureReleasesMapLease()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var engineId = "identity-" + Guid.NewGuid().ToString("N");
        var projectRoot = CreateProjectRoot();
        var engineIdHash = YokiFrameSharedMemoryTelemetryEngineIdHash.Compute(engineId);
        var frame = CreateFrame("{\"kit\":\"FsmKit\"}", false, engineIdHash + 1UL);
        var segmentName = SharedMemoryTelemetrySegmentName.Create(projectRoot, engineId, "FsmKit", "state");
        using var memoryMap = MemoryMappedFile.CreateNew(segmentName, frame.Length, MemoryMappedFileAccess.ReadWrite);
        using var accessor = memoryMap.CreateViewAccessor(0, frame.Length, MemoryMappedFileAccess.Write);
        accessor.WriteArray(0, frame, 0, frame.Length);
        using YokiFrameClient client = new(projectRoot);

        var result = client.ReadTelemetry(
            engineId, "FsmKit", "state", GENERATION,
            SharedMemoryTelemetryFrameReader.DEFAULT_MAX_PAYLOAD_BYTES);

        Assert.Equal(SharedMemoryTelemetryFrameStatus.EngineIdHashMismatch, result.Status);
        Assert.Equal(1, client.GetTelemetryMapOpenCount(engineId, "FsmKit", "state"));
        Assert.Equal(0, client.ActiveTelemetryLeaseCount);
    }

    /// <summary>验证 Client Dispose 会确定性关闭全部活动 accessor，并拒绝生命周期结束后的重新读取。</summary>
    [Fact]
    public void DisposeReleasesActiveMapLease()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var engineId = "dispose-" + Guid.NewGuid().ToString("N");
        var projectRoot = CreateProjectRoot();
        var engineIdHash = YokiFrameSharedMemoryTelemetryEngineIdHash.Compute(engineId);
        var frame = CreateFrame("{\"kit\":\"FsmKit\"}", false, engineIdHash);
        var segmentName = SharedMemoryTelemetrySegmentName.Create(projectRoot, engineId, "FsmKit", "state");
        using var memoryMap = MemoryMappedFile.CreateNew(segmentName, frame.Length, MemoryMappedFileAccess.ReadWrite);
        using var accessor = memoryMap.CreateViewAccessor(0, frame.Length, MemoryMappedFileAccess.Write);
        accessor.WriteArray(0, frame, 0, frame.Length);
        var client = new YokiFrameClient(projectRoot);

        var accepted = client.ReadTelemetry(
            engineId, "FsmKit", "state", GENERATION,
            SharedMemoryTelemetryFrameReader.DEFAULT_MAX_PAYLOAD_BYTES);
        Assert.True(accepted.IsAccepted);
        Assert.Equal(1, client.ActiveTelemetryLeaseCount);

        client.Dispose();

        Assert.Equal(0, client.ActiveTelemetryLeaseCount);
        Assert.Equal(0, client.CachedTelemetryTargetCount);
        Assert.Throws<ObjectDisposedException>(() => client.ReadTelemetry(
            engineId, "FsmKit", "state", GENERATION,
            SharedMemoryTelemetryFrameReader.DEFAULT_MAX_PAYLOAD_BYTES));
    }

    /// <summary>验证有界缓存重置时会 Dispose 已打开目标，不会只清字典后遗留 OS accessor。</summary>
    [Fact]
    public void BoundedCacheCleanupDisposesOpenedLease()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var projectRoot = CreateProjectRoot();
        var engineId = "active-" + Guid.NewGuid().ToString("N");
        var engineIdHash = YokiFrameSharedMemoryTelemetryEngineIdHash.Compute(engineId);
        var frame = CreateFrame("{\"kit\":\"FsmKit\"}", false, engineIdHash);
        var segmentName = SharedMemoryTelemetrySegmentName.Create(projectRoot, engineId, "FsmKit", "state");
        using var memoryMap = MemoryMappedFile.CreateNew(segmentName, frame.Length, MemoryMappedFileAccess.ReadWrite);
        using var accessor = memoryMap.CreateViewAccessor(0, frame.Length, MemoryMappedFileAccess.Write);
        accessor.WriteArray(0, frame, 0, frame.Length);
        using YokiFrameClient client = new(projectRoot);
        _ = client.ReadTelemetry(
            engineId, "FsmKit", "state", GENERATION,
            SharedMemoryTelemetryFrameReader.DEFAULT_MAX_PAYLOAD_BYTES);

        for (var index = 0; index < 128; index++)
        {
            _ = client.ReadTelemetry(
                "missing-" + index, "FsmKit", "state", GENERATION,
                SharedMemoryTelemetryFrameReader.DEFAULT_MAX_PAYLOAD_BYTES);
        }

        Assert.Equal(1, client.CachedTelemetryTargetCount);
        Assert.Equal(0, client.ActiveTelemetryLeaseCount);
    }

    /// <summary>把测试帧写入独立命名段，并使用正式 Client 执行一次游标读取。</summary>
    /// <param name="payloadJson">测试 payload。</param>
    /// <param name="invalidCrc">是否构造错误 CRC。</param>
    /// <param name="afterSequence">调用方已接受的 sequence。</param>
    /// <param name="sequence">候选帧 sequence。</param>
    /// <param name="writtenAtUtcTicks">候选帧写入时间。</param>
    /// <param name="useWrongEngineHash">是否故意写入其它 engine 哈希。</param>
    /// <returns>正式 Client 的增量读取结果。</returns>
    private static SharedMemoryTelemetryFrameReadResult? ReadFrameIfChanged(
        string payloadJson,
        bool invalidCrc,
        long afterSequence,
        long sequence = SEQUENCE,
        long writtenAtUtcTicks = WRITTEN_AT_UTC_TICKS,
        bool useWrongEngineHash = false)
    {
        var engineId = "cursor-" + Guid.NewGuid().ToString("N");
        var projectRoot = CreateProjectRoot();
        var engineIdHash = YokiFrameSharedMemoryTelemetryEngineIdHash.Compute(engineId);
        var frame = CreateFrame(
            payloadJson,
            invalidCrc,
            useWrongEngineHash ? engineIdHash + 1UL : engineIdHash,
            sequence,
            writtenAtUtcTicks);
        var segmentName = SharedMemoryTelemetrySegmentName.Create(projectRoot, engineId, "FsmKit", "state");
        using var memoryMap = MemoryMappedFile.CreateNew(segmentName, frame.Length, MemoryMappedFileAccess.ReadWrite);
        using var accessor = memoryMap.CreateViewAccessor(0, frame.Length, MemoryMappedFileAccess.Write);
        accessor.WriteArray(0, frame, 0, frame.Length);
        accessor.Flush();
        using YokiFrameClient client = new(projectRoot);
        return client.ReadTelemetryIfChanged(
            engineId,
            "FsmKit",
            "state",
            GENERATION,
            SharedMemoryTelemetryFrameReader.DEFAULT_MAX_PAYLOAD_BYTES,
            afterSequence);
    }

    /// <summary>创建无需真实存在的测试项目根目录。</summary>
    /// <returns>测试项目根路径。</returns>
    private static string CreateProjectRoot()
    {
        return Path.Combine(
            Path.GetTempPath(),
            "yokiframe-telemetry-cursor-tests",
            Guid.NewGuid().ToString("N"));
    }

    /// <summary>创建固定游标的测试帧，并可故意写入错误 CRC 证明 payload 路径是否执行。</summary>
    /// <param name="payloadJson">payload JSON。</param>
    /// <param name="invalidCrc">是否故意写入错误 CRC。</param>
    /// <param name="engineIdHash">写入 header 的宿主哈希。</param>
    /// <param name="sequence">写入 header 的帧序号。</param>
    /// <param name="writtenAtUtcTicks">写入 header 的 UTC ticks。</param>
    /// <param name="writeState">写入阶段。</param>
    /// <returns>完整帧字节。</returns>
    private static byte[] CreateFrame(
        string payloadJson,
        bool invalidCrc,
        ulong engineIdHash,
        long sequence = SEQUENCE,
        long writtenAtUtcTicks = WRITTEN_AT_UTC_TICKS,
        SharedMemoryTelemetryWriteState writeState = SharedMemoryTelemetryWriteState.Committed,
        long generation = GENERATION)
    {
        var payload = Encoding.UTF8.GetBytes(payloadJson);
        var frame = new byte[SharedMemoryTelemetryFrameHeader.HEADER_SIZE + payload.Length];
        var crc32 = SharedMemoryTelemetryCrc32.Compute(payload);
        SharedMemoryTelemetryFrameHeader header = new(
            SharedMemoryTelemetryFrameHeader.MAGIC,
            SharedMemoryTelemetryFrameHeader.PROTOCOL_VERSION,
            engineIdHash,
            generation,
            sequence,
            writtenAtUtcTicks,
            payload.Length,
            invalidCrc ? crc32 + 1U : crc32,
            writeState);
        header.WriteTo(frame.AsSpan(0, SharedMemoryTelemetryFrameHeader.HEADER_SIZE));
        payload.CopyTo(frame.AsSpan(SharedMemoryTelemetryFrameHeader.HEADER_SIZE));
        return frame;
    }
}
