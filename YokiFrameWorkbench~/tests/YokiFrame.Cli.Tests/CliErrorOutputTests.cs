using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using YokiFrame;
using YokiFrame.Protocol.FileBridge;
using YokiFrame.Protocol.Results;

namespace YokiFrame.Cli.Tests;

/// <summary>
/// 覆盖 CLI 失败输出契约，确保脚本和 AI 可以稳定解析错误 JSON。
/// </summary>
public sealed class CliErrorOutputTests
{
    private static readonly object sConsoleErrorGate = new();

    /// <summary>
    /// 验证未知命令返回标准 error 对象，而不是普通文本异常。
    /// </summary>
    [Fact]
    public async Task UnknownCommandWritesStandardErrorJson()
    {
        using var projectRoot = CliTestProjectRoot.Create();
        var result = await RunCliAsync("unknown", "command", "--project", projectRoot.Path);

        Assert.Equal(1, result.ExitCode);
        Assert.Equal(string.Empty, result.StandardOutput.Trim());
        AssertError(result.StandardError, "UnknownCommand");
    }

    /// <summary>
    /// 验证非法 payload JSON 会在写入命令前被拒绝，并返回标准错误结构。
    /// </summary>
    [Fact]
    public async Task InvalidPayloadWritesStandardErrorJson()
    {
        using var projectRoot = CliTestProjectRoot.Create();
        var result = await RunCliAsync(
            "command",
            "send",
            "--project",
            projectRoot.Path,
            "--engine",
            "unity-editor",
            "--kit",
            "System",
            "--action",
            "ping",
            "--payload",
            "{bad",
            "--timeout",
            "5000");

        Assert.Equal(1, result.ExitCode);
        Assert.Equal(string.Empty, result.StandardOutput.Trim());
        AssertError(result.StandardError, "InvalidPayloadJson");
    }

    /// <summary>
    /// 验证命令级 schema 会拒绝拼写错误选项，而不是把它静默传给业务模块。
    /// </summary>
    [Fact]
    public async Task UnknownOptionWritesStandardErrorJson()
    {
        using var projectRoot = CliTestProjectRoot.Create();
        var result = await RunCliAsync(
            "engine",
            "list",
            "--project",
            projectRoot.Path,
            "--proejct",
            projectRoot.Path);

        Assert.Equal(1, result.ExitCode);
        Assert.Equal(string.Empty, result.StandardOutput.Trim());
        AssertError(result.StandardError, "UnknownOption");
    }

    /// <summary>
    /// 验证重复选项不会覆盖先前值而继续执行，避免脚本输入产生隐含歧义。
    /// </summary>
    [Fact]
    public async Task DuplicateOptionWritesStandardErrorJson()
    {
        using var projectRoot = CliTestProjectRoot.Create();
        var result = await RunCliAsync(
            "engine",
            "list",
            "--project",
            projectRoot.Path,
            "--project",
            projectRoot.Path);

        Assert.Equal(1, result.ExitCode);
        AssertError(result.StandardError, "DuplicateOption");
    }

    /// <summary>
    /// 验证 command status 只读查询会把 pending 证据和 requestId 投影到机器输出。
    /// </summary>
    [Fact]
    public async Task CommandStatusWritesPendingEvidenceJson()
    {
        using var projectRoot = CliTestProjectRoot.Create();
        var commandRoot = Path.Combine(
            projectRoot.Path,
            YokiFrameFileBridgeLayout.YOKIFRAME_DIRECTORY,
            YokiFrameFileBridgeLayout.ENGINES_DIRECTORY,
            "unity-editor",
            YokiFrameFileBridgeLayout.COMMANDS_DIRECTORY);
        var requestId = "cli-status-pending";
        var pendingPath = Path.Combine(commandRoot, requestId + ".json");
        Directory.CreateDirectory(commandRoot);
        await File.WriteAllTextAsync(pendingPath, "{}");

        var result = await RunCliAsync(
            "command",
            "status",
            "--project",
            projectRoot.Path,
            "--engine",
            "unity-editor",
            "--request-id",
            requestId);

        var json = JsonNode.Parse(result.StandardOutput)
            ?? throw new InvalidOperationException("CLI stdout is not JSON.");
        Assert.Equal(0, result.ExitCode);
        Assert.Equal("Pending", json["state"]?.GetValue<string>());
        Assert.Equal(requestId, json["requestId"]?.GetValue<string>());
        Assert.Contains(
            pendingPath,
            json["evidencePaths"]!.AsArray().Select(static node => node!.GetValue<string>()));
    }

