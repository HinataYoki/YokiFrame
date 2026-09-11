using System.Text.Json;
using System.Text.Json.Nodes;
using YokiFrame.Protocol.Results;
using YokiFrame.Protocol.Telemetry.SharedMemory;

namespace YokiFrame.Cli;

/// <summary>
/// 统一 CLI 面向 AI 的状态词表、issue 结构和诊断分层。
/// AI 默认只看到本类产出的最小投影；协议原始字段只在 --detail full 或 Workbench 出现。
/// </summary>
internal static class CliStatusProjection
{
    /// <summary>业务可用：本次结果可以直接用于下一步决策。</summary>
    public const string READY = "Ready";

    /// <summary>部分可用：存在不影响主流程的问题，但需要 AI 知道。</summary>
    public const string DEGRADED = "Degraded";

    /// <summary>宿主或 Provider 当前不可用，AI 必须改用其它通道。</summary>
    public const string UNAVAILABLE = "Unavailable";

    /// <summary>状态已过期，需要刷新后再判断。</summary>
    public const string STALE = "Stale";

    /// <summary>读取或校验失败，重试通常不会自行恢复。</summary>
    public const string FAILED = "Failed";

    /// <summary>结果无法确认，例如命令超时；不得当作失败重放。</summary>
    public const string UNKNOWN = "Unknown";

    /// <summary>默认 summary 允许的最大字符数；超出时只提示详情可用，不污染 AI 上下文。</summary>
    public const int MAX_SUMMARY_CHARS = 1024;

    /// <summary>默认分层：状态摘要、关键指标和问题。</summary>
    public const string SUMMARY_DETAIL = "summary";

    /// <summary>完整分层：协议原始字段，仅供定位问题和 Workbench 使用。</summary>
    public const string FULL_DETAIL = "full";

    /// <summary>错误级 issue，通常需要用户或宿主介入。</summary>
    public const string SEVERITY_ERROR = "Error";

    /// <summary>警告级 issue，AI 可以先继续再决定是否处理。</summary>
    public const string SEVERITY_WARNING = "Warning";

    /// <summary>
    /// 判断某个状态是否允许 AI 直接使用本次结果。
    /// </summary>
    /// <param name="state">本类产出的状态词。</param>
    /// <returns>Ready 或 Degraded 时返回 true。</returns>
    public static bool IsUsable(string state)
    {
        return string.Equals(state, READY, StringComparison.Ordinal)
            || string.Equals(state, DEGRADED, StringComparison.Ordinal);
    }

    /// <summary>
    /// 把 shared memory telemetry 读取状态收敛为 AI 可见状态。
    /// 半写帧和写入中属于瞬时抖动，映射为 Degraded 以便 AI 重试而不是判定失败。
    /// </summary>
    /// <param name="status">telemetry 帧读取状态。</param>
    /// <returns>AI 可见状态词。</returns>
    public static string FromTelemetryFrame(SharedMemoryTelemetryFrameStatus status)
    {
        switch (status)
        {
            case SharedMemoryTelemetryFrameStatus.Accepted:
                return READY;
            case SharedMemoryTelemetryFrameStatus.Unavailable:
            case SharedMemoryTelemetryFrameStatus.EngineIdHashMismatch:
                return UNAVAILABLE;
            case SharedMemoryTelemetryFrameStatus.GenerationMismatch:
                return STALE;
            case SharedMemoryTelemetryFrameStatus.Writing:
            case SharedMemoryTelemetryFrameStatus.HalfWrite:
                return DEGRADED;
            default:
                return FAILED;
        }
    }

    /// <summary>
    /// 把 doctor 诊断等级收敛为 AI 可见状态；当前 doctor 只区分 Healthy 和 Warning。
    /// </summary>
    /// <param name="level">doctor 等级。</param>
    /// <returns>AI 可见状态词。</returns>
    public static string FromDoctorLevel(string level)
    {
        return string.Equals(level, "Healthy", StringComparison.Ordinal) ? READY : DEGRADED;
    }

