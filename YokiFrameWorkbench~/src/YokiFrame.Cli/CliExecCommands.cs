using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using YokiFrame.Protocol.Results;

namespace YokiFrame.Cli;

/// <summary>
/// 从 stdin 读取 NDJSON 步骤并在 CLI 进程内顺序执行，让多步编排不需要在磁盘上落脚本。
/// </summary>
/// <remarks>
/// 多步编排（等重载、重试、断言）过去只能靠外部 shell 脚本，会在本地留下 .ps1/.sh 临时文件。
/// exec 把这类脚本留在 stdin 上：不落盘、不留痕。
/// 步骤格式为 NDJSON，一行一步，支持三种形态：
/// <c>{"command":["command","send","RoslynKit","RoslynKit","--action","domain_state"]}</c>、
/// <c>{"wait":3000}</c>、
/// <c>{"command":[...],"retry":{"attempts":10,"delayMs":3000},"expect":{"contains":"isPlaying"}}</c>。
/// 每一步输出一行结果，最后输出一行汇总。
/// </remarks>
internal static class CliExecCommands
{
    private const int MAX_STEPS = 256;
    private const int MAX_DELAY_MS = 60000;
    private const int MAX_ATTEMPTS = 120;
    private const int MAX_EMBEDDED_CHARS = 256 * 1024;
    private const string PROJECT_OPTION = "project";
    private const string COMMAND_FIELD = "command";
    private const string WAIT_FIELD = "wait";
    private const string RETRY_FIELD = "retry";
    private const string EXPECT_FIELD = "expect";
    private const string ATTEMPTS_FIELD = "attempts";
    private const string DELAY_FIELD = "delayMs";
    private const string CONTAINS_FIELD = "contains";
    private const string EXEC_VERB = "exec";

    /// <summary>判断当前命令行是否为 exec。</summary>
    /// <param name="commandLine">已解析命令行。</param>
    /// <returns>是 exec 时返回 true。</returns>
    internal static bool IsExecCommand(CliCommandLine commandLine)
    {
        return commandLine.IsCommand(EXEC_VERB);
    }

    /// <summary>
    /// 读取 stdin 上的步骤并顺序执行；任何步骤失败都会记录，默认立即停止。
    /// </summary>
    /// <param name="commandLine">已通过 schema 校验的命令行。</param>
    /// <param name="lifetimeCancellation">进程级取消源。</param>
    /// <returns>全部步骤成功返回 0，否则返回 1。</returns>
    internal static async Task<int> RunAsync(CliCommandLine commandLine, CancellationTokenSource lifetimeCancellation)
    {
        if (!Console.IsInputRedirected)
        {
            return WriteExecFailure(
                "ExecInputMissing",
                "yoki exec reads NDJSON steps from stdin; redirect a file or pipe the steps in.");
        }

        string inheritedProject = commandLine.GetOption(PROJECT_OPTION, string.Empty);
        bool continueOnError = commandLine.GetBoolOption("continue-on-error", false);
        string input = await Console.In.ReadToEndAsync().ConfigureAwait(false);
        string[] lines = input.Replace("\r\n", "\n").Split('\n');

        int step = 0;
        int failed = 0;
        foreach (string rawLine in lines)
        {
            string line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            if (step >= MAX_STEPS)
            {
                WriteStep(step + 1, "limit", CliJsonOutput.FailureExitCode, false, "ExecStepLimit", "step limit of " + MAX_STEPS + " reached");
                failed++;
                break;
            }

            step++;
            if (!TryReadStep(line, step, out JsonObject parsedStep, out string parseError))
            {
                WriteStep(step, line, CliJsonOutput.FailureExitCode, false, "ExecStepInvalid", parseError);
                failed++;
                if (!continueOnError)
                {
                    break;
                }

                continue;
            }

            if (TryReadWait(parsedStep, out int waitMs))
            {
                await Task.Delay(ClampDelay(waitMs), lifetimeCancellation.Token).ConfigureAwait(false);
                WriteStep(step, "wait " + waitMs + "ms", CliJsonOutput.SuccessExitCode, true, null, null);
                continue;
            }

            if (!TryBuildArguments(parsedStep, inheritedProject, out string[] arguments, out string argumentError))
            {
                WriteStep(step, line, CliJsonOutput.FailureExitCode, false, "ExecStepInvalid", argumentError);
                failed++;
                if (!continueOnError)
                {
                    break;
                }

                continue;
            }

            (int attempts, int delayMs) = ReadRetry(parsedStep);
            string expected = ReadExpectation(parsedStep);
            int exitCode = CliJsonOutput.FailureExitCode;
            string output = string.Empty;
            bool ok = false;
            string failureCode = "ExecStepFailed";
            for (var attempt = 1; attempt <= attempts; attempt++)
            {
                (exitCode, output) = await RunStepAsync(arguments, lifetimeCancellation).ConfigureAwait(false);
                ok = exitCode == CliJsonOutput.SuccessExitCode
                    && (expected.Length == 0 || output.Contains(expected, StringComparison.Ordinal));
                if (ok || attempt == attempts)
                {
                    break;
                }

                failureCode = exitCode == CliJsonOutput.SuccessExitCode ? "ExecStepExpectationFailed" : "ExecStepFailed";
                if (delayMs > 0)
                {
                    await Task.Delay(ClampDelay(delayMs), lifetimeCancellation.Token).ConfigureAwait(false);
                }
            }

            WriteStep(step, string.Join(' ', arguments), exitCode, ok, ok ? null : failureCode, output);
            if (!ok)
            {
                failed++;
                if (!continueOnError)
                {
                    break;
                }
            }
        }

        WriteSummary(step, failed);
        return failed == 0 ? CliJsonOutput.SuccessExitCode : CliJsonOutput.FailureExitCode;
    }

