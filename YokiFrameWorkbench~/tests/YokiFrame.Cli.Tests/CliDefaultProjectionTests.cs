using System.Text.Json.Nodes;

namespace YokiFrame.Cli.Tests;

/// <summary>
/// 覆盖 CLI 默认投影：默认输出必须只包含 AI 判断状态所需的最小字段，完整字段只在显式请求或失败证据中出现。
/// </summary>
public sealed class CliDefaultProjectionTests
{
    private const string ENGINE_ID = "unity-editor";

    /// <summary>
    /// 验证 engine list 默认省略 registry 路径和 FastChannel action 全量列表。
    /// </summary>
    [Fact]
    public async Task EngineListDefaultOmitsRegistryPathsAndFastChannelCatalog()
    {
        using var project = ProjectFixture.Create();
        project.WriteEngineRegistry();
        project.WriteHeartbeat();

        var result = await RunCliAsync("engine", "list", "--project", project.Path);

        Assert.Equal(0, result.ExitCode);
        var json = JsonNode.Parse(result.StandardOutput)!;
        Assert.True(json["ok"]!.GetValue<bool>());
        Assert.Equal("Ready", json["state"]!.GetValue<string>());
        Assert.False(json.AsObject().ContainsKey("enginesRoot"));
        var engine = json["engines"]![0]!;
        Assert.Equal(ENGINE_ID, engine["engineId"]!.GetValue<string>());
        Assert.True(engine["fastChannel"]!.GetValue<bool>());
        Assert.False(engine.AsObject().ContainsKey("fastChannels"));
        Assert.False(engine.AsObject().ContainsKey("projectPath"));
        Assert.False(engine.AsObject().ContainsKey("sessionId"));
        Assert.True(result.StandardOutput.Length < 600, result.StandardOutput);
    }

    /// <summary>
    /// 验证 snapshot read 默认不再返回协议绝对路径，但仍返回 snapshot 内容。
    /// </summary>
    [Fact]
    public async Task SnapshotReadDefaultOmitsAbsolutePath()
    {
        using var project = ProjectFixture.Create();
        project.WriteEngineRegistry();
        project.WriteHeartbeat();
        project.WriteSnapshot();

        var result = await RunCliAsync(
            "snapshot",
            "read",
            "--project",
            project.Path,
            "--engine",
            ENGINE_ID,
            "--kit",
            "System",
            "--name",
            "state");

        Assert.Equal(0, result.ExitCode);
        var json = JsonNode.Parse(result.StandardOutput)!;
        Assert.Equal("Ready", json["state"]!.GetValue<string>());
        Assert.Equal("snapshot", json["source"]!.GetValue<string>());
        Assert.False(json.AsObject().ContainsKey("path"));
        Assert.False(json.AsObject().ContainsKey("data"));
        Assert.Equal("online", json["summary"]!["status"]!.GetValue<string>());
        Assert.Equal(1L, json["generation"]!.GetValue<long>());
        Assert.True(result.StandardOutput.Length < 400, result.StandardOutput);
    }

    /// <summary>
    /// 验证 telemetry 不可用时 kit status 自动回落 snapshot，并说明回落原因。
    /// </summary>
    [Fact]
    public async Task KitStatusFallsBackToSnapshotWhenTelemetryIsUnavailable()
    {
        using var project = ProjectFixture.Create();
        project.WriteEngineRegistry();
        project.WriteHeartbeat();
        project.WriteSnapshot();

        var result = await RunCliAsync(
            "kit",
            "status",
            "--project",
            project.Path,
            "--engine",
            ENGINE_ID,
            "--kit",
            "System");

        Assert.Equal(0, result.ExitCode);
        var json = JsonNode.Parse(result.StandardOutput)!;
        Assert.True(json["ok"]!.GetValue<bool>(), result.StandardError);
        Assert.Equal("Ready", json["state"]!.GetValue<string>());
        Assert.Equal("snapshot", json["source"]!.GetValue<string>());
        Assert.Equal("online", json["summary"]!["status"]!.GetValue<string>());
        Assert.Equal("TelemetryNotUsed", json["issues"]![0]!["code"]!.GetValue<string>());
        Assert.False(json.AsObject().ContainsKey("snapshotPath"));
    }

