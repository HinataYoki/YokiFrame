using System.Text.Json.Nodes;
using YokiFrame.Tooling.Application.Models;
using YokiFrame.Tooling.Application.Models.AudioKit;
using YokiFrame.Tooling.Application.Services;

namespace YokiFrame.Tooling.Application.Tests;

/// <summary>覆盖 AudioKit Snapshot 的强类型投影和无命令周期读取。</summary>
public sealed class WorkbenchAudioKitStateTests
{
    private const string AUDIO_PAYLOAD = """
        {"schemaVersion":1,"version":12,"backend":{"name":"UnityAudioSource","capabilities":63,"capabilityNames":"All","resourceLoader":"ResKit"},"master":{"volume":0.8,"effectiveVolume":0.8,"muted":false,"activeVoiceCount":1},"buses":[{"name":"Master","volume":0.8,"effectiveVolume":0.8,"muted":false,"isMaster":true,"isBuiltIn":true,"isRegistered":true,"activeVoiceCount":1},{"name":"DialogueNpc","volume":1,"effectiveVolume":1,"muted":false,"isMaster":false,"isBuiltIn":false,"isRegistered":true,"activeVoiceCount":0},{"name":"Music","volume":0.6,"effectiveVolume":0.48,"muted":false,"isMaster":false,"isBuiltIn":true,"isRegistered":true,"activeVoiceCount":1}],"busCount":3,"busTotal":9,"busesTruncated":true,"voices":[{"backendGeneration":4,"voiceId":7,"path":"Audio/Music/Menu","bus":"Music","backendName":"UnityAudioSource","loop":true,"playing":true,"paused":false,"volume":0.6,"pitch":1,"duration":120,"elapsed":4.5,"is3D":false,"position":{"x":0,"y":0,"z":0},"followTarget":"","minDistance":1,"maxDistance":500,"rolloffMode":"Logarithmic"}],"voiceCount":1,"voiceTotal":1,"voicesTruncated":false,"history":[{"sequence":3,"eventType":"play_started","backendGeneration":4,"voiceId":7,"path":"Audio/Music/Menu","bus":"Music","volume":0.6,"timestampUtc":"2026-07-17T08:00:00Z"}],"historyCount":1,"historyTotal":3,"historyTruncated":true}
        """;

    /// <summary>验证周期读取投影后端、voice 与裁剪事实且不创建命令文件。</summary>
    [Fact]
    public void LoadDashboardProjectsAudioKitWithoutSendingCommand()
    {
        string projectRoot = CreateProjectRoot();
        WriteOnlineBridge(projectRoot, AUDIO_PAYLOAD);

        WorkbenchDashboardState dashboard = new WorkbenchDashboardService(projectRoot)
            .LoadDashboard("unity-editor");
        WorkbenchAudioKitState state = Assert.IsType<WorkbenchAudioKitState>(dashboard.AudioKitState);

        Assert.Equal("UnityAudioSource", state.Backend.Name);
        Assert.Equal("Music", Assert.Single(state.Voices).Bus);
        WorkbenchAudioBus custom = Assert.Single(state.Buses, static bus => bus.Name == "DialogueNpc");
        Assert.True(custom.IsRegistered);
        Assert.False(custom.IsBuiltIn);
        Assert.Equal(9, state.BusTotal);
        Assert.True(state.BusesTruncated);
        Assert.Equal(3L, state.HistoryTotal);
        Assert.True(state.HistoryTruncated);
        string commandsRoot = Path.Combine(projectRoot, ".yokiframe", "engines", "unity-editor", "commands");
        Assert.False(Directory.Exists(commandsRoot)
            && Directory.EnumerateFiles(commandsRoot, "*.json").Any());
    }

