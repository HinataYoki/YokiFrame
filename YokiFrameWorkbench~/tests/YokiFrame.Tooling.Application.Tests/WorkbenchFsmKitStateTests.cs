using System.Text.Json.Nodes;
using YokiFrame.Client;
using YokiFrame.Client.Commands;
using YokiFrame.Client.FileBridge;
using YokiFrame.Client.FileBridge.Diagnostics;
using YokiFrame.Protocol.FileBridge;
using YokiFrame.Protocol.Telemetry.SharedMemory;
using YokiFrame.Tooling.Application.Services;

namespace YokiFrame.Tooling.Application.Tests;

/// <summary>
/// 覆盖 Workbench 对 FsmKit 强类型状态和显式详情查询的应用层契约。
/// </summary>
public sealed partial class WorkbenchFsmKitStateTests
{
    private const string FSM_PAYLOAD = """
        {"fsmName":"GameFlow","instanceId":"fsm-00000001","fsms":[{"instanceId":"fsm-00000001","name":"GameFlow","machineState":"Running","currentState":"Battle","currentStateId":2,"stateCount":2}],"count":1,"selected":{"fsmName":"GameFlow","instanceId":"fsm-00000001","machineState":"Running","currentState":"Battle","currentStateId":2,"stateCount":2,"states":[{"id":1,"orderIndex":0,"name":"Menu","entryCount":1,"stateType":"MenuState","isCurrent":false,"isComposite":false},{"id":2,"orderIndex":1,"name":"Battle","entryCount":2,"stateType":"BattleMachine","isCurrent":true,"isComposite":true,"childMachineName":"BattleFlow","machineState":"Running","currentState":"Turn","currentStateId":7,"stateCount":1,"children":[{"id":7,"orderIndex":0,"name":"Turn","entryCount":3,"stateType":"TurnState","isCurrent":true,"isComposite":false}]}]},"history":{"history":[{"from":"Menu","to":"Battle","time":"2026-07-11T09:14:00.0000000Z"}],"count":1},"stateEvents":{"events":[{"eventName":"added","state":"Battle","time":"2026-07-11T09:13:00.0000000Z"}],"count":1}}
        """;

    /// <summary>
    /// 验证周期 dashboard 只读取 snapshot，并完整投影 FSM 列表、树、历史、事件和数据源元数据。
    /// </summary>
    [Fact]
    public void LoadDashboardParsesCompleteFsmKitSnapshotWithoutSendingCommand()
    {
        var projectRoot = CreateProjectRoot();
        WriteOnlineBridge(projectRoot, FSM_PAYLOAD);

        var state = new WorkbenchDashboardService(projectRoot).LoadDashboard("unity-editor");
        var fsmState = Assert.IsType<YokiFrame.Tooling.Application.Models.FsmKit.WorkbenchFsmKitState>(state.FsmKitState);

        Assert.Equal("unity-editor", fsmState.EngineId);
        Assert.Equal("test-session", fsmState.SessionId);
        Assert.Equal(7, fsmState.Generation);
        Assert.Equal("EditMode", fsmState.Mode);
        Assert.Equal("snapshot", fsmState.Source);
        Assert.Equal(string.Empty, fsmState.Transport);
        Assert.Equal(DateTimeOffset.Parse("2026-07-11T09:15:00.0000000Z"), fsmState.UpdatedAtUtc);
        Assert.Equal(string.Empty, fsmState.StaleReason);
        Assert.Equal(FSM_PAYLOAD, fsmState.RawPayloadJson);
        Assert.Single(fsmState.EvidencePaths);
        Assert.EndsWith("FsmKit" + Path.DirectorySeparatorChar + "state.json", fsmState.EvidencePaths[0]);
        Assert.Equal("GameFlow", fsmState.FsmName);
        Assert.Equal("fsm-00000001", fsmState.InstanceId);
        Assert.Equal(1, fsmState.DeclaredCount);
        Assert.Equal("Battle", Assert.Single(fsmState.Machines).CurrentState);

        var selected = Assert.IsType<YokiFrame.Tooling.Application.Models.FsmKit.WorkbenchFsmMachineDetails>(fsmState.Selected);
        Assert.Equal("Battle", selected.CurrentState);
        Assert.Equal(2, selected.States.Count);
        Assert.Equal(1L, selected.States[0].EntryCount);
        var composite = Assert.Single(selected.States, static item => item.IsComposite);
        Assert.Equal(2L, composite.EntryCount);
        Assert.Equal("BattleFlow", composite.ChildMachineName);
        var child = Assert.Single(composite.Children);
        Assert.Equal("Turn", child.Name);
        Assert.Equal(3L, child.EntryCount);
        Assert.Equal("Menu", Assert.Single(fsmState.History).From);
        Assert.Equal("added", Assert.Single(fsmState.StateEvents).EventName);

        var commandsRoot = Path.Combine(projectRoot, ".yokiframe", "engines", "unity-editor", "commands");
        Assert.False(Directory.Exists(commandsRoot) && Directory.EnumerateFiles(commandsRoot, "*.json").Any());
    }