    /// <summary>
    /// 读取并校验 --detail 分层参数，拒绝未定义的隐式降级。
    /// </summary>
    /// <param name="commandLine">已解析命令行。</param>
    /// <returns>summary 或 full。</returns>
    public static string ParseDetail(CliCommandLine commandLine)
    {
        var detail = commandLine.GetOption("detail", SUMMARY_DETAIL);
        if (string.Equals(detail, SUMMARY_DETAIL, StringComparison.OrdinalIgnoreCase))
        {
            return SUMMARY_DETAIL;
        }

        if (string.Equals(detail, FULL_DETAIL, StringComparison.OrdinalIgnoreCase))
        {
            return FULL_DETAIL;
        }

        throw new YokiFrameProtocolException(new YokiFrameError(
            "InvalidOptionValue",
            "Option --detail must be summary or full.",
            "Use --detail=summary or --detail=full.",
            Array.Empty<string>()));
    }

    /// <summary>
    /// 创建统一的 issue 结构，保证不同命令的问题字段可被 AI 用同一段逻辑处理。
    /// </summary>
    /// <param name="code">稳定问题码。</param>
    /// <param name="severity">Error 或 Warning。</param>
    /// <param name="message">面向诊断的说明。</param>
    /// <param name="retryable">重试是否可能自行恢复。</param>
    /// <param name="evidencePaths">可选证据路径，由调用方决定是否携带。</param>
    /// <returns>issue JSON 对象。</returns>
    public static JsonObject CreateIssue(
        string code,
        string severity,
        string message,
        bool retryable,
        IEnumerable<string>? evidencePaths = null)
    {
        JsonObject issue = new()
        {
            ["code"] = code,
            ["severity"] = severity,
            ["message"] = message,
            ["retryable"] = retryable
        };
        if (evidencePaths == null)
        {
            return issue;
        }

        JsonArray paths = new();
        foreach (var path in evidencePaths)
        {
            if (!string.IsNullOrWhiteSpace(path))
            {
                paths.Add(JsonValue.Create(path));
            }
        }

        if (paths.Count > 0)
        {
            issue["evidencePaths"] = paths;
        }

        return issue;
    }

    /// <summary>
    /// 把 Project Model 读取状态收敛为 AI 可见状态。
    /// Missing 表示尚未生成模型，映射为 Unavailable；Partial 表示模型可用但有缺口，映射为 Degraded。
    /// </summary>
    /// <param name="state">Project Model 原始状态。</param>
    /// <returns>AI 可见状态词。</returns>
    public static string FromProjectModelState(string state)
    {
        switch (state)
        {
            case "Ready":
                return READY;
            case "Partial":
                return DEGRADED;
            case "Missing":
                return UNAVAILABLE;
            case "Stale":
                return STALE;
            case "Blocked":
                return FAILED;
            default:
                return UNKNOWN;
        }
    }

    /// <summary>
    /// 创建按 Kit 组织的默认状态 envelope，使 telemetry read 与 kit status 输出同一套字段。
    /// </summary>
    /// <param name="command">命令名。</param>
    /// <param name="engineId">目标 engine。</param>
    /// <param name="kit">目标 Kit。</param>
    /// <param name="state">AI 可见状态词。</param>
    /// <param name="source">状态来源：telemetry、snapshot 或 none。</param>
    /// <returns>已填充公共字段和空 issues 数组的 envelope。</returns>
    public static JsonObject CreateKitEnvelope(
        string command,
        string engineId,
        string kit,
        string state,
        string source)
    {
        return new JsonObject
        {
            ["command"] = command,
            ["engineId"] = engineId,
            ["kit"] = kit,
            ["state"] = state,
            ["source"] = source,
            ["issues"] = new JsonArray(),
            ["detailAvailable"] = true
        };
    }

    /// <summary>
    /// 创建 dry-run envelope：声明本次没有写入，并列出真实执行时会被写入的目标文件。
    /// </summary>
    /// <param name="command">命令名。</param>
    /// <param name="projectRoot">当前项目根目录，用于输出项目相对路径。</param>
    /// <param name="writes">计划写入的目标及其是否会覆盖现有内容。</param>
    /// <param name="nextAction">去掉 --dry-run 后的等价命令。</param>
    /// <returns>已填充公共字段的 dry-run envelope。</returns>
    public static JsonObject CreateDryRunEnvelope(
        string command,
        string projectRoot,
        IEnumerable<(string Path, bool OverwritesExisting)> writes,
        string nextAction)
    {
        JsonArray plan = new();
        foreach (var (path, overwritesExisting) in writes)
        {
            var compact = CompactPaths(projectRoot, new[] { path }).FirstOrDefault() ?? path;
            plan.Add(new JsonObject
            {
                ["path"] = compact,
                ["action"] = overwritesExisting ? "overwrite" : "create"
            });
        }

        return new JsonObject
        {
            ["command"] = command,
            ["state"] = READY,
            ["dryRun"] = true,
            ["writes"] = plan,
            ["issues"] = new JsonArray(),
            ["nextActions"] = new JsonArray(JsonValue.Create(nextAction)),
            ["detailAvailable"] = true
        };
    }