    /// <summary>
    /// 验证非法整数不会静默回落到默认超时值，也不会写入 FileBridge command。
    /// </summary>
    [Fact]
    public async Task InvalidTimeoutWritesStandardErrorJson()
    {
        using var projectRoot = CliTestProjectRoot.Create();
        var result = await RunCliAsync(
            "command",
            "send",
            "--project",
            projectRoot.Path,
            "--engine",
            "unity-editor",
            "--timeout",
            "abc");

        Assert.Equal(1, result.ExitCode);
        Assert.Equal(string.Empty, result.StandardOutput.Trim());
        AssertError(result.StandardError, "InvalidOptionValue");
    }

    /// <summary>
    /// 验证 FileBridge 等待超时会输出 Unknown，而不是让脚本误以为 Runtime 已明确失败。
    /// </summary>
    [Fact]
    public async Task CommandTimeoutWritesUnknownOutcomeJson()
    {
        using var projectRoot = CliTestProjectRoot.Create();
        var result = await RunCliAsync(
            "command",
            "send",
            "--project",
            projectRoot.Path,
            "--engine",
            "unity-editor",
            "--timeout",
            "1000");

        var json = JsonNode.Parse(result.StandardError)
            ?? throw new InvalidOperationException("CLI stderr is not JSON.");
        Assert.Equal(1, result.ExitCode);
        Assert.Equal("CommandTimeout", json["error"]!["code"]!.GetValue<string>());
        Assert.Equal("Unknown", json["outcome"]!.GetValue<string>());
    }

    /// <summary>
    /// 验证 CLI schema 与 FileBridge CommandPolicy 使用同一 timeout 范围。
    /// </summary>
    [Fact]
    public async Task TimeoutOutsideProtocolRangeIsRejectedBeforeClientCreation()
    {
        using var projectRoot = CliTestProjectRoot.Create();
        var result = await RunCliAsync(
            "command",
            "send",
            "--project",
            projectRoot.Path,
            "--engine",
            "unity-editor",
            "--timeout",
            "500");

        Assert.Equal(1, result.ExitCode);
        AssertError(result.StandardError, "OptionOutOfRange");
    }

    /// <summary>
    /// 验证 Runtime 已返回 status=Error 时，CLI 会输出标准失败 JSON 而不是成功 JSON。
    /// </summary>
    [Fact]
    public async Task RuntimeErrorResponseWritesStandardErrorJson()
    {
        using var projectRoot = CliTestProjectRoot.Create();
        var execution = await RunCliWithRuntimeErrorAsync(projectRoot.Path);

        AssertRuntimeError(execution);
    }

    /// <summary>
    /// 验证命令上下文中的 outcome 不能覆盖由错误码推导出的 Unknown 结果。
    /// </summary>
    [Fact]
    public async Task RuntimeTimeoutResponseKeepsDerivedUnknownOutcome()
    {
        using var projectRoot = CliTestProjectRoot.Create();
        var execution = await RunCliWithRuntimeErrorAsync(projectRoot.Path, "CommandTimeout");
        var json = JsonNode.Parse(execution.Result.StandardError)
            ?? throw new InvalidOperationException("CLI stderr is not JSON.");

        Assert.Equal(1, execution.Result.ExitCode);
        Assert.Equal("CommandTimeout", json["error"]!["code"]!.GetValue<string>());
        Assert.Equal("Unknown", json["outcome"]!.GetValue<string>());
    }

    /// <summary>
    /// 验证错误上下文不能覆盖标准 envelope 的保留字段，而普通上下文仍会保留。
    /// </summary>
    [Fact]
    public void ErrorContextCannotOverwriteReservedEnvelopeFields()
    {
        YokiFrameError error = new(
            "CommandTimeout",
            "The command timed out.",
            "Inspect the request evidence before retrying.",
            new[] { "error-evidence.json" },
            "error-request",
            "error-engine",
            "error-transport");
        JsonObject context = new()
        {
            ["ok"] = true,
            ["error"] = new JsonObject { ["code"] = "ContextError" },
            ["outcome"] = "Succeeded",
            ["requestId"] = "context-request",
            ["engineId"] = "context-engine",
            ["transport"] = "context-transport",
            ["evidencePaths"] = new JsonArray("context-evidence.json"),
            ["warnings"] = new JsonArray("context-warning"),
            ["custom"] = "preserved"
        };

        string output;
        lock (sConsoleErrorGate)
        {
            TextWriter originalError = Console.Error;
            using StringWriter capturedError = new();
            Console.SetError(capturedError);
            try
            {
                Assert.Equal(1, CliJsonOutput.WriteError(error, context));
                output = capturedError.ToString();
            }
            finally
            {
                Console.SetError(originalError);
            }
        }

        JsonObject json = JsonNode.Parse(output)?.AsObject()
            ?? throw new InvalidOperationException("CLI stderr is not JSON.");
        Assert.False(json["ok"]!.GetValue<bool>());
        Assert.Equal("CommandTimeout", json["error"]!["code"]!.GetValue<string>());
        Assert.Equal("Unknown", json["outcome"]!.GetValue<string>());
        Assert.Equal("error-request", json["requestId"]!.GetValue<string>());
        Assert.Equal("error-engine", json["engineId"]!.GetValue<string>());
        Assert.Equal("error-transport", json["transport"]!.GetValue<string>());
        Assert.Equal("error-evidence.json", json["error"]!["evidencePaths"]![0]!.GetValue<string>());
        Assert.False(json.ContainsKey("evidencePaths"));
        Assert.False(json.ContainsKey("warnings"));
        Assert.Equal("preserved", json["custom"]!.GetValue<string>());
    }

