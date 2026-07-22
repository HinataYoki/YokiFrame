using System.IO.MemoryMappedFiles;
using System.Text;
using YokiFrame;
using YokiFrame.Protocol.Telemetry.SharedMemory;
using YokiFrame.Tooling.Application.Models;
using YokiFrame.Tooling.Application.Services;

namespace YokiFrame.Tooling.Application.Tests;

/// <summary>
/// 覆盖 WorkbenchDashboardService 对 FileBridge 状态的聚合读取。
/// </summary>
public sealed class WorkbenchDashboardServiceTests
{
    /// <summary>
    /// 验证所有已完成 Runtime Kit 页面都进入统一高速 state 目录；文件型工具不应伪装成 Runtime telemetry。
    /// </summary>
    [Fact]
    public void RuntimeTelemetryCatalogContainsAllProviderBackedKits()
    {
        IReadOnlyList<string> kits = WorkbenchRuntimeKitCatalog.TelemetryStateKits;

        Assert.Equal(
            new[] { "Architecture", "FsmKit", "EventKit", "LogKit", "PoolKit", "ResKit", "ActionKit", "AudioKit", "SpatialKit", "UIKit" },
            kits);
        Assert.DoesNotContain("TableKit", kits);
        Assert.DoesNotContain("LocalizationKit", kits);
        Assert.DoesNotContain("SaveKit", kits);
        Assert.DoesNotContain("System", kits);
    }

    /// <summary>验证 SaveKit 保留 FileBridge state 读取，但不进入高频 Telemetry 目录。</summary>
    [Fact]
    public void RuntimeSnapshotCatalogKeepsSaveKitOutsideTelemetry()
    {
        IReadOnlyList<string> kits = WorkbenchRuntimeKitCatalog.SnapshotStateKits;

        Assert.Contains("SaveKit", kits);
        Assert.DoesNotContain("SaveKit", WorkbenchRuntimeKitCatalog.TelemetryStateKits);
    }

    /// <summary>
    /// 验证 snapshot 投影辅助按 Exists 决定宿主身份，并优先采用显式 stale 原因。
    /// </summary>
    [Fact]
    public void KitSnapshotProjectionResolvesIdentityAndStaleReason()
    {
        WorkbenchBridgeHealth bridgeHealth = new(
            WorkbenchBridgeConnectionState.Online,
            "online",
            "none",
            Array.Empty<string>(),
            1L,
            30L,
            "session-a",
            9L,
            "EditMode",
            4L);
        WorkbenchSnapshotState existing = new(
            "PoolKit",
            "state",
            "evidence/pool",
            "telemetry",
            true,
            "{}",
            "error-fallback",
            rawPayloadJson: "{\"ok\":true}",
            updatedAtUtc: DateTimeOffset.Parse("2026-07-21T00:00:00Z"),
            staleReason: "explicit-stale");
        WorkbenchSnapshotState missing = new(
            "ResKit",
            "state",
            "evidence/res",
            "snapshot",
            false,
            string.Empty,
            "read-failed");

        Assert.True(WorkbenchKitSnapshotProjection.TryGetSnapshot(
            new[] { existing, missing },
            "PoolKit",
            out WorkbenchSnapshotState? poolSnapshot));
        Assert.NotNull(poolSnapshot);
        Assert.Equal("explicit-stale", WorkbenchKitSnapshotProjection.ResolveStaleReason(poolSnapshot!));
        (string sessionId, long generation, string mode) = WorkbenchKitSnapshotProjection.ResolveHostIdentity(
            poolSnapshot!,
            bridgeHealth);
        Assert.Equal("session-a", sessionId);
        Assert.Equal(9L, generation);
        Assert.Equal("EditMode", mode);
        Assert.Equal(
            DateTimeOffset.Parse("2026-07-21T00:00:00Z"),
            WorkbenchKitSnapshotProjection.ResolveUpdatedAtUtc(poolSnapshot!));

        Assert.True(WorkbenchKitSnapshotProjection.TryGetSnapshot(
            new[] { existing, missing },
            "ResKit",
            out WorkbenchSnapshotState? resSnapshot));
        Assert.NotNull(resSnapshot);
        Assert.Equal("read-failed", WorkbenchKitSnapshotProjection.ResolveStaleReason(resSnapshot!));
        (string emptySession, long emptyGeneration, string emptyMode) = WorkbenchKitSnapshotProjection.ResolveHostIdentity(
            resSnapshot!,
            bridgeHealth);
        Assert.Equal(string.Empty, emptySession);
        Assert.Equal(0L, emptyGeneration);
        Assert.Equal(string.Empty, emptyMode);
        Assert.False(WorkbenchKitSnapshotProjection.TryGetSnapshot(
            new[] { existing },
            "MissingKit",
            out _));
    }

