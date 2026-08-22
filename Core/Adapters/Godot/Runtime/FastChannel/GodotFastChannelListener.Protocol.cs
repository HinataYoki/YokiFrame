#if GODOT && TOOLS
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace YokiFrame
{
    /// <summary>
    /// 承载 Godot FastChannel listener 的帧握手和后台入队逻辑，不调用宿主 dispatcher。
    /// </summary>
    internal sealed partial class GodotFastChannelListener
    {
        /// <summary>
        /// 完成单条传输连接的 Hello/HelloAck，并把后续 Command frame 交给主线程队列等待响应。
        /// </summary>
        /// <param name="stream">已连接的 Pipe 或 Unix socket 双向流。</param>
        /// <param name="cancellationToken">Host 停止令牌。</param>
        /// <returns>连接关闭后的异步任务。</returns>
        private async Task ProcessConnectionAsync(Stream stream, CancellationToken cancellationToken)
        {
            try
            {
                var hello = await ReadFrameWithTimeoutAsync(stream, cancellationToken).ConfigureAwait(false);
                if (!TryValidateHello(hello, out var error))
                {
                    await WriteErrorAsync(stream, error, cancellationToken).ConfigureAwait(false);
                    return;
                }

                await YokiFrameFastChannelFrameStream.WriteAsync(
                    stream,
                    CreateHelloAcknowledgement(),
                    cancellationToken).ConfigureAwait(false);
                await ProcessCommandFramesAsync(stream, cancellationToken).ConfigureAwait(false);
            }
            catch (EndOfStreamException)
            {
                // 客户端正常关闭连接，不计入 Host 故障或协议错误。
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (TimeoutException exception)
            {
                RecordConnectionFailure("FastChannel connection timed out: " + exception.Message);
            }
            catch (Exception exception)
            {
                RecordConnectionFailure("FastChannel connection failed: " + exception.Message);
                await TryWriteUnexpectedErrorAsync(stream, exception, cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// 连续读取 Command frame；每个请求都只入队，并等待 Godot 主线程生成终态响应。
        /// </summary>
        /// <param name="stream">已经完成握手的双向流。</param>
        /// <param name="cancellationToken">Host 停止令牌。</param>
        /// <returns>连接关闭后的异步任务。</returns>
        private async Task ProcessCommandFramesAsync(Stream stream, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var request = await ReadFrameWithTimeoutAsync(stream, cancellationToken).ConfigureAwait(false);
                if (request.MessageKind != YokiFrameFastChannelMessageKind.Command)
                {
                    await WriteErrorAsync(
                        stream,
                        CreateError(
                            "FastChannelCommandKindMismatch",
                            "FastChannel connection expects Command frames after HelloAck.",
                            "Reconnect and send a read-only command frame after completing the handshake."),
                        cancellationToken).ConfigureAwait(false);
                    return;
                }

                if (!mRequestQueue.TryEnqueue(request, cancellationToken, out Task<YokiFrameFastChannelFrame> responseTask))
                {
                    await WriteErrorAsync(
                        stream,
                        CreateError(
                            "FastChannelQueueUnavailable",
                            "Godot FastChannel request queue is stopped or full.",
                            "Retry after the engine becomes responsive, or use FileBridge fallback."),
                        cancellationToken).ConfigureAwait(false);
                    return;
                }

                var response = await responseTask.WaitAsync(cancellationToken).ConfigureAwait(false);
                await YokiFrameFastChannelFrameStream.WriteAsync(stream, response, cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// 为单个 Hello 或 Command frame 提供固定读取期限；超时只关闭当前连接，外层 accept 循环会继续服务后续客户端。
        /// </summary>
        /// <param name="stream">已连接的当前 Pipe 或 Unix socket 流。</param>
        /// <param name="cancellationToken">Host 停止时传入的取消令牌。</param>
        /// <returns>已通过 Core framing 校验的单个 frame。</returns>
        private static async Task<YokiFrameFastChannelFrame> ReadFrameWithTimeoutAsync(
            Stream stream,
            CancellationToken cancellationToken)
        {
            var readTask = YokiFrameFastChannelFrameStream.ReadAsync(stream, cancellationToken);
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
        /// 观察由外层连接释放终止的超时读取任务，避免后台 protocol fault 未被观察。
        /// </summary>
        /// <param name="readTask">达到读取期限且不再等待的 frame 读取任务。</param>
        private static void ObserveTimedOutRead(Task readTask)
        {
            readTask.ContinueWith(
                static completedTask => { var ignored = completedTask.Exception; },
                TaskContinuationOptions.OnlyOnFaulted);
        }

        /// <summary>
        /// 验证 Client Hello 的消息类型、身份 SafeId 和 engine/session/generation 三项会话一致性。
        /// 校验逻辑统一委托给跨宿主共享的 <see cref="YokiFrameFastChannelHostHandshake"/>。
        /// </summary>
        /// <param name="hello">已完成 Core framing 校验的首帧。</param>
        /// <param name="error">失败时返回给 Client 的稳定错误。</param>
        /// <returns>当前连接可以继续握手时返回 true。</returns>
        private bool TryValidateHello(
            YokiFrameFastChannelFrame hello,
            out GodotFastChannelError error)
        {
            if (!YokiFrameFastChannelHostHandshake.TryValidateHello(
                    hello,
                    mEngineId,
                    mSessionId,
                    mGeneration,
                    out var errorCode,
                    out var errorMessage))
            {
                error = CreateError(
                    errorCode,
                    errorMessage,
                    "Refresh engine registry and reconnect using the current endpoint.");
                return false;
            }

            error = null;
            return true;
        }

        /// <summary>
        /// 创建携带当前 Host 三项身份的 HelloAck frame，供工具侧强制比较生命周期。
        /// </summary>
        /// <returns>当前会话 HelloAck。</returns>
        private YokiFrameFastChannelFrame CreateHelloAcknowledgement()
        {
            GodotFastChannelSessionIdentity identity = new()
            {
                EngineId = mEngineId,
                SessionId = mSessionId,
                Generation = mGeneration
            };
            return new YokiFrameFastChannelFrame(
                YokiFrameFastChannelMessageKind.HelloAck,
                0,
                GodotFileBridgeJson.Serialize(identity));
        }

        /// <summary>
        /// 将标准错误模型写入 Error frame；写入失败交由外层连接生命周期结束处理。
        /// </summary>
        /// <param name="stream">当前连接流。</param>
        /// <param name="error">可由工具侧诊断的错误信息。</param>
        /// <param name="cancellationToken">Host 停止令牌。</param>
        /// <returns>写入完成后的异步任务。</returns>
        private static Task WriteErrorAsync(
            Stream stream,
            GodotFastChannelError error,
            CancellationToken cancellationToken)
        {
            return YokiFrameFastChannelFrameStream.WriteAsync(
                stream,
                new YokiFrameFastChannelFrame(
                    YokiFrameFastChannelMessageKind.Error,
                    0,
                    GodotFileBridgeJson.Serialize(error)),
                cancellationToken);
        }

        /// <summary>
        /// 尽力把未预期连接异常反馈给 Client；连接已断开时仅保留 listener 诊断。
        /// </summary>
        /// <param name="stream">当前连接流。</param>
        /// <param name="exception">未预期异常。</param>
        /// <param name="cancellationToken">Host 停止令牌。</param>
        /// <returns>错误反馈结束后的异步任务。</returns>
        private async Task TryWriteUnexpectedErrorAsync(
            Stream stream,
            Exception exception,
            CancellationToken cancellationToken)
        {
            try
            {
                await WriteErrorAsync(
                    stream,
                    CreateError(
                        "FastChannelConnectionFailed",
                        "FastChannel connection failed: " + exception.Message,
                        "Reconnect using the current registry endpoint or use FileBridge fallback."),
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception writeException)
            {
                RecordConnectionFailure("FastChannel error response failed: " + writeException.Message);
            }
        }

        /// <summary>
        /// 创建 FastChannel Error frame 使用的稳定错误模型。
        /// </summary>
        /// <param name="code">稳定错误码。</param>
        /// <param name="message">错误说明。</param>
        /// <param name="suggestion">回退或恢复建议。</param>
        /// <returns>错误 JSON 模型。</returns>
        private static GodotFastChannelError CreateError(string code, string message, string suggestion)
        {
            return new GodotFastChannelError
            {
                Code = code,
                Message = message,
                Suggestion = suggestion
            };
        }
    }
}
#endif
