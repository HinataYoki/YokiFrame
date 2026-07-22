using System.Text.Json.Nodes;
using YokiFrame.Client;
using YokiFrame.Client.Commands;
using YokiFrame.Client.FileBridge;
using YokiFrame.Client.FileBridge.Diagnostics;
using YokiFrame.Protocol.FileBridge;
using YokiFrame.Protocol.Telemetry.SharedMemory;
using YokiFrame.Tooling.Application.Models;
using YokiFrame.Tooling.Application.Models.FsmKit;
using YokiFrame.Tooling.Application.Services;

namespace YokiFrame.Tooling.Application.Tests;

/// <summary>覆盖 FsmKit 高频读取的命名段边界和 parser 前游标短路。</summary>
public sealed class WorkbenchFsmKitTelemetryCursorTests
{
    private const long GENERATION = 7L;
    private const long SEQUENCE = 31L;
    private const long WRITTEN_AT_UTC_TICKS = 638880000000000000L;
    private const string OVERVIEW_PAYLOAD = """
        {"fsmName":"Default","instanceId":"fsm-00000001","fsms":[{"instanceId":"fsm-00000001","name":"Default","machineState":"Running","currentState":"Idle","currentStateId":0,"stateCount":1}],"count":1,"selected":{"fsmName":"Default","instanceId":"fsm-00000001","machineState":"Running","currentState":"Idle","currentStateId":0,"stateCount":1,"states":[]},"history":{"history":[],"count":0},"stateEvents":{"events":[],"count":0}}
        """;

    /// <summary>验证选中实例的命名段缺失时返回空，不读取总览伪装该实例的详情和历史。</summary>
    [Fact]
    public void MissingSelectedSegmentDoesNotFallbackToOverview()
    {
        CursorTelemetryClient client = new(new Dictionary<string, string>
        {
            ["state"] = OVERVIEW_PAYLOAD
        });
        WorkbenchDashboardService service = new(client);

        var result = service.PollFsmKitTelemetry(
            "unity-editor",
            CreateOnlineHealth(),
            "fsm-00000002",
            long.MinValue);

        Assert.Equal(WorkbenchFsmKitTelemetryReadStatus.Unavailable, result.Status);
        Assert.Null(result.State);
        Assert.False(result.HasCursor);
        Assert.Equal(new[] { "fsm-00000002" }, client.TelemetryNames);
    }

    /// <summary>验证命名 payload 身份错误时返回负向游标，下一轮不重复解析同一坏帧。</summary>
    [Fact]
    public void RejectedNamedFrameAdvancesCheckedCursor()
    {
        CursorTelemetryClient client = new(new Dictionary<string, string>
        {
            ["fsm-00000002"] = OVERVIEW_PAYLOAD
        });
        WorkbenchDashboardService service = new(client);

        var rejected = service.PollFsmKitTelemetry(
            "unity-editor", CreateOnlineHealth(), "fsm-00000002", long.MinValue);
        var unchanged = service.PollFsmKitTelemetry(
            "unity-editor", CreateOnlineHealth(), "fsm-00000002", rejected.Sequence);

        Assert.Equal(WorkbenchFsmKitTelemetryReadStatus.Rejected, rejected.Status);
        Assert.True(rejected.HasCursor);
        Assert.Equal(WorkbenchFsmKitTelemetryReadStatus.Unchanged, unchanged.Status);
        Assert.Equal(2, client.TelemetryNames.Count);
    }

    /// <summary>验证 Writing/HalfWrite 只返回短重试信号，不携带会吞掉随后 committed 帧的负向游标。</summary>
    [Theory]
    [InlineData(SharedMemoryTelemetryFrameStatus.Writing)]
    [InlineData(SharedMemoryTelemetryFrameStatus.HalfWrite)]
    public void TransientFrameDoesNotAdvanceCheckedCursor(SharedMemoryTelemetryFrameStatus status)
    {
        CursorTelemetryClient client = new(CreateFrameResult(status, string.Empty));
        WorkbenchDashboardService service = new(client);

        var result = service.PollFsmKitTelemetry(
            "unity-editor", CreateOnlineHealth(), string.Empty, long.MinValue);

        Assert.Equal(WorkbenchFsmKitTelemetryReadStatus.Retryable, result.Status);
        Assert.False(result.HasCursor);
        Assert.Null(result.State);
    }

