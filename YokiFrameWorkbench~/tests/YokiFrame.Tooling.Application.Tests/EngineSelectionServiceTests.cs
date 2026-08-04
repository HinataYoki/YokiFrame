using System.Text.Json.Nodes;
using YokiFrame.Client;
using YokiFrame.Client.Commands;
using YokiFrame.Client.FileBridge;
using YokiFrame.Client.FileBridge.Diagnostics;
using YokiFrame.Protocol.FileBridge;
using YokiFrame.Protocol.Results;
using YokiFrame.Protocol.Telemetry.SharedMemory;
using YokiFrame.Tooling.Application.Engines;
using YokiFrame.Tooling.Application.Models;
using YokiFrame.Tooling.Application.Services;

namespace YokiFrame.Tooling.Application.Tests;

/// <summary>
/// 覆盖工具应用层的 engine 选择规则和 Client 注入边界。
/// </summary>
public sealed partial class EngineSelectionServiceTests
{
    private static readonly DateTimeOffset NowUtc = DateTimeOffset.Parse("2026-07-10T00:00:00Z");

    /// <summary>
    /// 验证未指定 engine 时只自动选择唯一 heartbeat 在线的 engine。
    /// </summary>
    [Fact]
    public void ResolveSelectsOnlyOnlineEngineWhenRequestIsEmpty()
    {
        var client = new StubYokiFrameClient("unity-editor", "godot-editor");
        client.SetHeartbeat("unity-editor", NowUtc.AddMinutes(-1));
        client.SetHeartbeat("godot-editor", NowUtc.AddSeconds(-2));

        var selectedEngineId = new EngineSelectionService(client).Resolve(string.Empty, NowUtc);

        Assert.Equal("godot-editor", selectedEngineId);
    }

    /// <summary>
    /// 验证多个 engine 同时在线时必须由调用方显式选择，避免把命令发到错误宿主。
    /// </summary>
    [Fact]
    public void ResolveRejectsAmbiguousOnlineEngines()
    {
        var client = new StubYokiFrameClient("unity-editor", "godot-editor");
        client.SetHeartbeat("unity-editor", NowUtc.AddSeconds(-1));
        client.SetHeartbeat("godot-editor", NowUtc.AddSeconds(-2));

        var exception = Assert.Throws<YokiFrameProtocolException>(
            () => new EngineSelectionService(client).Resolve(string.Empty, NowUtc));

        Assert.Equal("EngineSelectionRequired", exception.Error.Code);
        Assert.Contains("unity-editor", exception.Error.Message);
        Assert.Contains("godot-editor", exception.Error.Message);
    }

    /// <summary>
    /// 验证显式 engine 始终优先，允许对尚未写 registry 的宿主目录做诊断。
    /// </summary>
    [Fact]
    public void ResolveKeepsExplicitEngineSelection()
    {
        var client = new StubYokiFrameClient("unity-editor", "godot-editor");

        var selectedEngineId = new EngineSelectionService(client).Resolve("custom-editor", NowUtc);

        Assert.Equal("custom-editor", selectedEngineId);
        Assert.Equal(0, client.EngineReadCount);
    }

    /// <summary>
    /// 验证没有在线 engine 时可返回可恢复选择结果，而不是强制 Workbench 通过异常分支恢复。
    /// </summary>
    [Fact]
    public void SelectReturnsUnavailableResultWhenNoEngineIsOnline()
    {
        var client = new StubYokiFrameClient("unity-editor");

        var result = InvokeSelect(new EngineSelectionService(client), string.Empty, NowUtc);
        var error = ReadProperty<YokiFrameError>(result, "Error");

        Assert.Equal("Unavailable", ReadProperty<object>(result, "Status").ToString());
        Assert.Equal(string.Empty, ReadProperty<string>(result, "SelectedEngineId"));
        Assert.Empty(ReadProperty<IReadOnlyList<string>>(result, "OnlineEngineIds"));
        Assert.Equal("EngineUnavailable", error.Code);
    }

