using System.Text.Json;
using YokiFrame;
using YokiFrame.Client.Commands;
using YokiFrame.Client.Transports.FileBridge;
using YokiFrame.Protocol.FastChannel;
using YokiFrame.Protocol.FileBridge;
using YokiFrame.Protocol.Results;

namespace YokiFrame.Client.FastChannel;

/// <summary>
/// 在 Client 内部选择本机 FastChannel endpoint、缓存连接并发送 Host 声明的只读命令。
/// </summary>
internal sealed partial class FastChannelCommandTransport : IDisposable, IAsyncDisposable
{
    private const int MAX_CONNECT_TIMEOUT_MS = 500;
    private const int MAX_OPERATION_TIMEOUT_MS = 750;
    private const int DISPOSE_WAIT_MS = 500;
    private readonly FileBridgeTransport mFileBridgeTransport;
    private readonly SemaphoreSlim mConnectionGate = new(1, 1);
    private readonly Dictionary<string, CachedFastChannelConnection> mConnections = new(StringComparer.Ordinal);
    private int mDisposed;

    /// <summary>
    /// 使用同一 FileBridge registry reader 创建快速通道 transport，避免工具侧出现第二套项目路径或协议 IO。
    /// </summary>
    /// <param name="fileBridgeTransport">统一 Client 已拥有的 FileBridge transport。</param>
    public FastChannelCommandTransport(FileBridgeTransport fileBridgeTransport)
    {
        mFileBridgeTransport = fileBridgeTransport ?? throw new ArgumentNullException(nameof(fileBridgeTransport));
    }

    /// <summary>
    /// 根据最新 registry 发送一个已声明的只读 command；endpoint 无效、连接失败或 response 异常都会丢弃连接。
    /// </summary>
    /// <param name="engineId">目标 engine。</param>
    /// <param name="kit">目标 Kit。</param>
    /// <param name="action">目标 action。</param>
    /// <param name="payloadJson">只读查询 payload。</param>
    /// <param name="source">审计来源。</param>
    /// <param name="timeoutMs">调用方为本次快速通道操作分配的本地最大等待毫秒数；线上信封会单独遵守协议超时范围。</param>
    /// <param name="cancellationToken">外部取消令牌。</param>
    /// <returns>Host 返回且已校验关联字段的 terminal response。</returns>
    public async Task<CommandResponse> SendReadOnlyCommandAsync(
        string engineId,
        string kit,
        string action,
        string payloadJson,
        string source,
        int timeoutMs,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        if (timeoutMs <= 0)
        {
            throw CreateProtocolException(
                "InvalidTimeout",
                "FastChannel operation timeout must be greater than zero milliseconds.",
                "Pass a positive timeout value; the wire envelope will use the Runtime minimum when needed.");
        }

        // FastChannel 可以使用比 FileBridge 更短的本地操作期限，但 Host 仍会按
        // CommandPolicy 解析信封中的 timeoutMs；两者不能共用一个小于协议下限的值。
        var operationTimeoutMs = timeoutMs;
        var envelopeTimeoutMs = Math.Max(timeoutMs, CommandEnvelope.COMMAND_TIMEOUT_MIN_MS);
        var envelope = CommandEnvelope.Create(
            engineId,
            source,
            FileBridgeTransport.CreateRequestId(source),
            kit,
            action,
            payloadJson,
            envelopeTimeoutMs);
        var endpoint = await ResolveEndpointAsync(envelope.EngineId).ConfigureAwait(false);
        if (!endpoint.SupportsReadOnlyCommand(envelope.Kit, envelope.Action))
        {
            throw CreateProtocolException(
                "FastChannelCommandUnsupported",
                "The current endpoint does not advertise this command as read-only.",
                "Use reliable FileBridge for this command.");
        }
        FastChannelConnection? connection = null;
        using var operationSource = CreateOperationCancellationSource(operationTimeoutMs, cancellationToken);
        try
        {
            connection = await GetOrCreateConnectionAsync(endpoint, operationSource.Token).ConfigureAwait(false);
            var request = new FastChannelFrame(
                YokiFrameFastChannelMessageKind.Command,
                0,
                envelope.ToJson());
            var responseFrame = await connection.RequestAsync(request, operationSource.Token).ConfigureAwait(false);
            return ReadCommandResponse(responseFrame, envelope);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if (connection != null)
            {
                await InvalidateConnectionAsync(envelope.EngineId, connection).ConfigureAwait(false);
            }

            throw;
        }
        catch (OperationCanceledException)
        {
            if (connection != null)
            {
                await InvalidateConnectionAsync(envelope.EngineId, connection).ConfigureAwait(false);
            }

            throw CreateProtocolException(
                "FastChannelCommandTimeout",
                "FastChannel command did not complete before the short operation deadline.",
                "Use FileBridge fallback or wait for the engine adapter to become responsive.");
        }
        catch
        {
            if (connection != null)
            {
                await InvalidateConnectionAsync(envelope.EngineId, connection).ConfigureAwait(false);
            }

            throw;
        }
    }

