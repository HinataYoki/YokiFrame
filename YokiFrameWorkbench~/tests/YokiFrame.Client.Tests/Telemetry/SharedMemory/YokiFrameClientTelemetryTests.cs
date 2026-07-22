using System.IO.MemoryMappedFiles;
using System.Text;
using YokiFrame;
using YokiFrame.Protocol.Telemetry.SharedMemory;

namespace YokiFrame.Client.Tests.Telemetry.SharedMemory;

/// <summary>
/// 覆盖 Windows named memory map telemetry reader。
/// </summary>
public sealed class YokiFrameClientTelemetryTests
{
    private const long GENERATION = 120L;

    /// <summary>
    /// 验证不存在的 segment 会返回 Unavailable，供调用侧回落 snapshot。
    /// </summary>
    [Fact]
    public void MissingSegmentReturnsUnavailable()
    {
        using YokiFrameClient client = new(CreateProjectRoot());
        var result = client.ReadTelemetry(
            CreateUniqueEngineId(),
            "System",
            "state",
            GENERATION,
            SharedMemoryTelemetryFrameReader.DEFAULT_MAX_PAYLOAD_BYTES);

        Assert.False(result.IsAccepted);
        Assert.Equal(SharedMemoryTelemetryFrameStatus.Unavailable, result.Status);
    }

    /// <summary>
    /// 验证 Windows named memory map 中的已提交帧可以被读取。
    /// </summary>
    [Fact]
    public void ExistingNamedMapFrameCanBeRead()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var engineId = CreateUniqueEngineId();
        var projectRoot = CreateProjectRoot();
        var segmentName = SharedMemoryTelemetrySegmentName.Create(projectRoot, engineId, "System", "state");
        var frame = CreateFrame(
            "{\"kit\":\"System\"}",
            YokiFrameSharedMemoryTelemetryEngineIdHash.Compute(engineId),
            GENERATION,
            3L);
        using var memoryMap = MemoryMappedFile.CreateNew(segmentName, frame.Length, MemoryMappedFileAccess.ReadWrite);
        using var accessor = memoryMap.CreateViewAccessor(0, frame.Length, MemoryMappedFileAccess.Write);
        accessor.WriteArray(0, frame, 0, frame.Length);

        using YokiFrameClient client = new(projectRoot);
        var result = client.ReadTelemetry(
            engineId,
            "System",
            "state",
            GENERATION,
            SharedMemoryTelemetryFrameReader.DEFAULT_MAX_PAYLOAD_BYTES);