    /// <summary>验证错误布尔类型转为 stale 空状态，不把污染 payload 传给页面。</summary>
    [Fact]
    public void InvalidVoiceBooleanProducesStaleEmptyState()
    {
        string projectRoot = CreateProjectRoot();
        string invalid = AUDIO_PAYLOAD.Replace("\"playing\":true", "\"playing\":1", StringComparison.Ordinal);
        WriteOnlineBridge(projectRoot, invalid);

        WorkbenchAudioKitState state = Assert.IsType<WorkbenchAudioKitState>(
            new WorkbenchDashboardService(projectRoot).LoadDashboard("unity-editor").AudioKitState);

        Assert.Empty(state.Voices);
        Assert.Contains("must be a boolean", state.StaleReason, StringComparison.Ordinal);
    }

    /// <summary>创建唯一测试项目根。</summary>
    private static string CreateProjectRoot() => Path.Combine(
        Path.GetTempPath(), "yokiframe-audiokit-tests", Guid.NewGuid().ToString("N"));

    /// <summary>写入在线 bridge 与 dashboard 所需 Snapshot。</summary>
    private static void WriteOnlineBridge(string projectRoot, string audioPayload)
    {
        string engineRoot = Path.Combine(projectRoot, ".yokiframe", "engines", "unity-editor");
        Directory.CreateDirectory(Path.Combine(projectRoot, ".yokiframe", "harness"));
        Directory.CreateDirectory(Path.Combine(engineRoot, "status"));
        File.WriteAllText(Path.Combine(projectRoot, ".yokiframe", "harness", "capabilities.json"), "{\"package\":{\"name\":\"YokiFrame\"}}");
        File.WriteAllText(Path.Combine(engineRoot, "engine.json"), CreateEngineRegistryJson(projectRoot));
        File.WriteAllText(Path.Combine(engineRoot, "status", "heartbeat.json"),
            "{\"protocolVersion\":2,\"engineId\":\"unity-editor\",\"sessionId\":\"audio-session\",\"generation\":9,\"mode\":\"PlayMode\",\"sequence\":3,\"createdAtUtc\":\""
            + DateTimeOffset.UtcNow.ToString("O") + "\"}");
        WriteSnapshot(engineRoot, "System", "{\"status\":\"online\"}");
        WriteSnapshot(engineRoot, "Architecture", "{\"architectures\":[],\"count\":0}");
        WriteSnapshot(engineRoot, "FsmKit", "{\"fsms\":[],\"count\":0,\"selected\":{},\"history\":{\"history\":[],\"count\":0},\"stateEvents\":{\"events\":[],\"count\":0}}");
        WriteSnapshot(engineRoot, "EventKit", "{\"events\":[]}");
        WriteSnapshot(engineRoot, "LogKit", "{\"status\":\"online\"}");
        WriteSnapshot(engineRoot, "PoolKit", "{\"pools\":[]}");
        WriteSnapshot(engineRoot, "AudioKit", audioPayload);
    }

    /// <summary>创建只声明 snapshot.read 的测试 registry。</summary>
    private static string CreateEngineRegistryJson(string projectRoot)
    {
        JsonObject registry = new()
        {
            ["protocolVersion"] = 2, ["engineId"] = "unity-editor", ["engine"] = "Unity",
            ["version"] = "6000.7.0a2", ["projectPath"] = projectRoot, ["adapterVersion"] = "test",
            ["sessionId"] = "audio-session", ["generation"] = 9, ["mode"] = "PlayMode",
            ["capabilities"] = new JsonArray("snapshot.read")
        };
        return registry.ToJsonString();
    }

    /// <summary>写入带稳定身份的 Snapshot 信封。</summary>
    private static void WriteSnapshot(string engineRoot, string kit, string payloadJson)
    {
        string directory = Path.Combine(engineRoot, "snapshots", kit);
        Directory.CreateDirectory(directory);
        JsonObject snapshot = new()
        {
            ["protocolVersion"] = 2, ["engineId"] = "unity-editor", ["kit"] = kit,
            ["name"] = "state", ["generation"] = 9, ["sequence"] = 4,
            ["writtenAtUtc"] = "2026-07-17T08:00:00.0000000Z", ["payloadJson"] = payloadJson
        };
        File.WriteAllText(Path.Combine(directory, "state.json"), snapshot.ToJsonString());
    }
}