    /// <summary>
    /// 启动 CLI、接管 pending command 并写入宿主错误 terminal response。
    /// </summary>
    /// <param name="projectRoot">测试项目根目录。</param>
    /// <param name="errorCode">宿主返回的错误码。</param>
    /// <returns>CLI 输出及其对应的命令证据。</returns>
    private static async Task<RuntimeErrorExecution> RunCliWithRuntimeErrorAsync(
        string projectRoot,
        string errorCode = "HostRejected")
    {
        using var process = StartCli(
            "command", "send", "--project", projectRoot, "--engine", "unity-editor",
            "--kit", "System", "--action", "list_commands", "--timeout", "5000");
        var standardOutputTask = process.StandardOutput.ReadToEndAsync();
        var standardErrorTask = process.StandardError.ReadToEndAsync();

        try
        {
            var commandPath = await WaitForPendingCommandAsync(projectRoot, "unity-editor");
            var envelope = CommandEnvelope.FromJson(await File.ReadAllTextAsync(commandPath));
            var responsePath = GetResponsePath(projectRoot, envelope);
            await WriteRuntimeErrorResponseAsync(responsePath, envelope, errorCode);
            await process.WaitForExitAsync();
            var result = new CliProcessResult(
                process.ExitCode,
                await standardOutputTask,
                await standardErrorTask);
            return new RuntimeErrorExecution(result, envelope, commandPath, responsePath);
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
            }
        }
    }

    /// <summary>
    /// 写入与命令信封关联的宿主错误 terminal response。
    /// </summary>
    /// <param name="responsePath">response 文件路径。</param>
    /// <param name="envelope">待关联的命令信封。</param>
    /// <param name="errorCode">宿主返回的错误码。</param>
    private static async Task WriteRuntimeErrorResponseAsync(
        string responsePath,
        CommandEnvelope envelope,
        string errorCode)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(responsePath)!);
        CommandResponse response = new()
        {
            ProtocolVersion = envelope.ProtocolVersion,
            RequestId = envelope.RequestId,
            EngineId = envelope.EngineId,
            Status = "Error",
            ResultJson = "{}",
            ErrorCode = errorCode,
            ErrorMessage = "The host rejected this command.",
            CompletedAtUtc = DateTimeOffset.UtcNow.ToString("O")
        };
        await File.WriteAllTextAsync(responsePath, JsonSerializer.Serialize(response));
    }

    /// <summary>
    /// 断言宿主错误已被 CLI 投影为标准失败输出，并保留响应上下文和 evidence。
    /// </summary>
    /// <param name="execution">CLI 与宿主错误执行结果。</param>
    private static void AssertRuntimeError(RuntimeErrorExecution execution)
    {
        var result = execution.Result;
        var json = JsonNode.Parse(result.StandardError)
            ?? throw new InvalidOperationException("CLI stderr is not JSON.");

        Assert.Equal(1, result.ExitCode);
        Assert.Equal(string.Empty, result.StandardOutput.Trim());
        Assert.False(json["ok"]!.GetValue<bool>());
        Assert.Equal("HostRejected", json["error"]!["code"]!.GetValue<string>());
        Assert.Equal("The host rejected this command.", json["error"]!["message"]!.GetValue<string>());
        Assert.Equal("file-bridge", json["transport"]!.GetValue<string>());
        Assert.Equal(execution.Envelope.RequestId, json["requestId"]!.GetValue<string>());
        Assert.Equal(execution.CommandPath, json["commandPath"]!.GetValue<string>());
        Assert.Equal(execution.ResponsePath, json["responsePath"]!.GetValue<string>());
        Assert.Equal("Error", json["response"]!["status"]!.GetValue<string>());
        var evidencePaths = json["error"]!["evidencePaths"]!.AsArray()
            .Select(static node => node!.GetValue<string>());
        Assert.Contains(execution.CommandPath, evidencePaths);
        Assert.Contains(execution.ResponsePath, evidencePaths);
    }

    /// <summary>启动真实 CLI 程序并捕获标准输出和标准错误。</summary>
    /// <param name="arguments">传递给 CLI 的参数列表。</param>
    /// <returns>进程退出结果。</returns>
    private static Task<CliProcessResult> RunCliAsync(params string[] arguments)
        => CliTestHelpers.RunCliAsync(arguments);

    /// <summary>
    /// 启动真实 CLI 子进程，供测试在 Runtime 写入 terminal response 前接管命令队列。
    /// </summary>
    /// <param name="arguments">传递给 CLI 的参数列表。</param>
    /// <returns>已启动的 CLI 进程。</returns>
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

    /// <summary>
    /// 等待 CLI 原子写入指定 engine 的 pending command 文件。
    /// </summary>
    /// <param name="projectRoot">测试项目根目录。</param>
    /// <param name="engineId">目标 engine 标识。</param>
    /// <returns>已完成写入的 command 文件路径。</returns>
    private static Task<string> WaitForPendingCommandAsync(string projectRoot, string engineId)
    {
        var commandsRoot = Path.Combine(
            projectRoot,
            YokiFrameFileBridgeLayout.YOKIFRAME_DIRECTORY,
            YokiFrameFileBridgeLayout.ENGINES_DIRECTORY,
            engineId,
            YokiFrameFileBridgeLayout.COMMANDS_DIRECTORY);
        return FileBridgeTestHelpers.WaitForSingleCommandAsync(commandsRoot);
    }

    /// <summary>
    /// 根据命令信封计算 terminal response 文件路径。
    /// </summary>
    /// <param name="projectRoot">测试项目根目录。</param>
    /// <param name="envelope">已读取的命令信封。</param>
    /// <returns>预期 response 文件路径。</returns>
    private static string GetResponsePath(string projectRoot, CommandEnvelope envelope)
    {
        return Path.Combine(
            projectRoot,
            YokiFrameFileBridgeLayout.YOKIFRAME_DIRECTORY,
            YokiFrameFileBridgeLayout.ENGINES_DIRECTORY,
            envelope.EngineId,
            YokiFrameFileBridgeLayout.RESULTS_DIRECTORY,
            envelope.RequestId + YokiFrameFileBridgeLayout.RESPONSE_FILE_SUFFIX);
    }

    /// <summary>根据测试输出目录定位同一 solution 构建出的 CLI 程序。</summary>
    /// <returns>CLI 程序 DLL 路径。</returns>
    private static string GetCliAssemblyPath()
        => CliTestHelpers.GetCliAssemblyPath();

    /// <summary>
    /// 断言 CLI stderr 是 compact JSON，并包含标准错误字段。
    /// </summary>
    /// <param name="standardError">CLI 标准错误文本。</param>
    /// <param name="expectedCode">预期错误码。</param>
    private static void AssertError(string standardError, string expectedCode)
    {
        var json = JsonNode.Parse(standardError)
            ?? throw new InvalidOperationException("CLI stderr is not JSON.");
        var error = json["error"]
            ?? throw new InvalidOperationException("CLI stderr does not contain error object.");

        Assert.False(json["ok"]!.GetValue<bool>());
        Assert.Equal(expectedCode, error["code"]!.GetValue<string>());
        Assert.False(string.IsNullOrWhiteSpace(error["message"]!.GetValue<string>()));
        Assert.False(string.IsNullOrWhiteSpace(error["suggestion"]!.GetValue<string>()));
        Assert.NotNull(error["evidencePaths"]!.AsArray());
    }

    /// <summary>
    /// <summary>
    /// 保存 CLI 宿主错误测试所需的响应和证据路径。
    /// </summary>
    /// <param name="Result">CLI 进程结果。</param>
    /// <param name="Envelope">CLI 写入的命令信封。</param>
    /// <param name="CommandPath">命令文件路径。</param>
    /// <param name="ResponsePath">响应文件路径。</param>
    private sealed record RuntimeErrorExecution(
        CliProcessResult Result,
        CommandEnvelope Envelope,
        string CommandPath,
        string ResponsePath);

    /// <summary>
    /// 为 CLI 测试创建临时项目根目录，并在测试结束后清理。
    /// </summary>
    private sealed class CliTestProjectRoot : IDisposable
    {
        /// <summary>
        /// 创建临时项目根目录。
        /// </summary>
        private CliTestProjectRoot()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "yokiframe-cli-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        /// <summary>
        /// 获取临时项目根路径。
        /// </summary>
        public string Path { get; }

        /// <summary>
        /// 创建新的临时项目根。
        /// </summary>
        /// <returns>临时项目根实例。</returns>
        public static CliTestProjectRoot Create()
        {
            return new CliTestProjectRoot();
        }

        /// <summary>
        /// 清理测试创建的临时项目根目录。
        /// </summary>
        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, true);
            }
        }
    }
}