    /// <summary>
    /// 验证多个在线 engine 时结果保留稳定排序的候选，供 Workbench selector 让用户显式恢复。
    /// </summary>
    [Fact]
    public void SelectReturnsSortedCandidatesWhenMultipleEnginesAreOnline()
    {
        var client = new StubYokiFrameClient("unity-editor", "godot-editor");
        client.SetHeartbeat("unity-editor", NowUtc.AddSeconds(-1));
        client.SetHeartbeat("godot-editor", NowUtc.AddSeconds(-2));

        var result = InvokeSelect(new EngineSelectionService(client), string.Empty, NowUtc);
        var error = ReadProperty<YokiFrameError>(result, "Error");

        Assert.Equal("SelectionRequired", ReadProperty<object>(result, "Status").ToString());
        Assert.Equal(string.Empty, ReadProperty<string>(result, "SelectedEngineId"));
        Assert.Equal(new[] { "godot-editor", "unity-editor" }, ReadProperty<IReadOnlyList<string>>(result, "OnlineEngineIds"));
        Assert.Equal("EngineSelectionRequired", error.Code);
    }

    /// <summary>
    /// 验证自动选择唯一在线 engine 时结果同时保留该在线列表，便于调用方统一展示会话证据。
    /// </summary>
    [Fact]
    public void SelectReturnsSelectedResultForOnlyOnlineEngine()
    {
        var client = new StubYokiFrameClient("unity-editor", "godot-editor");
        client.SetHeartbeat("unity-editor", NowUtc.AddMinutes(-1));
        client.SetHeartbeat("godot-editor", NowUtc.AddSeconds(-2));

        var result = InvokeSelect(new EngineSelectionService(client), string.Empty, NowUtc);

        Assert.Equal("Selected", ReadProperty<object>(result, "Status").ToString());
        Assert.Equal("godot-editor", ReadProperty<string>(result, "SelectedEngineId"));
        Assert.Equal(new[] { "godot-editor" }, ReadProperty<IReadOnlyList<string>>(result, "OnlineEngineIds"));
        Assert.Null(ReadNullableProperty(result, "Error"));
    }

    /// <summary>
    /// 验证重复 registry 条目不会把同一个在线 engine 误判为需要用户多选。
    /// </summary>
    [Fact]
    public void SelectDeduplicatesRegistryEntriesBeforeAssessingAmbiguity()
    {
        var client = new StubYokiFrameClient("unity-editor", "unity-editor");
        client.SetHeartbeat("unity-editor", NowUtc.AddSeconds(-1));

        var result = InvokeSelect(new EngineSelectionService(client), string.Empty, NowUtc);

        Assert.Equal("Selected", ReadProperty<object>(result, "Status").ToString());
        Assert.Equal("unity-editor", ReadProperty<string>(result, "SelectedEngineId"));
        Assert.Equal(new[] { "unity-editor" }, ReadProperty<IReadOnlyList<string>>(result, "OnlineEngineIds"));
    }

    /// <summary>
    /// 验证没有在线 engine 时 Dashboard 仍返回 registry 与 harness 状态，并跳过所有 engine 专属读取。
    /// </summary>
    [Fact]
    public void DashboardReturnsRecoverableStateWhenNoEngineIsOnline()
    {
        var client = new StubYokiFrameClient("unity-editor");

        var state = new WorkbenchDashboardService(client).LoadDashboard(string.Empty);
        var selection = ReadProperty<object>(state, "EngineSelection");

        Assert.Equal("Unavailable", ReadProperty<object>(selection, "Status").ToString());
        Assert.Equal(string.Empty, state.SelectedEngineId);
        Assert.Single(state.Engines);
        Assert.Null(state.BridgeStatus);
        Assert.Null(state.DoctorReport);
        Assert.Empty(state.Snapshots);
        Assert.Equal("EngineUnavailable", state.BridgeHealth.State.ToString());
        Assert.Equal(0, client.BridgeReadCount);
        Assert.Equal(0, client.SnapshotReadCount);
        Assert.Equal(0, client.TelemetryReadCount);
        Assert.Equal(1, client.HarnessReadCount);
    }

