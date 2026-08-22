using YokiFrame.Protocol.FastChannel;

namespace YokiFrame.Client.FastChannel;

/// <summary>承载 FastChannel 连接获取、生命周期抢占与缓存失效。</summary>
internal sealed partial class FastChannelCommandTransport
{
    private readonly object mConnectionAttemptGate = new();
    private ConnectionAttempt? mActiveConnectionAttempt;

    /// <summary>复用当前 endpoint 连接；新生命周期身份会先取消仍在握手的旧连接。</summary>
    /// <param name="endpoint">本轮 registry 选择的 endpoint。</param>
    /// <param name="cancellationToken">连接或握手期间的取消令牌。</param>
    /// <returns>与当前 endpoint 完全匹配的已握手连接。</returns>
    private async Task<FastChannelConnection> GetOrCreateConnectionAsync(
        FastChannelEndpoint endpoint,
        CancellationToken cancellationToken)
    {
        SupersedeStaleConnectionAttempt(endpoint);
        await mConnectionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            if (mConnections.TryGetValue(endpoint.EngineId, out var cached))
            {
                if (EndpointsMatch(cached.Endpoint, endpoint))
                {
                    return cached.Connection;
                }

                mConnections.Remove(endpoint.EngineId);
                await cached.Connection.DisposeAsync().ConfigureAwait(false);
            }

            return await ConnectCurrentEndpointAsync(endpoint, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            mConnectionGate.Release();
        }
    }