    /// <summary>
    /// 验证既没有 telemetry 也没有 snapshot 时 kit status 返回 Unavailable 而不是静默成功。
    /// </summary>
    [Fact]
    public async Task KitStatusFailsWhenNeitherChannelIsAvailable()
    {
        using var project = ProjectFixture.Create();
        project.WriteEngineRegistry();
        project.WriteHeartbeat();

        var result = await RunCliAsync(
            "kit",
            "status",
            "--project",
            project.Path,
            "--engine",
            ENGINE_ID,
            "--kit",
            "System");

        Assert.Equal(1, result.ExitCode);
        Assert.Equal(string.Empty, result.StandardOutput.Trim());
        var json = JsonNode.Parse(result.StandardError)!;
        Assert.False(json["ok"]!.GetValue<bool>());
        Assert.Equal("Unavailable", json["state"]!.GetValue<string>());
        Assert.Equal("none", json["source"]!.GetValue<string>());
        Assert.Equal("KitUnavailable", json["error"]!["code"]!.GetValue<string>());
    }

    /// <summary>
    /// 验证 Project Model 缺失时 project status 直接失败，并且不重复输出聚合证据路径。
    /// </summary>
    [Fact]
    public async Task ProjectStatusFailsWithoutRepeatingAggregateEvidence()
    {
        using var project = ProjectFixture.Create();

        var result = await RunCliAsync("project", "status", "--project", project.Path);

        Assert.Equal(1, result.ExitCode);
        Assert.Equal(string.Empty, result.StandardOutput.Trim());
        var json = JsonNode.Parse(result.StandardError)!;
        Assert.False(json["ok"]!.GetValue<bool>());
        Assert.Equal("Unavailable", json["state"]!.GetValue<string>());
        Assert.Equal("ProjectModelNotReady", json["error"]!["code"]!.GetValue<string>());
        Assert.Equal("ProjectModelMissing", json["issues"]![0]!["code"]!.GetValue<string>());
        Assert.Contains("project refresh", json["nextActions"]!.AsArray().Select(node => node!.GetValue<string>()));
        // Client 层把五个固定模型文件作为审计证据，CLI 只负责不再重复输出一份聚合列表。
        Assert.False(json.AsObject().ContainsKey("evidencePaths"));
        Assert.Equal(5, json["error"]!["evidencePaths"]!.AsArray().Count);
        Assert.True(result.StandardError.Length < 900, result.StandardError);
    }

    /// <summary>
    /// 验证 doctor 与 bridge status 默认不返回协议目录绝对路径，完整 status 只在 --detail full 出现。
    /// </summary>
    [Fact]
    public async Task BridgeDiagnosticsKeepAbsolutePathsOutOfDefaultOutput()
    {
        using var project = ProjectFixture.Create();
        project.WriteEngineRegistry();
        project.WriteHeartbeat();

        string[][] defaultCommands =
        [
            ["doctor"],
            ["bridge", "status"]
        ];
        foreach (var verbs in defaultCommands)
        {
            var result = await RunCliAsync([.. verbs, "--project", project.Path, "--engine", ENGINE_ID]);

            Assert.Equal(0, result.ExitCode);
            var json = JsonNode.Parse(result.StandardOutput)!;
            Assert.False(json.AsObject().ContainsKey("status"));
            Assert.True(json["summary"]!.AsObject().ContainsKey("pending"));
            Assert.DoesNotContain(project.Path, result.StandardOutput, StringComparison.OrdinalIgnoreCase);
        }

        var fullResult = await RunCliAsync("doctor", "--detail", "full", "--project", project.Path, "--engine", ENGINE_ID);
        Assert.Equal(0, fullResult.ExitCode);
        var fullJson = JsonNode.Parse(fullResult.StandardOutput)!;
        Assert.True(fullJson["status"]!.AsObject().ContainsKey("engineRoot"));
    }

