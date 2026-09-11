using System.Text.Json.Nodes;
using YokiFrame;
using YokiFrame.Client;
using YokiFrame.Client.FileBridge.Diagnostics;
using YokiFrame.Cli;
using YokiFrame.Protocol.FileBridge;
using YokiFrame.Protocol.Results;
using YokiFrame.Protocol.Telemetry.SharedMemory;
using YokiFrame.Tooling.Application.Capabilities;
using YokiFrame.Tooling.Application.Engines;
using YokiFrame.Tooling.Application.Models;
using YokiFrame.Tooling.Application.Services;

/// <summary>
/// YokiFrame Phase 1 CLI 入口，提供 AI 和脚本可稳定调用的 compact JSON 命令。
/// </summary>
internal static class Program
{
    /// <summary>
    /// 解析命令；Installer 在创建 FileBridge client 前进入共享 Application 会话，其余命令继续走 Client。
    /// </summary>
    /// <param name="args">命令行参数。</param>
    /// <returns>进程退出码。</returns>
    private static async Task<int> Main(string[] args)
    {
        using CancellationTokenSource lifetimeCancellation = new();
        ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            lifetimeCancellation.Cancel();
        };
        Console.CancelKeyPress += cancelHandler;
        try
        {
            var commandLine = CliCommandLine.Parse(args);
            CliCommandSchemaRegistry.Validate(commandLine);
            if (CliInstallerCommands.IsInstallerCommand(commandLine))
            {
                return await CliInstallerCommands.DispatchAsync(commandLine, lifetimeCancellation.Token).ConfigureAwait(false);
            }

            var projectRoot = ResolveProjectRoot(commandLine);
            if (CliPlayerBuildCommands.IsPlayerBuildCommand(commandLine))
            {
                return await CliPlayerBuildCommands.DispatchAsync(
                    commandLine,
                    projectRoot,
                    lifetimeCancellation.Token).ConfigureAwait(false);
            }

            using YokiFrameClient client = new(projectRoot);
            var exitCode = await DispatchAsync(commandLine, client, lifetimeCancellation.Token).ConfigureAwait(false);
            // 查询命令必须先读取 evidence，再执行维护清理；command status 还要保留证据供连续排查。
            if (!commandLine.IsCommand("command", "status"))
            {
                TryPruneProjectStorage(projectRoot);
            }

            return exitCode;
        }
        catch (OperationCanceledException) when (lifetimeCancellation.IsCancellationRequested)
        {
            return CliJsonOutput.WriteCancelled();
        }
        catch (YokiFrameProtocolException exception)
        {
            return CliJsonOutput.WriteError(exception.Error);
        }
        catch (Exception exception)
        {
            return CliJsonOutput.WriteError(new YokiFrameError(
                "UnhandledError",
                exception.Message,
                "Run the command again with valid arguments or inspect the current project state.",
                Array.Empty<string>()));
        }
        finally
        {
            Console.CancelKeyPress -= cancelHandler;
        }
    }

    /// <summary>
    /// 根据动词组合执行对应命令。
    /// </summary>
    /// <param name="commandLine">已解析命令行。</param>
    /// <param name="client">FileBridge 客户端。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>进程退出码。</returns>
    private static async Task<int> DispatchAsync(
        CliCommandLine commandLine,
        IYokiFrameClient client,
        CancellationToken cancellationToken)
    {
        if (commandLine.IsCommand("harness", "status"))
        {
            return WriteHarnessStatus(client);
        }

        if (commandLine.IsCommand("harness", "catalog"))
        {
            return await WriteHarnessCatalogAsync(commandLine, client, cancellationToken).ConfigureAwait(false);
        }

        if (CliProjectModelCommands.IsProjectModelCommand(commandLine))
        {
            return CliProjectModelCommands.Dispatch(commandLine, client);
        }

        if (CliAudioKitCommands.IsAudioIndexCommand(commandLine))
        {
            return CliAudioKitCommands.Dispatch(commandLine, client);
        }

        if (CliLocalizationKitCommands.IsLocalizationCommand(commandLine))
        {
            return await CliLocalizationKitCommands.DispatchAsync(commandLine, client, cancellationToken).ConfigureAwait(false);
        }

        if (CliSpatialKitCommands.IsSpatialKitCommand(commandLine))
        {
            return await CliSpatialKitCommands.DispatchAsync(commandLine, client, cancellationToken).ConfigureAwait(false);
        }

        if (CliKitStatusCommand.IsKitStatusCommand(commandLine))
        {
            return CliKitStatusCommand.Dispatch(commandLine, client);
        }

        if (commandLine.IsCommand("engine", "list"))
        {
            return WriteEngineList(commandLine, client);
        }

        if (commandLine.IsCommand("snapshot", "read"))
        {
            return WriteSnapshot(commandLine, client);
        }

        if (commandLine.IsCommand("bridge", "status"))
        {
            return WriteBridgeStatus(commandLine, client);
        }

        if (commandLine.IsCommand("doctor"))
        {
            return WriteDoctor(commandLine, client);
        }

        if (commandLine.IsCommand("command", "send"))
        {
            return await WriteCommandSendAsync(commandLine, client, cancellationToken).ConfigureAwait(false);
        }

        if (commandLine.IsCommand("command", "status"))
        {
            return WriteCommandStatus(commandLine, client);
        }

        if (commandLine.IsCommand("telemetry", "read"))
        {
            return WriteTelemetryRead(commandLine, client);
        }

        if (commandLine.IsCommand("fastchannel", "status"))
        {
            return CliFastChannelCommands.WriteStatus(commandLine, client);
        }

        throw new YokiFrameProtocolException(new YokiFrameError(
            "UnknownCommand",
            "Unsupported command.",
            "Use project status/refresh, player build, spatialkit stats/indexes/density/analyze, audio index scan/generate, localization search/check/add/template generate, doctor, harness status/catalog, kit status, engine list, snapshot read, command send/status, bridge status, telemetry read or fastchannel status.",
            Array.Empty<string>()));
    }

    /// <summary>
    /// 输出 harness capability 状态。
    /// </summary>
    /// <param name="client">FileBridge 客户端。</param>
    /// <returns>进程退出码。</returns>
    private static int WriteHarnessStatus(IYokiFrameClient client)
    {
        JsonObject payload = new()
        {
            ["command"] = "harness status",
            ["projectRoot"] = client.Paths.ProjectRoot,
            ["path"] = client.Paths.GetHarnessCapabilitiesPath(),
            ["data"] = client.ReadHarnessCapabilities()
        };
        return CliJsonOutput.WriteSuccess(payload);
    }

    /// <summary>
    /// 汇总静态 harness、engine registry、heartbeat 和可选实时命令目录，供 AI 判断能力来源与漂移。
    /// </summary>
    /// <param name="commandLine">已解析命令行。</param>
    /// <param name="client">统一 YokiFrame Client。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>进程退出码。</returns>
    private static async Task<int> WriteHarnessCatalogAsync(
        CliCommandLine commandLine,
        IYokiFrameClient client,
        CancellationToken cancellationToken)
    {
        var requestedEngineId = commandLine.GetOption("engine", string.Empty);
        var refreshCommands = commandLine.GetBoolOption("refresh-commands", false);
        var strict = commandLine.GetBoolOption("strict", false);
        var timeoutMs = commandLine.GetIntOption("timeout", 10000);
        var result = await new CapabilityCatalogService(client).BuildAsync(
            requestedEngineId,
            refreshCommands,
            "cli",
            timeoutMs,
            cancellationToken).ConfigureAwait(false);
        JsonObject payload = new()
        {
            ["command"] = "harness catalog",
            ["state"] = result.State,
            ["catalog"] = CliJsonOutput.ToJsonNode(result.Catalog)
        };
        if (strict && !result.IsReady)
        {
            return CliJsonOutput.WriteError(new YokiFrameError(
                "CapabilityCatalogNotReady",
                "Capability catalog contains missing, stale or conflicting evidence.",
                "Inspect catalog.issues, refresh the engine state, then retry with --strict.",
                result.EvidencePaths), payload);
        }

        return CliJsonOutput.WriteSuccess(payload);
    }

    /// <summary>
    /// 输出当前 engine registry 列表；默认只给 AI 判断宿主身份和能力所需的字段。
    /// </summary>
    /// <param name="commandLine">已解析命令行。</param>
    /// <param name="client">FileBridge 客户端。</param>
    /// <returns>进程退出码。</returns>
    private static int WriteEngineList(CliCommandLine commandLine, IYokiFrameClient client)
    {
        var detail = CliStatusProjection.ParseDetail(commandLine);
        var entries = client.ReadEngineEntries();
        JsonArray engines = new();
        foreach (var entry in entries)
        {
            engines.Add(detail == CliStatusProjection.FULL_DETAIL
                ? CliJsonOutput.ToJsonNode(entry)
                : CreateEngineSummaryJson(entry));
        }

        JsonObject payload = new()
        {
            ["command"] = "engine list",
            ["state"] = entries.Count == 0 ? CliStatusProjection.UNAVAILABLE : CliStatusProjection.READY,
            ["engines"] = engines
        };
        if (detail == CliStatusProjection.FULL_DETAIL)
        {
            payload["enginesRoot"] = client.Paths.EnginesRoot;
        }

        if (entries.Count != 0)
        {
            return CliJsonOutput.WriteSuccess(payload);
        }

        return CliJsonOutput.WriteError(
            new YokiFrameError(
                "NoEngineOnline",
                "No engine host is registered in this project.",
                "Start the Unity Editor or Godot host, then run engine list again.",
                CliStatusProjection.CompactPaths(client.Paths.ProjectRoot, new[] { client.Paths.EnginesRoot })),
            payload);
    }

    /// <summary>
    /// 生成单个 engine 的默认摘要；省略 registry 绝对路径和 FastChannel action 全量列表。
    /// </summary>
    /// <param name="entry">engine registry 条目。</param>
    /// <returns>AI 默认可见的 engine 摘要。</returns>
    private static JsonObject CreateEngineSummaryJson(EngineRegistryEntry entry)
    {
        JsonArray capabilities = new();
        foreach (var capability in entry.Capabilities)
        {
            capabilities.Add(JsonValue.Create(capability));
        }

        return new JsonObject
        {
            ["engineId"] = entry.EngineId,
            ["engine"] = entry.Engine,
            ["version"] = entry.Version,
            ["mode"] = entry.Mode,
            ["generation"] = entry.Generation,
            ["capabilities"] = capabilities,
            ["fastChannel"] = entry.FastChannels.Count > 0
        };
    }

    /// <summary>
    /// 输出指定 snapshot；默认展开内嵌 payload 为 summary，原始节点和协议路径留给 --detail full。
    /// </summary>
    /// <param name="commandLine">已解析命令行。</param>
    /// <param name="client">FileBridge 客户端。</param>
    /// <returns>进程退出码。</returns>
    private static int WriteSnapshot(CliCommandLine commandLine, IYokiFrameClient client)
    {
        var engineId = ResolveEngineId(commandLine, client);
        var kit = commandLine.GetOption("kit", "System");
        var name = commandLine.GetOption("name", "state");
        var detail = CliStatusProjection.ParseDetail(commandLine);
        var data = client.ReadSnapshot(engineId, kit, name);
        if (detail == CliStatusProjection.FULL_DETAIL)
        {
            return CliJsonOutput.WriteSuccess(new JsonObject
            {
                ["command"] = "snapshot read",
                ["engineId"] = engineId,
                ["kit"] = kit,
                ["name"] = name,
                ["state"] = CliStatusProjection.READY,
                ["path"] = client.Paths.GetSnapshotPath(engineId, kit, name),
                ["data"] = data
            });
        }

        JsonObject payload = CliStatusProjection.CreateKitEnvelope(
            "snapshot read",
            engineId,
            kit,
            CliStatusProjection.READY,
            "snapshot");
        payload["name"] = name;
        if (data["generation"] is JsonValue generation && generation.TryGetValue<long>(out var generationValue))
        {
            payload["generation"] = generationValue;
        }

        if (data["sequence"] is JsonValue sequence && sequence.TryGetValue<long>(out var sequenceValue))
        {
            payload["sequence"] = sequenceValue;
        }

        var payloadJson = data["payloadJson"] is JsonValue payloadValue && payloadValue.TryGetValue<string>(out var text)
            ? text
            : string.Empty;
        if (CliStatusProjection.TryParseSummary(payloadJson, out var summary))
        {
            payload["summary"] = summary;
        }
        else
        {
            ((JsonArray)payload["issues"]!).Add(CliStatusProjection.CreateIssue(
                "SnapshotSummaryLimited",
                CliStatusProjection.SEVERITY_WARNING,
                "Snapshot payload exceeds the default summary budget; use --detail full for the raw node.",
                false));
        }

        return CliJsonOutput.WriteSuccess(payload);
    }

    /// <summary>
    /// 输出指定 engine 的 FileBridge 队列状态；默认只给队列指标和 heartbeat 新鲜度，不含协议目录绝对路径。
    /// </summary>
    /// <param name="commandLine">已解析命令行。</param>
    /// <param name="client">FileBridge 客户端。</param>
    /// <returns>进程退出码。</returns>
    private static int WriteBridgeStatus(CliCommandLine commandLine, IYokiFrameClient client)
    {
        var engineId = ResolveEngineId(commandLine, client);
        var detail = CliStatusProjection.ParseDetail(commandLine);
        var nowUtc = DateTimeOffset.UtcNow;
        var status = client.ReadBridgeStatus(engineId);
        var state = ResolveBridgeState(status, nowUtc);
        if (detail == CliStatusProjection.FULL_DETAIL)
        {
            return CliJsonOutput.WriteSuccess(new JsonObject
            {
                ["command"] = "bridge status",
                ["engineId"] = engineId,
                ["state"] = state,
                ["status"] = status.ToJson(nowUtc, WorkbenchDoctorService.HeartbeatStaleThreshold)
            });
        }

        JsonObject payload = new()
        {
            ["command"] = "bridge status",
            ["engineId"] = engineId,
            ["state"] = state,
            ["summary"] = CreateBridgeSummaryJson(status, nowUtc),
            ["issues"] = new JsonArray(),
            ["detailAvailable"] = true
        };
        if (status.DeadletterCount > 0)
        {
            ((JsonArray)payload["issues"]!).Add(CliStatusProjection.CreateIssue(
                "DeadletterPresent",
                CliStatusProjection.SEVERITY_WARNING,
                "Deadletter evidence exists; inspect it before sending more commands.",
                false));
        }

        if (CliStatusProjection.IsUsable(state))
        {
            return CliJsonOutput.WriteSuccess(payload);
        }

        payload["nextActions"] = new JsonArray(JsonValue.Create("doctor"));
        var heartbeat = status.Heartbeat;
        return CliJsonOutput.WriteError(
            new YokiFrameError(
                heartbeat == null ? "BridgeHostOffline" : "BridgeHeartbeatStale",
                heartbeat == null
                    ? "No engine heartbeat was found for this engine."
                    : "Engine heartbeat is stale.",
                "Start or restart the engine host, then run bridge status again.",
                CliStatusProjection.CompactPaths(client.Paths.ProjectRoot, new[] { client.Paths.GetHeartbeatPath(engineId) }),
                null,
                engineId),
            payload);
    }

    /// <summary>
    /// 根据 heartbeat 与队列事实判定 FileBridge 可用状态；缺失 heartbeat 表示宿主未运行。
    /// </summary>
    /// <param name="status">FileBridge 状态。</param>
    /// <param name="nowUtc">当前 UTC 时间。</param>
    /// <returns>AI 可见状态词。</returns>
    private static string ResolveBridgeState(FileBridgeStatus status, DateTimeOffset nowUtc)
    {
        var heartbeat = status.Heartbeat;
        if (heartbeat == null)
        {
            return CliStatusProjection.UNAVAILABLE;
        }

        if (heartbeat.IsStale(nowUtc, WorkbenchDoctorService.HeartbeatStaleThreshold))
        {
            return CliStatusProjection.STALE;
        }

        return status.DeadletterCount > 0 || status.BackpressureActive
            ? CliStatusProjection.DEGRADED
            : CliStatusProjection.READY;
    }

    /// <summary>
    /// 生成 FileBridge 默认摘要；省略协议目录绝对路径、保留策略和 heartbeat 文件路径。
    /// </summary>
    /// <param name="status">FileBridge 状态。</param>
    /// <param name="nowUtc">当前 UTC 时间。</param>
    /// <returns>队列与 heartbeat 摘要。</returns>
    private static JsonObject CreateBridgeSummaryJson(FileBridgeStatus status, DateTimeOffset nowUtc)
    {
        var heartbeat = status.Heartbeat;
        JsonObject summary = new()
        {
            ["pending"] = status.PendingCount,
            ["processing"] = status.ProcessingCount,
            ["deadletter"] = status.DeadletterCount,
            ["results"] = status.ResultCount,
            ["protocolFileCount"] = status.ProtocolFileCount,
            ["backpressure"] = status.BackpressureActive
        };
        if (heartbeat == null)
        {
            return summary;
        }

        summary["mode"] = heartbeat.Mode;
        summary["generation"] = heartbeat.Generation;
        summary["heartbeatAgeSeconds"] = Math.Max(0L, (long)(nowUtc - heartbeat.CreatedAtUtc).TotalSeconds);
        summary["heartbeatStale"] = heartbeat.IsStale(nowUtc, WorkbenchDoctorService.HeartbeatStaleThreshold);
        return summary;
    }

    /// <summary>
    /// 输出 FileBridge 只读诊断结果，供脚本和 Workbench 快速判断是否需要回落或提示用户。
    /// </summary>
    /// <param name="commandLine">已解析命令行。</param>
    /// <param name="client">FileBridge 客户端。</param>
    /// <returns>进程退出码。</returns>
    private static int WriteDoctor(CliCommandLine commandLine, IYokiFrameClient client)
    {
        var requestedEngineId = commandLine.GetOption("engine", string.Empty);
        var detail = CliStatusProjection.ParseDetail(commandLine);
        var report = new WorkbenchDoctorService(client).Analyze(requestedEngineId);
        JsonObject payload = new()
        {
            ["command"] = "doctor",
            ["engineId"] = report.EngineId,
            ["state"] = CliStatusProjection.FromDoctorLevel(report.Level),
            ["level"] = report.Level,
            ["issueCount"] = report.IssueCount,
            ["issues"] = CreateDoctorIssueArray(report.Issues)
        };
        if (detail == CliStatusProjection.FULL_DETAIL)
        {
            payload["status"] = report.Status.ToJson(report.GeneratedAtUtc, WorkbenchDoctorService.HeartbeatStaleThreshold);
            return CliJsonOutput.WriteSuccess(payload);
        }

        payload["summary"] = CreateBridgeSummaryJson(report.Status, report.GeneratedAtUtc);
        payload["detailAvailable"] = true;
        if (report.IssueCount > 0)
        {
            // doctor 的诊断结果本身可用，Degraded 仍为 ok=true；问题内容通过 issues 和 nextActions 传达。
            payload["nextActions"] = new JsonArray(JsonValue.Create("bridge status"), JsonValue.Create("doctor --detail full"));
        }

        return CliJsonOutput.WriteSuccess(payload);
    }

    /// <summary>
    /// 把 doctor 诊断投影为统一 issue 结构，保留证据路径并补齐 severity 和 retryable。
    /// </summary>
    /// <param name="issues">doctor 诊断问题列表。</param>
    /// <returns>统一 issue 数组。</returns>
    private static JsonArray CreateDoctorIssueArray(IReadOnlyList<WorkbenchDoctorIssue> issues)
    {
        JsonArray array = new();
        foreach (var issue in issues)
        {
            array.Add(CliStatusProjection.CreateIssue(
                issue.Code,
                CliStatusProjection.SEVERITY_WARNING,
                issue.Message,
                true,
                issue.EvidencePaths));
        }

        return array;
    }

    /// <summary>
    /// 写入命令并输出 Runtime response。
    /// </summary>
    /// <param name="commandLine">已解析命令行。</param>
    /// <param name="client">FileBridge 客户端。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>进程退出码。</returns>
    private static async Task<int> WriteCommandSendAsync(
        CliCommandLine commandLine,
        IYokiFrameClient client,
        CancellationToken cancellationToken)
    {
        var requestedEngineId = commandLine.GetOption("engine", string.Empty);
        var kit = commandLine.GetOption("kit", "System");
        var action = commandLine.GetOption("action", "ping");
        var payloadJson = commandLine.GetOption("payload", "{}");
        var source = commandLine.GetOption("source", "cli");
        var timeoutMs = commandLine.GetIntOption("timeout", 10000);
        var result = await new CommandExecutionService(client).ExecuteAsync(
            requestedEngineId,
            kit,
            action,
            payloadJson,
            source,
            timeoutMs,
            cancellationToken).ConfigureAwait(false);

        // 失败时必须保留请求证据路径：脚本和 AI 需要按 requestId 复查 terminal response。
        var detail = CliStatusProjection.ParseDetail(commandLine);
        var failed = !string.Equals(result.Response.Status, "Success", StringComparison.OrdinalIgnoreCase);
        JsonObject payload = new()
        {
            ["command"] = "command send",
            ["requestId"] = result.Response.RequestId,
            ["outcome"] = result.Outcome.ToString(),
            ["response"] = CliJsonOutput.ToJsonNode(result.Response)
        };
        // 传输与请求证据是 L2 信息：默认只保留相对路径证据，绝对路径仅在 --detail full 出现。
        if (detail == CliStatusProjection.FULL_DETAIL)
        {
            payload["transport"] = result.Transport;
            payload["commandPath"] = result.CommandPath;
            payload["responsePath"] = result.ResponsePath;
        }

        if (failed)
        {
            var evidencePaths = CliStatusProjection.CompactPaths(
                    client.Paths.ProjectRoot,
                    new[] { result.Evidence.CommandPath, result.Evidence.ResponsePath })
                .ToArray();
            var errorEngineId = string.IsNullOrWhiteSpace(result.Response.EngineId)
                ? requestedEngineId
                : result.Response.EngineId;
            return CliJsonOutput.WriteError(new YokiFrameError(
                result.Response.ErrorCode,
                result.Response.ErrorMessage,
                "Inspect the terminal response and evidence paths, correct the command or engine state, then retry.",
                evidencePaths,
                result.RequestId,
                errorEngineId,
                result.Transport), payload);
        }

        return CliJsonOutput.WriteSuccess(payload);
    }

    /// <summary>
    /// 按 requestId 读取可靠 FileBridge 状态，供 timeout 后只读确认而不是自动重放 mutation。
    /// </summary>
    /// <param name="commandLine">已解析命令行。</param>
    /// <param name="client">FileBridge 客户端。</param>
    /// <returns>进程退出码。</returns>
    private static int WriteCommandStatus(CliCommandLine commandLine, IYokiFrameClient client)
    {
        var engineId = ResolveEngineId(commandLine, client);
        var requestId = commandLine.GetOption("request-id", string.Empty);
        var status = client.ReadCommandStatus(engineId, requestId);
        JsonObject payload = new()
        {
            ["command"] = "command status",
            ["engineId"] = engineId,
            ["requestId"] = requestId,
            ["state"] = status.State.ToString(),
            ["terminal"] = status.IsTerminal,
            ["updatedAtUtc"] = status.UpdatedAtUtc?.ToString("O"),
            ["evidencePaths"] = CliJsonOutput.ToJsonNode(status.EvidencePaths),
            ["response"] = status.Response == null ? null : CliJsonOutput.ToJsonNode(status.Response)
        };
        return status.State == YokiFrame.Protocol.FileBridge.CommandRequestState.NotFound
            ? CliJsonOutput.WriteError(
                new YokiFrameError(
                    "CommandStatusNotFound",
                    $"Request {requestId} was not found in the current FileBridge evidence directories.",
                    "Verify --engine and --request-id, or inspect the engine retention policy.",
                    status.EvidencePaths,
                    requestId,
                    engineId,
                    "file-bridge"),
                payload)
            : CliJsonOutput.WriteSuccess(payload);
    }

    /// <summary>
    /// 读取 shared memory telemetry 最新帧；默认只输出状态摘要，协议原始字段需要 --detail full。
    /// </summary>
    /// <param name="commandLine">已解析命令行。</param>
    /// <param name="client">FileBridge 客户端，用于推断当前 engine generation。</param>
    /// <returns>进程退出码。</returns>
    private static int WriteTelemetryRead(CliCommandLine commandLine, IYokiFrameClient client)
    {
        var engineId = ResolveEngineId(commandLine, client);
        var kit = commandLine.GetOption("kit", "System");
        var name = commandLine.GetOption("name", "state");
        var detail = CliStatusProjection.ParseDetail(commandLine);
        var maxPayloadBytes = commandLine.GetIntOption(
            "maxPayload",
            SharedMemoryTelemetryFrameReader.DEFAULT_MAX_PAYLOAD_BYTES);
        var expectedGeneration = ResolveExpectedGeneration(commandLine, client, engineId);
        var result = client.ReadTelemetry(engineId, kit, name, expectedGeneration, maxPayloadBytes);
        if (detail == CliStatusProjection.FULL_DETAIL)
        {
            return WriteTelemetryFullDetail(client, engineId, kit, name, expectedGeneration, result);
        }

        var state = CliStatusProjection.FromTelemetryFrame(result.Status);
        JsonObject payload = CliStatusProjection.CreateKitEnvelope(
            "telemetry read",
            engineId,
            kit,
            state,
            "telemetry");
        if (result.Header != null)
        {
            payload["generation"] = result.Header.Generation;
            payload["sequence"] = result.Header.Sequence;
        }

        if (!result.IsAccepted)
        {
            return WriteTelemetryFailure(payload, client, engineId, kit, name, state, result.Status);
        }

        if (CliStatusProjection.TryParseSummary(result.PayloadJson, out var summary))
        {
            payload["summary"] = summary;
        }
        else
        {
            ((JsonArray)payload["issues"]!).Add(CliStatusProjection.CreateIssue(
                "TelemetrySummaryLimited",
                CliStatusProjection.SEVERITY_WARNING,
                "Telemetry payload exceeds the default summary budget; use --detail full for the raw payload.",
                false));
        }

        return CliJsonOutput.WriteSuccess(payload);
    }

    /// <summary>
    /// 输出 telemetry 完整分层：保留 segment、generation 期望值、header 校验字段和原始 payload。
    /// </summary>
    /// <param name="client">FileBridge 客户端。</param>
    /// <param name="engineId">目标 engine。</param>
    /// <param name="kit">目标 Kit。</param>
    /// <param name="name">telemetry 名称。</param>
    /// <param name="expectedGeneration">期望 generation。</param>
    /// <param name="result">telemetry 读取结果。</param>
    /// <returns>进程退出码。</returns>
    private static int WriteTelemetryFullDetail(
        IYokiFrameClient client,
        string engineId,
        string kit,
        string name,
        long? expectedGeneration,
        SharedMemoryTelemetryFrameReadResult result)
    {
        JsonObject payload = new()
        {
            ["command"] = "telemetry read",
            ["engineId"] = engineId,
            ["kit"] = kit,
            ["name"] = name,
            ["detail"] = CliStatusProjection.FULL_DETAIL,
            ["segment"] = SharedMemoryTelemetrySegmentName.Create(client.Paths.ProjectRoot, engineId, kit, name),
            ["expectedGeneration"] = expectedGeneration,
            ["result"] = CreateTelemetryResultJson(result)
        };
        return CliJsonOutput.WriteSuccess(payload);
    }

    /// <summary>
    /// 输出 telemetry 不可用时的标准失败 envelope，并指出可回落的 snapshot 证据。
    /// </summary>
    /// <param name="payload">已填充公共字段的 envelope。</param>
    /// <param name="client">FileBridge 客户端。</param>
    /// <param name="engineId">目标 engine。</param>
    /// <param name="kit">目标 Kit。</param>
    /// <param name="name">telemetry 名称。</param>
    /// <param name="state">AI 可见状态词。</param>
    /// <param name="status">telemetry 帧读取状态。</param>
    /// <returns>失败退出码。</returns>
    private static int WriteTelemetryFailure(
        JsonObject payload,
        IYokiFrameClient client,
        string engineId,
        string kit,
        string name,
        string state,
        SharedMemoryTelemetryFrameStatus status)
    {
        var (code, message, retryable) = DescribeTelemetryFailure(status);
        ((JsonArray)payload["issues"]!).Add(CliStatusProjection.CreateIssue(
            code,
            CliStatusProjection.SEVERITY_ERROR,
            message,
            retryable));
        payload["nextActions"] = new JsonArray(JsonValue.Create("kit status"));
        return CliJsonOutput.WriteError(
            new YokiFrameError(
                CliStatusProjection.ToErrorCode(state),
                message,
                "Retry the read, or fall back to snapshot read for this Kit.",
                CliStatusProjection.CompactPaths(
                    client.Paths.ProjectRoot,
                    new[] { client.Paths.GetSnapshotPath(engineId, kit, name) }),
                null,
                engineId),
            payload);
    }

    /// <summary>
    /// 把 telemetry 读取失败状态翻译为稳定错误码、说明和可重试性，保证 AI 不需要理解 reader 内部枚举。
    /// </summary>
    /// <param name="status">telemetry 帧读取状态。</param>
    /// <returns>错误码、说明和是否可重试。</returns>
    private static (string Code, string Message, bool Retryable) DescribeTelemetryFailure(
        SharedMemoryTelemetryFrameStatus status)
    {
        switch (status)
        {
            case SharedMemoryTelemetryFrameStatus.Unavailable:
                return ("TelemetrySegmentUnavailable", "Telemetry shared memory segment is not published for this Kit.", true);
            case SharedMemoryTelemetryFrameStatus.GenerationMismatch:
                return ("TelemetryGenerationMismatch", "Telemetry frame generation does not match the current engine generation.", true);
            case SharedMemoryTelemetryFrameStatus.Writing:
                return ("TelemetryWriterBusy", "The telemetry writer is publishing a frame; retry the read.", true);
            case SharedMemoryTelemetryFrameStatus.HalfWrite:
                return ("TelemetryHalfWrite", "The telemetry frame changed while reading; retry the read.", true);
            case SharedMemoryTelemetryFrameStatus.CrcMismatch:
                return ("TelemetryCrcMismatch", "Telemetry payload failed CRC validation.", true);
            case SharedMemoryTelemetryFrameStatus.EngineIdHashMismatch:
                return ("TelemetryEngineMismatch", "Telemetry segment belongs to a different engine host.", false);
            case SharedMemoryTelemetryFrameStatus.UnsupportedVersion:
                return ("TelemetryVersionUnsupported", "Telemetry frame protocol version is not supported by this CLI.", false);
            case SharedMemoryTelemetryFrameStatus.PayloadTooLarge:
            case SharedMemoryTelemetryFrameStatus.BufferTooSmall:
                return ("TelemetryPayloadTooLarge", "Telemetry payload exceeds the allowed read size.", false);
            case SharedMemoryTelemetryFrameStatus.InvalidUtf8:
                return ("TelemetryPayloadInvalid", "Telemetry payload is not valid UTF-8.", false);
            case SharedMemoryTelemetryFrameStatus.InvalidMagic:
                return ("TelemetrySegmentInvalid", "The shared memory segment is not a YokiFrame telemetry frame.", false);
            default:
                return ("TelemetryReadFailed", "Telemetry frame could not be read for this Kit.", true);
        }
    }

    /// <summary>
    /// 解析 telemetry generation；显式参数优先，缺失时尝试使用 heartbeat generation。
    /// </summary>
    /// <param name="commandLine">已解析命令行。</param>
    /// <param name="client">FileBridge 客户端。</param>
    /// <param name="engineId">目标 engine。</param>
    /// <returns>期望 generation；无法推断时返回 null。</returns>
    internal static long? ResolveExpectedGeneration(
        CliCommandLine commandLine,
        IYokiFrameClient client,
        string engineId)
    {
        if (commandLine.TryGetLongOption("generation", out var explicitGeneration))
        {
            return explicitGeneration;
        }

        try
        {
            var heartbeat = client.ReadHeartbeat(engineId);
            return heartbeat != null && heartbeat.Generation != 0L ? heartbeat.Generation : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// 把 telemetry 读取结果转换为 CLI compact JSON。
    /// </summary>
    /// <param name="result">telemetry 读取结果。</param>
    /// <returns>JSON 节点。</returns>
    private static JsonObject CreateTelemetryResultJson(SharedMemoryTelemetryFrameReadResult result)
    {
        JsonObject payload = new()
        {
            ["status"] = result.Status.ToString(),
            ["accepted"] = result.IsAccepted,
            ["message"] = result.Message,
            ["payloadJson"] = result.PayloadJson,
            ["header"] = result.Header == null ? null : CreateTelemetryHeaderJson(result.Header)
        };
        return payload;
    }

    /// <summary>
    /// 把 telemetry header 转换为 CLI 可读 JSON。
    /// </summary>
    /// <param name="header">telemetry header。</param>
    /// <returns>JSON 节点。</returns>
    private static JsonObject CreateTelemetryHeaderJson(SharedMemoryTelemetryFrameHeader header)
    {
        return new JsonObject
        {
            ["protocolVersion"] = header.ProtocolVersion,
            ["engineIdHash"] = header.EngineIdHash.ToString("X16"),
            ["generation"] = header.Generation,
            ["sequence"] = header.Sequence,
            ["writtenAtUtcTicks"] = header.WrittenAtUtcTicks,
            ["payloadLength"] = header.PayloadLength,
            ["payloadCrc32"] = header.PayloadCrc32.ToString("X8"),
            ["writeState"] = header.WriteState.ToString()
        };
    }

    /// <summary>
    /// 解析 CLI 本次操作的目标 engine；未显式指定时只允许选择唯一在线 engine。
    /// </summary>
    /// <param name="commandLine">已解析命令行。</param>
    /// <param name="client">统一 YokiFrame Client。</param>
    /// <returns>安全 engine 标识。</returns>
    internal static string ResolveEngineId(CliCommandLine commandLine, IYokiFrameClient client)
    {
        var requestedEngineId = commandLine.GetOption("engine", string.Empty);
        return new EngineSelectionService(client).Resolve(requestedEngineId, DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// 尝试回收项目旧协议证据；清理异常只写入标准错误，不改变 CLI 主命令结果。
    /// </summary>
    /// <param name="projectRoot">当前 CLI 目标项目根目录。</param>
    private static void TryPruneProjectStorage(string projectRoot)
    {
        try
        {
            var report = YokiFrameFileBridgePruner.Prune(projectRoot);
            if (report.HasFailures)
            {
                CliJsonOutput.AddWarning(
                    "YokiFrame storage cleanup deferred because some files were unavailable.");
            }
        }
        catch (IOException exception)
        {
            CliJsonOutput.AddWarning("YokiFrame storage cleanup skipped: " + exception.Message);
        }
        catch (UnauthorizedAccessException exception)
        {
            CliJsonOutput.AddWarning("YokiFrame storage cleanup skipped: " + exception.Message);
        }
    }

    /// <summary>
    /// 解析项目根目录；显式 --project 优先，否则从当前目录向上寻找 `.yokiframe`。
    /// </summary>
    /// <param name="commandLine">已解析命令行。</param>
    /// <returns>项目根目录完整路径。</returns>
    private static string ResolveProjectRoot(CliCommandLine commandLine)
    {
        var explicitRoot = commandLine.GetOption("project", string.Empty);
        if (!string.IsNullOrWhiteSpace(explicitRoot))
        {
            return Path.GetFullPath(explicitRoot);
        }

        var current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current != null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, YokiFrameFileBridgeLayout.YOKIFRAME_DIRECTORY)))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return Directory.GetCurrentDirectory();
    }
}
