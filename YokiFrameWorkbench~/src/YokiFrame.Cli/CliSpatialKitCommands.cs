using System.Text.Json.Nodes;
using YokiFrame.Client;
using YokiFrame.Protocol.Common;
using YokiFrame.Protocol.Results;
using YokiFrame.Tooling.Application.Models;
using YokiFrame.Tooling.Application.Services;

namespace YokiFrame.Cli;

/// <summary>提供 SpatialKit 实例、密度和分析只读 CLI。</summary>
internal static class CliSpatialKitCommands
{
    /// <summary>判断命令是否属于 SpatialKit 只读边界。</summary>
    internal static bool IsSpatialKitCommand(CliCommandLine commandLine)
    {
        return commandLine.IsCommand("spatialkit", "stats")
            || commandLine.IsCommand("spatialkit", "indexes")
            || commandLine.IsCommand("spatialkit", "density")
            || commandLine.IsCommand("spatialkit", "analyze");
    }

    /// <summary>发送对应 SpatialKit ReadOnly action 并输出统一 JSON。</summary>
    internal static async Task<int> DispatchAsync(
        CliCommandLine commandLine,
        IYokiFrameClient client,
        CancellationToken cancellationToken)
    {
        string action = commandLine.IsCommand("spatialkit", "stats")
            ? "stats"
            : commandLine.IsCommand("spatialkit", "indexes")
                ? "list_indexes"
                : commandLine.IsCommand("spatialkit", "analyze")
                    ? "analyze"
                    : "density";
        JsonObject requestPayload = CreatePayload(commandLine, action);
        int timeoutMs = commandLine.GetIntOption("timeout", 10000);
        CommandExecutionResult result = await new CommandExecutionService(client).ExecuteAsync(
            commandLine.GetOption("engine", string.Empty),
            "SpatialKit",
            action,
            requestPayload.ToJsonString(YokiFrameJson.CompactOptions),
            commandLine.GetOption("source", "cli"),
            timeoutMs,
            cancellationToken).ConfigureAwait(false);

        JsonObject output = new()
        {
            ["command"] = "spatialkit " + commandLine.Verbs[1],
            ["action"] = action,
            ["transport"] = result.Transport,
            ["requestId"] = result.Response.RequestId,
            ["commandPath"] = result.CommandPath,
            ["responsePath"] = result.ResponsePath,
            ["response"] = CliJsonOutput.ToJsonNode(result.Response)
        };
        if (!string.Equals(result.Response.Status, "Success", StringComparison.OrdinalIgnoreCase))
        {
            return CliJsonOutput.WriteError(new YokiFrameError(
                result.Response.ErrorCode,
                result.Response.ErrorMessage,
                "Inspect the SpatialKit response evidence and retry against the current engine generation.",
                new[] { result.CommandPath, result.ResponsePath }), output);
        }

        return CliJsonOutput.WriteSuccess(output);
    }

    /// <summary>创建严格受限的 SpatialKit payload。</summary>
    private static JsonObject CreatePayload(CliCommandLine commandLine, string action)
    {
        JsonObject payload = new();
        if (action == "density")
        {
            string diagnosticsId = commandLine.GetOption("index", string.Empty);
            if (!string.IsNullOrWhiteSpace(diagnosticsId))
            {
                payload["diagnosticsId"] = diagnosticsId;
            }

            payload["resolution"] = Math.Max(4, Math.Min(64, commandLine.GetIntOption("resolution", 32)));
        }

        return payload;
    }
}
