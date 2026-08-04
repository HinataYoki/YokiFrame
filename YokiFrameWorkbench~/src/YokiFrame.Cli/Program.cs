using System.Text.Json.Nodes;
using YokiFrame;
using YokiFrame.Client;
using YokiFrame.Cli;
using YokiFrame.Protocol.Results;
using YokiFrame.Protocol.Telemetry.SharedMemory;
using YokiFrame.Tooling.Application.Capabilities;
using YokiFrame.Tooling.Application.Engines;
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

        if (commandLine.IsCommand("engine", "list"))
        {
            return WriteEngineList(client);
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
            "Use project status/refresh, player build, spatialkit stats/indexes/density/analyze, audio index scan/generate, localization search/check/add/template generate, doctor, harness status/catalog, engine list, snapshot read, command send/status, bridge status, telemetry read or fastchannel status.",
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
    /// 输出当前 engine registry 列表。
    /// </summary>
    /// <param name="client">FileBridge 客户端。</param>
    /// <returns>进程退出码。</returns>
    private static int WriteEngineList(IYokiFrameClient client)
    {
        JsonArray engines = new();
        foreach (var entry in client.ReadEngineEntries())
        {
            engines.Add(CliJsonOutput.ToJsonNode(entry));
        }

        JsonObject payload = new()
        {
            ["command"] = "engine list",
            ["enginesRoot"] = client.Paths.EnginesRoot,
            ["engines"] = engines
        };
        return CliJsonOutput.WriteSuccess(payload);
    }

    /// <summary>
    /// 输出指定 snapshot 内容。
    /// </summary>
    /// <param name="commandLine">已解析命令行。</param>
    /// <param name="client">FileBridge 客户端。</param>
    /// <returns>进程退出码。</returns>
    private static int WriteSnapshot(CliCommandLine commandLine, IYokiFrameClient client)
    {
        var engineId = ResolveEngineId(commandLine, client);
        var kit = commandLine.GetOption("kit", "System");
        var name = commandLine.GetOption("name", "state");
        JsonObject payload = new()
        {
            ["command"] = "snapshot read",
            ["engineId"] = engineId,
            ["kit"] = kit,
            ["name"] = name,
            ["path"] = client.Paths.GetSnapshotPath(engineId, kit, name),
            ["data"] = client.ReadSnapshot(engineId, kit, name)
        };
        return CliJsonOutput.WriteSuccess(payload);
    }

    /// <summary>
    /// 输出指定 engine 的 FileBridge 队列状态。
    /// </summary>
    /// <param name="commandLine">已解析命令行。</param>
    /// <param name="client">FileBridge 客户端。</param>
    /// <returns>进程退出码。</returns>
    private static int WriteBridgeStatus(CliCommandLine commandLine, IYokiFrameClient client)
    {
        var engineId = ResolveEngineId(commandLine, client);
        var nowUtc = DateTimeOffset.UtcNow;
        JsonObject payload = new()
        {
            ["command"] = "bridge status",
            ["status"] = client.ReadBridgeStatus(engineId).ToJson(nowUtc, WorkbenchDoctorService.HeartbeatStaleThreshold)
        };
        return CliJsonOutput.WriteSuccess(payload);
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
        var report = new WorkbenchDoctorService(client).Analyze(requestedEngineId);
        JsonObject payload = new()
        {
            ["command"] = "doctor",
            ["engineId"] = report.EngineId,
            ["level"] = report.Level,
            ["issueCount"] = report.IssueCount,
            ["issues"] = CliJsonOutput.ToJsonNode(report.Issues.ToArray()),
            ["status"] = report.Status.ToJson(report.GeneratedAtUtc, WorkbenchDoctorService.HeartbeatStaleThreshold)
        };
        return CliJsonOutput.WriteSuccess(payload);
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

        JsonObject payload = new()
        {
            ["command"] = "command send",
            ["requestId"] = result.Response.RequestId,
            ["outcome"] = result.Outcome.ToString(),
            ["transport"] = result.Transport,
            ["commandPath"] = result.CommandPath,
            ["responsePath"] = result.ResponsePath,
            ["response"] = CliJsonOutput.ToJsonNode(result.Response)
        };
        if (!string.Equals(result.Response.Status, "Success", StringComparison.OrdinalIgnoreCase))
        {
            var evidencePaths = new[]
                {
                    result.Evidence.CommandPath,
                    result.Evidence.ResponsePath
                }
                .Where(static path => !string.IsNullOrWhiteSpace(path))
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
    /// 从 shared memory telemetry segment 读取最新帧；不可用时输出状态供调用侧回落 snapshot。
    /// </summary>
    /// <param name="commandLine">已解析命令行。</param>
    /// <param name="client">FileBridge 客户端，用于推断当前 engine generation。</param>
    /// <returns>进程退出码。</returns>
    private static int WriteTelemetryRead(CliCommandLine commandLine, IYokiFrameClient client)
    {
        var engineId = ResolveEngineId(commandLine, client);
        var kit = commandLine.GetOption("kit", "System");
        var name = commandLine.GetOption("name", "state");
        var segmentName = SharedMemoryTelemetrySegmentName.Create(client.Paths.ProjectRoot, engineId, kit, name);
        var maxPayloadBytes = commandLine.GetIntOption(
            "maxPayload",
            SharedMemoryTelemetryFrameReader.DEFAULT_MAX_PAYLOAD_BYTES);
        var expectedGeneration = ResolveExpectedGeneration(commandLine, client, engineId);
        var result = client.ReadTelemetry(engineId, kit, name, expectedGeneration, maxPayloadBytes);

        JsonObject payload = new()
        {
            ["command"] = "telemetry read",
            ["engineId"] = engineId,
            ["kit"] = kit,
            ["name"] = name,
            ["segment"] = segmentName,
            ["expectedGeneration"] = expectedGeneration,
            ["result"] = CreateTelemetryResultJson(result)
        };
        return CliJsonOutput.WriteSuccess(payload);
    }

    /// <summary>
    /// 解析 telemetry generation；显式参数优先，缺失时尝试使用 heartbeat generation。
    /// </summary>
    /// <param name="commandLine">已解析命令行。</param>
    /// <param name="client">FileBridge 客户端。</param>
    /// <param name="engineId">目标 engine。</param>
    /// <returns>期望 generation；无法推断时返回 null。</returns>
    private static long? ResolveExpectedGeneration(
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
    private static string ResolveEngineId(CliCommandLine commandLine, IYokiFrameClient client)
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
