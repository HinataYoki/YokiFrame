using System.Text.Json.Nodes;
using YokiFrame.Tooling.Application.Models;
using YokiFrame.Tooling.Application.Models.EventKit;
using YokiFrame.Tooling.Application.Services;

namespace YokiFrame.Tooling.Application.Tests;

/// <summary>
/// 覆盖 EventKit Snapshot 的强类型投影和无命令周期读取。
/// </summary>
public sealed class WorkbenchEventKitStateTests
{
    private const string EVENT_PAYLOAD = """
        {"version":12,"sequence":4,"counts":{"typeEvents":1,"enumEvents":1,"stringEvents":0,"totalEvents":2,"totalHandlers":3,"recentActivities":3},"events":[{"channel":"Type","eventKey":"DamageEvent","payloadType":"DamageEvent","handlerCount":2,"lastSequence":3,"lastTime":"10:00:00.030","deprecated":false},{"channel":"Enum","eventKey":"GameSignal.Ready","payloadType":"","handlerCount":1,"lastSequence":4,"lastTime":"10:00:00.040","deprecated":false}],"recentEvents":{"count":3,"events":[{"sequence":2,"kind":"register","channel":"Type","eventKey":"DamageEvent","payloadType":"DamageEvent","handler":"Demo.DamageView.OnDamage","time":"10:00:00.020"},{"sequence":3,"kind":"send","channel":"Type","eventKey":"DamageEvent","payloadType":"DamageEvent","handler":"","time":"10:00:00.030"},{"sequence":4,"kind":"send","channel":"Enum","eventKey":"GameSignal.Ready","payloadType":"","handler":"","time":"10:00:00.040"}]}}
        """;

    /// <summary>验证 dashboard 仅从 snapshot 投影 EventKit 事件、统计与时间线。</summary>
    [Fact]
    public void LoadDashboardParsesEventKitSnapshotWithoutSendingCommand()
    {
        string projectRoot = CreateProjectRoot();
        WriteOnlineBridge(projectRoot, EVENT_PAYLOAD);

        WorkbenchDashboardState dashboard = new WorkbenchDashboardService(projectRoot)
            .LoadDashboard("unity-editor");
        WorkbenchEventKitState state = Assert.IsType<WorkbenchEventKitState>(dashboard.EventKitState);

        Assert.Equal("event-session", state.SessionId);
        Assert.Equal(5, state.Generation);
        Assert.Equal("snapshot", state.Source);
        Assert.Equal(12, state.Version);
        Assert.Equal(4, state.Sequence);
        Assert.Equal(2, state.TotalEventCount);
        Assert.Equal(3, state.TotalHandlerCount);
        Assert.Equal("DamageEvent", state.Events[0].EventKey);
        Assert.Equal("register", state.RecentActivities[0].Kind);
        Assert.EndsWith("EventKit" + Path.DirectorySeparatorChar + "state.json", Assert.Single(state.EvidencePaths));
        string commandsRoot = Path.Combine(projectRoot, ".yokiframe", "engines", "unity-editor", "commands");
        Assert.False(Directory.Exists(commandsRoot) && Directory.EnumerateFiles(commandsRoot, "*.json").Any());
    }

    /// <summary>验证缺少 events 数组时返回带诊断的安全空状态。</summary>
    [Fact]
    public void InvalidEventKitPayloadBecomesEmptyStaleState()
    {
        string projectRoot = CreateProjectRoot();
        WriteOnlineBridge(projectRoot, "{\"counts\":{}}");

        WorkbenchEventKitState state = Assert.IsType<WorkbenchEventKitState>(
            new WorkbenchDashboardService(projectRoot).LoadDashboard("unity-editor").EventKitState);

        Assert.Empty(state.Events);
        Assert.Contains("events array", state.StaleReason, StringComparison.Ordinal);
    }

    /// <summary>创建唯一测试项目根。</summary>
    private static string CreateProjectRoot()
    {
        return Path.Combine(Path.GetTempPath(), "yokiframe-eventkit-tests", Guid.NewGuid().ToString("N"));
    }

    /// <summary>写入在线 bridge 与 dashboard 所需全部 Snapshot。</summary>
    private static void WriteOnlineBridge(string projectRoot, string eventPayload)
    {
        string engineRoot = Path.Combine(projectRoot, ".yokiframe", "engines", "unity-editor");
        Directory.CreateDirectory(Path.Combine(projectRoot, ".yokiframe", "harness"));
        Directory.CreateDirectory(Path.Combine(engineRoot, "status"));
        File.WriteAllText(
            Path.Combine(projectRoot, ".yokiframe", "harness", "capabilities.json"),
            "{\"package\":{\"name\":\"YokiFrame\"}}");
        File.WriteAllText(Path.Combine(engineRoot, "engine.json"), CreateEngineRegistryJson(projectRoot));
        File.WriteAllText(
            Path.Combine(engineRoot, "status", "heartbeat.json"),
            "{\"protocolVersion\":2,\"engineId\":\"unity-editor\",\"sessionId\":\"event-session\",\"generation\":5,\"mode\":\"PlayMode\",\"sequence\":3,\"createdAtUtc\":\"" + DateTimeOffset.UtcNow.ToString("O") + "\"}");
        WriteSnapshot(engineRoot, "System", "{\"status\":\"online\"}");
        WriteSnapshot(engineRoot, "Architecture", "{\"architectures\":[],\"count\":0}");
        WriteSnapshot(engineRoot, "FsmKit", "{\"fsms\":[],\"count\":0,\"selected\":{},\"history\":{\"history\":[],\"count\":0},\"stateEvents\":{\"events\":[],\"count\":0}}");
        WriteSnapshot(engineRoot, "EventKit", eventPayload);
        WriteSnapshot(engineRoot, "LogKit", "{\"status\":\"online\"}");
    }

    /// <summary>创建只声明 snapshot.read 的测试 registry。</summary>
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
            ["sessionId"] = "event-session",
            ["generation"] = 5,
            ["mode"] = "PlayMode",
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
            ["protocolVersion"] = 2,
            ["engineId"] = "unity-editor",
            ["kit"] = kit,
            ["name"] = "state",
            ["generation"] = 5,
            ["sequence"] = 4,
            ["writtenAtUtc"] = "2026-07-15T08:00:00.0000000Z",
            ["payloadJson"] = payloadJson
        };
        File.WriteAllText(Path.Combine(directory, "state.json"), snapshot.ToJsonString());
    }
}
