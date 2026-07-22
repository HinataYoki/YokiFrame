using System.Buffers.Binary;
using System.IO.MemoryMappedFiles;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json.Nodes;
using System.IO.Pipes;
using System.Net.Sockets;
using YokiFrame;

namespace YokiFrame.Godot.Runtime.Tests;

/// <summary>
/// 提供 Godot FileBridge 已发布状态、Telemetry 和 FastChannel 连接断言。
/// </summary>
internal sealed partial class GodotFileBridgeHostFixture
{
    /// <summary>
    /// 验证 engine registry、heartbeat 和四个首批 Kit snapshot 使用同一会话事实，且 capability 与当前平台的 telemetry 支持一致。
    /// </summary>
    /// <param name="sessionId">期望会话标识。</param>
    /// <param name="generation">期望 generation。</param>
    /// <param name="sequence">期望 sequence。</param>
    internal void AssertPublishedState(string sessionId, long generation, long sequence)
    {
        AssertRegistry(sessionId, generation);
        AssertHeartbeat(sessionId, generation, sequence);
        foreach (var kit in new[] { "System", "EventKit", "FsmKit", "LogKit" })
        {
            AssertSnapshot(kit, sessionId, generation, sequence);
        }

        Assert.Empty(Directory.EnumerateFiles(EngineRoot, "*.tmp", SearchOption.AllDirectories));
    }

    /// <summary>
    /// 读取指定 Kit 的 Windows named memory map，并验证 committed telemetry 与当前 FileBridge 会话事实一致。
    /// </summary>
    /// <param name="kit">目标 Kit 标识。</param>
    /// <param name="sessionId">期望会话标识。</param>
    /// <param name="generation">期望 generation。</param>
    /// <param name="sequence">期望 sequence。</param>
    [SupportedOSPlatform("windows")]
    internal void AssertCommittedTelemetry(string kit, string sessionId, long generation, long sequence)
    {
        Assert.True(OperatingSystem.IsWindows(), "当前测试仅在 Windows named memory map 可用时读取 telemetry。");
        var projectScopeId = YokiFrameSharedMemoryTelemetryProjectScopeId.Compute(ProjectRoot);
        var segmentName = YokiFrameSharedMemoryTelemetrySegmentName.Create(projectScopeId, ENGINE_ID, kit, "state");
        using var memoryMap = MemoryMappedFile.OpenExisting(segmentName, MemoryMappedFileRights.Read);
        using var accessor = memoryMap.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);
        var headerBytes = new byte[YokiFrameSharedMemoryTelemetryContract.HEADER_SIZE];
        accessor.ReadArray(0, headerBytes, 0, headerBytes.Length);
        var payloadLength = BinaryPrimitives.ReadInt32LittleEndian(headerBytes.AsSpan(
            YokiFrameSharedMemoryTelemetryContract.PAYLOAD_LENGTH_OFFSET,
            sizeof(int)));

        Assert.InRange(payloadLength, 0, YokiFrameSharedMemoryTelemetryContract.DEFAULT_MAX_PAYLOAD_BYTES);
        var payloadBytes = new byte[payloadLength];
        accessor.ReadArray(YokiFrameSharedMemoryTelemetryContract.PAYLOAD_OFFSET, payloadBytes, 0, payloadBytes.Length);

        Assert.Equal(
            YokiFrameSharedMemoryTelemetryContract.MAGIC,
            BinaryPrimitives.ReadUInt32LittleEndian(headerBytes.AsSpan(
                YokiFrameSharedMemoryTelemetryContract.MAGIC_OFFSET,
                sizeof(uint))));
        Assert.Equal(
            YokiFrameSharedMemoryTelemetryContract.PROTOCOL_VERSION,
            BinaryPrimitives.ReadInt32LittleEndian(headerBytes.AsSpan(
                YokiFrameSharedMemoryTelemetryContract.PROTOCOL_VERSION_OFFSET,
                sizeof(int))));
        Assert.Equal(
            generation,
            BinaryPrimitives.ReadInt64LittleEndian(headerBytes.AsSpan(
                YokiFrameSharedMemoryTelemetryContract.GENERATION_OFFSET,
                sizeof(long))));
        Assert.Equal(
            sequence,
            BinaryPrimitives.ReadInt64LittleEndian(headerBytes.AsSpan(
                YokiFrameSharedMemoryTelemetryContract.SEQUENCE_OFFSET,
                sizeof(long))));
        Assert.Equal(
            YokiFrameSharedMemoryTelemetryContract.WRITE_STATE_COMMITTED,
            BinaryPrimitives.ReadInt32LittleEndian(headerBytes.AsSpan(
                YokiFrameSharedMemoryTelemetryContract.WRITE_STATE_OFFSET,
                sizeof(int))));
        Assert.Equal(
            YokiFrameSharedMemoryTelemetryCrc32.Compute(payloadBytes),
            BinaryPrimitives.ReadUInt32LittleEndian(headerBytes.AsSpan(
                YokiFrameSharedMemoryTelemetryContract.PAYLOAD_CRC32_OFFSET,
                sizeof(uint))));

