using System.Text.Json;
using System.Text.Json.Nodes;
using YokiFrame.Protocol.Results;

namespace YokiFrame.Cli;

/// <summary>
/// 统一 CLI compact JSON 输出。
/// </summary>
internal static class CliJsonOutput
{
    /// <summary>成功退出码。</summary>
    public const int SuccessExitCode = 0;

    /// <summary>普通命令失败退出码。</summary>
    public const int FailureExitCode = 1;

    /// <summary>调用方通过 Ctrl+C 取消时使用的标准退出码。</summary>
    public const int CancelledExitCode = 130;
    private static readonly object sWarningGate = new();
    private static readonly List<string> sWarnings = new();
    private static readonly HashSet<string> sReservedErrorKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "ok",
        "error",
        "outcome",
        "requestId",
        "engineId",
        "transport",
        "evidencePaths",
        "warnings"
    };

    /// <summary>
    /// 登记不会改变主命令结果的结构化诊断，等待写入同一 JSON envelope。
    /// </summary>
    /// <param name="warning">面向机器和人的 warning 文本。</param>
    public static void AddWarning(string warning)
    {
        if (string.IsNullOrWhiteSpace(warning))
        {
            return;
        }

        lock (sWarningGate)
        {
            sWarnings.Add(warning);
        }
    }

    /// <summary>
    /// 输出成功 JSON 并返回进程退出码。
    /// </summary>
    /// <param name="payload">成功 payload。</param>
    /// <returns>成功退出码。</returns>
    public static int WriteSuccess(JsonObject payload)
    {
        AppendWarnings(payload);
        payload["ok"] = true;
        WriteJsonNode(Console.Out, payload);
        return SuccessExitCode;
    }

    /// <summary>
    /// 输出标准错误 JSON 并返回失败退出码。
    /// </summary>
    /// <param name="error">标准错误对象。</param>
    /// <returns>失败退出码。</returns>
    public static int WriteError(YokiFrameError error)
    {
        return WriteError(error, null);
    }

    /// <summary>
    /// 输出标准错误 JSON，并附加命令专属上下文字段。
    /// </summary>
    /// <param name="error">标准错误对象。</param>
    /// <param name="context">可选命令与状态上下文。</param>
    /// <returns>失败退出码。</returns>
    public static int WriteError(YokiFrameError error, JsonObject? context)
    {
        JsonObject payload = new()
        {
            ["ok"] = false,
            ["error"] = error.ToJson(),
            ["outcome"] = GetErrorOutcome(error.Code).ToString()
        };
        if (!string.IsNullOrEmpty(error.RequestId))
        {
            payload["requestId"] = error.RequestId;
        }

        if (!string.IsNullOrEmpty(error.EngineId))
        {
            payload["engineId"] = error.EngineId;
        }

        if (!string.IsNullOrEmpty(error.Transport))
        {
            payload["transport"] = error.Transport;
        }
        if (context != null)
        {
            foreach (var entry in context)
            {
                if (sReservedErrorKeys.Contains(entry.Key))
                {
                    CopyMissingErrorIdentityField(payload, entry.Key, entry.Value, error);
                    continue;
                }

                payload[entry.Key] = entry.Value?.DeepClone();
            }
        }

        AppendWarnings(payload);

        WriteJsonNode(Console.Error, payload);
        return FailureExitCode;
    }

    /// <summary>
    /// 在标准错误对象未携带身份字段时补充上下文，但绝不允许上下文覆盖已有值。
    /// </summary>
    /// <param name="payload">当前错误 envelope。</param>
    /// <param name="key">上下文字段名。</param>
    /// <param name="value">上下文字段值。</param>
    /// <param name="error">标准错误对象。</param>
    private static void CopyMissingErrorIdentityField(
        JsonObject payload,
        string key,
        JsonNode? value,
        YokiFrameError error)
    {
        if (key.Equals("requestId", StringComparison.Ordinal)
            && string.IsNullOrEmpty(error.RequestId)
            && !payload.ContainsKey(key))
        {
            payload[key] = value?.DeepClone();
            return;
        }

        if (key.Equals("engineId", StringComparison.Ordinal)
            && string.IsNullOrEmpty(error.EngineId)
            && !payload.ContainsKey(key))
        {
            payload[key] = value?.DeepClone();
            return;
        }

        if (key.Equals("transport", StringComparison.Ordinal)
            && string.IsNullOrEmpty(error.Transport)
            && !payload.ContainsKey(key))
        {
            payload[key] = value?.DeepClone();
        }
    }

    /// <summary>
    /// 输出统一取消错误，并使用 shell 可识别的 130 退出码。
    /// </summary>
    /// <param name="context">可选命令上下文。</param>
    /// <returns>取消退出码。</returns>
    public static int WriteCancelled(JsonObject? context = null)
    {
        WriteError(
            new YokiFrameError(
                "Cancelled",
                "The CLI operation was cancelled.",
                "Retry the command without Ctrl+C, or inspect the partial evidence before retrying.",
                Array.Empty<string>()),
            context);
        return CancelledExitCode;
    }

    /// <summary>
    /// 直接写出已构造的 JSON 节点，避免 Native AOT 为节点中的字符串再次查找反射元数据。
    /// </summary>
    /// <param name="output">标准输出或标准错误写入器。</param>
    /// <param name="payload">已完成结构化组装的 JSON 节点。</param>
    private static void WriteJsonNode(TextWriter output, JsonNode payload)
    {
        output.WriteLine(payload.ToJsonString(CliJson.CompactOptions));
    }

    /// <summary>
    /// 把进程级 warning 一次性投影到当前 JSON envelope，避免 stdout/stderr 混入普通文本。
    /// </summary>
    /// <param name="payload">待写出的 JSON payload。</param>
    private static void AppendWarnings(JsonObject payload)
    {
        string[] warnings;
        lock (sWarningGate)
        {
            warnings = sWarnings.ToArray();
            sWarnings.Clear();
        }

        if (warnings.Length > 0)
        {
            payload["warnings"] = ToJsonNode(warnings);
        }
    }

    /// <summary>
    /// 把任意对象转换成 JSON 节点，便于组合 compact 输出。
    /// </summary>
    /// <param name="value">待转换对象。</param>
    /// <returns>JSON 节点。</returns>
    public static JsonNode ToJsonNode<T>(T value)
    {
        return JsonSerializer.SerializeToNode(value, CliJson.CompactOptions) ?? new JsonObject();
    }

    /// <summary>
    /// 把稳定错误码投影为统一的命令结果状态；超时不能被误报为已失败。
    /// </summary>
    /// <param name="errorCode">标准错误码。</param>
    /// <returns>调用方可据此决定是否允许重试的结果状态。</returns>
    private static YokiFrame.Protocol.Results.CommandOutcomeState GetErrorOutcome(string errorCode)
    {
        if (string.Equals(errorCode, "Cancelled", StringComparison.Ordinal))
        {
            return YokiFrame.Protocol.Results.CommandOutcomeState.Cancelled;
        }

        if (string.Equals(errorCode, "CommandTimeout", StringComparison.Ordinal)
            || string.Equals(errorCode, "FastChannelCommandTimeout", StringComparison.Ordinal))
        {
            return YokiFrame.Protocol.Results.CommandOutcomeState.Unknown;
        }

        return YokiFrame.Protocol.Results.CommandOutcomeState.Failed;
    }
}
