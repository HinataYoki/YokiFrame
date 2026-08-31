using System.Diagnostics;
using YokiFrame.Client;
using YokiFrame.Protocol.FileBridge;
using YokiFrame.Protocol.Results;
using YokiFrame.Tooling.Application.Engines;
using YokiFrame.Tooling.Application.Models;

namespace YokiFrame.Tooling.Application.Services;

/// <summary>
/// 为 Workbench、CLI 和 AI 自动化统一选择 FastChannel 或可靠 FileBridge。
/// </summary>
public sealed class CommandExecutionService
{
    private static readonly TimeSpan FastChannelInitialCooldown = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan FastChannelMaximumCooldown = TimeSpan.FromSeconds(10);
    private const int MAX_FAST_CHANNEL_HEALTH_ENTRIES = 32;
    private const string SYSTEM_KIT = "System";
    private const string FAST_CHANNEL_TRANSPORT = "fast-channel";
    private const string FILE_BRIDGE_TRANSPORT = "file-bridge";
    private readonly IEngineStateReader mStateReader;
    private readonly ICommandTransport mCommandTransport;
    private readonly IFastChannelCommandTransport? mFastChannelTransport;
    private readonly EngineSelectionService mEngineSelectionService;
    private readonly object mFastChannelHealthGate = new();
    private readonly Dictionary<string, FastChannelHealth> mFastChannelHealth = new(StringComparer.Ordinal);

    /// <summary>
    /// 使用统一 Client 创建命令执行用例，入口层无需理解具体传输协议。
    /// </summary>
    /// <param name="client">统一 YokiFrame Client。</param>
    public CommandExecutionService(IYokiFrameClient client)
        : this((IEngineStateReader)client, (ICommandTransport)client)
    {
    }

    /// <summary>
    /// 使用状态读取端口和命令传输端口创建用例，避免依赖完整 Client 聚合接口。
    /// </summary>
    /// <param name="stateReader">引擎状态读取端口。</param>
    /// <param name="commandTransport">命令传输端口。</param>
    public CommandExecutionService(
        IEngineStateReader stateReader,
        ICommandTransport commandTransport)
    {
        mStateReader = stateReader;
        mCommandTransport = commandTransport;
        mFastChannelTransport = commandTransport as IFastChannelCommandTransport;
        mEngineSelectionService = new EngineSelectionService(stateReader);
    }

    /// <summary>
    /// 发送命令并返回实际传输、证据路径和 terminal response。
    /// 当前 endpoint 明确声明的 ReadOnly command 会尝试一次 FastChannel。
    /// </summary>
    /// <param name="requestedEngineId">显式 engine 标识；为空时只自动选择唯一在线 engine。</param>
    /// <param name="kit">目标 Kit；空值归一化为 System。</param>
    /// <param name="action">目标 action。</param>
    /// <param name="payloadJson">命令 payload JSON。</param>
    /// <param name="source">审计来源。</param>
    /// <param name="timeoutMs">命令超时毫秒数。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>实际传输与宿主响应。</returns>
    public Task<CommandExecutionResult> ExecuteAsync(
        string requestedEngineId,
        string kit,
        string action,
        string payloadJson,
        string source,
        int timeoutMs,
        CancellationToken cancellationToken)
    {
        return ExecuteCoreAsync(
            requestedEngineId,
            kit,
            action,
            payloadJson,
            source,
            timeoutMs,
            cancellationToken,
            null);
    }

    /// <summary>
    /// 发送命令并绑定调用方捕获的宿主身份，用于 Workbench 结果门禁。
    /// </summary>
    /// <param name="requestedEngineId">显式 engine 标识。</param>
    /// <param name="kit">目标 Kit。</param>
    /// <param name="action">目标 action。</param>
    /// <param name="payloadJson">命令 payload JSON。</param>
    /// <param name="source">审计来源。</param>
    /// <param name="timeoutMs">命令超时毫秒数。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <param name="expectedIdentity">请求开始时捕获的宿主身份。</param>
    /// <returns>带目标身份的命令结果。</returns>
    public Task<CommandExecutionResult> ExecuteWithIdentityAsync(
        string requestedEngineId,
        string kit,
        string action,
        string payloadJson,
        string source,
        int timeoutMs,
        CancellationToken cancellationToken,
        HostIdentity? expectedIdentity)
    {
        return ExecuteCoreAsync(
            requestedEngineId,
            kit,
            action,
            payloadJson,
            source,
            timeoutMs,
            cancellationToken,
            expectedIdentity);
    }

