using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using YokiFrame.Client;
using YokiFrame.Protocol.FileBridge;
using YokiFrame.Tooling.Application.ProjectModel;

namespace YokiFrame.Cli.Tests;

/// <summary>
/// 覆盖 harness catalog 的 CLI 边界，验证静态读取、显式 refresh 和 strict 解析语义。
/// </summary>
public sealed class CliHarnessCatalogTests
{
    /// <summary>
    /// 验证默认 catalog 不发送命令，只返回静态声明和 NotRequested 命令目录。
    /// </summary>
    [Fact]
    public async Task CatalogWithoutRefreshDoesNotWriteCommand()
    {
        using var project = CatalogProject.Create();
        var result = await RunCliAsync("harness", "catalog", "--project", project.Path);

        var json = AssertSuccess(result);
        Assert.Equal("Ready", json["state"]!.GetValue<string>());
        Assert.Equal("NotRequested", json["catalog"]!["engines"]![0]!["commandCatalog"]!["state"]!.GetValue<string>());
        Assert.Empty(Directory.EnumerateFiles(project.CommandsRoot, "*.json"));
    }

    /// <summary>
    /// 验证显式 refresh 会读取 Runtime terminal response，并将当前命令目录投影为 Observed。
    /// </summary>
    [Fact]
    public async Task CatalogRefreshReadsCurrentCommandCatalog()
    {
        using var project = CatalogProject.Create();
        using var process = StartCli(
            "harness", "catalog", "--refresh-commands", "--engine", "unity-editor", "--timeout", "5000",
            "--project", project.Path);
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

        var commandPath = await WaitForCommandAsync(project.CommandsRoot);
        var envelope = CommandEnvelope.FromJson(await File.ReadAllTextAsync(commandPath));
        var responsePath = Path.Combine(project.ResultsRoot, envelope.RequestId + YokiFrameFileBridgeLayout.RESPONSE_FILE_SUFFIX);
        var response = new CommandResponse
        {
            ProtocolVersion = envelope.ProtocolVersion,
            RequestId = envelope.RequestId,
            EngineId = envelope.EngineId,
            Status = "Success",
            ResultJson = project.CommandCatalogJson,
            CompletedAtUtc = DateTimeOffset.UtcNow.ToString("O")
        };
        await File.WriteAllTextAsync(responsePath, JsonSerializer.Serialize(response));

        await process.WaitForExitAsync();
        var result = new CliProcessResult(process.ExitCode, await outputTask, await errorTask);
        var json = AssertSuccess(result);
        Assert.Equal("Drifted", json["state"]!.GetValue<string>());
        Assert.Equal("Observed", json["catalog"]!["engines"]![0]!["commandCatalog"]!["state"]!.GetValue<string>());
        Assert.Equal("ping", json["catalog"]!["engines"]![0]!["commandCatalog"]!["commands"]![0]!["action"]!.GetValue<string>());
    }

    /// <summary>
    /// 验证 strict 在无 engine 事实时返回 stderr、非零退出码和结构化失败码。
    /// </summary>
    [Fact]
    public async Task CatalogStrictFailsWhenNoEngineIsAvailable()
    {
        using var project = CatalogProject.Create(withEngine: false);
        var result = await RunCliAsync("harness", "catalog", "--strict", "--project", project.Path);

        Assert.Equal(1, result.ExitCode);
        Assert.Empty(result.StandardOutput.Trim());
        var json = JsonNode.Parse(result.StandardError) ?? throw new InvalidOperationException("CLI stderr is not JSON.");
        Assert.False(json["ok"]!.GetValue<bool>());
        Assert.Equal("CapabilityCatalogNotReady", json["error"]!["code"]!.GetValue<string>());
        Assert.Equal("Blocked", json["state"]!.GetValue<string>());
    }

    /// <summary>
    /// 验证在线 engine 不能掩盖缺失 Project Model，非 strict 目录必须显式返回 Partial。
    /// </summary>
    [Fact]
    public async Task CatalogReportsMissingProjectModelAsPartial()
    {
        using var project = CatalogProject.Create(withProjectModel: false);
        var result = await RunCliAsync("harness", "catalog", "--project", project.Path);

        var json = AssertSuccess(result);
        Assert.Equal("Partial", json["state"]!.GetValue<string>());
        Assert.Equal("Missing", json["catalog"]!["project"]!["modelState"]!.GetValue<string>());

        var strictResult = await RunCliAsync("harness", "catalog", "--strict", "--project", project.Path);
        Assert.Equal(1, strictResult.ExitCode);
        Assert.Contains("CapabilityCatalogNotReady", strictResult.StandardError, StringComparison.Ordinal);
    }

