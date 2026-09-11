using System.Text.Json.Nodes;
using YokiFrame.Client;
using YokiFrame.Protocol.Results;
using YokiFrame.Protocol.Telemetry.SharedMemory;

namespace YokiFrame.Cli;

/// <summary>
/// 提供 kit status 单命令：按 telemetry -> snapshot 的渐进顺序返回任意 Kit 的最小状态。
/// AI 只需要这一条命令即可判断 Kit 是否可用，不必自己组合 telemetry 与 snapshot。
/// </summary>
internal static class CliKitStatusCommand
{
    /// <summary>状态来源：shared memory telemetry。</summary>
    private const string SOURCE_TELEMETRY = "telemetry";

    /// <summary>状态来源：回落 snapshot。</summary>
    private const string SOURCE_SNAPSHOT = "snapshot";

    /// <summary>状态来源：两者都不可用。</summary>
    private const string SOURCE_NONE = "none";

    /// <summary>
    /// 判断命令是否为 kit status。
    /// </summary>
    /// <param name="commandLine">已解析命令行。</param>
    /// <returns>匹配 kit status 子命令时返回 true。</returns>
    public static bool IsKitStatusCommand(CliCommandLine commandLine)
    {
        return commandLine.IsCommand("kit", "status");
    }

    /// <summary>
    /// 读取指定 Kit 的最小状态，telemetry 不可用时自动回落 snapshot。
    /// </summary>
    /// <param name="commandLine">已解析命令行。</param>
    /// <param name="client">统一 YokiFrame Client。</param>
    /// <returns>CLI 退出码。</returns>
    public static int Dispatch(CliCommandLine commandLine, IYokiFrameClient client)
    {
        var detail = CliStatusProjection.ParseDetail(commandLine);
        var engineId = Program.ResolveEngineId(commandLine, client);
        var kit = commandLine.GetOption("kit", "System");
        var name = commandLine.GetOption("name", "state");
        var expectedGeneration = Program.ResolveExpectedGeneration(commandLine, client, engineId);
        var telemetry = client.ReadTelemetry(
            engineId,
            kit,
            name,
            expectedGeneration,
            SharedMemoryTelemetryFrameReader.DEFAULT_MAX_PAYLOAD_BYTES);
        if (detail == CliStatusProjection.FULL_DETAIL)
        {
            return WriteFullDetail(client, engineId, kit, name, telemetry);
        }

        var telemetryState = CliStatusProjection.FromTelemetryFrame(telemetry.Status);
        if (telemetry.IsAccepted && CliStatusProjection.TryParseSummary(telemetry.PayloadJson, out var summary))
        {
            JsonObject payload = CliStatusProjection.CreateKitEnvelope(
                "kit status",
                engineId,
                kit,
                telemetryState,
                SOURCE_TELEMETRY);
            var header = telemetry.Header;
            if (header != null)
            {
                payload["generation"] = header.Generation;
                payload["sequence"] = header.Sequence;
            }

            payload["summary"] = summary;
            return CliJsonOutput.WriteSuccess(payload);
        }

        return WriteSnapshotFallback(client, engineId, kit, name, expectedGeneration, telemetry.Status);
    }

    /// <summary>
    /// telemetry 不可用时读取 snapshot 并判定新鲜度；generation 一致才视为 Ready。
    /// </summary>
    /// <param name="client">统一 YokiFrame Client。</param>
    /// <param name="engineId">目标 engine。</param>
    /// <param name="kit">目标 Kit。</param>
    /// <param name="name">snapshot 名称。</param>
    /// <param name="expectedGeneration">当前 engine generation；无法推断时为 null。</param>
    /// <param name="telemetryStatus">telemetry 读取失败状态，用于解释回落原因。</param>
    /// <returns>CLI 退出码。</returns>
    private static int WriteSnapshotFallback(
        IYokiFrameClient client,
        string engineId,
        string kit,
        string name,
        long? expectedGeneration,
        SharedMemoryTelemetryFrameStatus telemetryStatus)
    {
        var snapshotPath = client.Paths.GetSnapshotPath(engineId, kit, name);
        var evidence = CliStatusProjection.CompactPaths(client.Paths.ProjectRoot, new[] { snapshotPath });
        var snapshot = TryReadSnapshot(client, engineId, kit, name);
        if (snapshot == null)
        {
            JsonObject missing = CliStatusProjection.CreateKitEnvelope(
                "kit status",
                engineId,
                kit,
                CliStatusProjection.UNAVAILABLE,
                SOURCE_NONE);
            ((JsonArray)missing["issues"]!).Add(CliStatusProjection.CreateIssue(
                "KitStateUnavailable",
                CliStatusProjection.SEVERITY_ERROR,
                "Neither telemetry nor snapshot is available for this Kit.",
                true));
            return CliJsonOutput.WriteError(
                new YokiFrameError(
                    "KitUnavailable",
                    "Neither telemetry nor snapshot is available for this Kit.",
                    "Start the engine host, or refresh the Project Model and retry.",
                    evidence,
                    null,
                    engineId),
                missing);
        }

        var snapshotGeneration = ReadSnapshotGeneration(snapshot);
        var isFresh = expectedGeneration.HasValue && snapshotGeneration == expectedGeneration.Value;
        var state = isFresh ? CliStatusProjection.READY : CliStatusProjection.STALE;
        JsonObject payload = CliStatusProjection.CreateKitEnvelope(
            "kit status",
            engineId,
            kit,
            state,
            SOURCE_SNAPSHOT);
        payload["generation"] = snapshotGeneration;
        if (CliStatusProjection.TryParseSummary(ReadSnapshotPayload(snapshot), out var summary))
        {
            payload["summary"] = summary;
        }

        ((JsonArray)payload["issues"]!).Add(CliStatusProjection.CreateIssue(
            "TelemetryNotUsed",
            CliStatusProjection.SEVERITY_WARNING,
            "Telemetry was not usable (" + telemetryStatus + "); snapshot data was returned instead.",
            true));
        return isFresh
            ? CliJsonOutput.WriteSuccess(payload)
            : CliJsonOutput.WriteError(
                new YokiFrameError(
                    "KitStateStale",
                    "Snapshot generation does not match the current engine generation.",
                    "Wait for the host to publish a new snapshot, then retry.",
                    evidence,
                    null,
                    engineId),
                payload);
    }