    /// <summary>在捕获输出的情况下执行一步 CLI 调用。</summary>
    /// <param name="arguments">步骤参数。</param>
    /// <param name="lifetimeCancellation">进程级取消源。</param>
    /// <returns>退出码与捕获到的输出。</returns>
    private static async Task<(int ExitCode, string Output)> RunStepAsync(
        string[] arguments,
        CancellationTokenSource lifetimeCancellation)
    {
        TextWriter original = Console.Out;
        var capture = new StringWriter();
        Console.SetOut(capture);
        int exitCode;
        try
        {
            exitCode = await Program.ExecuteAsync(arguments, lifetimeCancellation).ConfigureAwait(false);
        }
        finally
        {
            Console.SetOut(original);
        }

        return (exitCode, capture.ToString().TrimEnd());
    }

    /// <summary>解析单行步骤 JSON。</summary>
    /// <param name="line">原始行。</param>
    /// <param name="step">步骤序号。</param>
    /// <param name="parsedStep">解析结果。</param>
    /// <param name="error">失败说明。</param>
    /// <returns>解析成功时返回 true。</returns>
    private static bool TryReadStep(string line, int step, out JsonObject parsedStep, out string error)
    {
        parsedStep = new JsonObject();
        error = string.Empty;
        try
        {
            if (JsonNode.Parse(line) is not JsonObject stepObject)
            {
                error = "step " + step + " must be a JSON object.";
                return false;
            }

            parsedStep = stepObject;
            return true;
        }
        catch (JsonException exception)
        {
            error = "step " + step + " is not valid JSON: " + exception.Message;
            return false;
        }
    }

    /// <summary>读取 wait 字段。</summary>
    /// <param name="step">步骤对象。</param>
    /// <param name="waitMs">等待毫秒。</param>
    /// <returns>该步骤是等待步骤时返回 true。</returns>
    private static bool TryReadWait(JsonObject step, out int waitMs)
    {
        waitMs = 0;
        if (step[WAIT_FIELD] is not JsonValue value || !value.TryGetValue(out int parsed))
        {
            return false;
        }

        waitMs = parsed;
        return true;
    }

    /// <summary>读取 retry 字段。</summary>
    /// <param name="step">步骤对象。</param>
    /// <returns>尝试次数与间隔毫秒。</returns>
    private static (int Attempts, int DelayMs) ReadRetry(JsonObject step)
    {
        if (step[RETRY_FIELD] is not JsonObject retry)
        {
            return (1, 0);
        }

        int attempts = ReadInt(retry[ATTEMPTS_FIELD], 1);
        int delayMs = ReadInt(retry[DELAY_FIELD], 0);
        return (attempts < 1 ? 1 : attempts > MAX_ATTEMPTS ? MAX_ATTEMPTS : attempts, ClampDelay(delayMs));
    }