    /// <summary>验证原始协议拒绝不信任 header sequence，避免错误宿主或损坏帧冻结后续合法帧。</summary>
    [Theory]
    [InlineData(SharedMemoryTelemetryFrameStatus.InvalidMagic)]
    [InlineData(SharedMemoryTelemetryFrameStatus.UnsupportedVersion)]
    [InlineData(SharedMemoryTelemetryFrameStatus.EngineIdHashMismatch)]
    [InlineData(SharedMemoryTelemetryFrameStatus.PayloadTooLarge)]
    [InlineData(SharedMemoryTelemetryFrameStatus.CrcMismatch)]
    public void ProtocolRejectedFrameDoesNotExposeUntrustedCursor(
        SharedMemoryTelemetryFrameStatus status)
    {
        CursorTelemetryClient client = new(CreateFrameResult(status, string.Empty));
        WorkbenchDashboardService service = new(client);

        var result = service.PollFsmKitTelemetry(
            "unity-editor", CreateOnlineHealth(), string.Empty, 10L);

        Assert.Equal(WorkbenchFsmKitTelemetryReadStatus.Rejected, result.Status);
        Assert.False(result.HasCursor);
        Assert.Equal(long.MinValue, result.Sequence);
        Assert.Null(result.State);
    }

    /// <summary>验证未变化帧即使 payload 不是合法 JSON，也会在应用层 parser 之前按 header 游标跳过。</summary>
    [Fact]
    public void UnchangedHeaderSkipsInvalidPayloadBeforeJsonParser()
    {
        CursorTelemetryClient client = new(new Dictionary<string, string>
        {
            ["state"] = "not-json"
        });
        WorkbenchDashboardService service = new(client);
        WorkbenchFsmKitTelemetryReadResult? result = null;

        var exception = Record.Exception(() =>
        {
            result = service.PollFsmKitTelemetry(
                "unity-editor",
                CreateOnlineHealth(),
                string.Empty,
                SEQUENCE);
        });

        Assert.Null(exception);
        Assert.NotNull(result);
        Assert.Equal(WorkbenchFsmKitTelemetryReadStatus.Unchanged, result.Status);
        Assert.False(result.HasCursor);
    }

    /// <summary>验证同一 generation 内 sequence 未增长时不重复解析同一帧。</summary>
    [Fact]
    public void SameSequenceDoesNotAcceptFrameAgain()
    {
        CursorTelemetryClient client = new(new Dictionary<string, string>
        {
            ["state"] = OVERVIEW_PAYLOAD
        });
        WorkbenchDashboardService service = new(client);

        var result = service.PollFsmKitTelemetry(
            "unity-editor",
            CreateOnlineHealth(),
            string.Empty,
            SEQUENCE);

        Assert.Equal(WorkbenchFsmKitTelemetryReadStatus.Unchanged, result.Status);
        Assert.Null(result.State);
    }

    /// <summary>验证 sequence 前进时接受新帧，并保留 header 写入时间供显示与诊断。</summary>
    [Fact]
    public void HigherSequenceAcceptsFrameAndPreservesWrittenAtTime()
    {
        CursorTelemetryClient client = new(new Dictionary<string, string>
        {
            ["state"] = OVERVIEW_PAYLOAD
        });
        WorkbenchDashboardService service = new(client);

        var result = service.PollFsmKitTelemetry(
            "unity-editor",
            CreateOnlineHealth(),
            string.Empty,
            SEQUENCE - 1L);

        Assert.Equal(WorkbenchFsmKitTelemetryReadStatus.Accepted, result.Status);
        Assert.NotNull(result.State);
        Assert.Equal(SEQUENCE, result.Sequence);
        Assert.Equal(WRITTEN_AT_UTC_TICKS, result.WrittenAtUtcTicks);
    }