    /// <summary>
    /// 验证多个在线 engine 时 Dashboard 保留候选并等待用户选择，不读取任一宿主的状态。
    /// </summary>
    [Fact]
    public void DashboardReturnsCandidatesWhenEngineSelectionIsRequired()
    {
        var client = new StubYokiFrameClient("unity-editor", "godot-editor");
        client.SetHeartbeat("unity-editor", DateTimeOffset.UtcNow);
        client.SetHeartbeat("godot-editor", DateTimeOffset.UtcNow);

        var state = new WorkbenchDashboardService(client).LoadDashboard(string.Empty);
        var selection = ReadProperty<object>(state, "EngineSelection");

        Assert.Equal("SelectionRequired", ReadProperty<object>(selection, "Status").ToString());
        Assert.Equal(new[] { "godot-editor", "unity-editor" }, ReadProperty<IReadOnlyList<string>>(selection, "OnlineEngineIds"));
        Assert.Equal(string.Empty, state.SelectedEngineId);
        Assert.Equal(2, state.Engines.Count);
        Assert.Null(state.BridgeStatus);
        Assert.Empty(state.Snapshots);
        Assert.Equal("EngineSelectionRequired", state.BridgeHealth.State.ToString());
        Assert.Equal(0, client.BridgeReadCount);
        Assert.Equal(0, client.SnapshotReadCount);
        Assert.Equal(0, client.TelemetryReadCount);
    }

    /// <summary>
    /// 验证兼容构造器不能创建 Selected 加空 engineId 的非法 dashboard 状态。
    /// </summary>
    [Fact]
    public void DashboardStateRejectsEmptySelectedEngineId()
    {
        WorkbenchBridgeHealth health = new(
            WorkbenchBridgeConnectionState.EngineUnavailable,
            "unavailable",
            "start adapter",
            Array.Empty<string>(),
            null,
            15,
            string.Empty,
            0,
            string.Empty,
            0);

        var exception = Assert.Throws<YokiFrameProtocolException>(() => new WorkbenchDashboardState(
            "F:/Project",
            NowUtc,
            Array.Empty<EngineRegistryEntry>(),
            string.Empty,
            null,
            health,
            null,
            Array.Empty<WorkbenchSnapshotState>(),
            "{}",
            Array.Empty<string>()));

        Assert.Equal("InvalidSafeId", exception.Error.Code);
    }

    /// <summary>
    /// 验证 Dashboard 通过抽象 Client 读取 engine、bridge、snapshot 和 harness，而不创建具体传输实现。
    /// </summary>
    [Fact]
    public void DashboardReadsStateThroughInjectedClient()
    {
        var client = new StubYokiFrameClient("godot-editor");
        client.SetHeartbeat("godot-editor", DateTimeOffset.UtcNow);

        var state = new WorkbenchDashboardService(client).LoadDashboard(string.Empty);

        Assert.Equal("godot-editor", state.SelectedEngineId);
        Assert.NotNull(state.BridgeStatus);
        Assert.Contains(state.Snapshots, static snapshot =>
            snapshot.Kit == "ActionKit" && snapshot.Name == "state");
        Assert.All(state.Snapshots, static snapshot => Assert.True(snapshot.Exists));
        Assert.Equal(state.Snapshots.Count, client.SnapshotReadCount);
        Assert.Equal(1, client.HarnessReadCount);
    }

    /// <summary>
    /// 验证 registry 未声明 telemetry.read 时，Dashboard 直接读取可靠 snapshot，不对不可用实时通道产生无效轮询。
    /// </summary>
    [Fact]
    public void DashboardSkipsTelemetryWhenRegistryDoesNotDeclareTelemetryRead()
    {
        var client = new StubYokiFrameClient("godot-runtime");
        client.SetHeartbeat("godot-runtime", DateTimeOffset.UtcNow);
        client.SetRegistryCapabilities("godot-runtime");

        var state = new WorkbenchDashboardService(client).LoadDashboard(string.Empty);

        Assert.Equal("godot-runtime", state.SelectedEngineId);
        Assert.Contains(state.Snapshots, static snapshot =>
            snapshot.Kit == "ActionKit" && snapshot.Name == "state");
        Assert.Equal(state.Snapshots.Count, client.SnapshotReadCount);
        Assert.Equal(0, client.TelemetryReadCount);
        Assert.All(state.Snapshots, static snapshot => Assert.Equal("snapshot", snapshot.Source));
    }

    /// <summary>
    /// 验证 registry 与 heartbeat 指向不同宿主代次时不会进入自动在线候选。
    /// </summary>
    [Fact]
    public void SelectFiltersRegistryHeartbeatIdentityMismatch()
    {
        var client = new StubYokiFrameClient("unity-editor", "godot-editor");
        client.SetHeartbeat("unity-editor", NowUtc.AddSeconds(-1));
        client.SetHeartbeat("godot-editor", NowUtc.AddSeconds(-2));
        client.SetRegistryIdentity("unity-editor", "old-session", 1L);

        var result = InvokeSelect(new EngineSelectionService(client), string.Empty, NowUtc);

        Assert.Equal("Selected", ReadProperty<object>(result, "Status").ToString());
        Assert.Equal("godot-editor", ReadProperty<string>(result, "SelectedEngineId"));
        var diagnostics = ReadProperty<IReadOnlyList<EngineSessionDiagnostic>>(result, "Diagnostics");
        Assert.Contains(diagnostics, static diagnostic => diagnostic.Code == "HostIdentityMismatch");
    }

