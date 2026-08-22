using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using YokiFrame.Client.Commands;
using YokiFrame.Client.FileBridge;
using YokiFrame.Client.FileBridge.Diagnostics;
using YokiFrame.Client.FileBridge.IO;
using YokiFrame.Protocol.FileBridge;
using YokiFrame.Protocol.Results;
using YokiFrame.Protocol.Validation;

namespace YokiFrame.Client.Transports.FileBridge;

/// <summary>
/// 提供工具侧访问 YokiFrame FileBridge 的最小 SDK。
/// </summary>
internal sealed class FileBridgeTransport
{
    // 覆盖 FileBridge 原子替换与跨进程短锁；挂载盘/高负载下需要更长窗口。
    private const int FILE_READ_RETRY_COUNT = 20;
    private static readonly TimeSpan DefaultPollInterval = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan FileReadRetryDelay = TimeSpan.FromMilliseconds(50);

    /// <summary>
    /// 使用项目根目录创建 FileBridge 客户端。
    /// </summary>
    /// <param name="projectRoot">Unity/Godot 项目根目录。</param>
    public FileBridgeTransport(string projectRoot)
    {
        Paths = new YokiFramePaths(projectRoot);
    }

    /// <summary>
    /// 获取路径解析器。
    /// </summary>
    public YokiFramePaths Paths { get; }

    /// <summary>
    /// 读取 harness capability 文件。
    /// </summary>
    /// <returns>capabilities JSON 节点。</returns>
    public JsonNode ReadHarnessCapabilities()
    {
        var path = Paths.GetHarnessCapabilitiesPath();
        return ReadJsonNode(path, "HarnessMissing", "Harness capabilities file was not found.");
    }

    /// <summary>
    /// 读取所有 engine registry 条目。
    /// </summary>
    /// <returns>registry 条目列表。</returns>
    public IReadOnlyList<EngineRegistryEntry> ReadEngineEntries()
    {
        PathSecurity.EnsureNoReparsePoint(Paths.ProjectRoot, Paths.EnginesRoot);
        if (!Directory.Exists(Paths.EnginesRoot))
        {
            return Array.Empty<EngineRegistryEntry>();
        }

        List<EngineRegistryEntry> entries = new();
        List<string> invalidPaths = new();
        List<string> invalidMessages = new();
        foreach (var engineDirectory in Directory.EnumerateDirectories(Paths.EnginesRoot))
        {
            PathSecurity.EnsureNoReparsePoint(Paths.EnginesRoot, engineDirectory);
            var registryPath = Path.Combine(engineDirectory, YokiFrameFileBridgeLayout.ENGINE_REGISTRY_FILE_NAME);
            if (File.Exists(registryPath))
            {
                try
                {
                    PathSecurity.EnsureNoReparsePoint(Paths.ProjectRoot, registryPath);
                    entries.Add(EngineRegistryEntry.FromJson(ReadAllTextWithRetry(registryPath)));
                }
                catch (JsonException exception)
                {
                    invalidPaths.Add(registryPath);
                    invalidMessages.Add(exception.Message);
                }
            }
        }

        if (invalidPaths.Count > 0)
        {
            throw new EngineRegistryReadException(
                entries,
                invalidPaths,
                "One or more engine registry files are invalid: " + string.Join("; ", invalidMessages));
        }

        return entries;
    }

    /// <summary>
    /// 读取指定 snapshot。
    /// </summary>
    /// <param name="engineId">目标 engine。</param>
    /// <param name="kit">目标 Kit。</param>
    /// <param name="name">snapshot 名称。</param>
    /// <returns>snapshot JSON 节点。</returns>
    public JsonNode ReadSnapshot(string engineId, string kit, string name)
    {
        var path = Paths.GetSnapshotPath(engineId, kit, name);
        return ReadJsonNode(path, "SnapshotMissing", "Snapshot file was not found.");
    }

    /// <summary>
    /// 读取指定 engine 的 heartbeat；文件缺失时返回 null。
    /// </summary>
    /// <param name="engineId">目标 engine。</param>
    /// <returns>heartbeat 信息或 null。</returns>
    public HeartbeatInfo? ReadHeartbeat(string engineId)
    {
        var path = Paths.GetHeartbeatPath(engineId);
        PathSecurity.EnsureNoReparsePoint(Paths.ProjectRoot, path);
        if (!File.Exists(path))
        {
            return null;
        }

        var node = ReadJsonNode(path, "HeartbeatInvalid", "Heartbeat file could not be read.");
        return HeartbeatInfo.FromJson(path, node);
    }