    /// <summary>注册可被新 endpoint 抢占的握手，并只把仍匹配 registry 的连接加入缓存。</summary>
    private async Task<FastChannelConnection> ConnectCurrentEndpointAsync(
        FastChannelEndpoint endpoint,
        CancellationToken cancellationToken)
    {
        var attempt = BeginConnectionAttempt(endpoint, cancellationToken);
        FastChannelConnection? connection = null;
        try
        {
            try
            {
                connection = await ConnectAsync(endpoint, attempt.Cancellation.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw CreateProtocolException(
                    "FastChannelEndpointSuperseded",
                    "FastChannel endpoint changed while the previous connection was handshaking.",
                    "Retry against the latest registry endpoint or use FileBridge fallback.");
            }

            EnsureEndpointIsCurrent(endpoint);
            mConnections.Add(endpoint.EngineId, new CachedFastChannelConnection(endpoint, connection));
            var cachedConnection = connection;
            connection = null;
            return cachedConnection;
        }
        finally
        {
            EndConnectionAttempt(attempt);
            if (connection != null)
            {
                await connection.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    /// <summary>在等待连接锁前取消同一 engine 已被 registry 新身份替代的握手。</summary>
    private void SupersedeStaleConnectionAttempt(FastChannelEndpoint endpoint)
    {
        var current = FindCurrentEndpoint(endpoint.EngineId);
        lock (mConnectionAttemptGate)
        {
            ThrowIfDisposed();
            if (current == null || !EndpointsMatch(current, endpoint))
            {
                throw CreateProtocolException(
                    "FastChannelEndpointSuperseded",
                    "FastChannel endpoint changed before the connection could become current.",
                    "Retry against the latest registry endpoint or use FileBridge fallback.");
            }

            if (mActiveConnectionAttempt != null
                && string.Equals(mActiveConnectionAttempt.Endpoint.EngineId, endpoint.EngineId, StringComparison.Ordinal)
                && !EndpointsMatch(mActiveConnectionAttempt.Endpoint, endpoint))
            {
                mActiveConnectionAttempt.Cancellation.Cancel();
            }
        }
    }

    /// <summary>建立当前唯一握手所有权，并在注册后再次确认 endpoint 尚未过期。</summary>
    private ConnectionAttempt BeginConnectionAttempt(
        FastChannelEndpoint endpoint,
        CancellationToken cancellationToken)
    {
        var current = FindCurrentEndpoint(endpoint.EngineId);
        lock (mConnectionAttemptGate)
        {
            ThrowIfDisposed();
            if (current == null || !EndpointsMatch(current, endpoint))
            {
                throw CreateProtocolException(
                    "FastChannelEndpointSuperseded",
                    "FastChannel endpoint changed before the connection could become current.",
                    "Retry against the latest registry endpoint or use FileBridge fallback.");
            }

            var attempt = new ConnectionAttempt(
                endpoint,
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken));
            mActiveConnectionAttempt = attempt;
            return attempt;
        }
    }

    /// <summary>结束仍属于当前调用的握手所有权并释放链接取消源。</summary>
    private void EndConnectionAttempt(ConnectionAttempt attempt)
    {
        lock (mConnectionAttemptGate)
        {
            if (ReferenceEquals(mActiveConnectionAttempt, attempt))
            {
                mActiveConnectionAttempt = null;
            }
        }

        attempt.Cancellation.Dispose();
    }

    /// <summary>Transport Dispose 前取消正在连接或握手的 endpoint，使连接锁可立即退出。</summary>
    private void CancelActiveConnectionAttempt()
    {
        lock (mConnectionAttemptGate)
        {
            mActiveConnectionAttempt?.Cancellation.Cancel();
        }
    }

    /// <summary>拒绝已经不再是 registry 当前身份的连接候选。</summary>
    private void EnsureEndpointIsCurrent(FastChannelEndpoint endpoint)
    {
        var current = FindCurrentEndpoint(endpoint.EngineId);
        if (current != null && EndpointsMatch(current, endpoint))
        {
            return;
        }

        throw CreateProtocolException(
            "FastChannelEndpointSuperseded",
            "FastChannel endpoint changed before the connection could become current.",
            "Retry against the latest registry endpoint or use FileBridge fallback.");
    }

    /// <summary>只释放仍指向失败连接的缓存项，避免旧失败路径关闭新 endpoint。</summary>
    private async Task InvalidateConnectionAsync(string engineId, FastChannelConnection? expectedConnection)
    {
        // 连接失效意味着 endpoint 身份已被证伪；同步清空 registry 缓存，下一轮读取回到磁盘事实。
        ClearRegistryCache();
        await mConnectionGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!mConnections.TryGetValue(engineId, out var cached)
                || (expectedConnection != null && !ReferenceEquals(cached.Connection, expectedConnection)))
            {
                return;
            }

            mConnections.Remove(engineId);
            await cached.Connection.DisposeAsync().ConfigureAwait(false);
        }
        finally
        {
            mConnectionGate.Release();
        }
    }

    /// <summary>按 endpoint transport 创建并完成 Hello/HelloAck 的连接。</summary>
    private static Task<FastChannelConnection> ConnectAsync(
        FastChannelEndpoint endpoint,
        CancellationToken cancellationToken)
    {
        var connectTimeout = TimeSpan.FromMilliseconds(MAX_CONNECT_TIMEOUT_MS);
        return string.Equals(endpoint.Transport, FastChannelTransport.NamedPipe, StringComparison.Ordinal)
            ? NamedPipeFastChannelConnector.ConnectAsync(endpoint, connectTimeout, cancellationToken)
            : UnixDomainSocketFastChannelConnector.ConnectAsync(endpoint, connectTimeout, cancellationToken);
    }

    /// <summary>比较会影响连接复用安全性的全部 endpoint 身份字段。</summary>
    private static bool EndpointsMatch(FastChannelEndpoint left, FastChannelEndpoint right)
    {
        return left.ProtocolVersion == right.ProtocolVersion
            && string.Equals(left.EngineId, right.EngineId, StringComparison.Ordinal)
            && string.Equals(left.SessionId, right.SessionId, StringComparison.Ordinal)
            && left.Generation == right.Generation
            && string.Equals(left.Transport, right.Transport, StringComparison.Ordinal)
            && string.Equals(left.Endpoint, right.Endpoint, StringComparison.Ordinal)
            && left.Enabled == right.Enabled
            && string.Equals(left.Fallback, right.Fallback, StringComparison.Ordinal);
    }

    /// <summary>保存当前握手 endpoint 与可被生命周期变化取消的链接令牌源。</summary>
    private sealed record ConnectionAttempt(
        FastChannelEndpoint Endpoint,
        CancellationTokenSource Cancellation);
}