        Assert.True(result.IsAccepted);
        Assert.Equal("{\"kit\":\"System\"}", result.PayloadJson);
    }

    /// <summary>
    /// 验证不同项目的 Client 不会打开另一项目的同名 telemetry segment。
    /// </summary>
    [Fact]
    public void NamedMapFrameDoesNotCrossProjectBoundary()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var engineId = CreateUniqueEngineId();
        var firstProjectRoot = CreateProjectRoot();
        var secondProjectRoot = CreateProjectRoot();
        var secondSegmentName = SharedMemoryTelemetrySegmentName.Create(
            secondProjectRoot,
            engineId,
            "System",
            "state");
        var frame = CreateFrame(
            "{\"project\":\"second\"}",
            YokiFrameSharedMemoryTelemetryEngineIdHash.Compute(engineId),
            GENERATION,
            7L);
        using var memoryMap = MemoryMappedFile.CreateNew(
            secondSegmentName,
            frame.Length,
            MemoryMappedFileAccess.ReadWrite);
        using var accessor = memoryMap.CreateViewAccessor(0, frame.Length, MemoryMappedFileAccess.Write);
        accessor.WriteArray(0, frame, 0, frame.Length);
        using YokiFrameClient firstClient = new(firstProjectRoot);
        using YokiFrameClient secondClient = new(secondProjectRoot);

        var firstResult = firstClient.ReadTelemetry(
            engineId,
            "System",
            "state",
            GENERATION,
            SharedMemoryTelemetryFrameReader.DEFAULT_MAX_PAYLOAD_BYTES);
        var secondResult = secondClient.ReadTelemetry(
            engineId,
            "System",
            "state",
            GENERATION,
            SharedMemoryTelemetryFrameReader.DEFAULT_MAX_PAYLOAD_BYTES);

        Assert.Equal(SharedMemoryTelemetryFrameStatus.Unavailable, firstResult.Status);
        Assert.True(secondResult.IsAccepted);
        Assert.Equal("{\"project\":\"second\"}", secondResult.PayloadJson);
    }

    /// <summary>
    /// 验证相对项目根经 Client 规范化后可读取绝对项目根对应的同一 telemetry segment。
    /// </summary>
    [Fact]
    public void EquivalentAbsoluteAndRelativeProjectRootsReadSameNamedMap()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var engineId = CreateUniqueEngineId();
        var absoluteProjectRoot = Path.Combine(
            Environment.CurrentDirectory,
            ".yokiframe-telemetry-tests",
            Guid.NewGuid().ToString("N"));
        var relativeProjectRoot = Path.GetRelativePath(Environment.CurrentDirectory, absoluteProjectRoot);
        var segmentName = SharedMemoryTelemetrySegmentName.Create(
            absoluteProjectRoot,
            engineId,
            "System",
            "state");
        var frame = CreateFrame(
            "{\"scope\":\"same\"}",
            YokiFrameSharedMemoryTelemetryEngineIdHash.Compute(engineId),
            GENERATION,
            9L);
        using var memoryMap = MemoryMappedFile.CreateNew(segmentName, frame.Length, MemoryMappedFileAccess.ReadWrite);
        using var accessor = memoryMap.CreateViewAccessor(0, frame.Length, MemoryMappedFileAccess.Write);
        accessor.WriteArray(0, frame, 0, frame.Length);
        using YokiFrameClient client = new(relativeProjectRoot);

        var result = client.ReadTelemetry(
            engineId,
            "System",
            "state",
            GENERATION,
            SharedMemoryTelemetryFrameReader.DEFAULT_MAX_PAYLOAD_BYTES);

        Assert.True(result.IsAccepted);
        Assert.Equal("{\"scope\":\"same\"}", result.PayloadJson);
    }

    /// <summary>
    /// 创建测试专用 engine 标识，避免并行测试互相影响。
    /// </summary>
    /// <returns>唯一 engine 标识。</returns>
    private static string CreateUniqueEngineId()
    {
        return "test-" + Guid.NewGuid().ToString("N");
    }

    /// <summary>
    /// 创建无需真实存在的测试项目根目录。
    /// </summary>
    /// <returns>测试项目根路径。</returns>
    private static string CreateProjectRoot()
    {
        return Path.Combine(Path.GetTempPath(), "yokiframe-telemetry-tests", Guid.NewGuid().ToString("N"));
    }

    /// <summary>
    /// 创建测试用 telemetry 帧。
    /// </summary>
    /// <param name="payloadJson">payload JSON。</param>
    /// <param name="engineIdHash">目标 engine 的稳定哈希。</param>
    /// <param name="generation">engine generation。</param>
    /// <param name="sequence">帧序号。</param>
    /// <returns>帧字节。</returns>
    private static byte[] CreateFrame(
        string payloadJson,
        ulong engineIdHash,
        long generation,
        long sequence)
    {
        var payload = Encoding.UTF8.GetBytes(payloadJson);
        var frame = new byte[SharedMemoryTelemetryFrameHeader.HEADER_SIZE + payload.Length];
        var header = new SharedMemoryTelemetryFrameHeader(
            SharedMemoryTelemetryFrameHeader.MAGIC,
            SharedMemoryTelemetryFrameHeader.PROTOCOL_VERSION,
            engineIdHash,
            generation,
            sequence,
            DateTimeOffset.UtcNow.UtcTicks,
            payload.Length,
            SharedMemoryTelemetryCrc32.Compute(payload),
            SharedMemoryTelemetryWriteState.Committed);
        header.WriteTo(frame.AsSpan(0, SharedMemoryTelemetryFrameHeader.HEADER_SIZE));
        payload.CopyTo(frame.AsSpan(SharedMemoryTelemetryFrameHeader.HEADER_SIZE));
        return frame;
    }
}
