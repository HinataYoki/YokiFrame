using System.Text.Json.Nodes;
using YokiFrame.Client;
using YokiFrame.Client.Commands;
using YokiFrame.Client.FileBridge;
using YokiFrame.Client.FileBridge.Diagnostics;
using YokiFrame.Protocol.FileBridge;
using YokiFrame.Protocol.Telemetry.SharedMemory;
using YokiFrame.Tooling.Application.Models;
using YokiFrame.Tooling.Application.Services;

namespace YokiFrame.Tooling.Application.Tests;

/// <summary>覆盖 FsmKit 总览与按实例命名 Shared Memory latest frame 的应用层选择。</summary>
public sealed class WorkbenchFsmKitTelemetryTests
{
    private const string OVERVIEW_PAYLOAD = """
        {"fsmName":"Default","instanceId":"fsm-00000001","fsms":[{"instanceId":"fsm-00000001","name":"Default","machineState":"Running","currentState":"Idle","currentStateId":0,"stateCount":1},{"instanceId":"fsm-00000002","name":"Chosen","machineState":"Running","currentState":"Battle","currentStateId":2,"stateCount":2}],"count":2,"selected":{"fsmName":"Default","instanceId":"fsm-00000001","machineState":"Running","currentState":"Idle","currentStateId":0,"stateCount":1,"states":[]},"history":{"history":[],"count":0},"stateEvents":{"events":[],"count":0}}
        """;

    private const string CHOSEN_PAYLOAD = """
        {"fsmName":"Chosen","instanceId":"fsm-00000002","fsms":[{"instanceId":"fsm-00000001","name":"Default","machineState":"Running","currentState":"Idle","currentStateId":0,"stateCount":1},{"instanceId":"fsm-00000002","name":"Chosen","machineState":"Running","currentState":"Battle","currentStateId":2,"stateCount":2}],"count":2,"selected":{"fsmName":"Chosen","instanceId":"fsm-00000002","machineState":"Running","currentState":"Battle","currentStateId":2,"stateCount":2,"states":[]},"history":{"history":[{"from":"Ready","to":"Battle","time":"12:00:00.000"}],"count":1},"stateEvents":{"events":[],"count":0}}
        """;

    /// <summary>
    /// 验证非默认选择直接读取该 instanceId 的命名 Shared Memory，不解析总览且不发送命令。
    /// </summary>
    [Fact]
    public void ReadFsmKitTelemetryUsesSelectedInstanceLatestFrameWithoutCommand()
    {
        Dictionary<string, string> payloads = new()
        {
            ["state"] = OVERVIEW_PAYLOAD,
            ["fsm-00000002"] = CHOSEN_PAYLOAD
        };
        TelemetryFsmClient client = new(payloads);
        WorkbenchBridgeHealth health = CreateOnlineHealth();

        var state = new WorkbenchDashboardService(client).ReadFsmKitTelemetry(
            "unity-editor",
            health,
            "fsm-00000002");

        Assert.NotNull(state);
        Assert.Equal("fsm-00000002", state.Selected?.InstanceId);
        Assert.Equal("Battle", Assert.Single(state.History).To);
        Assert.Equal(new[] { "fsm-00000002" }, client.TelemetryNames);
    }

    /// <summary>创建与测试 telemetry generation 对齐的在线宿主状态。</summary>
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
            7,
            "EditMode",
            3);
    }

    /// <summary>只实现 Shared Memory 读取的测试 Client；任何命令调用都直接失败。</summary>
    private sealed class TelemetryFsmClient : IYokiFrameClient
    {
        private readonly IReadOnlyDictionary<string, string> mPayloads;
        private readonly List<string> mTelemetryNames = new();

        /// <summary>使用按 name 索引的 latest frame 创建 Client。</summary>
        /// <param name="payloads">标准 state 与实例 payload。</param>
        internal TelemetryFsmClient(IReadOnlyDictionary<string, string> payloads)
        {
            mPayloads = payloads;
            Paths = new YokiFramePaths(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        }

        /// <summary>获取测试路径解析器。</summary>
        public YokiFramePaths Paths { get; }

        /// <summary>获取实际读取的 Telemetry name 顺序。</summary>
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

        /// <summary>按 name 返回已接受的测试 latest frame。</summary>
        public SharedMemoryTelemetryFrameReadResult ReadTelemetry(
            string engineId,
            string kit,
            string name,
            long? expectedGeneration,
            int maxPayloadBytes)
        {
            mTelemetryNames.Add(name);
            return mPayloads.TryGetValue(name, out var payloadJson)
                ? CreateAcceptedFrame(payloadJson, expectedGeneration ?? 0L, mTelemetryNames.Count)
                : new SharedMemoryTelemetryFrameReadResult(
                    SharedMemoryTelemetryFrameStatus.Unavailable,
                    null,
                    string.Empty,
                    "missing");
        }

        /// <summary>按写入时间与序号过滤完整读取结果，为响应式刷新提供真实的未变化语义。</summary>
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

        /// <summary>创建 generation 对齐且已提交的测试帧。</summary>
        private static SharedMemoryTelemetryFrameReadResult CreateAcceptedFrame(
            string payloadJson,
            long generation,
            long sequence)
        {
            SharedMemoryTelemetryFrameHeader header = new(
                SharedMemoryTelemetryFrameHeader.MAGIC,
                SharedMemoryTelemetryFrameHeader.PROTOCOL_VERSION,
                0UL,
                generation,
                sequence,
                DateTimeOffset.UtcNow.UtcTicks,
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
