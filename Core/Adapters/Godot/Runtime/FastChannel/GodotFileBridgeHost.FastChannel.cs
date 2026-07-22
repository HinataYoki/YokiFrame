#if GODOT && TOOLS
using System;
using System.Text;

namespace YokiFrame
{
    /// <summary>
    /// 承载 Godot FileBridge Host 对 FastChannel listener、queue 和 registry endpoint 的生命周期组合。
    /// </summary>
    public sealed partial class GodotFileBridgeHost
    {
        private const int FAST_CHANNEL_QUEUE_CAPACITY = 16;
        private const string SYSTEM_KIT = "System";
        private const string PING_ACTION = "ping";

        private static readonly Encoding sFastChannelUtf8 = new UTF8Encoding(false);
        private YokiFrameFastChannelRequestQueue mFastChannelRequestQueue;
        private GodotFastChannelListener mFastChannelListener;

        /// <summary>
        /// 在 Godot 主线程 drain 后台 listener 入队的只读命令，并通过现有 dispatcher 生成 FastChannel terminal frame。
        /// </summary>
        /// <returns>本帧已由主线程处理的 FastChannel 请求数。</returns>
        public int ProcessPendingFastChannelRequests()
        {
            EnsureRunning();
            return mFastChannelRequestQueue == null
                ? 0
                : mFastChannelRequestQueue.ProcessPending(CreateFastChannelResponse);
        }

        /// <summary>
        /// 先发布 disabled endpoint，再启动本机 listener；bind 完成后的下一次状态刷新才会发布 enabled。
        /// </summary>
        private void StartFastChannel()
        {
            mFastChannelRequestQueue = new(FAST_CHANNEL_QUEUE_CAPACITY);
            mFastChannelListener = new(
                ENGINE_ID,
                mProjectScopeId,
                mSessionId,
                mGeneration,
                mFastChannelRequestQueue);
            WriteEngineRegistry();
            try
            {
                mFastChannelListener.Start();
            }
            catch (Exception exception)
            {
                mLastError = "FastChannel startup failed: " + exception.Message;
                mFastChannelListener.Dispose();
                mFastChannelListener = null;
            }
        }

        /// <summary>
        /// 先取消主线程队列，再关闭 listener，使等待中的后台连接不会在 Host 停止后继续执行命令。
        /// </summary>
        private void StopFastChannel()
        {
            var requestQueue = mFastChannelRequestQueue;
            var listener = mFastChannelListener;
            mFastChannelRequestQueue = null;
            mFastChannelListener = null;
            requestQueue?.Stop();
            listener?.Dispose();
            requestQueue?.Dispose();
            if (listener != null && !string.IsNullOrWhiteSpace(listener.LastError))
            {
                mLastError = listener.LastError;
            }
        }

        /// <summary>
        /// 创建 registry 的强类型 FastChannel endpoint 数组；listener 未就绪时明确发布 disabled 和 FileBridge fallback。
        /// </summary>
        /// <returns>始终包含当前会话 endpoint 的单元素数组。</returns>
        private GodotFastChannelEndpoint[] GetFastChannelEndpoints()
        {
            var endpoint = mFastChannelListener == null
                ? GodotFastChannelEndpoint.Disabled(ENGINE_ID, mSessionId, mGeneration)
                : mFastChannelListener.GetEndpoint();
            return new[] { AddReadOnlyCommands(endpoint) };
        }

        /// <summary>
        /// 将当前 Runtime CommandPolicy 的只读能力写入 FastChannel endpoint。
        /// </summary>
        /// <param name="endpoint">已完成 listener 状态判断的 endpoint。</param>
        /// <returns>包含只读能力声明的 endpoint。</returns>
        private GodotFastChannelEndpoint AddReadOnlyCommands(GodotFastChannelEndpoint endpoint)
        {
            endpoint.ReadOnlyCommands.Add(YokiFrameFastChannelContract.CreateCommandKey(SYSTEM_KIT, PING_ACTION));
            var commands = mKitInteractions.GetCommandDescriptors();
            for (var index = 0; index < commands.Length; index++)
            {
                var command = commands[index];
                if (command.Kind == YokiFrameCommandKind.ReadOnly)
                {
                    endpoint.ReadOnlyCommands.Add(YokiFrameFastChannelContract.CreateCommandKey(
                        command.Kit,
                        command.Action));
                }
            }

            endpoint.ReadOnlyCommands.Sort(StringComparer.Ordinal);
            return endpoint;
        }

