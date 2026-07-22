#if UNITY_EDITOR_WIN

using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace YokiFrame
{
    /// <summary>
    /// 在 Windows Unity Editor 中承载 Named Pipe FastChannel listener；后台线程只读写 frame 并把请求交给主线程队列。
    /// </summary>
    internal sealed class YokiFrameEditorNamedPipeFastChannelHost : IDisposable
    {
        private const int MAX_PENDING_REQUESTS = 16;
        private const int FRAME_READ_TIMEOUT_MS = 1500;
        private readonly object mServerGate = new object();
        private readonly YokiFrameFastChannelRequestQueue mRequestQueue =
            new YokiFrameFastChannelRequestQueue(MAX_PENDING_REQUESTS);
        private readonly string mPipeName;
        private readonly CancellationTokenSource mCancellationSource = new CancellationTokenSource();
        private NamedPipeServerStream mActiveServer;
        private Task mListenerTask;
        private bool mDisposed;
        private bool mStarted;
        private volatile bool mReady;
        private string mLastError = string.Empty;

        /// <summary>
        /// 使用安全且当前进程唯一的 Pipe 名称创建 listener；实际监听在 Start 后的后台任务中建立。
        /// </summary>
        /// <param name="pipeName">不含 Windows Pipe 根前缀的安全 Pipe 名称。</param>
        public YokiFrameEditorNamedPipeFastChannelHost(string pipeName)
        {
            if (!YokiFrameSafeIdContract.IsSafeId(pipeName))
            {
                throw new ArgumentException("FastChannel Pipe name is invalid.", nameof(pipeName));
            }

            mPipeName = pipeName;
        }

        /// <summary>
        /// 获取 listener 当前是否已经成功创建 Pipe 并可安全发布 enabled endpoint。
        /// </summary>
        public bool IsReady => mReady;

        /// <summary>
        /// 获取 registry 中应发布的 Pipe 名称。
        /// </summary>
        public string PipeName => mPipeName;

        /// <summary>
        /// 获取后台 listener 最近捕获的非取消错误，仅供 bridge 状态诊断。
        /// </summary>
        public string LastError => mLastError;

        /// <summary>
        /// 启动单 listener 后台循环；重复调用保持幂等，不会创建第二个同名 Pipe server。
        /// </summary>
        public void Start()
        {
            ThrowIfDisposed();
            if (mStarted)
            {
                return;
            }

            mStarted = true;
            var server = CreateAndRegisterServer();
            mListenerTask = Task.Run(() => ListenAsync(server, mCancellationSource.Token));
        }

        /// <summary>
        /// 由 Unity Editor 主线程执行已排队请求，并完成 listener 等待的 response Task。
        /// </summary>
        /// <param name="responseFactory">主线程根据请求生成终态 response 的回调。</param>
        /// <returns>本次已处理的请求数。</returns>
        public int ProcessPending(Func<YokiFrameFastChannelFrame, YokiFrameFastChannelFrame> responseFactory)
        {
            ThrowIfDisposed();
            return mRequestQueue.ProcessPending(responseFactory);
        }

        /// <summary>
        /// 停止 listener、关闭正在等待连接的 Pipe，并取消尚未进入主线程的请求。
        /// </summary>
        public void Stop()
        {
            if (!mStarted)
            {
                return;
            }

            mStarted = false;
            mReady = false;
            mCancellationSource.Cancel();
            mRequestQueue.Stop();
            DisposeActiveServer();
        }

        /// <summary>
        /// 释放 listener 资源；不等待后台任务，避免 Unity Editor 主线程在 Domain Reload 时阻塞。
        /// </summary>
        public void Dispose()
        {
            if (mDisposed)
            {
                return;
            }

            mDisposed = true;
            Stop();
            mCancellationSource.Dispose();
            mRequestQueue.Dispose();
        }

        /// <summary>
        /// 循环创建单实例 Named Pipe server；每个连接可串行处理多个已经完成握手的命令。
        /// </summary>
        /// <param name="cancellationToken">Stop 触发的 listener 取消令牌。</param>
        /// <returns>listener 退出后的异步任务。</returns>
        private async Task ListenAsync(NamedPipeServerStream server, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await server.WaitForConnectionAsync().ConfigureAwait(false);
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        await ProcessConnectionAsync(server, cancellationToken).ConfigureAwait(false);
                    }
                }
                catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
                {
                }
                catch (IOException) when (cancellationToken.IsCancellationRequested)
                {
                }
                catch (Exception exception)
                {
                    mLastError = exception.Message;
                }
                finally
                {
                    mReady = false;
                    ClearActiveServer(server);
                    server.Dispose();
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                try
                {
                    server = CreateAndRegisterServer();
                }
                catch (Exception exception)
                {
                    mLastError = exception.Message;
                    return;
                }
            }
        }

        /// <summary>
        /// 创建并登记下一轮 Accept 使用的 Pipe server；只有该步骤成功时才将 endpoint 标记为 ready。
        /// </summary>
        /// <returns>已经登记为当前 active server 的 Pipe 实例。</returns>
        private NamedPipeServerStream CreateAndRegisterServer()
        {
            var server = CreateServer();
            SetActiveServer(server);
            mReady = true;
            return server;
        }

        /// <summary>
        /// 读取连接上的 Hello/Command frame，把每个有效请求交给主线程队列并将终态 response 写回同一 Pipe；每个 frame 都有期限，避免静默客户端独占唯一 listener。
        /// </summary>
        /// <param name="server">已经连接的 Named Pipe server。</param>
        /// <param name="cancellationToken">Host 停止取消令牌。</param>
        /// <returns>连接关闭后的异步任务。</returns>
        private async Task ProcessConnectionAsync(NamedPipeServerStream server, CancellationToken cancellationToken)
        {
            var handshakeCompleted = false;
            while (server.IsConnected && !cancellationToken.IsCancellationRequested)
            {
                YokiFrameFastChannelFrame request;
                try
                {
                    request = await ReadFrameWithTimeoutAsync(server, cancellationToken).ConfigureAwait(false);
                }
                catch (EndOfStreamException)
                {
                    return;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                if (!IsExpectedRequestKind(request, handshakeCompleted))
                {
                    await WriteErrorAsync(server, "FastChannelHandshakeRequired", "FastChannel command requires a successful Hello handshake.", cancellationToken)
                        .ConfigureAwait(false);
                    return;
                }

                if (!mRequestQueue.TryEnqueue(request, out var responseTask))
                {
                    await WriteErrorAsync(server, "FastChannelBusy", "FastChannel main-thread request queue is full or stopping.", cancellationToken)
                        .ConfigureAwait(false);
                    return;
                }

                try
                {
                    var response = await responseTask.ConfigureAwait(false);
                    await YokiFrameFastChannelFrameStream.WriteAsync(server, response, cancellationToken).ConfigureAwait(false);
                    if (!handshakeCompleted)
                    {
                        handshakeCompleted = response.MessageKind == YokiFrameFastChannelMessageKind.HelloAck;
                        if (!handshakeCompleted)
                        {
                            return;
                        }
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception)
                {
                    await WriteErrorAsync(server, "FastChannelHostStopping", "FastChannel host could not complete the queued request.", cancellationToken)
                        .ConfigureAwait(false);
                    return;
                }
            }
        }

        /// <summary>
        /// 创建同一用户、异步、单连接的 Windows Named Pipe server。
        /// </summary>
        /// <returns>尚未等待连接的 Pipe server。</returns>
        private NamedPipeServerStream CreateServer()
        {
            return YokiFrameEditorNamedPipeSecurity.CreateServer(mPipeName);
        }

        /// <summary>
        /// 在单帧读取上施加固定期限；超时时让外层释放当前 server 并创建下一轮 listener，避免未发 Hello 的客户端造成永久拒绝服务。
        /// </summary>
        /// <param name="server">已经连接的当前 Pipe server。</param>
        /// <param name="cancellationToken">Host 停止时传入的取消令牌。</param>
        /// <returns>已完成 framing 校验的单个 FastChannel frame。</returns>
        private static async Task<YokiFrameFastChannelFrame> ReadFrameWithTimeoutAsync(
            NamedPipeServerStream server,
            CancellationToken cancellationToken)
        {
            var readTask = YokiFrameFastChannelFrameStream.ReadAsync(server, cancellationToken);
            using (var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                var timeoutTask = Task.Delay(FRAME_READ_TIMEOUT_MS, timeoutSource.Token);
                var completedTask = await Task.WhenAny(readTask, timeoutTask).ConfigureAwait(false);
                if (completedTask == readTask)
                {
                    timeoutSource.Cancel();
                    return await readTask.ConfigureAwait(false);
                }
            }

            if (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException(cancellationToken);
            }

            ObserveTimedOutRead(readTask);
            throw new TimeoutException("FastChannel frame read timed out.");
        }

        /// <summary>
        /// 观察超时后由 server.Dispose 终止的异步读取异常，避免后台任务在 listener 轮换后成为未观察 fault。
        /// </summary>
        /// <param name="readTask">已经因读取期限到期而不再等待的 frame 读取任务。</param>
        private static void ObserveTimedOutRead(Task readTask)
        {
            readTask.ContinueWith(
                static completedTask => { var ignored = completedTask.Exception; },
                TaskContinuationOptions.OnlyOnFaulted);
        }

        /// <summary>
        /// 判断当前 listener 阶段允许的 frame 类型；握手前只接受 Hello，握手后只接受 Command。
        /// </summary>
        /// <param name="request">刚读取到的 frame。</param>
        /// <param name="handshakeCompleted">当前连接是否已经完成身份握手。</param>
        /// <returns>当前阶段允许该 frame 时返回 true。</returns>
        private static bool IsExpectedRequestKind(YokiFrameFastChannelFrame request, bool handshakeCompleted)
        {
            return handshakeCompleted
                ? request.MessageKind == YokiFrameFastChannelMessageKind.Command
                : request.MessageKind == YokiFrameFastChannelMessageKind.Hello;
        }

        /// <summary>
        /// 向当前连接写入固定结构的 Error frame；错误文本为常量，后台线程不调用 Unity JSON API。
        /// </summary>
        /// <param name="server">已连接 Pipe server。</param>
        /// <param name="code">稳定错误码。</param>
        /// <param name="message">面向 Client 的错误说明。</param>
        /// <param name="cancellationToken">写入取消令牌。</param>
        /// <returns>错误 frame 写入完成后的异步任务。</returns>
        private static Task WriteErrorAsync(
            NamedPipeServerStream server,
            string code,
            string message,
            CancellationToken cancellationToken)
        {
            var payloadJson = "{\"code\":\"" + code + "\",\"message\":\"" + message + "\"}";
            return YokiFrameFastChannelFrameStream.WriteAsync(
                server,
                new YokiFrameFastChannelFrame(YokiFrameFastChannelMessageKind.Error, 0, payloadJson),
                cancellationToken);
        }

        /// <summary>
        /// 记录当前可被 Stop 关闭的 server，避免 WaitForConnectionAsync 在 Domain Reload 时悬挂。
        /// </summary>
        /// <param name="server">刚创建的 Pipe server。</param>
        private void SetActiveServer(NamedPipeServerStream server)
        {
            lock (mServerGate)
            {
                mActiveServer = server;
            }
        }

        /// <summary>
        /// 清除已经退出的 server 引用，避免 Stop 误释放下一轮 listener。
        /// </summary>
        /// <param name="server">当前循环刚释放的 Pipe server。</param>
        private void ClearActiveServer(NamedPipeServerStream server)
        {
            lock (mServerGate)
            {
                if (ReferenceEquals(mActiveServer, server))
                {
                    mActiveServer = null;
                }
            }
        }

        /// <summary>
        /// 关闭当前可能正在 Accept 的 Pipe server，触发后台任务从 WaitForConnectionAsync 返回。
        /// </summary>
        private void DisposeActiveServer()
        {
            NamedPipeServerStream server;
            lock (mServerGate)
            {
                server = mActiveServer;
                mActiveServer = null;
            }

            if (server != null)
            {
                server.Dispose();
            }
        }

        /// <summary>
        /// 拒绝在已释放 Host 上启动或主线程 drain 请求。
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (!mDisposed)
            {
                return;
            }

            throw new ObjectDisposedException(nameof(YokiFrameEditorNamedPipeFastChannelHost));
        }
    }
}

#endif