    /// <summary>
    /// 验证显式详情查询由 Application 构造 instanceId payload，并返回实际传输和 FileBridge 证据。
    /// </summary>
    [Fact]
    public async Task QueryFsmDetailsUsesApplicationCommandAndReturnsTransportEvidence()
    {
        var client = new RecordingFsmClient(FSM_PAYLOAD);
        var service = new WorkbenchDashboardService(client);

        var state = await service.QueryFsmDetailsAsync(
            "unity-editor",
            "fsm-00000001",
            CancellationToken.None);

        Assert.Equal(1, client.CommandCallCount);
        Assert.Equal("FsmKit", client.LastKit);
        Assert.Equal("get_workbench_snapshot", client.LastAction);
        Assert.Equal("fsm-00000001", JsonNode.Parse(client.LastPayloadJson)?["instanceId"]?.GetValue<string>());
        Assert.Equal("workbench", client.LastSource);
        Assert.Equal("file-bridge", state.Transport);
        Assert.Equal("command", state.Source);
        Assert.Equal("test-session", state.SessionId);
        Assert.Equal(7, state.Generation);
        Assert.Equal("EditMode", state.Mode);
        Assert.Equal(new[] { "fsm-command.json", "fsm-response.json" }, state.EvidencePaths);
        Assert.Equal("Battle", state.Selected?.CurrentState);
        Assert.Equal(FSM_PAYLOAD, state.RawPayloadJson);
    }

    /// <summary>
    /// 创建唯一测试项目根目录。
    /// </summary>
    /// <returns>测试项目根目录。</returns>
    private static string CreateProjectRoot()
    {
        return Path.Combine(Path.GetTempPath(), "yokiframe-workbench-fsm-tests", Guid.NewGuid().ToString("N"));
    }

    /// <summary>
    /// 写入在线 engine、heartbeat、harness 和四个首屏 snapshot。
    /// </summary>
    /// <param name="projectRoot">测试项目根目录。</param>
    /// <param name="fsmPayloadJson">FsmKit payload。</param>
    private static void WriteOnlineBridge(string projectRoot, string fsmPayloadJson)
    {
        var engineRoot = Path.Combine(projectRoot, ".yokiframe", "engines", "unity-editor");
        Directory.CreateDirectory(Path.Combine(projectRoot, ".yokiframe", "harness"));
        Directory.CreateDirectory(Path.Combine(engineRoot, "status"));
        File.WriteAllText(
            Path.Combine(projectRoot, ".yokiframe", "harness", "capabilities.json"),
            "{\"package\":{\"name\":\"YokiFrame\"}}");
        File.WriteAllText(
            Path.Combine(engineRoot, "engine.json"),
            CreateEngineRegistryJson(projectRoot));
        File.WriteAllText(
            Path.Combine(engineRoot, "status", "heartbeat.json"),
            "{\"protocolVersion\":2,\"engineId\":\"unity-editor\",\"sessionId\":\"test-session\",\"generation\":7,\"mode\":\"EditMode\",\"sequence\":3,\"createdAtUtc\":\"" + DateTimeOffset.UtcNow.ToString("O") + "\"}");
        WriteSnapshot(engineRoot, "System", "{\"status\":\"online\"}");
        WriteSnapshot(engineRoot, "FsmKit", fsmPayloadJson);
        WriteSnapshot(engineRoot, "EventKit", "{\"status\":\"online\"}");
        WriteSnapshot(engineRoot, "LogKit", "{\"status\":\"online\"}");
    }

    /// <summary>
    /// 创建带 snapshot 能力但不声明 telemetry 的 engine registry JSON。
    /// </summary>
    /// <param name="projectRoot">测试项目根目录。</param>
    /// <returns>engine registry JSON。</returns>
    private static string CreateEngineRegistryJson(string projectRoot)
    {
        JsonObject registry = new()
        {
            ["protocolVersion"] = 2,
            ["engineId"] = "unity-editor",
            ["engine"] = "Unity",
            ["version"] = "6000.7.0a1",
            ["projectPath"] = projectRoot,
            ["adapterVersion"] = "test",
            ["sessionId"] = "test-session",
            ["generation"] = 7,
            ["mode"] = "EditMode",
            ["capabilities"] = new JsonArray("snapshot.read")
        };
        return registry.ToJsonString();
    }