    /// <summary>
    /// 执行一次命令并在需要时验证宿主身份。
    /// </summary>
    private async Task<CommandExecutionResult> ExecuteCoreAsync(
        string requestedEngineId,
        string kit,
        string action,
        string payloadJson,
        string source,
        int timeoutMs,
        CancellationToken cancellationToken,
        HostIdentity? expectedIdentity)
    {
        var selectedEngineId = mEngineSelectionService.Resolve(requestedEngineId, DateTimeOffset.UtcNow);
        var selectedKit = string.IsNullOrWhiteSpace(kit) ? SYSTEM_KIT : kit;
        var targetIdentity = expectedIdentity;
        EnsureExpectedEngine(selectedEngineId, expectedIdentity);
        var startTimestamp = Stopwatch.GetTimestamp();
        var fastChannelTimeoutMs = GetFastChannelBudget(timeoutMs);
        var fastChannelAttempted = false;
        if (mFastChannelTransport != null
            && fastChannelTimeoutMs > 0
            && ShouldTryFastChannel(selectedEngineId)
            && mFastChannelTransport.CanSendFastChannelReadOnlyCommand(selectedEngineId, selectedKit, action))
        {
            fastChannelAttempted = true;
            var fastChannelResponse = await TrySendFastChannelCommandAsync(
                selectedEngineId,
                selectedKit,
                action,
                payloadJson,
                source,
                fastChannelTimeoutMs,
                cancellationToken).ConfigureAwait(false);
            if (fastChannelResponse != null)
            {
                ClearFastChannelFailure(selectedEngineId);
                EnsureCurrentIdentity(selectedEngineId, expectedIdentity);
                return new CommandExecutionResult(
                    FAST_CHANNEL_TRANSPORT,
                    string.Empty,
                    string.Empty,
                    fastChannelResponse,
                    targetIdentity,
                    fastChannelResponse.RequestId,
                    CommandEvidence.Ephemeral(
                        "FastChannel returned a terminal response; no FileBridge file evidence was created."));
            }
        }

        if (fastChannelAttempted && !HasFileBridgeBudget(timeoutMs, startTimestamp))
        {
            throw CreateFastChannelTimeout(selectedEngineId);
        }

        var remainingTimeoutMs = GetRemainingTimeout(timeoutMs, startTimestamp);
        var fileBridgeResult = await mCommandTransport.SendCommandAsync(
            selectedEngineId,
            selectedKit,
            action,
            payloadJson,
            source,
            remainingTimeoutMs,
            cancellationToken).ConfigureAwait(false);
        EnsureCurrentIdentity(selectedEngineId, expectedIdentity);
        return new CommandExecutionResult(
            FILE_BRIDGE_TRANSPORT,
            fileBridgeResult.CommandPath,
            fileBridgeResult.ResponsePath,
            fileBridgeResult.Response,
            targetIdentity,
            fileBridgeResult.Envelope.RequestId,
            CommandEvidence.FileBacked(fileBridgeResult.CommandPath, fileBridgeResult.ResponsePath));
    }

    /// <summary>
    /// 为快速通道保留 FileBridge 最小合法预算，避免回退后重新消耗完整 timeout。
    /// </summary>
    /// <param name="timeoutMs">调用方总预算。</param>
    /// <returns>快速通道预算；总预算不足时返回零。</returns>
    private static int GetFastChannelBudget(int timeoutMs)
    {
        if (timeoutMs <= CommandEnvelope.COMMAND_TIMEOUT_MIN_MS)
        {
            // 最小协议期限仍允许直接 FastChannel 查询；若该查询失败，不再伪造一个
            // 超出调用方总期限的 FileBridge 回退。
            return timeoutMs;
        }

        var remainingForFallback = timeoutMs - CommandEnvelope.COMMAND_TIMEOUT_MIN_MS;
        return remainingForFallback <= 0 ? 0 : Math.Min(750, remainingForFallback);
    }