        /// <summary>
        /// 在主线程校验 FastChannel Command 信封并复用 FileBridge 的 dispatcher，禁止 listener 后台执行任何 Kit 逻辑。
        /// </summary>
        /// <param name="request">后台 listener 已完成 framing 校验并入队的 Command frame。</param>
        /// <returns>可直接写回当前 FastChannel 连接的终态 frame。</returns>
        private YokiFrameFastChannelFrame CreateFastChannelResponse(YokiFrameFastChannelFrame request)
        {
            if (request.MessageKind != YokiFrameFastChannelMessageKind.Command)
            {
                return CreateFastChannelErrorResponse(
                    "FastChannelCommandKindMismatch",
                    "Godot Runtime main-thread dispatcher only accepts Command frames.",
                    "Reconnect and send a Command frame after a successful handshake.");
            }

            try
            {
                var envelope = GodotFileBridgeJson.Deserialize<GodotCommandEnvelope>(request.PayloadJson);
                ValidateEnvelope(envelope);
                if (!IsFastChannelReadOnlyCommand(envelope.Kit, envelope.Action))
                {
                    return CreateFastChannelErrorResponse(
                        "FastChannelCommandRejected",
                        "The current endpoint does not advertise this command as read-only.",
                        "Use FileBridge for this command.");
                }

                var response = ExecuteCommand(envelope, sFastChannelUtf8.GetByteCount(request.PayloadJson));
                return new YokiFrameFastChannelFrame(
                    YokiFrameFastChannelMessageKind.Response,
                    0,
                    GodotFileBridgeJson.Serialize(response));
            }
            catch (Exception exception)
            {
                mLastError = "FastChannel command processing failed: " + exception.Message;
                return CreateFastChannelErrorResponse(
                    "FastChannelCommandInvalid",
                    "FastChannel command could not be validated: " + exception.Message,
                    "Use a valid current-session read-only command, or use FileBridge fallback.");
            }
        }

        /// <summary>
        /// 判断已通过基础信封校验的命令是否属于当前 CommandPolicy 声明的只读操作。
        /// </summary>
        /// <param name="envelope">已完成协议、engine 和安全标识校验的命令信封。</param>
        /// <returns>当前 descriptor 明确声明为 ReadOnly 时返回 true。</returns>
        private bool IsFastChannelReadOnlyCommand(string kit, string action)
        {
            if (kit == SYSTEM_KIT && action == PING_ACTION)
            {
                return true;
            }

            var commands = mKitInteractions.GetCommandDescriptors();
            for (var index = 0; index < commands.Length; index++)
            {
                var command = commands[index];
                if (command.Kind == YokiFrameCommandKind.ReadOnly
                    && command.Kit == kit
                    && command.Action == action)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 创建未能形成有效 FileBridge 风格 terminal response 时使用的 FastChannel Error frame。
        /// </summary>
        /// <param name="code">稳定错误码。</param>
        /// <param name="message">错误说明。</param>
        /// <param name="suggestion">恢复建议。</param>
        /// <returns>当前连接可读取的 Error frame。</returns>
        private static YokiFrameFastChannelFrame CreateFastChannelErrorResponse(
            string code,
            string message,
            string suggestion)
        {
            GodotFastChannelError error = new()
            {
                Code = code,
                Message = message,
                Suggestion = suggestion
            };
            return new YokiFrameFastChannelFrame(
                YokiFrameFastChannelMessageKind.Error,
                0,
                GodotFileBridgeJson.Serialize(error));
        }
    }
}
#endif