    /// <summary>
    /// 汇总指定 engine 的 FileBridge 状态。
    /// </summary>
    /// <param name="engineId">目标 engine。</param>
    /// <returns>bridge 状态快照。</returns>
    public FileBridgeStatus ReadBridgeStatus(string engineId)
    {
        var engineRoot = Paths.GetEngineRoot(engineId);
        var commandsRoot = Paths.GetCommandsRoot(engineId);
        var resultsRoot = Paths.GetResultsRoot(engineId);
        PathSecurity.EnsureNoReparsePoint(Paths.ProjectRoot, engineRoot);
        PathSecurity.EnsureNoReparsePoint(Paths.ProjectRoot, commandsRoot);
        PathSecurity.EnsureNoReparsePoint(Paths.ProjectRoot, resultsRoot);
        PathSecurity.EnsureNoReparsePoint(
            Paths.ProjectRoot,
            Path.Combine(commandsRoot, YokiFrameFileBridgeLayout.PROCESSING_DIRECTORY));
        PathSecurity.EnsureNoReparsePoint(
            Paths.ProjectRoot,
            Path.Combine(commandsRoot, YokiFrameFileBridgeLayout.ARCHIVE_DIRECTORY));
        PathSecurity.EnsureNoReparsePoint(
            Paths.ProjectRoot,
            Path.Combine(commandsRoot, YokiFrameFileBridgeLayout.DEADLETTER_DIRECTORY));
        var protocolStorage = ReadProtocolStorageDiagnostics(engineRoot);
        return new FileBridgeStatus(engineId, engineRoot, commandsRoot, resultsRoot)
        {
            PendingCount = CountJsonFiles(commandsRoot),
            ProcessingCount = CountJsonFiles(Path.Combine(commandsRoot, YokiFrameFileBridgeLayout.PROCESSING_DIRECTORY)),
            ArchiveCount = CountJsonFiles(Path.Combine(commandsRoot, YokiFrameFileBridgeLayout.ARCHIVE_DIRECTORY)),
            DeadletterCount = CountJsonFiles(Path.Combine(commandsRoot, YokiFrameFileBridgeLayout.DEADLETTER_DIRECTORY)),
            ResultCount = CountJsonFiles(resultsRoot),
            ProtocolFileCount = protocolStorage.FileCount,
            ProtocolBytes = protocolStorage.TotalBytes,
            OldestProtocolFileUtc = protocolStorage.OldestFileUtc,
            Heartbeat = ReadHeartbeat(engineId)
        };
    }

