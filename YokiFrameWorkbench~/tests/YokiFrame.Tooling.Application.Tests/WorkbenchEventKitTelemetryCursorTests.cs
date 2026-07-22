using System.Text.Json.Nodes;
using YokiFrame.Client;
using YokiFrame.Client.Commands;
using YokiFrame.Client.FileBridge;
using YokiFrame.Client.FileBridge.Diagnostics;
using YokiFrame.Protocol.FileBridge;
using YokiFrame.Protocol.Telemetry.SharedMemory;
using YokiFrame.Tooling.Application.Models;
using YokiFrame.Tooling.Application.Models.EventKit;
using YokiFrame.Tooling.Application.Services;

namespace YokiFrame.Tooling.Application.Tests;

/// <summary>覆盖 EventKit 高频读取的 parser 前游标短路和瞬态帧处理。</summary>
public sealed class WorkbenchEventKitTelemetryCursorTests
{
    private const long GENERATION = 7L;
    private const long SEQUENCE = 31L;
    private const long WRITTEN_AT_UTC_TICKS = 638880000000000000L;
    private const string EVENT_PAYLOAD = """
        {"version":12,"sequence":4,"counts":{"typeEvents":1,"enumEvents":0,"stringEvents":0,"totalEvents":1,"totalHandlers":2,"recentActivities":1},"events":[{"channel":"Type","eventKey":"DamageEvent","payloadType":"DamageEvent","handlerCount":2,"lastSequence":4,"lastTime":"10:00:00.040","deprecated":false}],"recentEvents":{"count":1,"events":[{"sequence":4,"kind":"send","channel":"Type","eventKey":"DamageEvent","payloadType":"DamageEvent","handler":"","time":"10:00:00.040"}]}}
        """;

    /// <summary>验证 sequence 前进时接受新帧，并保留可信 header 游标。</summary>
    [Fact]
    public void HigherSequenceAcceptsFrame()
    {
        EventTelemetryClient client = new(CreateFrameResult(
            SharedMemoryTelemetryFrameStatus.Accepted,
            EVENT_PAYLOAD));
        WorkbenchDashboardService service = new(client);

        var result = service.PollEventKitTelemetry(
            "unity-editor",
            CreateOnlineHealth(),
            SEQUENCE - 1L);

        Assert.Equal(WorkbenchEventKitTelemetryReadStatus.Accepted, result.Status);
        Assert.NotNull(result.State);
        Assert.True(result.HasCursor);
        Assert.Equal(SEQUENCE, result.Sequence);
    }

    /// <summary>验证相同 sequence 会在 JSON parser 前跳过无效 payload。</summary>
    [Fact]
    public void UnchangedHeaderSkipsInvalidPayloadBeforeJsonParser()
    {
        EventTelemetryClient client = new(CreateFrameResult(
            SharedMemoryTelemetryFrameStatus.Accepted,
            "not-json"));
        WorkbenchDashboardService service = new(client);

        var result = service.PollEventKitTelemetry(
            "unity-editor",
            CreateOnlineHealth(),
            SEQUENCE);

        Assert.Equal(WorkbenchEventKitTelemetryReadStatus.Unchanged, result.Status);
        Assert.Null(result.State);
        Assert.False(result.HasCursor);
    }

    /// <summary>验证 HalfWrite 只返回短重试信号，不携带会吞掉后续帧的游标。</summary>
    [Fact]
    public void HalfWriteFrameReturnsRetryableWithoutCursor()
    {
        EventTelemetryClient client = new(CreateFrameResult(
            SharedMemoryTelemetryFrameStatus.HalfWrite,
            string.Empty));
        WorkbenchDashboardService service = new(client);

        var result = service.PollEventKitTelemetry(
            "unity-editor",
            CreateOnlineHealth(),
            long.MinValue);

        Assert.Equal(WorkbenchEventKitTelemetryReadStatus.Retryable, result.Status);
        Assert.Null(result.State);
        Assert.False(result.HasCursor);
    }

    /// <summary>验证可信 header 上的无效 JSON 返回拒绝，并保留已检查 sequence。</summary>
    [Fact]
    public void InvalidJsonReturnsRejectedWithTrustedCursor()
    {
        EventTelemetryClient client = new(CreateFrameResult(
            SharedMemoryTelemetryFrameStatus.Accepted,
            "not-json"));
        WorkbenchDashboardService service = new(client);

        var result = service.PollEventKitTelemetry(
            "unity-editor",
            CreateOnlineHealth(),
            SEQUENCE - 1L);

        Assert.Equal(WorkbenchEventKitTelemetryReadStatus.Rejected, result.Status);
        Assert.Null(result.State);
        Assert.True(result.HasCursor);
        Assert.Equal(SEQUENCE, result.Sequence);
        Assert.Contains("invalid JSON", result.Diagnostic, StringComparison.Ordinal);
    }

