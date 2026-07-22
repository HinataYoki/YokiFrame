using System.Text.Json.Nodes;
using YokiFrame.Tooling.Application.Models;
using YokiFrame.Tooling.Application.Models.ActionKit;
using YokiFrame.Tooling.Application.Services;

namespace YokiFrame.Tooling.Application.Tests;

/// <summary>覆盖 ActionKit Snapshot 的严格强类型投影和无命令周期读取。</summary>
public sealed class WorkbenchActionKitStateTests
{
    private const string ACTION_PAYLOAD = """
        {"schemaVersion":1,"version":12,"stats":{"frameCount":240,"activeRootCount":1,"finishedCount":8,"cancelledCount":2,"faultedCount":1,"terminalEventCount":11,"stackTraceEnabled":true,"stackTraceCount":1},"roots":[{"actionId":"9007199254740993","type":"Sequence","status":"Started","paused":false,"deinited":false,"debugInfo":"Sequence(2 actions, index=1)","updateMode":"UnscaledDeltaTime","cancelRequested":false,"stackTrace":[{"method":"Sample.Start","file":"Sample.cs","line":18}],"children":[{"actionId":"8","type":"Delay","status":"Started","paused":false,"deinited":false,"debugInfo":"Delay(1s)","children":[]}]}],"rootCount":1,"rootTotal":1,"rootsTruncated":false,"nodeCount":2,"events":[{"actionId":"7","actionType":"Callback","outcome":"Completed","frame":238,"errorMessage":""}],"eventCount":1,"eventTotal":11,"eventsTruncated":true,"nodesTruncated":false,"depthTruncated":false,"stackTruncated":false}
        """;

    /// <summary>验证周期读取保留字符串 Action ID 且不会创建命令文件。</summary>
    [Fact]
    public void LoadDashboardProjectsActionKitWithoutSendingCommand()
    {
        string projectRoot = CreateProjectRoot();
        WriteOnlineBridge(projectRoot, ACTION_PAYLOAD);

        WorkbenchDashboardState dashboard = new WorkbenchDashboardService(projectRoot)
            .LoadDashboard("unity-editor");
        WorkbenchActionKitState state = Assert.IsType<WorkbenchActionKitState>(dashboard.ActionKitState);

        Assert.Equal("9007199254740993", Assert.Single(state.Roots).ActionId);
        Assert.Equal("UnscaledDeltaTime", state.Roots[0].UpdateMode);
        Assert.Equal(11L, state.EventTotal);
        Assert.True(state.EventsTruncated);
        string commandsRoot = Path.Combine(
            projectRoot,
            ".yokiframe",
            "engines",
            "unity-editor",
            "commands");
        Assert.False(Directory.Exists(commandsRoot)
            && Directory.EnumerateFiles(commandsRoot, "*.json").Any());
    }

    /// <summary>验证数值 Action ID 被拒绝，避免超过 JS 安全整数后静默丢精度。</summary>
    [Fact]
    public void NumericActionIdProducesStaleEmptyState()
    {
        string projectRoot = CreateProjectRoot();
        string invalid = ACTION_PAYLOAD.Replace(
            "\"actionId\":\"9007199254740993\"",
            "\"actionId\":9007199254740993",
            StringComparison.Ordinal);
        WriteOnlineBridge(projectRoot, invalid);

        WorkbenchActionKitState state = Assert.IsType<WorkbenchActionKitState>(
            new WorkbenchDashboardService(projectRoot).LoadDashboard("unity-editor").ActionKitState);

        Assert.Empty(state.Roots);
        Assert.Contains("must be a string", state.StaleReason, StringComparison.Ordinal);
    }

    /// <summary>创建唯一测试项目根。</summary>
    private static string CreateProjectRoot()
    {
        return Path.Combine(
            Path.GetTempPath(),
            "yokiframe-actionkit-tests",
            Guid.NewGuid().ToString("N"));
    }

    /// <summary>写入在线 bridge 与 dashboard 所需全部 Snapshot。</summary>
    private static void WriteOnlineBridge(string projectRoot, string actionPayload)
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
            "{\"protocolVersion\":2,\"engineId\":\"unity-editor\",\"sessionId\":\"action-session\",\"generation\":9,\"mode\":\"PlayMode\",\"sequence\":3,\"createdAtUtc\":\""
                + DateTimeOffset.UtcNow.ToString("O") + "\"}");
        WriteSnapshot(engineRoot, "System", "{\"status\":\"online\"}");
        WriteSnapshot(engineRoot, "Architecture", "{\"architectures\":[],\"count\":0}");
        WriteSnapshot(engineRoot, "FsmKit", "{\"fsms\":[],\"count\":0,\"selected\":{},\"history\":{\"history\":[],\"count\":0},\"stateEvents\":{\"events\":[],\"count\":0}}");
        WriteSnapshot(engineRoot, "EventKit", "{\"events\":[]}");
        WriteSnapshot(engineRoot, "LogKit", "{\"status\":\"online\"}");
        WriteSnapshot(engineRoot, "PoolKit", "{\"pools\":[]}");
        WriteSnapshot(engineRoot, "ActionKit", actionPayload);
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
            ["sessionId"] = "action-session",
            ["generation"] = 9,
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
            ["generation"] = 9,
            ["sequence"] = 4,
            ["writtenAtUtc"] = "2026-07-16T08:00:00.0000000Z",
            ["payloadJson"] = payloadJson
        };
        File.WriteAllText(Path.Combine(directory, "state.json"), snapshot.ToJsonString());
    }
}