    /// <summary>
    /// 查询指定请求在 pending、processing、results、archive 或 deadletter 中的可观察状态。
    /// </summary>
    /// <param name="engineId">目标 engine。</param>
    /// <param name="requestId">安全请求标识。</param>
    /// <returns>请求状态和可复查证据。</returns>
    public CommandRequestStatus ReadCommandStatus(string engineId, string requestId)
    {
        var safeRequestId = SafeIdValidator.EnsureSafeId(requestId, nameof(requestId));
        var pendingPath = Paths.GetPendingCommandPath(engineId, safeRequestId);
        var commandsRoot = Paths.GetCommandsRoot(engineId);
        var processingRoot = PathSecurity.CombineInside(
            commandsRoot,
            YokiFrameFileBridgeLayout.PROCESSING_DIRECTORY);
        var processingPath = PathSecurity.CombineInside(
            processingRoot,
            safeRequestId + YokiFrameFileBridgeLayout.JSON_EXTENSION);
        var responsePath = Paths.GetResponsePath(engineId, safeRequestId);
        var archiveRoot = PathSecurity.CombineInside(commandsRoot, YokiFrameFileBridgeLayout.ARCHIVE_DIRECTORY);
        var deadletterRoot = PathSecurity.CombineInside(commandsRoot, YokiFrameFileBridgeLayout.DEADLETTER_DIRECTORY);
        EnsureReadablePath(pendingPath);
        EnsureReadablePath(processingPath);
        EnsureReadablePath(responsePath);
        EnsureReadablePath(archiveRoot);
        EnsureReadablePath(deadletterRoot);

        if (File.Exists(responsePath))
        {
            CommandResponse response;
            try
            {
                response = CommandResponse.FromJson(ReadAllTextWithRetry(responsePath));
            }
            catch (JsonException exception)
            {
                throw new YokiFrameProtocolException(new YokiFrameError(
                    "FileBridgeResponseInvalid",
                    $"FileBridge response JSON is invalid: {exception.Message}",
                    "Inspect the response evidence and retry after the engine adapter has refreshed its FileBridge state.",
                    new[] { responsePath },
                    safeRequestId,
                    engineId,
                    "file-bridge"));
            }

            CommandResponseValidator.Validate(
                response,
                YokiFrameFileBridgeContract.PROTOCOL_VERSION,
                safeRequestId,
                engineId,
                "FileBridgeResponseMismatch",
                "FileBridge response does not match the requested command.",
                "Inspect the response evidence and retry after the engine adapter has refreshed its FileBridge state.",
                new[] { responsePath },
                safeRequestId,
                engineId,
                "file-bridge");
            var state = string.Equals(response.Status, "Success", StringComparison.OrdinalIgnoreCase)
                ? CommandRequestState.Succeeded
                : CommandRequestState.Failed;
            return CreateRequestStatus(
                engineId,
                safeRequestId,
                state,
                response,
                new[] { responsePath },
                ParseUpdatedAt(response.CompletedAtUtc, responsePath));
        }

        if (File.Exists(processingPath))
        {
            return CreateRequestStatus(
                engineId,
                safeRequestId,
                CommandRequestState.Processing,
                null,
                new[] { processingPath },
                File.GetLastWriteTimeUtc(processingPath));
        }

        if (File.Exists(pendingPath))
        {
            return CreateRequestStatus(
                engineId,
                safeRequestId,
                CommandRequestState.Pending,
                null,
                new[] { pendingPath },
                File.GetLastWriteTimeUtc(pendingPath));
        }

        var archivePath = FindEvidencePath(archiveRoot, safeRequestId + YokiFrameFileBridgeLayout.JSON_EXTENSION);
        if (archivePath != null)
        {
            return CreateRequestStatus(
                engineId,
                safeRequestId,
                CommandRequestState.Succeeded,
                null,
                new[] { archivePath },
                File.GetLastWriteTimeUtc(archivePath));
        }

        var deadletterInfoPath = FindEvidencePath(
            deadletterRoot,
            safeRequestId + "-deadletter" + YokiFrameFileBridgeLayout.JSON_EXTENSION);
        var deadletterRequestPath = FindEvidencePath(
            deadletterRoot,
            safeRequestId + "-request" + YokiFrameFileBridgeLayout.JSON_EXTENSION);
        if (deadletterInfoPath != null || deadletterRequestPath != null)
        {
            var evidence = new[] { deadletterInfoPath, deadletterRequestPath }
                .Where(static path => !string.IsNullOrWhiteSpace(path))
                .Cast<string>()
                .ToArray();
            var state = GetDeadletterState(deadletterInfoPath);
            return CreateRequestStatus(
                engineId,
                safeRequestId,
                state,
                null,
                evidence,
                deadletterInfoPath == null ? null : File.GetLastWriteTimeUtc(deadletterInfoPath));
        }

        return CreateRequestStatus(
            engineId,
            safeRequestId,
            CommandRequestState.NotFound,
            null,
            Array.Empty<string>(),
            null);
    }

    /// <summary>
    /// 创建统一 request status DTO。
    /// </summary>
    private static CommandRequestStatus CreateRequestStatus(
        string engineId,
        string requestId,
        CommandRequestState state,
        CommandResponse? response,
        IReadOnlyList<string> evidencePaths,
        DateTimeOffset? updatedAtUtc)
    {
        return new CommandRequestStatus
        {
            ProtocolVersion = YokiFrameFileBridgeContract.PROTOCOL_VERSION,
            RequestId = requestId,
            EngineId = engineId,
            State = state,
            Response = response,
            EvidencePaths = evidencePaths,
            UpdatedAtUtc = updatedAtUtc
        };
    }

