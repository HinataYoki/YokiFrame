using System.Text.Json.Nodes;
using YokiFrame.Tooling.Application.Models;
using YokiFrame.Tooling.Application.Services;

namespace YokiFrame.Tooling.Application.Tests;

/// <summary>覆盖 ResKit Snapshot 的强类型投影和无命令周期读取。</summary>
public sealed class WorkbenchResKitStateTests
{
    private const string RES_PAYLOAD = """
        {"schemaVersion":1,"diagnosticVersion":19,"provider":{"name":"Unity.Resources","generation":3,"capabilities":{"rawBytes":true,"rawText":true}},"stats":{"loadedCount":2,"inFlightCount":1,"totalLeaseCount":4,"unloadHistoryCount":3,"loadLocationTrackingEnabled":true},"resources":{"items":[{"path":"Audio/Hit","typeName":"UnityEngine.AudioClip","state":"Ready","leaseCount":3,"providerName":"Unity.Resources","providerGeneration":3,"trackedSourceCount":2,"sources":[{"display":"AudioLoader.Play","filePath":"Assets/Audio/AudioLoader.cs","line":42,"refCount":1,"anonymous":false,"tracked":true}],"sourceTotal":3,"sourcesTruncated":true}],"totalCount":5,"truncated":true},"unloadHistory":{"items":[{"path":"Audio/Old","typeName":"UnityEngine.AudioClip","providerName":"Unity.Resources","unloadTimeUtc":"2026-07-17T08:00:00Z"}],"totalCount":3,"droppedCount":2147483648,"truncated":true},"lastBackgroundFailure":""}
        """;

    /// <summary>验证 dashboard 公开 ResKit 强类型状态，并且周期读取不会创建命令文件。</summary>
    [Fact]
    public void LoadDashboardProjectsResKitSnapshotWithoutSendingCommand()
    {
        string projectRoot = CreateProjectRoot();
        WriteOnlineBridge(projectRoot);

        WorkbenchDashboardState dashboard = new WorkbenchDashboardService(projectRoot)
            .LoadDashboard("unity-editor");
        var state = Assert.IsType<YokiFrame.Tooling.Application.Models.ResKit.WorkbenchResKitState>(dashboard.ResKitState);

        Assert.Equal("Unity.Resources", state.Provider.Name);
        Assert.Equal(4, state.Stats.TotalLeaseCount);
        Assert.Equal(5, state.ResourceTotal);
        Assert.True(state.ResourcesTruncated);
        Assert.Equal(2147483648L, state.HistoryDroppedCount);
        var resource = Assert.Single(state.Resources);
        var source = Assert.Single(resource.Sources);
        Assert.Equal("Assets/Audio/AudioLoader.cs", source.FilePath);
        Assert.Equal(3, resource.SourceTotal);
        Assert.True(resource.SourcesTruncated);
        string commandsRoot = Path.Combine(projectRoot, ".yokiframe", "engines", "unity-editor", "commands");
        Assert.False(Directory.Exists(commandsRoot) && Directory.EnumerateFiles(commandsRoot, "*.json").Any());
    }

    /// <summary>验证按需详情保留原子诊断版本、源码行号和跟踪标记。</summary>
    [Fact]
    public void ResourceDetailParserPreservesVersionAndSources()
    {
        const string payload = """
            {"schemaVersion":1,"diagnosticVersion":27,"resource":{"path":"Audio/Hit","typeName":"UnityEngine.AudioClip","state":"Ready","leaseCount":2,"providerName":"Unity.Resources","providerGeneration":3,"trackedSourceCount":1,"sources":[{"display":"Loader.Load","filePath":"Assets/Audio/Loader.cs","line":42,"refCount":2,"anonymous":false,"tracked":true}],"sourceTotal":1,"sourcesTruncated":false}}
            """;
        Type parser = typeof(YokiFrame.Tooling.Application.Models.ResKit.WorkbenchResKitState).Assembly.GetType(
            "YokiFrame.Tooling.Application.Models.ResKit.WorkbenchResKitStateParser", true)!;
        var method = parser.GetMethod(
            "ParseResourceDetail",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);

        var detail = Assert.IsType<YokiFrame.Tooling.Application.Models.ResKit.WorkbenchResKitResourceDetail>(
            method!.Invoke(null, new object[] { payload }));

        Assert.Equal(27L, detail.Version);
        var source = Assert.Single(detail.Resource.Sources);
        Assert.Equal(42, source.Line);
        Assert.True(source.IsTracked);
    }

    /// <summary>创建唯一测试项目根。</summary>
    private static string CreateProjectRoot()
    {
        return Path.Combine(Path.GetTempPath(), "yokiframe-reskit-tests", Guid.NewGuid().ToString("N"));
    }

    /// <summary>写入在线 bridge 与 ResKit Snapshot。</summary>
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
            "{\"protocolVersion\":2,\"engineId\":\"unity-editor\",\"sessionId\":\"res-session\",\"generation\":8,\"mode\":\"PlayMode\",\"sequence\":3,\"createdAtUtc\":\"" + DateTimeOffset.UtcNow.ToString("O") + "\"}");
        WriteSnapshot(engineRoot, "ResKit", RES_PAYLOAD);
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
            ["sessionId"] = "res-session",
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
            ["writtenAtUtc"] = "2026-07-17T08:00:00.0000000Z",
            ["payloadJson"] = payloadJson
        };
        File.WriteAllText(Path.Combine(directory, "state.json"), snapshot.ToJsonString());
    }
}
