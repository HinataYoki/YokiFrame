#if GODOT && TOOLS
using System;
using System.IO;
using System.IO.Pipes;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace YokiFrame
{
    /// <summary>
    /// 拥有 Godot Runtime 的本机 FastChannel listener；后台任务只负责传输生命周期。
    /// </summary>
    internal sealed partial class GodotFastChannelListener : IDisposable
    {
        private const int SOCKET_BACKLOG = 4;
        private const int MAX_SOCKET_PATH_LENGTH = 100;
        private const int SOCKET_SESSION_TOKEN_LENGTH = 12;
        private const int FRAME_READ_TIMEOUT_MS = 1500;
        private const string PIPE_NAME_PREFIX = "YokiFrame.FastChannel.godot-runtime.";
        private const string SOCKET_FILE_PREFIX = "yf-godot-";
        private const string SOCKET_FILE_EXTENSION = ".sock";

        private readonly object mGate = new();
        private readonly string mEngineId;
        private readonly string mProjectScopeId;
        private readonly string mSessionId;
        private readonly long mGeneration;
        private readonly YokiFrameFastChannelRequestQueue mRequestQueue;
        private readonly CancellationTokenSource mStopSource = new();
        private IDisposable mActiveConnection;
        private Socket mUnixListener;
        private GodotFastChannelEndpoint mEndpoint;
        private string mLastError = string.Empty;
        private string mOwnedSocketPath = string.Empty;
        private bool mDisposed;
        private bool mIsReady;
        private bool mStopped;

        /// <summary>
        /// 创建与当前 engine 会话绑定的 listener；调用 Start 前 endpoint 保持 disabled。
        /// </summary>
        /// <param name="engineId">当前 engine 标识。</param>
        /// <param name="projectScopeId">由规范化项目根生成的安全作用域。</param>
        /// <param name="sessionId">当前会话标识。</param>
        /// <param name="generation">当前 generation。</param>
        /// <param name="requestQueue">后台 listener 仅可写入的主线程请求队列。</param>
        public GodotFastChannelListener(
            string engineId,
            string projectScopeId,
            string sessionId,
            long generation,
            YokiFrameFastChannelRequestQueue requestQueue)
        {
            if (!YokiFrameSafeIdContract.IsSafeId(engineId))
            {
                throw new ArgumentException("FastChannel engine ID is invalid.", nameof(engineId));
            }

            if (!YokiFrameSafeIdContract.IsSafeId(sessionId))
            {
                throw new ArgumentException("FastChannel session ID is invalid.", nameof(sessionId));
            }

            if (!YokiFrameSafeIdContract.IsSafeId(projectScopeId))
            {
                throw new ArgumentException("FastChannel project scope ID is invalid.", nameof(projectScopeId));
            }

            if (generation <= 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(generation));
            }

            mEngineId = engineId;
            mProjectScopeId = projectScopeId;
            mSessionId = sessionId;
            mGeneration = generation;
            mRequestQueue = requestQueue ?? throw new ArgumentNullException(nameof(requestQueue));
            mEndpoint = GodotFastChannelEndpoint.Disabled(engineId, sessionId, generation);
        }

        /// <summary>
        /// 获取最近一次 listener 或 socket 清理失败说明；没有失败时为空。
        /// </summary>
        public string LastError
        {
            get
            {
                lock (mGate)
                {
                    return mLastError;
                }
            }
        }

        /// <summary>
        /// 启动当前平台本机 listener；不支持的平台继续保留 disabled endpoint。
        /// </summary>
        public void Start()
        {
            EnsureCanStart();
            if (OperatingSystem.IsWindows())
            {
                StartNamedPipe();
                return;
            }

            if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
            {
                StartUnixDomainSocket();
            }
        }

        /// <summary>
        /// 读取可安全写入 registry 的 endpoint 快照；listener 未就绪时强制返回 disabled。
        /// </summary>
        /// <returns>当前会话 endpoint。</returns>
        public GodotFastChannelEndpoint GetEndpoint()
        {
            lock (mGate)
            {
                return mIsReady
                    ? mEndpoint
                    : GodotFastChannelEndpoint.Disabled(mEngineId, mSessionId, mGeneration);
            }
        }

        /// <summary>
        /// 停止 accept、关闭活动连接并清理当前 listener 自己创建的 Unix socket 路径。
        /// </summary>
        public void Stop()
        {
            IDisposable activeConnection;
            Socket unixListener;
            string ownedSocketPath;
            lock (mGate)
            {
                if (mStopped)
                {
                    return;
                }

                mStopped = true;
                mIsReady = false;
                mEndpoint = GodotFastChannelEndpoint.Disabled(mEngineId, mSessionId, mGeneration);
                activeConnection = mActiveConnection;
                unixListener = mUnixListener;
                ownedSocketPath = mOwnedSocketPath;
                mActiveConnection = null;
                mUnixListener = null;
                mOwnedSocketPath = string.Empty;
            }

            mStopSource.Cancel();
            activeConnection?.Dispose();
            unixListener?.Dispose();
            DeleteOwnedSocketPath(ownedSocketPath);
        }

        /// <summary>
        /// 释放 listener，语义等同于幂等停止并释放取消源。
        /// </summary>
        public void Dispose()
        {
            lock (mGate)
            {
                if (mDisposed)
                {
                    return;
                }

                mDisposed = true;
            }

            Stop();
            mStopSource.Dispose();
        }

        /// <summary>
        /// 创建同用户范围的 Named Pipe server，构造成功即表示 endpoint 已完成本机注册。
        /// </summary>
        /// <param name="pipeName">安全 pipe 名称。</param>
        /// <returns>等待 Client 连接的 server。</returns>
        private static NamedPipeServerStream CreateNamedPipeServer(string pipeName)
        {
            return new NamedPipeServerStream(
                pipeName,
                PipeDirection.InOut,
                1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        }

        /// <summary>
        /// 启动 Windows Named Pipe accept 循环，并在首个 server 构造后标记 endpoint ready。
        /// </summary>
        private void StartNamedPipe()
        {
            var pipeName = PIPE_NAME_PREFIX + mProjectScopeId + "." + mSessionId;
            if (!YokiFrameSafeIdContract.IsSafeId(pipeName))
            {
                throw new InvalidOperationException("Generated FastChannel pipe name is not a safe ID.");
            }

            var firstServer = CreateNamedPipeServer(pipeName);
            TrackReadyEndpoint(firstServer, GodotFastChannelEndpoint.NamedPipe(
                mEngineId,
                mSessionId,
                mGeneration,
                pipeName));
            _ = Task.Run(() => ListenNamedPipeAsync(firstServer, pipeName, mStopSource.Token));
        }

        /// <summary>
        /// 启动 Unix Domain Socket accept 循环，并仅记录本次成功 bind 后由 Host 拥有的路径。
        /// </summary>
        private void StartUnixDomainSocket()
        {
            var socketPath = Path.Combine(
                Path.GetTempPath(),
                SOCKET_FILE_PREFIX + mProjectScopeId + "-" + CreateSocketSessionToken() + SOCKET_FILE_EXTENSION);
            if (!Path.IsPathFullyQualified(socketPath) || socketPath.Length > MAX_SOCKET_PATH_LENGTH)
            {
                throw new InvalidOperationException("Generated FastChannel Unix socket path is invalid or too long.");
            }

            Socket listener = new(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            try
            {
                listener.Bind(new UnixDomainSocketEndPoint(socketPath));
                listener.Listen(SOCKET_BACKLOG);
                lock (mGate)
                {
                    if (mStopped)
                    {
                        throw new ObjectDisposedException(nameof(GodotFastChannelListener));
                    }

                    mUnixListener = listener;
                    mOwnedSocketPath = socketPath;
                    mEndpoint = GodotFastChannelEndpoint.UnixDomainSocket(
                        mEngineId,
                        mSessionId,
                        mGeneration,
                        socketPath);
                    mIsReady = true;
                }

                _ = Task.Run(() => ListenUnixDomainSocketAsync(listener, mStopSource.Token));
            }
            catch
            {
                listener.Dispose();
                throw;
            }
        }

        /// <summary>
        /// 截取仅用于 UDS 文件名去重的 session 令牌，控制 macOS 临时目录下的完整 socket 路径长度；握手仍校验完整 session。
        /// </summary>
        /// <returns>最多十二个安全字符的 session 文件名令牌。</returns>
        private string CreateSocketSessionToken()
        {
            return mSessionId.Length <= SOCKET_SESSION_TOKEN_LENGTH
                ? mSessionId
                : mSessionId.Substring(0, SOCKET_SESSION_TOKEN_LENGTH);
        }

        /// <summary>
        /// 循环接受单个 Named Pipe 连接；后台只完成握手和入队，不调用 Godot 或 dispatcher。
        /// </summary>
        /// <param name="firstServer">Start 阶段已经创建并发布的首个 server。</param>
        /// <param name="pipeName">后续 server 复用的稳定 pipe 名称。</param>
        /// <param name="cancellationToken">Host 停止令牌。</param>
        /// <returns>listener 结束任务。</returns>
        private async Task ListenNamedPipeAsync(
            NamedPipeServerStream firstServer,
            string pipeName,
            CancellationToken cancellationToken)
        {
            var server = firstServer;
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    await server.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
                    await ProcessConnectionAsync(server, cancellationToken).ConfigureAwait(false);
                    server.Dispose();
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    server = CreateNamedPipeServer(pipeName);
                    if (!TryTrackConnection(server))
                    {
                        return;
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                RecordFailure("FastChannel Named Pipe listener stopped: " + exception.Message);
            }
            finally
            {
                ReleaseTrackedConnection(server);
                server.Dispose();
            }
        }

        /// <summary>
        /// 循环接受 Unix socket 连接；后台只完成握手和入队，不调用 Godot 或 dispatcher。
        /// </summary>
        /// <param name="listener">已完成 bind 的 Unix listener。</param>
        /// <param name="cancellationToken">Host 停止令牌。</param>
        /// <returns>listener 结束任务。</returns>
        private async Task ListenUnixDomainSocketAsync(Socket listener, CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    using Socket acceptedSocket = await listener.AcceptAsync(cancellationToken).ConfigureAwait(false);
                    using NetworkStream stream = new(acceptedSocket, false);
                    if (!TryTrackConnection(stream))
                    {
                        return;
                    }

                    try
                    {
                        await ProcessConnectionAsync(stream, cancellationToken).ConfigureAwait(false);
                    }
                    finally
                    {
                        ReleaseTrackedConnection(stream);
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (SocketException exception) when (cancellationToken.IsCancellationRequested)
            {
                RecordFailure("FastChannel Unix listener cancelled: " + exception.Message);
            }
            catch (Exception exception)
            {
                RecordFailure("FastChannel Unix listener stopped: " + exception.Message);
            }
        }

        /// <summary>
        /// 在发布 enabled endpoint 前记录首个等待连接的传输对象，保证 Stop 可立即关闭它。
        /// </summary>
        /// <param name="connection">首个等待连接对象。</param>
        /// <param name="endpoint">已完成 bind 的 endpoint。</param>
        private void TrackReadyEndpoint(IDisposable connection, GodotFastChannelEndpoint endpoint)
        {
            lock (mGate)
            {
                if (mStopped)
                {
                    connection.Dispose();
                    throw new ObjectDisposedException(nameof(GodotFastChannelListener));
                }

                mActiveConnection = connection;
                mEndpoint = endpoint;
                mIsReady = true;
            }
        }

        /// <summary>
        /// 将后续 Named Pipe server 记录为当前可由 Stop 关闭的活动传输。
        /// </summary>
        /// <param name="connection">新 server。</param>
        /// <returns>listener 尚未停止时返回 true。</returns>
        private bool TryTrackConnection(IDisposable connection)
        {
            lock (mGate)
            {
                if (mStopped)
                {
                    connection.Dispose();
                    return false;
                }

                mActiveConnection = connection;
                return true;
            }
        }

        /// <summary>
        /// 仅在字段仍指向当前对象时释放跟踪关系，避免覆盖并发 Stop 的清理结果。
        /// </summary>
        /// <param name="connection">本轮 server。</param>
        private void ReleaseTrackedConnection(IDisposable connection)
        {
            lock (mGate)
            {
                if (ReferenceEquals(mActiveConnection, connection))
                {
                    mActiveConnection = null;
                }
            }
        }

        /// <summary>
        /// 记录后台 listener 失败并立即把 registry endpoint 状态降级为 disabled。
        /// </summary>
        /// <param name="message">可诊断错误说明。</param>
        private void RecordFailure(string message)
        {
            lock (mGate)
            {
                mLastError = message;
                mIsReady = false;
                mEndpoint = GodotFastChannelEndpoint.Disabled(mEngineId, mSessionId, mGeneration);
            }
        }

        /// <summary>
        /// 记录单条客户端连接的协议或写回失败，但不改变仍在 accept 的 listener endpoint；后续连接仍可使用 FileBridge 之外的可选快通道。
        /// </summary>
        /// <param name="message">连接级可诊断错误说明。</param>
        private void RecordConnectionFailure(string message)
        {
            lock (mGate)
            {
                mLastError = message;
            }
        }

        /// <summary>
        /// 删除本 listener 在成功 bind 后登记的 Unix socket 文件；失败时保留诊断而不阻断 Host 退出。
        /// </summary>
        /// <param name="socketPath">当前 listener 自己创建的路径。</param>
        private void DeleteOwnedSocketPath(string socketPath)
        {
            if (string.IsNullOrEmpty(socketPath) || !File.Exists(socketPath))
            {
                return;
            }

            try
            {
                File.Delete(socketPath);
            }
            catch (Exception exception)
            {
                RecordFailure("FastChannel Unix socket cleanup failed: " + exception.Message);
            }
        }

        /// <summary>
        /// 拒绝重复启动、停止后重启或释放后启动同一个 listener 对象。
        /// </summary>
        private void EnsureCanStart()
        {
            lock (mGate)
            {
                if (mDisposed || mStopped || mIsReady)
                {
                    throw new InvalidOperationException("Godot FastChannel listener cannot be started in its current state.");
                }
            }
        }
    }
}
#endif
