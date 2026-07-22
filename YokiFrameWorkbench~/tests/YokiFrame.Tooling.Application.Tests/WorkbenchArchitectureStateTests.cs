using System.Text.Json.Nodes;
using YokiFrame.Tooling.Application.Models.Architecture;
using YokiFrame.Tooling.Application.Services;

namespace YokiFrame.Tooling.Application.Tests;

/// <summary>
/// 覆盖 Architecture Snapshot 的 Application 强类型投影与只读周期读取。
/// </summary>
public sealed class WorkbenchArchitectureStateTests
{
    private const string ARCHITECTURE_PAYLOAD = """
        {"stats":{"diagnosticVersion":12,"architectureCount":1,"aliveCount":1,"serviceCount":2},"architectures":[{"typeName":"GameArchitecture","fullName":"Demo.GameArchitecture","createdAtUtc":"2026-07-14T09:00:00.0000000Z","instanceHash":31415,"isAlive":true,"initialized":true,"serviceCount":2,"services":[{"typeName":"IInventoryService","fullName":"Demo.IInventoryService","implementationTypeName":"InventoryService","implementationFullName":"Demo.InventoryService","initialized":true,"instanceHash":101},{"typeName":"ISaveService","fullName":"Demo.ISaveService","implementationTypeName":"SaveService","implementationFullName":"Demo.SaveService","initialized":false,"instanceHash":102}]}],"count":1}
        """;

    /// <summary>验证 dashboard 不发送命令即可投影实例、统计、服务和来源证据。</summary>
    [Fact]
    public void LoadDashboardParsesArchitectureSnapshotWithoutSendingCommand()
    {
        var projectRoot = CreateProjectRoot();
        WriteOnlineBridge(projectRoot, ARCHITECTURE_PAYLOAD);

        var dashboard = new WorkbenchDashboardService(projectRoot).LoadDashboard("unity-editor");
        var state = Assert.IsType<WorkbenchArchitectureState>(dashboard.ArchitectureState);

        Assert.Equal("unity-editor", state.EngineId);
        Assert.Equal("architecture-session", state.SessionId);
        Assert.Equal(9, state.Generation);
        Assert.Equal("EditMode", state.Mode);
        Assert.Equal("snapshot", state.Source);
        Assert.Equal(12, state.DiagnosticVersion);
        Assert.Equal(1, state.DeclaredCount);
        Assert.Equal(1, state.DeclaredAliveCount);
        Assert.Equal(2, state.DeclaredServiceCount);
        Assert.Equal(ARCHITECTURE_PAYLOAD, state.RawPayloadJson);
        Assert.EndsWith("Architecture" + Path.DirectorySeparatorChar + "state.json", Assert.Single(state.EvidencePaths));

        var architecture = Assert.Single(state.Architectures);
        Assert.Equal("GameArchitecture", architecture.TypeName);
        Assert.True(architecture.IsAlive);
        Assert.True(architecture.Initialized);
        Assert.Equal("InventoryService", architecture.Services[0].ImplementationTypeName);
        Assert.False(architecture.Services[1].Initialized);

        var commandsRoot = Path.Combine(projectRoot, ".yokiframe", "engines", "unity-editor", "commands");
        Assert.False(Directory.Exists(commandsRoot) && Directory.EnumerateFiles(commandsRoot, "*.json").Any());
    }

    /// <summary>验证无效 Architecture payload 不会中断 dashboard，并保留 stale 原因。</summary>
    [Fact]
    public void InvalidPayloadBecomesEmptyStaleState()
    {
        var projectRoot = CreateProjectRoot();
        WriteOnlineBridge(projectRoot, "{\"stats\":{}}");

        var dashboard = new WorkbenchDashboardService(projectRoot).LoadDashboard("unity-editor");
        var state = Assert.IsType<WorkbenchArchitectureState>(dashboard.ArchitectureState);

        Assert.Empty(state.Architectures);
        Assert.Contains("architectures array", state.StaleReason, StringComparison.Ordinal);
        Assert.Equal("{\"stats\":{}}", state.RawPayloadJson);
    }

    /// <summary>创建唯一测试项目根目录。</summary>
    private static string CreateProjectRoot()
    {
        return Path.Combine(Path.GetTempPath(), "yokiframe-workbench-architecture-tests", Guid.NewGuid().ToString("N"));
    }

    /// <summary>写入在线身份和当前 dashboard 需要的全部 Snapshot。</summary>
    private static void WriteOnlineBridge(string projectRoot, string architecturePayload)
    {
        var engineRoot = Path.Combine(projectRoot, ".yokiframe", "engines", "unity-editor");
        Directory.CreateDirectory(Path.Combine(projectRoot, ".yokiframe", "harness"));
        Directory.CreateDirectory(Path.Combine(engineRoot, "status"));
        File.WriteAllText(
            Path.Combine(projectRoot, ".yokiframe", "harness", "capabilities.json"),
            "{\"package\":{\"name\":\"YokiFrame\"}}");
        File.WriteAllText(Path.Combine(engineRoot, "engine.json"), CreateEngineRegistryJson(projectRoot));
        File.WriteAllText(
            Path.Combine(engineRoot, "status", "heartbeat.json"),
            "{\"protocolVersion\":2,\"engineId\":\"unity-editor\",\"sessionId\":\"architecture-session\",\"generation\":9,\"mode\":\"EditMode\",\"sequence\":3,\"createdAtUtc\":\"" + DateTimeOffset.UtcNow.ToString("O") + "\"}");
        WriteSnapshot(engineRoot, "System", "{\"status\":\"online\"}");
        WriteSnapshot(engineRoot, "Architecture", architecturePayload);
        WriteSnapshot(engineRoot, "FsmKit", "{\"fsms\":[],\"count\":0,\"selected\":{},\"history\":{\"history\":[],\"count\":0},\"stateEvents\":{\"events\":[],\"count\":0}}");
        WriteSnapshot(engineRoot, "EventKit", "{\"status\":\"online\"}");
        WriteSnapshot(engineRoot, "LogKit", "{\"status\":\"online\"}");
    }

    /// <summary>创建只声明 Snapshot 的测试 engine registry。</summary>
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
            ["sessionId"] = "architecture-session",
            ["generation"] = 9,
            ["mode"] = "EditMode",
            ["capabilities"] = new JsonArray("snapshot.read")
        };
        return registry.ToJsonString();
    }

    /// <summary>写入带稳定身份和时间的 Snapshot 信封。</summary>
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
            ["generation"] = 9,
            ["sequence"] = 4,
            ["writtenAtUtc"] = "2026-07-14T09:15:00.0000000Z",
            ["payloadJson"] = payloadJson
        };
        File.WriteAllText(Path.Combine(directory, "state.json"), snapshot.ToJsonString());
    }
}