    /// <summary>
    /// 输出 dry-run 校验失败结果；失败说明与真实执行完全一致，只是没有落盘。
    /// </summary>
    /// <param name="payload">已填充公共字段的 envelope。</param>
    /// <param name="code">稳定错误码。</param>
    /// <param name="message">失败说明。</param>
    /// <param name="nextAction">去掉 --dry-run 后的等价命令。</param>
    /// <returns>失败退出码。</returns>
    public static int WriteDryRunFailure(JsonObject payload, string code, string message, string nextAction)
    {
        payload["state"] = FAILED;
        ((JsonArray)payload["issues"]!).Add(CreateIssue(
            code,
            SEVERITY_ERROR,
            message,
            false));
        return CliJsonOutput.WriteError(
            new YokiFrameError(
                code,
                message,
                "Resolve the reported problem before running " + nextAction + ".",
                Array.Empty<string>()),
            payload);
    }

    /// <summary>
    /// 判断命令是否请求 dry-run。
    /// </summary>
    /// <param name="commandLine">已解析命令行。</param>
    /// <returns>显式传入 --dry-run 时返回 true。</returns>
    public static bool IsDryRun(CliCommandLine commandLine)
    {
        return commandLine.GetBoolOption("dry-run", false);
    }

    /// <summary>
    /// 把证据路径投影为项目内相对路径并去重，避免每个路径重复占用同一段项目根前缀。
    /// 项目外路径保持原样，避免产生无法解析的相对路径。
    /// </summary>
    /// <param name="projectRoot">当前项目根目录。</param>
    /// <param name="paths">原始证据路径。</param>
    /// <returns>去重后的紧凑路径序列。</returns>
    public static IEnumerable<string> CompactPaths(string projectRoot, IEnumerable<string> paths)
    {
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        foreach (var path in paths)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            var compact = ToProjectRelative(projectRoot, path);
            if (seen.Add(compact))
            {
                yield return compact;
            }
        }
    }

    /// <summary>
    /// 把单个路径转换为项目内相对路径；无法安全转换时保留原始路径。
    /// </summary>
    /// <param name="projectRoot">当前项目根目录。</param>
    /// <param name="path">原始路径。</param>
    /// <returns>项目相对路径或原始路径。</returns>
    private static string ToProjectRelative(string projectRoot, string path)
    {
        if (string.IsNullOrWhiteSpace(projectRoot))
        {
            return path;
        }

        string fullRoot;
        string fullPath;
        try
        {
            fullRoot = Path.GetFullPath(projectRoot);
            fullPath = Path.GetFullPath(path);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return path;
        }

        var prefix = Path.EndsInDirectorySeparator(fullRoot)
            ? fullRoot
            : fullRoot + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return path;
        }

        return fullPath.Substring(prefix.Length).Replace('\\', '/');
    }

    /// <summary>
    /// 把非可用状态映射为稳定错误码，供调用方构造标准失败 envelope。
    /// </summary>
    /// <param name="state">AI 可见状态词。</param>
    /// <returns>稳定错误码。</returns>
    public static string ToErrorCode(string state)
    {
        switch (state)
        {
            case UNAVAILABLE:
                return "KitUnavailable";
            case STALE:
                return "KitStateStale";
            case UNKNOWN:
                return "KitStateUnknown";
            default:
                return "KitStateFailed";
        }
    }

    /// <summary>
    /// 尝试把 Kit 自带的 payload JSON 展开为默认 summary。
    /// payload 缺失、超限或不是合法 JSON 时返回 false，由调用侧改用 issue 说明而不是塞入原始串。
    /// </summary>
    /// <param name="payloadJson">telemetry payload 文本。</param>
    /// <param name="summary">解析后的 summary 节点。</param>
    /// <returns>可以安全作为 summary 输出时返回 true。</returns>
    public static bool TryParseSummary(string payloadJson, out JsonNode? summary)
    {
        summary = null;
        if (string.IsNullOrWhiteSpace(payloadJson) || payloadJson.Length > MAX_SUMMARY_CHARS)
        {
            return false;
        }

        try
        {
            summary = JsonNode.Parse(payloadJson);
            return summary != null;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