    /// <summary>
    /// 验证单个 heartbeat 解析失败时仍保留其它健康 engine，并把失败原因作为局部诊断返回。
    /// </summary>
    [Fact]
    public void SelectPreservesHealthyEngineWhenAnotherHeartbeatIsInvalid()
    {
        var client = new StubYokiFrameClient("unity-editor", "godot-editor");
        client.SetHeartbeat("unity-editor", NowUtc.AddSeconds(-1));
        client.SetInvalidHeartbeat("godot-editor");

        var result = InvokeSelect(new EngineSelectionService(client), string.Empty, NowUtc);

        Assert.Equal("unity-editor", ReadProperty<string>(result, "SelectedEngineId"));
        var diagnostics = ReadProperty<IReadOnlyList<EngineSessionDiagnostic>>(result, "Diagnostics");
        Assert.Contains(diagnostics, static diagnostic => diagnostic.Code == "HeartbeatReadFailed");
    }

    /// <summary>
    /// 验证部分 registry 读取失败时协调器仍发布有效条目和坏文件诊断。
    /// </summary>
    [Fact]
    public void SessionCoordinatorPreservesValidEntriesWhenRegistryIsPartial()
    {
        var client = new StubYokiFrameClient("unity-editor", "broken");
        client.SetHeartbeat("unity-editor", NowUtc.AddSeconds(-1));
        client.SetPartialRegistryFailure("broken-engine.json");

        var snapshot = new EngineSessionCoordinator(client).Read(string.Empty, NowUtc);

        Assert.Equal("unity-editor", snapshot.Selection.SelectedEngineId);
        Assert.Equal("unity-editor", Assert.Single(snapshot.Engines).EngineId);
        Assert.Contains(snapshot.Diagnostics, static diagnostic => diagnostic.Code == "EngineRegistryPartialRead");
    }

    /// <summary>
    /// 验证显式 engine 选择只读取目标 heartbeat，不扫描无关候选。
    /// </summary>
    [Fact]
    public void SessionCoordinatorLimitsHeartbeatReadsForExplicitEngine()
    {
        var client = new StubYokiFrameClient("unity-editor", "godot-editor");
        client.SetHeartbeat("unity-editor", NowUtc.AddSeconds(-1));
        client.SetHeartbeat("godot-editor", NowUtc.AddSeconds(-1));

        var snapshot = new EngineSessionCoordinator(client).Read("unity-editor", NowUtc);

        Assert.Equal("unity-editor", snapshot.Selection.SelectedEngineId);
        Assert.Equal(1, client.HeartbeatReadCount);
    }

    /// <summary>
    /// 为应用层测试提供可控的内存 Client，避免依赖真实文件和共享内存。
    /// </summary>
    private sealed class StubYokiFrameClient : IYokiFrameClient
    {
        private readonly EngineRegistryEntry[] mEntries;
        private readonly Dictionary<string, HeartbeatInfo> mHeartbeats = new(StringComparer.Ordinal);
        private readonly HashSet<string> mInvalidHeartbeats = new(StringComparer.Ordinal);
        private EngineRegistryReadException? mRegistryReadException;

        /// <summary>
        /// 使用指定 engine 标识创建测试 Client。
        /// </summary>
        /// <param name="engineIds">需要暴露的 engine registry 标识。</param>
        public StubYokiFrameClient(params string[] engineIds)
        {
            var projectRoot = Path.Combine(Path.GetTempPath(), "yokiframe-tooling-tests", Guid.NewGuid().ToString("N"));
            Paths = new YokiFramePaths(projectRoot);
            mEntries = engineIds.Select(static engineId => new EngineRegistryEntry
            {
                ProtocolVersion = 2,
                EngineId = engineId,
                Engine = engineId.StartsWith("godot", StringComparison.Ordinal) ? "Godot" : "Unity"
            }).ToArray();
        }

        /// <summary>
        /// 获取测试路径解析器。
        /// </summary>
        public YokiFramePaths Paths { get; }