    /// <summary>
    /// 验证缺少 engine 现场时 dashboard 可返回离线状态而不是抛出异常。
    /// </summary>
    [Fact]
    public void LoadDashboardReturnsOfflineStateWhenBridgeIsMissing()
    {
        var projectRoot = CreateProjectRoot();
        Directory.CreateDirectory(Path.Combine(projectRoot, ".yokiframe"));

        var state = new WorkbenchDashboardService(projectRoot).LoadDashboard("unity-editor");

        Assert.Empty(state.Engines);
        Assert.NotNull(state.BridgeStatus);
        Assert.Null(state.BridgeStatus.Heartbeat);
        Assert.Equal(WorkbenchBridgeConnectionState.EngineUnregistered, state.BridgeHealth.State);
        Assert.True(state.BridgeHealth.RequiresReconnect);
        Assert.All(state.Snapshots, snapshot => Assert.False(snapshot.Exists));
    }

    /// <summary>
    /// 验证最小 FileBridge 现场能被聚合为在线 dashboard 状态。
    /// </summary>
    [Fact]
    public void LoadDashboardReadsMinimalOnlineBridgeState()
    {
        var projectRoot = CreateProjectRoot();
        WriteMinimalBridge(projectRoot);

        var state = new WorkbenchDashboardService(projectRoot).LoadDashboard("unity-editor");

        Assert.Single(state.Engines);
        Assert.NotNull(state.DoctorReport);
        Assert.Equal("Healthy", state.DoctorReport.Level);
        Assert.Equal(0, state.DoctorReport.IssueCount);
        Assert.NotNull(state.BridgeStatus);
        Assert.Equal(WorkbenchBridgeConnectionState.Online, state.BridgeHealth.State);
        Assert.False(state.BridgeHealth.RequiresReconnect);
        Assert.Equal("EditMode", state.BridgeHealth.Mode);
        Assert.Equal("test-session", state.BridgeHealth.SessionId);
        Assert.Equal(7, state.BridgeHealth.Generation);
        Assert.Equal(3, state.BridgeHealth.Sequence);
        Assert.Contains(state.Snapshots, snapshot => snapshot.Kit == "System" && snapshot.Exists && snapshot.Source == "snapshot");
        Assert.Contains(state.Snapshots, snapshot => snapshot.Kit == "FsmKit" && snapshot.Exists);
        Assert.Contains(state.Snapshots, snapshot => snapshot.Kit == "LogKit" && snapshot.Exists);
    }

    /// <summary>
    /// 验证在线 bridge 存在匹配 generation 的 telemetry 时 Kit 页优先使用 telemetry。
    /// </summary>
    [Fact]
    public void LoadDashboardPrefersTelemetryWhenAvailable()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var projectRoot = CreateProjectRoot();
        var engineId = "test-" + Guid.NewGuid().ToString("N");
        WriteMinimalBridge(projectRoot, engineId: engineId);
        using var systemSegment = CreateTelemetrySegment(projectRoot, engineId, "System", "{\"status\":\"system-telemetry\"}");
        using var fsmSegment = CreateTelemetrySegment(projectRoot, engineId, "FsmKit", "{\"status\":\"fsm-telemetry\"}");
        using var logSegment = CreateTelemetrySegment(projectRoot, engineId, "LogKit", "{\"status\":\"log-telemetry\"}");

        var state = new WorkbenchDashboardService(projectRoot).LoadDashboard(engineId);
        var systemState = state.Snapshots.Single(snapshot => snapshot.Kit == "System");
        var fsmState = state.Snapshots.Single(snapshot => snapshot.Kit == "FsmKit");
        var logState = state.Snapshots.Single(snapshot => snapshot.Kit == "LogKit");

