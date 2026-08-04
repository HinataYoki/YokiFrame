using System.Text.Json.Nodes;
using YokiFrame.Client;
using YokiFrame.Client.Commands;
using YokiFrame.Client.FileBridge;
using YokiFrame.Client.FileBridge.Diagnostics;
using YokiFrame.Protocol.FileBridge;
using YokiFrame.Protocol.Results;
using YokiFrame.Protocol.Telemetry.SharedMemory;
using YokiFrame.Tooling.Application.Services;

namespace YokiFrame.Tooling.Application.Tests;

/// <summary>
/// 覆盖 FsmKit Application 对协议身份、payload 形状、证据和时间的防御性读取。
/// </summary>
public sealed partial class WorkbenchFsmKitStateTests
{
    /// <summary>
    /// 验证旧 generation 的 snapshot 不会被投影成当前宿主的 FSM 状态。
    /// </summary>
    [Fact]
    public void LoadDashboardRejectsFsmSnapshotFromPreviousGeneration()
    {
        var projectRoot = CreateProjectRoot();
        WriteOnlineBridge(projectRoot, FSM_PAYLOAD);
        MutateFsmSnapshot(projectRoot, static snapshot => snapshot["generation"] = 6);

        var state = new WorkbenchDashboardService(projectRoot).LoadDashboard("unity-editor");
        var fsmState = Assert.IsType<YokiFrame.Tooling.Application.Models.FsmKit.WorkbenchFsmKitState>(state.FsmKitState);

        Assert.Empty(fsmState.Machines);
        Assert.Null(fsmState.Selected);
        Assert.Equal(string.Empty, fsmState.SessionId);
        Assert.Equal(0, fsmState.Generation);
        Assert.Contains("generation", fsmState.StaleReason, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 验证合法 JSON 对象缺少 FsmKit 工作台必需字段时会明确标记 stale。
    /// </summary>
    [Fact]
    public void LoadDashboardMarksIncompleteFsmPayloadAsStale()
    {
        var projectRoot = CreateProjectRoot();
        WriteOnlineBridge(projectRoot, "{}");

        var state = new WorkbenchDashboardService(projectRoot).LoadDashboard("unity-editor");
        var fsmState = Assert.IsType<YokiFrame.Tooling.Application.Models.FsmKit.WorkbenchFsmKitState>(state.FsmKitState);

        Assert.Empty(fsmState.Machines);
        Assert.Contains("FsmKit requires property", fsmState.StaleReason, StringComparison.Ordinal);
    }

    /// <summary>
    /// 验证非字符串 payloadJson 不会把 snapshot 外层信封冒充成 FsmKit 业务 payload。
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LoadDashboardRejectsMissingOrNonStringSnapshotPayload(bool removePayload)
    {
        var projectRoot = CreateProjectRoot();
        WriteOnlineBridge(projectRoot, FSM_PAYLOAD);
        MutateFsmSnapshot(projectRoot, snapshot =>
        {
            if (removePayload)
            {
                snapshot.Remove("payloadJson");
                return;
            }

            snapshot["payloadJson"] = JsonNode.Parse(FSM_PAYLOAD);
        });

        var state = new WorkbenchDashboardService(projectRoot).LoadDashboard("unity-editor");
        var fsmState = Assert.IsType<YokiFrame.Tooling.Application.Models.FsmKit.WorkbenchFsmKitState>(state.FsmKitState);

        Assert.Empty(fsmState.Machines);
        Assert.Equal(string.Empty, fsmState.RawPayloadJson);
        Assert.Contains("payloadJson", fsmState.StaleReason, StringComparison.Ordinal);
    }

    /// <summary>
    /// 验证 Runtime terminal error 会把 command 与 response 路径保留到标准协议异常中。
    /// </summary>
    [Fact]
    public async Task QueryFsmDetailsPreservesTerminalErrorEvidence()
    {
        var client = ScenarioFsmClient.CreateQueryClient(FSM_PAYLOAD);
        client.ResponseStatus = "Error";
        client.ErrorCode = "FsmNotFound";
        client.ErrorMessage = "FSM was not found.";

        var exception = await Assert.ThrowsAsync<YokiFrameProtocolException>(() =>
            new WorkbenchDashboardService(client).QueryFsmDetailsAsync(
                "unity-editor",
                "fsm-00000001",
                CancellationToken.None));

        Assert.Equal("FsmNotFound", exception.Error.Code);
        Assert.Equal(new[] { "fsm-command.json", "fsm-response.json" }, exception.Error.EvidencePaths);
    }

    /// <summary>
    /// 验证命令等待期间 registry 换代时保留结果，但不冒充任一宿主身份。
    /// </summary>
    [Fact]
    public async Task QueryFsmDetailsMarksHostRestartRaceAsStale()
    {
        var client = ScenarioFsmClient.CreateQueryClient(FSM_PAYLOAD);
        client.RegistryAfterCommand = CreateRegistry("session-after", 8, includeTelemetry: false);

        var state = await new WorkbenchDashboardService(client).QueryFsmDetailsAsync(
            "unity-editor",
            "fsm-00000001",
            CancellationToken.None);

        Assert.Equal("Battle", state.Selected?.CurrentState);
        Assert.Equal(string.Empty, state.SessionId);
        Assert.Equal(0, state.Generation);
        Assert.Contains("changed", state.StaleReason, StringComparison.OrdinalIgnoreCase);
        // QueryFsmDetails 读取命令前后身份各一次；CommandExecutionService 的 FastChannel cooldown
        // 还会通过 IEngineStateReader 读取一次当前代次，即使本次 FsmKit 命令最终走 FileBridge。
        Assert.Equal(3, client.RegistryReadCount);
    }

    /// <summary>
    /// 验证 registry 缺少可用 session 或 generation 时不会被当作已确认的命令宿主身份。
    /// </summary>
    [Fact]
    public async Task QueryFsmDetailsMarksIncompleteRegistryIdentityAsStale()
    {
        var client = ScenarioFsmClient.CreateQueryClient(FSM_PAYLOAD, CreateRegistry(string.Empty, 0L, includeTelemetry: false));

        var state = await new WorkbenchDashboardService(client).QueryFsmDetailsAsync(
            "unity-editor",
            "fsm-00000001",
            CancellationToken.None);

        Assert.Equal(string.Empty, state.SessionId);
        Assert.Equal(0, state.Generation);
        Assert.Contains("could not be confirmed", state.StaleReason, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 验证 telemetry 失败回落 snapshot 后同时保留内存段和文件证据。
    /// </summary>
    [Fact]
    public void LoadDashboardPreservesTelemetryAndSnapshotFallbackEvidence()
    {
        var client = ScenarioFsmClient.CreateDashboardClient(FSM_PAYLOAD);

        var state = new WorkbenchDashboardService(client).LoadDashboard("unity-editor");
        var fsmState = Assert.IsType<YokiFrame.Tooling.Application.Models.FsmKit.WorkbenchFsmKitState>(state.FsmKitState);

        Assert.Equal("snapshot", fsmState.Source);
        Assert.Contains("telemetry unavailable", fsmState.StaleReason, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(2, fsmState.EvidencePaths.Count);
        Assert.Contains(
            SharedMemoryTelemetrySegmentName.Create(client.Paths.ProjectRoot, "unity-editor", "FsmKit", "state"),
            fsmState.EvidencePaths);
        Assert.Contains(client.Paths.GetSnapshotPath("unity-editor", "FsmKit", "state"), fsmState.EvidencePaths);
    }

    /// <summary>
    /// 验证 snapshot 时间无效时仍可查看 payload，但来源明确标记 stale。
    /// </summary>
    [Fact]
    public void LoadDashboardMarksInvalidSnapshotTimestampAsStale()
    {
        var projectRoot = CreateProjectRoot();
        WriteOnlineBridge(projectRoot, FSM_PAYLOAD);
        MutateFsmSnapshot(projectRoot, static snapshot => snapshot["writtenAtUtc"] = "not-a-time");

        var state = new WorkbenchDashboardService(projectRoot).LoadDashboard("unity-editor");
        var fsmState = Assert.IsType<YokiFrame.Tooling.Application.Models.FsmKit.WorkbenchFsmKitState>(state.FsmKitState);

        Assert.Equal("Battle", fsmState.Selected?.CurrentState);
        Assert.Equal(DateTimeOffset.MinValue, fsmState.UpdatedAtUtc);
        Assert.Contains("writtenAtUtc", fsmState.StaleReason, StringComparison.Ordinal);
    }

    /// <summary>
    /// 验证 command 完成时间无效时不伪造成当前时间，并明确标记 stale。
    /// </summary>
    [Fact]
    public async Task QueryFsmDetailsMarksInvalidCompletionTimestampAsStale()
    {
        var client = ScenarioFsmClient.CreateQueryClient(FSM_PAYLOAD);
        client.CompletedAtUtc = "not-a-time";

        var state = await new WorkbenchDashboardService(client).QueryFsmDetailsAsync(
            "unity-editor",
            "fsm-00000001",
            CancellationToken.None);

        Assert.Equal(DateTimeOffset.MinValue, state.UpdatedAtUtc);
        Assert.Contains("completedAtUtc", state.StaleReason, StringComparison.Ordinal);
    }

    /// <summary>
    /// 修改测试项目中的 FsmKit snapshot 外层信封并写回原路径。
    /// </summary>
    /// <param name="projectRoot">测试项目根目录。</param>
    /// <param name="mutation">信封修改动作。</param>
    private static void MutateFsmSnapshot(string projectRoot, Action<JsonObject> mutation)
    {
        var path = Path.Combine(projectRoot, ".yokiframe", "engines", "unity-editor", "snapshots", "FsmKit", "state.json");
        var snapshot = JsonNode.Parse(File.ReadAllText(path))?.AsObject()
            ?? throw new InvalidOperationException("FsmKit test snapshot is missing.");
        mutation(snapshot);
        File.WriteAllText(path, snapshot.ToJsonString());
    }

    /// <summary>
    /// 创建测试 registry，并按需声明 telemetry 能力。
    /// </summary>
    /// <param name="sessionId">会话标识。</param>
    /// <param name="generation">宿主 generation。</param>
    /// <param name="includeTelemetry">是否声明 telemetry.read。</param>
    /// <returns>测试 registry。</returns>
    private static EngineRegistryEntry CreateRegistry(string sessionId, long generation, bool includeTelemetry)
    {
        return new EngineRegistryEntry
        {
            ProtocolVersion = 2,
            EngineId = "unity-editor",
            Engine = "Unity",
            SessionId = sessionId,
            Generation = generation,
            Mode = "EditMode",
            Capabilities = includeTelemetry ? new List<string> { "snapshot.read", "telemetry.read" } : new List<string> { "snapshot.read" }
        };
    }

    /// <summary>
    /// 为 dashboard 和显式命令场景提供可控的 Client 边界。
    /// </summary>
    private sealed class ScenarioFsmClient : IYokiFrameClient
    {
        private readonly string mFsmPayload;

        /// <summary>
        /// 使用指定 FsmKit payload 和初始 registry 创建场景 Client。
        /// </summary>
        private ScenarioFsmClient(string fsmPayload, EngineRegistryEntry registry)
        {
            mFsmPayload = fsmPayload;
            RegistryBeforeCommand = registry;
            RegistryAfterCommand = registry;
            Paths = new YokiFramePaths(CreateProjectRoot());
        }

        /// <summary>获取测试路径解析器。</summary>
        public YokiFramePaths Paths { get; }

        /// <summary>获取命令前 registry。</summary>
        public EngineRegistryEntry RegistryBeforeCommand { get; }

        /// <summary>获取或设置命令后 registry。</summary>
        public EngineRegistryEntry RegistryAfterCommand { get; set; }

        /// <summary>获取 registry 读取次数。</summary>
        public int RegistryReadCount { get; private set; }

        /// <summary>获取或设置响应状态。</summary>
        public string ResponseStatus { get; set; } = "Success";

        /// <summary>获取或设置响应错误码。</summary>
        public string ErrorCode { get; set; } = string.Empty;

        /// <summary>获取或设置响应错误信息。</summary>
        public string ErrorMessage { get; set; } = string.Empty;

        /// <summary>获取或设置响应完成时间。</summary>
        public string CompletedAtUtc { get; set; } = "2026-07-11T09:16:00.0000000Z";

        /// <summary>
        /// 创建只执行显式查询的 Client。
        /// </summary>
        public static ScenarioFsmClient CreateQueryClient(string fsmPayload, EngineRegistryEntry? registry = null)
        {
            return new ScenarioFsmClient(fsmPayload, registry ?? CreateRegistry("session-before", 7, includeTelemetry: false));
        }

        /// <summary>
        /// 创建 telemetry 失败并回落 snapshot 的 dashboard Client。
        /// </summary>
        public static ScenarioFsmClient CreateDashboardClient(string fsmPayload)
        {
            return new ScenarioFsmClient(fsmPayload, CreateRegistry("session-before", 7, includeTelemetry: true));
        }

        /// <summary>返回最小 harness 状态。</summary>
        public JsonNode ReadHarnessCapabilities() => JsonNode.Parse("{\"package\":{\"name\":\"YokiFrame\"}}")!;

        /// <summary>按调用顺序返回命令前后 registry。</summary>
        public IReadOnlyList<EngineRegistryEntry> ReadEngineEntries()
        {
            RegistryReadCount++;
            return new[] { RegistryReadCount == 1 ? RegistryBeforeCommand : RegistryAfterCommand };
        }

        /// <summary>返回与请求身份一致的 snapshot 信封。</summary>
        public JsonNode ReadSnapshot(string engineId, string kit, string name)
        {
            return new JsonObject
            {
                ["protocolVersion"] = 2,
                ["engineId"] = engineId,
                ["kit"] = kit,
                ["name"] = name,
                ["generation"] = RegistryAfterCommand.Generation,
                ["sequence"] = 4,
                ["writtenAtUtc"] = "2026-07-11T09:15:00.0000000Z",
                ["payloadJson"] = kit == "FsmKit" ? mFsmPayload : "{\"status\":\"online\"}"
            };
        }

        /// <summary>返回当前在线 heartbeat。</summary>
        public HeartbeatInfo? ReadHeartbeat(string engineId)
        {
            return CreateHeartbeat(engineId);
        }

        /// <summary>返回包含当前 heartbeat 的 bridge 状态。</summary>
        public FileBridgeStatus ReadBridgeStatus(string engineId)
        {
            return new FileBridgeStatus(engineId, Paths.GetEngineRoot(engineId), Paths.GetCommandsRoot(engineId), Paths.GetResultsRoot(engineId))
            {
                Heartbeat = CreateHeartbeat(engineId)
            };
        }

        /// <summary>始终模拟 telemetry 不可用，驱动 snapshot 回落。</summary>
        public SharedMemoryTelemetryFrameReadResult ReadTelemetry(
            string engineId,
            string kit,
            string name,
            long? expectedGeneration,
            int maxPayloadBytes)
        {
            return new SharedMemoryTelemetryFrameReadResult(
                SharedMemoryTelemetryFrameStatus.Unavailable,
                null,
                string.Empty,
                "telemetry unavailable");
        }

        /// <summary>按统一游标规则过滤完整读取结果，同时保留不可用结果以驱动 snapshot 回落。</summary>
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

        /// <summary>返回可配置的 FsmKit terminal response。</summary>
        public Task<CommandSendResult> SendCommandAsync(
            string engineId,
            string kit,
            string action,
            string payloadJson,
            string source,
            int timeoutMs,
            CancellationToken cancellationToken)
        {
            var envelope = CommandEnvelope.Create(engineId, source, "fsm-query", kit, action, payloadJson, timeoutMs);
            CommandResponse response = new()
            {
                ProtocolVersion = 2,
                RequestId = envelope.RequestId,
                EngineId = engineId,
                Status = ResponseStatus,
                ResultJson = mFsmPayload,
                ErrorCode = ErrorCode,
                ErrorMessage = ErrorMessage,
                CompletedAtUtc = CompletedAtUtc
            };
            return Task.FromResult(new CommandSendResult(envelope, "fsm-command.json", "fsm-response.json", response));
        }

        /// <summary>创建与当前 registry 身份一致的在线 heartbeat。</summary>
        private HeartbeatInfo CreateHeartbeat(string engineId)
        {
            return new HeartbeatInfo(
                Paths.GetHeartbeatPath(engineId),
                engineId,
                DateTimeOffset.UtcNow,
                RegistryAfterCommand.SessionId,
                RegistryAfterCommand.Generation,
                RegistryAfterCommand.Mode,
                3);
        }
    }
}