    /// <summary>读取 expect 字段中的 contains 断言。</summary>
    /// <param name="step">步骤对象。</param>
    /// <returns>期望文本；未声明时为空字符串。</returns>
    private static string ReadExpectation(JsonObject step)
    {
        return step[EXPECT_FIELD] is JsonObject expect
            && expect[CONTAINS_FIELD] is JsonValue value
            && value.TryGetValue(out string? text)
                ? text ?? string.Empty
                : string.Empty;
    }

    /// <summary>把步骤的 command 数组转换为 CLI 参数，并继承外层项目根。</summary>
    /// <param name="step">步骤对象。</param>
    /// <param name="inheritedProject">外层 --project。</param>
    /// <param name="arguments">转换结果。</param>
    /// <param name="error">失败说明。</param>
    /// <returns>转换成功时返回 true。</returns>
    private static bool TryBuildArguments(
        JsonObject step,
        string inheritedProject,
        out string[] arguments,
        out string error)
    {
        arguments = Array.Empty<string>();
        error = string.Empty;
        if (step[COMMAND_FIELD] is not JsonArray commandArray || commandArray.Count == 0)
        {
            error = "step requires a non-empty command array.";
            return false;
        }

        var list = new List<string>(commandArray.Count + 2);
        bool hasProject = false;
        foreach (JsonNode? item in commandArray)
        {
            if (item is not JsonValue value || !value.TryGetValue(out string? text) || string.IsNullOrEmpty(text))
            {
                error = "command array items must be non-empty strings.";
                return false;
            }

            list.Add(text);
            hasProject |= string.Equals(text, "--" + PROJECT_OPTION, StringComparison.Ordinal);
        }

        if (string.Equals(list[0], EXEC_VERB, StringComparison.Ordinal))
        {
            error = "nested exec steps are not allowed.";
            return false;
        }

        if (!hasProject && !string.IsNullOrEmpty(inheritedProject))
        {
            list.Add("--" + PROJECT_OPTION);
            list.Add(inheritedProject);
        }

        arguments = list.ToArray();
        return true;
    }

    private static int ReadInt(JsonNode? node, int fallback)
    {
        return node is JsonValue value && value.TryGetValue(out int parsed) ? parsed : fallback;
    }

    private static int ClampDelay(int value)
    {
        return value < 0 ? 0 : value > MAX_DELAY_MS ? MAX_DELAY_MS : value;
    }

    private static void WriteStep(
        int step,
        string input,
        int exitCode,
        bool ok,
        string? errorCode,
        string? output)
    {
        var node = new JsonObject
        {
            ["command"] = EXEC_VERB,
            ["step"] = step,
            ["input"] = input,
            ["exitCode"] = exitCode,
            ["ok"] = ok
        };
        if (!string.IsNullOrEmpty(errorCode))
        {
            node["errorCode"] = errorCode;
        }

        if (!string.IsNullOrEmpty(output))
        {
            bool truncated = output.Length > MAX_EMBEDDED_CHARS;
            string payload = truncated ? output.Substring(0, MAX_EMBEDDED_CHARS) : output;
            try
            {
                node["result"] = JsonNode.Parse(payload);
            }
            catch (JsonException)
            {
                node["result"] = payload;
            }

            if (truncated)
            {
                node["truncated"] = true;
            }
        }

        Console.Out.WriteLine(node.ToJsonString());
    }

    private static void WriteSummary(int steps, int failed)
    {
        var node = new JsonObject
        {
            ["command"] = EXEC_VERB,
            ["steps"] = steps,
            ["failed"] = failed,
            ["ok"] = failed == 0
        };
        Console.Out.WriteLine(node.ToJsonString());
    }

    private static int WriteExecFailure(string code, string message)
    {
        Console.Out.WriteLine(new JsonObject
        {
            ["command"] = EXEC_VERB,
            ["ok"] = false,
            ["errorCode"] = code,
            ["message"] = message
        }.ToJsonString());
        return CliJsonOutput.FailureExitCode;
    }
}