    /// <summary>创建指定底层状态并携带稳定 header 的测试读取结果。</summary>
    /// <param name="status">底层 frame 状态。</param>
    /// <param name="payloadJson">测试 payload。</param>
    /// <returns>使用当前测试 generation 和 sequence 的 frame 结果。</returns>
    private static SharedMemoryTelemetryFrameReadResult CreateFrameResult(
        SharedMemoryTelemetryFrameStatus status,
        string payloadJson)
    {
        SharedMemoryTelemetryFrameHeader header = new(
            SharedMemoryTelemetryFrameHeader.MAGIC,
            SharedMemoryTelemetryFrameHeader.PROTOCOL_VERSION,
            0UL,
            GENERATION,
            SEQUENCE,
            WRITTEN_AT_UTC_TICKS,
            payloadJson.Length,
            0U,
            status == SharedMemoryTelemetryFrameStatus.HalfWrite
                ? SharedMemoryTelemetryWriteState.Writing
                : SharedMemoryTelemetryWriteState.Committed);
        return new SharedMemoryTelemetryFrameReadResult(status, header, payloadJson, status.ToString());
    }

    /// <summary>创建与测试帧 generation 对齐的在线宿主状态。</summary>
    /// <returns>允许读取 Shared Memory 的健康信息。</returns>
    private static WorkbenchBridgeHealth CreateOnlineHealth()
    {
        return new WorkbenchBridgeHealth(
            WorkbenchBridgeConnectionState.Online,
            string.Empty,
            string.Empty,
            Array.Empty<string>(),
            0,
            5,
            "test-session",
            GENERATION,
            "PlayMode",
            3);
    }

    /// <summary>为 EventKit 应用层游标测试提供固定帧的 Client。</summary>
    private sealed class EventTelemetryClient : IYokiFrameClient
    {
        private readonly SharedMemoryTelemetryFrameReadResult mFrame;

        /// <summary>使用固定帧创建测试 Client。</summary>
        /// <param name="frame">每次 Telemetry 读取返回的帧。</param>
        internal EventTelemetryClient(SharedMemoryTelemetryFrameReadResult frame)
        {
            mFrame = frame;
            Paths = new YokiFramePaths(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        }

        /// <summary>获取测试路径解析器。</summary>
        public YokiFramePaths Paths { get; }

        /// <summary>本测试不读取 harness。</summary>
        public JsonNode ReadHarnessCapabilities() => throw new NotSupportedException();

        /// <summary>本测试不读取 registry。</summary>
        public IReadOnlyList<EngineRegistryEntry> ReadEngineEntries() => throw new NotSupportedException();

        /// <summary>本测试不读取 snapshot。</summary>
        public JsonNode ReadSnapshot(string engineId, string kit, string name) => throw new NotSupportedException();

        /// <summary>本测试不读取 heartbeat。</summary>
        public HeartbeatInfo? ReadHeartbeat(string engineId) => throw new NotSupportedException();

        /// <summary>本测试不读取 FileBridge 状态。</summary>
        public FileBridgeStatus ReadBridgeStatus(string engineId) => throw new NotSupportedException();

        /// <summary>返回固定的完整 Shared Memory 帧。</summary>
        public SharedMemoryTelemetryFrameReadResult ReadTelemetry(
            string engineId,
            string kit,
            string name,
            long? expectedGeneration,
            int maxPayloadBytes)
        {
            return mFrame;
        }

        /// <summary>按生产游标顺序过滤固定帧，使未变化帧在 JSON 解析前返回空。</summary>
        public SharedMemoryTelemetryFrameReadResult? ReadTelemetryIfChanged(
            string engineId,
            string kit,
            string name,
            long? expectedGeneration,
            int maxPayloadBytes,
            long afterSequence)
        {
            return TelemetryFrameCursorTestHelper.Filter(mFrame, afterSequence);
        }

        /// <summary>拒绝测试范围外的 FastChannel System 命令。</summary>
        public Task<CommandResponse> SendFastChannelReadOnlySystemCommandAsync(
            string engineId,
            string action,
            string source,
            int timeoutMs,
            CancellationToken cancellationToken) => throw new InvalidOperationException("周期 telemetry 不得发送 command。");

        /// <summary>拒绝测试范围外的 FileBridge 命令。</summary>
        public Task<CommandSendResult> SendCommandAsync(
            string engineId,
            string kit,
            string action,
            string payloadJson,
            string source,
            int timeoutMs,
            CancellationToken cancellationToken) => throw new InvalidOperationException("周期 telemetry 不得发送 command。");
    }
}