        Assert.Equal("telemetry", systemState.Source);
        Assert.Contains("system-telemetry", systemState.PayloadPreview);
        Assert.Equal("telemetry", fsmState.Source);
        Assert.Contains("fsm-telemetry", fsmState.PayloadPreview);
        Assert.Equal("telemetry", logState.Source);
        Assert.Contains("log-telemetry", logState.PayloadPreview);
    }

    /// <summary>
    /// 验证 registry 已进入新 generation 而 heartbeat 仍属于旧 generation 时，dashboard 不会接受同样属于旧 generation 的 telemetry。
    /// </summary>
    [Fact]
    public void LoadDashboardFallsBackToSnapshotWhenRegistryAndHeartbeatGenerationsDiffer()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var projectRoot = CreateProjectRoot();
        var engineId = "test-" + Guid.NewGuid().ToString("N");
        WriteMinimalBridge(
            projectRoot,
            engineId: engineId,
            registryGeneration: 8L,
            heartbeatGeneration: 7L);
        using var systemSegment = CreateTelemetrySegment(projectRoot, engineId, "System", "{\"status\":\"old-telemetry\"}");

        var state = new WorkbenchDashboardService(projectRoot).LoadDashboard(engineId);
        var systemState = state.Snapshots.Single(snapshot => snapshot.Kit == "System");

        Assert.Equal(WorkbenchBridgeConnectionState.Stale, state.BridgeHealth.State);
        Assert.True(state.BridgeHealth.RequiresReconnect);
        Assert.Equal("snapshot", systemState.Source);
        Assert.DoesNotContain("old-telemetry", systemState.PayloadPreview);
        Assert.NotNull(state.DoctorReport);
        Assert.Contains(state.DoctorReport.Issues, static issue => issue.Code == "HostIdentityMismatch");
    }

    /// <summary>
    /// 验证 registry 与 heartbeat 的 session 不一致时，即使 generation 相同也不会接受可能属于旧宿主会话的 telemetry。
    /// </summary>
    [Fact]
    public void LoadDashboardFallsBackToSnapshotWhenRegistryAndHeartbeatSessionsDiffer()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var projectRoot = CreateProjectRoot();
        var engineId = "test-" + Guid.NewGuid().ToString("N");
        WriteMinimalBridge(
            projectRoot,
            engineId: engineId,
            registrySessionId: "current-session",
            heartbeatSessionId: "old-session");
        using var systemSegment = CreateTelemetrySegment(projectRoot, engineId, "System", "{\"status\":\"old-session-telemetry\"}");

        var state = new WorkbenchDashboardService(projectRoot).LoadDashboard(engineId);
        var systemState = state.Snapshots.Single(snapshot => snapshot.Kit == "System");

        Assert.Equal(WorkbenchBridgeConnectionState.Stale, state.BridgeHealth.State);
        Assert.Equal("current-session", state.BridgeHealth.SessionId);
        Assert.Equal("snapshot", systemState.Source);
        Assert.DoesNotContain("old-session-telemetry", systemState.PayloadPreview);
    }

    /// <summary>
    /// 验证指定未知 engine 时会保留选择，并返回可显示的缺失 snapshot 状态。
    /// </summary>
    [Fact]
    public void LoadDashboardKeepsSelectedEngineWhenSnapshotsAreMissing()
    {
        var projectRoot = CreateProjectRoot();
        WriteMinimalBridge(projectRoot);

        var state = new WorkbenchDashboardService(projectRoot).LoadDashboard("godot-editor");

        Assert.Equal("godot-editor", state.SelectedEngineId);
        Assert.Equal(WorkbenchBridgeConnectionState.EngineUnregistered, state.BridgeHealth.State);
        Assert.All(state.Snapshots, snapshot => Assert.False(snapshot.Exists));
    }

    /// <summary>
    /// 验证 registry 存在但 heartbeat 缺失时会给出明确 reconnect 状态。
    /// </summary>
    [Fact]
    public void LoadDashboardReportsMissingHeartbeat()
    {
        var projectRoot = CreateProjectRoot();
        WriteMinimalBridge(projectRoot, writeHeartbeat: false);

        var state = new WorkbenchDashboardService(projectRoot).LoadDashboard("unity-editor");

        Assert.Equal(WorkbenchBridgeConnectionState.HeartbeatMissing, state.BridgeHealth.State);
        Assert.True(state.BridgeHealth.RequiresReconnect);
        Assert.Null(state.BridgeHealth.HeartbeatAgeSeconds);
        Assert.Contains("heartbeat", state.BridgeHealth.EvidencePaths[0], StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 验证旧 heartbeat 会被 Tooling.Application 聚合为 stale 状态。
    /// </summary>
    [Fact]
    public void LoadDashboardReportsStaleHeartbeat()
    {
        var projectRoot = CreateProjectRoot();
        WriteMinimalBridge(projectRoot, heartbeatCreatedAtUtc: DateTimeOffset.UtcNow.AddMinutes(-5));

        var state = new WorkbenchDashboardService(projectRoot).LoadDashboard("unity-editor");

        Assert.Equal(WorkbenchBridgeConnectionState.Stale, state.BridgeHealth.State);
        Assert.True(state.BridgeHealth.RequiresReconnect);
        Assert.True(state.BridgeHealth.HeartbeatAgeSeconds >= 15);
    }

    /// <summary>
    /// 创建唯一测试项目根目录。
    /// </summary>
    /// <returns>测试项目根目录。</returns>
    private static string CreateProjectRoot()
    {
        return Path.Combine(Path.GetTempPath(), "yokiframe-workbench-dashboard-tests", Guid.NewGuid().ToString("N"));
    }

    /// <summary>
    /// 写入 Tooling.Application 测试所需的最小 FileBridge 文件。
    /// </summary>
    /// <param name="projectRoot">测试项目根目录。</param>
    /// <param name="engineId">写入的 engine 标识。</param>
    /// <param name="writeHeartbeat">是否写入 heartbeat 文件。</param>
    /// <param name="heartbeatCreatedAtUtc">可选 heartbeat 写入时间。</param>
    /// <param name="registryGeneration">engine registry 的 generation。</param>
    /// <param name="heartbeatGeneration">可选 heartbeat generation；为空时与 registry 保持一致。</param>
    /// <param name="registrySessionId">engine registry 的 session 标识。</param>
    /// <param name="heartbeatSessionId">可选 heartbeat session 标识；为空时与 registry 保持一致。</param>
    private static void WriteMinimalBridge(
        string projectRoot,
        string engineId = "unity-editor",
        bool writeHeartbeat = true,
        DateTimeOffset? heartbeatCreatedAtUtc = null,
        long registryGeneration = 7L,
        long? heartbeatGeneration = null,
        string registrySessionId = "test-session",
        string? heartbeatSessionId = null)
    {
        var engineRoot = Path.Combine(projectRoot, ".yokiframe", "engines", engineId);
        Directory.CreateDirectory(Path.Combine(projectRoot, ".yokiframe", "harness"));
        Directory.CreateDirectory(Path.Combine(engineRoot, "status"));
        Directory.CreateDirectory(Path.Combine(engineRoot, "snapshots", "System"));
        Directory.CreateDirectory(Path.Combine(engineRoot, "snapshots", "FsmKit"));
        Directory.CreateDirectory(Path.Combine(engineRoot, "snapshots", "EventKit"));
        Directory.CreateDirectory(Path.Combine(engineRoot, "snapshots", "LogKit"));
        File.WriteAllText(Path.Combine(projectRoot, ".yokiframe", "harness", "capabilities.json"), "{\"package\":{\"name\":\"YokiFrame\"}}");
        File.WriteAllText(Path.Combine(engineRoot, "engine.json"), "{\"protocolVersion\":2,\"engineId\":\"" + engineId + "\",\"engine\":\"Unity\",\"version\":\"6000.7.0a1\",\"projectPath\":\"" + Escape(projectRoot) + "\",\"adapterVersion\":\"test\",\"sessionId\":\"" + registrySessionId + "\",\"generation\":" + registryGeneration + ",\"mode\":\"EditMode\",\"capabilities\":[\"snapshot.read\",\"telemetry.read\"]}");
        if (writeHeartbeat)
        {
            var createdAtUtc = heartbeatCreatedAtUtc ?? DateTimeOffset.UtcNow;
            var currentHeartbeatGeneration = heartbeatGeneration ?? registryGeneration;
            var currentHeartbeatSessionId = heartbeatSessionId ?? registrySessionId;
            File.WriteAllText(Path.Combine(engineRoot, "status", "heartbeat.json"), "{\"protocolVersion\":2,\"engineId\":\"" + engineId + "\",\"sessionId\":\"" + currentHeartbeatSessionId + "\",\"generation\":" + currentHeartbeatGeneration + ",\"mode\":\"EditMode\",\"sequence\":3,\"createdAtUtc\":\"" + createdAtUtc.ToString("O") + "\"}");
        }

        WriteSnapshot(engineRoot, engineId, "System", registryGeneration);
        WriteSnapshot(engineRoot, engineId, "FsmKit", registryGeneration);
        WriteSnapshot(engineRoot, engineId, "EventKit", registryGeneration);
        WriteSnapshot(engineRoot, engineId, "LogKit", registryGeneration);
    }

    /// <summary>
    /// 创建测试用 telemetry segment。
    /// </summary>
    /// <param name="projectRoot">当前测试项目根。</param>
    /// <param name="engineId">engine 标识。</param>
    /// <param name="kit">Kit 名称。</param>
    /// <param name="payloadJson">payload JSON。</param>
    /// <returns>持有 memory map 和 accessor 的释放对象。</returns>
    private static IDisposable CreateTelemetrySegment(string projectRoot, string engineId, string kit, string payloadJson)
    {
        var segmentName = SharedMemoryTelemetrySegmentName.Create(projectRoot, engineId, kit, "state");
        var frame = CreateTelemetryFrame(
            payloadJson,
            YokiFrameSharedMemoryTelemetryEngineIdHash.Compute(engineId),
            7L,
            4L);
        var memoryMap = MemoryMappedFile.CreateNew(segmentName, frame.Length, MemoryMappedFileAccess.ReadWrite);
        var accessor = memoryMap.CreateViewAccessor(0, frame.Length, MemoryMappedFileAccess.Write);
        accessor.WriteArray(0, frame, 0, frame.Length);
        return new TelemetrySegmentHandle(memoryMap, accessor);
    }

    /// <summary>
    /// 写入指定 Kit 的最小 state snapshot。
    /// </summary>
    /// <param name="engineRoot">engine 根目录。</param>
    /// <param name="kit">Kit 名称。</param>
    /// <param name="generation">与当前 registry 对齐的 generation。</param>
    private static void WriteSnapshot(string engineRoot, string engineId, string kit, long generation)
    {
        var path = Path.Combine(engineRoot, "snapshots", kit, "state.json");
        File.WriteAllText(path, "{\"protocolVersion\":2,\"engineId\":\"" + engineId + "\",\"kit\":\"" + kit + "\",\"name\":\"state\",\"generation\":" + generation + ",\"writtenAtUtc\":\"" + DateTimeOffset.UtcNow.ToString("O") + "\",\"payloadJson\":\"{\\\"status\\\":\\\"online\\\"}\"}");
    }

    /// <summary>
    /// 创建测试用 telemetry 帧。
    /// </summary>
    /// <param name="payloadJson">payload JSON。</param>
    /// <param name="engineIdHash">frame 所属 engine 的稳定哈希。</param>
    /// <param name="generation">engine generation。</param>
    /// <param name="sequence">帧序号。</param>
    /// <returns>帧字节。</returns>
    private static byte[] CreateTelemetryFrame(
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

    /// <summary>
    /// 持有测试 telemetry segment 资源并统一释放。
    /// </summary>
    /// <param name="MemoryMap">memory map。</param>
    /// <param name="Accessor">view accessor。</param>
    private sealed record TelemetrySegmentHandle(MemoryMappedFile MemoryMap, MemoryMappedViewAccessor Accessor) : IDisposable
    {
        /// <summary>
        /// 释放测试 telemetry segment 资源。
        /// </summary>
        public void Dispose()
        {
            Accessor.Dispose();
            MemoryMap.Dispose();
        }
    }

    /// <summary>
    /// 转义 Windows 路径中的反斜杠，避免测试 JSON 无效。
    /// </summary>
    /// <param name="text">待转义文本。</param>
    /// <returns>JSON 字符串内可用的文本。</returns>
    private static string Escape(string text)
    {
        return text.Replace("\\", "\\\\", StringComparison.Ordinal);
    }
}