    /// <summary>创建指定底层状态并携带稳定 header 的测试读取结果。</summary>
    /// <param name="status">底层 frame 状态。</param>
    /// <param name="payloadJson">可选 payload。</param>
    /// <returns>使用当前测试 generation 和游标的 frame 结果。</returns>
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
            status == SharedMemoryTelemetryFrameStatus.Writing
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
            "EditMode",
            3);
    }

    /// <summary>为应用层测试提供固定 header 的 Shared Memory Client。</summary>
    private sealed class CursorTelemetryClient : IYokiFrameClient
    {
        private readonly IReadOnlyDictionary<string, string> mPayloads;
        private readonly Queue<SharedMemoryTelemetryFrameReadResult>? mFrames;
        private readonly List<string> mTelemetryNames = new();

        /// <summary>使用按 segment name 索引的 payload 创建测试 Client。</summary>
        /// <param name="payloads">测试 payload。</param>
        internal CursorTelemetryClient(IReadOnlyDictionary<string, string> payloads)
        {
            mPayloads = payloads;
            Paths = new YokiFramePaths(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        }

        /// <summary>使用有序底层 frame 创建测试 Client，供瞬态到稳定状态序列使用。</summary>
        /// <param name="frames">每次读取依次返回的 frame。</param>
        internal CursorTelemetryClient(params SharedMemoryTelemetryFrameReadResult[] frames)
        {
            mPayloads = new Dictionary<string, string>();
            mFrames = new Queue<SharedMemoryTelemetryFrameReadResult>(frames);
            Paths = new YokiFramePaths(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        }

        /// <summary>获取测试路径解析器。</summary>
        public YokiFramePaths Paths { get; }

        /// <summary>获取实际读取的 segment name 顺序。</summary>
        public IReadOnlyList<string> TelemetryNames => mTelemetryNames;

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

        /// <summary>按 name 返回固定帧头的测试 payload。</summary>
        public SharedMemoryTelemetryFrameReadResult ReadTelemetry(
            string engineId,
            string kit,
            string name,
            long? expectedGeneration,
            int maxPayloadBytes)
        {
            mTelemetryNames.Add(name);
            if (mFrames != null && mFrames.Count > 0)
            {
                return mFrames.Dequeue();
            }

            return mPayloads.TryGetValue(name, out var payloadJson)
                ? CreateAcceptedFrame(payloadJson)
                : new SharedMemoryTelemetryFrameReadResult(
                    SharedMemoryTelemetryFrameStatus.Unavailable,
                    null,
                    string.Empty,
                    "missing");
        }

        /// <summary>按生产游标顺序过滤完整读取结果，使未变化帧在 JSON 解析前返回空。</summary>
        public SharedMemoryTelemetryFrameReadResult? ReadTelemetryIfChanged(
            string engineId,
            string kit,
            string name,
            long? expectedGeneration,
            int maxPayloadBytes,
            long afterSequence)
        {
            var frame = ReadTelemetry(engineId, kit, name, expectedGeneration, maxPayloadBytes);
            return TelemetryFrameCursorTestHelper.Filter(frame, afterSequence);
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

        /// <summary>创建固定 generation、sequence 和时间的已接受帧。</summary>
        /// <param name="payloadJson">测试 payload。</param>
        /// <returns>已接受读取结果。</returns>
        private static SharedMemoryTelemetryFrameReadResult CreateAcceptedFrame(string payloadJson)
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
                SharedMemoryTelemetryWriteState.Committed);
            return new SharedMemoryTelemetryFrameReadResult(
                SharedMemoryTelemetryFrameStatus.Accepted,
                header,
                payloadJson,
                "accepted");
        }
    }
}