        /// <summary>
        /// 获取 snapshot 读取次数，用于验证 Dashboard 通过 Client 边界取数。
        /// </summary>
        public int SnapshotReadCount { get; private set; }

        /// <summary>
        /// 获取 harness 读取次数，用于验证 Dashboard 通过 Client 边界取数。
        /// </summary>
        public int HarnessReadCount { get; private set; }

        /// <summary>
        /// 获取 bridge 状态读取次数，用于验证未选择 engine 时不会访问宿主目录。
        /// </summary>
        public int BridgeReadCount { get; private set; }

        /// <summary>
        /// 获取 telemetry 读取次数，用于验证未选择 engine 时不会访问共享内存。
        /// </summary>
        public int TelemetryReadCount { get; private set; }

        /// <summary>
        /// 获取 registry 读取次数，用于验证显式 engine 不依赖 discovery。
        /// </summary>
        public int EngineReadCount { get; private set; }

        /// <summary>
        /// 获取 heartbeat 读取次数，用于验证显式选择不会扫描无关 engine。
        /// </summary>
        public int HeartbeatReadCount { get; private set; }

        /// <summary>
        /// 设置指定 engine 的 heartbeat 时间。
        /// </summary>
        /// <param name="engineId">engine 标识。</param>
        /// <param name="createdAtUtc">heartbeat 创建时间。</param>
        public void SetHeartbeat(string engineId, DateTimeOffset createdAtUtc)
        {
            mHeartbeats[engineId] = new HeartbeatInfo(
                Paths.GetHeartbeatPath(engineId),
                engineId,
                createdAtUtc,
                "test-session",
                1L,
                "EditMode",
                1L);
        }

        /// <summary>
        /// 设置指定 engine 的 registry 身份，用于验证代次切换门禁。
        /// </summary>
        /// <param name="engineId">目标 engine。</param>
        /// <param name="sessionId">registry 会话标识。</param>
        /// <param name="generation">registry 代次。</param>
        public void SetRegistryIdentity(string engineId, string sessionId, long generation)
        {
            var entry = mEntries.Single(entry => string.Equals(entry.EngineId, engineId, StringComparison.Ordinal));
            entry.SessionId = sessionId;
            entry.Generation = generation;
        }

        /// <summary>
        /// 让指定 heartbeat 读取抛出协议错误，模拟文件正在写入或内容损坏。
        /// </summary>
        /// <param name="engineId">目标 engine。</param>
        public void SetInvalidHeartbeat(string engineId)
        {
            mInvalidHeartbeats.Add(engineId);
        }

        /// <summary>
        /// 让 registry 读取抛出带有效条目的部分读取异常。
        /// </summary>
        /// <param name="invalidPath">坏 registry 文件路径。</param>
        public void SetPartialRegistryFailure(string invalidPath)
        {
            mRegistryReadException = new EngineRegistryReadException(
                mEntries.Where(static entry => string.Equals(entry.EngineId, "unity-editor", StringComparison.Ordinal)).ToArray(),
                new[] { invalidPath },
                "One engine registry is invalid.");
        }

        /// <summary>
        /// 设置指定 registry 的 capability 集合，使测试可以验证 Application 是否尊重宿主明确发布的传输能力。
        /// </summary>
        /// <param name="engineId">目标 engine 标识。</param>
        /// <param name="capabilities">需要公开的 capability 列表。</param>
        public void SetRegistryCapabilities(string engineId, params string[] capabilities)
        {
            var entry = mEntries.Single(entry => string.Equals(entry.EngineId, engineId, StringComparison.Ordinal));
            entry.Capabilities = capabilities.ToList();
        }

        /// <summary>
        /// 返回测试 harness capability。
        /// </summary>
        /// <returns>最小 capability JSON。</returns>
        public JsonNode ReadHarnessCapabilities()
        {
            HarnessReadCount++;
            return JsonNode.Parse("{\"available\":true}")!;
        }

        /// <summary>
        /// 返回测试 engine registry 列表。
        /// </summary>
        /// <returns>registry 条目。</returns>
        public IReadOnlyList<EngineRegistryEntry> ReadEngineEntries()
        {
            EngineReadCount++;
            if (mRegistryReadException != null)
            {
                throw mRegistryReadException;
            }

            return mEntries;
        }