    /// <summary>
    /// 判断当前总 deadline 是否还足够提交一个合法 FileBridge 命令。
    /// </summary>
    /// <param name="timeoutMs">调用方总预算。</param>
    /// <param name="startTimestamp">本次命令起始时间戳。</param>
    /// <returns>剩余时间达到协议最小期限时返回 true。</returns>
    private static bool HasFileBridgeBudget(int timeoutMs, long startTimestamp)
    {
        var elapsedMs = Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;
        return timeoutMs - elapsedMs >= CommandEnvelope.COMMAND_TIMEOUT_MIN_MS;
    }

    /// <summary>
    /// 创建快速通道失败且总期限已耗尽时的稳定未知结果错误。
    /// </summary>
    /// <param name="engineId">目标 engine。</param>
    /// <returns>可由 Workbench 投影为 Unknown 的协议异常。</returns>
    private static YokiFrameProtocolException CreateFastChannelTimeout(string engineId)
    {
        return new YokiFrameProtocolException(new YokiFrameError(
            "FastChannelCommandTimeout",
            "FastChannel failed before a valid FileBridge fallback budget remained.",
            "Query command status when evidence is available, then retry explicitly.",
            Array.Empty<string>(),
            null,
            engineId,
            FAST_CHANNEL_TRANSPORT));
    }

    /// <summary>
    /// 计算 FileBridge 回退的剩余预算，并保持协议允许的最小 timeout。
    /// </summary>
    /// <param name="timeoutMs">调用方总预算。</param>
    /// <param name="startTimestamp">本次命令开始时间戳。</param>
    /// <returns>FileBridge 应使用的 timeout。</returns>
    private static int GetRemainingTimeout(int timeoutMs, long startTimestamp)
    {
        var elapsedMs = (int)Math.Min(int.MaxValue, Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds);
        return Math.Max(CommandEnvelope.COMMAND_TIMEOUT_MIN_MS, timeoutMs - elapsedMs);
    }