    /// <summary>启动真实 CLI 程序并捕获输出。</summary>
    /// <param name="arguments">CLI 参数。</param>
    /// <returns>进程执行结果。</returns>
    private static Task<CliProcessResult> RunCliAsync(params string[] arguments)
        => CliTestHelpers.RunCliAsync(arguments);

    /// <summary>创建带最小 engine registry 的临时项目根，并在测试结束后清理。</summary>
    private sealed class ProjectFixture : IDisposable
    {
        /// <summary>创建临时项目根和 FileBridge 目录。</summary>
        private ProjectFixture()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "yokiframe-cli-projection-tests", Guid.NewGuid().ToString("N"));
            EngineRoot = System.IO.Path.Combine(Path, ".yokiframe", "engines", ENGINE_ID);
            Directory.CreateDirectory(System.IO.Path.Combine(EngineRoot, "status"));
        }

        /// <summary>获取临时项目根路径。</summary>
        public string Path { get; }

        /// <summary>获取测试 engine 根目录。</summary>
        private string EngineRoot { get; }

        /// <summary>创建新的测试项目根。</summary>
        /// <returns>临时项目实例。</returns>
        public static ProjectFixture Create() => new();

        /// <summary>写入带 FastChannel endpoint 的 engine registry。</summary>
        public void WriteEngineRegistry()
        {
            File.WriteAllText(
                System.IO.Path.Combine(EngineRoot, "engine.json"),
                "{\"protocolVersion\":2,\"engineId\":\"unity-editor\",\"engine\":\"Unity\",\"version\":\"6000.0.0f1\",\"projectPath\":\""
                + Escape(Path)
                + "\",\"adapterVersion\":\"test\",\"sessionId\":\"session\",\"generation\":1,\"mode\":\"EditMode\",\"capabilities\":[\"snapshot.read\",\"command.send\"],\"fastChannels\":[{\"protocolVersion\":1,\"engineId\":\"unity-editor\",\"sessionId\":\"session\",\"generation\":1,\"transport\":\"namedPipe\",\"endpoint\":\"YokiFrame.FastChannel.test\",\"enabled\":true,\"fallback\":\"filebridge\",\"readOnlyCommands\":[\"System/ping\",\"FsmKit/get_state\"]}]}");
        }

        /// <summary>写入新鲜 heartbeat，使 generation 可以被 CLI 解析。</summary>
        public void WriteHeartbeat()
        {
            File.WriteAllText(
                System.IO.Path.Combine(EngineRoot, "status", "heartbeat.json"),
                "{\"protocolVersion\":2,\"engineId\":\"unity-editor\",\"sessionId\":\"session\",\"generation\":1,\"mode\":\"EditMode\",\"sequence\":1,\"createdAtUtc\":\""
                + DateTimeOffset.UtcNow.ToString("O")
                + "\"}");
        }

        /// <summary>写入与 heartbeat generation 一致的 System snapshot。</summary>
        public void WriteSnapshot()
        {
            var snapshotRoot = System.IO.Path.Combine(EngineRoot, "snapshots", "System");
            Directory.CreateDirectory(snapshotRoot);
            File.WriteAllText(
                System.IO.Path.Combine(snapshotRoot, "state.json"),
                "{\"protocolVersion\":2,\"engineId\":\"unity-editor\",\"kit\":\"System\",\"name\":\"state\",\"generation\":1,\"sequence\":1,\"writtenAtUtc\":\""
                + DateTimeOffset.UtcNow.ToString("O")
                + "\",\"payloadJson\":\"{\\\"status\\\":\\\"online\\\"}\"}");
        }

        /// <summary>清理测试创建的临时项目根。</summary>
        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, true);
            }
        }

        /// <summary>转义 Windows 路径反斜杠，避免测试 JSON 无效。</summary>
        /// <param name="text">待转义文本。</param>
        /// <returns>可放入 JSON 字符串的文本。</returns>
        private static string Escape(string text) => text.Replace("\\", "\\\\", StringComparison.Ordinal);
    }
}