    /// <summary>
    /// 判断最新 registry 是否声明指定命令可由当前本机 FastChannel endpoint 执行。
    /// </summary>
    /// <param name="engineId">目标 engine。</param>
    /// <param name="kit">目标 Kit。</param>
    /// <param name="action">请求 action。</param>
    /// <returns>endpoint 当前启用且声明该命令时返回 true。</returns>
    public bool CanSendReadOnlyCommand(string engineId, string kit, string action)
    {
        ThrowIfDisposed();
        return FindCurrentEndpoint(engineId)?.SupportsReadOnlyCommand(kit, action) == true;
    }

    /// <summary>
    /// 释放指定 engine 的缓存连接，供宿主生命周期文件变化时立即切断旧 stream。
    /// </summary>
    /// <param name="engineId">需要失效连接的 engine。</param>
    /// <returns>连接释放完成后的异步任务。</returns>
    public Task InvalidateConnectionsAsync(string engineId)
    {
        ThrowIfDisposed();
        return InvalidateConnectionAsync(engineId, null);
    }

    /// <summary>阻止新连接进入后释放全部缓存 stream；并发 Dispose 保持幂等。</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref mDisposed, 1) != 0)
        {
            return;
        }

        CancelActiveConnectionAttempt();
        List<CachedFastChannelConnection> cachedConnections;
        if (!mConnectionGate.Wait(DISPOSE_WAIT_MS))
        {
            _ = CompleteDeferredDisposeAsync();
            return;
        }

        try
        {
            cachedConnections = new List<CachedFastChannelConnection>(mConnections.Values);
            mConnections.Clear();
        }
        finally
        {
            mConnectionGate.Release();
        }