    /// <summary>
    /// 尝试通过 FastChannel 发送白名单只读命令；可恢复故障返回 null，由调用方只回退一次 FileBridge。
    /// </summary>
    /// <param name="engineId">已选择的 engine。</param>
    /// <param name="action">白名单 System action。</param>
    /// <param name="source">审计来源。</param>
    /// <param name="timeoutMs">命令超时毫秒数。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>成功响应；优化层不可用时返回 null。</returns>
    private async Task<CommandResponse?> TrySendFastChannelCommandAsync(
        string engineId,
        string kit,
        string action,
        string payloadJson,
        string source,
        int timeoutMs,
        CancellationToken cancellationToken)
    {
        try
        {
            return await mFastChannelTransport!.SendFastChannelReadOnlyCommandAsync(
                engineId,
                kit,
                action,
                payloadJson,
                source,
                timeoutMs,
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (IsRecoverableFastChannelFailure(exception))
        {
            RecordFastChannelFailure(engineId);
            return null;
        }
    }

    /// <summary>
    /// 按 engine/session/generation 对快速通道失败执行短暂退避，避免失效 endpoint 让每次命令重复超时。
    /// </summary>
    /// <param name="engineId">目标 engine。</param>
    /// <returns>当前不在退避窗口时返回 true。</returns>
    private bool ShouldTryFastChannel(string engineId)
    {
        var identityKey = ReadFastChannelIdentityKey(engineId);
        lock (mFastChannelHealthGate)
        {
            if (!mFastChannelHealth.TryGetValue(identityKey, out var health))
            {
                return true;
            }

            if (DateTimeOffset.UtcNow >= health.CooldownUntilUtc)
            {
                return true;
            }

            return false;
        }
    }

    /// <summary>
    /// 记录快速通道可恢复失败，并逐步延长同一宿主代次的冷却窗口。
    /// </summary>
    /// <param name="engineId">目标 engine。</param>
    private void RecordFastChannelFailure(string engineId)
    {
        var identityKey = ReadFastChannelIdentityKey(engineId);
        lock (mFastChannelHealthGate)
        {
            var failureCount = mFastChannelHealth.TryGetValue(identityKey, out var health)
                ? health.FailureCount + 1
                : 1;
            var multiplier = Math.Pow(2, Math.Min(failureCount - 1, 4));
            var cooldown = TimeSpan.FromMilliseconds(
                Math.Min(
                    FastChannelMaximumCooldown.TotalMilliseconds,
                    FastChannelInitialCooldown.TotalMilliseconds * multiplier));
            mFastChannelHealth[identityKey] = new FastChannelHealth(
                failureCount,
                DateTimeOffset.UtcNow + cooldown);
            TrimFastChannelHealthCache();
        }
    }

    /// <summary>
    /// 快速通道成功后清除旧失败窗口，允许当前宿主立即恢复低延迟路径。
    /// </summary>
    /// <param name="engineId">当前成功响应所属的 engine。</param>
    private void ClearFastChannelFailure(string engineId)
    {
        var identityKey = ReadFastChannelIdentityKey(engineId);
        lock (mFastChannelHealthGate)
        {
            mFastChannelHealth.Remove(identityKey);
        }
    }

    /// <summary>
    /// 将健康缓存限制在有界规模，避免长期切换 engine/session 时保存无限历史。
    /// </summary>
    private void TrimFastChannelHealthCache()
    {
        while (mFastChannelHealth.Count > MAX_FAST_CHANNEL_HEALTH_ENTRIES)
        {
            var oldest = mFastChannelHealth
                .OrderBy(static pair => pair.Value.CooldownUntilUtc)
                .First();
            mFastChannelHealth.Remove(oldest.Key);
        }
    }

    /// <summary>
    /// 读取快速通道健康缓存所需的宿主代次；读取失败时退化为 engine 级键。
    /// </summary>
    /// <param name="engineId">目标 engine。</param>
    /// <returns>稳定的健康缓存键。</returns>
    private string ReadFastChannelIdentityKey(string engineId)
    {
        try
        {
            var entry = mStateReader.ReadEngineEntries()
                .FirstOrDefault(candidate => string.Equals(candidate.EngineId, engineId, StringComparison.Ordinal));
            return entry == null
                ? engineId
                : engineId + "/" + entry.SessionId + "/" + entry.Generation;
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or NotSupportedException
            or YokiFrameProtocolException)
        {
            return engineId;
        }
    }

    /// <summary>
    /// 保存一个宿主代次的快速通道失败窗口。
    /// </summary>
    private sealed record FastChannelHealth(int FailureCount, DateTimeOffset CooldownUntilUtc);

    /// <summary>
    /// 判断异常是否属于可选 FastChannel 的连接、协议或对象生命周期故障。
    /// </summary>
    /// <param name="exception">FastChannel 调用异常。</param>
    /// <returns>可以回退 FileBridge 时返回 true。</returns>
    private static bool IsRecoverableFastChannelFailure(Exception exception)
    {
        if (exception is IOException
            or TimeoutException
            or ObjectDisposedException)
        {
            return true;
        }

        if (exception is not YokiFrameProtocolException protocolException)
        {
            return false;
        }

        return protocolException.Error.Code switch
        {
            "FastChannelEndpointUnsupported" => true,
            "FastChannelEndpointInvalid" => true,
            "FastChannelConnectTimeout" => true,
            "FastChannelConnectFailed" => true,
            "FastChannelUnavailable" => true,
            "FastChannelCommandUnsupported" => true,
            "FastChannelCommandTimeout" => true,
            "FastChannelEndpointSuperseded" => true,
            "FastChannelHandshakeMismatch" => true,
            "FastChannelCommandRejected" => true,
            "FastChannelBusy" => true,
            "FastChannelHostStopping" => true,
            "FastChannelQueueUnavailable" => true,
            "FastChannelConnectionFailed" => true,
            _ => false
        };
    }

    /// <summary>
    /// 确认显式身份与当前命令目标一致，防止调用方把请求发往其它 engine。
    /// </summary>
    /// <param name="selectedEngineId">解析后的目标 engine。</param>
    /// <param name="expectedIdentity">调用方捕获的宿主身份。</param>
    private static void EnsureExpectedEngine(string selectedEngineId, HostIdentity? expectedIdentity)
    {
        if (expectedIdentity == null || string.Equals(selectedEngineId, expectedIdentity.EngineId, StringComparison.Ordinal))
        {
            return;
        }

        throw new YokiFrameProtocolException(new YokiFrameError(
            "CommandTargetIdentityMismatch",
            "The command target changed before execution started.",
            "Refresh the selected engine and retry the command.",
            Array.Empty<string>()));
    }

    /// <summary>
    /// 在命令完成后再次确认宿主仍属于请求代次，避免 late response 污染当前 Workbench。
    /// </summary>
    /// <param name="engineId">目标 engine。</param>
    /// <param name="expectedIdentity">请求开始时捕获的身份。</param>
    private void EnsureCurrentIdentity(string engineId, HostIdentity? expectedIdentity)
    {
        if (expectedIdentity == null)
        {
            return;
        }

        var currentIdentity = ReadCurrentHostIdentity(engineId);
        if (currentIdentity == expectedIdentity)
        {
            return;
        }

        throw new YokiFrameProtocolException(new YokiFrameError(
            "HostIdentityChanged",
            "The host session or generation changed while the command was waiting.",
            "Refresh the engine session and retry the command.",
            Array.Empty<string>()));
    }

    /// <summary>
    /// 读取当前 registry/heartbeat 的一致身份，仅用于已经绑定身份的命令收尾校验。
    /// </summary>
    /// <param name="engineId">目标 engine。</param>
    /// <returns>当前一致身份；读取失败时返回 null。</returns>
    private HostIdentity? ReadCurrentHostIdentity(string engineId)
    {
        try
        {
            var registry = mStateReader.ReadEngineEntries()
                .FirstOrDefault(entry => string.Equals(entry.EngineId, engineId, StringComparison.Ordinal));
            var heartbeat = mStateReader.ReadHeartbeat(engineId);
            if (registry == null || heartbeat == null || EngineHostIdentity.HasMismatch(registry, heartbeat))
            {
                return null;
            }

            var sessionId = string.IsNullOrWhiteSpace(heartbeat.SessionId) ? registry.SessionId : heartbeat.SessionId;
            var generation = heartbeat.Generation != 0L ? heartbeat.Generation : registry.Generation;
            var mode = string.IsNullOrWhiteSpace(heartbeat.Mode) ? registry.Mode : heartbeat.Mode;
            var identity = new HostIdentity(engineId, sessionId, generation, mode);
            return identity.IsValid ? identity : null;
        }
        catch (YokiFrameProtocolException)
        {
            throw;
        }
        catch (IOException exception)
        {
            throw CreateIdentityReadException(engineId, exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            throw CreateIdentityReadException(engineId, exception);
        }
    }

    /// <summary>
    /// 把身份读取的 IO 故障转换为独立错误码，避免与真实 session/generation 变化混淆。
    /// </summary>
    /// <param name="engineId">当前 engine 标识。</param>
    /// <param name="exception">底层读取异常。</param>
    /// <returns>带可执行建议的协议异常。</returns>
    private static YokiFrameProtocolException CreateIdentityReadException(
        string engineId,
        Exception exception)
    {
        return new YokiFrameProtocolException(new YokiFrameError(
            "HostIdentityUnavailable",
            "The current host identity could not be read for " + engineId + ": " + exception.Message,
            "Refresh the engine registry and heartbeat, then retry the command.",
            Array.Empty<string>(),
            null,
            engineId,
            "file-bridge"));
    }

}