        var payload = JsonNode.Parse(Encoding.UTF8.GetString(payloadBytes))?.AsObject() ?? throw new InvalidDataException("Telemetry payload is not an object.");
        if (kit == "FsmKit")
        {
            AssertFsmKitPayload(payload);
            return;
        }
        if (kit == "EventKit")
        {
            AssertEventKitPayload(payload);
            return;
        }
        if (kit == "LogKit")
        {
            AssertLogKitPayload(payload);
            return;
        }
        Assert.Equal(kit, payload["kit"]?.GetValue<string>());
        Assert.Equal(sessionId, payload["sessionId"]?.GetValue<string>());
        Assert.Equal(generation, payload["generation"]?.GetValue<long>());
        Assert.Equal(sequence, payload["sequence"]?.GetValue<long>());
    }

    /// <summary>
    /// 验证 Host 关闭后不会留下可被下一会话误读的 named memory map。
    /// </summary>
    /// <param name="projectRoot">当前测试项目根。</param>
    /// <param name="kit">目标 Kit 标识。</param>
    [SupportedOSPlatform("windows")]
    internal static void AssertTelemetryUnavailable(string projectRoot, string kit)
    {
        Assert.True(OperatingSystem.IsWindows(), "当前测试仅在 Windows named memory map 可用时验证释放。");
        var projectScopeId = YokiFrameSharedMemoryTelemetryProjectScopeId.Compute(projectRoot);
        var segmentName = YokiFrameSharedMemoryTelemetrySegmentName.Create(projectScopeId, ENGINE_ID, kit, "state");
        Assert.Throws<FileNotFoundException>(() =>
        {
            using var memoryMap = MemoryMappedFile.OpenExisting(segmentName, MemoryMappedFileRights.Read);
        });
    }

    /// <summary>
    /// 复位全局 Runtime Settings Store，并删除 fixture 产生的临时项目和协议文件。
    /// 该副作用必须在每个测试结束时执行，避免内存 Store 泄漏到后续测试。
    /// </summary>
    public void Dispose()
    {
        KitSettings.Reset();
        if (Directory.Exists(ProjectRoot))
        {
            Directory.Delete(ProjectRoot, recursive: true);
        }
    }

    /// <summary>
    /// 验证 engine.json 的宿主身份、会话和 capability 与当前平台支持的 telemetry 传输一致。
    /// </summary>
    /// <param name="sessionId">期望会话标识。</param>
    /// <param name="generation">期望 generation。</param>
    private void AssertRegistry(string sessionId, long generation)
    {
        var registry = ReadObject(RegistryPath);
        Assert.Equal(2, registry["protocolVersion"]?.GetValue<int>());
        Assert.Equal(ENGINE_ID, registry["engineId"]?.GetValue<string>());
        Assert.Equal("Godot", registry["engine"]?.GetValue<string>());
        Assert.Equal("Godot", registry["engineKind"]?.GetValue<string>());
        Assert.Equal("4.7.0", registry["version"]?.GetValue<string>());
        Assert.Equal(sessionId, registry["sessionId"]?.GetValue<string>());
        Assert.Equal(generation, registry["generation"]?.GetValue<long>());
        var capabilities = registry["capabilities"]?.AsArray()
            ?? throw new InvalidDataException("engine.json capabilities are missing.");
        Assert.Contains(capabilities, node => node?.GetValue<string>() == "snapshot.read");
        if (OperatingSystem.IsWindows())
        {
            Assert.Contains(capabilities, node => node?.GetValue<string>() == "telemetry.read");
            return;
        }

        Assert.DoesNotContain(capabilities, node => node?.GetValue<string>() == "telemetry.read");
    }

    /// <summary>
    /// 连接当前用户范围内的 Windows Named Pipe，并让调用侧拥有 Pipe 生命周期。
    /// </summary>
    /// <param name="pipeName">registry 发布的安全 pipe 名称。</param>
    /// <param name="cancellationToken">连接取消令牌。</param>
    /// <returns>已连接 Named Pipe stream。</returns>
    private static async Task<NamedPipeClientStream> ConnectNamedPipeAsync(
        string pipeName,
        CancellationToken cancellationToken)
    {
        NamedPipeClientStream pipe = new(
            ".",
            pipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        try
        {
            await pipe.ConnectAsync(cancellationToken);
            return pipe;
        }
        catch
        {
            pipe.Dispose();
            throw;
        }
    }

    /// <summary>
    /// 连接 macOS 或 Linux Unix Domain Socket，并让返回的 NetworkStream 拥有 Socket 生命周期。
    /// </summary>
    /// <param name="socketPath">registry 发布的绝对 socket 路径。</param>
    /// <param name="cancellationToken">连接取消令牌。</param>
    /// <returns>已连接 Unix socket stream。</returns>
    private static async Task<NetworkStream> ConnectUnixDomainSocketAsync(
        string socketPath,
        CancellationToken cancellationToken)
    {
        Socket socket = new(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        try
        {
            await socket.ConnectAsync(new UnixDomainSocketEndPoint(socketPath), cancellationToken);
            return new NetworkStream(socket, true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    /// <summary>
    /// 验证 heartbeat 与当前会话、generation 和 sequence 完全一致。
    /// </summary>
    /// <param name="sessionId">期望会话标识。</param>
    /// <param name="generation">期望 generation。</param>
    /// <param name="sequence">期望 sequence。</param>
    private void AssertHeartbeat(string sessionId, long generation, long sequence)
    {
        var heartbeat = ReadObject(HeartbeatPath);
        Assert.Equal(sessionId, heartbeat["sessionId"]?.GetValue<string>());
        Assert.Equal(generation, heartbeat["generation"]?.GetValue<long>());
        Assert.Equal(sequence, heartbeat["sequence"]?.GetValue<long>());
        Assert.Equal("Runtime", heartbeat["mode"]?.GetValue<string>());
    }

    /// <summary>
    /// 验证指定 Kit snapshot 的外层会话事实和 payload 回落状态。
    /// </summary>
    /// <param name="kit">Kit 标识。</param>
    /// <param name="sessionId">期望会话标识。</param>
    /// <param name="generation">期望 generation。</param>
    /// <param name="sequence">期望 sequence。</param>
    private void AssertSnapshot(string kit, string sessionId, long generation, long sequence)
    {
        var snapshot = ReadObject(GetSnapshotPath(kit));
        Assert.Equal(kit, snapshot["kit"]?.GetValue<string>());
        Assert.Equal(generation, snapshot["generation"]?.GetValue<long>());
        Assert.Equal(sequence, snapshot["sequence"]?.GetValue<long>());
        var payloadText = snapshot["payloadJson"]?.GetValue<string>() ?? "{}";
        var payload = JsonNode.Parse(payloadText)?.AsObject() ?? throw new InvalidDataException("Snapshot payload is not an object.");
        if (kit == "FsmKit")
        {
            AssertFsmKitPayload(payload);
            return;
        }
        if (kit == "EventKit")
        {
            AssertEventKitPayload(payload);
            return;
        }
        if (kit == "LogKit")
        {
            AssertLogKitPayload(payload);
            return;
        }
        Assert.Equal(kit, payload["kit"]?.GetValue<string>());
        Assert.Equal(sessionId, payload["sessionId"]?.GetValue<string>());
        Assert.Equal("filebridge-fallback", payload["fastChannel"]?.GetValue<string>());
    }
    /// <summary>
    /// 验证 FsmKit 诊断 payload 保留 Workbench 所需的列表、选中详情和两类有界记录对象。
    /// </summary>
    /// <param name="payload">已解析的 FsmKit state 或 telemetry JSON 根对象。</param>
    private static void AssertFsmKitPayload(JsonObject payload)
    {
        Assert.IsType<JsonArray>(payload["fsms"]);
        Assert.True(payload.ContainsKey("selected"));
        Assert.IsType<JsonObject>(payload["history"]);
        Assert.IsType<JsonObject>(payload["stateEvents"]);
    }

    /// <summary>
    /// 验证 EventKit 已由真实 Provider 接管，payload 只包含 Kit 自有诊断 schema。
    /// </summary>
    /// <param name="payload">已解析的 EventKit state JSON 根对象。</param>
    private static void AssertEventKitPayload(JsonObject payload)
    {
        Assert.NotNull(payload["version"]);
        Assert.NotNull(payload["sequence"]);
        Assert.IsType<JsonObject>(payload["counts"]);
        Assert.IsType<JsonArray>(payload["events"]);
        var recentEvents = Assert.IsType<JsonObject>(payload["recentEvents"]);
        Assert.IsType<JsonArray>(recentEvents["events"]);
        Assert.Null(payload["kit"]);
        Assert.Null(payload["sessionId"]);
        Assert.Null(payload["fastChannel"]);
    }

    /// <summary>
    /// 验证 LogKit 已由专用 Provider 接管，payload 使用强类型状态而不是宿主通用占位字段。
    /// </summary>
    /// <param name="payload">已解析的 LogKit state JSON 根对象。</param>
    private static void AssertLogKitPayload(JsonObject payload)
    {
        Assert.NotNull(payload["schemaVersion"]);
        Assert.NotNull(payload["diagnosticVersion"]);
        Assert.NotNull(payload["settingsVersion"]);
        Assert.IsType<JsonObject>(payload["stats"]);
        Assert.IsType<JsonObject>(payload["settings"]);
        Assert.IsType<JsonObject>(payload["capabilities"]);
        Assert.IsType<JsonObject>(payload["files"]);
        var history = Assert.IsType<JsonObject>(payload["history"]);
        Assert.IsType<JsonArray>(history["entries"]);
        Assert.Null(payload["kit"]);
        Assert.Null(payload["sessionId"]);
        Assert.Null(payload["fastChannel"]);
    }
}
