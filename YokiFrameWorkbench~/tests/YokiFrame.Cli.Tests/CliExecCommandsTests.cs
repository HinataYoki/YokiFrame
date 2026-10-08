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

    /// <summary>验证命令本身成功但输出不含 expect 时，步骤仍失败。</summary>
    [Fact]
    public async Task Expectation_failure_keeps_step_failed()
    {
        using var project = CliTestHelpers.CreateProjectRoot("exec-expectation");
        const string script = """{"command":["project","refresh","--dry-run"],"expect":{"contains":"never-matches"}}""";

        var result = await RunExecAsync(script, "--project", project.Path);

        Assert.Equal(1, result.ExitCode);
        var step = Assert.Single(ReadSteps(result.StandardOutput));
        Assert.Equal(0, step!["exitCode"]!.GetValue<int>());
        Assert.False(step["ok"]!.GetValue<bool>());
        Assert.Equal("ExecStepFailed", step["errorCode"]!.GetValue<string>());
        Assert.Equal(1, ReadSummary(result.StandardOutput)["failed"]!.GetValue<int>());
    }

}
