using System.Text.Json.Nodes;
using YokiFrame.Client;
using YokiFrame.Client.ProjectModel;
using YokiFrame.Protocol.ProjectModel;
using YokiFrame.Protocol.Results;
using YokiFrame.Tooling.Application.Models.ProjectModel;
using YokiFrame.Tooling.Application.ProjectModel;

namespace YokiFrame.Cli;

/// <summary>
/// 提供 Project Model 的显式 status/refresh CLI 入口，避免 AI 直接写入 .yokiframe。
/// status 与 refresh 都直接返回 AI 可见状态；模型不可用时即为命令失败，不再依赖可选 strict 开关。
/// </summary>
internal static class CliProjectModelCommands
{
    /// <summary>判断命令是否为 project model 子命令。</summary>
    /// <param name="commandLine">解析后的命令行。</param>
    /// <returns>匹配 project status 或 project refresh 时返回 true。</returns>
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
        return commandLine.IsCommand("project", "status")
            ? WriteStatus(commandLine, client)
            : WriteRefresh(commandLine, client);
    }

    /// <summary>读取并输出当前 Project Model。</summary>
    /// <param name="commandLine">解析后的命令行。</param>
    /// <param name="client">统一 YokiFrame Client。</param>
    /// <returns>CLI 退出码。</returns>
    private static int WriteStatus(CliCommandLine commandLine, IYokiFrameClient client)
    {
        var detail = CliStatusProjection.ParseDetail(commandLine);
        var result = new ProjectModelService(client).Inspect();
        return WriteResult("project status", result, detail, client.Paths.ProjectRoot);
    }

    /// <summary>执行显式 Project Model refresh，并输出提交 generation 与证据。</summary>
    /// <param name="commandLine">解析后的命令行。</param>
    /// <param name="client">统一 YokiFrame Client。</param>
    /// <returns>CLI 退出码。</returns>
    private static int WriteRefresh(CliCommandLine commandLine, IYokiFrameClient client)
    {
        var detail = CliStatusProjection.ParseDetail(commandLine);
        if (CliStatusProjection.IsDryRun(commandLine))
        {
            return WriteRefreshDryRun(client);
        }

        var packageRoot = commandLine.GetOption("package", string.Empty);
        var result = new ProjectModelService(client).Refresh(packageRoot);
        return WriteResult("project refresh", result, detail, client.Paths.ProjectRoot);
    }

    /// <summary>
    /// 不提交 bundle，只报告 refresh 是否会改写模型；Inspect 与 Refresh 使用同一输入 hash 判断，结论可直接采信。
    /// </summary>
    /// <param name="client">统一 YokiFrame Client。</param>
    /// <returns>CLI 退出码。</returns>
    private static int WriteRefreshDryRun(IYokiFrameClient client)
    {
        var projectRoot = client.Paths.ProjectRoot;
        var result = new ProjectModelService(client).Inspect();
        var state = CliStatusProjection.FromProjectModelState(result.State);
        var writes = result.IsReady
            ? Array.Empty<(string, bool)>()
            : new ProjectModelFileStore(client.Paths).GetEvidencePaths()
                .Select(path => (path, File.Exists(path)))
                .ToArray();
        JsonObject payload = CliStatusProjection.CreateDryRunEnvelope(
            "project refresh",
            projectRoot,
            writes,
            "project refresh");
        // 当前模型状态独立于本次计划：Stale 表示会重写，Ready 表示不会产生写入。
        payload["currentState"] = state;
        return CliJsonOutput.WriteSuccess(payload);
    }

    /// <summary>
    /// 输出 Project Model 结果；默认省略聚合证据列表，避免把同一批路径重复写入 AI 上下文。
    /// </summary>
    /// <param name="command">稳定命令名。</param>
    /// <param name="result">Project Model 结果。</param>
    /// <param name="detail">summary 或 full。</param>
    /// <param name="projectRoot">当前项目根目录，用于输出项目相对证据路径。</param>
    /// <returns>CLI 退出码。</returns>
    private static int WriteResult(string command, ProjectModelResult result, string detail, string projectRoot)
    {
        var state = CliStatusProjection.FromProjectModelState(result.State);
        var usable = CliStatusProjection.IsUsable(state);
        JsonObject context = new()
        {
            ["command"] = command,
            ["state"] = state,
            ["changed"] = result.Changed,
            ["model"] = CreateModelNode(result.Bundle, detail),
            // 失败 envelope 的证据集中在 error 对象，成功 envelope 的证据随 issue 输出，避免同一批路径出现两次。
            ["issues"] = CreateIssueArray(result.Issues, usable, projectRoot),
            ["nextActions"] = CreateNextActions(state),
            ["detailAvailable"] = detail != CliStatusProjection.FULL_DETAIL
        };
        if (detail == CliStatusProjection.FULL_DETAIL)
        {
            context["evidencePaths"] = CliJsonOutput.ToJsonNode(
                CliStatusProjection.CompactPaths(projectRoot, result.EvidencePaths).ToArray());
        }

        if (usable)
        {
            return CliJsonOutput.WriteSuccess(context);
        }

        var primary = result.Issues.Count > 0 ? result.Issues[0] : null;
        var evidence = primary?.EvidencePaths ?? result.EvidencePaths;
        return CliJsonOutput.WriteError(
            new YokiFrameError(
                "ProjectModelNotReady",
                primary?.Message ?? "Project Model is not available.",
                primary?.Suggestion ?? "Run project refresh and retry.",
                CliStatusProjection.CompactPaths(projectRoot, evidence)),
            context);
    }

    /// <summary>把 Project Model 问题投影为统一 issue 结构。</summary>
    /// <param name="issues">Project Model 问题列表。</param>
    /// <param name="includeEvidence">是否随 issue 输出证据路径。</param>
    /// <param name="projectRoot">当前项目根目录，用于输出项目相对证据路径。</param>
    /// <returns>统一 issue 数组。</returns>
    private static JsonArray CreateIssueArray(
        IReadOnlyList<ProjectModelIssue> issues,
        bool includeEvidence,
        string projectRoot)
    {
        JsonArray array = new();
        foreach (var issue in issues)
        {
            array.Add(CliStatusProjection.CreateIssue(
                issue.Code,
                issue.Severity,
                issue.Message,
                string.Equals(issue.Code, "ProjectModelReadFailed", StringComparison.Ordinal),
                includeEvidence
                    ? CliStatusProjection.CompactPaths(projectRoot, issue.EvidencePaths)
                    : null));
        }

        return array;
    }

    /// <summary>按状态给出 AI 可以直接执行的下一步命令；Ready 时为空。</summary>
    /// <param name="state">AI 可见状态词。</param>
    /// <returns>下一步命令数组。</returns>
    private static JsonArray CreateNextActions(string state)
    {
        JsonArray actions = new();
        if (!string.Equals(state, CliStatusProjection.READY, StringComparison.Ordinal))
        {
            actions.Add(JsonValue.Create("project refresh"));
        }

        return actions;
    }

    /// <summary>按 detail 输出完整 bundle 或仅 manifest/ref 摘要。</summary>
    /// <param name="bundle">已提交 bundle；缺失时为 null。</param>
    /// <param name="detail">summary 或 full。</param>
    /// <returns>模型 JSON 节点。</returns>
    private static JsonNode CreateModelNode(ProjectModelBundle? bundle, string detail)
    {
        if (bundle == null)
        {
            return new JsonObject();
        }

        if (detail == CliStatusProjection.FULL_DETAIL)
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