        /// <summary>
        /// 返回指定 engine 的测试 snapshot。
        /// </summary>
        /// <param name="engineId">engine 标识。</param>
        /// <param name="kit">Kit 标识。</param>
        /// <param name="name">snapshot 名称。</param>
        /// <returns>满足当前 FileBridge 信封身份校验的最小 snapshot JSON。</returns>
        public JsonNode ReadSnapshot(string engineId, string kit, string name)
        {
            SnapshotReadCount++;
            JsonObject snapshot = new()
            {
                ["protocolVersion"] = 2,
                ["engineId"] = engineId,
                ["kit"] = kit,
                ["name"] = name,
                ["generation"] = 1,
                ["writtenAtUtc"] = DateTimeOffset.UtcNow.ToString("O"),
                ["payloadJson"] = "{}"
            };
            return snapshot;
        }

        /// <summary>
        /// 返回指定 engine 的测试 heartbeat。
        /// </summary>
        /// <param name="engineId">engine 标识。</param>
        /// <returns>heartbeat；未设置时返回 null。</returns>
        public HeartbeatInfo? ReadHeartbeat(string engineId)
        {
            HeartbeatReadCount++;
            if (mInvalidHeartbeats.Contains(engineId))
            {
                throw new YokiFrameProtocolException(new YokiFrameError(
                    "HeartbeatInvalid",
                    "Heartbeat JSON is invalid.",
                    "Wait for the host to finish publishing heartbeat.",
                    new[] { Paths.GetHeartbeatPath(engineId) }));
            }

            return mHeartbeats.GetValueOrDefault(engineId);
        }

        /// <summary>
        /// 汇总指定 engine 的测试 bridge 状态。
        /// </summary>
        /// <param name="engineId">engine 标识。</param>
        /// <returns>bridge 状态。</returns>
        public FileBridgeStatus ReadBridgeStatus(string engineId)
        {
            BridgeReadCount++;
            return new FileBridgeStatus(
                engineId,
                Paths.GetEngineRoot(engineId),
                Paths.GetCommandsRoot(engineId),
                Paths.GetResultsRoot(engineId))
            {
                Heartbeat = ReadHeartbeat(engineId)
            };
        }

        /// <summary>
        /// 测试 Client 不提供共享内存帧，使 Dashboard 验证 snapshot fallback。
        /// </summary>
        /// <param name="engineId">engine 标识。</param>
        /// <param name="kit">Kit 标识。</param>
        /// <param name="name">状态名称。</param>
        /// <param name="expectedGeneration">期望 generation。</param>
        /// <param name="maxPayloadBytes">最大 payload 字节数。</param>
        /// <returns>不可用的 telemetry 读取结果。</returns>
        public SharedMemoryTelemetryFrameReadResult ReadTelemetry(
            string engineId,
            string kit,
            string name,
            long? expectedGeneration,
            int maxPayloadBytes)
        {
            TelemetryReadCount++;
            return new SharedMemoryTelemetryFrameReadResult(
                SharedMemoryTelemetryFrameStatus.Unavailable,
                null,
                string.Empty,
                "test fallback");
        }

        /// <summary>
        /// 按统一游标规则过滤完整读取结果，同时保留不可用结果供 Dashboard 验证 snapshot fallback。
        /// </summary>
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

        /// <summary>
        /// 测试 Client 不提供 FastChannel；EngineSelection 用例不应触发命令传输。
        /// </summary>
        /// <param name="engineId">目标 engine。</param>
        /// <param name="action">只读 System action。</param>
        /// <param name="source">调用来源。</param>
        /// <param name="timeoutMs">最大等待毫秒数。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>不会正常完成的任务。</returns>
        public Task<CommandResponse> SendFastChannelReadOnlySystemCommandAsync(
            string engineId,
            string action,
            string source,
            int timeoutMs,
            CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("本测试不应发送 FastChannel 命令。");
        }

        /// <summary>
        /// 测试不发送命令；意外调用时立即失败。
        /// </summary>
        /// <param name="engineId">engine 标识。</param>
        /// <param name="kit">Kit 标识。</param>
        /// <param name="action">action 标识。</param>
        /// <param name="payloadJson">payload JSON。</param>
        /// <param name="source">调用来源。</param>
        /// <param name="timeoutMs">超时毫秒数。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>不会正常完成的任务。</returns>
        public Task<CommandSendResult> SendCommandAsync(
            string engineId,
            string kit,
            string action,
            string payloadJson,
            string source,
            int timeoutMs,
            CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("本测试不应发送命令。");
        }
    }

}