    /// <summary>
    /// 验证显式布尔值只接受 true/false，避免脚本误把任意文本当成开关。
    /// </summary>
    [Theory]
    [InlineData("--refresh-commands=maybe")]
    [InlineData("--refresh-commands=")]
    [InlineData("--strict=")]
    public async Task CatalogRejectsInvalidBooleanOption(string option)
    {
        var result = await RunCliAsync(
            "harness", "catalog", option, "--project", Path.GetTempPath());

        Assert.Equal(1, result.ExitCode);
        Assert.Empty(result.StandardOutput.Trim());
        var json = JsonNode.Parse(result.StandardError) ?? throw new InvalidOperationException("CLI stderr is not JSON.");
        Assert.Equal("InvalidOptionValue", json["error"]!["code"]!.GetValue<string>());
    }

    /// <summary>启动真实 CLI 子进程。</summary>
    /// <param name="arguments">CLI 参数。</param>
    /// <returns>已启动进程。</returns>
    private static Process StartCli(params string[] arguments)
    {
        ProcessStartInfo startInfo = new("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        startInfo.ArgumentList.Add(GetCliAssemblyPath());
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start YokiFrame.Cli process.");
    }

    /// <summary>运行 CLI 并等待进程结束。</summary>
    /// <param name="arguments">CLI 参数。</param>
    /// <returns>进程输出。</returns>
    private static Task<CliProcessResult> RunCliAsync(params string[] arguments)
        => CliTestHelpers.RunCliAsync(arguments);

    /// <summary>等待 CLI 写出唯一 pending command。</summary>
    /// <param name="commandsRoot">commands 目录。</param>
    /// <returns>命令文件路径。</returns>
    private static Task<string> WaitForCommandAsync(string commandsRoot)
        => FileBridgeTestHelpers.WaitForSingleCommandAsync(commandsRoot);

    /// <summary>断言 CLI 成功 JSON。</summary>
    /// <param name="result">CLI 输出。</param>
    /// <returns>解析后的 JSON。</returns>
    private static JsonNode AssertSuccess(CliProcessResult result)
    {
        Assert.Equal(0, result.ExitCode);
        Assert.Empty(result.StandardError.Trim());
        var json = JsonNode.Parse(result.StandardOutput) ?? throw new InvalidOperationException("CLI stdout is not JSON.");
        Assert.True(json["ok"]!.GetValue<bool>());
        return json;
    }

    /// <summary>定位测试构建得到的 CLI 程序。</summary>
    /// <returns>CLI 程序 DLL 路径。</returns>
    private static string GetCliAssemblyPath()
        => CliTestHelpers.GetCliAssemblyPath();

    /// <summary>创建最小可运行 catalog 项目。</summary>
    private sealed class CatalogProject : IDisposable
    {
        private const string ENGINE_ID = "unity-editor";

        /// <summary>
        /// 创建带有效 harness，以及可选 registry/heartbeat 的临时项目。
        /// </summary>
        /// <param name="withEngine">是否写入可在线识别的 engine 事实。</param>
        /// <param name="withProjectModel">是否生成有效 Project Model 五文件。</param>
        private CatalogProject(bool withEngine, bool withProjectModel)
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "yokiframe-cli-catalog-tests", Guid.NewGuid().ToString("N"));
            HarnessPath = System.IO.Path.Combine(Path, ".yokiframe", "harness", "capabilities.json");
            EngineRoot = System.IO.Path.Combine(Path, ".yokiframe", "engines", ENGINE_ID);
            CommandsRoot = System.IO.Path.Combine(EngineRoot, "commands");
            ResultsRoot = System.IO.Path.Combine(EngineRoot, "results");
            CreateUnityProject();
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(HarnessPath)!);
            File.WriteAllText(HarnessPath, """
                {"schemaVersion":1,"generatedAtUtc":"2026-07-12T00:00:00.0000000Z","package":{"name":"com.hinatayoki.yokiframe","version":"test","packageRoot":"Assets/YokiFrame"},"protocol":{"fileBridgeVersion":2,"sharedMemoryTelemetryVersion":1,"fastChannelVersion":1},"engines":{"knownKinds":["Unity"]},"kits":{"snapshots":["System"],"commands":["System"]}}
                """);
            CommandCatalogJson = "{\"engineId\":\"unity-editor\",\"sessionId\":\"session\",\"generation\":1,\"sequence\":1,\"kits\":[{\"kit\":\"System\",\"actions\":[{\"action\":\"ping\",\"kind\":\"ReadOnly\"}]}]}";
            if (withProjectModel)
            {
                var model = new ProjectModelService(new YokiFrameClient(Path)).Refresh();
                if (!model.IsReady)
                {
                    throw new InvalidOperationException("Failed to create CLI Project Model fixture: "
                        + string.Join("; ", model.Issues.Select(issue => issue.Code)));
                }
            }
            if (withEngine)
            {
                Directory.CreateDirectory(CommandsRoot);
                Directory.CreateDirectory(ResultsRoot);
                Directory.CreateDirectory(System.IO.Path.Combine(EngineRoot, "status"));
                File.WriteAllText(System.IO.Path.Combine(EngineRoot, "engine.json"), "{\"protocolVersion\":2,\"engineId\":\"unity-editor\",\"engine\":\"Unity\",\"version\":\"test\",\"adapterVersion\":\"test\",\"sessionId\":\"session\",\"generation\":1,\"mode\":\"EditMode\",\"capabilities\":[\"command.send\"]}");
                File.WriteAllText(System.IO.Path.Combine(EngineRoot, "status", "heartbeat.json"), "{\"protocolVersion\":2,\"engineId\":\"unity-editor\",\"sessionId\":\"session\",\"generation\":1,\"mode\":\"EditMode\",\"sequence\":1,\"createdAtUtc\":\"" + DateTimeOffset.UtcNow.ToString("O") + "\"}");
            }
        }

        /// <summary>获取临时项目根路径。</summary>
        public string Path { get; }

        /// <summary>获取 harness 文件路径。</summary>
        public string HarnessPath { get; }

        /// <summary>获取测试 engine 根路径。</summary>
        public string EngineRoot { get; }

        /// <summary>获取 pending command 目录。</summary>
        public string CommandsRoot { get; }

        /// <summary>获取 terminal response 目录。</summary>
        public string ResultsRoot { get; }

        /// <summary>获取 Runtime 返回的命令目录 JSON。</summary>
        public string CommandCatalogJson { get; }

        /// <summary>创建扫描器要求的最小 Unity 项目和本地 YokiFrame 包。</summary>
        private void CreateUnityProject()
        {
            Directory.CreateDirectory(System.IO.Path.Combine(Path, "Assets"));
            Directory.CreateDirectory(System.IO.Path.Combine(Path, "Packages"));
            Directory.CreateDirectory(System.IO.Path.Combine(Path, "ProjectSettings"));
            Directory.CreateDirectory(System.IO.Path.Combine(Path, "Assets", "YokiFrame"));
            File.WriteAllText(
                System.IO.Path.Combine(Path, "Packages", "manifest.json"),
                "{\"dependencies\":{}}\n");
            File.WriteAllText(
                System.IO.Path.Combine(Path, "ProjectSettings", "ProjectVersion.txt"),
                "m_EditorVersion: 2022.3.0f1\n");
            File.WriteAllText(
                System.IO.Path.Combine(Path, "Assets", "YokiFrame", "package.json"),
                "{\"name\":\"com.hinatayoki.yokiframe\",\"version\":\"test\"}\n");
        }

        /// <summary>创建临时 catalog 测试项目。</summary>
        /// <param name="withEngine">是否写入 engine registry/heartbeat。</param>
        /// <param name="withProjectModel">是否生成有效 Project Model 五文件。</param>
        /// <returns>临时项目实例。</returns>
        public static CatalogProject Create(bool withEngine = true, bool withProjectModel = true)
        {
            return new CatalogProject(withEngine, withProjectModel);
        }

        /// <summary>清理测试创建的临时项目目录。</summary>
        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, true);
            }
        }
    }
}
