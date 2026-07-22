using System.IO.MemoryMappedFiles;
using System.Text;
using YokiFrame.Protocol.Telemetry.SharedMemory;
using YokiFrame.Tooling.Application.Models;
using YokiFrame.Tooling.Application.Models.LogKit;
using YokiFrame.Tooling.Application.Services;

namespace YokiFrame.Tooling.Application.Tests;

/// <summary>覆盖 LogKit 高频 telemetry 的游标短路和强类型解析。</summary>
public sealed class WorkbenchLogKitTelemetryTests
{
    private const string PAYLOAD = """
        {"schemaVersion":1,"diagnosticVersion":8,"settingsVersion":2,"stats":{"loggerName":"Test","hasLogger":true,"enabled":true,"minimumLevel":"Debug","historyCount":0,"droppedCount":0},"settings":{"enabled":true,"minimumLevel":"Debug"},"capabilities":{},"files":{},"history":{"entries":[],"count":0,"totalCount":0,"droppedCount":0,"truncated":false}}
        """;

    /// <summary>验证新帧解析后，相同 sequence 的下一轮只读取 header 并返回 Unchanged。</summary>
    [Fact]
    public void PollTelemetryShortCircuitsUnchangedFrame()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var projectRoot = Path.Combine(Path.GetTempPath(), "yokiframe-logkit-telemetry-tests", Guid.NewGuid().ToString("N"));
        var engineId = "log-" + Guid.NewGuid().ToString("N");
        using var segment = CreateSegment(projectRoot, engineId, PAYLOAD, 6L, 12L);
        var service = new WorkbenchDashboardService(projectRoot);
        var health = CreateHealth(engineId, 6L);

        var accepted = service.PollLogKitTelemetry(engineId, health, long.MinValue);
        var unchanged = service.PollLogKitTelemetry(engineId, health, accepted.Sequence);

        Assert.Equal(WorkbenchLogKitTelemetryReadStatus.Accepted, accepted.Status);
        Assert.Equal(12L, accepted.Sequence);
        Assert.Equal(8L, accepted.State!.DiagnosticVersion);
        Assert.Equal(WorkbenchLogKitTelemetryReadStatus.Unchanged, unchanged.Status);
        Assert.False(unchanged.HasCursor);
    }

    /// <summary>创建与当前项目、engine 和 generation 隔离的 Shared Memory 帧。</summary>
    private static IDisposable CreateSegment(
        string projectRoot,
        string engineId,
        string payloadJson,
        long generation,
        long sequence)
    {
        var payload = Encoding.UTF8.GetBytes(payloadJson);
        var frame = new byte[SharedMemoryTelemetryFrameHeader.HEADER_SIZE + payload.Length];
        var header = new SharedMemoryTelemetryFrameHeader(
            SharedMemoryTelemetryFrameHeader.MAGIC,
            SharedMemoryTelemetryFrameHeader.PROTOCOL_VERSION,
            YokiFrameSharedMemoryTelemetryEngineIdHash.Compute(engineId),
            generation,
            sequence,
            DateTimeOffset.UtcNow.UtcTicks,
            payload.Length,
            SharedMemoryTelemetryCrc32.Compute(payload),
            SharedMemoryTelemetryWriteState.Committed);
        header.WriteTo(frame.AsSpan(0, SharedMemoryTelemetryFrameHeader.HEADER_SIZE));
        payload.CopyTo(frame.AsSpan(SharedMemoryTelemetryFrameHeader.HEADER_SIZE));
        var name = SharedMemoryTelemetrySegmentName.Create(projectRoot, engineId, "LogKit", "state");
        var map = MemoryMappedFile.CreateNew(name, frame.Length, MemoryMappedFileAccess.ReadWrite);
        var accessor = map.CreateViewAccessor(0, frame.Length, MemoryMappedFileAccess.Write);
        accessor.WriteArray(0, frame, 0, frame.Length);
        return new SegmentHandle(map, accessor);
    }

    /// <summary>创建在线且身份完整的 bridge health。</summary>
    private static WorkbenchBridgeHealth CreateHealth(string engineId, long generation)
    {
        return new WorkbenchBridgeHealth(
            WorkbenchBridgeConnectionState.Online,
            "online",
            string.Empty,
            Array.Empty<string>(),
            0L,
            15L,
            "telemetry-session",
            generation,
            "PlayMode",
            1L);
    }

    /// <summary>统一释放测试 Shared Memory 句柄。</summary>
    private sealed record SegmentHandle(
        MemoryMappedFile Map,
        MemoryMappedViewAccessor Accessor) : IDisposable
    {
        /// <summary>释放 view 后再释放 map。</summary>
        public void Dispose()
        {
            Accessor.Dispose();
            Map.Dispose();
        }
    }
}