    /// <summary>
    /// 输出完整分层：telemetry 原始字段和 snapshot 路径，仅供定位问题和 Workbench 使用。
    /// </summary>
    /// <param name="client">统一 YokiFrame Client。</param>
    /// <param name="engineId">目标 engine。</param>
    /// <param name="kit">目标 Kit。</param>
    /// <param name="name">snapshot 名称。</param>
    /// <param name="telemetry">telemetry 读取结果。</param>
    /// <returns>CLI 退出码。</returns>
    private static int WriteFullDetail(
        IYokiFrameClient client,
        string engineId,
        string kit,
        string name,
        SharedMemoryTelemetryFrameReadResult telemetry)
    {
        JsonObject telemetryNode = new()
        {
            ["status"] = telemetry.Status.ToString(),
            ["accepted"] = telemetry.IsAccepted,
            ["message"] = telemetry.Message,
            ["payloadJson"] = telemetry.PayloadJson
        };
        var header = telemetry.Header;
        if (header != null)
        {
            telemetryNode["generation"] = header.Generation;
            telemetryNode["sequence"] = header.Sequence;
            telemetryNode["writtenAtUtcTicks"] = header.WrittenAtUtcTicks;
        }

        JsonObject payload = new()
        {
            ["command"] = "kit status",
            ["engineId"] = engineId,
            ["kit"] = kit,
            ["name"] = name,
            ["detail"] = CliStatusProjection.FULL_DETAIL,
            ["telemetry"] = telemetryNode,
            ["snapshotPath"] = client.Paths.GetSnapshotPath(engineId, kit, name)
        };
        return CliJsonOutput.WriteSuccess(payload);
    }

    /// <summary>
    /// 读取 snapshot；只有 SnapshotMissing 被视为可降级结果，其它协议异常继续向上抛出。
    /// </summary>
    /// <param name="client">统一 YokiFrame Client。</param>
    /// <param name="engineId">目标 engine。</param>
    /// <param name="kit">目标 Kit。</param>
    /// <param name="name">snapshot 名称。</param>
    /// <returns>snapshot 节点；文件缺失时返回 null。</returns>
    private static JsonNode? TryReadSnapshot(IYokiFrameClient client, string engineId, string kit, string name)
    {
        try
        {
            return client.ReadSnapshot(engineId, kit, name);
        }
        catch (YokiFrameProtocolException exception)
            when (string.Equals(exception.Error.Code, "SnapshotMissing", StringComparison.Ordinal))
        {
            return null;
        }
    }

    /// <summary>
    /// 读取 snapshot 内嵌的 payloadJson 文本。
    /// </summary>
    /// <param name="snapshot">snapshot 节点。</param>
    /// <returns>payload 文本；字段缺失或类型不符时返回空串。</returns>
    private static string ReadSnapshotPayload(JsonNode snapshot)
    {
        return snapshot["payloadJson"] is JsonValue value && value.TryGetValue<string>(out var text)
            ? text
            : string.Empty;
    }

    /// <summary>
    /// 读取 snapshot 的 generation，用于与当前 engine generation 比较新鲜度。
    /// </summary>
    /// <param name="snapshot">snapshot 节点。</param>
    /// <returns>generation；字段缺失或类型不符时返回 0。</returns>
    private static long ReadSnapshotGeneration(JsonNode snapshot)
    {
        return snapshot["generation"] is JsonValue value && value.TryGetValue<long>(out var generation)
            ? generation
            : 0L;
    }
}
