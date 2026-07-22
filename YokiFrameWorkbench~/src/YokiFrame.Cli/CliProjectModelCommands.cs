using System.Text.Json.Nodes;
using YokiFrame.Client;
using YokiFrame.Protocol.ProjectModel;
using YokiFrame.Protocol.Results;
using YokiFrame.Tooling.Application.Models.ProjectModel;
using YokiFrame.Tooling.Application.ProjectModel;

namespace YokiFrame.Cli;

/// <summary>
/// 提供 Project Model 的显式 status/refresh CLI 入口，避免 AI 直接写入 `.yokiframe`。
/// </summary>
internal static class CliProjectModelCommands
{
    /// <summary>判断命令是否为 project model 子命令。</summary>
    /// <param name="commandLine">解析后的命令行。</param>
    /// <returns>匹配 `project status` 或 `project refresh` 时返回 true。</returns>
    public static bool IsProjectModelCommand(CliCommandLine commandLine)
    {
        return commandLine.IsCommand("project", "status") || commandLine.IsCommand("project", "refresh");
    }

    /// <summary>分派 status/refresh，并把业务判断保留在 Tooling.Application。</summary>
    /// <param name="commandLine">解析后的命令行。</param>
    /// <param name="client">统一 YokiFrame Client。</param>
    /// <returns>CLI 退出码。</returns>
    public static int Dispatch(CliCommandLine commandLine, IYokiFrameClient client)
    {
        if (commandLine.IsCommand("project", "status"))
        {
            return WriteStatus(commandLine, client);
        }

        return WriteRefresh(commandLine, client);
    }

    /// <summary>读取并输出当前 Project Model，strict 时把非 Ready 转为 stderr/非零退出。</summary>
    private static int WriteStatus(CliCommandLine commandLine, IYokiFrameClient client)
    {
        var strict = commandLine.GetBoolOption("strict", false);
        var detail = ReadDetail(commandLine);
        var result = new ProjectModelService(client).Inspect();
        return WriteResult("project status", result, detail, strict);
    }

    /// <summary>执行显式 Project Model refresh，并输出提交 generation 与证据。</summary>
    private static int WriteRefresh(CliCommandLine commandLine, IYokiFrameClient client)
    {
        var strict = commandLine.GetBoolOption("strict", false);
        var detail = ReadDetail(commandLine);
        var packageRoot = commandLine.GetOption("package", string.Empty);
        var result = new ProjectModelService(client).Refresh(packageRoot);
        return WriteResult("project refresh", result, detail, strict);
    }

    /// <summary>输出 Project Model 结果；refresh 的 Partial 结果保留已提交 bundle 和问题。</summary>
    private static int WriteResult(string command, ProjectModelResult result, string detail, bool strict)
    {
        JsonObject context = new()
        {
            ["command"] = command,
            ["state"] = result.State,
            ["changed"] = result.Changed,
            ["model"] = CreateModelNode(result.Bundle, detail),
            ["issues"] = CliJsonOutput.ToJsonNode(result.Issues.ToArray()),
            ["evidencePaths"] = CliJsonOutput.ToJsonNode(result.EvidencePaths.ToArray())
        };
        if (strict && !result.IsReady)
        {
            return CliJsonOutput.WriteError(new YokiFrameError(
                "ProjectModelNotReady",
                "Project Model is missing, stale, partial or blocked.",
                "Run project refresh, resolve the reported source issue, then retry with --strict.",
                result.EvidencePaths), context);
        }

        return CliJsonOutput.WriteSuccess(context);
    }

    /// <summary>读取 summary/full 输出模式，拒绝未定义的隐式降级。</summary>
    private static string ReadDetail(CliCommandLine commandLine)
    {
        var detail = commandLine.GetOption("detail", "summary");
        if (!string.Equals(detail, "summary", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(detail, "full", StringComparison.OrdinalIgnoreCase))
        {
            throw new YokiFrameProtocolException(new YokiFrameError(
                "InvalidOptionValue",
                "Option --detail must be summary or full.",
                "Use --detail=summary or --detail=full.",
                Array.Empty<string>()));
        }

        return detail.ToLowerInvariant();
    }

    /// <summary>按 detail 输出完整 bundle 或仅 manifest/ref 摘要。</summary>
    private static JsonNode CreateModelNode(ProjectModelBundle? bundle, string detail)
    {
        if (bundle == null)
        {
            return new JsonObject();
        }

        if (detail == "full")
        {
            return CliJsonOutput.ToJsonNode(bundle);
        }

        return new JsonObject
        {
            ["manifest"] = CliJsonOutput.ToJsonNode(bundle.Manifest),
            ["capabilityKits"] = CliJsonOutput.ToJsonNode(bundle.Capabilities.Kits),
            ["validationProfile"] = bundle.ValidationProfile.Profile
        };
    }
}
