using System.Buffers.Binary;
using System.IO.MemoryMappedFiles;
using System.Runtime.Versioning;
using System.Text.Json.Nodes;
using YokiFrame;

namespace YokiFrame.Godot.Runtime.Tests;

/// <summary>
/// 验证 Godot Runtime Host 对 FsmKit 发布真实诊断 snapshot 并路由只读命令。
/// </summary>
[Collection(GodotFileBridgeHostCollection.NAME)]
public sealed class GodotFsmKitBridgeTests : IDisposable
{
    /// <summary>测试使用的最小状态标识。</summary>
    private enum PayloadStateId
    {
        Idle,
        Running
    }

    /// <summary>
    /// 创建隔离用例并清空进程级 FSM 注册表。
    /// </summary>
    public GodotFsmKitBridgeTests()
    {
        FsmKitCommandHandler.ClearAll();
    }

    /// <summary>
    /// 用例结束时清空进程级 FSM 注册表，避免实例泄漏到其它 xUnit 用例。
    /// </summary>
    public void Dispose()
    {
        FsmKitCommandHandler.ClearAll();
    }

    /// <summary>
    /// 验证 FsmKit/state payload 包含当前进程已注册状态机，而不是通用在线字段集合。
    /// </summary>
    [Fact]
    public void StartPublishesRegisteredFsmInFsmKitSnapshot()
    {
        FSM<PayloadStateId> fsm = new("GodotPayload");
        fsm.Add(PayloadStateId.Idle, new EmptyState());
        using GodotFileBridgeHostFixture fixture = GodotFileBridgeHostFixture.Create();
        using GodotFileBridgeHost host = new(fixture.ProjectRoot, "4.7.0");

        host.Start();

        var snapshot = GodotFileBridgeHostFixture.ReadObject(fixture.GetSnapshotPath("FsmKit"));
        var payload = JsonNode.Parse(snapshot["payloadJson"]?.GetValue<string>() ?? "{}")?.AsObject();
        var fsms = payload?["fsms"]?.AsArray();
        Assert.NotNull(fsms);
        var fsmObject = Assert.IsType<JsonObject>(Assert.Single(fsms!));
        Assert.Equal("GodotPayload", fsmObject["name"]?.GetValue<string>());
    }

    /// <summary>
    /// 验证 FsmKit/list_all 通过 Godot Runtime dispatcher 产生成功 terminal response。
    /// </summary>
    [Fact]
    public void FsmKitCommandProducesTerminalSuccessResponse()
    {
        FSM<PayloadStateId> fsm = new("GodotCommand");
        fsm.Add(PayloadStateId.Idle, new EmptyState());
        using GodotFileBridgeHostFixture fixture = GodotFileBridgeHostFixture.Create();
        using GodotFileBridgeHost host = new(fixture.ProjectRoot, "4.7.0");
        host.Start();
        WriteFsmKitCommand(fixture, "fsm-list-001", "list_all");

        var processed = host.ProcessPendingCommands();

        Assert.Equal(1, processed);
        var response = GodotFileBridgeHostFixture.ReadObject(fixture.GetResponsePath("fsm-list-001"));
        Assert.Equal("Success", response["status"]?.GetValue<string>());
        var resultJson = response["resultJson"]?.GetValue<string>() ?? "{}";
        Assert.Contains("GodotCommand", resultJson, StringComparison.Ordinal);
    }

    /// <summary>
    /// 验证宿主侧 segment 名称与 Client 侧共享同一总长度上限，避免创建后无法被读取。
    /// </summary>
    [Fact]
    public void TelemetrySegmentNameRejectsOverlongCombinedIdentity()
    {
        var longId = new string('a', 128);

        Assert.Throws<ArgumentException>(() => YokiFrameSharedMemoryTelemetrySegmentName.Create(
            longId,
            longId,
            longId,
            longId));
    }

    /// <summary>
    /// 验证单实例变化只推进自身命名帧，并且 Host 重启会为新 generation 重发全部实例首帧。
    /// </summary>
    [Fact]
    public void ChangedFsmOnlyPublishesItsNamedTelemetryAndRestartRepublishesAll()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        FSM<PayloadStateId> changedFsm = CreateTwoStateFsm("ChangedFsm");
        FSM<PayloadStateId> stableFsm = CreateTwoStateFsm("StableFsm");
        using GodotFileBridgeHostFixture fixture = GodotFileBridgeHostFixture.Create();
        using GodotFileBridgeHost host = new(fixture.ProjectRoot, "4.7.0");
        host.Start();
        var instanceIds = ReadInstanceIdsByName(fixture.GetSnapshotPath("FsmKit"));
        var stableBefore = ReadNamedTelemetryIdentity(
            fixture.ProjectRoot,
            instanceIds["StableFsm"]);

        changedFsm.Start(PayloadStateId.Idle);
        host.RefreshChangedTelemetry();

        var changedAfter = ReadNamedTelemetryIdentity(
            fixture.ProjectRoot,
            instanceIds["ChangedFsm"]);
        var stableAfter = ReadNamedTelemetryIdentity(
            fixture.ProjectRoot,
            instanceIds["StableFsm"]);
        Assert.True(changedAfter.Sequence > stableBefore.Sequence);
        Assert.Equal(stableBefore, stableAfter);

