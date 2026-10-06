using System.Diagnostics;
using System.Text;
using System.Text.Json.Nodes;
using YokiFrame.Client;
using YokiFrame.Protocol.Results;
using YokiFrame.Tooling.Application.Models;
using YokiFrame.Tooling.Application.Services;

namespace YokiFrame.Cli;

internal static class CliScriptCommands
{
    internal static async Task<int> RunAsync(CliCommandLine command, IYokiFrameClient client, CancellationToken token)
    {
        if (!command.GetBoolOption("confirm-execution", false))
            return Fail("ScriptConfirmationRequired", "Trusted C# requires --confirm-execution.");
        string target = command.GetOption("target", "");
        if (target != "editor" && target != "play" && target != "runtime")
            return Fail("ScriptTargetInvalid", "Specify editor, play or runtime explicitly.");
        string file = command.GetOption("file", "");
        if (file.Length > 0 && command.GetBoolOption("stdin", false))
            return Fail("ScriptInputInvalid", "Use either --file or --stdin, not both.");
        if (file.Length == 0 && !Console.IsInputRedirected)
            return Fail("ScriptInputMissing", "Pipe a C# method body to stdin or provide --file.");

        string body;
        if (file.Length > 0)
        {
            string root = Path.GetFullPath(command.GetOption("project", Environment.CurrentDirectory));
            string path = YokiFrame.YokiFrameFilePathPolicy.CombineInside(root, file);
            using var reader = File.OpenText(path);
            body = await ReadSourceAsync(reader, token);
        }
        else body = await ReadSourceAsync(Console.In, token);
        if (string.IsNullOrWhiteSpace(body) || Encoding.UTF8.GetByteCount(body) > 128 * 1024)
            return Fail("ScriptInputLimit", "Input must be nonempty and at most 128 KiB UTF-8.");

        string engine = command.GetOption("engine", "");
        int timeoutMs = command.GetIntOption("timeout", 30000);
        var service = new CommandExecutionService(client);
        var payload = new JsonObject
        {
            ["code"] = body, ["target"] = target, ["confirmed"] = true, ["timeoutMs"] = timeoutMs
        };
        CommandExecutionResult submitted = await service.ExecuteAsync(
            engine, "Engine", "script_run", payload.ToJsonString(), "cli", 10000, token);
        if (submitted.Outcome != CommandOutcomeState.Succeeded)
            return Fail(submitted.Response.ErrorCode.Length == 0 ? "ScriptSubmissionUnknown" : submitted.Response.ErrorCode,
                submitted.Response.ErrorMessage, new JsonObject { ["requestId"] = submitted.RequestId });
        var accepted = JsonNode.Parse(submitted.Response.ResultJson)!;
        string runId = accepted["runId"]!.GetValue<string>();
        var query = new JsonObject { ["runId"] = runId };
        var context = new JsonObject
        {
            ["command"] = "script", ["runId"] = runId, ["submittedRequestId"] = submitted.RequestId
        };
        var elapsed = Stopwatch.StartNew();
        string interruptedCode = "ScriptWaitInterrupted";
        try
        {
            while (elapsed.ElapsedMilliseconds < timeoutMs + 10000L)
            {
                token.ThrowIfCancellationRequested();
                CommandExecutionResult observed = await service.ExecuteWithIdentityAsync(
                    engine, "Engine", "run_result", query.ToJsonString(), "cli", 5000, token, submitted.TargetIdentity);
                if (observed.Outcome != CommandOutcomeState.Succeeded)
                {
                    interruptedCode = "ScriptObservationFailed";
                    break;
                }
                JsonNode result = JsonNode.Parse(observed.Response.ResultJson)!;
                if (result["terminal"]?.GetValue<bool>() == true)
                {
                    context["result"] = result;
                    if (result["state"]?.GetValue<string>() == "Passed")
                        return CliJsonOutput.WriteSuccess(context);
                    return Fail("Script" + result["state"]!.GetValue<string>(),
                        "Script did not pass; inspect result and compiler diagnostics.", context);
                }
                await Task.Delay(100, token);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // The host owns execution. Cancellation of this CLI does not prove the C# stopped.
        }
        catch (Exception exception) when (exception is YokiFrameProtocolException || exception is System.Text.Json.JsonException)
        {
            interruptedCode = "ScriptObservationFailed";
        }
        using var cleanup = new CancellationTokenSource(2000);
        try
        {
            var cancellation = await service.ExecuteWithIdentityAsync(engine, "Engine", "run_cancel",
                query.ToJsonString(), "cli", 1500, cleanup.Token, submitted.TargetIdentity);
            context["cancelRequestAccepted"] = cancellation.Outcome == CommandOutcomeState.Succeeded;
        }
        catch (Exception exception) when (exception is OperationCanceledException || exception is YokiFrameProtocolException)
        {
            context["cancelRequestAccepted"] = false;
        }
        return Fail(interruptedCode, "Stopped waiting; execution may continue. Query run_result; do not resubmit.", context);
    }

    private static async Task<string> ReadSourceAsync(TextReader reader, CancellationToken token)
    {
        var source = new StringBuilder();
        char[] buffer = new char[4096];
        while (true)
        {
            int read = await reader.ReadAsync(buffer.AsMemory(), token);
            if (read == 0) return source.ToString();
            if (source.Length + read > 128 * 1024)
                throw new YokiFrameProtocolException(new YokiFrameError(
                    "ScriptInputLimit", "C# input exceeds 128 KiB.", "Submit a smaller task.", Array.Empty<string>()));
            source.Append(buffer, 0, read);
        }
    }

    private static int Fail(string code, string message, JsonObject? context = null)
    {
        return CliJsonOutput.WriteError(new YokiFrameError(code, message,
            "Inspect Engine/script_status and the run record. Do not automatically retry execution.",
            Array.Empty<string>()), context);
    }
}
