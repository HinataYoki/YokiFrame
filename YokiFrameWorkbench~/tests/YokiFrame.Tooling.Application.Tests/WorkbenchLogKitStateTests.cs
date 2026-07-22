using System.Text.Json.Nodes;
using YokiFrame.Tooling.Application.Models.LogKit;
using YokiFrame.Tooling.Application.Services;

namespace YokiFrame.Tooling.Application.Tests;

/// <summary>覆盖 Dashboard 对 schemaVersion=1 LogKit state 的强类型投影。</summary>
public sealed class WorkbenchLogKitStateTests
{
    private const string VALID_STATE = """
        {"schemaVersion":1,"diagnosticVersion":9,"settingsVersion":3,"stats":{"loggerName":"UnityEngineLogger","hasLogger":true,"enabled":true,"minimumLevel":"Info","historyCount":1,"droppedCount":2},"settings":{"enabled":true,"minimumLevel":"Info","saveLogInEditor":true,"saveLogInPlayer":false,"enableIMGUIInPlayer":false,"enableEncryption":true,"maxQueueSize":4096,"maxSameLogCount":7,"maxRetentionDays":5,"maxFileSizeMB":32,"imguiMaxLogCount":80,"logDirectory":"","editorFileName":"editor.log","playerFileName":"player.log"},"capabilities":{"settingsApply":true,"filePreview":true,"fileWriter":false,"playerImGui":false,"encryption":false},"files":{"directory":"C:/Logs","editor":{"kind":"editor","path":"C:/Logs/editor.log","fileName":"editor.log","exists":true,"sizeBytes":12,"modifiedUtc":"2026-07-15T08:00:00Z"},"player":{"kind":"player","path":"C:/Logs/player.log","fileName":"player.log","exists":false,"sizeBytes":0,"modifiedUtc":""}},"history":{"entries":[{"level":"Warning","message":"test","context":"ctx","exceptionType":"","exceptionMessage":"","stackTrace":"stack","timestampUtc":"2026-07-15T08:00:01Z"}],"count":1,"totalCount":4,"droppedCount":2,"truncated":true}}
        """;

    /// <summary>验证 Dashboard 完整投影设置、统计、能力、文件和历史。</summary>
    [Fact]
    public void LoadDashboardParsesCompleteLogKitState()
    {
        var projectRoot = CreateProjectRoot();
        WriteOnlineBridge(projectRoot, VALID_STATE);

        var dashboard = new WorkbenchDashboardService(projectRoot).LoadDashboard("unity-editor");
        var state = Assert.IsType<WorkbenchLogKitState>(dashboard.LogKitState);

        Assert.Equal(1, state.SchemaVersion);
        Assert.Equal(9, state.DiagnosticVersion);
        Assert.Equal(3, state.SettingsVersion);
        Assert.Equal("snapshot", state.Source);
        Assert.Equal("Info", state.Settings.MinimumLevel);
        Assert.Equal(4096, state.Settings.MaxQueueSize);
        Assert.True(state.Capabilities.SettingsApply);
        Assert.False(state.Capabilities.Encryption);
        Assert.Equal("C:/Logs/editor.log", state.Files.Editor.Path);
        Assert.True(state.Files.Editor.Exists);
        Assert.Single(state.History.Entries);
        Assert.Equal(4, state.History.TotalCount);
        Assert.True(state.History.Truncated);
        Assert.Equal("stack", state.History.Entries[0].StackTrace);
    }

    /// <summary>验证未知 schema 不进入兼容分支，而是形成带原因的安全空状态。</summary>
    [Fact]
    public void LoadDashboardRejectsUnknownLogKitSchema()
    {
        var projectRoot = CreateProjectRoot();
        WriteOnlineBridge(projectRoot, "{\"schemaVersion\":2,\"settings\":{}}");

        var state = Assert.IsType<WorkbenchLogKitState>(
            new WorkbenchDashboardService(projectRoot).LoadDashboard("unity-editor").LogKitState);

        Assert.Equal(0, state.SchemaVersion);
        Assert.Empty(state.History.Entries);
        Assert.Contains("schemaVersion", state.StaleReason, StringComparison.Ordinal);
    }

    /// <summary>创建唯一测试项目。</summary>
    private static string CreateProjectRoot()
    {
        return Path.Combine(Path.GetTempPath(), "yokiframe-logkit-state-tests", Guid.NewGuid().ToString("N"));
    }

    /// <summary>写入可被 Dashboard 验证的最小在线 Host 和 LogKit snapshot。</summary>
    private static void WriteOnlineBridge(string projectRoot, string payloadJson)
    {
        var engineRoot = Path.Combine(projectRoot, ".yokiframe", "engines", "unity-editor");
        Directory.CreateDirectory(Path.Combine(engineRoot, "status"));
        Directory.CreateDirectory(Path.Combine(engineRoot, "snapshots", "LogKit"));
        File.WriteAllText(
            Path.Combine(engineRoot, "engine.json"),
            new JsonObject
            {
                ["protocolVersion"] = 2,
                ["engineId"] = "unity-editor",
                ["engine"] = "Unity",
                ["projectPath"] = projectRoot,
                ["sessionId"] = "log-session",
                ["generation"] = 4,
                ["mode"] = "PlayMode",
                ["capabilities"] = new JsonArray("snapshot.read")
            }.ToJsonString());
        File.WriteAllText(
            Path.Combine(engineRoot, "status", "heartbeat.json"),
            new JsonObject
            {
                ["protocolVersion"] = 2,
                ["engineId"] = "unity-editor",
                ["sessionId"] = "log-session",
                ["generation"] = 4,
                ["mode"] = "PlayMode",
                ["sequence"] = 2,
                ["createdAtUtc"] = DateTimeOffset.UtcNow.ToString("O")
            }.ToJsonString());
        File.WriteAllText(
            Path.Combine(engineRoot, "snapshots", "LogKit", "state.json"),
            new JsonObject
            {
                ["protocolVersion"] = 2,
                ["engineId"] = "unity-editor",
                ["kit"] = "LogKit",
                ["name"] = "state",
                ["generation"] = 4,
                ["writtenAtUtc"] = "2026-07-15T08:00:02Z",
                ["payloadJson"] = payloadJson
            }.ToJsonString());
    }
}
