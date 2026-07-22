using System.Text.Json;
using System.Text.Json.Nodes;
using YokiFrame;
using YokiFrame.Protocol.Results;
using YokiFrame.Tooling.Application.Installer;

namespace YokiFrame.Cli;

/// <summary>
/// 将 installer plan/apply 参数适配为共享 Application 会话，并输出稳定 compact JSON。
/// </summary>
internal static class CliInstallerCommands
{
    private const string UNITY_LOCAL_MODE = "unity-local";
    private const string UNITY_GIT_MODE = "unity-git";
    private const string GODOT_LOCAL_MODE = "godot-local";

    /// <summary>
    /// 判断命令是否属于 Installer 产品入口，供 Program 在创建 FileBridge client 前分流。
    /// </summary>
    /// <param name="commandLine">已解析命令行。</param>
    /// <returns>首个动词为 installer 时返回 true。</returns>
    public static bool IsInstallerCommand(CliCommandLine commandLine)
    {
        return commandLine.Verbs.Count > 0
            && string.Equals(commandLine.Verbs[0], "installer", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 执行 installer plan 或 apply，并保证两者使用相同 Application gateway 和会话状态机。
    /// </summary>
    /// <param name="commandLine">已解析 Installer 命令。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>进程退出码。</returns>
    public static async Task<int> DispatchAsync(
        CliCommandLine commandLine,
        CancellationToken cancellationToken)
    {
        if (!commandLine.IsCommand("installer", "plan")
            && !commandLine.IsCommand("installer", "apply"))
        {
            throw CreateInputException(
                "UnknownInstallerCommand",
                "Unsupported installer command.",
                "Use installer plan or installer apply.");
        }

        var options = CreateInstallOptions(commandLine);
        InstallerSessionService session = new(new InstallerCoreWorkflowGateway());
        var state = await session.PrepareAsync(options, cancellationToken).ConfigureAwait(false);
        if (commandLine.IsCommand("installer", "apply") && state.Status == InstallerSessionStatus.PlanReady)
        {
            state = await session.ApplyAsync(cancellationToken).ConfigureAwait(false);
        }

        var command = commandLine.IsCommand("installer", "plan") ? "installer plan" : "installer apply";
        return WriteSession(command, state);
    }

    /// <summary>
    /// 把 CLI 模式、路径、Godot 开关和接管确认转换为 Application 不可变安装选项。
    /// </summary>
    /// <param name="commandLine">已解析 Installer 命令。</param>
    /// <returns>可用于检测、计划和执行的共享 Application 输入。</returns>
    private static InstallerInstallOptions CreateInstallOptions(CliCommandLine commandLine)
    {
        var mode = RequireOption(commandLine, "mode");
        var target = RequireOption(commandLine, "target");
        var takeOver = ReadBooleanOption(commandLine, "take-over", defaultValue: false);
        var legacyPolicy = takeOver
            ? InstallerLegacyPackagePolicy.TakeOverConfirmed
            : InstallerLegacyPackagePolicy.Reject;
        return mode.ToLowerInvariant() switch
        {
            UNITY_LOCAL_MODE => InstallerInstallOptions.CreateUnityLocal(
                RequireOption(commandLine, "source"),
                target,
                legacyPolicy),
            UNITY_GIT_MODE => InstallerInstallOptions.CreateUnityGit(
                target,
                RequireOption(commandLine, "git-url")),
            GODOT_LOCAL_MODE => CreateGodotOptions(commandLine, target, legacyPolicy),
            _ => throw CreateInputException(
                "InvalidInstallerMode",
                "Unsupported installer mode: " + mode + ".",
                "Use unity-local, unity-git or godot-local.")
        };
    }

    /// <summary>
    /// 创建 Godot local 选项，并严格解析 repair 与 enable 布尔值。
    /// </summary>
    /// <param name="commandLine">已解析命令行。</param>
    /// <param name="target">目标 Godot 项目根。</param>
    /// <param name="legacyPolicy">legacy 包接管策略。</param>
    /// <returns>Godot local Application 输入。</returns>
    private static InstallerInstallOptions CreateGodotOptions(
        CliCommandLine commandLine,
        string target,
        InstallerLegacyPackagePolicy legacyPolicy)
    {
        GodotInstallOptions godotOptions = new(
            ReadBooleanOption(commandLine, "repair-godot", defaultValue: true),
            ReadBooleanOption(commandLine, "enable-godot", defaultValue: true));
        return InstallerInstallOptions.CreateGodotLocal(
            RequireOption(commandLine, "source"),
            target,
            godotOptions,
            legacyPolicy);
    }

    /// <summary>
    /// 读取非空必填选项，并将缺失统一为标准 CLI 输入错误。
    /// </summary>
    /// <param name="commandLine">已解析命令行。</param>
    /// <param name="name">不含双横线的选项名。</param>
    /// <returns>非空白选项值。</returns>
    private static string RequireOption(CliCommandLine commandLine, string name)
    {
        var value = commandLine.GetOption(name, string.Empty);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw CreateInputException(
                "MissingInstallerOption",
                "Installer option --" + name + " is required.",
                "Provide --" + name + " with a valid value.");
        }

        return value;
    }

    /// <summary>
    /// 严格读取 true/false 选项，防止拼写错误静默回落默认行为。
    /// </summary>
    /// <param name="commandLine">已解析命令行。</param>
    /// <param name="name">选项名。</param>
    /// <param name="defaultValue">缺失时默认值。</param>
    /// <returns>解析后的布尔值。</returns>
    private static bool ReadBooleanOption(CliCommandLine commandLine, string name, bool defaultValue)
    {
        var rawValue = commandLine.GetOption(name, defaultValue ? "true" : "false");
        if (bool.TryParse(rawValue, out var value))
        {
            return value;
        }

        throw CreateInputException(
            "InvalidInstallerOption",
            "Installer option --" + name + " must be true or false.",
            "Use --" + name + " true or --" + name + " false.");
    }

    /// <summary>
    /// 根据 Application 终态输出成功 JSON，或保留同一会话快照的标准错误 JSON。
    /// </summary>
    /// <param name="command">稳定命令名称。</param>
    /// <param name="state">Installer 会话终态。</param>
    /// <returns>进程退出码。</returns>
    private static int WriteSession(string command, InstallerSessionState state)
    {
        JsonObject context = new()
        {
            ["command"] = command,
            ["session"] = CreateSessionJson(state)
        };
        if (state.Status is InstallerSessionStatus.PlanReady or InstallerSessionStatus.Succeeded)
        {
            return CliJsonOutput.WriteSuccess(context);
        }

        return CliJsonOutput.WriteError(CreateSessionError(state), context);
    }

    /// <summary>
    /// 将统一 plan、日志、冲突、证据、进度和结果投影为 compact JSON。
    /// </summary>
    /// <param name="state">Installer 会话快照。</param>
    /// <returns>不含协议实现细节的会话 JSON。</returns>
    private static JsonObject CreateSessionJson(InstallerSessionState state)
    {
        return new JsonObject
        {
            ["status"] = state.Status.ToString(),
            ["plan"] = CreatePlanJson(state.Plan),
            ["logs"] = CreateLogsJson(state.Logs),
            ["conflicts"] = CreateStringArray(state.ConflictPaths),
            ["evidence"] = CreateEvidenceJson(state),
            ["rollbackSucceeded"] = state.RollbackSucceeded,
            ["errorMessage"] = state.ErrorMessage,
            ["progress"] = CreateProgressJson(state.Progress),
            ["result"] = CreateResultJson(state.Result)
        };
    }

    /// <summary>
    /// 将 Application 安装计划转换为统一动作与 warning JSON；没有计划时返回 null。
    /// </summary>
    /// <param name="plan">Application 计划预览。</param>
    /// <returns>计划 JSON 或 null。</returns>
    private static JsonNode? CreatePlanJson(InstallerPlanPreview? plan)
    {
        if (plan == null)
        {
            return null;
        }

        JsonArray actions = new();
        foreach (var action in plan.Actions)
        {
            actions.Add(new JsonObject
            {
                ["kind"] = action.Kind.ToString(),
                ["targetPath"] = action.TargetPath,
                ["value"] = action.Value,
                ["description"] = action.Description
            });
        }

        return new JsonObject
        {
            ["engine"] = plan.Engine.ToString(),
            ["mode"] = plan.Mode.ToString(),
            ["source"] = plan.Source,
            ["targetProjectRoot"] = plan.TargetProjectRoot,
            ["packageTarget"] = plan.PackageTarget,
            ["actions"] = actions,
            ["warnings"] = CreateStringArray(plan.Warnings)
        };
    }

    /// <summary>
    /// 将会话日志按产生顺序转换为稳定 JSON 数组。
    /// </summary>
    /// <param name="logs">Application 日志快照。</param>
    /// <returns>日志 JSON 数组。</returns>
    private static JsonArray CreateLogsJson(IReadOnlyList<InstallerLogEntry> logs)
    {
        JsonArray result = new();
        foreach (var log in logs)
        {
            result.Add(new JsonObject
            {
                ["timestampUtc"] = log.TimestampUtc.ToString("O"),
                ["level"] = log.Level.ToString(),
                ["message"] = log.Message
            });
        }

        return result;
    }

    /// <summary>
    /// 合并失败证据与成功结果证据，避免调用方按终态切换字段位置。
    /// </summary>
    /// <param name="state">Installer 会话快照。</param>
    /// <returns>去重后的证据路径数组。</returns>
    private static JsonArray CreateEvidenceJson(InstallerSessionState state)
    {
        IEnumerable<string> evidence = state.EvidencePaths;
        if (state.Result != null)
        {
            evidence = evidence.Concat(state.Result.EvidencePaths);
        }

        return CreateStringArray(evidence.Distinct(StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 将当前进度转换为 JSON；未进入 apply 时返回 null。
    /// </summary>
    /// <param name="progress">最近一次进度。</param>
    /// <returns>进度 JSON 或 null。</returns>
    private static JsonNode? CreateProgressJson(InstallerProgressUpdate? progress)
    {
        return progress == null
            ? null
            : new JsonObject
            {
                ["stage"] = progress.Stage.ToString(),
                ["completed"] = progress.Completed,
                ["total"] = progress.Total,
                ["message"] = progress.Message
            };
    }

    /// <summary>
    /// 将成功执行结果转换为 JSON；plan 阶段返回 null。
    /// </summary>
    /// <param name="result">Application 统一执行结果。</param>
    /// <returns>结果 JSON 或 null。</returns>
    private static JsonNode? CreateResultJson(InstallerExecutionResult? result)
    {
        return result == null
            ? null
            : new JsonObject
            {
                ["targetPath"] = result.TargetPath,
                ["changed"] = result.Changed,
                ["replacedExistingPackage"] = result.ReplacedExistingPackage,
                ["evidencePaths"] = CreateStringArray(result.EvidencePaths)
            };
    }

    /// <summary>
    /// 根据 Conflict 或 Failed 状态创建现有标准错误对象。
    /// </summary>
    /// <param name="state">Installer 会话终态。</param>
    /// <returns>标准 YokiFrame 错误。</returns>
    private static YokiFrameError CreateSessionError(InstallerSessionState state)
    {
        var isConflict = state.Status == InstallerSessionStatus.Conflict;
        return new YokiFrameError(
            isConflict ? "InstallerConflict" : "InstallerFailed",
            string.IsNullOrWhiteSpace(state.ErrorMessage) ? "Installer did not reach a successful state." : state.ErrorMessage,
            isConflict
                ? "Resolve managed conflicts or rerun with --take-over for confirmed legacy content."
                : "Inspect session logs and evidence paths, then retry after correcting the target project.",
            state.EvidencePaths.ToArray());
    }

    /// <summary>
    /// 把字符串序列转换为 compact JSON 数组。
    /// </summary>
    /// <param name="values">字符串序列。</param>
    /// <returns>JSON 数组。</returns>
    private static JsonArray CreateStringArray(IEnumerable<string> values)
    {
        JsonArray result = new();
        foreach (var value in values)
        {
            result.Add(JsonSerializer.SerializeToNode(value, CliJsonContext.Default.String));
        }

        return result;
    }

    /// <summary>
    /// 创建会被 Program 现有异常边界转换为标准错误 JSON 的输入异常。
    /// </summary>
    /// <param name="code">稳定错误码。</param>
    /// <param name="message">错误说明。</param>
    /// <param name="suggestion">修复建议。</param>
    /// <returns>协议异常包装。</returns>
    private static YokiFrameProtocolException CreateInputException(
        string code,
        string message,
        string suggestion)
    {
        return new YokiFrameProtocolException(new YokiFrameError(
            code,
            message,
            suggestion,
            Array.Empty<string>()));
    }
}