    /// <summary>
    /// 解析响应完成时间；格式异常时回落到文件写入时间，保留状态可观测性。
    /// </summary>
    private static DateTimeOffset ParseUpdatedAt(string? text, string fallbackPath)
    {
        return DateTimeOffset.TryParse(text, out var parsed)
            ? parsed.ToUniversalTime()
            : File.GetLastWriteTimeUtc(fallbackPath);
    }

    /// <summary>
    /// 在终态证据目录中查找精确文件名，避免把临时文件当作状态证据。
    /// </summary>
    private string? FindEvidencePath(string root, string fileName)
    {
        EnsureReadablePath(root);
        if (!Directory.Exists(root))
        {
            return null;
        }

        var path = Directory.EnumerateFiles(root, fileName, SearchOption.TopDirectoryOnly).FirstOrDefault();
        if (path != null)
        {
            EnsureReadablePath(path);
        }

        return path;
    }

    /// <summary>
    /// 在使用 FileBridge 文件前再次检查从项目根到最终路径的所有现存组件。
    /// </summary>
    /// <param name="path">待读取或写入的最终路径。</param>
    private void EnsureReadablePath(string path)
    {
        PathSecurity.EnsureNoReparsePoint(Paths.ProjectRoot, path);
    }

    /// <summary>
    /// 识别 Host 因 processing lease 过期而保留的终态证据。
    /// </summary>
    private static bool IsExpiredDeadletter(string? deadletterInfoPath)
    {
        if (string.IsNullOrWhiteSpace(deadletterInfoPath))
        {
            return false;
        }

        try
        {
            var node = JsonNode.Parse(ReadAllTextWithRetry(deadletterInfoPath));
            return string.Equals(
                node?["errorCode"]?.GetValue<string>(),
                "ProcessingExpired",
                StringComparison.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>
    /// 把 deadletter 诊断映射为可重试语义明确的 request status。
    /// </summary>
    /// <param name="deadletterInfoPath">deadletter 诊断文件路径。</param>
    /// <returns>Expired、Unknown 或普通 Deadletter。</returns>
    private static CommandRequestState GetDeadletterState(string? deadletterInfoPath)
    {
        if (IsExpiredDeadletter(deadletterInfoPath))
        {
            return CommandRequestState.Expired;
        }

        if (string.IsNullOrWhiteSpace(deadletterInfoPath))
        {
            return CommandRequestState.Deadletter;
        }

        try
        {
            var node = JsonNode.Parse(ReadAllTextWithRetry(deadletterInfoPath));
            return string.Equals(
                node?["errorCode"]?.GetValue<string>(),
                "CommandExecutionUnknown",
                StringComparison.OrdinalIgnoreCase)
                ? CommandRequestState.Unknown
                : CommandRequestState.Deadletter;
        }
        catch (JsonException)
        {
            return CommandRequestState.Deadletter;
        }
    }

    /// <summary>
    /// 写入命令并等待 Runtime 写入 terminal response。
    /// </summary>
    /// <param name="engineId">目标 engine。</param>
    /// <param name="kit">目标 Kit。</param>
    /// <param name="action">目标 action。</param>
    /// <param name="payloadJson">payload JSON 字符串。</param>
    /// <param name="source">命令来源。</param>
    /// <param name="timeoutMs">等待超时毫秒数。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>发送结果和 Runtime 响应。</returns>
    public async Task<CommandSendResult> SendCommandAsync(
        string engineId,
        string kit,
        string action,
        string payloadJson,
        string source,
        int timeoutMs,
        CancellationToken cancellationToken)
    {
        var requestId = CreateRequestId(source);
        var envelope = CommandEnvelope.Create(engineId, source, requestId, kit, action, payloadJson, timeoutMs);
        var commandPath = Paths.GetPendingCommandPath(engineId, requestId);
        var responsePath = Paths.GetResponsePath(engineId, requestId);
        EnsureReadablePath(commandPath);
        EnsureReadablePath(responsePath);
        YokiFrame.YokiFrameAtomicFileWriter.WriteAllText(commandPath, envelope.ToJson());
        CommandResponse response;
        try
        {
            response = await WaitForResponseAsync(
                    responsePath,
                    envelope,
                    commandPath,
                    Paths.ProjectRoot,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw new YokiFrameProtocolException(new YokiFrameError(
                "Cancelled",
                $"Command {requestId} was cancelled while waiting for FileBridge.",
                "Query command status before deciding whether a mutation may be retried.",
                new[] { commandPath, responsePath },
                requestId,
                envelope.EngineId,
                "file-bridge"));
        }

        return new CommandSendResult(envelope, commandPath, responsePath, response);
    }

    /// <summary>
    /// 生成安全且低碰撞的请求标识。
    /// </summary>
    /// <param name="source">命令来源。</param>
    /// <returns>安全请求标识。</returns>
    public static string CreateRequestId(string source)
    {
        var safeSource = string.IsNullOrWhiteSpace(source) ? YokiFrame.YokiFrameCommandSourceContract.CLI : source;
        var randomSuffix = RandomNumberGenerator.GetHexString(8).ToLowerInvariant();
        return $"{safeSource}-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}-{randomSuffix}";
    }

    /// <summary>
    /// 读取 JSON 文件并给缺失或解析失败提供标准错误。
    /// </summary>
    /// <param name="path">文件路径。</param>
    /// <param name="missingCode">文件缺失时使用的错误码。</param>
    /// <param name="missingMessage">文件缺失时使用的错误说明。</param>
    /// <returns>解析后的 JSON 节点。</returns>
    private JsonNode ReadJsonNode(string path, string missingCode, string missingMessage)
    {
        EnsureReadablePath(path);
        if (!File.Exists(path))
        {
            throw new YokiFrameProtocolException(new YokiFrameError(
                missingCode,
                missingMessage,
                "Start the engine adapter or verify the requested engine/kit/name.",
                new[] { path }));
        }

        try
        {
            return JsonNode.Parse(ReadAllTextWithRetry(path))
                ?? throw new JsonException("JSON document must contain an object or array, not null.");
        }
        catch (JsonException exception)
        {
            throw new YokiFrameProtocolException(new YokiFrameError(
                "InvalidJson",
                $"JSON file is invalid: {exception.Message}",
                "Inspect the evidence file and regenerate the FileBridge artifact.",
                new[] { path }));
        }
    }

    /// <summary>
    /// 统计目录中直接包含的 JSON 文件数量。
    /// </summary>
    /// <param name="directoryPath">待统计目录。</param>
    /// <returns>JSON 文件数量。</returns>
    private static int CountJsonFiles(string directoryPath)
    {
        if (!Directory.Exists(directoryPath)) return 0;
        var count = 0;
        foreach (var _ in Directory.EnumerateFiles(directoryPath, "*" + YokiFrameFileBridgeLayout.JSON_EXTENSION, SearchOption.TopDirectoryOnly)) count++;
        return count;
    }

    /// <summary>
    /// 统计 engine 协议目录下 JSON 证据文件的数量、体积和最旧更新时间。
    /// </summary>
    /// <param name="engineRoot">engine 协议根目录。</param>
    /// <returns>协议存储诊断摘要。</returns>
    private static ProtocolStorageDiagnostics ReadProtocolStorageDiagnostics(string engineRoot)
    {
        if (!Directory.Exists(engineRoot))
        {
            return new ProtocolStorageDiagnostics(0, 0L, null);
        }

        EnumerationOptions options = new()
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint
        };
        var fileCount = 0;
        var totalBytes = 0L;
        DateTimeOffset? oldestFileUtc = null;
        foreach (var path in Directory.EnumerateFiles(
                     engineRoot,
                     "*" + YokiFrameFileBridgeLayout.JSON_EXTENSION,
                     options))
        {
            var info = new FileInfo(path);
            fileCount++;
            totalBytes += info.Length;
            var writeTimeUtc = new DateTimeOffset(info.LastWriteTimeUtc);
            if (oldestFileUtc == null || writeTimeUtc < oldestFileUtc.Value)
            {
                oldestFileUtc = writeTimeUtc;
            }
        }

        return new ProtocolStorageDiagnostics(fileCount, totalBytes, oldestFileUtc);
    }

    /// <summary>
    /// 保存协议目录存储占用诊断结果。
    /// </summary>
    /// <param name="FileCount">JSON 文件数量。</param>
    /// <param name="TotalBytes">JSON 文件总字节数。</param>
    /// <param name="OldestFileUtc">最旧 JSON 文件最后写入时间。</param>
    private sealed record ProtocolStorageDiagnostics(int FileCount, long TotalBytes, DateTimeOffset? OldestFileUtc);

    /// <summary>
    /// 等待 Runtime 写入响应文件，超时后返回标准错误。
    /// </summary>
    /// <param name="responsePath">预期响应路径。</param>
    /// <param name="envelope">已写入的命令信封。</param>
    /// <param name="commandPath">命令文件路径。</param>
    /// <param name="projectRoot">当前 Client 绑定的项目根。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>读取到的响应。</returns>
    private static async Task<CommandResponse> WaitForResponseAsync(
        string responsePath,
        CommandEnvelope envelope,
        string commandPath,
        string projectRoot,
        CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow.AddMilliseconds(envelope.TimeoutMs);
        while (DateTimeOffset.UtcNow <= deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PathSecurity.EnsureNoReparsePoint(projectRoot, responsePath);
            if (File.Exists(responsePath))
            {
                var json = await ReadAllTextWithRetryAsync(responsePath, cancellationToken)
                    .ConfigureAwait(false);
                CommandResponse response;
                try
                {
                    response = CommandResponse.FromJson(json);
                }
                catch (JsonException exception)
                {
                    throw new YokiFrameProtocolException(new YokiFrameError(
                        "FileBridgeResponseInvalid",
                        $"FileBridge response JSON is invalid: {exception.Message}",
                        "Inspect the response evidence and retry after the engine adapter has refreshed its FileBridge state.",
                        new[] { commandPath, responsePath },
                        envelope.RequestId,
                        envelope.EngineId,
                        "file-bridge"));
                }

                return CommandResponseValidator.Validate(
                        response,
                        envelope,
                        "FileBridgeResponseMismatch",
                        "FileBridge response does not match the current command request.",
                        "Inspect the response evidence and retry after the engine adapter has refreshed its FileBridge state.",
                        new[] { commandPath, responsePath },
                        envelope.RequestId,
                        envelope.EngineId,
                        "file-bridge");
            }

            await Task.Delay(DefaultPollInterval, cancellationToken).ConfigureAwait(false);
        }

        throw new YokiFrameProtocolException(new YokiFrameError(
            "CommandTimeout",
            $"Command {envelope.RequestId} timed out after {envelope.TimeoutMs} ms.",
            "Query command status before deciding whether a mutation may be retried.",
            new[] { commandPath, responsePath },
            envelope.RequestId,
            envelope.EngineId,
            "file-bridge"));
    }

    /// <summary>
    /// 读取文件内容，并在 FileBridge 原子替换的短暂 IO 窗口内做小间隔重试。
    /// </summary>
    /// <param name="path">待读取文件路径。</param>
    /// <returns>文件文本内容。</returns>
    private static string ReadAllTextWithRetry(string path)
    {
        var attempt = 0;
        while (true)
        {
            try
            {
                return File.ReadAllText(path);
            }
            catch (IOException) when (attempt < FILE_READ_RETRY_COUNT)
            {
                attempt++;
                Thread.Sleep(FileReadRetryDelay);
            }
            catch (UnauthorizedAccessException) when (attempt < FILE_READ_RETRY_COUNT)
            {
                attempt++;
                Thread.Sleep(FileReadRetryDelay);
            }
        }
    }

    /// <summary>
    /// 异步读取文件内容，并兼容 FileBridge response 写入时的短暂替换窗口。
    /// </summary>
    /// <param name="path">待读取文件路径。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>文件文本内容。</returns>
    private static async Task<string> ReadAllTextWithRetryAsync(string path, CancellationToken cancellationToken)
    {
        var attempt = 0;
        while (true)
        {
            try
            {
                return await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
            }
            catch (IOException) when (attempt < FILE_READ_RETRY_COUNT)
            {
                attempt++;
                await Task.Delay(FileReadRetryDelay, cancellationToken).ConfigureAwait(false);
            }
            catch (UnauthorizedAccessException) when (attempt < FILE_READ_RETRY_COUNT)
            {
                attempt++;
                await Task.Delay(FileReadRetryDelay, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
