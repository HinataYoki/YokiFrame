using System.Diagnostics;
using System.Text.Json.Nodes;

namespace YokiFrame.Cli.Tests;

/// <summary>
/// 覆盖 yoki exec：从 stdin 读 NDJSON 步骤、进程内顺序执行、失败停止与 continue-on-error。
/// </summary>
/// <remarks>
/// 用例自带进程与 stdin，不依赖任何在线 engine，因此可以稳定断言步骤编排行为本身。
/// </remarks>
public sealed class CliExecCommandsTests
{
    [Fact]
    public async Task Review_checklist_smoke_script_is_valid_ndjson()
    {
        // 审查清单 §2.0 里那段 Unity 冒烟脚本；这里锁定"它是合法步骤流"，
        // 防止文档里的示例随 schema 演进而失效（没有在线 engine 时会在第一步失败，那是预期的）。
        const string script = """
{"command":["command","send","RoslynKit","RoslynKit","--action","domain_state"],"expect":{"contains":"engineKind"}}
{"command":["command","send","RoslynKit","RoslynKit","--action","engine_capabilities"],"expect":{"contains":"scene_query"}}
{"command":["command","send","RoslynKit","RoslynKit","--action","scene_query","--payload","{\"path\":\"/\",\"depth\":2}"],"expect":{"contains":"rootCount"}}
{"command":["command","send","RoslynKit","RoslynKit","--action","entry_list"],"expect":{"contains":"entries"}}
{"command":["command","send","RoslynKit","RoslynKit","--action","eval","--payload","{\"confirmed\":true,\"id\":\"smoke1\",\"code\":\"ctx.Log(\\\"smoke\\\");\"}"],"retry":{"attempts":5,"delayMs":1000},"expect":{"contains":"accepted"}}
{"command":["command","send","RoslynKit","RoslynKit","--action","eval_result","--payload","{\"id\":\"smoke1\"}"],"retry":{"attempts":30,"delayMs":2000},"expect":{"contains":"Ready"}}
{"command":["command","send","RoslynKit","RoslynKit","--action","eval_prune","--payload","{}"],"expect":{"contains":"removedRecords"}}
""";

        (int exitCode, string output) = await RunExecAsync(script);

        // 步骤流必须被真正解析并开始执行：不能出现"输入缺失/步骤非法/超步数"这类结构化拒绝
        Assert.DoesNotContain("ExecInputMissing", output, StringComparison.Ordinal);
        Assert.DoesNotContain("ExecStepInvalid", output, StringComparison.Ordinal);
        Assert.DoesNotContain("ExecStepLimit", output, StringComparison.Ordinal);
        Assert.Contains("\"step\":1", output, StringComparison.Ordinal);

        // 退出码取决于"当前是否有在线 engine"，因此只断言步骤流合法，不断言成败。
        Assert.True(exitCode == 0 || exitCode == 1);
    }

    private static async Task<(int ExitCode, string StandardOutput)> RunExecAsync(
        string standardInput,
        params string[] extraArguments)
    {
        ProcessStartInfo startInfo = new("dotnet")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        startInfo.ArgumentList.Add(CliTestHelpers.GetCliAssemblyPath());
        startInfo.ArgumentList.Add("exec");
        foreach (string argument in extraArguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start YokiFrame.Cli process.");
        await process.StandardInput.WriteAsync(standardInput);
        process.StandardInput.Close();
        Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
        Task<string> errorTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        await errorTask;
        return (process.ExitCode, await outputTask);
    }

    private static JsonArray ReadSteps(string standardOutput)
    {
        var steps = new JsonArray();
        foreach (string line in standardOutput.Split('\n'))
        {
            string trimmed = line.Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }

            if (JsonNode.Parse(trimmed) is JsonObject node
                && node["step"] != null)
            {
                steps.Add(node);
            }
        }

        return steps;
    }

    private static JsonObject ReadSummary(string standardOutput)
    {
        JsonObject? summary = null;
        foreach (string line in standardOutput.Split('\n'))
        {
            string trimmed = line.Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }

            if (JsonNode.Parse(trimmed) is JsonObject node && node["steps"] != null)
            {
                summary = node;
            }
        }

        return summary ?? throw new InvalidOperationException("exec summary line is missing.");
    }

    [Fact]
    public async Task Wait_steps_run_and_summary_reports_success()
    {
        var result = await RunExecAsync("{\"wait\":5}\n{\"wait\":5}\n");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(2, ReadSteps(result.StandardOutput).Count);
        JsonObject summary = ReadSummary(result.StandardOutput);
        Assert.Equal(2, summary["steps"]!.GetValue<int>());
        Assert.Equal(0, summary["failed"]!.GetValue<int>());
        Assert.True(summary["ok"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Failing_step_is_reported_and_stops_by_default()
    {
        string projectRoot = Path.Combine(Path.GetTempPath(), "yokiframe-exec-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(projectRoot);
        try
        {
            string script =
                "{\"command\":[\"engine\",\"list\"]}\n" +
                "{\"wait\":5}\n";

            var result = await RunExecAsync(script, "--project", projectRoot);

            Assert.Equal(1, result.ExitCode);
            JsonArray steps = ReadSteps(result.StandardOutput);
            Assert.Single(steps);
            Assert.False(steps[0]!["ok"]!.GetValue<bool>());
            Assert.Equal("ExecStepFailed", steps[0]!["errorCode"]!.GetValue<string>());
            Assert.Equal(1, ReadSummary(result.StandardOutput)["failed"]!.GetValue<int>());
        }
        finally
        {
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public async Task Continue_on_error_keeps_running_remaining_steps()
    {
        string projectRoot = Path.Combine(Path.GetTempPath(), "yokiframe-exec-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(projectRoot);
        try
        {
            string script =
                "{\"command\":[\"engine\",\"list\"]}\n" +
                "{\"wait\":5}\n";

            var result = await RunExecAsync(script, "--project", projectRoot, "--continue-on-error");

            Assert.Equal(1, result.ExitCode);
            Assert.Equal(2, ReadSteps(result.StandardOutput).Count);
            Assert.Equal(1, ReadSummary(result.StandardOutput)["failed"]!.GetValue<int>());
        }
        finally
        {
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public async Task Nested_exec_step_is_rejected()
    {
        var result = await RunExecAsync("{\"command\":[\"exec\"]}\n");

        Assert.Equal(1, result.ExitCode);
        JsonArray steps = ReadSteps(result.StandardOutput);
        Assert.Single(steps);
        Assert.Equal("ExecStepInvalid", steps[0]!["errorCode"]!.GetValue<string>());
    }

    [Fact]
    public async Task Invalid_step_json_is_rejected()
    {
        var result = await RunExecAsync("not-json\n");

        Assert.Equal(1, result.ExitCode);
        JsonArray steps = ReadSteps(result.StandardOutput);
        Assert.Single(steps);
        Assert.Equal("ExecStepInvalid", steps[0]!["errorCode"]!.GetValue<string>());
    }

    [Fact]
    public async Task Expectation_failure_keeps_step_failed()
    {
        string projectRoot = Path.Combine(Path.GetTempPath(), "yokiframe-exec-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(projectRoot);
        try
        {
            string script =
                "{\"command\":[\"engine\",\"list\"],\"expect\":{\"contains\":\"never-matches\"}}\n";

            var result = await RunExecAsync(script, "--project", projectRoot);

            Assert.Equal(1, result.ExitCode);
            Assert.False(ReadSteps(result.StandardOutput)[0]!["ok"]!.GetValue<bool>());
        }
        finally
        {
            Directory.Delete(projectRoot, recursive: true);
        }
    }
}