        DisposeConnections(cachedConnections);
    }

    /// <summary>阻止新连接进入后异步释放全部缓存 stream；并发释放保持幂等。</summary>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref mDisposed, 1) != 0)
        {
            return;
        }

        CancelActiveConnectionAttempt();
        if (!await mConnectionGate.WaitAsync(DISPOSE_WAIT_MS).ConfigureAwait(false))
        {
            _ = CompleteDeferredDisposeAsync();
            return;
        }
        List<CachedFastChannelConnection> cachedConnections;
        try
        {
            cachedConnections = new List<CachedFastChannelConnection>(mConnections.Values);
            mConnections.Clear();
        }
        finally
        {
            mConnectionGate.Release();
        }

        await DisposeConnectionsAsync(cachedConnections).ConfigureAwait(false);
    }

    /// <summary>
    /// 在连接创建或失效操作释放闸门后继续完成延迟 Dispose，避免调用方被永久阻塞。
    /// </summary>
    private async Task CompleteDeferredDisposeAsync()
    {
        try
        {
            await mConnectionGate.WaitAsync().ConfigureAwait(false);
            List<CachedFastChannelConnection> cachedConnections;
            try
            {
                cachedConnections = new List<CachedFastChannelConnection>(mConnections.Values);
                mConnections.Clear();
            }
            finally
            {
                mConnectionGate.Release();
            }

            await DisposeConnectionsAsync(cachedConnections).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // 生命周期已经进入 Dispose；后台收口失败不能重新传播到已返回的调用方。
        }
    }

    /// <summary>逐一关闭已移出缓存的连接（同步路径），并在全部尝试完成后汇总释放异常。</summary>
    /// <param name="cachedConnections">当前 Transport 曾拥有的连接快照。</param>
    private static void DisposeConnections(IReadOnlyList<CachedFastChannelConnection> cachedConnections)
    {
        List<Exception>? failures = null;
        for (var index = 0; index < cachedConnections.Count; index++)
        {
            try
            {
                cachedConnections[index].Connection.DisposeAsync().AsTask().GetAwaiter().GetResult();
            }
            catch (Exception exception)
            {
                failures ??= new List<Exception>();
                failures.Add(exception);
            }
        }

        if (failures != null)
        {
            throw new AggregateException("FastChannel connections could not be fully disposed.", failures);
        }
    }

    /// <summary>逐一异步关闭已移出缓存的连接，并在全部尝试完成后汇总释放异常。</summary>
    /// <param name="cachedConnections">当前 Transport 曾拥有的连接快照。</param>
    private static async ValueTask DisposeConnectionsAsync(IReadOnlyList<CachedFastChannelConnection> cachedConnections)
    {
        List<Exception>? failures = null;
        for (var index = 0; index < cachedConnections.Count; index++)
        {
            try
            {
                await cachedConnections[index].Connection.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                failures ??= new List<Exception>();
                failures.Add(exception);
            }
        }

        if (failures != null)
        {
            throw new AggregateException("FastChannel connections could not be fully disposed.", failures);
        }
    }

    /// <summary>拒绝 Transport 生命周期结束后的查询、失效与连接创建。</summary>
    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref mDisposed) != 0, this);
    }

    /// <summary>
    /// 从当前 registry 找到身份与平台均有效的 endpoint；缺失、禁用或生命周期不一致时先释放旧连接，防止后续请求复用陈旧 stream。
    /// </summary>
    /// <param name="engineId">目标 engine。</param>
    /// <returns>当前本机可连接的 endpoint。</returns>
    private async Task<FastChannelEndpoint> ResolveEndpointAsync(string engineId)
    {
        ThrowIfDisposed();
        var endpoint = FindCurrentEndpoint(engineId);
        if (endpoint != null)
        {
            return endpoint;
        }

        await InvalidateConnectionAsync(engineId, null).ConfigureAwait(false);
        throw CreateProtocolException(
            "FastChannelUnavailable",
            "The current engine registry does not publish a compatible FastChannel endpoint.",
            "Use FileBridge fallback, or refresh registry after the engine adapter is ready.");
    }

    /// <summary>
    /// 在当前 engine registry 中选择第一个和 registry 身份、协议版本及本机平台完全一致的启用 endpoint。
    /// </summary>
    /// <param name="engineId">目标 engine。</param>
    /// <returns>可连接 endpoint；没有兼容 endpoint 时返回 null。</returns>
    private FastChannelEndpoint? FindCurrentEndpoint(string engineId)
    {
        var registry = mFileBridgeTransport.ReadEngineEntries().FirstOrDefault(
            entry => string.Equals(entry.EngineId, engineId, StringComparison.Ordinal));
        if (registry == null)
        {
            return null;
        }

        return registry.FastChannels.FirstOrDefault(endpoint => IsCurrentLocalEndpoint(registry, endpoint));
    }

    /// <summary>
    /// 确认 endpoint 与 registry 公开的 session/generation 相同，并且 transport 属于当前操作系统支持的本机实现。
    /// </summary>
    /// <param name="registry">本轮读取到的 engine registry。</param>
    /// <param name="endpoint">待选择的 endpoint。</param>
    /// <returns>可由当前 Client 使用时返回 true。</returns>
    private static bool IsCurrentLocalEndpoint(EngineRegistryEntry registry, FastChannelEndpoint endpoint)
    {
        return endpoint.Enabled
            && endpoint.ProtocolVersion == YokiFrameFastChannelContract.PROTOCOL_VERSION
            && string.Equals(endpoint.EngineId, registry.EngineId, StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(endpoint.SessionId)
            && string.Equals(endpoint.SessionId, registry.SessionId, StringComparison.Ordinal)
            && endpoint.Generation > 0L
            && endpoint.Generation == registry.Generation
            && string.Equals(endpoint.Fallback, FastChannelEndpoint.FILEBRIDGE_FALLBACK, StringComparison.Ordinal)
            && SupportsCurrentPlatform(endpoint.Transport);
    }

    /// <summary>
    /// 根据当前操作系统限制 FastChannel 到 Windows Named Pipe 或 macOS/Linux Unix Domain Socket，拒绝 HTTP、WebSocket 和跨平台错误 endpoint。
    /// </summary>
    /// <param name="transport">registry 声明的 transport 名称。</param>
    /// <returns>当前进程可使用时返回 true。</returns>
    private static bool SupportsCurrentPlatform(string transport)
    {
        return (OperatingSystem.IsWindows() && string.Equals(transport, FastChannelTransport.NamedPipe, StringComparison.Ordinal))
            || ((OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
                && string.Equals(transport, FastChannelTransport.UnixDomainSocket, StringComparison.Ordinal));
    }

    /// <summary>
    /// 读取 Host 返回的 response frame，并校验其关联到本次生成的 requestId 和 engine，避免连接断开重连后误接受旧响应。
    /// </summary>
    /// <param name="responseFrame">连接返回的下一条 frame。</param>
    /// <param name="envelope">本次发送的 command 信封。</param>
    /// <returns>已验证的 FileBridge 风格 terminal response。</returns>
    private static CommandResponse ReadCommandResponse(FastChannelFrame responseFrame, CommandEnvelope envelope)
    {
        if (responseFrame.Kind == YokiFrameFastChannelMessageKind.Error)
        {
            throw CreateHostError(responseFrame.PayloadJson);
        }

        if (responseFrame.Kind != YokiFrameFastChannelMessageKind.Response)
        {
            throw CreateProtocolException(
                "FastChannelResponseKindMismatch",
                "FastChannel host returned a non-response frame after a command.",
                "Discard the connection and use FileBridge fallback.");
        }

        CommandResponse response;
        try
        {
            response = CommandResponse.FromJson(responseFrame.PayloadJson);
        }
        catch (System.Text.Json.JsonException)
        {
            throw CreateProtocolException(
                "FastChannelResponseInvalid",
                "FastChannel host returned a malformed response JSON payload.",
                "Discard the connection and use FileBridge fallback.");
        }

        return CommandResponseValidator.Validate(
            response,
            envelope,
            "FastChannelResponseMismatch",
            "FastChannel response does not match the current command request.",
            "Discard the connection, refresh registry, and use FileBridge fallback.");
    }

    /// <summary>
    /// 解析 Host Error frame 的稳定错误码；保留 queue/host 生命周期错误的可回退语义，
    /// 同时避免把真正的协议损坏统一伪装成“通道不可用”。
    /// </summary>
    /// <param name="payloadJson">Host 写入 Error frame 的 JSON payload。</param>
    /// <returns>带 Host 错误码的标准协议异常。</returns>
    private static YokiFrameProtocolException CreateHostError(string payloadJson)
    {
        try
        {
            using var document = JsonDocument.Parse(payloadJson);
            if (document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("code", out var codeProperty)
                && codeProperty.ValueKind == JsonValueKind.String)
            {
                var code = codeProperty.GetString();
                if (!string.IsNullOrWhiteSpace(code))
                {
                    var message = document.RootElement.TryGetProperty("message", out var messageProperty)
                        && messageProperty.ValueKind == JsonValueKind.String
                        ? messageProperty.GetString()
                        : "FastChannel host rejected the read-only command.";
                    return CreateProtocolException(
                        code,
                        message ?? "FastChannel host rejected the read-only command.",
                        "Use FileBridge fallback or refresh the engine registry.");
                }
            }
        }
        catch (JsonException)
        {
            // 继续使用稳定的通用错误码，让上层把损坏的 Error frame 当作协议错误暴露。
        }

        return CreateProtocolException(
            "FastChannelHostError",
            "FastChannel host returned a malformed or unclassified Error frame.",
            "Discard the connection and inspect the host protocol version.");
    }

    /// <summary>
    /// 创建携带外部取消令牌的短操作期限，避免 FastChannel 异常时占满 Workbench 的常规 command timeout。
    /// </summary>
    /// <param name="operationTimeoutMs">调用方为本次 FastChannel 操作分配的本地期限。</param>
    /// <param name="cancellationToken">调用侧取消令牌。</param>
    /// <returns>用于连接、握手和单次请求响应的链接取消源。</returns>
    private static CancellationTokenSource CreateOperationCancellationSource(
        int operationTimeoutMs,
        CancellationToken cancellationToken)
    {
        var source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        source.CancelAfter(Math.Min(operationTimeoutMs, MAX_OPERATION_TIMEOUT_MS));
        return source;
    }

    /// <summary>
    /// 创建供 Workbench 和 CLI 统一识别的可回退 FastChannel 协议异常。
    /// </summary>
    /// <param name="code">稳定错误码。</param>
    /// <param name="message">当前失败说明。</param>
    /// <param name="suggestion">恢复或回退建议。</param>
    /// <returns>标准协议异常。</returns>
    private static YokiFrameProtocolException CreateProtocolException(string code, string message, string suggestion)
    {
        return new YokiFrameProtocolException(new YokiFrameError(code, message, suggestion));
    }

    /// <summary>
    /// 保存连接创建时的完整 endpoint 身份，供下一次 registry 刷新判断是否仍可安全复用。
    /// </summary>
    /// <param name="Endpoint">连接创建时验证过的 endpoint。</param>
    /// <param name="Connection">已经完成 Hello/HelloAck 的 transport 连接。</param>
    private sealed record CachedFastChannelConnection(FastChannelEndpoint Endpoint, FastChannelConnection Connection);
}