        host.Stop();
        host.Start();
        var stableRestarted = ReadNamedTelemetryIdentity(
            fixture.ProjectRoot,
            instanceIds["StableFsm"]);
        Assert.Equal(host.Generation, stableRestarted.Generation);
        Assert.NotEqual(stableAfter.Generation, stableRestarted.Generation);
    }

    /// <summary>创建包含两个可切换状态的诊断 FSM。</summary>
    /// <param name="name">诊断名称。</param>
    /// <returns>已注册两个状态的 FSM。</returns>
    private static FSM<PayloadStateId> CreateTwoStateFsm(string name)
    {
        FSM<PayloadStateId> fsm = new(name);
        fsm.Add(PayloadStateId.Idle, new EmptyState());
        fsm.Add(PayloadStateId.Running, new EmptyState());
        return fsm;
    }

    /// <summary>从 FsmKit state snapshot 建立诊断名称到稳定实例标识的映射。</summary>
    /// <param name="snapshotPath">FsmKit state snapshot 路径。</param>
    /// <returns>按诊断名称索引的实例标识。</returns>
    private static Dictionary<string, string> ReadInstanceIdsByName(string snapshotPath)
    {
        var snapshot = GodotFileBridgeHostFixture.ReadObject(snapshotPath);
        var payload = JsonNode.Parse(snapshot["payloadJson"]?.GetValue<string>() ?? "{}")?.AsObject();
        var fsms = payload?["fsms"]?.AsArray()
            ?? throw new InvalidDataException("FsmKit snapshot does not contain fsms.");
        Dictionary<string, string> instanceIds = new(StringComparer.Ordinal);
        for (var index = 0; index < fsms.Count; index++)
        {
            var fsm = fsms[index]?.AsObject()
                ?? throw new InvalidDataException("FsmKit instance is not an object.");
            instanceIds.Add(
                fsm["name"]?.GetValue<string>() ?? string.Empty,
                fsm["instanceId"]?.GetValue<string>() ?? string.Empty);
        }

        return instanceIds;
    }

    /// <summary>读取指定 FsmKit 实例命名帧的 generation 与 sequence。</summary>
    /// <param name="projectRoot">当前隔离 Godot 项目根。</param>
    /// <param name="instanceId">FsmKit 稳定实例标识。</param>
    /// <returns>用于判断帧是否被重写的身份值。</returns>
    [SupportedOSPlatform("windows")]
    private static (long Generation, long Sequence) ReadNamedTelemetryIdentity(
        string projectRoot,
        string instanceId)
    {
        var projectScopeId = YokiFrameSharedMemoryTelemetryProjectScopeId.Compute(projectRoot);
        var segmentName = YokiFrameSharedMemoryTelemetrySegmentName.Create(
            projectScopeId,
            GodotFileBridgeHostFixture.ENGINE_ID,
            "FsmKit",
            instanceId);
        using var memoryMap = MemoryMappedFile.OpenExisting(segmentName, MemoryMappedFileRights.Read);
        using var accessor = memoryMap.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);
        var headerBytes = new byte[YokiFrameSharedMemoryTelemetryContract.HEADER_SIZE];
        accessor.ReadArray(0, headerBytes, 0, headerBytes.Length);
        return (
            BinaryPrimitives.ReadInt64LittleEndian(headerBytes.AsSpan(
                YokiFrameSharedMemoryTelemetryContract.GENERATION_OFFSET,
                sizeof(long))),
            BinaryPrimitives.ReadInt64LittleEndian(headerBytes.AsSpan(
                YokiFrameSharedMemoryTelemetryContract.SEQUENCE_OFFSET,
                sizeof(long))));
    }

    /// <summary>
    /// 写入符合 FileBridge contract 的 FsmKit 命令信封。
    /// </summary>
    /// <param name="fixture">当前隔离协议目录。</param>
    /// <param name="requestId">安全请求标识。</param>
    /// <param name="action">FsmKit action。</param>
    private static void WriteFsmKitCommand(
        GodotFileBridgeHostFixture fixture,
        string requestId,
        string action)
    {
        Directory.CreateDirectory(fixture.CommandsRoot);
        JsonObject envelope = new()
        {
            ["protocolVersion"] = 2,
            ["engineId"] = GodotFileBridgeHostFixture.ENGINE_ID,
            ["source"] = "cli",
            ["createdAtUtc"] = DateTimeOffset.UtcNow.ToString("O"),
            ["requestId"] = requestId,
            ["kit"] = "FsmKit",
            ["action"] = action,
            ["payloadJson"] = "{}",
            ["timeoutMs"] = 10000
        };
        File.WriteAllText(
            Path.Combine(fixture.CommandsRoot, requestId + ".json"),
            envelope.ToJsonString());
    }

    /// <summary>
    /// 提供无副作用的最小状态，供 Godot Host 测试注册真实 FSM。
    /// </summary>
    private sealed class EmptyState : IState
    {
        /// <summary>始终允许进入。</summary>
        /// <returns>始终返回 true。</returns>
        public bool Condition() => true;

        /// <summary>测试状态无需启动逻辑。</summary>
        public void Start() { }

        /// <summary>测试状态无需暂停逻辑。</summary>
        public void Suspend() { }

        /// <summary>测试状态无需普通更新逻辑。</summary>
        public void Update() { }

        /// <summary>测试状态无需固定更新逻辑。</summary>
        public void FixedUpdate() { }

        /// <summary>测试状态无需自定义更新逻辑。</summary>
        public void CustomUpdate() { }

        /// <summary>测试状态无需结束逻辑。</summary>
        public void End() { }

        /// <summary>测试状态无需释放逻辑。</summary>
        public void Dispose() { }

        /// <summary>测试状态忽略消息。</summary>
        /// <typeparam name="TMsg">消息类型。</typeparam>
        /// <param name="message">消息值。</param>
        public void SendMessage<TMsg>(TMsg message) { }
    }
}