    /// <summary>
    /// 写入带固定更新时间的 Kit snapshot 外层信封。
    /// </summary>
    /// <param name="engineRoot">engine 协议根目录。</param>
    /// <param name="kit">Kit 名称。</param>
    /// <param name="payloadJson">业务 payload。</param>
    private static void WriteSnapshot(string engineRoot, string kit, string payloadJson)
    {
        var directory = Path.Combine(engineRoot, "snapshots", kit);
        Directory.CreateDirectory(directory);
        JsonObject snapshot = new()
        {
            ["protocolVersion"] = 2,
            ["engineId"] = "unity-editor",
            ["kit"] = kit,
            ["name"] = "state",
            ["generation"] = 7,
            ["sequence"] = 4,
            ["writtenAtUtc"] = "2026-07-11T09:15:00.0000000Z",
            ["payloadJson"] = payloadJson
        };
        File.WriteAllText(Path.Combine(directory, "state.json"), snapshot.ToJsonString());
    }

    /// <summary>
    /// 记录 FsmKit 显式查询，并返回稳定 FileBridge terminal response。
    /// </summary>
    private sealed class RecordingFsmClient : IYokiFrameClient
    {
        private readonly string mResultJson;

        /// <summary>
        /// 使用指定 FsmKit 结果创建记录型 Client。
        /// </summary>
        /// <param name="resultJson">命令业务结果。</param>
        public RecordingFsmClient(string resultJson)
        {
            mResultJson = resultJson;
            Paths = new YokiFramePaths(CreateProjectRoot());
        }

        /// <summary>获取测试路径解析器。</summary>
        public YokiFramePaths Paths { get; }

        /// <summary>获取可靠命令调用次数。</summary>
        public int CommandCallCount { get; private set; }

        /// <summary>获取最近一次 Kit。</summary>
        public string LastKit { get; private set; } = string.Empty;

        /// <summary>获取最近一次 action。</summary>
        public string LastAction { get; private set; } = string.Empty;

        /// <summary>获取最近一次 payload。</summary>
        public string LastPayloadJson { get; private set; } = string.Empty;

        /// <summary>获取最近一次审计来源。</summary>
        public string LastSource { get; private set; } = string.Empty;

        /// <summary>本测试不读取 harness。</summary>
        public JsonNode ReadHarnessCapabilities() => throw new NotSupportedException();

        /// <summary>返回显式查询使用的当前 engine registry。</summary>
        public IReadOnlyList<EngineRegistryEntry> ReadEngineEntries()
        {
            return new[]
            {
                new EngineRegistryEntry
                {
                    ProtocolVersion = 2,
                    EngineId = "unity-editor",
                    Engine = "Unity",
                    SessionId = "test-session",
                    Generation = 7,
                    Mode = "EditMode"
                }
            };
        }

        /// <summary>本测试不读取 snapshot。</summary>
        public JsonNode ReadSnapshot(string engineId, string kit, string name) => throw new NotSupportedException();

        /// <summary>本测试不读取 heartbeat。</summary>
        public HeartbeatInfo? ReadHeartbeat(string engineId) => throw new NotSupportedException();

        /// <summary>本测试不读取 bridge 状态。</summary>
        public FileBridgeStatus ReadBridgeStatus(string engineId) => throw new NotSupportedException();

        /// <summary>本测试不读取 telemetry。</summary>
        public SharedMemoryTelemetryFrameReadResult ReadTelemetry(
            string engineId,
            string kit,
            string name,
            long? expectedGeneration,
            int maxPayloadBytes)
        {
            throw new NotSupportedException();
        }

        /// <summary>本测试不读取增量 telemetry；意外调用时立即失败。</summary>
        public SharedMemoryTelemetryFrameReadResult? ReadTelemetryIfChanged(
            string engineId,
            string kit,
            string name,
            long? expectedGeneration,
            int maxPayloadBytes,
            long afterSequence)
        {
            throw new NotSupportedException();
        }

        /// <summary>FsmKit 查询不允许走 FastChannel。</summary>
        public Task<CommandResponse> SendFastChannelReadOnlySystemCommandAsync(
            string engineId,
            string action,
            string source,
            int timeoutMs,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        /// <summary>记录 FileBridge 命令并返回包含证据路径的结果。</summary>
        public Task<CommandSendResult> SendCommandAsync(
            string engineId,
            string kit,
            string action,
            string payloadJson,
            string source,
            int timeoutMs,
            CancellationToken cancellationToken)
        {
            CommandCallCount++;
            LastKit = kit;
            LastAction = action;
            LastPayloadJson = payloadJson;
            LastSource = source;
            var envelope = CommandEnvelope.Create(engineId, source, "fsm-query", kit, action, payloadJson, timeoutMs);
            CommandResponse response = new()
            {
                ProtocolVersion = 2,
                RequestId = envelope.RequestId,
                EngineId = engineId,
                Status = "Success",
                ResultJson = mResultJson,
                CompletedAtUtc = "2026-07-11T09:16:00.0000000Z"
            };
            return Task.FromResult(new CommandSendResult(envelope, "fsm-command.json", "fsm-response.json", response));
        }
    }
}
