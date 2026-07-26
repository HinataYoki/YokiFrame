using System.Text.Json.Nodes;
using YokiFrame.Tooling.Application.Models;
using YokiFrame.Tooling.Application.Services;

namespace YokiFrame.Tooling.Application.Tests;

/// <summary>
/// 覆盖 PoolKit Snapshot 的强类型投影和无命令周期读取。
/// </summary>
public sealed class WorkbenchPoolKitStateTests
{
    private const string POOL_PAYLOAD = """
        {"schemaVersion":1,"version":9,"stats":{"poolCount":1,"totalCount":3,"totalActive":2,"totalInactive":1,"totalPeak":3,"trackingEnabled":true,"stackTraceEnabled":false,"eventHistoryEnabled":true,"eventHistoryCount":2},"pools":[{"poolId":"pool-42","name":"PanelHandler","typeName":"YokiFrame.PanelHandler","totalCount":3,"activeCount":2,"inactiveCount":1,"peakCount":3,"maxCacheCount":20,"usageRate":0.6667,"healthStatus":"Normal","activeObjectTotal":2,"activeObjectTruncated":false,"inactiveObjectTotal":1,"inactiveObjectTruncated":false,"activeObjects":[{"objectName":"Panel-A","spawnTime":16.85,"sourceFile":"Assets/UI/Panel.cs","sourceLine":18},{"objectName":"Panel-B","spawnTime":20.44,"sourceFile":"","sourceLine":0}],"inactiveObjects":[{"objectName":"Panel-C"}]}],"events":[{"eventType":"Spawn","timestamp":20.44,"poolId":"pool-42","poolName":"PanelHandler","objectName":"Panel-B","sourceFile":"","sourceLine":0}],"leaks":{"suspectedLeaks":[{"poolId":"pool-42","poolName":"PanelHandler","activeCount":2,"peakCount":3}],"count":1,"total":2,"truncated":true,"trackingEnabled":true}}
        """;

    /// <summary>验证 dashboard 公开 PoolKit 强类型状态，并且周期读取不会创建命令文件。</summary>
    [Fact]
    public void LoadDashboardProjectsPoolKitSnapshotWithoutSendingCommand()
    {
        string projectRoot = CreateProjectRoot();
        WriteOnlineBridge(projectRoot);

        WorkbenchDashboardState dashboard = new WorkbenchDashboardService(projectRoot)
            .LoadDashboard("unity-editor");

        Assert.NotNull(dashboard.PoolKitState);
        var state = dashboard.PoolKitState!;
        Assert.Equal(1, state.PoolCount);
        Assert.Equal(2, state.TotalActive);
        var pool = Assert.Single(state.Pools);
        Assert.Equal("pool-42", pool.PoolId);
        Assert.Equal(2, state.Leaks.Total);
        Assert.True(state.Leaks.Truncated);
        string commandsRoot = Path.Combine(projectRoot, ".yokiframe", "engines", "unity-editor", "commands");
        Assert.False(Directory.Exists(commandsRoot) && Directory.EnumerateFiles(commandsRoot, "*.json").Any());
    }

    /// <summary>创建唯一测试项目根。</summary>
    private static string CreateProjectRoot()
    {
        return Path.Combine(Path.GetTempPath(), "yokiframe-poolkit-tests", Guid.NewGuid().ToString("N"));
    }

    /// <summary>写入在线 bridge 与 dashboard 所需全部 Snapshot。</summary>
    private static void WriteOnlineBridge(string projectRoot)
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
            "{\"protocolVersion\":2,\"engineId\":\"unity-editor\",\"sessionId\":\"pool-session\",\"generation\":8,\"mode\":\"PlayMode\",\"sequence\":3,\"createdAtUtc\":\"" + DateTimeOffset.UtcNow.ToString("O") + "\"}");
        WriteSnapshot(engineRoot, "System", "{\"status\":\"online\"}");
        WriteSnapshot(engineRoot, "Architecture", "{\"architectures\":[],\"count\":0}");
        WriteSnapshot(engineRoot, "FsmKit", "{\"fsms\":[],\"count\":0,\"selected\":{},\"history\":{\"history\":[],\"count\":0},\"stateEvents\":{\"events\":[],\"count\":0}}");
        WriteSnapshot(engineRoot, "EventKit", "{\"events\":[]}");
        WriteSnapshot(engineRoot, "LogKit", "{\"status\":\"online\"}");
        WriteSnapshot(engineRoot, "PoolKit", POOL_PAYLOAD);
    }

    /// <summary>创建只声明 snapshot.read 的测试 registry。</summary>
    private static string CreateEngineRegistryJson(string projectRoot)
    {
        JsonObject registry = new()
        {
            ["protocolVersion"] = 2,
            ["engineId"] = "unity-editor",
            ["engine"] = "Unity",
            ["version"] = "6000.7.0a2",
            ["projectPath"] = projectRoot,
            ["adapterVersion"] = "test",
            ["sessionId"] = "pool-session",
            ["generation"] = 8,
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
            ["generation"] = 8,
            ["sequence"] = 4,
            ["writtenAtUtc"] = "2026-07-16T08:00:00.0000000Z",
            ["payloadJson"] = payloadJson
        };
        File.WriteAllText(Path.Combine(directory, "state.json"), snapshot.ToJsonString());
    }
}
